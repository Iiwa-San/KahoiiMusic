import express from "express";
import cors from "cors";
import { resolveAudioStream } from "./services/streamResolver.js";
import { searchYouTubeMusicStructured, getPlaylistTracks } from "./services/youtubeSearch.js";
import { fetchLyrics } from "./services/lyricsService.js";
import {
  getUserSessionData,
  getUserAvatarBuffer,
  loginWithFirebase,
  logoutUser,
  getUserLibraryData,
  saveUserLibraryData,
  syncCloudUserData,
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

// 5. User Session & Profile
app.get(["/api/user/session", "/user/session"], async (_req, res) => {
  try {
    const session = await getUserSessionData();
    return res.json(session);
  } catch (err: any) {
    return res.status(500).json({ error: err.message });
  }
});

// 6. User Avatar Stream
app.get(["/api/user/avatar", "/user/avatar"], (_req, res) => {
  const buf = getUserAvatarBuffer();
  if (buf) {
    res.setHeader("Content-Type", "image/png");
    res.setHeader("Cache-Control", "public, max-age=60");
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

// 8. User Logout
app.post(["/api/user/logout", "/user/logout"], (_req, res) => {
  logoutUser();
  return res.json({ success: true });
});

// 8b. Sync from Cloud
app.post(["/api/user/sync", "/user/sync"], async (_req, res) => {
  try {
    const updatedSession = await syncCloudUserData();
    return res.json(updatedSession);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Đồng bộ thất bại" });
  }
});

// 9. User Library (Favorites, History, Playlists directly from Firestore DB)
app.get(["/api/user/library", "/user/library"], async (_req, res) => {
  try {
    const data = await getUserLibraryData();
    return res.json(data);
  } catch (err: any) {
    return res.status(500).json({ error: err.message || "Failed to fetch library" });
  }
});

app.post(["/api/user/library", "/user/library"], async (req, res) => {
  try {
    await saveUserLibraryData(req.body);
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

