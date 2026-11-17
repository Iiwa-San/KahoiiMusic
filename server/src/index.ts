import express from "express";
import cors from "cors";
import path from "path";
import fs from "fs";
import { resolveAudioStream } from "./services/streamResolver.js";
import { searchYouTubeTracks } from "./services/youtubeSearch.js";
import { fetchLyrics } from "./services/lyricsService.js";
import {
  getUserSessionData,
  getUserAvatarBuffer,
  loginWithFirebase,
  logoutUser,
  getUserLibraryData,
  syncCloudUserData,
} from "./services/authService.js";

const app = express();
const PORT = process.env.PORT || 3001;

app.use(cors());
app.use(express.json());

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

// 2. Search Tracks on YouTube
app.get("/api/search", async (req, res) => {
  const query = req.query.q as string;
  if (!query) {
    return res.json({ results: [] });
  }

  try {
    const results = await searchYouTubeTracks(query);
    return res.json({ results });
  } catch (err: any) {
    console.error("[API Search Error]", err.message);
    return res.status(500).json({ error: "Failed to search tracks" });
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

