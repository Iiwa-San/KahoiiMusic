export interface LyricsResponse {
  syncedLyrics: string | null;
  plainLyrics: string | null;
  instrumental: boolean;
}

export async function fetchLyrics(title: string, artist?: string, duration?: number): Promise<LyricsResponse> {
  try {
    // 1. Try exact match if title & artist are provided
    if (title && artist) {
      const cleanTitle = cleanTrackTitle(title);
      const url = new URL("https://lrclib.net/api/get");
      url.searchParams.set("track_name", cleanTitle);
      url.searchParams.set("artist_name", artist);
      if (duration && duration > 0) {
        url.searchParams.set("duration", Math.round(duration).toString());
      }

      const res = await fetch(url.toString(), {
        headers: { "User-Agent": "KahoiiMusic-Web/1.0 (https://github.com/Iiwa-San/KahoiiMusic)" },
      });

      if (res.ok) {
        const data = (await res.json()) as any;
        if (data.syncedLyrics || data.plainLyrics) {
          return {
            syncedLyrics: data.syncedLyrics || null,
            plainLyrics: data.plainLyrics || null,
            instrumental: data.instrumental || false,
          };
        }
      }
    }

    // 2. Search fallback
    const query = cleanTrackTitle(title);
    const searchUrl = `https://lrclib.net/api/search?q=${encodeURIComponent(query)}`;
    const searchRes = await fetch(searchUrl, {
      headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
    });

    if (searchRes.ok) {
      const list = (await searchRes.json()) as any[];
      if (Array.isArray(list) && list.length > 0) {
        // Find best match with synced lyrics
        const best = list.find((item) => item.syncedLyrics) || list[0];
        return {
          syncedLyrics: best.syncedLyrics || null,
          plainLyrics: best.plainLyrics || null,
          instrumental: best.instrumental || false,
        };
      }
    }
  } catch (err) {
    console.warn("[LyricsService] Error fetching lyrics:", err);
  }

  return {
    syncedLyrics: null,
    plainLyrics: null,
    instrumental: false,
  };
}

function cleanTrackTitle(title: string): string {
  return title
    .replace(/\s*[\[\(](?:Official|Audio|Video|MV|Lyric|Lyrics|HD|4K|M\/V|Visualizer)[^\]\)]*[\]\)]/gi, "")
    .replace(/ft\.?|feat\.?/gi, "")
    .trim();
}

