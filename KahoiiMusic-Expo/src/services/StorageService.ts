import AsyncStorage from '@react-native-async-storage/async-storage';
import { PlaylistItem, AppSettings } from '../types/music';

const KEYS = {
  HISTORY: '@kahoii_history',
  FAVORITES: '@kahoii_favorites',
  QUEUE: '@kahoii_queue',
  SETTINGS: '@kahoii_settings',
  LAST_TRACK: '@kahoii_last_track',
};

const DEFAULT_SETTINGS: AppSettings = {
  autoPlayNext: true,
  highQualityAudio: true,
  keepHistoryCount: 100,
  showArtworkInBackground: true,
};

export class StorageService {
  // History
  static async getHistory(): Promise<PlaylistItem[]> {
    try {
      const data = await AsyncStorage.getItem(KEYS.HISTORY);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  static async recordPlay(track: PlaylistItem, maxCount: number = 100): Promise<PlaylistItem[]> {
    try {
      const current = await this.getHistory();
      const updated = [
        { ...track, addedAt: Date.now() },
        ...current.filter((item) => item.id !== track.id),
      ].slice(0, maxCount);
      await AsyncStorage.setItem(KEYS.HISTORY, JSON.stringify(updated));
      return updated;
    } catch {
      return [];
    }
  }

  static async removeHistoryItem(id: string): Promise<PlaylistItem[]> {
    try {
      const current = await this.getHistory();
      const updated = current.filter((item) => item.id !== id);
      await AsyncStorage.setItem(KEYS.HISTORY, JSON.stringify(updated));
      return updated;
    } catch {
      return [];
    }
  }

  static async clearHistory(): Promise<void> {
    try {
      await AsyncStorage.removeItem(KEYS.HISTORY);
    } catch (e) {
      console.error(e);
    }
  }

  static async saveHistory(history: PlaylistItem[]): Promise<void> {
    try {
      await AsyncStorage.setItem(KEYS.HISTORY, JSON.stringify(history));
    } catch (e) {
      console.error(e);
    }
  }

  // Favorites
  static async getFavorites(): Promise<PlaylistItem[]> {
    try {
      const data = await AsyncStorage.getItem(KEYS.FAVORITES);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  static async saveFavorites(favorites: PlaylistItem[]): Promise<void> {
    try {
      await AsyncStorage.setItem(KEYS.FAVORITES, JSON.stringify(favorites));
    } catch (e) {
      console.error(e);
    }
  }

  static async toggleFavorite(track: PlaylistItem): Promise<{ favorites: PlaylistItem[]; isFav: boolean }> {
    try {
      const current = await this.getFavorites();
      const index = current.findIndex((item) => item.id === track.id);
      let updated: PlaylistItem[];
      let isFav: boolean;

      if (index >= 0) {
        updated = current.filter((item) => item.id !== track.id);
        isFav = false;
      } else {
        updated = [{ ...track, isFavorite: true, addedAt: Date.now() }, ...current];
        isFav = true;
      }

      await AsyncStorage.setItem(KEYS.FAVORITES, JSON.stringify(updated));
      return { favorites: updated, isFav };
    } catch {
      return { favorites: [], isFav: false };
    }
  }

  // Queue
  static async getSavedQueue(): Promise<PlaylistItem[]> {
    try {
      const data = await AsyncStorage.getItem(KEYS.QUEUE);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  static async saveQueue(queue: PlaylistItem[]): Promise<void> {
    try {
      await AsyncStorage.setItem(KEYS.QUEUE, JSON.stringify(queue));
    } catch (e) {
      console.error(e);
    }
  }

  // Settings
  static async getSettings(): Promise<AppSettings> {
    try {
      const data = await AsyncStorage.getItem(KEYS.SETTINGS);
      return data ? { ...DEFAULT_SETTINGS, ...JSON.parse(data) } : DEFAULT_SETTINGS;
    } catch {
      return DEFAULT_SETTINGS;
    }
  }

  static async saveSettings(settings: AppSettings): Promise<void> {
    try {
      await AsyncStorage.setItem(KEYS.SETTINGS, JSON.stringify(settings));
    } catch (e) {
      console.error(e);
    }
  }
}

