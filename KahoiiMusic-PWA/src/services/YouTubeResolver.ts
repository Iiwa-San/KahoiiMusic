import { PlaylistItem } from '../types/music';

export interface ResolvedStream {
  streamUrl: string;
  title: string;
  author: string;
  durationSeconds: number;
  thumbnailUrl: string;
}

export class YouTubeResolver {
  private static instance: YouTubeResolver;

  public static getInstance(): YouTubeResolver {
    if (!YouTubeResolver.instance) {
      YouTubeResolver.instance = new YouTubeResolver();
    }
    return YouTubeResolver.instance;
  }

  public extractVideoId(input: string): string | null {
    const trimmed = input.trim();
    if (!trimmed) return null;
    if (/^[a-zA-Z0-9_-]{11}$/.test(trimmed)) return trimmed;

    const patterns = [
      /(?:v=|\/v\/|embed\/|youtu\.be\/|\/shorts\/)([a-zA-Z0-9_-]{11})/,
      /[?&]v=([a-zA-Z0-9_-]{11})/,
    ];
    for (const pattern of patterns) {
      const match = trimmed.match(pattern);
      if (match && match[1]) return match[1];
    }
    return null;
  }

  public isUrl(input: string): boolean {
    const trimmed = input.trim().toLowerCase();
    return (
      trimmed.startsWith('http://') ||
      trimmed.startsWith('https://') ||
      trimmed.includes('youtu.be') ||
      trimmed.includes('youtube.com')
    );
  }

  public async resolveAudioStream(videoId: string): Promise<ResolvedStream> {
    // 1. Try local or hosted proxy /api/player
    try {
      const res = await fetch(`/api/player?id=${videoId}`);
      if (res.ok) {
        const data = await res.json();
        if (data && data.streamUrl) return data;
      }
    } catch {
      // fallback
    }

    // 2. Fallback to public Invidious CORS instances
    const invidiousInstances = [
      'https://invidious.protokolla.fi',
      'https://yt.artemislena.eu',
    ];
    for (const inst of invidiousInstances) {
      try {
        const res = await fetch(`${inst}/api/v1/videos/${videoId}`);
        if (!res.ok) continue;
        const j = await res.json();
        const audio = (j.adaptiveFormats || []).find((f: any) => (f.type || '').includes('audio') && f.url);
        if (audio && audio.url) {
          return {
            streamUrl: audio.url,
            title: j.title || 'Bài hát YouTube',
            author: j.author || 'YouTube',
            durationSeconds: j.lengthSeconds || 0,
            thumbnailUrl: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
          };
        }
      } catch {
        continue;
      }
    }

    throw new Error('Không thể phân giải luồng phát âm thanh cho video này.');
  }

  public async search(query: string): Promise<PlaylistItem[]> {
    const trimmed = query.trim();
    if (!trimmed) return [];

    try {
      const res = await fetch(`/api/search?q=${encodeURIComponent(trimmed)}`);
      if (res.ok) {
        return await res.json();
      }
    } catch {
      // fallback
    }

    return [];
  }
}

