import { CloudMusicPayload, PlaylistItem, UserPlaylist } from '../types/music';
import { FirebaseAuthService } from './FirebaseAuthService';

const PROJECT_ID = 'kahoiimusic';
const API_KEY = 'AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8';

export function normalizePlaylistItem(raw: any): PlaylistItem {
  if (!raw) {
    return {
      id: '',
      title: '',
      author: '',
      durationSeconds: 0,
      thumbnailUrl: '',
      addedAt: Date.now(),
    };
  }
  const id = String(raw.id || raw.Id || '');
  const title = String(raw.title || raw.Title || '');
  const author = String(raw.author || raw.artist || raw.Artist || 'YouTube');
  const durationSeconds = Number(raw.durationSeconds ?? raw.DurationSeconds ?? 0);

  const thumb = raw.thumbnailUrl || raw.ThumbnailUrl || '';
  const thumbnailUrl =
    thumb && String(thumb).trim().length > 0
      ? String(thumb)
      : id
      ? `https://i.ytimg.com/vi/${id}/hqdefault.jpg`
      : '';

  const streamUrl = raw.streamUrl || raw.localAudioPath || raw.LocalAudioPath || undefined;

  let addedAt = Date.now();
  const rawAdded = raw.addedAt ?? raw.AddedAt;
  if (typeof rawAdded === 'number') {
    addedAt = rawAdded > 100_000_000_000 ? rawAdded : rawAdded * 1000;
  } else if (typeof rawAdded === 'string') {
    const parsed = Date.parse(rawAdded);
    if (!isNaN(parsed)) addedAt = parsed;
  }

  const isFavorite = Boolean(raw.isFavorite ?? raw.IsFavorite);

  return {
    id,
    title,
    author,
    durationSeconds,
    thumbnailUrl,
    streamUrl,
    addedAt,
    isFavorite,
  };
}

export function normalizeUserPlaylist(raw: any): UserPlaylist {
  if (!raw) {
    return {
      id: Date.now().toString(),
      name: 'Bộ sưu tập',
      createdAt: Date.now(),
      items: [],
    };
  }
  const id = String(raw.id || raw.Id || Date.now().toString());
  const name = String(raw.name || raw.Name || 'Bộ sưu tập');

  let createdAt = Date.now();
  const rawCreated = raw.createdAt ?? raw.CreatedAt ?? raw.created ?? raw.Created;
  if (typeof rawCreated === 'number') {
    createdAt = rawCreated > 100_000_000_000 ? rawCreated : rawCreated * 1000;
  } else if (typeof rawCreated === 'string') {
    // Handle Microsoft /Date(1793680102703)/ format from C# PC app
    const msMatch = rawCreated.match(/\/Date\((\d+)\)\//);
    if (msMatch) {
      createdAt = parseInt(msMatch[1], 10);
    } else {
      const parsed = Date.parse(rawCreated);
      if (!isNaN(parsed)) createdAt = parsed;
    }
  }

  const rawItems = raw.items || raw.Items || [];
  const items = Array.isArray(rawItems) ? rawItems.map(normalizePlaylistItem) : [];

  return {
    id,
    name,
    createdAt,
    items,
  };
}

export function normalizeCloudPayload(raw: any): CloudMusicPayload {
  if (!raw) {
    return {
      playlists: [],
      savedTracks: [],
      history: [],
      favorites: [],
      collections: [],
      updatedAt: new Date().toISOString(),
    };
  }

  const rawPlaylists = raw.playlists || raw.Playlists || [];
  const rawSaved = raw.savedTracks || raw.SavedTracks || [];
  const rawHistory = raw.history || raw.History || [];
  const rawFavorites = raw.favorites || raw.Favorites || [];
  const rawCollections = raw.collections || raw.Collections || [];
  const updatedAt = raw.updatedAt || raw.UpdatedAt || new Date().toISOString();

  return {
    playlists: Array.isArray(rawPlaylists) ? rawPlaylists.map(normalizePlaylistItem) : [],
    savedTracks: Array.isArray(rawSaved) ? rawSaved.map(normalizePlaylistItem) : [],
    history: Array.isArray(rawHistory) ? rawHistory.map(normalizePlaylistItem) : [],
    favorites: Array.isArray(rawFavorites) ? rawFavorites.map(normalizePlaylistItem) : [],
    collections: Array.isArray(rawCollections) ? rawCollections.map(normalizeUserPlaylist) : [],
    updatedAt,
  };
}

export class FirebaseSyncService {
  private static instance: FirebaseSyncService;
  public isSyncing: boolean = false;
  public syncStatusMessage: string = '';
  private listeners: Set<() => void> = new Set();
  private autoUploadTimer: any = null;

  public static getInstance(): FirebaseSyncService {
    if (!FirebaseSyncService.instance) {
      FirebaseSyncService.instance = new FirebaseSyncService();
    }
    return FirebaseSyncService.instance;
  }

  public async syncFetch(userId: string, idToken: string): Promise<CloudMusicPayload | null> {
    this.isSyncing = true;
    this.syncStatusMessage = 'Đang tải dữ liệu đám mây...';
    this.notify();

    try {
      const endpoint = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${userId}?key=${API_KEY}`;
      const res = await fetch(endpoint, {
        method: 'GET',
        headers: {
          Authorization: `Bearer ${idToken}`,
        },
      });

      if (!res.ok) {
        this.syncStatusMessage = 'Chưa có dữ liệu đồng bộ đám mây';
        return null;
      }

      const json = await res.json();
      const stringValue = json?.fields?.musicDataJson?.stringValue;
      if (!stringValue) {
        this.syncStatusMessage = 'Dữ liệu đám mây rỗng';
        return null;
      }

      const parsed = JSON.parse(stringValue);
      const payload = normalizeCloudPayload(parsed);
      this.syncStatusMessage = 'Đồng bộ đám mây thành công';
      return payload;
    } catch (e: any) {
      this.syncStatusMessage = `Lỗi tải đồng bộ: ${e.message}`;
      return null;
    } finally {
      this.isSyncing = false;
      this.notify();
    }
  }

  public async syncUpload(
    userId: string,
    idToken: string,
    payload: CloudMusicPayload
  ): Promise<boolean> {
    this.isSyncing = true;
    this.syncStatusMessage = 'Đang lưu lên đám mây...';
    this.notify();

    try {
      const endpoint = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${userId}?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt&key=${API_KEY}`;
      const jsonString = JSON.stringify(payload);

      const firestoreBody = {
        fields: {
          musicDataJson: {
            stringValue: jsonString,
          },
          updatedAt: {
            stringValue: payload.updatedAt,
          },
        },
      };

      const res = await fetch(endpoint, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${idToken}`,
        },
        body: JSON.stringify(firestoreBody),
      });

      if (res.ok) {
        this.syncStatusMessage = 'Đã lưu lên đám mây thành công';
        return true;
      } else {
        this.syncStatusMessage = 'Lưu đám mây không thành công';
        return false;
      }
    } catch (e: any) {
      this.syncStatusMessage = `Lỗi lưu đám mây: ${e.message}`;
      return false;
    } finally {
      this.isSyncing = false;
      this.notify();
    }
  }

  public requestAutoUpload(payloadProvider: () => CloudMusicPayload) {
    const authService = FirebaseAuthService.getInstance();
    const user = authService.currentUser;
    if (!user) return;

    if (this.autoUploadTimer) {
      clearTimeout(this.autoUploadTimer);
    }

    this.autoUploadTimer = setTimeout(async () => {
      try {
        const payload = payloadProvider();
        await this.syncUpload(user.localId, user.idToken, payload);
      } catch (err) {
        console.warn('[FirebaseSyncService] autoUpload failed:', err);
      }
    }, 2500);
  }

  public subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify() {
    this.listeners.forEach((l) => l());
  }
}
