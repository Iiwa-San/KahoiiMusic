// KahoiiMusic Extension - Popup Script
document.addEventListener("DOMContentLoaded", () => {
  // Elements - Views & Header
  const viewLogin = document.getElementById("view-login");
  const viewDashboard = document.getElementById("view-dashboard");
  const userHeaderActions = document.getElementById("user-header-actions");
  const btnLogout = document.getElementById("btn-logout");
  const userEmailDisplay = document.getElementById("user-email-display");
  const btnLoginStrip = document.getElementById("btn-login-strip");
  const badgeConnected = document.getElementById("badge-connected");
  const userAvatarImg = document.getElementById("user-avatar-img");
  const userAvatarPlaceholder = document.getElementById("user-avatar-placeholder");
  const btnSkipLogin = document.getElementById("btn-skip-login");

  // Elements - Auth (Login / Register / Forgot Password)
  const authTitle = document.getElementById("auth-title");
  const authDesc = document.getElementById("auth-desc");
  const tabLogin = document.getElementById("tab-login");
  const tabRegister = document.getElementById("tab-register");
  const formAuth = document.getElementById("form-auth");
  const inputEmail = document.getElementById("input-email");
  const inputPassword = document.getElementById("input-password");
  const groupConfirmPassword = document.getElementById("group-confirm-password");
  const inputConfirmPassword = document.getElementById("input-confirm-password");
  const btnForgotPassword = document.getElementById("btn-forgot-password");
  const btnAuthSubmit = document.getElementById("btn-auth-submit");
  const loginError = document.getElementById("login-error");

  let currentAuthMode = "LOGIN"; // "LOGIN" or "REGISTER"

  // Elements - Video Detection
  const currentVideoBox = document.getElementById("current-video-box");
  const noVideoBox = document.getElementById("no-video-box");
  const videoThumb = document.getElementById("video-thumb");
  const videoTitle = document.getElementById("video-title");
  const videoAuthor = document.getElementById("video-author");
  const videoDurationBadge = document.getElementById("video-duration-badge");
  const playlistTag = document.getElementById("playlist-tag");
  const playlistTagName = document.getElementById("playlist-tag-name");
  const btnRefreshTab = document.getElementById("btn-refresh-tab");

  // Elements - Manual Input
  const manualUrlInput = document.getElementById("manual-url-input");
  const btnManualFetch = document.getElementById("btn-manual-fetch");

  // Elements - Action Buttons
  const btnPlayQueue = document.getElementById("btn-play-queue");
  const btnPlayTitle = document.getElementById("btn-play-title");
  const btnPlaySub = document.getElementById("btn-play-sub");
  const btnDownloadTrack = document.getElementById("btn-download-track");
  const btnSaveCollection = document.getElementById("btn-save-collection");
  const btnSaveColSub = document.getElementById("btn-save-col-sub");
  const actionStatusMsg = document.getElementById("action-status-msg");
  const btnOpenApp = document.getElementById("btn-open-app");
  const btnHeaderDownload = document.getElementById("btn-header-download");
  const btnDownloadAppLogin = document.getElementById("btn-download-app-login");
  const btnDownloadAppFooter = document.getElementById("btn-download-app-footer");
  const modalDownload = document.getElementById("modal-download");
  const btnCloseModal = document.getElementById("btn-close-modal");

  let currentSession = null;
  let currentTargetUrl = null;
  let currentVideoId = null;
  let currentTitle = "YouTube Video";
  let currentAuthor = "YouTube";
  let currentDurationSeconds = 0;
  let currentPlaylistTitle = "Danh sách YouTube";
  let isCurrentPlaylist = false;
  let currentPlaylistItems = [];
  let cachedSavedTracks = [];
  let cachedCollections = [];

  // ================= INITIALIZATION ================= //
  checkSession();

  function checkSession() {
    chrome.runtime.sendMessage({ action: "GET_SESSION" }, (res) => {
      if (res && res.session) {
        showDashboard(res.session);
      } else {
        showLoginView();
      }
    });
  }

  function showLoginView() {
    currentSession = null;
    viewLogin.classList.remove("hidden");
    viewDashboard.classList.add("hidden");
    userHeaderActions.classList.add("hidden");
    clearAuthAlert();
  }

  function showDashboard(session) {
    currentSession = session;
    viewLogin.classList.add("hidden");
    viewDashboard.classList.remove("hidden");
    userHeaderActions.classList.remove("hidden");

    if (session) {
      userEmailDisplay.textContent = session.email || "Đã đăng nhập";
      badgeConnected.classList.remove("hidden");
      btnLoginStrip.classList.add("hidden");
      btnLogout.classList.remove("hidden");

      // Set Avatar if available
      renderAvatar(session.avatarBase64);

      // Fetch cloud data to ensure avatar & duplicate tracks cache is up to date
      chrome.runtime.sendMessage({ action: "GET_CLOUD_DATA" }, (cloudRes) => {
        if (cloudRes && cloudRes.avatarBase64) {
          renderAvatar(cloudRes.avatarBase64);
        }
        if (cloudRes && cloudRes.payload) {
          cachedSavedTracks = cloudRes.payload.savedTracks || [];
          cachedCollections = cloudRes.payload.collections || [];
          updateDuplicateBadges();
        }
      });
    } else {
      userEmailDisplay.textContent = "Chế độ ngoại tuyến";
      badgeConnected.classList.add("hidden");
      btnLoginStrip.classList.remove("hidden");
      btnLogout.classList.add("hidden");
      renderAvatar(null);
      cachedSavedTracks = [];
      cachedCollections = [];
      updateDuplicateBadges();
    }

    detectCurrentTab();
  }

  if (btnSkipLogin) {
    btnSkipLogin.addEventListener("click", () => {
      showDashboard(null);
    });
  }

  if (btnLoginStrip) {
    btnLoginStrip.addEventListener("click", () => {
      showLoginView();
    });
  }

  function renderAvatar(base64) {
    if (base64) {
      userAvatarImg.src = "data:image/png;base64," + base64;
      userAvatarImg.classList.remove("hidden");
      userAvatarPlaceholder.classList.add("hidden");
    } else {
      userAvatarImg.classList.add("hidden");
      userAvatarPlaceholder.classList.remove("hidden");
    }
  }

  // ================= AUTH TABS & HANDLERS ================= //
  function clearAuthAlert() {
    if (loginError) {
      loginError.textContent = "";
      loginError.className = "alert-box error hidden";
    }
  }

  function showAuthAlert(msg, type = "error") {
    if (!loginError) return;
    loginError.textContent = msg;
    loginError.className = `alert-box ${type}`;
    loginError.classList.remove("hidden");
  }

  function switchAuthTab(mode) {
    currentAuthMode = mode;
    clearAuthAlert();

    if (mode === "REGISTER") {
      tabRegister.classList.add("active");
      tabLogin.classList.remove("active");
      groupConfirmPassword.classList.remove("hidden");
      inputConfirmPassword.setAttribute("required", "required");
      btnForgotPassword.classList.add("hidden");
      if (authTitle) authTitle.textContent = "Tạo tài khoản mới";
      if (authDesc) authDesc.textContent = "Đăng ký để đồng bộ danh sách bài hát giữa PC, Mobile và Extension.";
      btnAuthSubmit.querySelector(".btn-text").textContent = "Đăng ký ngay";
    } else {
      tabLogin.classList.add("active");
      tabRegister.classList.remove("active");
      groupConfirmPassword.classList.add("hidden");
      inputConfirmPassword.removeAttribute("required");
      btnForgotPassword.classList.remove("hidden");
      if (authTitle) authTitle.textContent = "Đăng nhập tài khoản";
      if (authDesc) authDesc.textContent = "Đăng nhập bằng tài khoản KahoiiMusic để đồng bộ với ứng dụng.";
      btnAuthSubmit.querySelector(".btn-text").textContent = "Đăng nhập";
    }
  }

  if (tabLogin) {
    tabLogin.addEventListener("click", () => switchAuthTab("LOGIN"));
  }

  if (tabRegister) {
    tabRegister.addEventListener("click", () => switchAuthTab("REGISTER"));
  }

  // Quên mật khẩu
  if (btnForgotPassword) {
    btnForgotPassword.addEventListener("click", () => {
      clearAuthAlert();
      const email = inputEmail.value.trim();
      if (!email || !email.includes("@")) {
        showAuthAlert("Vui lòng nhập địa chỉ Email vào ô phía trên rồi bấm 'Quên mật khẩu?'.", "error");
        inputEmail.focus();
        return;
      }

      setLoading(btnAuthSubmit, true, "Đang gửi liên kết...");
      chrome.runtime.sendMessage({ action: "FORGOT_PASSWORD", email }, (res) => {
        setLoading(btnAuthSubmit, false, currentAuthMode === "REGISTER" ? "Đăng ký ngay" : "Đăng nhập");
        if (!res || !res.success) {
          showAuthAlert(res?.error || "Gửi yêu cầu đặt lại mật khẩu thất bại.", "error");
          return;
        }
        showAuthAlert(res.message || "Đã gửi email đặt lại mật khẩu! Vui lòng kiểm tra hộp thư của bạn.", "success");
      });
    });
  }

  // Submit Form Auth (Login or Register)
  if (formAuth) {
    formAuth.addEventListener("submit", (e) => {
      e.preventDefault();
      clearAuthAlert();

      const email = inputEmail.value.trim();
      const password = inputPassword.value.trim();

      if (!email || !password) {
        showAuthAlert("Vui lòng điền đầy đủ Email và Mật khẩu.", "error");
        return;
      }

      if (currentAuthMode === "REGISTER") {
        const confirmPassword = inputConfirmPassword.value.trim();
        if (password.length < 6) {
          showAuthAlert("Mật khẩu phải có độ dài tối thiểu 6 ký tự.", "error");
          return;
        }
        if (password !== confirmPassword) {
          showAuthAlert("Mật khẩu xác nhận không trùng khớp. Vui lòng kiểm tra lại.", "error");
          return;
        }

        setLoading(btnAuthSubmit, true, "Đang tạo tài khoản...");

        chrome.runtime.sendMessage({ action: "REGISTER", email, password }, (res) => {
          setLoading(btnAuthSubmit, false, "Đăng ký ngay");

          if (!res || !res.success) {
            showAuthAlert(res?.error || "Đăng ký thất bại. Vui lòng thử lại.", "error");
            return;
          }

          inputPassword.value = "";
          inputConfirmPassword.value = "";
          showDashboard(res.user);
        });
      } else {
        // LOGIN
        setLoading(btnAuthSubmit, true, "Đang đăng nhập...");

        chrome.runtime.sendMessage({ action: "LOGIN", email, password }, (res) => {
          setLoading(btnAuthSubmit, false, "Đăng nhập");

          if (!res || !res.success) {
            showAuthAlert(res?.error || "Đăng nhập thất bại. Vui lòng thử lại.", "error");
            return;
          }

          inputPassword.value = "";
          showDashboard(res.user);
        });
      }
    });
  }

  btnLogout.addEventListener("click", () => {
    if (confirm("Bạn có chắc chắn muốn đăng xuất?")) {
      chrome.runtime.sendMessage({ action: "LOGOUT" }, () => {
        showLoginView();
      });
    }
  });

  // ================= PROTOCOL TRIGGER HELPER ================= //
  function triggerProtocol(protocolUrl) {
    const iframe = document.createElement("iframe");
    iframe.style.display = "none";
    iframe.src = protocolUrl;
    document.body.appendChild(iframe);
    setTimeout(() => {
      try { iframe.remove(); } catch { }
    }, 1500);

    try {
      window.location.href = protocolUrl;
    } catch { }
  }

  // ================= VIDEO / PLAYLIST DETECTION ================= //
  btnRefreshTab.addEventListener("click", () => {
    detectCurrentTab();
  });

  function extractYouTubeVideoId(urlStr) {
    try {
      const url = new URL(urlStr);
      if (url.hostname.includes("youtube.com")) return url.searchParams.get("v");
      if (url.hostname.includes("youtu.be")) return url.pathname.slice(1).split("?")[0];
    } catch { }
    return null;
  }

  function extractYouTubeListId(urlStr) {
    try {
      const url = new URL(urlStr);
      return url.searchParams.get("list");
    } catch { }
    return null;
  }

  function formatDuration(seconds) {
    if (!seconds || seconds <= 0) return "--:--";
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    const sStr = s < 10 ? "0" + s : "" + s;
    if (h > 0) {
      const mStr = m < 10 ? "0" + m : "" + m;
      return `${h}:${mStr}:${sStr}`;
    }
    return `${m}:${sStr}`;
  }

  function detectCurrentTab() {
    clearStatus();
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      const currentTab = tabs && tabs[0];
      if (!currentTab || !currentTab.url) {
        showNoContentUI();
        return;
      }

      const videoId = extractYouTubeVideoId(currentTab.url);
      const listId = extractYouTubeListId(currentTab.url);

      if (!videoId && !listId) {
        showNoContentUI();
        return;
      }

      currentTargetUrl = currentTab.url;
      currentVideoId = videoId;
      isCurrentPlaylist = !!listId;

      const fallbackTitle = (currentTab.title || "").replace(/ - YouTube$/, "").trim() || "YouTube Track";
      currentTitle = fallbackTitle;

      setContentPreview(videoId, fallbackTitle, "YouTube", 0, isCurrentPlaylist, "Đang tải tên playlist...");

      // Read active tab DOM for playlist title & duration
      try {
        chrome.scripting.executeScript(
          {
            target: { tabId: currentTab.id },
            func: () => {
              // 1. Duration
              let dur = 0;
              const v = document.querySelector("video");
              if (v && v.duration && !isNaN(v.duration) && v.duration > 0) {
                dur = Math.round(v.duration);
              } else {
                const metaDur = document.querySelector('meta[itemprop="duration"]')?.getAttribute("content");
                if (metaDur) {
                  const match = metaDur.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
                  if (match) {
                    const h = parseInt(match[1] || 0, 10);
                    const m = parseInt(match[2] || 0, 10);
                    const s = parseInt(match[3] || 0, 10);
                    dur = h * 3600 + m * 60 + s;
                  }
                }
              }

              // 2. Playlist Title
              const plSelectors = [
                "ytd-playlist-panel-renderer #header-description a",
                "ytd-playlist-panel-renderer #title",
                "h1.ytd-playlist-header-renderer",
                "#header-description a",
                "yt-formatted-string.ytd-playlist-panel-renderer",
                "#playlist-header-description h3",
                "ytd-watch-metadata ytd-playlist-panel-renderer #title"
              ];

              let plTitle = "";
              for (const sel of plSelectors) {
                const el = document.querySelector(sel);
                if (el && el.textContent && el.textContent.trim()) {
                  const t = el.textContent.trim();
                  if (t.length > 1 && !t.startsWith("http")) {
                    plTitle = t;
                    break;
                  }
                }
              }

              // 3. Extract Playlist Items (Tracks)
              const items = [];
              // Case A: ytd-playlist-panel-video-renderer (Watch page with playlist panel)
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
                    items.push({
                      Id: vid,
                      Title: title,
                      Author: author,
                      DurationSeconds: 0
                    });
                  }
                });
              }

              // Case B: ytd-playlist-video-renderer (Playlist page: /playlist?list=...)
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
                      items.push({
                        Id: vid,
                        Title: title,
                        Author: author,
                        DurationSeconds: 0
                      });
                    }
                  });
                }
              }

              return { duration: dur, playlistTitle: plTitle, playlistItems: items };
            }
          },
          (results) => {
            const data = results && results[0] && results[0].result;
            if (data) {
              if (data.duration > 0) {
                currentDurationSeconds = data.duration;
                videoDurationBadge.textContent = formatDuration(data.duration);
              }
              if (data.playlistTitle) {
                currentPlaylistTitle = data.playlistTitle;
                playlistTagName.textContent = "Playlist: " + currentPlaylistTitle;
                btnSaveColSub.textContent = `Lưu với tên: "${currentPlaylistTitle}"`;
              }
              if (Array.isArray(data.playlistItems) && data.playlistItems.length > 0) {
                currentPlaylistItems = data.playlistItems;
                btnSaveColSub.textContent = `Lưu với tên: "${currentPlaylistTitle}" (${currentPlaylistItems.length} bài)`;
              }
            }
          }
        );
      } catch { }

      // Fetch metadata from oEmbed
      if (videoId) {
        chrome.runtime.sendMessage({ action: "FETCH_OEMBED", videoId }, (meta) => {
          if (meta && meta.success && meta.title) {
            currentTitle = meta.title;
            currentAuthor = meta.author || "YouTube";
            currentDurationSeconds = meta.duration || currentDurationSeconds;
            setContentPreview(videoId, currentTitle, currentAuthor, currentDurationSeconds, isCurrentPlaylist, currentPlaylistTitle);
          }
        });
      }
    });
  }

  function setContentPreview(videoId, title, author, durationSeconds, isPlaylist, plTitle) {
    if (videoId) {
      videoThumb.src = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
    } else {
      videoThumb.src = `https://www.youtube.com/img/desktop/yt_1200.png`;
    }

    videoTitle.textContent = title;
    videoTitle.title = title;
    videoAuthor.textContent = author;
    videoDurationBadge.textContent = formatDuration(durationSeconds);

    if (isPlaylist) {
      playlistTag.classList.remove("hidden");
      currentPlaylistTitle = plTitle || currentPlaylistTitle || "Danh sách YouTube";
      playlistTagName.textContent = "Playlist: " + currentPlaylistTitle;

      btnPlayTitle.textContent = "Nạp phát cả danh sách";
      btnPlaySub.textContent = "Phát toàn bộ danh sách trên KahoiiMusic";

      btnSaveCollection.classList.remove("hidden");
      btnSaveColSub.textContent = `Lưu vào BST với tên: "${currentPlaylistTitle}"`;
    } else {
      playlistTag.classList.add("hidden");
      btnPlayTitle.textContent = "Nạp phát trên Kahoii";
      btnPlaySub.textContent = "Phát bài hát trên KahoiiMusic";

      btnSaveCollection.classList.add("hidden");
    }

    currentVideoBox.classList.remove("hidden");
    noVideoBox.classList.add("hidden");
    btnPlayQueue.disabled = false;
    btnDownloadTrack.disabled = !videoId;

    updateDuplicateBadges();
  }

  function updateDuplicateBadges() {
    // 1. Kiểm tra bài hát đang xem có trong "Đã tải" (Saved tracks) chưa
    if (currentVideoId && Array.isArray(cachedSavedTracks)) {
      const isSaved = cachedSavedTracks.some((t) => {
        const id = (t.Id || t.id || t.videoId || "").toLowerCase();
        return id === currentVideoId.toLowerCase();
      });

      const mainTxt = btnDownloadTrack.querySelector(".main-txt");
      const subTxt = btnDownloadTrack.querySelector(".sub-txt");
      const icon = btnDownloadTrack.querySelector(".btn-icon");

      if (isSaved) {
        btnDownloadTrack.classList.add("already-saved");
        if (mainTxt) mainTxt.textContent = "Đã có trong mục Đã tải";
        if (subTxt) subTxt.textContent = "Bài hát này đã được tải về máy (Offline)";
        if (icon) icon.textContent = "✓";
      } else {
        btnDownloadTrack.classList.remove("already-saved");
        if (mainTxt) mainTxt.textContent = "Tải về";
        if (subTxt) subTxt.textContent = "Lưu vào mục Đã tải trong KahoiiMusic để nghe Offline";
        if (icon) icon.textContent = "📥";
      }
    }

    // 2. Kiểm tra playlist có trong "Bộ sưu tập" chưa
    if (isCurrentPlaylist && currentPlaylistTitle && Array.isArray(cachedCollections)) {
      const isColSaved = cachedCollections.some((c) => {
        const name = (c.name || c.Name || "").toLowerCase().trim();
        return name === currentPlaylistTitle.toLowerCase().trim();
      });

      const mainTxt = btnSaveCollection.querySelector(".main-txt");
      const subTxt = btnSaveCollection.querySelector(".sub-txt");
      const icon = btnSaveCollection.querySelector(".btn-icon");

      if (isColSaved) {
        btnSaveCollection.classList.add("already-saved");
        if (mainTxt) mainTxt.textContent = "Đã lưu vào Bộ sưu tập";
        if (subTxt) subTxt.textContent = `Danh sách "${currentPlaylistTitle}" đã có trong BST`;
        if (icon) icon.textContent = "✓";
      } else {
        btnSaveCollection.classList.remove("already-saved");
        if (mainTxt) mainTxt.textContent = "Lưu vào Bộ sưu tập";
        if (subTxt) subTxt.textContent = `Lưu với tên: "${currentPlaylistTitle}"`;
        if (icon) icon.textContent = "💾";
      }
    }
  }

  function showNoContentUI() {
    currentTargetUrl = null;
    currentVideoId = null;
    currentVideoBox.classList.add("hidden");
    noVideoBox.classList.remove("hidden");
    btnPlayQueue.disabled = true;
    btnDownloadTrack.disabled = true;
    btnSaveCollection.classList.add("hidden");
  }

  // Manual input fetch
  btnManualFetch.addEventListener("click", () => {
    const rawUrl = manualUrlInput.value.trim();
    if (!rawUrl) return;

    const videoId = extractYouTubeVideoId(rawUrl);
    const listId = extractYouTubeListId(rawUrl);

    if (!videoId && !listId) {
      alert("Đường dẫn YouTube không hợp lệ! Vui lòng kiểm tra lại link.");
      return;
    }

    currentTargetUrl = rawUrl;
    currentVideoId = videoId;
    isCurrentPlaylist = !!listId;

    btnManualFetch.textContent = "⏳...";
    btnManualFetch.disabled = true;

    if (videoId) {
      chrome.runtime.sendMessage({ action: "FETCH_OEMBED", videoId }, (meta) => {
        btnManualFetch.textContent = "Kiểm tra";
        btnManualFetch.disabled = false;

        currentTitle = (meta && meta.title) ? meta.title : `YouTube Track (${videoId})`;
        currentAuthor = (meta && meta.author) ? meta.author : "YouTube";
        currentDurationSeconds = (meta && meta.duration) ? meta.duration : 0;

        setContentPreview(videoId, currentTitle, currentAuthor, currentDurationSeconds, isCurrentPlaylist, isCurrentPlaylist ? "Danh sách YouTube" : "");
        manualUrlInput.value = "";
      });
    } else {
      btnManualFetch.textContent = "Kiểm tra";
      btnManualFetch.disabled = false;
      setContentPreview(null, "YouTube Playlist", "YouTube", 0, true, "Danh sách YouTube");
      manualUrlInput.value = "";
    }
  });

  // ================= ACTION 1: NẠP PHÁT DANH SÁCH / BÀI HÁT ================= //
  btnPlayQueue.addEventListener("click", () => {
    if (!currentTargetUrl) return;

    clearStatus();
    const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
    triggerProtocol(`kahoiimusic://play?url=${encodeURIComponent(currentTargetUrl)}${userParam}`);

    if (isCurrentPlaylist) {
      showStatus("🚀 Đã gửi toàn bộ danh sách sang KahoiiMusic để phát!", "success");
    } else {
      showStatus("🚀 Đã gửi bài hát sang KahoiiMusic để phát!", "success");
    }
  });

  // ================= ACTION 2: TẢI BÀI HÁT NÀY VỀ MÁY (OFFLINE) ================= //
  btnDownloadTrack.addEventListener("click", () => {
    if (!currentVideoId) return;

    clearStatus();

    // 1. Gửi lệnh qua giao thức để KahoiiMusic tải trực tiếp (kèm user để kiểm tra tài khoản)
    const durParam = currentDurationSeconds > 0 ? `&dur=${currentDurationSeconds}` : "";
    const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
    triggerProtocol(`kahoiimusic://download?v=${encodeURIComponent(currentVideoId)}&title=${encodeURIComponent(currentTitle)}&author=${encodeURIComponent(currentAuthor)}${durParam}${userParam}`);

    // 2. Đồng thời lưu vào đám mây Firestore mục savedTracks của tài khoản đang đăng nhập trên extension
    chrome.runtime.sendMessage(
      {
        action: "SAVE_TO_OFFLINE",
        track: {
          Id: currentVideoId,
          Title: currentTitle,
          Author: currentAuthor,
          DurationSeconds: currentDurationSeconds
        }
      },
      (res) => {
        if (res && res.alreadyExists) {
          showStatus(`ℹ️ "${currentTitle}" đã có trong mục Đã tải!`, "info");
        } else if (res && !res.success && res.error && res.error.includes("Chưa đăng nhập")) {
          showStatus(`📥 Đã gửi lệnh tải "${currentTitle}" sang KahoiiMusic! (Đăng nhập để đồng bộ đám mây)`, "info");
        } else {
          showStatus(`📥 Đang bắt đầu tải "${currentTitle}" về mục Đã tải trong KahoiiMusic!`, "success");
        }
        if (!cachedSavedTracks.some(t => (t.Id || t.id || '') === currentVideoId)) {
          cachedSavedTracks.unshift({ Id: currentVideoId, Title: currentTitle, Author: currentAuthor, DurationSeconds: currentDurationSeconds });
        }
        updateDuplicateBadges();
      }
    );
  });

  // ================= ACTION 3: LƯU PLAYLIST VÀO BỘ SƯU TẬP ================= //
  btnSaveCollection.addEventListener("click", () => {
    if (!currentTargetUrl || !isCurrentPlaylist) return;

    clearStatus();
    const finalTitle = currentPlaylistTitle || "Playlist YouTube";

    // Gửi lệnh qua giao thức kèm tài khoản để KahoiiMusic đối chiếu
    const userParam = currentSession && currentSession.email ? `&user=${encodeURIComponent(currentSession.email)}` : "";
    triggerProtocol(`kahoiimusic://save_collection?url=${encodeURIComponent(currentTargetUrl)}&title=${encodeURIComponent(finalTitle)}${userParam}`);

    // Chuẩn bị danh sách bài hát đầy đủ để lưu vào Firestore (tránh lưu rỗng [])
    let itemsToSave = currentPlaylistItems;
    if ((!itemsToSave || itemsToSave.length === 0) && currentVideoId) {
      itemsToSave = [{
        Id: currentVideoId,
        Title: currentTitle,
        Author: currentAuthor,
        DurationSeconds: currentDurationSeconds
      }];
    }

    chrome.runtime.sendMessage(
      {
        action: "SAVE_TO_COLLECTION",
        name: finalTitle,
        url: currentTargetUrl,
        items: itemsToSave
      },
      (res) => {
        if (res && res.alreadyExists) {
          showStatus(`ℹ️ "${finalTitle}" đã có trong Bộ sưu tập!`, "info");
        } else if (res && !res.success && res.error && res.error.includes("Chưa đăng nhập")) {
          showStatus(`💾 Đã gửi lưu "${finalTitle}" sang KahoiiMusic! (Đăng nhập để đồng bộ đám mây)`, "info");
        } else {
          showStatus(`💾 Đang tải & lưu "${finalTitle}" vào Bộ sưu tập KahoiiMusic!`, "success");
        }

        if (!cachedCollections.some(c => (c.name || c.Name || '').toLowerCase().trim() === finalTitle.toLowerCase().trim())) {
          cachedCollections.unshift({ Name: finalTitle, Items: itemsToSave });
        }
        updateDuplicateBadges();
      }
    );
  });

  // ================= ACTION: OPEN APP ================= //
  if (btnOpenApp) {
    btnOpenApp.addEventListener("click", (e) => {
      e.preventDefault();
      triggerProtocol("kahoiimusic://open");
    });
  }

  // ================= MODAL: DOWNLOAD APPS ================= //
  function openDownloadModal() {
    if (modalDownload) modalDownload.classList.remove("hidden");
  }

  function closeDownloadModal() {
    if (modalDownload) modalDownload.classList.add("hidden");
  }

  if (btnHeaderDownload) btnHeaderDownload.addEventListener("click", openDownloadModal);
  if (btnDownloadAppLogin) btnDownloadAppLogin.addEventListener("click", openDownloadModal);
  if (btnDownloadAppFooter) btnDownloadAppFooter.addEventListener("click", openDownloadModal);
  if (btnCloseModal) btnCloseModal.addEventListener("click", closeDownloadModal);

  if (modalDownload) {
    modalDownload.addEventListener("click", (e) => {
      if (e.target === modalDownload) {
        closeDownloadModal();
      }
    });
  }

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && modalDownload && !modalDownload.classList.contains("hidden")) {
      closeDownloadModal();
    }
  });

  // ================= STATUS HELPERS ================= //
  function showStatus(text, type) {
    actionStatusMsg.textContent = text;
    actionStatusMsg.className = `action-status-msg ${type}`;
    actionStatusMsg.classList.remove("hidden");
  }

  function clearStatus() {
    actionStatusMsg.textContent = "";
    actionStatusMsg.className = "action-status-msg hidden";
  }

  function setLoading(btn, isLoading, text) {
    const textSpan = btn.querySelector(".btn-text");
    const spinner = btn.querySelector(".spinner");
    if (isLoading) {
      btn.disabled = true;
      if (textSpan) textSpan.textContent = text;
      if (spinner) spinner.classList.remove("hidden");
    } else {
      btn.disabled = false;
      if (textSpan) textSpan.textContent = text;
      if (spinner) spinner.classList.add("hidden");
    }
  }
});
