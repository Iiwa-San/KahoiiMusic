import { exec } from "child_process";
import path from "path";
import fs from "fs";

export interface StreamInfo {
  streamUrl: string;
  title?: string;
  author?: string;
  duration?: number;
  thumbnail?: string;
  provider: "visionos" | "innertube" | "piped" | "yt-dlp";
}

const PIPED_INSTANCES = [
  "https://pipedapi.kavin.rocks",
  "https://api.piped.privacy.com.de",
  "https://piped-api.lunar.icu",
  "https://pipedapi.tokhmi.xyz",
  "https://api-piped.mha.fi",
];

let cachedVisitorData: string | null = null;
let cachedSts = Math.floor(Date.now() / 86400000);
let lastVisitorFetchTime = 0;

/**
 * Extracts the highest quality AAC / M4A direct stream URL from YouTube streaming data
 */
function extractBestAudioFormat(streamingData: any): string | null {
  const adaptiveFormats: any[] = streamingData?.adaptiveFormats || [];
  const audioFormats = adaptiveFormats.filter((f) => f.mimeType && f.mimeType.startsWith("audio/"));
  const m4aFormat = audioFormats.find((f) => f.mimeType && f.mimeType.includes("mp4") && f.url);
  const bestAudio = m4aFormat || audioFormats.sort((a, b) => (b.bitrate || 0) - (a.bitrate || 0)).find((f) => f.url);
  return bestAudio?.url || null;
}

/**
 * Fetches or returns cached YouTube Visitor Data & Signature Timestamp (STS)
 */
async function getVisitorDataAndSts(videoId: string): Promise<{ visitorData: string; sts: number }> {
  const now = Date.now();
  const currentSts = Math.floor(now / 86400000);

  if (cachedVisitorData && now - lastVisitorFetchTime < 3600000) {
    return { visitorData: cachedVisitorData, sts: currentSts };
  }

  // Strategy A: InnerTube /visitor_id official API
  try {
    const res = await fetch("https://www.youtube.com/youtubei/v1/visitor_id", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
      },
      body: JSON.stringify({
        context: {
          client: {
            clientName: "WEB",
            clientVersion: "2.20240101.01.00",
            hl: "en",
            gl: "US",
          },
        },
      }),
      signal: AbortSignal.timeout(3500),
    });

    if (res.ok) {
      const data = (await res.json()) as any;
      const v = data?.responseContext?.visitorData;
      if (v) {
        cachedVisitorData = decodeURIComponent(v);
        cachedSts = currentSts;
        lastVisitorFetchTime = now;
        return { visitorData: cachedVisitorData, sts: currentSts };
      }
    }
  } catch (err: any) {
    console.warn("[StreamResolver] Failed to fetch visitor_id from API:", err?.message);
  }

  // Strategy B: Fallback watch page scraping (if visitor_id fails)
  try {
    const pageRes = await fetch(`https://www.youtube.com/watch?v=${videoId}`, {
      headers: {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Accept-Language": "en-US,en;q=0.9",
      },
      signal: AbortSignal.timeout(4000),
    });
    if (pageRes.ok) {
      const html = await pageRes.text();
      const visitorMatch = html.match(/["']VISITOR_DATA["']\s*:\s*["']([^"']+)["']/);
      const stsMatch = html.match(/["']STS["']\s*:\s*(\d+)/);
      if (visitorMatch && visitorMatch[1]) {
        cachedVisitorData = visitorMatch[1];
        lastVisitorFetchTime = now;
      }
      if (stsMatch && stsMatch[1]) {
        cachedSts = parseInt(stsMatch[1], 10);
      }
    }
  } catch (err) {
    console.warn("[StreamResolver] Failed to refresh visitor data:", err);
  }

  return {
    visitorData: cachedVisitorData || "CgtOOTF5Rk5IZllJYyiw_vnXBjIKCgJWThIEGgAg",
    sts: cachedSts || 20775,
  };
}

/**
 * Method 1: Resolve via InnerTube VisionOS client (Direct 128kbps AAC audio stream, zero bot check)
 */
async function resolveViaVisionOS(videoId: string): Promise<StreamInfo | null> {
  try {
    const { visitorData, sts } = await getVisitorDataAndSts(videoId);

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
      videoId,
      playbackContext: {
        contentPlaybackContext: {
          html5Preference: "HTML5_PREF_WANTS",
          signatureTimestamp: sts,
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
        "X-Goog-Visitor-Id": visitorData,
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(10000),
    });

    if (!res.ok) return null;
    const json = (await res.json()) as any;
    if (json.playabilityStatus?.status !== "OK") {
      console.warn("[VisionOS Playability Status]:", json.playabilityStatus?.status, json.playabilityStatus?.reason);
      return null;
    }

    const videoDetails = json.videoDetails;
    const title = videoDetails?.title || "Kahoii Music";
    const author = videoDetails?.author || "";
    const duration = parseInt(videoDetails?.lengthSeconds || "0", 10) || 0;
    const streamUrl = extractBestAudioFormat(json.streamingData);

    if (streamUrl) {
      return {
        streamUrl,
        title,
        author,
        duration,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hq720.jpg`,
        provider: "visionos",
      };
    }
  } catch (err: any) {
    console.warn("[VisionOS Stream Warning]:", err?.message || err);
  }
  return null;
}

/**
 * Method 2: Resolve via InnerTube Android VR client (Oculus Quest 3)
 */
async function resolveViaInnerTubeVR(videoId: string): Promise<StreamInfo | null> {
  try {
    const payload = {
      videoId,
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
      signal: AbortSignal.timeout(10000),
    });

    if (!res.ok) return null;
    const json = (await res.json()) as any;
    if (json.playabilityStatus?.status !== "OK") return null;

    const videoDetails = json.videoDetails;
    const title = videoDetails?.title || "Kahoii Music";
    const author = videoDetails?.author || "";
    const duration = parseInt(videoDetails?.lengthSeconds || "0", 10) || 0;
    const streamUrl = extractBestAudioFormat(json.streamingData);

    if (streamUrl) {
      return {
        streamUrl,
        title,
        author,
        duration,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hq720.jpg`,
        provider: "innertube",
      };
    }
  } catch (err: any) {
    console.warn("[InnerTube VR Stream Warning]:", err?.message || err);
  }
  return null;
}

async function resolveViaTvEmbedded(videoId: string): Promise<StreamInfo | null> {
  try {
    const payload = {
      videoId,
      context: {
        client: {
          clientName: "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
          clientVersion: "2.0",
          hl: "en",
          gl: "US",
        },
        thirdParty: { embedUrl: "https://www.youtube.com/" },
      },
      contentCheckOk: true,
      racyCheckOk: true,
    };

    const res = await fetch("https://www.youtube.com/youtubei/v1/player?prettyPrint=false", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "User-Agent": "Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0) AppleWebKit/537.36",
      },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(8000),
    });

    if (!res.ok) return null;
    const json = (await res.json()) as any;
    if (json.playabilityStatus?.status !== "OK") return null;

    const videoDetails = json.videoDetails;
    const streamUrl = extractBestAudioFormat(json.streamingData);
    if (streamUrl) {
      return {
        streamUrl,
        title: videoDetails?.title || "Kahoii Music",
        author: videoDetails?.author || "",
        duration: parseInt(videoDetails?.lengthSeconds || "0", 10) || 0,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`,
        provider: "innertube",
      };
    }
  } catch { }
  return null;
}

/**
 * Locate local yt-dlp binary if running in local environment
 */
function getLocalYtDlpPath(): string | null {
  const possiblePaths = [
    "/usr/local/bin/yt-dlp",
    "/usr/bin/yt-dlp",
    path.resolve(process.cwd(), "../../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "yt-dlp.exe"),
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function getCookiesArg(): string {
  const rawCookies = process.env.YT_COOKIES;
  if (!rawCookies || !rawCookies.trim()) return "";
  try {
    const cookiePath = "/tmp/yt_cookies.txt";
    if (!fs.existsSync(cookiePath)) {
      fs.writeFileSync(cookiePath, rawCookies.replace(/\\n/g, "\n"), "utf8");
    }
    return `--cookies "${cookiePath}" `;
  } catch {
    return "";
  }
}

/**
 * Method 3: Resolve using local yt-dlp CLI tool
 */
async function resolveViaYtDlp(videoId: string): Promise<StreamInfo | null> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return null;

  return new Promise((resolve) => {
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const cookiesArg = getCookiesArg();
    const cmd = `"${ytDlpPath}" ${cookiesArg}-g --no-playlist --no-warnings --force-ipv4 -f "bestaudio[ext=m4a]/bestaudio/best" "${url}"`;

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

/**
 * Method 2b: Resolve via Piped API instances in parallel (bypasses datacenter IP throttling)
 */
async function resolveViaPiped(videoId: string): Promise<StreamInfo | null> {
  try {
    return await Promise.any(
      PIPED_INSTANCES.map(async (instance) => {
        const res = await fetch(`${instance}/streams/${videoId}`, {
          signal: AbortSignal.timeout(4500),
          headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
        });
        if (!res.ok) throw new Error(`piped-${res.status}`);
        const data = (await res.json()) as any;
        const audioStreams: any[] = Array.isArray(data?.audioStreams) ? data.audioStreams : [];
        if (audioStreams.length === 0) throw new Error("piped-no-audio");
        const m4a = audioStreams.find(
          (s: any) => s.format === "M4A" || (s.mimeType && s.mimeType.includes("mp4"))
        );
        const best = m4a || audioStreams.sort((a, b) => (b.bitrate || 0) - (a.bitrate || 0))[0];
        if (!best?.url) throw new Error("piped-no-url");
        return {
          streamUrl: best.url,
          title: data.title || "Kahoii Music",
          author: data.uploader || "",
          duration: data.duration || 0,
          thumbnail: data.thumbnailUrl || `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`,
          provider: "piped" as const,
        };
      })
    );
  } catch {
    return null;
  }
}

const INVIDIOUS_INSTANCES = [
  "https://inv.nadeko.net",
  "https://invidious.nerdvpn.de",
  "https://yt.artemislena.eu",
  "https://invidious.jing.rocks",
];

async function resolveViaInvidious(videoId: string): Promise<StreamInfo | null> {
  try {
    return await Promise.any(
      INVIDIOUS_INSTANCES.map(async (instance) => {
        const res = await fetch(`${instance}/api/v1/videos/${videoId}`, {
          signal: AbortSignal.timeout(8000),
          headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
        });
        if (!res.ok) throw new Error(`inv-${res.status}`);
        const data = (await res.json()) as any;
        const adaptive: any[] = Array.isArray(data?.adaptiveFormats) ? data.adaptiveFormats : [];
        const audio = adaptive.filter((f) => f.type && f.type.startsWith("audio/") && f.url);
        const m4a = audio.find((f) => f.type.includes("mp4"));
        const best = m4a || audio[0];
        if (!best?.url) throw new Error("inv-no-audio");
        return {
          streamUrl: best.url,
          title: data.title || "Kahoii Music",
          author: data.author || "",
          duration: data.lengthSeconds || 0,
          thumbnail: `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`,
          provider: "piped" as const,
        };
      })
    );
  } catch {
    return null;
  }
}

const inFlightResolves = new Map<string, Promise<StreamInfo>>();

/**
 * Master Stream Resolver: races VisionOS + InnerTube VR + Piped + Invidious in parallel, then falls back to yt-dlp.
 * Deduplicates concurrent Range-request probes for the same videoId.
 */
export async function resolveAudioStream(videoId: string): Promise<StreamInfo> {
  const existing = inFlightResolves.get(videoId);
  if (existing) return existing;

  const task = (async (): Promise<StreamInfo> => {
    try {
      // Race VisionOS, Android VR, TV Embedded, Piped, and Invidious in parallel so the fastest valid stream wins immediately
      const fastWinner = await Promise.any([
        resolveViaVisionOS(videoId).then((r) => (r ? r : Promise.reject(new Error("no-visionos")))),
        resolveViaInnerTubeVR(videoId).then((r) => (r ? r : Promise.reject(new Error("no-vr")))),
        resolveViaTvEmbedded(videoId).then((r) => (r ? r : Promise.reject(new Error("no-tv")))),
        resolveViaPiped(videoId).then((r) => (r ? r : Promise.reject(new Error("no-piped")))),
        resolveViaInvidious(videoId).then((r) => (r ? r : Promise.reject(new Error("no-inv")))),
      ]);
      if (fastWinner) return fastWinner;
    } catch {
      // All fast parallel methods failed, fall back to yt-dlp
    }

    try {
      const ytDlpResult = await resolveViaYtDlp(videoId);
      if (ytDlpResult) {
        return ytDlpResult;
      }
    } catch (err) {
      console.warn("[StreamResolver] yt-dlp fallback error:", err);
    }

    throw new Error("Không thể trích xuất luồng âm thanh từ video YouTube này. Vui lòng thử lại!");
  })();

  inFlightResolves.set(videoId, task);
  try {
    return await task;
  } finally {
    inFlightResolves.delete(videoId);
  }
}
