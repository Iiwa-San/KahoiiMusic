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
  await chrome.storage.local.remove(['currentUser']);
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

    return { success: true, exists: true, payload, avatarBase64 };
  } catch (err) {
    return { success: false, error: "Lỗi kết nối Firestore: " + err.message };
  }
}

// Helper: Ensure accurate duration
async function ensureTrackDuration(videoId, duration) {
  if (duration && duration > 0) return duration;
  try {
    const url = `https://www.youtube.com/watch?v=${videoId}`;
    const res = await fetch(url, { headers: { "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" } });
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
        notifyAllTabs();
      }
    }
  } catch { }
}

// Kiểm tra đồng bộ từ App mỗi 25 giây khi trình duyệt hoạt động
setInterval(pollCloudUpdates, 25000);

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
      } else {
        sendResponse({ success: false, error: "Action không xác định: " + request.action });
      }
    } catch (err) {
      sendResponse({ success: false, error: err.message || String(err) });
    }
  })();

  return true; // Keep asynchronous channel open
});
