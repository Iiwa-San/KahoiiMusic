import { execFile } from "child_process";
import path from "path";
import fs from "fs";

export interface SearchTrackResult {
  id: string;
  title: string;
  author: string;
  duration: number; // in seconds
  thumbnail: string;
}

const PIPED_SEARCH_INSTANCES = [
  "https://pipedapi.kavin.rocks",
  "https://api.piped.privacy.com.de",
  "https://piped-api.lunar.icu",
];

function getLocalYtDlpPath(): string | null {
  const possiblePaths = [
    path.resolve(process.cwd(), "../../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "../KahoiiMusic/yt-dlp.exe"),
    "D:/KahoiiProjects/MusicApp/KahoiiMusic/yt-dlp.exe",
    path.resolve(process.cwd(), "yt-dlp.exe"),
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

async function searchViaYtDlp(query: string, maxResults = 50): Promise<SearchTrackResult[]> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return [];

  return new Promise((resolve) => {
    const args = [
      "--flat-playlist",
      "--encoding", "utf-8",
      "--no-warnings",
      "--print", "%(id)s||%(title)s||%(uploader)s||%(duration)s||%(thumbnail)s",
      `ytsearch${maxResults}:${query}`,
    ];

    execFile(
      ytDlpPath,
      args,
      {
        timeout: 25000,
        encoding: "utf8",
        env: { ...process.env, PYTHONIOENCODING: "utf-8" },
        maxBuffer: 10 * 1024 * 1024,
      },
      (error, stdout) => {
        if (error && (!stdout || stdout.trim().length === 0)) {
          console.warn("[yt-dlp search error]:", error.message);
          resolve([]);
          return;
        }

        const lines = (stdout || "").trim().split("\n");
        const results: SearchTrackResult[] = [];
        const seenIds = new Set<string>();

        for (const line of lines) {
          const parts = line.split("||");
          if (parts.length >= 2 && parts[0].trim().length > 0) {
            const id = parts[0].trim();
            if (id.startsWith("UC") || seenIds.has(id)) continue;
            seenIds.add(id);

            const title = parts[1]?.trim() || "Chưa có tiêu đề";
            const author = parts[2]?.trim() || "Kahoii Music";
            const durRaw = parts[3]?.trim();
            const duration = durRaw && !isNaN(Number(durRaw)) ? Math.round(Number(durRaw)) : 0;
            const rawThumb = parts[4]?.trim();
            const thumbnail = rawThumb && rawThumb !== "NA" && rawThumb.startsWith("http")
              ? rawThumb
              : `https://i.ytimg.com/vi/${id}/mqdefault.jpg`;

            results.push({ id, title, author, duration, thumbnail });
          }
        }
        resolve(results);
      }
    );
  });
}

async function searchViaPiped(query: string): Promise<SearchTrackResult[]> {
  for (const instance of PIPED_SEARCH_INSTANCES) {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 6000);

      const res = await fetch(`${instance}/search?q=${encodeURIComponent(query)}&filter=music_songs`, {
        signal: controller.signal,
        headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
      });
      clearTimeout(timeoutId);

      if (!res.ok) continue;

      const data = (await res.json()) as any;
      if (data && Array.isArray(data.items)) {
        return data.items
          .filter((item: any) => item.type === "stream")
          .map((item: any) => {
            const urlParts = item.url ? item.url.split("/watch?v=") : [];
            const id = urlParts[1] || item.url || "";
            return {
              id,
              title: item.title,
              author: item.uploaderName || "Nghệ sĩ",
              duration: item.duration || 0,
              thumbnail: item.thumbnail || `https://i.ytimg.com/vi/${id}/mqdefault.jpg`,
            };
          });
      }
    } catch {
      continue;
    }
  }
  return [];
}

export async function searchYouTubeTracks(query: string): Promise<SearchTrackResult[]> {
  if (!query || !query.trim()) return [];

  // Check if user pasted a direct YouTube link / video ID
  const ytMatch = query.match(/(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?\/\s]{11})/i);
  if (ytMatch && ytMatch[1]) {
    const videoId = ytMatch[1];
    return [
      {
        id: videoId,
        title: "YouTube Video",
        author: "YouTube",
        duration: 0,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
      },
    ];
  }

  // 1. Try yt-dlp first with up to 50 results
  const ytDlpResults = await searchViaYtDlp(query, 50);
  if (ytDlpResults.length > 0) {
    return ytDlpResults;
  }

  // 2. Fallback to Piped
  return await searchViaPiped(query);
}
