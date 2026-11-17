import fs from "fs";
import path from "path";

const FIREBASE_API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";
const PROJECT_ID = "kahoiimusic";

export const isProduction = !!process.env.VERCEL || !!process.env.AWS_LAMBDA_FUNCTION_NAME || process.env.NODE_ENV === "production";

export function extractBearerToken(req: any): string | null {
  const authHeader = req.headers?.authorization || req.headers?.Authorization;
  if (typeof authHeader === "string" && authHeader.startsWith("Bearer ")) {
    return authHeader.slice(7).trim();
  }
  if (typeof req.query?.token === "string" && req.query.token) {
    return req.query.token.trim();
  }
  return null;
}

function getSavedDir(): string {
  if (isProduction) {
    const tmpDir = path.join("/tmp", "Saved");
    if (!fs.existsSync(tmpDir)) {
      try {
        fs.mkdirSync(tmpDir, { recursive: true });
      } catch {}
    }
    return tmpDir;
  }

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
  const fallback = path.resolve(process.cwd(), "Saved");
  if (!fs.existsSync(fallback)) {
    try {
      fs.mkdirSync(fallback, { recursive: true });
    } catch {}
  }
  return fallback;
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

export async function verifyFirebaseIdToken(token: string): Promise<{ localId: string; email: string; displayName?: string } | null> {
  if (!token || typeof token !== "string" || token.length < 10) return null;
  try {
    const url = `https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=${FIREBASE_API_KEY}`;
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ idToken: token }),
    });
    if (!res.ok) return null;
    const data = (await res.json()) as any;
    const user = data.users?.[0];
    if (user && user.localId) {
      return {
        localId: user.localId,
        email: user.email || "",
        displayName: user.displayName,
      };
    }
    return null;
  } catch {
    return null;
  }
}

export async function getValidIdToken(session: any): Promise<string | null> {
  if (!session) return null;
  const now = Math.floor(Date.now() / 1000);

  if (session.expiryTimestamp && now < session.expiryTimestamp - 60 && session.idToken) {
    return session.idToken;
  }

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

        if (!isProduction) {
          const savedDir = getSavedDir();
          const sessionPath = path.join(savedDir, "user_session.json");
          try {
            fs.writeFileSync(sessionPath, JSON.stringify(session, null, 2), "utf8");
          } catch {}
        }

        return session.idToken;
      }
    } catch (err) {
      console.error("[Auth] Token refresh error:", err);
    }
  }

  return session.idToken || null;
}

export async function getUserSessionData(clientToken?: string | null): Promise<any> {
  // 1. If client provided a Bearer token, look up that specific user securely from Firebase
  if (clientToken) {
    const verified = await verifyFirebaseIdToken(clientToken);
    if (verified) {
      let avatarBase64: string | null = null;
      let favCount = 0;
      let histCount = 0;
      let plCount = 0;

      try {
        const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
        const docRes = await fetch(docUrl, {
          headers: { Authorization: `Bearer ${clientToken}` },
        });
        if (docRes.ok) {
          const docData = (await docRes.json()) as any;
          if (docData.fields?.avatarBase64?.stringValue) {
            avatarBase64 = docData.fields.avatarBase64.stringValue;
          }
          if (docData.fields?.musicDataJson?.stringValue) {
            try {
              const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);
              const favs = Array.isArray(musicData.favorites) ? musicData.favorites : Array.isArray(musicData.savedTracks) ? musicData.savedTracks : [];
              const hist = Array.isArray(musicData.history) ? musicData.history : [];
              const pls = Array.isArray(musicData.collections) ? musicData.collections : Array.isArray(musicData.playlists) ? musicData.playlists : [];
              favCount = favs.length;
              histCount = hist.length;
              plCount = pls.length;
            } catch {}
          }
        }
      } catch {}

      return {
        isLoggedIn: true,
        user: {
          localId: verified.localId,
          email: verified.email,
          name: verified.displayName || (verified.email ? verified.email.split("@")[0] : "Kahoii User"),
          avatarUrl: avatarBase64 ? `data:image/png;base64,${avatarBase64}` : "/api/user/avatar",
          avatarBase64: avatarBase64 || null,
          stats: {
            favoritesCount: favCount,
            historyCount: histCount,
            playlistsCount: plCount,
          },
        },
      };
    }
  }

  // 2. If in production or on Vercel, unauthenticated visitor NEVER sees any session
  if (isProduction) {
    return {
      isLoggedIn: false,
      user: null,
    };
  }

  // 3. In local development ONLY:
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);
  if (!session || !session.localId) {
    return {
      isLoggedIn: false,
      user: null,
    };
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
      avatarBase64: session.avatarBase64 ? `data:image/png;base64,${session.avatarBase64}` : null,
      stats: {
        favoritesCount: Array.isArray(favorites) ? favorites.length : 0,
        historyCount: Array.isArray(history) ? history.length : 0,
        playlistsCount: Array.isArray(playlists) ? playlists.length : 0,
      },
    },
  };
}

export async function getUserAvatarBuffer(clientToken?: string | null): Promise<Buffer | null> {
  if (clientToken) {
    const verified = await verifyFirebaseIdToken(clientToken);
    if (verified) {
      try {
        const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
        const docRes = await fetch(docUrl, {
          headers: { Authorization: `Bearer ${clientToken}` },
        });
        if (docRes.ok) {
          const docData = await docRes.json();
          if (docData.fields?.avatarBase64?.stringValue) {
            return Buffer.from(docData.fields.avatarBase64.stringValue, "base64");
          }
        }
      } catch {}
    }
  }

  if (!isProduction) {
    const savedDir = getSavedDir();
    const avatarPath = path.join(savedDir, "avatar.png");
    if (fs.existsSync(avatarPath)) {
      try {
        return fs.readFileSync(avatarPath);
      } catch {}
    }
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
    if (msg.includes("USER_DISABLED")) {
      throw new Error("Tài khoản này đã bị tạm khóa.");
    }
    throw new Error(msg);
  }

  const expiresIn = Number(data.expiresIn) || 3600;
  let avatarBase64: string | null = null;
  let favCount = 0;
  let histCount = 0;
  let plCount = 0;

  try {
    const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${data.localId}?key=${FIREBASE_API_KEY}`;
    const docRes = await fetch(docUrl, {
      headers: { Authorization: `Bearer ${data.idToken}` },
    });
    if (docRes.ok) {
      const docData = await docRes.json();
      if (docData.fields?.avatarBase64?.stringValue) {
        avatarBase64 = docData.fields.avatarBase64.stringValue;
      }
      if (docData.fields?.musicDataJson?.stringValue) {
        try {
          const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);
          const favs = Array.isArray(musicData.favorites) ? musicData.favorites : Array.isArray(musicData.savedTracks) ? musicData.savedTracks : [];
          const hist = Array.isArray(musicData.history) ? musicData.history : [];
          const pls = Array.isArray(musicData.collections) ? musicData.collections : Array.isArray(musicData.playlists) ? musicData.playlists : [];
          favCount = favs.length;
          histCount = hist.length;
          plCount = pls.length;
        } catch {}
      }
    }
  } catch {}

  const session = {
    isLoggedIn: true,
    user: {
      localId: data.localId,
      email: data.email,
      name: data.displayName || (data.email ? data.email.split("@")[0] : "Kahoii User"),
      avatarUrl: avatarBase64 ? `data:image/png;base64,${avatarBase64}` : "/api/user/avatar",
      avatarBase64: avatarBase64 || null,
      idToken: data.idToken,
      refreshToken: data.refreshToken,
      expiryTimestamp: Math.floor(Date.now() / 1000) + expiresIn,
      stats: {
        favoritesCount: favCount,
        historyCount: histCount,
        playlistsCount: plCount,
      },
    },
  };

  // In local development ONLY: also save to local Saved/ folder
  if (!isProduction) {
    try {
      const savedDir = getSavedDir();
      if (!fs.existsSync(savedDir)) fs.mkdirSync(savedDir, { recursive: true });
      fs.writeFileSync(path.join(savedDir, "user_session.json"), JSON.stringify({
        localId: data.localId,
        email: data.email,
        idToken: data.idToken,
        refreshToken: data.refreshToken,
        expiryTimestamp: Math.floor(Date.now() / 1000) + expiresIn,
        avatarBase64: avatarBase64 || null,
      }, null, 2), "utf8");
      if (avatarBase64) {
        fs.writeFileSync(path.join(savedDir, "avatar.png"), Buffer.from(avatarBase64, "base64"));
      }
    } catch {}
  }

  return session;
}

export async function registerWithFirebase(email: string, pass: string): Promise<any> {
  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${FIREBASE_API_KEY}`;
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
    const msg = data.error?.message || "Đăng ký thất bại";
    if (msg.includes("EMAIL_EXISTS")) {
      throw new Error("Email này đã được sử dụng. Vui lòng đăng nhập hoặc chọn email khác.");
    }
    if (msg.includes("WEAK_PASSWORD")) {
      throw new Error("Mật khẩu quá yếu (yêu cầu tối thiểu 6 ký tự).");
    }
    if (msg.includes("INVALID_EMAIL")) {
      throw new Error("Định dạng email không hợp lệ.");
    }
    throw new Error(msg);
  }

  const expiresIn = Number(data.expiresIn) || 3600;

  // Create initial user document in Firestore
  try {
    const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${data.localId}?key=${FIREBASE_API_KEY}`;
    await fetch(docUrl, {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${data.idToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        fields: {
          email: { stringValue: email },
          createdAt: { stringValue: new Date().toISOString() },
          musicDataJson: {
            stringValue: JSON.stringify({
              favorites: [],
              history: [],
              playlists: [],
              collections: [],
            }),
          },
        },
      }),
    });
  } catch (err) {
    console.warn("Init user Firestore doc error:", err);
  }

  const session = {
    isLoggedIn: true,
    user: {
      localId: data.localId,
      email: data.email,
      name: data.email ? data.email.split("@")[0] : "Kahoii User",
      avatarUrl: "/api/user/avatar",
      avatarBase64: null,
      idToken: data.idToken,
      refreshToken: data.refreshToken,
      expiryTimestamp: Math.floor(Date.now() / 1000) + expiresIn,
      stats: {
        favoritesCount: 0,
        historyCount: 0,
        playlistsCount: 0,
      },
    },
  };

  // In local development ONLY: also save to local Saved/ folder
  if (!isProduction) {
    try {
      const savedDir = getSavedDir();
      if (!fs.existsSync(savedDir)) fs.mkdirSync(savedDir, { recursive: true });
      fs.writeFileSync(path.join(savedDir, "user_session.json"), JSON.stringify(session.user, null, 2), "utf8");
      fs.writeFileSync(path.join(savedDir, "favorites.json"), "[]", "utf8");
      fs.writeFileSync(path.join(savedDir, "history.json"), "[]", "utf8");
      fs.writeFileSync(path.join(savedDir, "playlists.json"), "[]", "utf8");
    } catch {}
  }

  return session;
}

export function logoutUser(clientToken?: string | null): void {
  if (!isProduction) {
    const savedDir = getSavedDir();
    const sessionPath = path.join(savedDir, "user_session.json");
    if (fs.existsSync(sessionPath)) {
      try {
        fs.unlinkSync(sessionPath);
      } catch {}
    }
  }
}

export async function syncCloudUserData(clientToken?: string | null): Promise<any> {
  if (clientToken) {
    return await getUserSessionData(clientToken);
  }
  if (isProduction) {
    throw new Error("Chưa đăng nhập.");
  }
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);
  if (!session || !session.localId) {
    throw new Error("Chưa đăng nhập.");
  }
  return await getUserSessionData();
}

export async function getUserLibraryData(clientToken?: string | null): Promise<any> {
  // 1. If client provided a Bearer token, fetch that specific user's Firestore document
  if (clientToken) {
    const verified = await verifyFirebaseIdToken(clientToken);
    if (verified) {
      try {
        const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
        const docRes = await fetch(docUrl, {
          headers: { Authorization: `Bearer ${clientToken}` },
        });
        if (docRes.ok) {
          const docData = (await docRes.json()) as any;
          if (docData.fields?.musicDataJson?.stringValue) {
            const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);
            const favSources = Array.isArray(musicData.favorites) && musicData.favorites.length > 0
              ? musicData.favorites
              : Array.isArray(musicData.savedTracks)
              ? musicData.savedTracks
              : [];
            const favorites = favSources.map((t: any) => ({
              id: t.Id || t.id,
              title: t.Title || t.title,
              duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
              author: t.Author || t.Artist || t.author || "",
            }));

            const histSources = Array.isArray(musicData.history) ? musicData.history : [];
            const history = histSources.map((t: any) => ({
              id: t.Id || t.id,
              title: t.Title || t.title,
              duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
              author: t.Author || t.Artist || t.author || "",
            }));

            const plSources = Array.isArray(musicData.collections) && musicData.collections.length > 0
              ? musicData.collections
              : Array.isArray(musicData.playlists)
              ? musicData.playlists
              : [];
            const playlists = plSources.map((pl: any) => ({
              id: pl.Id || pl.id || pl.Name || pl.name,
              name: pl.Name || pl.name,
              tracks: (pl.Tracks || pl.tracks || []).map((t: any) => ({
                id: t.Id || t.id,
                title: t.Title || t.title,
                duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
                author: t.Author || t.Artist || t.author || "",
              })),
            }));

            return { favorites, history, playlists };
          }
        }
      } catch (err) {
        console.warn("[getUserLibraryData Firestore Error]", err);
      }
    }
  }

  // 2. If in production or unauthenticated guest:
  if (isProduction || !clientToken) {
    return {
      favorites: [],
      history: [],
      playlists: [],
    };
  }

  // 3. In local development ONLY:
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);
  if (!session || !session.localId) {
    return { favorites: [], history: [], playlists: [] };
  }

  const favorites = readJsonClean(path.join(savedDir, "favorites.json")) || [];
  const history = readJsonClean(path.join(savedDir, "history.json")) || [];
  const playlists = readJsonClean(path.join(savedDir, "playlists.json")) || [];

  return { favorites, history, playlists };
}

export async function saveUserLibraryData(
  data: { favorites?: any[]; history?: any[]; playlists?: any[] },
  clientToken?: string | null
): Promise<void> {
  const favorites = Array.isArray(data.favorites) ? data.favorites : [];
  const history = Array.isArray(data.history) ? data.history : [];
  const playlists = Array.isArray(data.playlists) ? data.playlists : [];

  // 1. If client provided a Bearer token, save directly to user's Firestore document
  if (clientToken) {
    const verified = await verifyFirebaseIdToken(clientToken);
    if (verified) {
      try {
        const musicDataJson = JSON.stringify({
          favorites,
          history,
          playlists,
          collections: playlists,
        });

        const patchUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?updateMask.fieldPaths=musicDataJson&key=${FIREBASE_API_KEY}`;
        await fetch(patchUrl, {
          method: "PATCH",
          headers: {
            Authorization: `Bearer ${clientToken}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            fields: {
              musicDataJson: {
                stringValue: musicDataJson,
              },
            },
          }),
        });
        return;
      } catch (err) {
        console.warn("[Save Library to Firestore Error]", err);
      }
    }
  }

  // 2. In local development ONLY:
  if (!isProduction) {
    const savedDir = getSavedDir();
    try {
      fs.writeFileSync(path.join(savedDir, "favorites.json"), JSON.stringify(favorites, null, 2), "utf8");
      fs.writeFileSync(path.join(savedDir, "history.json"), JSON.stringify(history, null, 2), "utf8");
      fs.writeFileSync(path.join(savedDir, "playlists.json"), JSON.stringify(playlists, null, 2), "utf8");
    } catch {}
  }
}
