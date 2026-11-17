export interface VideoQualityOption {
  quality: string;
  height: number;
  videoUrl: string;
  audioUrl?: string;
  isMuxed: boolean;
}

export interface Track {
  id: string;
  title: string;
  author: string;
  duration: number; // in seconds
  durationText?: string;
  thumbnail: string;
  streamUrl?: string;
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
