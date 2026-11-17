import { exec } from "child_process";
import path from "path";
import fs from "fs";

export interface StreamInfo {
  streamUrl: string;
  title?: string;
  author?: string;
  duration?: number;
  thumbnail?: string;
  provider: "yt-dlp" | "piped" | "invidious";
}

const PIPED_INSTANCES = [
  "https://pipedapi.kavin.rocks",
  "https://api.piped.privacy.com.de",
  "https://piped-api.lunar.icu",
  "https://pipedapi.tokhmi.xyz",
  "https://api-piped.mha.fi",
];

// Locate local yt-dlp if running locally
function getLocalYtDlpPath(): string | null {
  const possiblePaths = [
    path.resolve(process.cwd(), "../../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "yt-dlp.exe"),
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

// 1. Resolve via local yt-dlp
async function resolveViaYtDlp(videoId: string): Promise<StreamInfo | null> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return null;

  return new Promise((resolve) => {
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const cmd = `"${ytDlpPath}" -g --no-playlist --no-warnings --force-ipv4 -f "bestaudio[ext=m4a]/bestaudio/best" "${url}"`;

    exec(cmd, { timeout: 15000 }, (error, stdout, stderr) => {
      if (error || !stdout) {
        if (error) console.warn(`[yt-dlp error for ${videoId}]:`, error.message, stderr);
        resolve(null);
        return;
      }

      const streamUrl = stdout.trim().split("\n")[0]?.trim();
      if (streamUrl && streamUrl.startsWith("http")) {
        resolve({
          streamUrl,
          provider: "yt-dlp",
        });
      } else {
        resolve(null);
      }
    });
  });
}

// 2. Resolve via Piped API instances
async function resolveViaPiped(videoId: string): Promise<StreamInfo | null> {
  for (const instance of PIPED_INSTANCES) {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 6000);

      const res = await fetch(`${instance}/streams/${videoId}`, {
        signal: controller.signal,
        headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
      });
      clearTimeout(timeoutId);

      if (!res.ok) continue;

      const data = (await res.json()) as any;
      if (data && Array.isArray(data.audioStreams) && data.audioStreams.length > 0) {
        // Find best audio stream (preferably m4a for web compatibility)
        const m4a = data.audioStreams.find((s: any) => s.format === "M4A" || (s.mimeType && s.mimeType.includes("mp4")));
        const chosen = m4a || data.audioStreams[0];

        if (chosen && chosen.url) {
          return {
            streamUrl: chosen.url,
            title: data.title,
            author: data.uploader,
            duration: data.duration,
            thumbnail: data.thumbnailUrl,
            provider: "piped",
          };
        }
      }
    } catch {
      // Try next instance
      continue;
    }
  }
  return null;
}

export async function resolveAudioStream(videoId: string): Promise<StreamInfo> {
  // Strategy 1: Local yt-dlp (Fastest and most reliable on machine with yt-dlp)
  try {
    const ytDlpResult = await resolveViaYtDlp(videoId);
    if (ytDlpResult) {
      return ytDlpResult;
    }
  } catch (err) {
    console.warn("[StreamResolver] yt-dlp fallback to piped:", err);
  }

  // Strategy 2: Piped API
  const pipedResult = await resolveViaPiped(videoId);
  if (pipedResult) {
    return pipedResult;
  }

  throw new Error("Không thể trích xuất luồng âm thanh từ video YouTube này. Vui lòng thử lại!");
}

