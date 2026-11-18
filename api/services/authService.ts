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

export function extractRefreshToken(req: any): string | null {
  const refHeader = req.headers?.["x-refresh-token"] || req.headers?.["X-Refresh-Token"];
  if (typeof refHeader === "string" && refHeader) {
    return refHeader.trim();
  }
  if (typeof req.body?.refreshToken === "string" && req.body.refreshToken) {
    return req.body.refreshToken.trim();
  }
  if (typeof req.query?.refreshToken === "string" && req.query.refreshToken) {
    return req.query.refreshToken.trim();
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

export async function refreshFirebaseToken(refreshToken: string): Promise<{ idToken: string; refreshToken: string; expiresIn: number; localId?: string } | null> {
  if (!refreshToken || typeof refreshToken !== "string" || refreshToken.length < 10) return null;
  try {
    const refreshUrl = `https://securetoken.googleapis.com/v1/token?key=${FIREBASE_API_KEY}`;
    const params = new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: refreshToken,
    });
    const res = await fetch(refreshUrl, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: params.toString(),
    });
    if (!res.ok) return null;
    const data = await res.json();
    return {
      idToken: data.id_token,
      refreshToken: data.refresh_token || refreshToken,
      expiresIn: Number(data.expires_in) || 3600,
      localId: data.user_id,
    };
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
      const refreshed = await refreshFirebaseToken(session.refreshToken);
      if (refreshed) {
        session.idToken = refreshed.idToken;
        session.refreshToken = refreshed.refreshToken;
        session.expiryTimestamp = Math.floor(Date.now() / 1000) + refreshed.expiresIn;

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

export async function getVerifiedUserAndTokens(
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<{
  verified: { localId: string; email: string; displayName?: string } | null;
  idToken: string | null;
  newRefreshToken?: string | null;
}> {
  let token = clientToken || null;

  // 1. If we have a token, check if it's currently valid
  if (token) {
    const verified = await verifyFirebaseIdToken(token);
    if (verified) {
      return { verified, idToken: token };
    }
  }

  // 2. If token verification failed or no token, attempt refresh with clientRefreshToken
  let refreshToken = clientRefreshToken || null;
  if (!refreshToken && !isProduction) {
    const savedDir = getSavedDir();
    const session = readJsonClean(path.join(savedDir, "user_session.json"));
    if (session?.refreshToken) {
      refreshToken = session.refreshToken;
    }
  }

  if (refreshToken) {
    const refreshed = await refreshFirebaseToken(refreshToken);
    if (refreshed?.idToken) {
      let verified = await verifyFirebaseIdToken(refreshed.idToken);
      if (!verified && refreshed.localId) {
        let email = "";
        if (!isProduction) {
          try {
            const savedDir = getSavedDir();
            const session = readJsonClean(path.join(savedDir, "user_session.json"));
            email = session?.email || "";
          } catch {}
        }
        verified = { localId: refreshed.localId, email };
      }
      if (verified) {
        if (!isProduction) {
          const savedDir = getSavedDir();
          const sessionPath = path.join(savedDir, "user_session.json");
          const session = readJsonClean(sessionPath) || {};
          session.idToken = refreshed.idToken;
          session.refreshToken = refreshed.refreshToken;
          session.expiryTimestamp = Math.floor(Date.now() / 1000) + refreshed.expiresIn;
          try {
            fs.writeFileSync(sessionPath, JSON.stringify(session, null, 2), "utf8");
          } catch {}
        }
        return {
          verified,
          idToken: refreshed.idToken,
          newRefreshToken: refreshed.refreshToken,
        };
      }
    }
  }

  return { verified: null, idToken: null };
}

export async function getUserSessionData(
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<any> {
  // 1. If client provided a Bearer token or refresh token, look up that specific user securely from Firebase
  const { verified, idToken, newRefreshToken } = await getVerifiedUserAndTokens(clientToken, clientRefreshToken);
  if (verified && idToken) {
    let avatarBase64: string | null = null;
    let favCount = 0;
    let savedCount = 0;
    let histCount = 0;
    let plCount = 0;

    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
      const docRes = await fetch(docUrl, {
        headers: { Authorization: `Bearer ${idToken}` },
      });
      if (docRes.ok) {
        const docData = (await docRes.json()) as any;
        if (docData.fields?.avatarBase64?.stringValue) {
          avatarBase64 = docData.fields.avatarBase64.stringValue;
        }
        if (docData.fields?.musicDataJson?.stringValue) {
          try {
            const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);
            const favs = Array.isArray(musicData.favorites)
              ? musicData.favorites
              : Array.isArray(musicData.Favorites)
              ? musicData.Favorites
              : [];
            const saved = Array.isArray(musicData.savedTracks)
              ? musicData.savedTracks
              : Array.isArray(musicData.SavedTracks)
              ? musicData.SavedTracks
              : [];
            const hist = Array.isArray(musicData.history)
              ? musicData.history
              : Array.isArray(musicData.History)
              ? musicData.History
              : [];
            const pls = Array.isArray(musicData.collections)
              ? musicData.collections
              : Array.isArray(musicData.Collections)
              ? musicData.Collections
              : Array.isArray(musicData.playlists)
              ? musicData.playlists
              : [];
            favCount = favs.length;
            savedCount = saved.length;
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
        idToken,
        refreshToken: newRefreshToken || clientRefreshToken || undefined,
        stats: {
          favoritesCount: favCount,
          savedTracksCount: savedCount,
          historyCount: histCount,
          playlistsCount: plCount,
        },
      },
      _newIdToken: newRefreshToken ? idToken : null,
      _newRefreshToken: newRefreshToken || null,
    };
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

  // Ensure idToken is fresh before returning to client
  const validIdToken = await getValidIdToken(session);

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
      idToken: validIdToken || session.idToken,
      refreshToken: session.refreshToken,
      stats: {
        favoritesCount: Array.isArray(favorites) ? favorites.length : 0,
        historyCount: Array.isArray(history) ? history.length : 0,
        playlistsCount: Array.isArray(playlists) ? playlists.length : 0,
      },
    },
    _newIdToken: validIdToken && validIdToken !== session.idToken ? validIdToken : null,
    _newRefreshToken: session.refreshToken || null,
  };
}

export async function getUserAvatarBuffer(
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<Buffer | null> {
  const { verified, idToken } = await getVerifiedUserAndTokens(clientToken, clientRefreshToken);
  if (verified && idToken) {
    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
      const docRes = await fetch(docUrl, {
        headers: { Authorization: `Bearer ${idToken}` },
      });
      if (docRes.ok) {
        const docData = await docRes.json();
        if (docData.fields?.avatarBase64?.stringValue) {
          return Buffer.from(docData.fields.avatarBase64.stringValue, "base64");
        }
      }
    } catch {}
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

export async function syncCloudUserData(
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<any> {
  if (clientToken || clientRefreshToken) {
    return await getUserSessionData(clientToken, clientRefreshToken);
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

export async function getUserLibraryData(
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<{
  favorites: any[];
  savedTracks: any[];
  history: any[];
  playlists: any[];
  _newIdToken?: string | null;
  _newRefreshToken?: string | null;
}> {
  // 1. If client provided a Bearer token or refresh token, fetch that specific user's Firestore document
  const { verified, idToken, newRefreshToken } = await getVerifiedUserAndTokens(clientToken, clientRefreshToken);
  if (verified && idToken) {
    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;
      const docRes = await fetch(docUrl, {
        headers: { Authorization: `Bearer ${idToken}` },
      });
      if (docRes.ok) {
        const docData = (await docRes.json()) as any;
        if (docData.fields?.musicDataJson?.stringValue) {
          const musicData = JSON.parse(docData.fields.musicDataJson.stringValue);

          // Build local author lookup map if in local environment to backfill authentic authors
          const localAuthorMap = new Map<string, string>();
          if (!isProduction) {
            try {
              const savedDir = getSavedDir();
              const localFavs = readJsonClean(path.join(savedDir, "favorites.json")) || [];
              const localHist = readJsonClean(path.join(savedDir, "history.json")) || [];
              [...localFavs, ...localHist].forEach((item: any) => {
                const iId = item.Id || item.id;
                const iAuthor = item.Author || item.Artist || item.author;
                if (iId && iAuthor && iAuthor !== "Nghệ sĩ" && iAuthor !== "Kahoii Music" && iAuthor !== "YouTube Music") {
                  localAuthorMap.set(iId, iAuthor);
                }
              });
            } catch {}
          }

          const resolveAuthor = (t: any): string => {
            const id = t.Id || t.id;
            const explicit = (t.Author || t.Artist || t.author || t.channel || t.uploader || "").trim();
            if (explicit && explicit !== "Nghệ sĩ" && explicit !== "Kahoii Music" && explicit !== "YouTube Music") {
              return explicit;
            }
            if (id && localAuthorMap.has(id)) {
              return localAuthorMap.get(id)!;
            }
            return "";
          };

          const favSources = Array.isArray(musicData.favorites)
            ? musicData.favorites
            : Array.isArray(musicData.Favorites)
            ? musicData.Favorites
            : [];
          const favorites = favSources.map((t: any) => ({
            id: t.Id || t.id,
            title: t.Title || t.title,
            duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
            durationText: t.durationText,
            author: resolveAuthor(t),
            thumbnail: t.thumbnail || `https://i.ytimg.com/vi/${t.Id || t.id}/mqdefault.jpg`,
          }));

          const savedSources = Array.isArray(musicData.savedTracks)
            ? musicData.savedTracks
            : Array.isArray(musicData.SavedTracks)
            ? musicData.SavedTracks
            : [];
          const savedTracks = savedSources.map((t: any) => ({
            id: t.Id || t.id,
            title: t.Title || t.title,
            duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
            durationText: t.durationText,
            author: resolveAuthor(t),
            thumbnail: t.thumbnail || `https://i.ytimg.com/vi/${t.Id || t.id}/mqdefault.jpg`,
          }));

          const histSources = Array.isArray(musicData.history)
            ? musicData.history
            : Array.isArray(musicData.History)
            ? musicData.History
            : [];
          const history = histSources.map((t: any) => ({
            id: t.Id || t.id,
            title: t.Title || t.title,
            duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
            durationText: t.durationText,
            author: resolveAuthor(t),
            thumbnail: t.thumbnail || `https://i.ytimg.com/vi/${t.Id || t.id}/mqdefault.jpg`,
          }));

          const plSources = Array.isArray(musicData.collections) && musicData.collections.length > 0
            ? musicData.collections
            : Array.isArray(musicData.Collections) && musicData.Collections.length > 0
            ? musicData.Collections
            : Array.isArray(musicData.playlists)
            ? musicData.playlists
            : [];
          const playlists = plSources.map((pl: any) => {
            const rawItems = pl.Items || pl.items || pl.Tracks || pl.tracks || [];
            const mapped = rawItems.map((t: any) => ({
              id: t.Id || t.id,
              title: t.Title || t.title,
              duration: Number(t.DurationSeconds ?? t.duration ?? t.durationSeconds ?? t.Duration ?? 0),
              durationText: t.durationText,
              author: resolveAuthor(t),
              thumbnail: t.thumbnail || `https://i.ytimg.com/vi/${t.Id || t.id}/mqdefault.jpg`,
            }));
            return {
              id: pl.Id || pl.id || pl.Name || pl.name,
              name: pl.Name || pl.name,
              created: pl.CreatedAt || pl.created,
              items: mapped,
              tracks: mapped,
            };
          });

          return {
            favorites,
            savedTracks,
            history,
            playlists,
            _newIdToken: newRefreshToken ? idToken : null,
            _newRefreshToken: newRefreshToken || null,
          };
        }
      }
    } catch (err) {
      console.warn("[getUserLibraryData Firestore Error]", err);
    }
  }

  // 2. If in production and not authenticated:
  if (isProduction) {
    return {
      favorites: [],
      savedTracks: [],
      history: [],
      playlists: [],
    };
  }

  // 3. In local development ONLY:
  const savedDir = getSavedDir();
  const sessionPath = path.join(savedDir, "user_session.json");
  const session = readJsonClean(sessionPath);
  if (!session || !session.localId) {
    return { favorites: [], savedTracks: [], history: [], playlists: [] };
  }

  const favorites = readJsonClean(path.join(savedDir, "favorites.json")) || [];
  const savedTracks = readJsonClean(path.join(savedDir, "saved_tracks.json")) || [];
  const history = readJsonClean(path.join(savedDir, "history.json")) || [];
  const playlists = readJsonClean(path.join(savedDir, "playlists.json")) || [];

  return { favorites, savedTracks, history, playlists };
}

export async function saveUserLibraryData(
  data: { favorites?: any[]; history?: any[]; playlists?: any[]; savedTracks?: any[] },
  clientToken?: string | null,
  clientRefreshToken?: string | null
): Promise<{ success: boolean; newIdToken?: string | null; newRefreshToken?: string | null }> {
  const favorites = Array.isArray(data.favorites) ? data.favorites : [];
  const history = Array.isArray(data.history) ? data.history : [];
  const playlists = Array.isArray(data.playlists) ? data.playlists : [];
  const savedTracks = Array.isArray(data.savedTracks) ? data.savedTracks : [];

  // 1. If client provided a Bearer token or refresh token, save directly to user's Firestore document
  const { verified, idToken, newRefreshToken } = await getVerifiedUserAndTokens(clientToken, clientRefreshToken);
  if (verified && idToken) {
    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${verified.localId}?key=${FIREBASE_API_KEY}`;

      let finalSavedTracks = savedTracks;
      if (data.savedTracks === undefined) {
        // Preserve existing savedTracks from cloud if not provided in payload
        try {
          const checkRes = await fetch(docUrl, { headers: { Authorization: `Bearer ${idToken}` } });
          if (checkRes.ok) {
            const checkData = (await checkRes.json()) as any;
            if (checkData.fields?.musicDataJson?.stringValue) {
              const parsed = JSON.parse(checkData.fields.musicDataJson.stringValue);
              if (Array.isArray(parsed.savedTracks)) finalSavedTracks = parsed.savedTracks;
              else if (Array.isArray(parsed.SavedTracks)) finalSavedTracks = parsed.SavedTracks;
            }
          }
        } catch {}
      }

      const collections = playlists.map((pl: any) => ({
        Id: pl.id || pl.Id,
        Name: pl.name || pl.Name,
        CreatedAt: pl.created || pl.CreatedAt || Date.now(),
        Items: (pl.items || pl.tracks || []).map((t: any) => ({
          Id: t.id || t.Id,
          Title: t.title || t.Title,
          Author: t.author || t.Author || t.Artist || "",
          Duration: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
          DurationSeconds: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
        })),
      }));

      const musicDataJson = JSON.stringify({
        favorites: favorites.map((t: any) => ({
          Id: t.id || t.Id,
          Title: t.title || t.Title,
          Author: t.author || t.Author || t.Artist || "",
          Duration: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
          DurationSeconds: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
        })),
        savedTracks: finalSavedTracks.map((t: any) => ({
          Id: t.id || t.Id,
          Title: t.title || t.Title,
          Author: t.author || t.Author || t.Artist || "",
          Duration: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
          DurationSeconds: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
        })),
        history: history.map((t: any) => ({
          Id: t.id || t.Id,
          Title: t.title || t.Title,
          Author: t.author || t.Author || t.Artist || "",
          Duration: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
          DurationSeconds: Number(t.duration ?? t.DurationSeconds ?? t.durationSeconds ?? t.Duration ?? 0),
        })),
        playlists: collections,
        collections: collections,
      });

      const patchUrl = `${docUrl}&updateMask.fieldPaths=musicDataJson`;
      await fetch(patchUrl, {
        method: "PATCH",
        headers: {
          Authorization: `Bearer ${idToken}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          fields: {
            musicDataJson: { stringValue: musicDataJson },
          },
        }),
      });
      return { success: true, newIdToken: newRefreshToken ? idToken : null, newRefreshToken };
    } catch (err) {
      console.warn("[saveUserLibraryData Firestore Error]", err);
    }
  }

  // 2. In local development ONLY:
  if (!isProduction) {
    try {
      const savedDir = getSavedDir();
      if (!fs.existsSync(savedDir)) fs.mkdirSync(savedDir, { recursive: true });
      fs.writeFileSync(path.join(savedDir, "favorites.json"), JSON.stringify(favorites, null, 2), "utf8");
      fs.writeFileSync(path.join(savedDir, "history.json"), JSON.stringify(history, null, 2), "utf8");
      fs.writeFileSync(path.join(savedDir, "playlists.json"), JSON.stringify(playlists, null, 2), "utf8");
    } catch {}
  }
  return { success: true };
}
