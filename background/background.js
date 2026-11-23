// KahoiiMusic Extension - Background Service Worker (Manifest V3)
const API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";
const PROJECT_ID = "kahoiimusic";

// ================= AUTH & STORAGE ================= //
async function getSession() {
  const result = await chrome.storage.local.get(['currentUser']);
  return result.currentUser || null;
}

async function setSession(user) {
  await chrome.storage.local.set({ currentUser: user });
}

async function clearSession() {
  await chrome.storage.local.remove(['currentUser', 'cachedMusicData']);
}

async function login(email, password) {
  email = (email || '').trim();
  password = (password || '').trim();
  if (!email || !password) {
    return { success: false, error: "Vui lòng nhập đầy đủ Email và Mật khẩu." };
  }

  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=${API_KEY}`;
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email,
        password,
        returnSecureToken: true
      })
    });

    const data = await res.json();
    if (!res.ok) {
      const msg = data?.error?.message || "Đăng nhập thất bại";
      if (msg.includes("EMAIL_NOT_FOUND") || msg.includes("INVALID_LOGIN_CREDENTIALS") || msg.includes("INVALID_PASSWORD")) {
        return { success: false, error: "Email hoặc mật khẩu không chính xác." };
      }
      if (msg.includes("USER_DISABLED")) {
        return { success: false, error: "Tài khoản của bạn đã bị vô hiệu hóa." };
      }
      return { success: false, error: "Lỗi đăng nhập: " + msg };
    }

    const expiresIn = parseInt(data.expiresIn || '3600', 10);
    const expiryTimestamp = Math.floor(Date.now() / 1000) + expiresIn - 120; // 2 min buffer

    const user = {
      localId: data.localId,
      email: data.email,
      idToken: data.idToken,
      refreshToken: data.refreshToken,
      expiryTimestamp: expiryTimestamp,
      avatarBase64: null
    };

    // Fetch avatar from Firestore immediately after login
    try {
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${user.localId}?key=${API_KEY}`;
      const docRes = await fetch(docUrl, {
        headers: { "Authorization": `Bearer ${user.idToken}` }
      });
      if (docRes.ok) {
        const docData = await docRes.json();
        if (docData.fields && docData.fields.avatarBase64 && docData.fields.avatarBase64.stringValue) {
          user.avatarBase64 = docData.fields.avatarBase64.stringValue;
        }
      }
    } catch { }

    await setSession(user);

    // Warm up local cache with cloud data and broadcast login
    try {
      const cloudRes = await getUserCloudData();
      if (cloudRes && cloudRes.success && cloudRes.payload) {
        await chrome.storage.local.set({ cachedMusicData: cloudRes.payload });
        notifyAllTabs({ action: "LOGIN_SUCCESS", session: user, payload: cloudRes.payload });
      } else {
        notifyAllTabs({ action: "LOGIN_SUCCESS", session: user });
      }
    } catch {
      notifyAllTabs({ action: "LOGIN_SUCCESS", session: user });
    }

    return { success: true, user };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối máy chủ: " + err.message };
  }
}

async function register(email, password) {
  email = (email || '').trim();
  password = (password || '').trim();
  if (!email || !password) {
    return { success: false, error: "Vui lòng nhập đầy đủ Email và Mật khẩu." };
  }
  if (password.length < 6) {
    return { success: false, error: "Mật khẩu phải có ít nhất 6 ký tự." };
  }

  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`;
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email,
        password,
        returnSecureToken: true
      })
    });

    const data = await res.json();
    if (!res.ok) {
      const msg = data?.error?.message || "Đăng ký thất bại";
      if (msg.includes("EMAIL_EXISTS")) {
        return { success: false, error: "Email này đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác." };
      }
      if (msg.includes("WEAK_PASSWORD")) {
        return { success: false, error: "Mật khẩu quá yếu (phải từ 6 ký tự trở lên)." };
      }
      if (msg.includes("INVALID_EMAIL")) {
        return { success: false, error: "Địa chỉ email không hợp lệ." };
      }
      return { success: false, error: "Lỗi đăng ký: " + msg };
    }

    const expiresIn = parseInt(data.expiresIn || '3600', 10);
    const expiryTimestamp = Math.floor(Date.now() / 1000) + expiresIn - 120;

    const user = {
      localId: data.localId,
      email: data.email,
      idToken: data.idToken,
      refreshToken: data.refreshToken,
      expiryTimestamp: expiryTimestamp,
      avatarBase64: null
    };

    // Khởi tạo tài liệu người dùng trên Firestore
    try {
      const initialDoc = {
        fields: {
          email: { stringValue: user.email },
          createdAt: { stringValue: new Date().toISOString() },
          musicDataJson: {
            stringValue: JSON.stringify({
              savedTracks: [],
              collections: [],
              history: [],
              updatedAt: new Date().toISOString()
            })
          }
        }
      };
      const docUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${user.localId}?key=${API_KEY}`;
      await fetch(docUrl, {
        method: "PATCH",
        headers: {
          "Authorization": `Bearer ${user.idToken}`,
          "Content-Type": "application/json"
        },
        body: JSON.stringify(initialDoc)
      });
    } catch { }

    await setSession(user);

    // Warm up local cache with cloud data and broadcast login
    try {
      const cloudRes = await getUserCloudData();
      if (cloudRes && cloudRes.success && cloudRes.payload) {
        await chrome.storage.local.set({ cachedMusicData: cloudRes.payload });
        notifyAllTabs({ action: "LOGIN_SUCCESS", session: user, payload: cloudRes.payload });
      } else {
        notifyAllTabs({ action: "LOGIN_SUCCESS", session: user });
      }
    } catch {
      notifyAllTabs({ action: "LOGIN_SUCCESS", session: user });
    }

    return { success: true, user };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối máy chủ: " + err.message };
  }
}

async function forgotPassword(email) {
  email = (email || '').trim();
  if (!email || !email.includes('@')) {
    return { success: false, error: "Vui lòng nhập địa chỉ email hợp lệ." };
  }

  const url = `https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=${API_KEY}`;
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        requestType: "PASSWORD_RESET",
        email
      })
    });

    const data = await res.json();
    if (!res.ok) {
      const msg = data?.error?.message || "Đặt lại mật khẩu thất bại";
      if (msg.includes("EMAIL_NOT_FOUND")) {
        return { success: false, error: "Không tìm thấy tài khoản tương ứng với email này." };
      }
      if (msg.includes("INVALID_EMAIL")) {
        return { success: false, error: "Địa chỉ email không hợp lệ." };
      }
      return { success: false, error: "Lỗi: " + msg };
    }

    return { success: true, message: "Đã gửi liên kết đặt lại mật khẩu! Vui lòng kiểm tra hộp thư (cả hòm thư Spam) để tạo mật khẩu mới." };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối máy chủ: " + err.message };
  }
}

async function getValidToken() {
  let session = await getSession();
  if (!session || !session.idToken) return null;

  const nowSec = Math.floor(Date.now() / 1000);
  if (nowSec >= (session.expiryTimestamp || 0)) {
    if (!session.refreshToken) {
      await clearSession();
      return null;
    }

    try {
      const url = `https://securetoken.googleapis.com/v1/token?key=${API_KEY}`;
      const params = new URLSearchParams();
      params.append("grant_type", "refresh_token");
      params.append("refresh_token", session.refreshToken);

      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: params.toString()
      });

      if (!res.ok) {
        await clearSession();
        return null;
      }

      const data = await res.json();
      const expiresIn = parseInt(data.expires_in || '3600', 10);
      session.idToken = data.id_token;
      session.refreshToken = data.refresh_token;
      session.expiryTimestamp = Math.floor(Date.now() / 1000) + expiresIn - 120;

      await setSession(session);
      return session.idToken;
    } catch {
      return null;
    }
  }

  return session.idToken;
}

// ================= FIRESTORE REST API ================= //
async function getUserCloudData() {
  const session = await getSession();
  if (!session) return { success: false, error: "Chưa đăng nhập." };

  const token = await getValidToken();
  if (!token) return { success: false, error: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." };

  const url = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${session.localId}?key=${API_KEY}`;

  try {
    const res = await fetch(url, {
      method: "GET",
      headers: { "Authorization": `Bearer ${token}` }
    });

    if (res.status === 404) {
      return { success: true, exists: false, payload: { savedTracks: [], history: [], collections: [] }, avatarBase64: null };
    }

    if (!res.ok) {
      return { success: false, error: `Lỗi tải đám mây (${res.status})` };
    }

    const data = await res.json();
    const fields = data.fields || {};
    let payload = { savedTracks: [], history: [], collections: [] };
    let avatarBase64 = null;

    if (fields.avatarBase64 && fields.avatarBase64.stringValue) {
      avatarBase64 = fields.avatarBase64.stringValue;
      if (session.avatarBase64 !== avatarBase64) {
        session.avatarBase64 = avatarBase64;
        await setSession(session);
      }
    }

    if (fields.musicDataJson && fields.musicDataJson.stringValue) {
      try {
        payload = JSON.parse(fields.musicDataJson.stringValue);
      } catch (e) {
        console.error("Parse cloud JSON error:", e);
      }
    }

    if (!Array.isArray(payload.collections)) payload.collections = [];
    if (!Array.isArray(payload.savedTracks)) payload.savedTracks = [];
    if (!Array.isArray(payload.history)) payload.history = [];

    // Cache locally for instant 0ms tab reads
    try {
      await chrome.storage.local.set({ cachedMusicData: payload });
    } catch { }

    return { success: true, exists: true, payload, avatarBase64 };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối Firestore: " + err.message };
  }
}

// Helper: Ensure accurate duration with strict 1.5s timeout so saving never hangs
async function ensureTrackDuration(videoId, duration) {
  if (duration && duration > 0) return duration;
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 1500);
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const res = await fetch(url, {
      signal: controller.signal,
      headers: { "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" }
    });
    clearTimeout(timeoutId);
    if (res.ok) {
      const html = await res.text();
      // 1. Try lengthSeconds
      const lenMatch = html.match(/"lengthSeconds":"(\d+)"/);
      if (lenMatch && lenMatch[1]) {
        const sec = parseInt(lenMatch[1], 10);
        if (sec > 0) return sec;
      }
      // 2. Try itemprop="duration" (e.g. PT4M11S)
      const durMatch = html.match(/itemprop="duration" content="([^"]+)"/);
      if (durMatch && durMatch[1]) {
        const iso = durMatch[1];
        const m = iso.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
        if (m) {
          const h = parseInt(m[1] || 0, 10);
          const min = parseInt(m[2] || 0, 10);
          const s = parseInt(m[3] || 0, 10);
          const total = h * 3600 + min * 60 + s;
          if (total > 0) return total;
        }
      }
    }
  } catch { }
  return 0;
}

// Lưu bài hát vào mục ĐÃ TẢI (savedTracks) trên Firestore
async function saveTrackToSavedTracks(track) {
  if (!track || !track.Id) {
    return { success: false, error: "Không tìm thấy thông tin bài hát hợp lệ." };
  }

  const session = await getSession();
  if (!session) {
    return { success: false, error: "Chưa đăng nhập tài khoản KahoiiMusic." };
  }

  const token = await getValidToken();
  if (!token) {
    return { success: false, error: "Phiên đăng nhập đã hết hạn. Vui lòng mở extension đăng nhập lại." };
  }

  track.DurationSeconds = await ensureTrackDuration(track.Id, track.DurationSeconds);

  const cloudRes = await getUserCloudData();
  if (!cloudRes.success) {
    return { success: false, error: cloudRes.error };
  }

  let payload = cloudRes.payload || {};
  if (!Array.isArray(payload.savedTracks)) payload.savedTracks = [];
  if (!Array.isArray(payload.collections)) payload.collections = [];
  if (!Array.isArray(payload.history)) payload.history = [];

  // Kiểm tra trùng
  const existingIdx = payload.savedTracks.findIndex(t => (t.Id || '').toLowerCase() === track.Id.toLowerCase());
  if (existingIdx >= 0) {
    return {
      success: true,
      alreadyExists: true,
      message: `"${track.Title || track.Id}" đã có trong mục Đã tải!`,
      track: payload.savedTracks[existingIdx]
    };
  }

  const newTrack = {
    Id: track.Id,
    Title: track.Title || "Chưa có tiêu đề",
    Author: track.Author || "",
    DurationSeconds: track.DurationSeconds || 0
  };

  payload.savedTracks.unshift(newTrack);
  payload.updatedAt = new Date().toISOString();

  // Optimistic local storage update & instant tab broadcast
  try {
    await chrome.storage.local.set({ cachedMusicData: payload });
    notifyAllTabs({ action: "TRACK_SAVED", track: newTrack, payload });
  } catch { }

  const patchUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${session.localId}?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt&key=${API_KEY}`;

  const bodyData = {
    fields: {
      musicDataJson: {
        stringValue: JSON.stringify(payload)
      },
      updatedAt: {
        stringValue: payload.updatedAt
      }
    }
  };

  try {
    const res = await fetch(patchUrl, {
      method: "PATCH",
      headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify(bodyData)
    });

    if (!res.ok) {
      return { success: false, error: `Lỗi lưu đám mây (${res.status})` };
    }

    return {
      success: true,
      alreadyExists: false,
      message: `Đã lưu "${newTrack.Title}" vào mục Đã tải!`,
      track: newTrack
    };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối khi lưu: " + err.message };
  }
}

async function saveCollectionToCloud(collectionName, playlistUrl, items) {
  const session = await getSession();
  if (!session) {
    return { success: false, error: "Chưa đăng nhập tài khoản KahoiiMusic." };
  }

  const token = await getValidToken();
  if (!token) {
    return { success: false, error: "Phiên đăng nhập đã hết hạn." };
  }

  const cleanName = (collectionName || "Danh sách YouTube").trim();

  const cloudRes = await getUserCloudData();
  if (!cloudRes.success) {
    return { success: false, error: cloudRes.error };
  }

  let payload = cloudRes.payload || {};
  if (!Array.isArray(payload.collections)) payload.collections = [];
  if (!Array.isArray(payload.savedTracks)) payload.savedTracks = [];
  if (!Array.isArray(payload.history)) payload.history = [];

  // Chuẩn hóa danh sách bài hát (items)
  let validItems = [];
  if (Array.isArray(items) && items.length > 0) {
    validItems = items.map(t => ({
      Id: t.Id || t.id || "",
      Title: t.Title || t.title || "Bài hát",
      Author: t.Author || t.author || "",
      DurationSeconds: parseInt(t.DurationSeconds || t.duration || 0, 10) || 0
    })).filter(t => t.Id && t.Id.length > 0);
  }

  // Kiểm tra trùng tên BST
  const existingIdx = payload.collections.findIndex(c =>
    (c.Name || c.name || "").toLowerCase().trim() === cleanName.toLowerCase()
  );

  if (existingIdx >= 0) {
    const existingCol = payload.collections[existingIdx];
    // Nếu BST đã có nhưng đang rỗng items và lần này có items mới -> cập nhật items vào
    if ((!Array.isArray(existingCol.Items) || existingCol.Items.length === 0) && validItems.length > 0) {
      existingCol.Items = validItems;
      payload.updatedAt = new Date().toISOString();
      await updateCloudPayloadDirect(session.localId, token, payload);
      return {
        success: true,
        alreadyExists: true,
        message: `Đã bổ sung ${validItems.length} bài hát vào "${cleanName}"!`,
        collection: existingCol
      };
    }
    return {
      success: true,
      alreadyExists: true,
      message: `"${cleanName}" đã có trong Bộ sưu tập!`,
      collection: existingCol
    };
  }

  const newCol = {
    Id: "col_" + Date.now().toString(36),
    Name: cleanName,
    CreatedAt: Date.now(),
    Items: validItems
  };

  payload.collections.unshift(newCol);
  payload.updatedAt = new Date().toISOString();

  // Optimistic local storage update & instant tab broadcast
  try {
    await chrome.storage.local.set({ cachedMusicData: payload });
    notifyAllTabs({ action: "COLLECTION_SAVED", collection: newCol, payload });
  } catch { }

  const patchRes = await updateCloudPayloadDirect(session.localId, token, payload);
  if (!patchRes.success) {
    return patchRes;
  }

  return {
    success: true,
    alreadyExists: false,
    message: `Đã lưu "${cleanName}" (${validItems.length} bài) vào Bộ sưu tập!`,
    collection: newCol
  };
}

async function updateCloudPayloadDirect(localId, token, payload) {
  const patchUrl = `https://firestore.googleapis.com/v1/projects/${PROJECT_ID}/databases/(default)/documents/users/${localId}?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt&key=${API_KEY}`;

  const bodyData = {
    fields: {
      musicDataJson: {
        stringValue: JSON.stringify(payload)
      },
      updatedAt: {
        stringValue: payload.updatedAt
      }
    }
  };

  try {
    const res = await fetch(patchUrl, {
      method: "PATCH",
      headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify(bodyData)
    });

    if (!res.ok) {
      return { success: false, error: `Lỗi lưu đám mây (${res.status})` };
    }

    return { success: true };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối khi lưu: " + err.message };
  }
}

// Fetch YouTube oEmbed metadata helper
async function fetchOEmbed(videoId) {
  let title = "";
  let author = "";
  let thumbnail = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
  let duration = 0;

  try {
    const url = `https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v=${videoId}&format=json`;
    const res = await fetch(url);
    if (res.ok) {
      const data = await res.json();
      title = data.title || "";
      author = data.author_name || "";
      thumbnail = data.thumbnail_url || thumbnail;
    }
  } catch { }

  duration = await ensureTrackDuration(videoId, 0);

  return {
    success: true,
    title,
    author,
    thumbnail,
    duration
  };
}

function notifyAllTabs(extraData = {}) {
  try {
    chrome.tabs.query({}, (tabs) => {
      for (const t of tabs || []) {
        if (t.id) {
          chrome.tabs.sendMessage(t.id, { action: "CLOUD_DATA_CHANGED", ...extraData }, () => {
            if (chrome.runtime.lastError) {}
          });
        }
      }
    });
  } catch { }
}

let lastCloudUpdatedAt = null;
async function pollCloudUpdates() {
  try {
    const session = await getSession();
    if (!session) return;
    const res = await getUserCloudData();
    if (res && res.success && res.payload) {
      const updatedAt = res.payload.updatedAt || null;
      if (updatedAt && updatedAt !== lastCloudUpdatedAt) {
        lastCloudUpdatedAt = updatedAt;
        await chrome.storage.local.set({ cachedMusicData: res.payload });
        notifyAllTabs({ action: "CLOUD_DATA_CHANGED", payload: res.payload });
      }
    }
  } catch { }
}

// Kiểm tra đồng bộ từ App hoặc thiết bị khác mỗi 8 giây khi trình duyệt hoạt động
setInterval(pollCloudUpdates, 8000);

// Đồng thời kiểm tra ngay lập tức khi người dùng chuyển tab sang YouTube
try {
  chrome.tabs.onActivated.addListener((activeInfo) => {
    chrome.tabs.get(activeInfo.tabId, (tab) => {
      if (tab && tab.url && tab.url.includes("youtube.com")) {
        pollCloudUpdates();
      }
    });
  });
} catch { }

// ================= WEB APP ROUTING HELPERS ================= //
const DEFAULT_WEB_APP_URL = "https://iiwa-san.github.io/KahoiiMusic/";

function isWebAppUrl(urlStr) {
  if (!urlStr) return false;
  try {
    const u = new URL(urlStr);
    // GitHub Pages deployment
    if (u.hostname.includes("iiwa-san.github.io") && u.pathname.includes("KahoiiMusic")) return true;
    // Local dev servers
    if ((u.hostname === "localhost" || u.hostname === "127.0.0.1") && (u.port === "3000" || u.port === "3001" || u.port === "5173")) return true;
    // Custom domain
    if (u.hostname.includes("kahoiimusic") || u.pathname.includes("kahoiimusic")) return true;
  } catch { }
  return false;
}

async function findOpenWebAppTab() {
  try {
    const tabs = await chrome.tabs.query({});
    for (const t of tabs) {
      if (t.id && (isWebAppUrl(t.url) || (t.title && (t.title.includes("KahoiiMusic") || t.title.includes("Kahoii Music"))))) {
        return t;
      }
    }
  } catch (err) {
    console.warn("[Background] Error querying tabs for Web App:", err);
  }
  return null;
}

async function focusAndPlayInWebApp(tab, trackData) {
  try {
    if (tab.id) {
      await chrome.tabs.update(tab.id, { active: true });
      if (tab.windowId) {
        await chrome.windows.update(tab.windowId, { focused: true });
      }

      if (trackData) {
        try {
          await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            func: (track) => {
              window.postMessage({ type: "KAHOII_PLAY_TRACK", track }, "*");
              window.dispatchEvent(new CustomEvent("kahoii-play-track", { detail: track }));
            },
            args: [trackData]
          });
        } catch (scriptErr) {
          console.warn("[Background] Scripting error, falling back to tab messaging:", scriptErr);
          chrome.tabs.sendMessage(tab.id, { action: "PLAY_TRACK", track: trackData }, () => {
            if (chrome.runtime.lastError) {}
          });
        }
      }
      return true;
    }
  } catch (err) {
    console.warn("[Background] Error focusing Web App tab:", err);
  }
  return false;
}

async function openNewWebAppTab(trackData, play = true) {
  try {
    const base = DEFAULT_WEB_APP_URL.replace(/\/$/, "");
    let targetUrl = base + "/";
    if (trackData && (trackData.v || trackData.id)) {
      const vId = trackData.v || trackData.id;
      const title = encodeURIComponent(trackData.title || "");
      const author = encodeURIComponent(trackData.author || "");
      const dur = trackData.dur || trackData.duration || 0;
      targetUrl = `${base}/?v=${encodeURIComponent(vId)}&title=${title}&author=${author}&dur=${dur}&play=${play}`;
    }
    const createdTab = await chrome.tabs.create({ url: targetUrl, active: true });
    return createdTab;
  } catch (err) {
    console.warn("[Background] Error creating new Web App tab:", err);
    return null;
  }
}

// ================= MESSAGE LISTENER ================= //
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  (async () => {
    try {
      if (request.action === 'GET_SESSION') {
        const session = await getSession();
        sendResponse({ success: true, session });
      } else if (request.action === 'LOGIN') {
        const res = await login(request.email, request.password);
        if (res.success) notifyAllTabs();
        sendResponse(res);
      } else if (request.action === 'REGISTER') {
        const res = await register(request.email, request.password);
        if (res.success) notifyAllTabs();
        sendResponse(res);
      } else if (request.action === 'FORGOT_PASSWORD') {
        const res = await forgotPassword(request.email);
        sendResponse(res);
      } else if (request.action === 'LOGOUT') {
        await clearSession();
        notifyAllTabs({ action: "LOGOUT", isLogout: true });
        sendResponse({ success: true });
      } else if (request.action === 'GET_CLOUD_DATA') {
        const res = await getUserCloudData();
        sendResponse(res);
      } else if (request.action === 'SAVE_TO_OFFLINE') {
        const res = await saveTrackToSavedTracks(request.track);
        if (res.success) notifyAllTabs();
        sendResponse(res);
      } else if (request.action === 'SAVE_TO_COLLECTION') {
        const res = await saveCollectionToCloud(request.name, request.url, request.items);
        if (res.success) notifyAllTabs();
        sendResponse(res);
      } else if (request.action === 'FETCH_OEMBED') {
        const res = await fetchOEmbed(request.videoId);
        sendResponse(res);
      } else if (request.action === 'CHECK_WEB_APP') {
        const webTab = await findOpenWebAppTab();
        sendResponse({ success: true, isOpen: !!webTab, tab: webTab });
      } else if (request.action === 'OPEN_OR_FOCUS_WEB_APP') {
        const webTab = await findOpenWebAppTab();
        if (webTab) {
          await focusAndPlayInWebApp(webTab, request.trackData);
          sendResponse({ success: true, mode: "focused", tabId: webTab.id });
        } else {
          const newTab = await openNewWebAppTab(request.trackData, request.play !== false);
          sendResponse({ success: true, mode: "opened", tabId: newTab?.id });
        }
      } else if (request.action === 'OPEN_NEW_WEB_APP_TAB') {
        const newTab = await openNewWebAppTab(request.trackData, request.play !== false);
        sendResponse({ success: true, tabId: newTab?.id });
      } else if (request.action === 'SET_APP_STATUS') {
        const dataToSave = {};
        if (typeof request.hasAppInstalled === "boolean") dataToSave.hasAppInstalled = request.hasAppInstalled;
        if (request.preferredTarget) dataToSave.preferredTarget = request.preferredTarget;
        await chrome.storage.local.set(dataToSave);
        sendResponse({ success: true, ...dataToSave });
      } else if (request.action === 'ROUTE_PLAY_TRACK') {
        // Step 1: If Web App is currently open, jump to Web App first!
        const webTab = await findOpenWebAppTab();
        if (webTab) {
          await focusAndPlayInWebApp(webTab, request.trackData);
          sendResponse({ success: true, routedTo: "web", tabId: webTab.id });
          return;
        }

        // Step 2: Web App is not open. Check user preferences & app status
        const localSettings = await chrome.storage.local.get(['hasAppInstalled', 'preferredTarget']);
        if (localSettings.preferredTarget === "web") {
          const newTab = await openNewWebAppTab(request.trackData, true);
          sendResponse({ success: true, routedTo: "web_new_tab", tabId: newTab?.id });
          return;
        }

        if (localSettings.hasAppInstalled === true) {
          sendResponse({ success: true, routedTo: "app", protocolUrl: request.protocolUrl });
          return;
        }

        // Step 3: Prompt user whether to open Web App or launch/download desktop app
        sendResponse({
          success: true,
          routedTo: "prompt_app_or_web",
          protocolUrl: request.protocolUrl,
          trackData: request.trackData
        });
      } else {
        sendResponse({ success: false, error: "Action không xác định: " + request.action });
      }
    } catch (err) {
      sendResponse({ success: false, error: err.message || String(err) });
    }
  })();

  return true; // Keep asynchronous channel open
});
