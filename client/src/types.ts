export interface Track {
  id: string;
  title: string;
  author: string;
  duration: number; // in seconds
  thumbnail: string;
  streamUrl?: string;
}

export interface LyricLine {
  time: number; // in seconds
  text: string;
}

