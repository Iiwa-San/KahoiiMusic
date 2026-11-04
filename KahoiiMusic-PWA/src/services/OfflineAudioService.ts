import { PlaylistItem } from '../types/music';

const DB_NAME = 'kahoii_offline_db';
const DB_VERSION = 1;
const STORE_NAME = 'offline_tracks';

export interface OfflineRecord {
  id: string;
  track: PlaylistItem;
  audioBlob: Blob;
  thumbBlob?: Blob;
  savedAt: number;
}

export class OfflineAudioService {
  private static instance: OfflineAudioService;
  private dbPromise: Promise<IDBDatabase> | null = null;
  private activeBlobUrls: Map<string, string> = new Map();
  private listeners: Set<() => void> = new Set();

  public static getInstance(): OfflineAudioService {
    if (!OfflineAudioService.instance) {
      OfflineAudioService.instance = new OfflineAudioService();
    }
    return OfflineAudioService.instance;
  }

  private getDB(): Promise<IDBDatabase> {
    if (this.dbPromise) return this.dbPromise;

    this.dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          db.createObjectStore(STORE_NAME, { keyPath: 'id' });
        }
      };

      request.onsuccess = () => {
        resolve(request.result);
      };

      request.onerror = () => {
        reject(request.error);
      };
    });

    return this.dbPromise;
  }

  public subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    this.listeners.forEach((fn) => fn());
  }

  // Check if a track is saved offline in IndexedDB
  public async isSaved(id: string): Promise<boolean> {
    try {
      const db = await this.getDB();
      return new Promise((resolve) => {
        const tx = db.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.get(id);
        req.onsuccess = () => resolve(!!req.result);
        req.onerror = () => resolve(false);
      });
    } catch {
      return false;
    }
  }

  // Get offline Blob Object URL for HTML5 <audio>
  public async getOfflineAudioUrl(id: string): Promise<string | null> {
    if (this.activeBlobUrls.has(id)) {
      return this.activeBlobUrls.get(id)!;
    }

    try {
      const db = await this.getDB();
      return new Promise((resolve) => {
        const tx = db.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.get(id);

        req.onsuccess = () => {
          const record: OfflineRecord | undefined = req.result;
          if (record && record.audioBlob) {
            const url = URL.createObjectURL(record.audioBlob);
            this.activeBlobUrls.set(id, url);
            resolve(url);
          } else {
            resolve(null);
          }
        };

        req.onerror = () => resolve(null);
      });
    } catch {
      return null;
    }
  }

  // Download and save track audio file + thumbnail into IndexedDB
  public async saveTrackOffline(
    track: PlaylistItem,
    streamUrl: string,
    onProgress?: (percent: number) => void
  ): Promise<boolean> {
    try {
      if (await this.isSaved(track.id)) return true;

      // 1. Fetch audio binary blob
      const res = await fetch(streamUrl);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      // Support progress if content-length exists
      const contentLength = res.headers.get('content-length');
      let audioBlob: Blob;

      if (contentLength && res.body && onProgress) {
        const total = parseInt(contentLength, 10);
        let loaded = 0;
        const reader = res.body.getReader();
        const chunks: Uint8Array[] = [];

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          if (value) {
            chunks.push(value);
            loaded += value.length;
            onProgress(Math.round((loaded / total) * 100));
          }
        }
        audioBlob = new Blob(chunks as BlobPart[], { type: 'audio/mp4' });
      } else {
        audioBlob = await res.blob();
      }

      // 2. Fetch thumbnail blob
      let thumbBlob: Blob | undefined;
      try {
        const thumbUrl = track.thumbnailUrl || `https://i.ytimg.com/vi/${track.id}/hqdefault.jpg`;
        const tRes = await fetch(thumbUrl);
        if (tRes.ok) thumbBlob = await tRes.blob();
      } catch {
        // non-fatal
      }

      // 3. Store record in IndexedDB
      const record: OfflineRecord = {
        id: track.id,
        track,
        audioBlob,
        thumbBlob,
        savedAt: Date.now(),
      };

      const db = await this.getDB();
      await new Promise<void>((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const req = store.put(record);
        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
      });

      this.notify();
      return true;
    } catch (e) {
      console.error('[OfflineAudioService] Save failed:', e);
      return false;
    }
  }

  // Delete track from IndexedDB
  public async deleteTrack(id: string): Promise<void> {
    try {
      const activeUrl = this.activeBlobUrls.get(id);
      if (activeUrl) {
        URL.revokeObjectURL(activeUrl);
        this.activeBlobUrls.delete(id);
      }

      const db = await this.getDB();
      await new Promise<void>((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const req = store.delete(id);
        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
      });

      this.notify();
    } catch (e) {
      console.warn('[OfflineAudioService] Delete error:', e);
    }
  }

  // Get all saved offline tracks
  public async getAllOfflineTracks(): Promise<PlaylistItem[]> {
    try {
      const db = await this.getDB();
      return new Promise((resolve) => {
        const tx = db.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.getAll();

        req.onsuccess = () => {
          const records: OfflineRecord[] = req.result || [];
          records.sort((a, b) => b.savedAt - a.savedAt);
          resolve(records.map((r) => r.track));
        };

        req.onerror = () => resolve([]);
      });
    } catch {
      return [];
    }
  }

  // Get storage usage estimation
  public async getStorageEstimate(): Promise<{ usedMB: number; quotaMB: number }> {
    if ('storage' in navigator && 'estimate' in navigator.storage) {
      try {
        const estimate = await navigator.storage.estimate();
        const usedMB = Math.round((estimate.usage || 0) / (1024 * 1024));
        const quotaMB = Math.round((estimate.quota || 0) / (1024 * 1024));
        return { usedMB, quotaMB };
      } catch {
        // ignore
      }
    }
    return { usedMB: 0, quotaMB: 0 };
  }
}

