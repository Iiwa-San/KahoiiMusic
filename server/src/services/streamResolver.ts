import { exec } from "child_process";
import path from "path";
import fs from "fs";

export interface StreamInfo {
  streamUrl: string;
  title?: string;
  author?: string;
  duration?: number;
  thumbnail?: string;
  provider: "innertube" | "visionos" | "yt-dlp" | "piped" | "invidious";
}

// 1. Resolve via InnerTube Android VR client (Pure Node.js HTTPS, fastest & most reliable on Vercel)
async function resolveViaInnerTubeVR(videoId: string): Promise<StreamInfo | null> {
  try {
    const payload = {
      videoId: videoId,
      context: {
        client: {
          clientName: "ANDROID_VR",
          clientVersion: "1.61.48",
          deviceMake: "Oculus",
          deviceModel: "Quest 3",
          osName: "Android",
          osVersion: "12",
          hl: "en",
          gl: "US",
        },
      },
    };

    const res = await fetch("https://www.youtube.com/youtubei/v1/player", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "User-Agent": "com.google.android.apps.youtube.vr.oculus/1.61.48 (Linux; U; Android 12)",
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000),
    });

    if (!res.ok) return null;
    const json = (await res.json()) as any;
    if (json.playabilityStatus?.status !== "OK") return null;

    const videoDetails = json.videoDetails;
    const title = videoDetails?.title || "Kahoii Music";
    const author = videoDetails?.author || "";
    const duration = parseInt(videoDetails?.lengthSeconds || "0", 10) || 0;

    const adaptiveFormats: any[] = json.streamingData?.adaptiveFormats || [];
    const audioFormats = adaptiveFormats.filter((f) => f.mimeType && f.mimeType.startsWith("audio/"));

    // Prioritize itag 140 (128kbps AAC / audio/mp4) for 100% native mobile compatibility (iOS Safari & Android Chrome)
    const m4aFormat = audioFormats.find((f) => f.mimeType && f.mimeType.includes("mp4") && f.url);
    const bestAudio = m4aFormat || audioFormats.sort((a, b) => (b.bitrate || 0) - (a.bitrate || 0)).find((f) => f.url);

    if (bestAudio?.url) {
      return {
        streamUrl: bestAudio.url,
        title,
        author,
        duration,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hq720.jpg`,
        provider: "innertube",
      };
    }
  } catch (err: any) {
    console.warn("[InnerTube VR Stream Warning]:", err.message);
  }
  return null;
}

// 2. Resolve via InnerTube VisionOS client
async function resolveViaVisionOS(videoId: string): Promise<StreamInfo | null> {
  try {
    const payload = {
      context: {
        client: {
          clientName: "VISIONOS",
          clientVersion: "1.02",
          deviceMake: "Apple",
          deviceModel: "RealityDevice17,1",
          userAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
          osName: "visionOS",
          osVersion: "26.5.23O471",
          hl: "en",
          timeZone: "UTC",
          utcOffsetMinutes: 0,
        },
      },
      videoId: videoId,
      playbackContext: {
        contentPlaybackContext: {
          html5Preference: "HTML5_PREF_WANTS",
          signatureTimestamp: 20752,
        },
      },
      contentCheckOk: true,
      racyCheckOk: true,
    };

    const res = await fetch("https://www.youtube.com/youtubei/v1/player?prettyPrint=false", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
        "X-Youtube-Client-Name": "101",
        "X-Youtube-Client-Version": "1.02",
        "Origin": "https://www.youtube.com",
        "Cookie": "PREF=hl=en&tz=UTC",
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000),
    });

    if (!res.ok) return null;
    const json = (await res.json()) as any;
    if (json.playabilityStatus?.status !== "OK") return null;

    const videoDetails = json.videoDetails;
    const title = videoDetails?.title || "Kahoii Music";
    const author = videoDetails?.author || "";
    const duration = parseInt(videoDetails?.lengthSeconds || "0", 10) || 0;

    const adaptiveFormats: any[] = json.streamingData?.adaptiveFormats || [];
    const audioFormats = adaptiveFormats.filter((f) => f.mimeType && f.mimeType.startsWith("audio/"));
    const m4aFormat = audioFormats.find((f) => f.mimeType && f.mimeType.includes("mp4") && f.url);
    const bestAudio = m4aFormat || audioFormats.sort((a, b) => (b.bitrate || 0) - (a.bitrate || 0)).find((f) => f.url);

    if (bestAudio?.url) {
      return {
        streamUrl: bestAudio.url,
        title,
        author,
        duration,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hq720.jpg`,
        provider: "visionos",
      };
    }
  } catch (err: any) {
    console.warn("[VisionOS Stream Warning]:", err.message);
  }
  return null;
}

// 3. Locate local yt-dlp if running locally
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

// 4. Master Stream Resolver
export async function resolveAudioStream(videoId: string): Promise<StreamInfo> {
  // Method 1: InnerTube VR (Direct 128kbps AAC / m4a without cipher, blazing fast & 100% Vercel compatible)
  const vrResult = await resolveViaInnerTubeVR(videoId);
  if (vrResult) {
    return vrResult;
  }

  // Method 2: VisionOS client
  const visionResult = await resolveViaVisionOS(videoId);
  if (visionResult) {
    return visionResult;
  }

  // Method 3: Local yt-dlp (if available on local dev machine)
  try {
    const ytDlpResult = await resolveViaYtDlp(videoId);
    if (ytDlpResult) {
      return ytDlpResult;
    }
  } catch (err) {
    console.warn("[StreamResolver] yt-dlp fallback:", err);
  }

  throw new Error("Không thể trích xuất luồng âm thanh từ video YouTube này. Vui lòng thử lại!");
}
