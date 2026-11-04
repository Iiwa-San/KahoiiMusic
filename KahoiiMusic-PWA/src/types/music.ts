export interface PlaylistItem {
  id: string;
  title: string;
  author: string;
  durationSeconds: number;
  thumbnailUrl: string;
  streamUrl?: string;
  addedAt?: number;
  isFavorite?: boolean;
}

export type PlaybackStatusType = 'idle' | 'loading' | 'buffering' | 'playing' | 'paused' | 'error';
export type LoopMode = 'off' | 'all' | 'one';

export interface GradientPalette {
  primary: string;
  secondary: string;
  background: string;
}

export interface AppSettings {
  autoPlayNext: boolean;
  highQualityAudio: boolean;
  keepHistoryCount: number;
  showArtworkInBackground: boolean;
}

