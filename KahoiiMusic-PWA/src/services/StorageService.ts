import { PlaylistItem, AppSettings } from '../types/music';

const QUEUE_KEY = 'kahoii_queue';
const FAVORITES_KEY = 'kahoii_favorites';
const HISTORY_KEY = 'kahoii_history';
const SETTINGS_KEY = 'kahoii_settings';

export const DEFAULT_SETTINGS: AppSettings = {
  autoPlayNext: true,
  highQualityAudio: true,
  keepHistoryCount: 50,
  showArtworkInBackground: true,
};

export class StorageService {
  public static loadQueue(): PlaylistItem[] {
    try {
      const data = localStorage.getItem(QUEUE_KEY);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  public static saveQueue(items: PlaylistItem[]): void {
    try {
      localStorage.setItem(QUEUE_KEY, JSON.stringify(items));
    } catch {
      // non-fatal
    }
  }

  public static loadFavorites(): PlaylistItem[] {
    try {
      const data = localStorage.getItem(FAVORITES_KEY);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  public static saveFavorites(items: PlaylistItem[]): void {
    try {
      localStorage.setItem(FAVORITES_KEY, JSON.stringify(items));
    } catch {
      // non-fatal
    }
  }

  public static loadHistory(): PlaylistItem[] {
    try {
      const data = localStorage.getItem(HISTORY_KEY);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  }

  public static recordPlay(track: PlaylistItem, maxCount = 50): PlaylistItem[] {
    try {
      let history = this.loadHistory();
      history = history.filter((t) => t.id !== track.id);
      history.unshift({ ...track, addedAt: Date.now() });
      if (history.length > maxCount) {
        history = history.slice(0, maxCount);
      }
      localStorage.setItem(HISTORY_KEY, JSON.stringify(history));
      return history;
    } catch {
      return [];
    }
  }

  public static clearHistory(): void {
    try {
      localStorage.removeItem(HISTORY_KEY);
    } catch {
      // non-fatal
    }
  }

  public static loadSettings(): AppSettings {
    try {
      const data = localStorage.getItem(SETTINGS_KEY);
      return data ? { ...DEFAULT_SETTINGS, ...JSON.parse(data) } : DEFAULT_SETTINGS;
    } catch {
      return DEFAULT_SETTINGS;
    }
  }

  public static saveSettings(settings: AppSettings): void {
    try {
      localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
    } catch {
      // non-fatal
    }
  }
}

