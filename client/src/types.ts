export interface Track {
  id: string;
  title: string;
  author: string;
  duration: number; // in seconds
  durationText?: string;
  thumbnail: string;
  streamUrl?: string;
  isMusic?: boolean;
}

export interface LyricLine {
  time: number; // in seconds
  text: string;
}

export interface SearchTopResult {
  id: string;
  title: string;
  subtitle: string;
  author: string;
  duration: number;
  durationText?: string;
  thumbnail: string;
  type: "song" | "artist" | "playlist" | "video";
  isMusic?: boolean;
}

export interface SearchArtistItem {
  id: string;
  name: string;
  subscribers?: string;
  thumbnail: string;
}

export interface SearchPlaylistItem {
  id: string;
  title: string;
  author: string;
  trackCountText?: string;
  thumbnail: string;
}

export interface StructuredSearchResult {
  query: string;
  topResult: SearchTopResult | null;
  tracks: Track[];
  artists: SearchArtistItem[];
  playlists: SearchPlaylistItem[];
}

export interface OfflineTrackItem {
  id: string;
  title: string;
  author: string;
  duration: number;
  durationText?: string;
  thumbnail: string;
  savedAt: number;
  sizeBytes: number;
  audioBlob: Blob;
  lyrics?: {
    syncedLyrics?: LyricLine[] | null;
    plainLyrics?: string | null;
    instrumental?: boolean;
  } | null;
}
