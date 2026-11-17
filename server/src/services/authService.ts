import fs from "fs";
import path from "path";

const FIREBASE_API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";
const PROJECT_ID = "kahoiimusic";

function getSavedDir(): string {
  const candidateDirs = [
    path.resolve(process.cwd(), "../../KahoiiMusic/bin/Debug/Saved"),
    path.resolve(process.cwd(), "../../KahoiiMusic/publish/Saved"),
    path.resolve(process.cwd(), "../KahoiiMusic/bin/Debug/Saved"),
    "D:/KahoiiProjects/MusicApp/KahoiiMusic/bin/Debug/Saved",
    "D:/KahoiiProjects/MusicApp/KahoiiMusic/publish/Saved",
  ];

  for (const dir of candidateDirs) {
    if (fs.existsSync(dir)) return dir;
  }
  return path.resolve(process.cwd(), "Saved");
}

function readJsonClean(filePath: string): any {
  try {
    if (!fs.existsSync(filePath)) return null;
    const raw = fs.readFileSync(filePath, "utf8").replace(/^\uFEFF/, "");
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export async function getValidIdToken(session: any): Promise<string | null> {
  if (!session) return null;
  const now = Math.floor(Date.now() / 1000);

  // If token is still valid (with 60s buffer)
  if (session.expiryTimestamp && now < session.expiryTimestamp - 60 && session.idToken) {
    return session.idToken;
  }

  // Refresh token if expired
  if (session.refreshToken) {
    try {
      const refreshUrl = `https://securetoken.googleapis.com/v1/token?key=${FIREBASE_API_KEY}`;
      const params = new URLSearchParams({
        grant_type: "refresh_token",
        refresh_token: session.refreshToken,
      });

      const res = await fetch(refreshUrl, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: params.toString(),
      });

      if (res.ok) {
        const data = await res.json();
        const expiresIn = Number(data.expires_in) || 3600;
        session.idToken = data.id_token;
        session.refreshToken = data.refresh_token || session.refreshToken;
        session.expiryTimestamp = Math.floor(Date.now() / 1000) + expiresIn;

        // Save updated session
        const savedDir = getSavedDir();
        const sessionPath = path.join(savedDir, "user_session.json");
        try {
          fs.writeFileSync(sessionPath, JSON.stringify(session, null, 2), "utf8");
        } catch {}

        return session.idToken;
      }
    } catch (err) {
      console.error("[Auth] Token refresh error:", err);
    }
  }

  return session.idToken || null;
}

export async function getUserSessionData(): Promise<any> {
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);

  if (!session || !session.localId) {
    return {
      isLoggedIn: false,
      user: null,
    };
  }

  // Check valid token & optionally fetch latest Firestore doc
  const token = await getValidIdToken(session);
  let avatarBase64 = session.avatarBase64;

  if (token && session.localId) {
    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${session.localId}?key=${FIREBASE_API_KEY}`;
      const docRes = await fetch(docUrl, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (docRes.ok) {
        const docData = await docRes.json();
        if (docData.fields?.avatarBase64?.stringValue) {
          avatarBase64 = docData.fields.avatarBase64.stringValue;
          session.avatarBase64 = avatarBase64;
        }
      }
    } catch {}
  }

  const favorites = readJsonClean(path.join(savedDir, "favorites.json")) || [];
  const history = readJsonClean(path.join(savedDir, "history.json")) || [];
  const playlists = readJsonClean(path.join(savedDir, "playlists.json")) || [];

  return {
    isLoggedIn: true,
    user: {
      localId: session.localId,
      email: session.email,
      name: session.email ? session.email.split("@")[0] : "Kahoii User",
      avatarUrl: "/api/user/avatar",
      avatarBase64: avatarBase64 ? `data:image/png;base64,${avatarBase64}` : null,
      stats: {
        favoritesCount: Array.isArray(favorites) ? favorites.length : 0,
        historyCount: Array.isArray(history) ? history.length : 0,
        playlistsCount: Array.isArray(playlists) ? playlists.length : 0,
      },
    },
  };
}

export function getUserAvatarBuffer(): Buffer | null {
  const savedDir = getSavedDir();
  const avatarPath = path.join(savedDir, "avatar.png");

  if (fs.existsSync(avatarPath)) {
    return fs.readFileSync(avatarPath);
  }

  const session = readJsonClean(path.join(savedDir, "user_session.json"));
  if (session?.avatarBase64) {
    return Buffer.from(session.avatarBase64, "base64");
  }

  return null;
}

export async function loginWithFirebase(email: string, pass: string): Promise<any> {
  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=${FIREBASE_API_KEY}`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email,
      password: pass,
      returnSecureToken: true,
    }),
  });

  const data = await res.json();
  if (!res.ok) {
    const msg = data.error?.message || "Đăng nhập thất bại";
    if (msg.includes("EMAIL_NOT_FOUND") || msg.includes("INVALID_LOGIN_CREDENTIALS") || msg.includes("INVALID_PASSWORD")) {
      throw new Error("Email hoặc mật khẩu không chính xác.");
    }
    throw new Error(msg);
  }

  const expiresIn = Number(data.expiresIn) || 3600;
  const session = {
    localId: data.localId,
    email: data.email,
    idToken: data.idToken,
    refreshToken: data.refreshToken,
    expiryTimestamp: Math.floor(Date.now() / 1000) + expiresIn,
    rememberMe: true,
    avatarBase64: null as string | null,
  };

  // Fetch avatar and user document from Firestore
  try {
    const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${data.localId}?key=${FIREBASE_API_KEY}`;
    const docRes = await fetch(docUrl, {
      headers: { Authorization: `Bearer ${data.idToken}` },
    });
    if (docRes.ok) {
      const docData = await docRes.json();
      if (docData.fields?.avatarBase64?.stringValue) {
        const b64 = docData.fields.avatarBase64.stringValue as string;
        session.avatarBase64 = b64;
        // Save avatar.png to disk
        const savedDir = getSavedDir();
        if (!fs.existsSync(savedDir)) fs.mkdirSync(savedDir, { recursive: true });
        fs.writeFileSync(path.join(savedDir, "avatar.png"), Buffer.from(b64, "base64"));
      }
    }
  } catch {}

  // Save session
  const savedDir = getSavedDir();
  if (!fs.existsSync(savedDir)) fs.mkdirSync(savedDir, { recursive: true });
  fs.writeFileSync(path.join(savedDir, "user_session.json"), JSON.stringify(session, null, 2), "utf8");

  return await getUserSessionData();
}

export function logoutUser(): void {
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  if (fs.existsSync(sessionPath)) {
    try {
      fs.unlinkSync(sessionPath);
    } catch {}
  }
}

export async function syncCloudUserData(): Promise<any> {
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);

  if (!session || !session.localId) {
    throw new Error("Chưa đăng nhập.");
  }

  const token = await getValidIdToken(session);
  if (!token) {
    throw new Error("Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.");
  }

  const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${session.localId}?key=${FIREBASE_API_KEY}`;
  const docRes = await fetch(docUrl, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!docRes.ok) {
    throw new Error("Không thể kết nối đến Firestore Cloud.");
  }

  const docData = await docRes.json();

  // 1. Sync Avatar
  if (docData.fields?.avatarBase64?.stringValue) {
    const b64 = docData.fields.avatarBase64.stringValue as string;
    session.avatarBase64 = b64;
    fs.writeFileSync(path.join(savedDir, "avatar.png"), Buffer.from(b64, "base64"));
    fs.writeFileSync(sessionPath, JSON.stringify(session, null, 2), "utf8");
  }

  // 2. Sync musicDataJson
  if (docData.fields?.musicDataJson?.stringValue) {
    try {
      const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);

      // Favorites directly from DB
      const favSources = Array.isArray(musicData.favorites) && musicData.favorites.length > 0
        ? musicData.favorites
        : Array.isArray(musicData.savedTracks)
        ? musicData.savedTracks
        : [];
      const mappedFavs = favSources.map((t: any) => ({
        id: t.Id || t.id,
        title: t.Title || t.title,
        duration: t.DurationSeconds || t.duration || 0,
        author: t.Author || t.Artist || t.author || "",
      }));
      fs.writeFileSync(path.join(savedDir, "favorites.json"), JSON.stringify(mappedFavs, null, 2), "utf8");

      // History directly from DB
      if (Array.isArray(musicData.history) && musicData.history.length > 0) {
        const mappedHist = musicData.history.map((t: any) => ({
          id: t.Id || t.id,
          title: t.Title || t.title,
          duration: t.DurationSeconds || t.duration || 0,
          author: t.Author || t.Artist || t.author || "",
        }));
        fs.writeFileSync(path.join(savedDir, "history.json"), JSON.stringify(mappedHist, null, 2), "utf8");
      }

      // Collections / Playlists directly from DB
      const plSources = Array.isArray(musicData.collections) && musicData.collections.length > 0
        ? musicData.collections
        : Array.isArray(musicData.playlists)
        ? musicData.playlists
        : [];
      if (plSources.length > 0) {
        const mappedPlaylists = plSources.map((pl: any) => ({
          id: pl.Id || pl.id,
          name: pl.Name || pl.name || "Danh sách phát",
          created: pl.CreatedAt || pl.created,
          items: (pl.Items || pl.items || []).map((t: any) => ({
            id: t.Id || t.id,
            title: t.Title || t.title,
            duration: t.DurationSeconds || t.duration || 0,
            author: t.Author || t.Artist || t.author || "",
          })),
        }));
        fs.writeFileSync(path.join(savedDir, "playlists.json"), JSON.stringify(mappedPlaylists, null, 2), "utf8");
      }
    } catch (parseErr) {
      console.warn("Failed to parse musicDataJson:", parseErr);
    }
  }

  return await getUserSessionData();
}

export async function getUserLibraryData(): Promise<any> {
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);

  // If user is logged in, try direct live fetch from Firestore Database
  if (session && session.localId) {
    try {
      const token = await getValidIdToken(session);
      if (token) {
        const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${session.localId}?key=${FIREBASE_API_KEY}`;
        const docRes = await fetch(docUrl, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (docRes.ok) {
          const docData = await docRes.json();
          if (docData.fields?.musicDataJson?.stringValue) {
            const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);

            // Favorites directly from DB
            const favSources = Array.isArray(musicData.favorites) && musicData.favorites.length > 0
              ? musicData.favorites
              : Array.isArray(musicData.savedTracks)
              ? musicData.savedTracks
              : [];
            const favorites = favSources.map((t: any) => ({
              id: t.Id || t.id,
              title: t.Title || t.title,
              duration: t.DurationSeconds || t.duration || 0,
              author: t.Author || t.Artist || t.author || "",
            }));

            // History directly from DB
            const history = (musicData.history || []).map((t: any) => ({
              id: t.Id || t.id,
              title: t.Title || t.title,
              duration: t.DurationSeconds || t.duration || 0,
              author: t.Author || t.Artist || t.author || "",
            }));

            // Collections / Playlists directly from DB
            const plSources = Array.isArray(musicData.collections) && musicData.collections.length > 0
              ? musicData.collections
              : Array.isArray(musicData.playlists)
              ? musicData.playlists
              : [];
            const playlists = plSources.map((pl: any) => ({
              id: pl.Id || pl.id,
              name: pl.Name || pl.name || "Danh sách phát",
              created: pl.CreatedAt || pl.created,
              items: (pl.Items || pl.items || []).map((t: any) => ({
                id: t.Id || t.id,
                title: t.Title || t.title,
                duration: t.DurationSeconds || t.duration || 0,
                author: t.Author || t.Artist || t.author || "",
              })),
            }));

            // Update local disk cache
            try {
              fs.writeFileSync(path.join(savedDir, "favorites.json"), JSON.stringify(favorites, null, 2), "utf8");
              fs.writeFileSync(path.join(savedDir, "history.json"), JSON.stringify(history, null, 2), "utf8");
              fs.writeFileSync(path.join(savedDir, "playlists.json"), JSON.stringify(playlists, null, 2), "utf8");
            } catch {}

            return { favorites, history, playlists };
          }
        }
      }
    } catch (err) {
      console.warn("[Direct Firestore DB Fetch Error, fallback to local]:", err);
    }
  }

  // Fallback to local cache if offline or unauthenticated
  const favorites = readJsonClean(path.join(savedDir, "favorites.json")) || [];
  const history = readJsonClean(path.join(savedDir, "history.json")) || [];
  const playlists = readJsonClean(path.join(savedDir, "playlists.json")) || [];

  return {
    favorites: Array.isArray(favorites) ? favorites : [],
    history: Array.isArray(history) ? history : [],
    playlists: Array.isArray(playlists) ? playlists : [],
  };
}
