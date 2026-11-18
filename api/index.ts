import express from "express";
import cors from "cors";
import { resolveAudioStream } from "./services/streamResolver.js";
import { searchYouTubeMusicStructured, getPlaylistTracks } from "./services/youtubeSearch.js";
import { fetchLyrics } from "./services/lyricsService.js";
import {
  extractBearerToken,
  extractRefreshToken,
  getUserSessionData,
  getUserAvatarBuffer,
  loginWithFirebase,
  registerWithFirebase,
  logoutUser,
  getUserLibraryData,
  saveUserLibraryData,
  syncCloudUserData,
  refreshFirebaseToken,
} from "./services/authService.js";

const app = express();

app.use(cors({ origin: true, credentials: true }));
app.use(express.json());

// 1. Resolve Audio Stream URL
app.get(["/api/stream", "/stream"], async (req, res) => {
  const videoId = req.query.id as string;
  if (!videoId) {
    return res.status(400).json({ error: "Missing video ID parameter ('id')" });
  }

  try {
    const streamInfo = await resolveAudioStream(videoId);
    return res.json(streamInfo);
  } catch (err: any) {
    console.error("[API Stream Error]", err.message);
    return res.status(500).json({ error: err.message || "Failed to resolve stream" });
  }
});

// 2. Search Tracks on YouTube (Structured with 100% YouTube Music Tags)
app.get(["/api/search", "/search"], async (req, res) => {
  const query = req.query.q as string;
  if (!query) {
    return res.json({
      query: "",
      topResult: null,
      tracks: [],
      artists: [],
      playlists: [],
      results: [],
    });
  }

  try {
    const data = await searchYouTubeMusicStructured(query);
    return res.json(data);
  } catch (err: any) {
    console.error("[API Search Error]", err.message);
    return res.status(500).json({ error: "Failed to search tracks" });
  }
});

// 2b. Get Playlist Tracks
app.get(["/api/playlist", "/playlist"], async (req, res) => {
  const playlistId = req.query.id as string;
  if (!playlistId) {
    return res.status(400).json({ error: "Missing playlist ID parameter ('id')" });
  }

  try {
    const tracks = await getPlaylistTracks(playlistId);
    return res.json({ tracks });
  } catch (err: any) {
    console.error("[API Playlist Error]", err.message);
    return res.status(500).json({ error: "Failed to load playlist tracks" });
  }
});

// 3. Synced Lyrics
app.get(["/api/lyrics", "/lyrics"], async (req, res) => {
  const title = (req.query.title as string) || "";
  const artist = (req.query.artist as string) || "";
  const duration = req.query.duration ? Number(req.query.duration) : undefined;

  if (!title) {
    return res.json({ syncedLyrics: null, plainLyrics: null, instrumental: false });
  }

  try {
    const lyrics = await fetchLyrics(title, artist, duration);
    return res.json(lyrics);
  } catch (err: any) {
    console.error("[API Lyrics Error]", err.message);
    return res.json({ syncedLyrics: null, plainLyrics: null, instrumental: false });
  }
});

// 4. Stream Proxy (Ensures CORS-free streaming)
app.get(["/api/proxy-audio", "/proxy-audio"], async (req, res) => {
  const targetUrl = req.query.url as string;
  if (!targetUrl) {
    return res.status(400).send("Missing target audio URL");
  }

  try {
    const range = req.headers.range;
    const headers: Record<string, string> = {
      "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    };
    if (range) {
      headers["Range"] = range;
    }

    const response = await fetch(targetUrl, { headers });
    res.status(response.status);

    response.headers.forEach((value, key) => {
      if (["content-type", "content-length", "content-range", "accept-ranges"].includes(key.toLowerCase())) {
        res.setHeader(key, value);
      }
    });

    res.setHeader("Access-Control-Allow-Origin", "*");

    if (!response.body) {
      return res.end();
    }

    const reader = response.body.getReader();
    let isClosed = false;

    req.on("close", () => {
      isClosed = true;
      try {
        reader.cancel();
      } catch {}
    });

    const pump = async () => {
      try {
        while (!isClosed) {
          const { done, value } = await reader.read();
          if (done || isClosed) break;
          res.write(value);
        }
        if (!isClosed) res.end();
      } catch {
        if (!isClosed) res.end();
      }
    };
    pump();
  } catch (err: any) {
    console.error("[Proxy Audio Error]", err.message);
    res.status(500).send("Proxy error");
  }
});

// 4b. Direct Track MP3 Download (Resolves audio stream & pipes as downloadable MP3 file)
app.get(["/api/download-audio", "/download-audio"], async (req, res) => {
  const videoId = req.query.id as string;
  const rawTitle = (req.query.title as string) || "Track";
  const rawAuthor = (req.query.author as string) || "Kahoii Music";
  if (!videoId) return res.status(400).send("Missing video ID");

  try {
    const streamInfo = await resolveAudioStream(videoId);
    if (!streamInfo?.streamUrl) {
      return res.redirect(`https://www.youtube.com/watch?v=${videoId}`);
    }

    const safeFileName = `${rawTitle.replace(/[\\/:*?"<>|]/g, "_")} - ${rawAuthor.replace(/[\\/:*?"<>|]/g, "_")}.mp3`;
    res.setHeader("Content-Disposition", `attachment; filename*=UTF-8''${encodeURIComponent(safeFileName)}`);
    res.setHeader("Content-Type", "audio/mpeg");
    res.setHeader("Access-Control-Allow-Origin", "*");

    const audioRes = await fetch(streamInfo.streamUrl, {
      headers: {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
      },
    });

    if (!audioRes.body) return res.end();
    const reader = audioRes.body.getReader();
    let isClosed = false;
    req.on("close", () => {
      isClosed = true;
      try { reader.cancel(); } catch {}
    });

    while (!isClosed) {
      const { done, value } = await reader.read();
      if (done || isClosed) break;
      res.write(value);
    }
    if (!isClosed) res.end();
  } catch (err: any) {
    return res.redirect(`https://www.youtube.com/watch?v=${videoId}`);
  }
});

// 4c. Latest App Downloads (PC, Android, iOS, Extension - Always latest from GitHub)
let downloadsCache: { data: any; expiry: number } | null = null;

app.get(["/api/downloads", "/downloads"], async (req, res) => {
  const now = Date.now();
  if (downloadsCache && downloadsCache.expiry > now) {
    return res.json(downloadsCache.data);
  }

  try {
    const ghRes = await fetch("https://api.github.com/repos/Iiwa-San/KahoiiMusic/releases", {
      headers: { "User-Agent": "KahoiiMusic-WebApp" },
    });
    if (!ghRes.ok) throw new Error("GitHub Releases fetch failed");
    const releases = (await ghRes.json()) as any[];

    const pcRelease = releases.find((r) => /^v\d+/i.test(r.tag_name));
    const pcAsset = pcRelease?.assets?.find((a: any) => a.name.endsWith(".exe"));

    const androidRelease = releases.find(
      (r) => r.tag_name === "latest-android" || r.assets?.some((a: any) => a.name.endsWith(".apk"))
    );
    const androidAsset = androidRelease?.assets?.find((a: any) => a.name.endsWith(".apk"));

    const iosRelease = releases.find(
      (r) => r.tag_name === "latest-ios" || r.assets?.some((a: any) => a.name.endsWith(".ipa"))
    );
    const iosAsset = iosRelease?.assets?.find((a: any) => a.name.endsWith(".ipa"));

    const extRelease = releases.find((r) =>
      r.assets?.some((a: any) => a.name.toLowerCase().includes("extension") && a.name.endsWith(".zip"))
    );
    const extAsset = extRelease?.assets?.find(
      (a: any) => a.name.toLowerCase().includes("extension") && a.name.endsWith(".zip")
    );

    const formatBytes = (bytes: number) => {
      if (!bytes) return "";
      return (bytes / (1024 * 1024)).toFixed(1) + " MB";
    };

    const data = {
      pc: {
        version: pcRelease?.tag_name || "v1.0.34",
        fileName: pcAsset?.name || "KahoiiMusic_Setup.exe",
        downloadUrl:
          pcAsset?.browser_download_url ||
          "https://github.com/Iiwa-San/KahoiiMusic/releases/download/v1.0.34/KahoiiMusic_Setup_v1.0.34.exe",
        size: pcAsset?.size ? formatBytes(pcAsset.size) : "24.2 MB",
        updatedAt: pcRelease?.published_at || new Date().toISOString(),
      },
      android: {
        version: "Bản mới nhất",
        fileName: androidAsset?.name || "KahoiiMusic.apk",
        downloadUrl:
          androidAsset?.browser_download_url ||
          "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/KahoiiMusic.apk",
        size: androidAsset?.size ? formatBytes(androidAsset.size) : "14.6 MB",
        updatedAt: androidRelease?.published_at || new Date().toISOString(),
      },
      ios: {
        version: "Bản mới nhất",
        fileName: iosAsset?.name || "KahoiiMusic.ipa",
        downloadUrl:
          iosAsset?.browser_download_url ||
          "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-ios/KahoiiMusic.ipa",
        size: iosAsset?.size ? formatBytes(iosAsset.size) : "1.4 MB",
        updatedAt: iosRelease?.published_at || new Date().toISOString(),
      },
      extension: {
        version: "Bản mới nhất",
        fileName: extAsset?.name || "Extension.zip",
        downloadUrl:
          extAsset?.browser_download_url ||
          "https://github.com/Iiwa-San/KahoiiMusic/archive/refs/heads/Extension.zip",
        size: extAsset?.size ? formatBytes(extAsset.size) : "Source ZIP",
        updatedAt: extRelease?.published_at || new Date().toISOString(),
      },
    };

    downloadsCache = { data, expiry: now + 5 * 60 * 1000 };
    return res.json(data);
  } catch (err: any) {
    if (downloadsCache) return res.json(downloadsCache.data);
    return res.json({
      pc: {
        version: "v1.0.34",
        fileName: "KahoiiMusic_Setup_v1.0.34.exe",
        downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/v1.0.34/KahoiiMusic_Setup_v1.0.34.exe",
        size: "24.2 MB",
      },
      android: {
        version: "Bản mới nhất",
        fileName: "KahoiiMusic.apk",
        downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/KahoiiMusic.apk",
        size: "14.6 MB",
      },
      ios: {
        version: "Bản mới nhất",
        fileName: "KahoiiMusic.ipa",
        downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-ios/KahoiiMusic.ipa",
        size: "1.4 MB",
      },
      extension: {
        version: "Bản mới nhất",
        fileName: "Extension.zip",
        downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/archive/refs/heads/Extension.zip",
        size: "Source ZIP",
      },
    });
  }
});

// Helper to set refreshed token headers if auto-refreshed
const attachRefreshedHeaders = (res: any, newIdToken?: string | null, newRefreshToken?: string | null) => {
  if (newIdToken) {
    res.setHeader("x-refreshed-id-token", newIdToken);
  }
  if (newRefreshToken) {
    res.setHeader("x-refreshed-refresh-token", newRefreshToken);
  }
  if (newIdToken || newRefreshToken) {
    res.setHeader("Access-Control-Expose-Headers", "x-refreshed-id-token, x-refreshed-refresh-token");
  }
};

// 4c. Token Refresh Endpoint
app.post(["/api/user/refresh", "/user/refresh"], async (req, res) => {
  try {
    const refreshToken = extractRefreshToken(req);
    if (!refreshToken) return res.status(400).json({ error: "Missing refresh token" });
    const refreshed = await refreshFirebaseToken(refreshToken);
    if (!refreshed) return res.status(401).json({ error: "Invalid refresh token" });
    attachRefreshedHeaders(res, refreshed.idToken, refreshed.refreshToken);
    return res.json(refreshed);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to refresh token" });
  }
});

// 5. User Session & Profile
app.get(["/api/user/session", "/user/session"], async (req, res) => {
  try {
    const token = extractBearerToken(req);
    const refreshToken = extractRefreshToken(req);
    const sessionResult = await getUserSessionData(token, refreshToken);
    attachRefreshedHeaders(res, sessionResult._newIdToken, sessionResult._newRefreshToken);
    const { _newIdToken, _newRefreshToken, ...cleanSession } = sessionResult;
    return res.json(cleanSession);
  } catch (err: any) {
    return res.status(500).json({ error: err.message });
  }
});

// 6. User Avatar Stream
app.get(["/api/user/avatar", "/user/avatar"], async (req, res) => {
  const token = extractBearerToken(req);
  const refreshToken = extractRefreshToken(req);
  const buf = await getUserAvatarBuffer(token, refreshToken);
  if (buf) {
    res.setHeader("Content-Type", "image/png");
    res.setHeader("Cache-Control", "private, max-age=60");
    return res.send(buf);
  }
  return res.status(404).send("No avatar found");
});

// 7. User Login with Firebase
app.post(["/api/user/login", "/user/login"], async (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: "Vui lòng nhập email và mật khẩu" });
  }
  try {
    const session = await loginWithFirebase(email, password);
    return res.json(session);
  } catch (err: any) {
    return res.status(401).json({ error: err.message || "Đăng nhập thất bại" });
  }
});

// 7b. User Register with Firebase
app.post(["/api/user/register", "/user/register"], async (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: "Vui lòng nhập đầy đủ email và mật khẩu" });
  }
  try {
    const session = await registerWithFirebase(email, password);
    return res.json(session);
  } catch (err: any) {
    return res.status(400).json({ error: err.message || "Đăng ký thất bại" });
  }
});

// 8. User Logout
app.post(["/api/user/logout", "/user/logout"], (req, res) => {
  const token = extractBearerToken(req);
  logoutUser(token);
  return res.json({ success: true });
});

// 8b. Sync from Cloud
app.post(["/api/user/sync", "/user/sync"], async (req, res) => {
  try {
    const token = extractBearerToken(req);
    const refreshToken = extractRefreshToken(req);
    const updatedSession = await syncCloudUserData(token, refreshToken);
    attachRefreshedHeaders(res, updatedSession._newIdToken, updatedSession._newRefreshToken);
    const { _newIdToken, _newRefreshToken, ...cleanSession } = updatedSession;
    return res.json(cleanSession);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Đồng bộ thất bại" });
  }
});

// 9. User Library (Favorites, History, Playlists directly from Firestore DB)
app.get(["/api/user/library", "/user/library"], async (req, res) => {
  try {
    const token = extractBearerToken(req);
    const refreshToken = extractRefreshToken(req);
    const result = await getUserLibraryData(token, refreshToken);
    attachRefreshedHeaders(res, result._newIdToken, result._newRefreshToken);
    const { _newIdToken, _newRefreshToken, ...library } = result;
    return res.json(library);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to fetch library" });
  }
});

app.post(["/api/user/library", "/user/library"], async (req, res) => {
  try {
    const token = extractBearerToken(req);
    const refreshToken = extractRefreshToken(req);
    const result = await saveUserLibraryData(req.body, token, refreshToken);
    attachRefreshedHeaders(res, result.newIdToken, result.newRefreshToken);
    return res.json({ success: true });
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to save library" });
  }
});

// 10. Home Feed (Trending & New Releases with Randomization and Dynamic Rotation)
const TRENDING_QUERIES = [
  "nhạc trending việt nam",
  "top bài hát vpop hot",
  "nhạc trẻ hot tiktok thịnh hành",
  "nhạc thịnh hành việt nam",
  "top hits việt nam",
  "nhạc hot triệu view việt nam",
  "bài hát việt nam nghe nhiều nhất",
  "v-pop trending hits",
  "bài hát hot trend tiktok",
  "nhạc edm remix việt nam hot",
  "nhạc lofi chill việt nam",
  "acoustic vpop hay nhất"
];

const NEW_RELEASE_QUERIES = [
  "bài hát mới phát hành vpop",
  "nhạc mới ra mắt vpop",
  "mv mới nhất việt nam",
  "ca khúc mới hay nhất vpop",
  "single mới phát hành việt nam",
  "nhạc việt mới nhất 2024",
  "vpop mới phát hành hot",
  "bài hát mới ra mắt việt nam",
  "nhạc trẻ mới phát hành",
  "nhạc mới phát hành hôm nay"
];

function shuffleArray<T>(array: T[]): T[] {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

app.get(["/api/home", "/home"], async (_req, res) => {
  res.setHeader("Cache-Control", "no-store, no-cache, must-revalidate, proxy-revalidate");
  res.setHeader("Pragma", "no-cache");
  res.setHeader("Expires", "0");
  try {
    const randomTrendingQuery = TRENDING_QUERIES[Math.floor(Math.random() * TRENDING_QUERIES.length)];
    const randomNewReleaseQuery = NEW_RELEASE_QUERIES[Math.floor(Math.random() * NEW_RELEASE_QUERIES.length)];

    const [trendingRes, newReleasesRes] = await Promise.all([
      searchYouTubeMusicStructured(randomTrendingQuery),
      searchYouTubeMusicStructured(randomNewReleaseQuery),
    ]);

    const trendingTracks = shuffleArray(trendingRes.tracks || []);
    const newReleaseTracks = shuffleArray(newReleasesRes.tracks || []);

    return res.json({
      trending: trendingTracks,
      newReleases: newReleaseTracks,
    });
  } catch (err: any) {
    console.error("[Home API Error]", err.message);
    return res.status(500).json({ error: "Failed to load home data" });
  }
});

export default app;

