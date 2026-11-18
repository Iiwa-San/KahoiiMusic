import type { Track, OfflineTrackItem } from "../types";

const DB_NAME = "KahoiiMusicOfflineDB";
const DB_VERSION = 1;
const STORE_NAME = "offline_tracks";

/**
 * Open (or initialize) the IndexedDB database for offline tracks.
 */
export function openDB(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof window === "undefined" || !window.indexedDB) {
      return reject(new Error("IndexedDB không được hỗ trợ trên trình duyệt này."));
    }

    const request = window.indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        const store = db.createObjectStore(STORE_NAME, { keyPath: "id" });
        store.createIndex("savedAt", "savedAt", { unique: false });
        store.createIndex("title", "title", { unique: false });
      }
    };

    request.onsuccess = () => {
      resolve(request.result);
    };

    request.onerror = () => {
      reject(request.error || new Error("Không thể mở IndexedDB."));
    };
  });
}

/**
 * Save a track, its audio binary Blob, and optional lyrics into IndexedDB.
 */
export async function saveTrackOffline(
  track: Track,
  audioBlob: Blob,
  lyrics?: any
): Promise<OfflineTrackItem> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction([STORE_NAME], "readwrite");
    const store = transaction.objectStore(STORE_NAME);

    const offlineItem: OfflineTrackItem = {
      id: track.id,
      title: track.title,
      author: track.author || "Kahoii Music",
      duration: track.duration || 0,
      durationText: track.durationText || "",
      thumbnail: track.thumbnail || "",
      savedAt: Date.now(),
      sizeBytes: audioBlob.size,
      audioBlob: audioBlob,
      lyrics: lyrics || null,
    };

    const request = store.put(offlineItem);

    request.onsuccess = () => {
      resolve(offlineItem);
    };

    request.onerror = () => {
      reject(request.error || new Error(`Lưu bài hát "${track.title}" thất bại.`));
    };

    transaction.oncomplete = () => {
      db.close();
    };
  });
}

/**
 * Retrieve a specific track from IndexedDB by its ID.
 */
export async function getOfflineTrack(id: string): Promise<OfflineTrackItem | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction([STORE_NAME], "readonly");
    const store = transaction.objectStore(STORE_NAME);
    const request = store.get(id);

    request.onsuccess = () => {
      resolve(request.result || null);
    };

    request.onerror = () => {
      reject(request.error || new Error(`Không thể tìm bài hát offline ID: ${id}`));
    };

    transaction.oncomplete = () => {
      db.close();
    };
  });
}

/**
 * Check if a track is already saved in IndexedDB.
 */
export async function isOfflineTrackSaved(id: string): Promise<boolean> {
  try {
    const item = await getOfflineTrack(id);
    return item !== null;
  } catch {
    return false;
  }
}

/**
 * Retrieve all offline tracks stored in IndexedDB, sorted by newest saved first.
 */
export async function getAllOfflineTracks(): Promise<OfflineTrackItem[]> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction([STORE_NAME], "readonly");
    const store = transaction.objectStore(STORE_NAME);
    const request = store.getAll();

    request.onsuccess = () => {
      const items: OfflineTrackItem[] = request.result || [];
      // Sort newest first
      items.sort((a, b) => b.savedAt - a.savedAt);
      resolve(items);
    };

    request.onerror = () => {
      reject(request.error || new Error("Không thể tải danh sách bài hát offline."));
    };

    transaction.oncomplete = () => {
      db.close();
    };
  });
}

/**
 * Delete a track from IndexedDB by ID.
 */
export async function deleteOfflineTrack(id: string): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction([STORE_NAME], "readwrite");
    const store = transaction.objectStore(STORE_NAME);
    const request = store.delete(id);

    request.onsuccess = () => {
      resolve();
    };

    request.onerror = () => {
      reject(request.error || new Error(`Không thể xóa bài hát ID: ${id}`));
    };

    transaction.oncomplete = () => {
      db.close();
    };
  });
}

/**
 * Get total count and storage usage in bytes.
 */
export async function getOfflineStorageUsage(): Promise<{ count: number; totalBytes: number }> {
  try {
    const tracks = await getAllOfflineTracks();
    const totalBytes = tracks.reduce((sum, item) => sum + (item.sizeBytes || item.audioBlob?.size || 0), 0);
    return {
      count: tracks.length,
      totalBytes,
    };
  } catch {
    return { count: 0, totalBytes: 0 };
  }
}

/**
 * Human-readable byte formatting (e.g. 4.2 MB)
 */
export function formatBytes(bytes: number): string {
  if (!bytes || bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}
