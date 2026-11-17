import React, { useState, useEffect, useRef } from "react";
import {
  Play,
  Pause,
  SkipBack,
  SkipForward,
  Repeat,
  Repeat1,
  Volume2,
  VolumeX,
  Search,
  Heart,
  X,
  Loader2,
  Clock,
  ArrowDownAZ,
  ClipboardPaste,
  User,
  ShieldCheck,
  RefreshCw,
  Menu,
  LogOut,
  Check,
  LogIn,
  AlertCircle,
  ArrowLeft,
} from "lucide-react";
import type { Track } from "./types";
import { formatTime } from "./utils/lyricsParser";
import { loadAndExtractTheme, type ThemePalette, DEFAULT_THEME } from "./utils/colorExtractor";

// Clean, robust SVG placeholder used only when a song has no thumbnail
const DEFAULT_TRACK_ART =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect width='100' height='100' rx='16' fill='%23240a50'/%3E%3Ccircle cx='50' cy='50' r='30' fill='%23381078'/%3E%3Cpath d='M44 36v20.5a6.5 6.5 0 1 1-3-5.5V40l18-4v16.5a6.5 6.5 0 1 1-3-5.5V32z' fill='%23e6007e'/%3E%3C/svg%3E";

// World-Famous Artists (Real artists currently dominating worldwide charts)
const WORLD_FAMOUS_ARTISTS = [
  {
    name: "Taylor Swift",
    genre: "Pop Superstar",
    avatar: "/artists/taylor_swift.jpg",
    query: "Taylor Swift",
  },
  {
    name: "The Weeknd",
    genre: "R&B / Pop",
    avatar: "/artists/the_weeknd.jpg",
    query: "The Weeknd",
  },
  {
    name: "Billie Eilish",
    genre: "Alt / Pop",
    avatar: "/artists/billie_eilish.jpg",
    query: "Billie Eilish",
  },
  {
    name: "Bruno Mars",
    genre: "Pop / Funk",
    avatar: "/artists/bruno_mars.jpg",
    query: "Bruno Mars",
  },
  {
    name: "Ariana Grande",
    genre: "Pop / R&B",
    avatar: "/artists/ariana_grande.jpg",
    query: "Ariana Grande",
  },
  {
    name: "Alan Walker",
    genre: "EDM / Electro",
    avatar: "/artists/alan_walker.jpg",
    query: "Alan Walker",
  },
];

type LeftTab = "library" | "favorites" | "history";

const SIDEBAR_TABS: { key: LeftTab; label: string }[] = [
  { key: "library", label: "Thư viện" },
  { key: "favorites", label: "Yêu thích" },
  { key: "history", label: "Lịch sử" },
];

export default function App() {
  const [playlist, setPlaylist] = useState<Track[]>([]);
  const [queueTracks, setQueueTracks] = useState<Track[]>([]);
  const [currentIndex, setCurrentIndex] = useState<number>(0);
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [duration, setDuration] = useState<number>(0);
  const [volume, setVolume] = useState<number>(() => {
    const saved = localStorage.getItem("kahoii_web_volume");
    return saved ? parseFloat(saved) : 0.8;
  });
  const [isMuted, setIsMuted] = useState<boolean>(false);
  const [repeatMode, setRepeatMode] = useState<"off" | "all" | "one">("all");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Left Sidebar
  const [activeLeftTab, setActiveLeftTab] = useState<LeftTab>("library");
  const [leftSearchQuery, setLeftSearchQuery] = useState<string>("");
  const activeTabIndex = Math.max(0, SIDEBAR_TABS.findIndex((t) => t.key === activeLeftTab));

  // User Profile & Real Database Session
  const [isAccountOpen, setIsAccountOpen] = useState<boolean>(false);
  const [userSession, setUserSession] = useState<{
    isLoggedIn: boolean;
    user: {
      localId?: string;
      email?: string;
      name?: string;
      avatarUrl?: string;
      avatarBase64?: string | null;
      stats?: {
        favoritesCount: number;
        historyCount: number;
        playlistsCount: number;
      };
    } | null;
  } | null>(null);

  const [libraryData, setLibraryData] = useState<{
    favorites: Track[];
    history: Track[];
    playlists: any[];
  }>({
    favorites: [],
    history: [],
    playlists: [],
  });

  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [syncSuccessMsg, setSyncSuccessMsg] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState<boolean>(false);
  const [loginEmail, setLoginEmail] = useState<string>("");
  const [loginPassword, setLoginPassword] = useState<string>("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [avatarKey, setAvatarKey] = useState<number>(Date.now());

  // Top Search State
  const [topSearchText, setTopSearchText] = useState<string>("");
  const [searchQueryTitle, setSearchQueryTitle] = useState<string>("");
  const [isSearchOpen, setIsSearchOpen] = useState<boolean>(false);
  const [searchResults, setSearchResults] = useState<Track[]>([]);
  const [isSearching, setIsSearching] = useState<boolean>(false);

  // Dynamic Theme Palette matching KahoiiMusic desktop ColorExtractorService
  const [currentTheme, setCurrentTheme] = useState<ThemePalette>(DEFAULT_THEME);

  // Volume slider popover
  const [showVolumeSlider, setShowVolumeSlider] = useState<boolean>(false);

  const audioRef = useRef<HTMLAudioElement | null>(null);

  const currentTrack = playlist[currentIndex] || null;

  // Apply theme CSS custom variables to document root for seamless 1.2s color transitions
  useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty("--theme-bg-0", currentTheme.max0);
    root.style.setProperty("--theme-bg-1", currentTheme.max1);
    root.style.setProperty("--theme-bg-2", currentTheme.max2);
    root.style.setProperty("--theme-bg-3", currentTheme.max3);
    root.style.setProperty("--theme-accent", currentTheme.accent);
    root.style.setProperty("--theme-sec-accent", currentTheme.secondaryAccent);
    root.style.setProperty("--theme-panel-bg", currentTheme.panelBg);
    root.style.setProperty("--theme-panel-border", currentTheme.panelBorder);
    root.style.setProperty("--theme-btn-gradient", currentTheme.btnGradient);
  }, [currentTheme]);

  // Extract color palette dynamically whenever the current playing track changes
  useEffect(() => {
    if (!currentTrack) return;
    const thumbUrl =
      currentTrack.thumbnail && !currentTrack.thumbnail.includes("figma")
        ? currentTrack.thumbnail
        : currentTrack.id
        ? `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg`
        : "";

    if (thumbUrl) {
      loadAndExtractTheme(thumbUrl).then((palette) => {
        setCurrentTheme(palette);
      });
    }
  }, [currentTrack?.id]);

  // Persist volume
  useEffect(() => {
    localStorage.setItem("kahoii_web_volume", volume.toString());
    if (audioRef.current) {
      audioRef.current.volume = isMuted ? 0 : volume;
    }
  }, [volume, isMuted]);

  // Fetch User Session & Library Data
  const fetchUserSession = async () => {
    try {
      const res = await fetch("/api/user/session");
      if (res.ok) {
        const data = await res.json();
        setUserSession(data);
      }
    } catch (err) {
      console.error("Session fetch failed:", err);
    }
  };

  const fetchLibraryData = async () => {
    try {
      const res = await fetch("/api/user/library");
      if (res.ok) {
        const data = await res.json();
        const mappedFavs: Track[] = (data.favorites || []).map((f: any) => ({
          id: f.id,
          title: f.title,
          author: f.author || "Kahoii Music",
          duration: f.duration || 180,
          thumbnail: `https://i.ytimg.com/vi/${f.id}/mqdefault.jpg`,
        }));
        const mappedHist: Track[] = (data.history || []).map((h: any) => ({
          id: h.id,
          title: h.title,
          author: h.author || "Kahoii Music",
          duration: h.duration || 180,
          thumbnail: `https://i.ytimg.com/vi/${h.id}/mqdefault.jpg`,
        }));
        const rawPlaylists = (data.playlists || []).map((pl: any) => ({
          id: pl.id || pl.Id,
          name: pl.name || pl.Name || "Danh sách phát",
          created: pl.created || pl.CreatedAt,
          items: (pl.items || pl.Items || []).map((it: any) => ({
            id: it.id || it.Id,
            title: it.title || it.Title,
            author: it.author || it.Author || it.Artist || "",
            duration: it.duration || it.DurationSeconds || 0,
            thumbnail: `https://i.ytimg.com/vi/${it.id || it.Id}/mqdefault.jpg`,
          })),
        }));
        setLibraryData({
          favorites: mappedFavs,
          history: mappedHist,
          playlists: rawPlaylists,
        });

        // Initialize playlist & queue exclusively with real user tracks
        const initialTracks: Track[] =
          mappedHist.length > 0
            ? mappedHist
            : mappedFavs.length > 0
            ? mappedFavs
            : rawPlaylists[0]?.items?.length > 0
            ? rawPlaylists[0].items
            : [];

        if (initialTracks.length > 0) {
          setPlaylist((prev) => (prev.length === 0 ? initialTracks : prev));
          setQueueTracks((prev) => (prev.length === 0 ? initialTracks : prev));
          loadAndPlayTrack(initialTracks[0], false);
        }
      }
    } catch (err) {
      console.error("Library fetch failed:", err);
    }
  };

  const searchArtistSongs = async (artistName: string) => {
    setTopSearchText(artistName);
    setSearchQueryTitle(artistName);
    setIsSearchOpen(true);
    setIsSearching(true);
    setSearchResults([]);
    try {
      const res = await fetch(`/api/search?q=${encodeURIComponent(artistName + " top songs")}`);
      const data = await res.json();
      setSearchResults(data.results || []);
    } catch (err) {
      console.error("Search error:", err);
    } finally {
      setIsSearching(false);
    }
  };

  // Preload initial track metadata & user data on mount
  useEffect(() => {
    fetchUserSession();
    fetchLibraryData();
    if (playlist.length > 0) {
      loadAndPlayTrack(playlist[0], false);
    }
  }, []);

  const handleSyncCloud = async () => {
    setIsSyncing(true);
    setSyncSuccessMsg(null);
    try {
      const res = await fetch("/api/user/sync", { method: "POST" });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Đồng bộ thất bại");
      setUserSession(data);
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setSyncSuccessMsg("Đã đồng bộ dữ liệu mới nhất từ Cloud thành công!");
      setTimeout(() => setSyncSuccessMsg(null), 4000);
    } catch (err: any) {
      alert("Lỗi đồng bộ: " + err.message);
    } finally {
      setIsSyncing(false);
    }
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!loginEmail || !loginPassword) {
      setLoginError("Vui lòng nhập đầy đủ email và mật khẩu");
      return;
    }
    setIsLoggingIn(true);
    setLoginError(null);
    try {
      const res = await fetch("/api/user/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: loginEmail, password: loginPassword }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Đăng nhập thất bại");
      setUserSession(data);
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setLoginPassword("");
      setSyncSuccessMsg("Đăng nhập thành công!");
      setTimeout(() => setSyncSuccessMsg(null), 3000);
    } catch (err: any) {
      setLoginError(err.message || "Lỗi đăng nhập");
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleLogout = async () => {
    if (!window.confirm("Bạn có chắc chắn muốn đăng xuất?")) return;
    try {
      await fetch("/api/user/logout", { method: "POST" });
      setUserSession({ isLoggedIn: false, user: null });
      setLibraryData({ favorites: [], history: [], playlists: [] });
    } catch (err) {
      console.error("Logout error:", err);
    }
  };

  const loadAndPlayTrack = async (track: Track, autoPlay = true) => {
    if (!track) return;
    setIsLoading(true);
    setErrorMessage(null);
    setCurrentTime(0);

    if (audioRef.current) {
      try {
        audioRef.current.pause();
      } catch {}
    }

    try {
      const res = await fetch(`/api/stream?id=${track.id}`);
      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.error || "Không thể trích xuất luồng âm thanh");
      }
      const data = await res.json();

      if (data.streamUrl && audioRef.current) {
        const playableUrl = data.streamUrl.includes("googlevideo.com")
          ? `/api/proxy-audio?url=${encodeURIComponent(data.streamUrl)}`
          : data.streamUrl;

        audioRef.current.src = playableUrl;
        audioRef.current.load();

        if (autoPlay) {
          const playPromise = audioRef.current.play();
          if (playPromise !== undefined) {
            playPromise
              .then(() => setIsPlaying(true))
              .catch((err) => {
                console.warn("Autoplay deferred or prevented:", err);
                setIsPlaying(false);
              });
          }
        } else {
          setIsPlaying(false);
        }
      }
    } catch (err: any) {
      console.error("Playback error:", err);
      setErrorMessage(err.message || "Lỗi nạp bài hát");
      setIsPlaying(false);
    } finally {
      setIsLoading(false);
    }
  };

  const handleTimeUpdate = () => {
    if (!audioRef.current) return;
    setCurrentTime(audioRef.current.currentTime);
  };

  const togglePlayPause = () => {
    if (!audioRef.current) return;
    if (isPlaying) {
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      if (!audioRef.current.src || !currentTrack) {
        if (currentTrack) loadAndPlayTrack(currentTrack, true);
        return;
      }
      const playPromise = audioRef.current.play();
      if (playPromise !== undefined) {
        playPromise
          .then(() => setIsPlaying(true))
          .catch((err) => {
            console.warn("Play error, reloading:", err);
            if (currentTrack) loadAndPlayTrack(currentTrack, true);
          });
      }
    }
  };

  const handleNext = () => {
    if (playlist.length === 0) return;
    const nextIdx = (currentIndex + 1) % playlist.length;
    setCurrentIndex(nextIdx);
    loadAndPlayTrack(playlist[nextIdx], true);
  };

  const handlePrev = () => {
    if (playlist.length === 0) return;
    if (currentTime > 3) {
      if (audioRef.current) audioRef.current.currentTime = 0;
    } else {
      const prevIdx = (currentIndex - 1 + playlist.length) % playlist.length;
      setCurrentIndex(prevIdx);
      loadAndPlayTrack(playlist[prevIdx], true);
    }
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newTime = parseFloat(e.target.value);
    setCurrentTime(newTime);
    if (audioRef.current) {
      audioRef.current.currentTime = newTime;
    }
  };

  const cycleRepeatMode = () => {
    if (repeatMode === "off") setRepeatMode("all");
    else if (repeatMode === "all") setRepeatMode("one");
    else setRepeatMode("off");
  };

  const handleTrackEnded = () => {
    if (repeatMode === "one") {
      if (audioRef.current) {
        audioRef.current.currentTime = 0;
        audioRef.current.play();
      }
    } else {
      handleNext();
    }
  };

  // Top Search Submit
  const handleTopSearchSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!topSearchText.trim()) return;

    const ytMatch = topSearchText.match(
      /(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?\/\s]{11})/i
    );
    if (ytMatch && ytMatch[1]) {
      const videoId = ytMatch[1];
      const directTrack: Track = {
        id: videoId,
        title: "YouTube Video",
        author: "YouTube",
        duration: 0,
        thumbnail: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
      };
      playSearchResult(directTrack);
      setTopSearchText("");
      return;
    }

    setSearchQueryTitle(topSearchText);
    setIsSearchOpen(true);
    setIsSearching(true);
    setSearchResults([]);
    try {
      const res = await fetch(`/api/search?q=${encodeURIComponent(topSearchText)}`);
      const data = await res.json();
      setSearchResults(data.results || []);
    } catch (err) {
      console.error("Search error:", err);
    } finally {
      setIsSearching(false);
    }
  };

  // Paste action
  const handlePasteClick = async () => {
    try {
      const text = await navigator.clipboard.readText();
      if (text) {
        setTopSearchText(text);
        if (text.includes("youtube.com") || text.includes("youtu.be")) {
          const ytMatch = text.match(
            /(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?\/\s]{11})/i
          );
          if (ytMatch && ytMatch[1]) {
            const videoId = ytMatch[1];
            const directTrack: Track = {
              id: videoId,
              title: "YouTube Video",
              author: "YouTube",
              duration: 0,
              thumbnail: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
            };
            playSearchResult(directTrack);
            setTopSearchText("");
            return;
          }
        }
      }
    } catch {
      const pasted = window.prompt("Dán link YouTube vào đây:");
      if (pasted) {
        setTopSearchText(pasted);
      }
    }
  };

  const playSearchResult = (track: Track) => {
    const updated = [track, ...playlist.filter((t) => t.id !== track.id)];
    setPlaylist(updated);
    setCurrentIndex(0);
    setIsSearchOpen(false);
    loadAndPlayTrack(track, true);
    setQueueTracks((prev) => [track, ...prev.filter((t) => t.id !== track.id)]);
  };

  const playTrackDirectly = (track: Track) => {
    const existsIdx = playlist.findIndex((t) => t.id === track.id);
    if (existsIdx >= 0) {
      setCurrentIndex(existsIdx);
      loadAndPlayTrack(playlist[existsIdx], true);
    } else {
      const updated = [track, ...playlist];
      setPlaylist(updated);
      setCurrentIndex(0);
      loadAndPlayTrack(track, true);
    }
    setQueueTracks((prev) => [track, ...prev.filter((t) => t.id !== track.id)]);
  };

  // Filtered Left list based on active tab and query
  const getSidebarItems = () => {
    const q = leftSearchQuery.toLowerCase().trim();

    if (activeLeftTab === "favorites") {
      const items = libraryData.favorites;
      if (items.length > 0) {
        return items
          .filter((t) => !q || t.title.toLowerCase().includes(q) || t.author.toLowerCase().includes(q))
          .map((t) => ({
            id: t.id,
            title: t.title,
            subtitle: `${t.author || "Kahoii Music"} • ${formatTime(t.duration)}`,
            thumb: t.thumbnail || `https://i.ytimg.com/vi/${t.id}/mqdefault.jpg`,
            rawTrack: t,
            rawPlaylist: null as any,
          }));
      }
      return [];
    }

    if (activeLeftTab === "history") {
      const items = libraryData.history;
      if (items.length > 0) {
        return items
          .filter((t) => !q || t.title.toLowerCase().includes(q) || t.author.toLowerCase().includes(q))
          .map((t) => ({
            id: t.id,
            title: t.title,
            subtitle: `${t.author || "Kahoii Music"} • ${formatTime(t.duration)}`,
            thumb: t.thumbnail || `https://i.ytimg.com/vi/${t.id}/mqdefault.jpg`,
            rawTrack: t,
            rawPlaylist: null as any,
          }));
      }
      return [];
    }

    // Tab "library" (Danh sách phát)
    if (libraryData.playlists.length > 0) {
      return libraryData.playlists
        .filter((p) => !q || (p.name && p.name.toLowerCase().includes(q)))
        .map((p, idx) => ({
          id: p.id || `pl-${idx}`,
          title: p.name || "Danh sách phát",
          subtitle: `${p.items ? p.items.length : 0} bài hát`,
          thumb: p.items?.[0] ? `https://i.ytimg.com/vi/${p.items[0].id}/mqdefault.jpg` : DEFAULT_TRACK_ART,
          rawTrack: null as any,
          rawPlaylist: p,
        }));
    }

    return [];
  };

  // Real tracks to display in Discovery Feed (Center Canvas)
  const recentTracksToDisplay = libraryData.history.slice(0, 12);
  const recommendedTracksToDisplay = libraryData.favorites.slice(0, 12);

  return (
    <div className="w-screen h-screen flex flex-col justify-between overflow-hidden text-white select-none relative font-sans">
      <audio
        ref={audioRef}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={() => setDuration(audioRef.current?.duration || currentTrack?.duration || 207)}
        onEnded={handleTrackEnded}
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
      />

      {/* ========================================================================= */}
      {/* 1. TOP BAR (Seamless & Centered matching Figma mockup)                     */}
      {/* ========================================================================= */}
      <header className="h-[54px] w-full px-5 flex items-center justify-between flex-shrink-0 z-30">
        {/* Left spacer for perfect balance */}
        <div className="w-[140px]" />

        {/* Center: Search Pill with Search icon and "Dán" Button */}
        <form
          onSubmit={handleTopSearchSubmit}
          className="flex-1 max-w-[580px] h-[36px] px-4 rounded-full bg-[#390f8d]/60 border border-purple-500/40 hover:border-pink-500/70 focus-within:border-pink-500 flex items-center gap-2.5 shadow-inner transition-all"
        >
          <Search className="w-4 h-4 text-pink-400 flex-shrink-0" />
          <input
            type="text"
            placeholder="Search"
            value={topSearchText}
            onChange={(e) => setTopSearchText(e.target.value)}
            className="flex-1 bg-transparent text-sm text-white placeholder-white/40 focus:outline-none"
          />
          {(topSearchText || isSearchOpen) && (
            <button
              type="button"
              onClick={() => {
                setTopSearchText("");
                setSearchQueryTitle("");
                setIsSearchOpen(false);
              }}
              title="Xóa tìm kiếm"
              className="p-1 text-white/40 hover:text-white transition-colors cursor-pointer"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
          <button
            type="button"
            onClick={handlePasteClick}
            title="Dán từ Clipboard"
            className="px-4 py-0.5 rounded-full bg-[#200645] hover:bg-pink-600 border border-white/20 text-xs font-semibold text-white transition-all cursor-pointer flex items-center gap-1"
          >
            <ClipboardPaste className="w-3 h-3 text-pink-300" />
            <span>Dán</span>
          </button>
        </form>

        {/* Right: User Avatar Pill Button (Real avatar from database) */}
        <div className="w-[140px] flex justify-end">
          {userSession?.isLoggedIn ? (
            <button
              onClick={() => setIsAccountOpen(true)}
              title={`Tài khoản: ${userSession.user?.name || "Kahoii User"}`}
              className="flex items-center gap-2 pl-1 pr-3 py-1 rounded-full bg-[#f1f1f5] hover:bg-white text-black font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 group"
            >
              <img
                src={`/api/user/avatar?t=${avatarKey}`}
                alt="Avatar"
                onError={(e) => {
                  e.currentTarget.src = DEFAULT_TRACK_ART;
                }}
                className="w-6 h-6 rounded-full object-cover border border-pink-500/40 shadow-sm"
              />
              <span className="max-w-[70px] truncate text-black font-semibold">
                {userSession.user?.name || "avatar"}
              </span>
            </button>
          ) : (
            <button
              onClick={() => setIsAccountOpen(true)}
              title="Đăng nhập"
              className="px-3.5 py-1.5 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 hover:opacity-90 text-white font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 flex items-center gap-1.5"
            >
              <User className="w-3.5 h-3.5" />
              <span>Đăng nhập</span>
            </button>
          )}
        </div>
      </header>

      {/* Error Toast */}
      {errorMessage && (
        <div className="absolute top-[56px] left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-xl bg-red-950/90 border border-red-500/50 text-red-200 text-xs flex items-center gap-2 shadow-xl backdrop-blur-md">
          <span>{errorMessage}</span>
          <button onClick={() => setErrorMessage(null)} className="text-red-400 hover:text-white cursor-pointer">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 2. MAIN CANVAS (Exact Figma 3-Column Layout with Real Proportions)         */}
      {/* ========================================================================= */}
      <main className="flex-1 w-full px-5 pb-5 pt-1 flex gap-4 overflow-hidden min-h-0">
        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 1 (LEFT): Sidebar / Library Panel (~19% Width)                   */}
        {/* ----------------------------------------------------------------------- */}
        <aside
          className="w-[290px] xl:w-[320px] flex-shrink-0 figma-panel flex flex-col min-h-0 overflow-hidden"
          style={{ padding: "30px 18px 22px 18px" }}
        >
          {/* Top 3 Tabs Row: Silky Smooth Sliding Indicator Animation */}
          <div className="relative grid grid-cols-3 gap-2.5" style={{ marginBottom: "20px" }}>
            {/* Static Capsule Backgrounds */}
            <div className="absolute inset-0 grid grid-cols-3 gap-2.5 pointer-events-none">
              <div className="rounded-xl bg-[#250746] border border-white/10" />
              <div className="rounded-xl bg-[#250746] border border-white/10" />
              <div className="rounded-xl bg-[#250746] border border-white/10" />
            </div>

            {/* Active Sliding Glowing Pill */}
            <div
              className="absolute top-0 bottom-0 rounded-xl pointer-events-none z-10"
              style={{
                width: "calc((100% - 20px) / 3)",
                left: 0,
                transform: `translateX(calc(${activeTabIndex} * (100% + 10px)))`,
                transition: "transform 0.32s cubic-bezier(0.22, 1, 0.36, 1), background 0.8s ease, box-shadow 0.8s ease",
                background: "var(--theme-btn-gradient)",
                boxShadow: "0 4px 18px var(--theme-panel-border)",
              }}
            />

            {/* Clickable Buttons */}
            {SIDEBAR_TABS.map((tab) => (
              <button
                key={tab.key}
                onClick={() => setActiveLeftTab(tab.key)}
                className={`relative z-20 py-2 px-1 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer truncate ${
                  activeLeftTab === tab.key ? "text-white" : "text-white/70 hover:text-white"
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Search Box in Sidebar with Sort Icon (Matching Figma: Centered text, pink icons) */}
          <div
            className="relative flex items-center h-10 px-3.5 rounded-full bg-[#1e053a] border border-purple-500/30 hover:border-pink-500/50 focus-within:border-pink-500 transition-all flex-shrink-0"
            style={{ marginBottom: "22px" }}
          >
            <Search className="w-4 h-4 text-[#e0007b] flex-shrink-0" />
            <input
              type="text"
              placeholder="Search"
              value={leftSearchQuery}
              onChange={(e) => setLeftSearchQuery(e.target.value)}
              className="flex-1 bg-transparent text-xs text-center font-medium text-white placeholder-white/50 focus:outline-none px-2"
            />
            <button
              type="button"
              title="Sắp xếp"
              className="text-[#e0007b] hover:text-white transition-colors cursor-pointer flex-shrink-0"
            >
              <ArrowDownAZ className="w-4 h-4" />
            </button>
          </div>

          {/* Vertical List of Playlist Items (Matching Figma) */}
          <div
            className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1"
            style={{ gap: "12px" }}
          >
            {getSidebarItems().length > 0 ? (
              getSidebarItems().map((item, idx) => (
                <div
                  key={item.id + idx}
                  onClick={() => {
                    if (item.rawTrack) {
                      playTrackDirectly(item.rawTrack);
                    } else if (item.rawPlaylist?.items?.length) {
                      const plTracks: Track[] = item.rawPlaylist.items.map((it: any) => ({
                        id: it.id,
                        title: it.title,
                        author: it.author || "",
                        duration: it.duration || 0,
                        thumbnail: `https://i.ytimg.com/vi/${it.id}/mqdefault.jpg`,
                      }));
                      setPlaylist(plTracks);
                      setCurrentIndex(0);
                      loadAndPlayTrack(plTracks[0], true);
                      setQueueTracks(plTracks);
                    } else if (playlist[idx % playlist.length]) {
                      playTrackDirectly(playlist[idx % playlist.length]);
                    }
                  }}
                  className="flex items-center gap-3.5 p-2 rounded-2xl hover:bg-white/10 hover:translate-x-1 transition-all duration-200 cursor-pointer group"
                  style={{ marginBottom: "2px" }}
                >
                  <img
                    src={item.thumb}
                    alt=""
                    onError={(e) => {
                      e.currentTarget.src = DEFAULT_TRACK_ART;
                    }}
                    className="w-11 h-11 rounded-[14px] object-cover flex-shrink-0 border border-white/10 shadow-sm"
                  />
                  <div className="flex-1 min-w-0">
                    <p className="text-[13.5px] font-semibold text-white group-hover:text-pink-300 truncate transition-colors">
                      {item.title}
                    </p>
                    <p className="text-[11.5px] text-purple-200/60 truncate mt-1">
                      {item.subtitle}
                    </p>
                  </div>
                </div>
              ))
            ) : (
              <div className="flex flex-col items-center justify-center py-16 text-center text-white/40 text-xs px-2">
                <p>
                  {activeLeftTab === "library"
                    ? "Chưa có danh sách phát nào"
                    : activeLeftTab === "favorites"
                    ? "Chưa có bài hát yêu thích nào"
                    : "Chưa có lịch sử phát nhạc"}
                </p>
              </div>
            )}
          </div>
        </aside>

        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 2 (CENTER): Main Discovery Feed / Search View (~58% Width)        */}
        {/* ----------------------------------------------------------------------- */}
        <section
          className="flex-1 h-full figma-panel flex flex-col justify-between min-h-0 overflow-y-auto"
          style={{ padding: "26px 28px" }}
        >
          {isSearchOpen ? (
            /* ===================================================================== */
            /* SEARCH VIEW (Dynamically transforms Center Canvas when searching)     */
            /* ===================================================================== */
            <div className="flex flex-col h-full min-h-0">
              {/* Header with Back button and search title */}
              <div className="flex items-center justify-between pb-3.5 border-b border-white/10 mb-5 flex-shrink-0">
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => {
                      setIsSearchOpen(false);
                      setTopSearchText("");
                      setSearchQueryTitle("");
                    }}
                    className="px-3 py-1.5 rounded-xl bg-white/5 hover:bg-pink-500/20 text-white/80 hover:text-white border border-white/10 transition-all cursor-pointer flex items-center gap-2 text-xs font-semibold group"
                  >
                    <ArrowLeft className="w-4 h-4 text-pink-400 group-hover:-translate-x-0.5 transition-transform" />
                    <span>Quay lại Khám phá</span>
                  </button>
                  <h2 className="text-[20px] font-bold text-white tracking-wide truncate">
                    {searchQueryTitle ? `Kết quả: "${searchQueryTitle}"` : "Kết quả tìm kiếm trên YouTube"}
                  </h2>
                </div>
                {searchResults.length > 0 && (
                  <span className="text-xs text-purple-200/60 font-mono">
                    {searchResults.length} bài hát
                  </span>
                )}
              </div>

              {/* Body: Loading / Results Grid / Empty state */}
              {isSearching ? (
                <div className="flex-1 flex flex-col items-center justify-center py-24 text-center gap-3">
                  <Loader2 className="w-8 h-8 animate-spin text-pink-400" />
                  <p className="text-sm font-medium text-purple-200/80">Đang tìm kiếm bài hát trên YouTube...</p>
                </div>
              ) : searchResults.length > 0 ? (
                <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
                  {searchResults.map((item) => {
                    const isCurrentPlaying = currentTrack?.id === item.id;
                    return (
                      <div
                        key={item.id}
                        onClick={() => playSearchResult(item)}
                        className="w-full flex flex-col cursor-pointer group"
                      >
                        <div className={`w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${
                          isCurrentPlaying
                            ? "border-pink-500 shadow-pink-500/30"
                            : "border-white/5 bg-purple-900/30"
                        }`}>
                          <img
                            src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                            alt={item.title}
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                          {/* Play overlay on hover or active playing */}
                          <div className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity duration-200 ${
                            isCurrentPlaying ? "opacity-100" : "opacity-0 group-hover:opacity-100"
                          }`}>
                            <div className="w-10 h-10 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg">
                              {isCurrentPlaying && isPlaying ? (
                                <Pause className="w-5 h-5 text-white fill-current" />
                              ) : (
                                <Play className="w-5 h-5 text-white fill-current translate-x-0.5" />
                              )}
                            </div>
                          </div>
                          {item.duration > 0 && (
                            <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                              {formatTime(item.duration)}
                            </span>
                          )}
                        </div>
                        <p className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${
                          isCurrentPlaying ? "text-pink-300 font-bold" : "text-white/90 group-hover:text-pink-300"
                        }`}>
                          {item.title}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                          {item.author || "YouTube"}
                        </p>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="flex-1 flex flex-col items-center justify-center py-20 text-center gap-3">
                  <p className="text-sm text-white/50 italic">Không tìm thấy bài hát nào phù hợp với từ khóa.</p>
                  <button
                    type="button"
                    onClick={() => {
                      setIsSearchOpen(false);
                      setTopSearchText("");
                      setSearchQueryTitle("");
                    }}
                    className="px-4 py-2 rounded-xl bg-white/10 hover:bg-pink-600 text-xs font-semibold text-white transition-all cursor-pointer"
                  >
                    Quay lại Khám phá
                  </button>
                </div>
              )}
            </div>
          ) : (
            /* ===================================================================== */
            /* DISCOVERY VIEW (Gần đây, Có thể bạn sẽ thích, Nghệ sĩ)                */
            /* ===================================================================== */
            <div className="flex flex-col justify-between h-full min-h-0">
              {/* Section 1: "Gần đây" (Full Width 6-Column Grid - Real History) */}
              <div style={{ marginBottom: "22px" }}>
                <h2 className="text-[22px] font-bold text-white tracking-wide" style={{ marginBottom: "14px" }}>
                  Gần đây
                </h2>
                {recentTracksToDisplay.length > 0 ? (
                  <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
                    {recentTracksToDisplay.map((item) => (
                      <div
                        key={item.id}
                        onClick={() => playTrackDirectly(item)}
                        className="w-full flex flex-col cursor-pointer group"
                      >
                        <div className="w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                          <img
                            src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                            alt={item.title}
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-1.5 transition-colors">
                          {item.title}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                          {item.author || "Kahoii Music"}
                        </p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="py-8 text-center text-white/40 text-xs italic">
                    Chưa có bài hát nào gần đây. Hãy chọn hoặc tìm kiếm bài hát để phát.
                  </div>
                )}
              </div>

              {/* Section 2: "Có thể bạn sẽ thích" (Full Width 6-Column Grid - Real Favorites) */}
              <div style={{ marginBottom: "22px" }}>
                <h2 className="text-[22px] font-bold text-white tracking-wide" style={{ marginBottom: "14px" }}>
                  Có thể bạn sẽ thích
                </h2>
                {recommendedTracksToDisplay.length > 0 ? (
                  <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
                    {recommendedTracksToDisplay.map((item) => (
                      <div
                        key={item.id}
                        onClick={() => playTrackDirectly(item)}
                        className="w-full flex flex-col cursor-pointer group"
                      >
                        <div className="w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                          <img
                            src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                            alt={item.title}
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-1.5 transition-colors">
                          {item.title}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                          {item.author || "Kahoii Music"}
                        </p>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="py-8 text-center text-white/40 text-xs italic">
                    Chưa có bài hát yêu thích nào.
                  </div>
                )}
              </div>

              {/* Section 3: "Nghệ sĩ" (Full Width 6-Column Circular Grid - World-famous Artists) */}
              <div>
                <h2 className="text-[22px] font-bold text-white tracking-wide" style={{ marginBottom: "14px" }}>
                  Nghệ sĩ
                </h2>
                <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
                  {WORLD_FAMOUS_ARTISTS.map((artist, idx) => (
                    <div
                      key={idx}
                      onClick={() => searchArtistSongs(artist.query)}
                      className="w-full flex flex-col items-center cursor-pointer group"
                    >
                      <div className="w-full aspect-square rounded-full overflow-hidden bg-purple-900/30 shadow-md border-2 border-white/10 group-hover:border-pink-500 group-hover:scale-[1.03] transition-all duration-200">
                        <img
                          src={artist.avatar}
                          alt={artist.name}
                          onError={(e) => {
                            e.currentTarget.src = DEFAULT_TRACK_ART;
                          }}
                          className="w-full h-full object-cover"
                        />
                      </div>
                      <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-1.5 text-center w-full transition-colors">
                        {artist.name}
                      </p>
                      <p className="text-[10px] text-purple-200/60 truncate text-center w-full">
                        {artist.genre}
                      </p>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
        </section>

        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 3 (RIGHT): Now Playing & Queue Player (~23% Width)               */}
        {/* ----------------------------------------------------------------------- */}
        <aside
          className="w-[310px] xl:w-[340px] flex-shrink-0 figma-panel flex flex-col min-h-0 overflow-hidden"
          style={{ padding: "26px 18px 20px 18px" }}
        >
          {/* Top Part: Now Playing Artwork Card with Dynamic Artwork & Glow */}
          <div
            className="w-full aspect-square rounded-[22px] overflow-hidden relative shadow-2xl bg-[#1b0833] flex-shrink-0 group"
            style={{ marginBottom: "18px" }}
          >
            {/* Real Dynamic Artwork with HQ Thumbnail & Fallback */}
            <img
              src={
                currentTrack?.thumbnail && !currentTrack.thumbnail.includes("figma")
                  ? (currentTrack.thumbnail.includes("i.ytimg.com")
                      ? currentTrack.thumbnail.replace("mqdefault.jpg", "hqdefault.jpg")
                      : currentTrack.thumbnail)
                  : (currentTrack?.id ? `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg` : DEFAULT_TRACK_ART)
              }
              alt={currentTrack?.title || "Track Artwork"}
              onError={(e) => {
                e.currentTarget.src = DEFAULT_TRACK_ART;
              }}
              className="w-full h-full object-cover transition-all duration-700 group-hover:scale-105"
            />

            {/* Dark gradient fade over lower half to mask controls & provide high contrast */}
            <div className="absolute inset-x-0 bottom-0 h-[52%] bg-gradient-to-t from-black/95 via-black/60 to-transparent pointer-events-none z-10" />

            {/* Track Title and Artist floating gently over the gradient */}
            <div className="absolute bottom-[92px] left-4 right-4 z-20 pointer-events-none">
              <p className="text-[13px] font-bold text-white truncate drop-shadow-md">
                {currentTrack?.title || "Kahoii Music"}
              </p>
              <p className="text-[11px] text-white/70 truncate drop-shadow">
                {currentTrack?.author || "Đang phát"}
              </p>
            </div>

            {/* Top-left Menu & Volume Controls */}
            <div className="absolute top-3 left-3 flex items-center gap-1.5 z-20">
              <button
                type="button"
                className="w-7 h-7 rounded-lg bg-black/40 backdrop-blur-md flex items-center justify-center text-white/80 hover:text-white hover:bg-black/60 transition-all cursor-pointer"
              >
                <Menu className="w-4 h-4" />
              </button>
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setShowVolumeSlider(!showVolumeSlider)}
                  className="w-7 h-7 rounded-lg bg-black/40 backdrop-blur-md flex items-center justify-center text-pink-400 hover:text-white hover:bg-black/60 transition-all cursor-pointer"
                  title="Âm lượng"
                >
                  {isMuted || volume === 0 ? <VolumeX className="w-4 h-4" /> : <Volume2 className="w-4 h-4" />}
                </button>
                {showVolumeSlider && (
                  <div className="absolute top-9 left-0 p-2.5 rounded-xl bg-black/95 border border-pink-500/50 backdrop-blur-xl shadow-2xl flex items-center gap-2 z-30">
                    <input
                      type="range"
                      min={0}
                      max={1}
                      step={0.01}
                      value={isMuted ? 0 : volume}
                      onChange={(e) => {
                        setIsMuted(false);
                        setVolume(parseFloat(e.target.value));
                      }}
                      className="w-20 h-1 bg-white/30 rounded appearance-none cursor-pointer accent-pink-500"
                    />
                    <span className="text-[10px] text-pink-400 font-mono font-bold">
                      {Math.round((isMuted ? 0 : volume) * 100)}%
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Top-right Bright Red Heart Icon (Matching Figma) */}
            <div className="absolute top-3 right-3 w-8 h-8 rounded-lg bg-black/40 backdrop-blur-md flex items-center justify-center z-20 cursor-pointer group">
              <Heart className="w-5 h-5 fill-red-500 text-red-500 drop-shadow-[0_0_10px_rgba(239,68,68,0.9)] group-hover:scale-115 transition-transform" />
            </div>

            {/* Elevated Seekbar Progress Line with Scrubber Handle & Time (at bottom-[66px]) */}
            <div className="absolute bottom-[66px] left-4 right-4 flex flex-col gap-1 z-20">
              <div className="relative w-full flex items-center group/seek">
                <div className="absolute left-0 right-0 h-1 rounded-full bg-white/20" />
                <div
                  className="absolute left-0 h-1 rounded-full"
                  style={{
                    width: `${duration ? Math.min(100, (currentTime / duration) * 100) : 0}%`,
                    background: "var(--theme-btn-gradient)",
                  }}
                />
                <input
                  type="range"
                  min={0}
                  max={duration || 100}
                  value={currentTime}
                  onChange={handleSeek}
                  className="w-full h-2.5 opacity-0 cursor-pointer z-10"
                />
                <div
                  className="absolute w-3 h-3 rounded-full bg-white shadow-md pointer-events-none transition-transform group-hover/seek:scale-125"
                  style={{
                    left: `calc(${duration ? Math.min(100, (currentTime / duration) * 100) : 0}% - 6px)`,
                    boxShadow: "0 0 10px var(--theme-accent)",
                  }}
                />
              </div>
              <div className="flex justify-between text-[10px] text-white/60 font-mono px-0.5">
                <span>{formatTime(currentTime)}</span>
                <span>{formatTime(duration)}</span>
              </div>
            </div>

            {/* 5 Playback Control Buttons Row - Enlarged & Beautifully Spaced */}
            <div className="absolute bottom-2.5 left-3 right-3 flex items-center justify-between px-1 z-20">
              {/* Loop */}
              <button
                type="button"
                onClick={cycleRepeatMode}
                title={`Lặp: ${repeatMode === "off" ? "Tắt" : repeatMode === "one" ? "1 bài" : "Tất cả"}`}
                className={`p-1.5 transition-all cursor-pointer hover:scale-110 active:scale-90 ${
                  repeatMode !== "off" ? "text-pink-400" : "text-white/60 hover:text-white"
                }`}
              >
                {repeatMode === "one" ? <Repeat1 className="w-5 h-5" /> : <Repeat className="w-5 h-5" />}
              </button>

              {/* Prev */}
              <button
                type="button"
                onClick={handlePrev}
                title="Bài trước"
                className="p-1.5 text-pink-500 hover:text-pink-400 transition-all cursor-pointer hover:scale-115 active:scale-90"
              >
                <SkipBack className="w-6 h-6 fill-current" />
              </button>

              {/* Large Dynamic Play / Pause Circle (w-12 h-12) */}
              <button
                type="button"
                onClick={togglePlayPause}
                disabled={isLoading}
                title={isPlaying ? "Tạm dừng" : "Phát"}
                className="w-12 h-12 rounded-full flex items-center justify-center text-white shadow-xl hover:scale-110 active:scale-95 transition-all cursor-pointer"
                style={{
                  background: "var(--theme-btn-gradient)",
                  boxShadow: "0 4px 18px var(--theme-panel-border)",
                }}
              >
                {isLoading ? (
                  <Loader2 className="w-6 h-6 animate-spin text-white" />
                ) : isPlaying ? (
                  <Pause className="w-6 h-6 fill-current" />
                ) : (
                  <Play className="w-6 h-6 fill-current translate-x-0.5" />
                )}
              </button>

              {/* Next */}
              <button
                type="button"
                onClick={handleNext}
                title="Bài kế tiếp"
                className="p-1.5 text-pink-500 hover:text-pink-400 transition-all cursor-pointer hover:scale-115 active:scale-90"
              >
                <SkipForward className="w-6 h-6 fill-current" />
              </button>

              {/* Timer */}
              <button
                type="button"
                onClick={() => {}}
                title="Hẹn giờ"
                className="p-1.5 text-pink-400 hover:text-white transition-all cursor-pointer hover:scale-110 active:scale-90"
              >
                <Clock className="w-5 h-5" />
              </button>
            </div>
          </div>

          {/* Bottom Part: Queue List (Starting directly with Active Highlight Beam) */}
          <div
            className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1"
            style={{ gap: "8px" }}
          >
            {queueTracks.length > 0 ? (
              queueTracks.map((track, i) => {
                const isCurrent = currentTrack ? currentTrack.id === track.id : i === currentIndex;
                return (
                  <div
                    key={track.id + i}
                    onClick={() => {
                      const plIdx = playlist.findIndex((t) => t.id === track.id);
                      if (plIdx >= 0) {
                        setCurrentIndex(plIdx);
                        loadAndPlayTrack(playlist[plIdx], true);
                      } else {
                        playTrackDirectly(track);
                      }
                    }}
                    className={`flex items-center gap-2.5 px-2.5 py-1.5 transition-all cursor-pointer group ${
                      isCurrent ? "active-queue-beam shadow-md" : "hover:bg-white/5 rounded-xl"
                    }`}
                  >
                    <img
                      src={track.thumbnail || `https://i.ytimg.com/vi/${track.id}/mqdefault.jpg`}
                      alt=""
                      onError={(e) => {
                        e.currentTarget.src = DEFAULT_TRACK_ART;
                      }}
                      className="w-8 h-8 rounded-md object-cover flex-shrink-0"
                    />
                    <div className="flex-1 min-w-0">
                      <p className="text-xs font-semibold text-white truncate group-hover:text-pink-300 transition-colors">
                        {track.title}
                      </p>
                      {track.author && (
                        <p className="text-[10px] text-white/50 truncate">
                          {track.author}
                        </p>
                      )}
                    </div>
                    <span className="text-[10px] text-white/60 font-mono pr-1 flex-shrink-0">
                      {track.duration && track.duration > 0 ? formatTime(track.duration) : "--:--"}
                    </span>
                  </div>
                );
              })
            ) : (
              <div className="flex flex-col items-center justify-center py-12 text-center text-white/40 text-xs">
                <p>Hàng đợi đang trống</p>
              </div>
            )}
          </div>
        </aside>
      </main>

      {/* ========================================================================= */}
      {/* 3. USER PROFILE MODAL (Clicked from "avatar" button)                      */}
      {/* ========================================================================= */}
      {isAccountOpen && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="figma-panel w-full max-w-[420px] flex flex-col p-5 relative shadow-2xl border border-pink-500/50">
            <div className="flex items-center justify-between pb-3 border-b border-white/10">
              <span className="font-extrabold text-sm text-pink-400 flex items-center gap-2">
                <User className="w-4 h-4" />
                Hồ sơ người dùng
              </span>
              <button
                onClick={() => {
                  setIsAccountOpen(false);
                  setLoginError(null);
                  setSyncSuccessMsg(null);
                }}
                className="p-1 rounded-lg text-white/60 hover:text-white hover:bg-white/10 transition-all cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {userSession?.isLoggedIn ? (
              <div className="flex flex-col gap-4 mt-4">
                {/* User Info Header Card */}
                <div className="flex items-center gap-3.5 p-3 rounded-2xl bg-white/5 border border-pink-500/30">
                  <div className="relative flex-shrink-0">
                    <img
                      src={`/api/user/avatar?t=${avatarKey}`}
                      alt="Avatar"
                      onError={(e) => {
                        e.currentTarget.src = DEFAULT_TRACK_ART;
                      }}
                      className="w-14 h-14 rounded-full object-cover border-2 border-pink-400 shadow-lg shadow-pink-500/30"
                    />
                    <div
                      className="absolute bottom-0 right-0 w-3.5 h-3.5 rounded-full bg-emerald-500 border-2 border-[#1e053a]"
                      title="Đang hoạt động"
                    />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5">
                      <h3 className="font-bold text-base text-white truncate">
                        {userSession.user?.name || "Kahoii User"}
                      </h3>
                      <ShieldCheck className="w-4 h-4 text-pink-400 flex-shrink-0" />
                    </div>
                    <p className="text-xs text-purple-200/70 truncate mt-0.5">
                      {userSession.user?.email || "danlanga26@gmail.com"}
                    </p>
                    <span className="inline-block mt-1 text-[10px] font-semibold px-2 py-0.5 rounded-full bg-pink-500/20 text-pink-300 border border-pink-500/30">
                      Kahoii Member • Cloud Synced
                    </span>
                  </div>
                </div>

                {/* User Stats Grid (Real Database Counts) */}
                <div className="grid grid-cols-3 gap-2 text-center">
                  <div className="p-2.5 rounded-xl bg-purple-950/40 border border-purple-500/20">
                    <p className="text-lg font-bold text-pink-400">
                      {userSession.user?.stats?.favoritesCount ?? libraryData.favorites.length}
                    </p>
                    <p className="text-[11px] text-white/70 mt-0.5">Yêu thích</p>
                  </div>
                  <div className="p-2.5 rounded-xl bg-purple-950/40 border border-purple-500/20">
                    <p className="text-lg font-bold text-pink-400">
                      {userSession.user?.stats?.historyCount ?? libraryData.history.length}
                    </p>
                    <p className="text-[11px] text-white/70 mt-0.5">Lịch sử</p>
                  </div>
                  <div className="p-2.5 rounded-xl bg-purple-950/40 border border-purple-500/20">
                    <p className="text-lg font-bold text-pink-400">
                      {userSession.user?.stats?.playlistsCount ?? libraryData.playlists.length}
                    </p>
                    <p className="text-[11px] text-white/70 mt-0.5">Playlists</p>
                  </div>
                </div>

                {/* Success Banner */}
                {syncSuccessMsg && (
                  <div className="p-2.5 rounded-xl bg-emerald-950/80 border border-emerald-500/50 text-emerald-200 text-xs flex items-center gap-2">
                    <Check className="w-4 h-4 text-emerald-400 flex-shrink-0" />
                    <span>{syncSuccessMsg}</span>
                  </div>
                )}

                {/* Action Buttons */}
                <div className="flex flex-col gap-2 pt-1">
                  <button
                    onClick={handleSyncCloud}
                    disabled={isSyncing}
                    className="w-full py-2.5 rounded-xl bg-gradient-to-r from-pink-500 to-purple-600 hover:opacity-95 text-white font-bold text-xs shadow-lg shadow-pink-500/30 transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                  >
                    {isSyncing ? (
                      <>
                        <Loader2 className="w-4 h-4 animate-spin text-white" />
                        <span>Đang đồng bộ Cloud...</span>
                      </>
                    ) : (
                      <>
                        <RefreshCw className="w-3.5 h-3.5" />
                        <span>Đồng bộ từ Cloud Firestore</span>
                      </>
                    )}
                  </button>

                  <button
                    onClick={handleLogout}
                    className="w-full py-2 rounded-xl bg-white/5 hover:bg-red-500/20 text-white/80 hover:text-red-300 font-semibold text-xs border border-white/10 hover:border-red-500/30 transition-all flex items-center justify-center gap-2 cursor-pointer"
                  >
                    <LogOut className="w-3.5 h-3.5" />
                    <span>Đăng xuất</span>
                  </button>
                </div>
              </div>
            ) : (
              /* Login Form for Non-Logged-in State */
              <form onSubmit={handleLogin} className="flex flex-col gap-3.5 mt-4">
                <p className="text-xs text-white/70">
                  Đăng nhập tài khoản Kahoii Music để đồng bộ danh sách phát và bài hát yêu thích.
                </p>

                {loginError && (
                  <div className="p-2.5 rounded-xl bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 text-red-400 flex-shrink-0" />
                    <span>{loginError}</span>
                  </div>
                )}

                <div className="flex flex-col gap-1.5">
                  <label className="text-[11px] font-semibold text-white/80">Email</label>
                  <input
                    type="email"
                    placeholder="nhap-email@gmail.com"
                    value={loginEmail}
                    onChange={(e) => setLoginEmail(e.target.value)}
                    className="w-full px-3.5 py-2 rounded-xl bg-black/40 border border-purple-500/30 focus:border-pink-500 text-xs text-white placeholder-white/30 focus:outline-none"
                  />
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-[11px] font-semibold text-white/80">Mật khẩu</label>
                  <input
                    type="password"
                    placeholder="••••••••"
                    value={loginPassword}
                    onChange={(e) => setLoginPassword(e.target.value)}
                    className="w-full px-3.5 py-2 rounded-xl bg-black/40 border border-purple-500/30 focus:border-pink-500 text-xs text-white placeholder-white/30 focus:outline-none"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isLoggingIn}
                  className="w-full mt-2 py-2.5 rounded-xl bg-gradient-to-r from-pink-500 to-purple-600 hover:opacity-95 text-white font-bold text-xs shadow-lg shadow-pink-500/30 transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                >
                  {isLoggingIn ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin text-white" />
                      <span>Đang xác thực...</span>
                    </>
                  ) : (
                    <>
                      <LogIn className="w-4 h-4" />
                      <span>Đăng nhập</span>
                    </>
                  )}
                </button>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
