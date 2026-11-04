export interface PlaylistItem {
  id: string;
  title: string;
  author: string;
  durationSeconds: number;
  thumbnailUrl: string;
  streamUrl?: string;
  localAudioUri?: string;
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

export interface ResolvedAudioStream {
  streamUrl: string;
  hlsUrl?: string;
  title: string;
  author: string;
  durationSeconds: number;
  thumbnailUrl: string;
  contentLength?: number;
}

export interface UserPlaylist {
  id: string;
  name: string;
  createdAt: number;
  items: PlaylistItem[];
}

export interface FirebaseUserInfo {
  localId: string;
  email: string;
  idToken: string;
}

export interface CloudMusicPayload {
  playlists: PlaylistItem[];
  savedTracks: PlaylistItem[];
  history: PlaylistItem[];
  favorites: PlaylistItem[];
  collections: UserPlaylist[];
  updatedAt: string;
}

export interface ErrorLogItem {
  id: string;
  title: string;
  message: string;
  category: string;
  timestamp: number;
}
