import { exec } from "child_process";
import path from "path";
import fs from "fs";

export interface VideoQualityOption {
  quality: string;
  height: number;
  videoUrl: string;
  audioUrl?: string;
  isMuxed: boolean;
}

export interface StreamInfo {
  streamUrl: string;
  videoUrl?: string;
  qualities: VideoQualityOption[];
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

function buildQualityLabel(height: number): string {
  if (height >= 2160) return "4K";
  if (height >= 1440) return "1440p";
  if (height >= 1080) return "1080p";
  if (height >= 720) return "720p";
  if (height >= 480) return "480p";
  if (height >= 360) return "360p";
  return `${height}p`;
}

async function resolveViaYtDlp(videoId: string): Promise<StreamInfo | null> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return null;

  return new Promise((resolve) => {
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const cmd = `"${ytDlpPath}" -J --no-playlist --no-warnings --force-ipv4 "${url}"`;

    exec(cmd, { timeout: 20000, maxBuffer: 10 * 1024 * 1024 }, (error, stdout) => {
      if (error || !stdout) {
        resolve(null);
        return;
      }

      try {
        const info = JSON.parse(stdout);
        const formats: any[] = Array.isArray(info.formats) ? info.formats : [];

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
        const qualityMap = new Map<number, VideoQualityOption>();

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

        if (bestAudioUrl) {
          const adaptiveMp4 = formats
            .filter(
              (f) =>
                f.vcodec &&
                f.vcodec !== "none" &&
                (!f.acodec || f.acodec === "none") &&
                f.ext === "mp4" &&
                (!f.protocol || f.protocol === "https") &&
                f.height &&
                f.height <= 2160 &&
                f.url
            )
            .sort((a, b) => {
              const aAvc = String(a.vcodec).startsWith("avc1") ? 1 : 0;
              const bAvc = String(b.vcodec).startsWith("avc1") ? 1 : 0;
              return bAvc - aAvc;
            });

          for (const f of adaptiveMp4) {
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

        resolve({
          streamUrl: fallbackAudio,
          videoUrl: defaultVideo?.videoUrl,
          qualities,
          title: info.title,
          author: info.uploader || info.channel,
          duration: info.duration,
          thumbnail: info.thumbnail,
          provider: "yt-dlp",
        });
      } catch {
        resolve(null);
      }
    });
  });
}

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

      return {
        streamUrl,
        videoUrl: defaultVideo?.videoUrl,
        qualities,
        title: data.title,
        author: data.uploader,
        duration: data.duration,
        thumbnail: data.thumbnailUrl,
        provider: "piped",
      };
    } catch {
      continue;
    }
  }
  return null;
}

export async function resolveAudioStream(videoId: string): Promise<StreamInfo> {
  try {
    const ytDlpResult = await resolveViaYtDlp(videoId);
    if (ytDlpResult) {
      return ytDlpResult;
    }
  } catch (err) {
    console.warn("[StreamResolver] yt-dlp fallback to piped:", err);
  }

  const pipedResult = await resolveViaPiped(videoId);
  if (pipedResult) {
    return pipedResult;
  }

  throw new Error("Không thể trích xuất luồng phát từ video YouTube này. Vui lòng thử lại!");
}
