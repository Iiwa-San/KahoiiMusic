import AsyncStorage from '@react-native-async-storage/async-storage';
import { UserPlaylist, PlaylistItem } from '../types/music';

const COLLECTIONS_KEY = '@kahoii_collections';

export class PlaylistCollectionService {
  private static instance: PlaylistCollectionService;
  public collections: UserPlaylist[] = [];
  private listeners: Set<() => void> = new Set();

  public static getInstance(): PlaylistCollectionService {
    if (!PlaylistCollectionService.instance) {
      PlaylistCollectionService.instance = new PlaylistCollectionService();
    }
    return PlaylistCollectionService.instance;
  }

  private constructor() {
    this.loadCollections();
  }

  public async loadCollections(): Promise<UserPlaylist[]> {
    try {
      const json = await AsyncStorage.getItem(COLLECTIONS_KEY);
      if (json) {
        this.collections = JSON.parse(json);
        this.notify();
      }
    } catch (e) {
      console.warn('Load collections error:', e);
    }
    return this.collections;
  }

  public async setCollections(cols: UserPlaylist[]): Promise<void> {
    this.collections = cols;
    await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(cols));
    this.notify();
  }

  public async createPlaylist(name: string): Promise<UserPlaylist> {
    const trimmed = name.trim();
    const newPl: UserPlaylist = {
      id: `${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      name: trimmed || 'Bộ sưu tập mới',
      createdAt: Date.now(),
      items: [],
    };
    this.collections.unshift(newPl);
    await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(this.collections));
    this.notify();
    return newPl;
  }

  public async deletePlaylist(id: string): Promise<void> {
    this.collections = this.collections.filter((p) => p.id !== id);
    await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(this.collections));
    this.notify();
  }

  public async renamePlaylist(id: string, newName: string): Promise<void> {
    const trimmed = newName.trim();
    if (!trimmed) return;
    const idx = this.collections.findIndex((p) => p.id === id);
    if (idx >= 0) {
      this.collections[idx].name = trimmed;
      await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(this.collections));
      this.notify();
    }
  }

  public async addTrack(playlistId: string, track: PlaylistItem): Promise<boolean> {
    const idx = this.collections.findIndex((p) => p.id === playlistId);
    if (idx >= 0) {
      if (!this.collections[idx].items.some((item) => item.id === track.id)) {
        this.collections[idx].items.unshift(track);
        await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(this.collections));
        this.notify();
        return true;
      }
    }
    return false;
  }

  public async removeTrack(playlistId: string, trackId: string): Promise<void> {
    const idx = this.collections.findIndex((p) => p.id === playlistId);
    if (idx >= 0) {
      this.collections[idx].items = this.collections[idx].items.filter((item) => item.id !== trackId);
      await AsyncStorage.setItem(COLLECTIONS_KEY, JSON.stringify(this.collections));
      this.notify();
    }
  }

  public async clearAll(): Promise<void> {
    this.collections = [];
    await AsyncStorage.removeItem(COLLECTIONS_KEY);
    this.notify();
  }

  public subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify() {
    this.listeners.forEach((l) => l());
  }
}

