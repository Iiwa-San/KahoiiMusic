// KahoiiMusic Extension - YouTube Content Script
(function () {
  let isProcessingPlay = false;
  let isProcessingSave = false;
  let isProcessingDownload = false;
  let lastUrl = null;
  let cachedSavedTracks = [];
  let cachedCollections = [];
  let currentSession = null;

  // 1. Đọc ngay từ cache cục bộ (chrome.storage.local) nếu có để hiển thị trạng thái nút 0ms không độ trễ
  if (chrome.storage && chrome.storage.local) {
    chrome.storage.local.get(['currentUser', 'cachedMusicData'], (result) => {
      if (result) {
        currentSession = result.currentUser || null;
        if (result.cachedMusicData) {
          cachedSavedTracks = result.cachedMusicData.savedTracks || [];
          cachedCollections = result.cachedMusicData.collections || [];
        }
        updateButtonStatus();
      }
    });
  }

  function syncCloudData() {
    try {
      chrome.runtime.sendMessage({ action: "GET_SESSION" }, (sRes) => {
        currentSession = sRes?.session || null;
        if (!currentSession || !currentSession.email) {
          cachedSavedTracks = [];
          cachedCollections = [];
          updateButtonStatus();
          return;
        }

        chrome.runtime.sendMessage({ action: "GET_CLOUD_DATA" }, (res) => {
          if (res && res.payload) {
            cachedSavedTracks = res.payload.savedTracks || [];
            cachedCollections = res.payload.collections || [];
          } else {
            cachedSavedTracks = [];
            cachedCollections = [];
          }
          updateButtonStatus();
        });
      });
    } catch { }
  }

  // Lắng nghe thay đổi Storage trong thời gian thực (đăng nhập / đăng xuất / đồng bộ trên popup)
  if (chrome.storage && chrome.storage.onChanged) {
    chrome.storage.onChanged.addListener((changes, area) => {
      if (area === "local") {
        if (changes.currentUser) {
          currentSession = changes.currentUser.newValue || null;
          if (!currentSession) {
            cachedSavedTracks = [];
            cachedCollections = [];
            updateButtonStatus();
            return;
          }
        }
        if (changes.cachedMusicData) {
          if (changes.cachedMusicData.newValue) {
            const data = changes.cachedMusicData.newValue;
            cachedSavedTracks = data.savedTracks || [];
            cachedCollections = data.collections || [];
          } else {
            cachedSavedTracks = [];
            cachedCollections = [];
          }
          updateButtonStatus();
          return;
        }
        syncCloudData();
      }
    });
  }

  // Lắng nghe thông điệp cập nhật tức thì (0ms) từ background / popup
  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg) {
      if (msg.action === "LOGOUT" || msg.isLogout) {
        currentSession = null;
        cachedSavedTracks = [];
        cachedCollections = [];
        updateButtonStatus();
      } else if (msg.action === "LOGIN_SUCCESS" && msg.session) {
        currentSession = msg.session;
        if (msg.payload) {
          cachedSavedTracks = msg.payload.savedTracks || [];
          cachedCollections = msg.payload.collections || [];
        }
        updateButtonStatus();
      } else if (msg.action === "TRACK_SAVED" && msg.track) {
        const t = msg.track;
        const tid = (t.Id || t.id || t.videoId || "").toLowerCase();
        if (tid && !cachedSavedTracks.some(x => (x.Id || x.id || x.videoId || "").toLowerCase() === tid)) {
          cachedSavedTracks.unshift(t);
        }
        updateButtonStatus();
      } else if (msg.action === "COLLECTION_SAVED" && msg.collection) {
        const c = msg.collection;
        const cname = (c.Name || c.name || "").toLowerCase().trim();
        if (cname && !cachedCollections.some(x => (x.Name || x.name || "").toLowerCase().trim() === cname)) {
          cachedCollections.unshift(c);
        }
        updateButtonStatus();
      } else if (msg.action === "CLOUD_DATA_CHANGED" || msg.action === "STATE_UPDATED") {
        if (msg.payload) {
          cachedSavedTracks = msg.payload.savedTracks || [];
          cachedCollections = msg.payload.collections || [];
          updateButtonStatus();
        } else {
          syncCloudData();
        }
      }
      sendResponse({ received: true });
    }
  });

  function getQueryParam(key) {
    try {
      const url = new URL(window.location.href);
      return url.searchParams.get(key);
    } catch {
      return null;
    }
  }

  function getVideoId() {
    return getQueryParam("v");
  }

  function getListId() {
    return getQueryParam("list");
  }

  function getVideoTitle() {
    const titleEl = document.querySelector("h1.ytd-watch-metadata yt-formatted-string, #title h1 yt-formatted-string, h1.title");
    if (titleEl && titleEl.textContent && titleEl.textContent.trim()) {
      return titleEl.textContent.trim();
    }
    const docTitle = document.title || "";
    return docTitle.replace(/ - YouTube$/, "").trim();
  }

  function getChannelName() {
    const channelEl = document.querySelector("#channel-name #text a, ytd-channel-name yt-formatted-string a, #owner-name a, #upload-info #channel-name a");
    if (channelEl && channelEl.textContent) {
      return channelEl.textContent.trim();
    }
    return "YouTube";
  }

  function getVideoDuration() {
    const durEl = document.querySelector(".ytp-time-duration");
    if (durEl && durEl.textContent) {
      const parts = durEl.textContent.trim().split(":").map(n => parseInt(n, 10));
      if (parts.length === 2 && !isNaN(parts[0]) && !isNaN(parts[1])) {
        return parts[0] * 60 + parts[1];
      }
      if (parts.length === 3 && !isNaN(parts[0]) && !isNaN(parts[1]) && !isNaN(parts[2])) {
        return parts[0] * 3600 + parts[1] * 60 + parts[2];
      }
    }
    return 0;
  }

  function extractPlaylistItemsFromPage() {
    const items = [];
    const panelRenderers = document.querySelectorAll("ytd-playlist-panel-video-renderer");
    if (panelRenderers && panelRenderers.length > 0) {
      panelRenderers.forEach((el) => {
        const link = el.querySelector("a#wc-endpoint") || el.querySelector("a");
        const href = link ? link.getAttribute("href") || "" : "";
        const match = href.match(/[?&]v=([a-zA-Z0-9_-]{11})/);
        const vid = match ? match[1] : null;
        const titleEl = el.querySelector("#video-title");
        const title = titleEl ? titleEl.textContent.trim() : "";
        const authorEl = el.querySelector("#byline") || el.querySelector("#channel-name");
        const author = authorEl ? authorEl.textContent.trim() : "YouTube";
        if (vid && title && !items.some(x => x.Id === vid)) {
          items.push({ Id: vid, Title: title, Author: author, DurationSeconds: 0 });
        }
      });
    }

    if (items.length === 0) {
      const listRenderers = document.querySelectorAll("ytd-playlist-video-renderer");
      if (listRenderers && listRenderers.length > 0) {
        listRenderers.forEach((el) => {
          const link = el.querySelector("a#video-title") || el.querySelector("a#thumbnail");
          const href = link ? link.getAttribute("href") || "" : "";
          const match = href.match(/[?&]v=([a-zA-Z0-9_-]{11})/);
          const vid = match ? match[1] : null;
          const titleEl = el.querySelector("#video-title");
          const title = titleEl ? titleEl.textContent.trim() : "";
          const authorEl = el.querySelector("#byline-container a") || el.querySelector("#channel-name");
          const author = authorEl ? authorEl.textContent.trim() : "YouTube";
          if (vid && title && !items.some(x => x.Id === vid)) {
            items.push({ Id: vid, Title: title, Author: author, DurationSeconds: 0 });
          }
        });
      }
    }
    return items;
  }

  function getPlaylistTitle() {
    const selectors = [
      "ytd-playlist-panel-renderer #header-description a",
      "ytd-playlist-panel-renderer #title",
      "h1.ytd-playlist-header-renderer",
      "#header-description a",
      "yt-formatted-string.ytd-playlist-panel-renderer",
      "#playlist-header-description h3",
      "ytd-watch-metadata ytd-playlist-panel-renderer #title"
    ];

    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (el && el.textContent && el.textContent.trim()) {
        const text = el.textContent.trim();
        if (text && text.length > 1 && !text.startsWith("http")) {
          return text;
        }
      }
    }

    return "Danh sách YouTube";
  }

  function triggerProtocol(protocolUrl) {
    try {
      const a = document.createElement("a");
      a.href = protocolUrl;
      a.style.display = "none";
      document.body.appendChild(a);
      a.click();
      setTimeout(() => {
        try { a.remove(); } catch { }
      }, 500);
    } catch {
      try {
        window.location.href = protocolUrl;
      } catch { }
    }
  }

  function showToast(title, message, icon = "🎵", duration = 3500) {
    const existing = document.getElementById("kahoii-toast-container");
    if (existing) existing.remove();

    const toast = document.createElement("div");
    toast.id = "kahoii-toast-container";
    toast.className = "kahoii-toast";
    toast.innerHTML = `
      <div class="kahoii-toast-icon">${icon}</div>
      <div class="kahoii-toast-body">
        <div class="kahoii-toast-title">${title}</div>
        <div class="kahoii-toast-msg">${message}</div>
      </div>
    `;

    toast.onclick = () => {
      toast.style.animation = "kahoiiFadeOut 0.2s forwards";
      setTimeout(() => toast.remove(), 200);
    };

    document.body.appendChild(toast);

    setTimeout(() => {
      if (toast.parentElement) {
        toast.style.animation = "kahoiiFadeOut 0.25s forwards";
        setTimeout(() => toast.remove(), 250);
      }
    }, duration);
  }

  function updateButtonStatus() {
    const vId = getVideoId();
    const listId = getListId();
    const isLoggedIn = !!(currentSession && currentSession.email);

    // 1. Cập nhật nút Tải về
    const btnDownload = document.getElementById("kahoii-download-track-btn");
    if (btnDownload) {
      btnDownload.classList.remove("loading");
      const dlText = btnDownload.querySelector(".kahoii-btn-text");
      const dlIcon = btnDownload.querySelector(".kahoii-btn-icon");

      if (!isLoggedIn) {
        btnDownload.classList.remove("already-saved");
        if (dlIcon) dlIcon.textContent = "📥";
        if (dlText) dlText.textContent = "Tải về";
        btnDownload.title = "Đăng nhập KahoiiMusic để tải bài hát này về máy (Offline)";
      } else if (vId && Array.isArray(cachedSavedTracks)) {
        const isSaved = cachedSavedTracks.some(t => {
          const id = (t.Id || t.id || t.videoId || "").toLowerCase();
          return id === vId.toLowerCase();
        });

        if (isSaved) {
          btnDownload.classList.add("already-saved");
          if (dlIcon) dlIcon.textContent = "✓";
          if (dlText) dlText.textContent = "Đã tải";
          btnDownload.title = "Bài hát này đã có trong mục Đã tải (Offline)";
        } else if (!isProcessingDownload) {
          btnDownload.classList.remove("already-saved");
          if (dlIcon) dlIcon.textContent = "📥";
          if (dlText) dlText.textContent = "Tải về";
          btnDownload.title = "Tải bài hát này về mục Đã tải trong KahoiiMusic (Nghe Offline)";
        }
      }
    }

    // 2. Cập nhật nút Lưu vào BST
    const btnSaveCol = document.getElementById("kahoii-save-col-btn");
    if (btnSaveCol) {
      btnSaveCol.classList.remove("loading");
      const colText = btnSaveCol.querySelector(".kahoii-btn-text");
      const colIcon = btnSaveCol.querySelector(".kahoii-btn-icon");

      if (!isLoggedIn) {
        btnSaveCol.classList.remove("already-saved");
        if (colIcon) colIcon.textContent = "💾";
        if (colText) colText.textContent = "Lưu vào BST";
        btnSaveCol.title = "Đăng nhập KahoiiMusic để lưu danh sách vào Bộ sưu tập";
      } else if (listId && Array.isArray(cachedCollections)) {
        const playlistTitle = getPlaylistTitle();
        const isColSaved = cachedCollections.some(c => {
          const name = (c.name || c.Name || "").toLowerCase().trim();
          return name === playlistTitle.toLowerCase().trim();
        });

        if (isColSaved) {
          btnSaveCol.classList.add("already-saved");
          if (colIcon) colIcon.textContent = "✓";
          if (colText) colText.textContent = "Đã lưu vào BST";
          btnSaveCol.title = `Danh sách "${playlistTitle}" đã có trong Bộ sưu tập KahoiiMusic!`;
        } else if (!isProcessingSave) {
          btnSaveCol.classList.remove("already-saved");
          if (colIcon) colIcon.textContent = "💾";
          if (colText) colText.textContent = "Lưu vào BST";
          btnSaveCol.title = `Lưu nhanh toàn bộ danh sách "${playlistTitle}" vào Bộ sưu tập KahoiiMusic`;
        }
      }
    }
  }

  function injectButtons() {
    const videoId = getVideoId();
    const listId = getListId();

    if (!videoId && !listId) return;

    const currentUrl = window.location.href;
    if (currentUrl !== lastUrl) {
      lastUrl = currentUrl;
      const oldGroup = document.getElementById("kahoii-btn-group");
      if (oldGroup) oldGroup.remove();
    }

    if (document.getElementById("kahoii-btn-group")) return;

    // Search for YouTube action buttons container
    const targets = [
      document.querySelector("#actions #top-level-buttons-computed"),
      document.querySelector("#actions-inner #top-level-buttons-computed"),
      document.querySelector("#actions-inner"),
      document.querySelector("#actions #menu"),
      document.querySelector("ytd-watch-metadata #actions")
    ];

    const container = targets.find(t => t !== null && t !== undefined);
    if (!container) return;

    const isPlaylist = !!listId;

    const group = document.createElement("div");
    group.id = "kahoii-btn-group";
    group.className = "kahoii-btn-group";

    // 1. NÚT NẠP PHÁT TẠM (Giống dán link)
    const btnPlay = document.createElement("button");
    btnPlay.id = "kahoii-play-queue-btn";
    btnPlay.className = "kahoii-btn";
    btnPlay.title = isPlaylist ? "Nạp toàn bộ danh sách phát vào hàng đợi KahoiiMusic (giống dán link)" : "Nạp phát bài hát trên KahoiiMusic";
    btnPlay.innerHTML = `
      <span class="kahoii-btn-icon">⚡</span>
      <span class="kahoii-btn-text">${isPlaylist ? "Nạp phát cả danh sách" : "Nạp phát trên Kahoii"}</span>
    `;

    btnPlay.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();

      if (isProcessingPlay) return;
      isProcessingPlay = true;

      const playText = btnPlay.querySelector(".kahoii-btn-text");
      const playIcon = btnPlay.querySelector(".kahoii-btn-icon");

      btnPlay.className = "kahoii-btn loading";
      if (playIcon) playIcon.textContent = "⏳";
      if (playText) playText.textContent = isPlaylist ? "Đang nạp cả list..." : "Đang nạp...";

      const targetUrl = window.location.href;
      const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
      triggerProtocol(`kahoiimusic://play?url=${encodeURIComponent(targetUrl)}${userParam}`);

      if (isPlaylist) {
        showToast("Đang nạp danh sách!", "KahoiiMusic đang tải toàn bộ danh sách vào hàng đợi phát tạm...", "⚡", 4000);
      } else {
        const title = getVideoTitle();
        showToast("Đã gửi sang KahoiiMusic!", `Đang nạp "${title}" vào hàng đợi phát...`, "🎵");
      }

      const resetPlayBtn = () => {
        isProcessingPlay = false;
        btnPlay.className = "kahoii-btn";
        if (playIcon) playIcon.textContent = "⚡";
        if (playText) playText.textContent = isPlaylist ? "Nạp phát cả danh sách" : "Nạp phát trên Kahoii";
      };

      const safetyTimer = setTimeout(resetPlayBtn, 3000);

      setTimeout(() => {
        btnPlay.className = "kahoii-btn success";
        if (playIcon) playIcon.textContent = "✅";
        if (playText) playText.textContent = isPlaylist ? "Đang phát cả list" : "Đã nạp";

        setTimeout(() => {
          clearTimeout(safetyTimer);
          resetPlayBtn();
        }, 1500);
      }, 700);
    });

    group.appendChild(btnPlay);

    // 2. NÚT TẢI BÀI HÁT NÀY VỀ MÁY (Nghe Offline - Dù đang phát trong list vẫn tải riêng bài này)
    if (videoId) {
      const btnDownload = document.createElement("button");
      btnDownload.id = "kahoii-download-track-btn";
      btnDownload.className = "kahoii-btn download";
      btnDownload.title = "Tải bài hát này về mục Đã tải trong KahoiiMusic (Nghe Offline)";
      btnDownload.innerHTML = `
        <span class="kahoii-btn-icon">📥</span>
        <span class="kahoii-btn-text">Tải về</span>
      `;

      btnDownload.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();

        if (!currentSession || !currentSession.email) {
          showToast("Yêu cầu đăng nhập!", "Tính năng Tải về (Offline) chỉ có thể dùng khi đăng nhập tài khoản KahoiiMusic trên tiện ích mở rộng.", "🔒", 4500);
          return;
        }

        if (isProcessingDownload) return;
        isProcessingDownload = true;

        const dlText = btnDownload.querySelector(".kahoii-btn-text");
        const dlIcon = btnDownload.querySelector(".kahoii-btn-icon");

        btnDownload.className = "kahoii-btn download loading";
        if (dlIcon) dlIcon.textContent = "⏳";
        if (dlText) dlText.textContent = "Đang gửi...";

        const vId = getVideoId();
        const title = getVideoTitle();
        const author = getChannelName();
        const dur = getVideoDuration();

        // Optimistic update
        const newTrack = { Id: vId, Title: title, Author: author, DurationSeconds: dur };
        if (!cachedSavedTracks.some(t => (t.Id || t.id || t.videoId || '').toLowerCase() === vId.toLowerCase())) {
          cachedSavedTracks.unshift(newTrack);
        }

        // Gửi lệnh tải về máy qua URI Scheme kèm token đăng nhập nhanh
        const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
        const rtParam = currentSession && currentSession.refreshToken ? `&auth_rt=${encodeURIComponent(currentSession.refreshToken)}` : "";
        triggerProtocol(`kahoiimusic://download?v=${encodeURIComponent(vId)}&title=${encodeURIComponent(title)}&author=${encodeURIComponent(author)}&dur=${dur}${userParam}${rtParam}`);

        // Safety fallback timer: Đảm bảo nút KHÔNG BAO GIỜ bị treo nếu đóng app ngay hoặc chrome background không phản hồi
        let safetyTimer = setTimeout(() => {
          isProcessingDownload = false;
          updateButtonStatus();
        }, 2500);

        // Đồng bộ lên đám mây Firestore savedTracks
        try {
          chrome.runtime.sendMessage({
            action: "SAVE_TO_OFFLINE",
            track: {
              Id: vId,
              Title: title,
              Author: author,
              DurationSeconds: dur
            }
          }, (res) => {
            clearTimeout(safetyTimer);
            if (chrome.runtime.lastError) {
              console.warn("[Kahoii] runtime error:", chrome.runtime.lastError.message);
            }
            if (res && res.alreadyExists) {
              showToast("Đã có trong Đã tải!", `Bài hát "${title}" đã có sẵn trong mục Đã tải (Offline).`, "ℹ️", 3500);
            } else if (res && !res.success && res.error && res.error.includes("Chưa đăng nhập")) {
              showToast("Đang tải về máy!", `KahoiiMusic đang tải "${title}" về máy. (💡 Mở tiện ích đăng nhập để đồng bộ đám mây)`, "📥", 4500);
            } else {
              showToast("Đang tải về máy!", `KahoiiMusic đang tải "${title}" vào mục Đã tải (Offline)...`, "📥", 4000);
            }

            if (!cachedSavedTracks.some(t => (t.Id || t.id || t.videoId || '').toLowerCase() === vId.toLowerCase())) {
              cachedSavedTracks.unshift({ Id: vId, Title: title, Author: author, DurationSeconds: dur });
            }

            setTimeout(() => {
              isProcessingDownload = false;
              updateButtonStatus();
            }, 500);
          });
        } catch (err) {
          clearTimeout(safetyTimer);
          isProcessingDownload = false;
          updateButtonStatus();
        }
      });

      group.appendChild(btnDownload);
    }

    // 3. NÚT LƯU PLAYLIST VÀO BỘ SƯU TẬP (Nếu đang ở trong playlist)
    if (isPlaylist) {
      const btnSaveCol = document.createElement("button");
      btnSaveCol.id = "kahoii-save-col-btn";
      btnSaveCol.className = "kahoii-btn secondary";
      btnSaveCol.title = "Lưu nhanh toàn bộ danh sách này vào Bộ sưu tập KahoiiMusic với tên YouTube";
      btnSaveCol.innerHTML = `
        <span class="kahoii-btn-icon">💾</span>
        <span class="kahoii-btn-text">Lưu vào BST</span>
      `;

      btnSaveCol.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();

        if (!currentSession || !currentSession.email) {
          showToast("Yêu cầu đăng nhập!", "Tính năng Lưu vào Bộ sưu tập chỉ có thể dùng khi đăng nhập tài khoản KahoiiMusic trên tiện ích mở rộng.", "🔒", 4500);
          return;
        }

        if (isProcessingSave) return;
        isProcessingSave = true;

        const colText = btnSaveCol.querySelector(".kahoii-btn-text");
        const colIcon = btnSaveCol.querySelector(".kahoii-btn-icon");

        const playlistTitle = getPlaylistTitle();

        btnSaveCol.className = "kahoii-btn secondary loading";
        if (colIcon) colIcon.textContent = "⏳";
        if (colText) colText.textContent = "Đang lưu...";

        const targetUrl = window.location.href;

        // 1. Gửi lệnh mở giao thức cho KahoiiMusic desktop kèm thông tin tài khoản đăng nhập nhanh
        const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
        const rtParam = currentSession && currentSession.refreshToken ? `&auth_rt=${encodeURIComponent(currentSession.refreshToken)}` : "";
        triggerProtocol(`kahoiimusic://save_collection?url=${encodeURIComponent(targetUrl)}&title=${encodeURIComponent(playlistTitle)}${userParam}${rtParam}`);

        // Trích xuất các bài hát có trên trang
        const items = extractPlaylistItemsFromPage();
        const vId = getVideoId();
        const fallbackItems = items.length > 0 ? items : (vId ? [{ Id: vId, Title: getVideoTitle(), Author: getChannelName(), DurationSeconds: getVideoDuration() }] : []);

        // Optimistic update
        const newCol = { Name: playlistTitle, Items: fallbackItems };
        if (!cachedCollections.some(c => (c.name || c.Name || "").toLowerCase().trim() === playlistTitle.toLowerCase().trim())) {
          cachedCollections.unshift(newCol);
        }

        // Safety fallback timer: Tối đa 2.5s sẽ tự động phục hồi nút
        let safetyTimer = setTimeout(() => {
          isProcessingSave = false;
          updateButtonStatus();
        }, 2500);

        // 2. Đồng thời lưu vào đám mây Firestore collections
        try {
          chrome.runtime.sendMessage({
            action: "SAVE_TO_COLLECTION",
            name: playlistTitle,
            url: targetUrl,
            items: fallbackItems
          }, (res) => {
            clearTimeout(safetyTimer);
            if (chrome.runtime.lastError) {
              console.warn("[Kahoii] runtime error:", chrome.runtime.lastError.message);
            }
            if (res && res.alreadyExists) {
              showToast("Đã có trong BST!", `Danh sách "${playlistTitle}" đã có sẵn trong Bộ sưu tập.`, "ℹ️", 3500);
            } else if (res && !res.success && res.error && res.error.includes("Chưa đăng nhập")) {
              showToast("Đang lưu BST!", `KahoiiMusic đang lưu "${playlistTitle}" vào BST. (💡 Mở tiện ích đăng nhập để đồng bộ đám mây)`, "💾", 4500);
            } else {
              showToast("Lưu vào Bộ sưu tập", `KahoiiMusic đang tải và lưu "${playlistTitle}" (${fallbackItems.length} bài) vào Bộ sưu tập...`, "💾", 4500);
            }

            if (!cachedCollections.some(c => (c.name || c.Name || "").toLowerCase().trim() === playlistTitle.toLowerCase().trim())) {
              cachedCollections.unshift({ Name: playlistTitle, Items: fallbackItems });
            }

            setTimeout(() => {
              isProcessingSave = false;
              updateButtonStatus();
            }, 600);
          });
        } catch (err) {
          clearTimeout(safetyTimer);
          isProcessingSave = false;
          updateButtonStatus();
        }
      });

      group.appendChild(btnSaveCol);
    }

    container.appendChild(group);
    updateButtonStatus();
  }

  // Periodic check & YouTube SPA Navigation listeners
  setInterval(injectButtons, 1200);

  window.addEventListener("yt-navigate-finish", () => {
    setTimeout(() => {
      injectButtons();
      syncCloudData();
    }, 500);
  });

  window.addEventListener("load", () => {
    setTimeout(() => {
      injectButtons();
      syncCloudData();
    }, 800);
  });

  // Initial sync
  syncCloudData();
})();
