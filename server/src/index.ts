import express from "express";
import cors from "cors";
import path from "path";
import fs from "fs";
import { resolveAudioStream } from "./services/streamResolver.js";
import { searchYouTubeMusicStructured, searchYouTubeTracks, getPlaylistTracks, getArtistDetails, resolveTrackArtist, getOfficialTrendingTracks, isUnwantedTrack } from "./services/youtubeSearch.js";
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
const PORT = process.env.PORT || 3001;

app.use(cors({
  origin: true,
  credentials: true,
  exposedHeaders: ["x-refreshed-id-token", "x-refreshed-refresh-token"],
}));
app.use(express.json());

// 1. Resolve Audio Stream URL
app.get("/api/stream", async (req, res) => {
  const videoId = req.query.id as string;
  if (!videoId) {
    return res.status(400).json({ error: "Missing video ID parameter ('id')" });
  }

  try {
    const streamInfo = await resolveAudioStream(videoId);
    if (!streamInfo.author || streamInfo.author === "Nghệ sĩ" || streamInfo.author === "Kahoii Music") {
      const resolved = await resolveTrackArtist(videoId, streamInfo.title);
      if (resolved) streamInfo.author = resolved;
    }
    return res.json(streamInfo);
  } catch (err: any) {
    console.warn("[API Stream Fallback]", err.message);
    return res.json({ fallback: true, message: err.message || "Failed to resolve direct stream" });
  }
});

// 2. Search Tracks on YouTube (Structured with 100% YouTube Music Tags)
app.get("/api/search", async (req, res) => {
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
app.get("/api/playlist", async (req, res) => {
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

// 2c. Get Artist Details by browseId (UC...)
app.get("/api/artist", async (req, res) => {
  const id = (req.query.id as string) || "";
  const name = (req.query.name as string) || "Nghệ sĩ";
  if (!id && !name) {
    return res.status(400).json({ error: "Missing artist id or name" });
  }

  try {
    const data = await getArtistDetails(id, name);
    return res.json(data);
  } catch (err: any) {
    console.error("[API Artist Error]", err.message);
    return res.status(500).json({ error: "Failed to fetch artist details" });
  }
});

// 3. Synced Lyrics & Romanization / Translation
app.get("/api/lyrics", async (req, res) => {
  const title = (req.query.title as string) || "";
  let artist = (req.query.artist as string) || "";
  const videoId = (req.query.videoId as string) || (req.query.id as string) || "";
  const duration = req.query.duration ? Number(req.query.duration) : undefined;

  if (!title && !videoId) {
    return res.json({ syncedLyrics: null, plainLyrics: null, instrumental: false, lines: [] });
  }

  if ((!artist || artist === "Nghệ sĩ" || artist === "Kahoii Music") && videoId) {
    const resolved = await resolveTrackArtist(videoId, title);
    if (resolved) artist = resolved;
  }

  const refresh = req.query.refresh === "true";

  try {
    const lyrics = await fetchLyrics(title, artist, duration, videoId, refresh);
    return res.json({ ...lyrics, artist: artist || undefined });
  } catch (err: any) {
    console.error("[API Lyrics Error]", err.message);
    return res.json({ syncedLyrics: null, plainLyrics: null, instrumental: false, lines: [] });
  }
});

// 4. Stream Proxy (Ensures 100% CORS-free, range-request compatible streaming for web audio)
app.get("/api/proxy-audio", async (req, res) => {
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

    // Pipe response stream to express response
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
      } catch (streamErr) {
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
app.get("/api/download-audio", async (req, res) => {
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

app.get("/api/downloads", async (req, res) => {
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
app.post("/api/user/refresh", async (req, res) => {
  try {
    const refreshToken = extractRefreshToken(req);
    if (!refreshToken) return res.status(400).json({ error: "Missing refresh token" });
    const refreshed = await refreshFirebaseToken(refreshToken);
    if (!refreshed) return res.status(401).json({ error: "Invalid refresh token" });
    attachRefreshedHeaders(res, refreshed.idToken, refreshed.refreshToken);
    const nowSec = Math.floor(Date.now() / 1000);
    return res.json({
      ...refreshed,
      token: refreshed.idToken,
      expiryTimestamp: nowSec + refreshed.expiresIn,
    });
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to refresh token" });
  }
});

// 5. User Session & Profile
app.get("/api/user/session", async (req, res) => {
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
app.get("/api/user/avatar", async (req, res) => {
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
app.post("/api/user/login", async (req, res) => {
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
app.post("/api/user/register", async (req, res) => {
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
app.post("/api/user/logout", (req, res) => {
  const token = extractBearerToken(req);
  logoutUser(token);
  return res.json({ success: true });
});

// 8b. Sync from Cloud
app.post("/api/user/sync", async (req, res) => {
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
app.get("/api/user/library", async (req, res) => {
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

app.post("/api/user/library", async (req, res) => {
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

// 10. Home Feed (Official YouTube Music Vietnam Charts & Fresh Releases, 100% Bolero-Free)
const NEW_RELEASE_QUERIES = [
  "single mới vpop",
  "vpop mới phát hành",
  "mv vpop mới nhất",
  "ca khúc vpop mới ra mắt",
  "nhạc trẻ mới phát hành",
  "bài hát mới vpop",
  "hit vpop mới nhất",
];

const CURATED_ARTISTS = [
  { name: "Sơn Tùng M-TP", genre: "Pop / R&B", avatar: "./artists/son_tung.jpg" },
  { name: "Vũ.", genre: "Indie / Acoustic", avatar: "./artists/vu.jpg" },
  { name: "Đen", genre: "Rap / Hip-Hop", avatar: "./artists/den.jpg" },
  { name: "Soobin Hoàng Sơn", genre: "R&B / Ballad", avatar: "./artists/soobin.jpg" },
  { name: "Taylor Swift", genre: "Pop Superstar", avatar: "./artists/taylor_swift.jpg" },
  { name: "The Weeknd", genre: "R&B / Pop", avatar: "./artists/the_weeknd.jpg" },
  { name: "HIEUTHUHAI", genre: "Rap / Hip-Hop", avatar: "https://i.ytimg.com/vi/bY8o44A_aI4/hqdefault.jpg" },
  { name: "Wren Evans", genre: "Pop / R&B", avatar: "https://i.ytimg.com/vi/571_T9gKrqU/hqdefault.jpg" },
  { name: "tuki.", genre: "J-Pop / Acoustic", avatar: "https://i.ytimg.com/vi/kJQP7kiw5Fk/hqdefault.jpg" },
  { name: "MCK", genre: "Hip-Hop / Trap", avatar: "https://i.ytimg.com/vi/Wk9Uj4Qk6q4/hqdefault.jpg" },
  { name: "tlinh", genre: "R&B / Hip-Hop", avatar: "https://i.ytimg.com/vi/lJgK34lZ7Zk/hqdefault.jpg" },
  { name: "Charlie Puth", genre: "Pop / Funk", avatar: "https://i.ytimg.com/vi/3AtDnEC4zak/hqdefault.jpg" },
  { name: "MONO", genre: "Dance-Pop / R&B", avatar: "https://i.ytimg.com/vi/lX_q2rZ5Yh4/hqdefault.jpg" },
  { name: "GREY D", genre: "Pop / Acoustic", avatar: "https://i.ytimg.com/vi/E5t0k7d09m0/hqdefault.jpg" },
  { name: "Phương Ly", genre: "Pop / R&B", avatar: "https://i.ytimg.com/vi/L4a024Wk8c4/hqdefault.jpg" },
  { name: "Hoàng Dũng", genre: "Indie / Ballad", avatar: "https://i.ytimg.com/vi/2v5r8e4k_08/hqdefault.jpg" },
  { name: "Amee", genre: "Teen-Pop / R&B", avatar: "https://i.ytimg.com/vi/K0t2Z4o2r9s/hqdefault.jpg" },
  { name: "Bruno Mars", genre: "Funk / Pop / R&B", avatar: "https://i.ytimg.com/vi/PMivT7MJ41M/hqdefault.jpg" },
  { name: "Billie Eilish", genre: "Alt-Pop / Indie", avatar: "https://i.ytimg.com/vi/4i_T4vPzN5E/hqdefault.jpg" },
  { name: "NewJeans", genre: "K-Pop / R&B", avatar: "https://i.ytimg.com/vi/ArmDp-zijuc/hqdefault.jpg" },
  { name: "Rosé", genre: "K-Pop / Pop", avatar: "https://i.ytimg.com/vi/ekr2nIex040/hqdefault.jpg" },
  { name: "Vũ Cát Tường", genre: "Pop / Ballad", avatar: "https://i.ytimg.com/vi/v5k-8s2Q7fI/hqdefault.jpg" },
  { name: "B Ray", genre: "Rap / Hip-Hop", avatar: "https://i.ytimg.com/vi/N7vX8Zq9j5I/hqdefault.jpg" },
  { name: "JustaTee", genre: "R&B / Hip-Hop", avatar: "https://i.ytimg.com/vi/6jK39tZk8c8/hqdefault.jpg" },
  { name: "Quốc Thiên", genre: "Vocal / Ballad", avatar: "https://i.ytimg.com/vi/7j5h2Y6h8c8/hqdefault.jpg" },
  { name: "Rhymastic", genre: "Rap / Producer", avatar: "https://i.ytimg.com/vi/8j5h2Y6h8c8/hqdefault.jpg" },
  { name: "Phùng Khánh Linh", genre: "City Pop / Synth", avatar: "https://i.ytimg.com/vi/4T9kL1m8Y9I/hqdefault.jpg" },
  { name: "Erik", genre: "Pop / Ballad", avatar: "https://i.ytimg.com/vi/3h2L5T9mK8Y/hqdefault.jpg" },
  { name: "Đức Phúc", genre: "Pop / Ballad", avatar: "https://i.ytimg.com/vi/9mK8Y3h2L5T/hqdefault.jpg" },
  { name: "Hòa Minzy", genre: "Pop / Dân gian đương đại", avatar: "https://i.ytimg.com/vi/5T9mK8Y3h2L/hqdefault.jpg" },
];

function shuffleArray<T>(array: T[]): T[] {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

app.get("/api/home", async (_req, res) => {
  res.setHeader("Cache-Control", "no-store, no-cache, must-revalidate, proxy-revalidate");
  res.setHeader("Pragma", "no-cache");
  res.setHeader("Expires", "0");
  try {
    const randomNewReleaseQuery = NEW_RELEASE_QUERIES[Math.floor(Math.random() * NEW_RELEASE_QUERIES.length)];

    // Fetch official Vietnam Trending Charts & fresh modern releases in parallel
    const [officialTrending, newReleasesRes] = await Promise.all([
      getOfficialTrendingTracks(),
      searchYouTubeMusicStructured(randomNewReleaseQuery),
    ]);

    // 1. Trending tracks: pure official YouTube Music Vietnam Charts (zero bolero)
    const cleanTrending = (officialTrending.tracks || []).filter(
      (t: any) => !isUnwantedTrack(t.title, t.author)
    );
    const trendingTracks = shuffleArray(cleanTrending).slice(0, 30);

    // 2. New releases: filter strictly to remove any bolero / unwanted tracks
    const cleanNewReleases = (newReleasesRes.tracks || []).filter(
      (t: any) => !isUnwantedTrack(t.title, t.author)
    );
    let newReleaseTracks = shuffleArray(cleanNewReleases);
    if (newReleaseTracks.length < 12) {
      const moreTracks = cleanTrending.slice(30);
      newReleaseTracks = [...newReleaseTracks, ...shuffleArray(moreTracks)].slice(0, 24);
    }

    // 3. Dynamic artists from both official charts and new release queries
    const rawArtists: any[] = [
      ...(officialTrending.artists || []),
      ...(newReleasesRes.artists || []),
    ];

    const seenArtistNames = new Set<string>();
    const homeArtists: any[] = [];

    const NON_ARTIST_PATTERNS = [
      "various artists",
      "youtube music",
      "topic",
      "chủ đề",
      "karaoke",
      "sound effect",
      "nhạc bay",
      "nhạc phật",
      "nhật ký",
      "bảng xếp hạng",
      "bxh",
      "nhạc việt ai",
      "production",
      "records",
      "tổng hợp",
      "nhạc sống",
      "nhạc vàng",
      "nhạc trữ tình",
      "nhạc chế",
      "nhạc lofi",
      "thư viện",
      "soundtrack",
      "beat",
      "instrumental",
      "meme media",
      "vie channel",
      "phố nhạc",
      "h2o remix",
      "ss remix",
      "kẻ viết nhạc tình",
      "manhhakyno",
    ];

    for (const a of rawArtists) {
      if (!a.name) continue;
      const lower = a.name.toLowerCase().trim();
      if (
        lower.length < 2 ||
        NON_ARTIST_PATTERNS.some((p) => lower.includes(p)) ||
        isUnwantedTrack("", lower) ||
        seenArtistNames.has(lower)
      ) {
        continue;
      }
      seenArtistNames.add(lower);
      homeArtists.push({
        id: a.id,
        name: a.name,
        genre: a.subscribers || "Nghệ sĩ",
        subscribers: a.subscribers || "Nghệ sĩ",
        avatar: a.thumbnail || (trendingTracks[0]?.thumbnail || ""),
      });
    }

    // Complement with rotating curated pool to guarantee variety and high-res avatars
    const shuffledCatalog = shuffleArray(CURATED_ARTISTS);
    for (const c of shuffledCatalog) {
      const lower = c.name.toLowerCase().trim();
      if (!seenArtistNames.has(lower)) {
        seenArtistNames.add(lower);
        homeArtists.push({
          name: c.name,
          genre: c.genre,
          subscribers: c.genre,
          avatar: c.avatar,
        });
      }
      if (homeArtists.length >= 24) break;
    }

    const finalArtists = shuffleArray(homeArtists).slice(0, 12);

    return res.json({
      trending: trendingTracks,
      newReleases: newReleaseTracks,
      artists: finalArtists,
    });
  } catch (err: any) {
    console.error("[Home API Error]", err.message);
    return res.status(500).json({ error: "Failed to load home data" });
  }
});

// 10. Smart Taste-Based Recommendations
app.all(["/api/recommendations", "/recommendations"], async (req, res) => {
  try {
    const querySeed = (req.body?.querySeed || req.query.q || "") as string;
    const artists = (req.body?.artists || (req.query.artists ? String(req.query.artists).split(",") : [])) as string[];
    const genres = (req.body?.genres || (req.query.genres ? String(req.query.genres).split(",") : [])) as string[];
    const excludeIds = (req.body?.excludeIds || (req.query.exclude ? String(req.query.exclude).split(",") : [])) as string[];
    const excludeSet = new Set<string>(Array.isArray(excludeIds) ? excludeIds : []);

    let searchQueries: string[] = [];
    if (querySeed && typeof querySeed === "string" && querySeed.trim()) {
      searchQueries.push(querySeed.trim());
    }

    if (Array.isArray(artists) && artists.length > 0) {
      artists.slice(0, 2).forEach((a) => {
        if (a && a.trim()) searchQueries.push(`${a.trim()} bài hát hay nhất`);
      });
    }

    if (Array.isArray(genres) && genres.length > 0) {
      genres.slice(0, 2).forEach((g) => {
        if (g && g.trim()) searchQueries.push(`${g.trim()} tuyển chọn hay nhất`);
      });
    }

    if (searchQueries.length === 0) {
      searchQueries = ["Nhạc thịnh hành Việt Nam mới nhất", "V-Pop chill hay nhất"];
    }

    const results = await Promise.all(
      searchQueries.slice(0, 3).map((q) =>
        searchYouTubeMusicStructured(q).catch(() => ({ tracks: [] } as any))
      )
    );

    const seenTrackIds = new Set<string>();
    const recommendedTracks: any[] = [];

    for (const r of results) {
      for (const t of r.tracks || []) {
        if (!seenTrackIds.has(t.id) && !excludeSet.has(t.id) && !isUnwantedTrack(t.title, t.author)) {
          seenTrackIds.add(t.id);
          recommendedTracks.push(t);
        }
      }
    }

    const shuffled = shuffleArray(recommendedTracks).slice(0, 24);
    return res.json({
      tracks: shuffled,
      total: shuffled.length,
    });
  } catch (err: any) {
    console.error("[API Recommendations Error]", err.message);
    return res.status(500).json({ error: "Failed to generate recommendations", tracks: [] });
  }
});

// Serve frontend dist in production
const clientDist = path.resolve(process.cwd(), "../client/dist");
if (fs.existsSync(clientDist)) {
  app.use(express.static(clientDist));
  app.get("*", (req, res) => {
    res.sendFile(path.join(clientDist, "index.html"));
  });
}

app.listen(PORT, () => {
  console.log(`🎵 [Kahoii Music Server] Running on http://localhost:${PORT}`);
});

