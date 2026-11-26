import { exec } from "child_process";
import path from "path";
import fs from "fs";

export interface VideoQualityOption {
  quality: string; // e.g., "1080p", "720p", "480p", "360p", "Audio"
  height: number;
  videoUrl: string;
  audioUrl?: string;
  isMuxed: boolean;
}

export interface StreamInfo {
  streamUrl: string; // Best audio URL (for audio-only mode & backwards compatibility)
  videoUrl?: string; // Default best video URL
  qualities: VideoQualityOption[];
  title?: string;
  author?: string;
  duration?: number;
  thumbnail?: string;
  categories?: string[];
  isMusic?: boolean;
  provider: "yt-dlp" | "piped" | "invidious";
}

const PIPED_INSTANCES = [
  "https://pipedapi.kavin.rocks",
  "https://api.piped.privacy.com.de",
  "https://piped-api.lunar.icu",
  "https://pipedapi.tokhmi.xyz",
  "https://api-piped.mha.fi",
];

const streamCache = new Map<string, { data: StreamInfo; expiresAt: number }>();

// Locate local yt-dlp if running locally
function getLocalYtDlpPath(): string | null {
  const possiblePaths = [
    path.resolve(process.cwd(), "yt-dlp.exe"),
    path.resolve(process.cwd(), "server/yt-dlp.exe"),
    path.resolve(process.cwd(), "../yt-dlp.exe"),
    path.resolve(process.cwd(), "../../main/yt-dlp.exe"),
    path.resolve(process.cwd(), "../../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "../KahoiiMusic/yt-dlp.exe"),
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function buildQualityLabel(height: number): string {
  if (height >= 2160) return "4K";
  if (height >= 1440) return "1440p";
  if (height >= 1080) return "1080p";
  if (height >= 720) return "720p";
  if (height >= 480) return "480p";
  if (height >= 360) return "360p";
  return `${height}p`;
}

// 1. Resolve via local yt-dlp JSON dump (supports multi-quality video + audio)
async function resolveViaYtDlp(videoId: string): Promise<StreamInfo | null> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return null;

  return new Promise((resolve) => {
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const cmd = `"${ytDlpPath}" -J --no-playlist --no-warnings --no-check-certificates --force-ipv4 "${url}"`;

    exec(cmd, { timeout: 30000, maxBuffer: 15 * 1024 * 1024 }, (error, stdout) => {
      if (error || !stdout) {
        resolve(null);
        return;
      }

      try {
        const info = JSON.parse(stdout);
        const formats: any[] = Array.isArray(info.formats) ? info.formats : [];

        // Best audio-only format (prefer direct https m4a/mp4a for browser compatibility)
        const audioFormats = formats
          .filter(
            (f) =>
              f.vcodec === "none" &&
              f.acodec &&
              f.acodec !== "none" &&
              f.url &&
              (!f.protocol || f.protocol === "https")
          )
          .sort((a, b) => {
            const aM4a = a.ext === "m4a" ? 1 : 0;
            const bM4a = b.ext === "m4a" ? 1 : 0;
            if (aM4a !== bM4a) return bM4a - aM4a;
            return (b.abr || 0) - (a.abr || 0);
          });

        const bestAudioUrl: string = audioFormats[0]?.url || "";

        // Collect direct HTTPS video formats (exclude m3u8_native HLS manifests; prefer avc1 H.264 mp4)
        const qualityMap = new Map<number, VideoQualityOption>();

        // First pass: muxed mp4 https formats (video + audio in 1 URL, e.g. itag 18 360p)
        for (const f of formats) {
          if (
            f.vcodec &&
            f.vcodec !== "none" &&
            f.acodec &&
            f.acodec !== "none" &&
            f.ext === "mp4" &&
            (!f.protocol || f.protocol === "https") &&
            f.height &&
            f.url
          ) {
            qualityMap.set(f.height, {
              quality: buildQualityLabel(f.height),
              height: f.height,
              videoUrl: f.url,
              isMuxed: true,
            });
          }
        }

        // Second pass: adaptive https video-only formats (prefer mp4 avc1, fallback to webm vp9)
        if (bestAudioUrl) {
          const adaptiveVideo = formats
            .filter(
              (f) =>
                f.vcodec &&
                f.vcodec !== "none" &&
                (!f.acodec || f.acodec === "none") &&
                (f.ext === "mp4" || f.ext === "webm") &&
                (!f.protocol || f.protocol === "https") &&
                f.height &&
                f.height <= 2160 &&
                f.url
            )
            .sort((a, b) => {
              const aMp4 = a.ext === "mp4" ? 1 : 0;
              const bMp4 = b.ext === "mp4" ? 1 : 0;
              if (aMp4 !== bMp4) return bMp4 - aMp4;
              const aAvc = String(a.vcodec).startsWith("avc1") ? 1 : 0;
              const bAvc = String(b.vcodec).startsWith("avc1") ? 1 : 0;
              return bAvc - aAvc;
            });

          for (const f of adaptiveVideo) {
            if (!qualityMap.has(f.height)) {
              qualityMap.set(f.height, {
                quality: buildQualityLabel(f.height),
                height: f.height,
                videoUrl: f.url,
                audioUrl: bestAudioUrl,
                isMuxed: false,
              });
            }
          }
        }

        const qualities = Array.from(qualityMap.values()).sort((a, b) => b.height - a.height);
        const fallbackAudio = bestAudioUrl || qualities.find((q) => q.isMuxed)?.videoUrl || "";

        if (fallbackAudio) {
          qualities.push({
            quality: "Chỉ âm thanh",
            height: 0,
            videoUrl: "",
            audioUrl: fallbackAudio,
            isMuxed: false,
          });
        }

        if (!fallbackAudio && qualities.length === 0) {
          resolve(null);
          return;
        }

        const defaultVideo =
          qualities.find((q) => q.height === 720) ||
          qualities.find((q) => q.height > 0) ||
          undefined;

        const categories: string[] | undefined = Array.isArray(info.categories)
          ? info.categories.map((c: any) => String(c))
          : undefined;
        const isMusic: boolean | undefined = categories
          ? categories.some((c) => /music|âm nhạc/i.test(c))
          : info.track || info.artist
          ? true
          : undefined;

        resolve({
          streamUrl: fallbackAudio,
          videoUrl: defaultVideo?.videoUrl,
          qualities,
          title: info.title,
          author: info.uploader || info.channel,
          duration: info.duration,
          thumbnail: info.thumbnail,
          categories,
          isMusic,
          provider: "yt-dlp",
        });
      } catch {
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
        headers: { "User-Agent": "ThuongTube-Web/1.0" },
      });
      clearTimeout(timeoutId);

      if (!res.ok) continue;

      const data = (await res.json()) as any;
      const audioStreams: any[] = Array.isArray(data?.audioStreams) ? data.audioStreams : [];
      const videoStreams: any[] = Array.isArray(data?.videoStreams) ? data.videoStreams : [];

      if (audioStreams.length === 0 && videoStreams.length === 0) continue;

      const m4a = audioStreams.find(
        (s: any) => s.format === "M4A" || (s.mimeType && s.mimeType.includes("mp4"))
      );
      const bestAudioUrl = (m4a || audioStreams[0])?.url || "";

      const qualityMap = new Map<number, VideoQualityOption>();

      for (const v of videoStreams) {
        if (!v.url || !v.quality) continue;
        const heightMatch = String(v.quality).match(/(\d+)/);
        const height = heightMatch ? parseInt(heightMatch[1], 10) : 0;
        if (!height || height > 2160) continue;

        const isMp4 = v.format === "MPEG_4" || (v.mimeType && v.mimeType.includes("mp4"));
        if (!isMp4) continue;

        const isMuxed = v.videoOnly === false;
        const existing = qualityMap.get(height);
        if (!existing || (isMuxed && !existing.isMuxed)) {
          qualityMap.set(height, {
            quality: buildQualityLabel(height),
            height,
            videoUrl: v.url,
            audioUrl: isMuxed ? undefined : bestAudioUrl,
            isMuxed,
          });
        }
      }

      const qualities = Array.from(qualityMap.values()).sort((a, b) => b.height - a.height);
      const streamUrl = bestAudioUrl || qualities.find((q) => q.isMuxed)?.videoUrl || "";

      if (streamUrl) {
        qualities.push({
          quality: "Chỉ âm thanh",
          height: 0,
          videoUrl: "",
          audioUrl: streamUrl,
          isMuxed: false,
        });
      }

      if (!streamUrl && qualities.length === 0) continue;

      const defaultVideo =
        qualities.find((q) => q.height === 720) ||
        qualities.find((q) => q.height > 0) ||
        undefined;

      const categories: string[] | undefined = data.category
        ? [String(data.category)]
        : undefined;
      const isMusic: boolean | undefined = data.category
        ? /music|âm nhạc/i.test(String(data.category))
        : undefined;

      return {
        streamUrl,
        videoUrl: defaultVideo?.videoUrl,
        qualities,
        title: data.title,
        author: data.uploader,
        duration: data.duration,
        thumbnail: data.thumbnailUrl,
        categories,
        isMusic,
        provider: "piped",
      };
    } catch {
      continue;
    }
  }
  return null;
}

export async function resolveAudioStream(videoId: string): Promise<StreamInfo> {
  const cached = streamCache.get(videoId);
  if (cached && cached.expiresAt > Date.now()) {
    return cached.data;
  }

  // Strategy 1: Local yt-dlp (Fastest and supports up to 1080p/4K + audio)
  try {
    const ytDlpResult = await resolveViaYtDlp(videoId);
    if (ytDlpResult) {
      streamCache.set(videoId, { data: ytDlpResult, expiresAt: Date.now() + 30 * 60 * 1000 });
      return ytDlpResult;
    }
  } catch (err) {
    console.warn("[StreamResolver] yt-dlp fallback to piped:", err);
  }

  // Strategy 2: Piped API
  const pipedResult = await resolveViaPiped(videoId);
  if (pipedResult) {
    streamCache.set(videoId, { data: pipedResult, expiresAt: Date.now() + 30 * 60 * 1000 });
    return pipedResult;
  }

  throw new Error("Không thể trích xuất luồng phát từ video YouTube này. Vui lòng thử lại!");
}
