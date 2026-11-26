import express from "express";
import cors from "cors";
import path from "path";
import fs from "fs";
import { resolveAudioStream } from "./services/streamResolver.js";
import { searchYouTubeMusicStructured, searchYouTubeTracks, getPlaylistTracks, getRelatedVideos } from "./services/youtubeSearch.js";
import { fetchLyrics } from "./services/lyricsService.js";
import {
  getUserSessionData,
  getUserAvatarBuffer,
  loginWithFirebase,
  registerWithFirebase,
  updateUserProfile,
  logoutUser,
  getUserLibraryData,
  syncCloudUserData,
} from "./services/authService.js";

const app = express();
const PORT = process.env.PORT || 3001;

app.use(cors());
app.use(express.json({ limit: "5mb" }));

// 1. Resolve Audio Stream URL
app.get("/api/stream", async (req, res) => {
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

// 2c. Get Related / Up-Next YouTube Videos for current video
app.get("/api/related", async (req, res) => {
  const videoId = req.query.id as string;
  const fallbackQuery = (req.query.q as string) || "";
  if (!videoId) {
    return res.json({ tracks: [] });
  }

  try {
    const tracks = await getRelatedVideos(videoId, fallbackQuery);
    return res.json({ tracks });
  } catch (err: any) {
    console.error("[API Related Error]", err.message);
    return res.json({ tracks: [] });
  }
});

// 3. Synced Lyrics
app.get("/api/lyrics", async (req, res) => {
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

// 5. User Session & Profile
app.get("/api/user/session", async (req, res) => {
  try {
    const session = await getUserSessionData();
    return res.json(session);
  } catch (err: any) {
    return res.status(500).json({ error: err.message });
  }
});

// 6. User Avatar Stream
app.get("/api/user/avatar", (req, res) => {
  const buf = getUserAvatarBuffer();
  if (buf) {
    res.setHeader("Content-Type", "image/png");
    res.setHeader("Cache-Control", "public, max-age=60");
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
  const { email, password, displayName } = req.body;
  if (!email || !password) {
    return res.status(400).json({ error: "Vui lòng nhập email và mật khẩu" });
  }
  if (String(password).length < 6) {
    return res.status(400).json({ error: "Mật khẩu phải có ít nhất 6 ký tự" });
  }
  try {
    const session = await registerWithFirebase(email, password, displayName);
    return res.json(session);
  } catch (err: any) {
    return res.status(400).json({ error: err.message || "Đăng ký thất bại" });
  }
});

// 7c. Update User Profile (Display Name & Avatar)
app.post("/api/user/profile", async (req, res) => {
  const { name, avatarBase64 } = req.body;
  try {
    const updated = await updateUserProfile(name, avatarBase64);
    return res.json(updated);
  } catch (err: any) {
    return res.status(400).json({ error: err.message || "Cập nhật thông tin thất bại" });
  }
});

// 8. User Logout
app.post("/api/user/logout", (req, res) => {
  logoutUser();
  return res.json({ success: true });
});

// 8b. Sync from Cloud
app.post("/api/user/sync", async (req, res) => {
  try {
    const updatedSession = await syncCloudUserData();
    return res.json(updatedSession);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Đồng bộ thất bại" });
  }
});

// 9. User Library (Favorites, History, Playlists directly from Firestore DB)
app.get("/api/user/library", async (req, res) => {
  try {
    const data = await getUserLibraryData();
    return res.json(data);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to fetch library" });
  }
});

const TRENDING_TOPICS = [
  "nhạc trending việt nam",
  "top hits vpop",
  "nhạc trẻ hot nhất",
  "ballad việt hay nhất",
  "lofi chill việt",
  "us uk top hits",
  "nhạc remix thịnh hành",
  "acoustic việt chill",
];

const RELEASE_TOPICS = [
  "nhạc mới phát hành vpop",
  "mv mới ra mắt",
  "indie việt mới nhất",
  "rap việt hot",
  "nhạc chill buổi tối",
  "kpop hot hits",
  "piano chill thư giãn",
  "nhạc phim hay nhất",
];

function seededHash(str: string): number {
  let h = 2166136261;
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

function seededShuffle<T>(arr: T[], seed: number): T[] {
  const copy = [...arr];
  let s = seed || 1;
  for (let i = copy.length - 1; i > 0; i--) {
    s = (Math.imul(1664525, s) + 1013904223) >>> 0;
    const j = s % (i + 1);
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

let dailyHomeCache: { dateKey: string; trending: any[]; newReleases: any[] } | null = null;

// 10. Home Feed (Daily Randomized Trending & New Releases)
app.get("/api/home", async (req, res) => {
  try {
    const todayKey = new Date().toISOString().slice(0, 10);
    const forceRefresh = req.query.refresh === "1";

    if (!forceRefresh && dailyHomeCache && dailyHomeCache.dateKey === todayKey) {
      return res.json({
        trending: dailyHomeCache.trending,
        newReleases: dailyHomeCache.newReleases,
        dateKey: todayKey,
      });
    }

    const seed = forceRefresh ? Date.now() : seededHash(todayKey);
    const tQuery = TRENDING_TOPICS[seed % TRENDING_TOPICS.length];
    const rQuery = RELEASE_TOPICS[(seed >>> 3) % RELEASE_TOPICS.length];

    const [trendingRes, newReleasesRes] = await Promise.all([
      searchYouTubeMusicStructured(tQuery),
      searchYouTubeMusicStructured(rQuery),
    ]);

    const trending = seededShuffle(trendingRes.tracks || [], seed);
    const newReleases = seededShuffle(newReleasesRes.tracks || [], seed ^ 0x9e3779b9);

    if (!forceRefresh && trending.length > 0) {
      dailyHomeCache = { dateKey: todayKey, trending, newReleases };
    }

    return res.json({
      trending,
      newReleases,
      dateKey: todayKey,
    });
  } catch (err: any) {
    console.error("[Home API Error]", err.message);
    return res.status(500).json({ error: "Failed to load home data" });
  }
});

// Serve frontend dist in standalone non-Vercel production
const clientDist = path.resolve(process.cwd(), "../client/dist");
if (!process.env.VERCEL && fs.existsSync(clientDist)) {
  app.use(express.static(clientDist));
  app.get("*", (req, res) => {
    res.sendFile(path.join(clientDist, "index.html"));
  });
}

if (!process.env.VERCEL) {
  app.listen(PORT, () => {
    console.log(`🎵 [Kahoii Music Server] Running on http://localhost:${PORT}`);
  });
}

export default app;

