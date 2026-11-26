import React, { useState, useEffect, useRef } from "react";
import {
  Play,
  Pause,
  SkipBack,
  SkipForward,
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
  Sparkles,
  Flame,
  Mic2,
  Compass,
  Film,
  Shuffle,
  ListPlus,
  ListEnd,
  Plus,
  Trash2,
  FolderPlus,
  Download,
  Music,
  Laptop,
  Smartphone,
  Tablet,
  Puzzle,
  ExternalLink,
} from "lucide-react";
import type {
  Track,
  StructuredSearchResult,
  SearchPlaylistItem,
  VideoQualityOption,
} from "./types";
import { VideoPlayerStage } from "./components/VideoPlayerStage";
import { ConfirmDialog } from "./components/ConfirmDialog";
import { formatTime } from "./utils/lyricsParser";
import { loadAndExtractTheme, type ThemePalette, DEFAULT_THEME } from "./utils/colorExtractor";

// Clean, robust SVG placeholder used only when a song has no thumbnail
const DEFAULT_TRACK_ART =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect width='100' height='100' rx='16' fill='%23240a50'/%3E%3Ccircle cx='50' cy='50' r='30' fill='%23381078'/%3E%3Cpath d='M44 36v20.5a6.5 6.5 0 1 1-3-5.5V40l18-4v16.5a6.5 6.5 0 1 1-3-5.5V32z' fill='%23e6007e'/%3E%3C/svg%3E";

const handleThumbError = (e: React.SyntheticEvent<HTMLImageElement>, videoId?: string) => {
  const img = e.currentTarget;
  if (videoId && !img.dataset.triedHq) {
    img.dataset.triedHq = "1";
    img.src = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
    return;
  }
  if (videoId && !img.dataset.triedMq) {
    img.dataset.triedMq = "1";
    img.src = `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`;
    return;
  }
  img.src = DEFAULT_TRACK_ART;
};

// Base API URL for flexible deployment (GitHub Pages + Remote Backend or Monolithic Express)
const API_BASE = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/$/, "");
const KAHOII_URL =
  import.meta.env.VITE_KAHOII_URL ||
  (typeof window !== "undefined" && (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1")
    ? "http://localhost:3000"
    : "https://kahoiimusic.vercel.app");

const NON_MUSIC_PATTERN =
  /tập\s*\d+|episode\s*\d+|\bep\.?\s*\d+|\bfull\s*bộ|\bhoạt\s*hình|\banime\b|\bdoraemon\b|\bconan\b|\bdragon\s*ball|\bnaruto|\bone\s*piece|\bphim\b|\bmovie\b|\btrailer\b|\breview\b|\bvlog\b|\bgaming\b|\bgameplay\b|\bminecraft|\bliên\s*quân|\bpodcast|\btin\s*tức|\bnews\b|\bthời\s*sự|\bhài\s*kịch|\btiểu\s*phẩm|\bthuyết\s*minh|\bvietsub\s*tập/i;

const MUSIC_PATTERN =
  /\b(official\s*(mv|music\s*video|audio|lyric)|mv|lyrics?|lời\s*bài\s*hát|acoustic|remix|lofi|cover|mashup|karaoke|instrumental|beat|ost|nhạc|ca\s*khúc|bài\s*hát|album|single|topic|vevo|records|music|prod\.|liên\s*khúc)\b/i;

const isTrackMusic = (track: Track | null): boolean => {
  if (!track) return false;
  if (typeof track.isMusic === "boolean") return track.isMusic;
  const text = `${track.title || ""} ${track.author || ""}`;
  if (NON_MUSIC_PATTERN.test(text)) return false;
  if (MUSIC_PATTERN.test(text) || /-\s*topic$/i.test(track.author || "")) return true;
  if (track.duration && track.duration > 900) return false;
  return true;
};

// Nhạc Sĩ Nổi Bật (Featured Artists - High-res local avatars)
const FEATURED_ARTISTS = [
  {
    name: "Sơn Tùng M-TP",
    genre: "Pop / R&B",
    avatar: "./artists/son_tung.jpg",
    query: "Sơn Tùng M-TP",
  },
  {
    name: "Vũ.",
    genre: "Indie / Acoustic",
    avatar: "./artists/vu.jpg",
    query: "Vũ",
  },
  {
    name: "Đen",
    genre: "Rap / Hip-Hop",
    avatar: "./artists/den.jpg",
    query: "Đen Vâu",
  },
  {
    name: "Soobin Hoàng Sơn",
    genre: "R&B / Ballad",
    avatar: "./artists/soobin.jpg",
    query: "Soobin Hoàng Sơn",
  },
  {
    name: "Taylor Swift",
    genre: "Pop Superstar",
    avatar: "./artists/taylor_swift.jpg",
    query: "Taylor Swift",
  },
  {
    name: "The Weeknd",
    genre: "R&B / Pop",
    avatar: "./artists/the_weeknd.jpg",
    query: "The Weeknd",
  },
];

type LeftTab = "library" | "favorites" | "history";

const SIDEBAR_TABS: { key: LeftTab; label: string }[] = [
  { key: "library", label: "Thư viện" },
  { key: "favorites", label: "Yêu thích" },
  { key: "history", label: "Lịch sử" },
];

export default function App() {
  const [playlist, setPlaylist] = useState<Track[]>(() => {
    try {
      const saved = localStorage.getItem("thuongtube_playlist");
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });
  const [queueTracks, setQueueTracks] = useState<Track[]>(() => {
    try {
      const saved = localStorage.getItem("thuongtube_queue");
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });
  const [currentIndex, setCurrentIndex] = useState<number>(() => {
    try {
      const saved = localStorage.getItem("thuongtube_current_index");
      return saved ? parseInt(saved, 10) || 0 : 0;
    } catch {
      return 0;
    }
  });
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

  // Home Discovery Feed (Trending & New Releases loaded from backend)
  const [trendingTracks, setTrendingTracks] = useState<Track[]>([]);
  const [newReleaseTracks, setNewReleaseTracks] = useState<Track[]>([]);

  // Dual/Video-Engine Playback (Video + Synced Audio + YouTube Iframe Fallback)
  const ytPlayerRef = useRef<any>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const activePlayerType = useRef<"video" | "audio" | "youtube">("video");
  const isAdaptiveSyncedRef = useRef<boolean>(false);
  const lastSyncTimeRef = useRef<number>(0);
  const seekTokenRef = useRef<number>(0);
  const [trackMusicMap, setTrackMusicMap] = useState<Record<string, boolean>>({});
  const pendingYtTrackRef = useRef<{
    videoId: string;
    startSeconds: number;
    autoPlay: boolean;
  } | null>(null);
  const [playerMode, setPlayerMode] = useState<"video" | "audio" | "youtube">("video");
  const [availableQualities, setAvailableQualities] = useState<VideoQualityOption[]>([]);
  const [selectedQuality, setSelectedQuality] = useState<string>("720p");
  const [playbackRate, setPlaybackRate] = useState<number>(1);
  const [showDiscovery, setShowDiscovery] = useState<boolean>(false);
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState<boolean>(false);
  const [lyrics, setLyrics] = useState<any>(null);
  const [randomTracks, setRandomTracks] = useState<Track[]>([]);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [playlistTargetTrack, setPlaylistTargetTrack] = useState<Track | null>(null);
  const [selectedPlaylistId, setSelectedPlaylistId] = useState<string | null>(null);
  const [isCreatePlaylistOpen, setIsCreatePlaylistOpen] = useState<boolean>(false);
  const [newPlaylistName, setNewPlaylistName] = useState<string>("");
  const [confirmDialog, setConfirmDialog] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
    variant?: "danger" | "primary";
    showCancel?: boolean;
    onConfirm: () => void;
  } | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage((prev) => (prev === msg ? null : prev));
    }, 2500);
  };

  const handleAddToQueue = (track: Track, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setQueueTracks((prev) => {
      if (prev.some((t) => t.id === track.id)) return prev;
      return [...prev, track];
    });
    setPlaylist((prev) => {
      if (prev.some((t) => t.id === track.id)) return prev;
      return [...prev, track];
    });
    showToast(`Đã thêm "${track.title}" vào Hàng đợi`);
  };

  const handleRemoveFromQueue = (index: number, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    const removed = queueTracks[index];
    setQueueTracks((prev) => prev.filter((_, i) => i !== index));
    if (removed) {
      setPlaylist((prev) => prev.filter((t) => t.id !== removed.id));
    }
  };

  const handleShuffleQueue = () => {
    if (queueTracks.length <= 1) return;
    const shuffled = shuffleList(queueTracks);
    setQueueTracks(shuffled);
    setPlaylist(shuffled);
    if (currentTrack) {
      const idx = shuffled.findIndex((t) => t.id === currentTrack.id);
      if (idx >= 0) setCurrentIndex(idx);
    }
    showToast("Đã trộn ngẫu nhiên Hàng đợi");
  };

  const handleClearQueue = () => {
    setConfirmDialog({
      isOpen: true,
      title: "Xóa hàng đợi",
      message: "Bạn có chắc chắn muốn xóa toàn bộ video trong hàng đợi hiện tại?",
      confirmText: "Xóa hết",
      cancelText: "Hủy",
      variant: "danger",
      onConfirm: () => {
        setQueueTracks([]);
        setPlaylist([]);
        setCurrentIndex(0);
        setConfirmDialog(null);
        showToast("Đã xóa Hàng đợi");
      },
    });
  };

  const handleCreatePlaylist = (name: string, initialTrack?: Track | null) => {
    const trimmed = name.trim();
    if (!trimmed) return;
    const newPl = {
      id: `pl-${Date.now()}`,
      name: trimmed,
      created: Date.now(),
      items: initialTrack ? [initialTrack] : [],
    };
    setLibraryData((prev) => ({
      ...prev,
      playlists: [newPl, ...prev.playlists],
    }));
    setNewPlaylistName("");
    setIsCreatePlaylistOpen(false);
    if (initialTrack) {
      setPlaylistTargetTrack(null);
      showToast(`Đã tạo "${trimmed}" và thêm video`);
    } else {
      showToast(`Đã tạo danh sách "${trimmed}"`);
    }
  };

  const handleDeletePlaylist = (playlistId: string, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    const targetPl = libraryData.playlists.find((p) => p.id === playlistId);
    setConfirmDialog({
      isOpen: true,
      title: "Xóa danh sách phát",
      message: `Bạn có chắc chắn muốn xóa danh sách "${targetPl?.name || "này"}"?`,
      confirmText: "Xóa",
      cancelText: "Hủy",
      variant: "danger",
      onConfirm: () => {
        setLibraryData((prev) => ({
          ...prev,
          playlists: prev.playlists.filter((p) => p.id !== playlistId),
        }));
        if (selectedPlaylistId === playlistId) setSelectedPlaylistId(null);
        setConfirmDialog(null);
        showToast("Đã xóa danh sách phát");
      },
    });
  };

  const handleAddTrackToPlaylist = (playlistId: string, track: Track) => {
    let targetName = "";
    setLibraryData((prev) => ({
      ...prev,
      playlists: prev.playlists.map((p) => {
        if (p.id !== playlistId) return p;
        targetName = p.name;
        const items: Track[] = Array.isArray(p.items) ? p.items : [];
        if (items.some((t) => t.id === track.id)) return p;
        return { ...p, items: [...items, track] };
      }),
    }));
    setPlaylistTargetTrack(null);
    showToast(`Đã thêm vào "${targetName || "Danh sách phát"}"`);
  };

  const handleRemoveTrackFromPlaylist = (playlistId: string, trackId: string, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setLibraryData((prev) => ({
      ...prev,
      playlists: prev.playlists.map((p) => {
        if (p.id !== playlistId) return p;
        return {
          ...p,
          items: (p.items || []).filter((t: Track) => t.id !== trackId),
        };
      }),
    }));
  };

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
  }>(() => {
    try {
      const saved = localStorage.getItem("thuongtube_library");
      if (saved) {
        const parsed = JSON.parse(saved);
        return {
          favorites: Array.isArray(parsed.favorites) ? parsed.favorites : [],
          history: Array.isArray(parsed.history) ? parsed.history : [],
          playlists: Array.isArray(parsed.playlists) ? parsed.playlists : [],
        };
      }
    } catch { }
    return { favorites: [], history: [], playlists: [] };
  });

  // Persist libraryData, playlist, queueTracks, and currentIndex to localStorage
  useEffect(() => {
    try {
      localStorage.setItem("thuongtube_library", JSON.stringify(libraryData));
    } catch { }
  }, [libraryData]);

  useEffect(() => {
    try {
      localStorage.setItem("thuongtube_playlist", JSON.stringify(playlist));
    } catch { }
  }, [playlist]);

  useEffect(() => {
    try {
      localStorage.setItem("thuongtube_queue", JSON.stringify(queueTracks));
    } catch { }
  }, [queueTracks]);

  useEffect(() => {
    try {
      localStorage.setItem("thuongtube_current_index", String(currentIndex));
    } catch { }
  }, [currentIndex]);

  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [syncSuccessMsg, setSyncSuccessMsg] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState<boolean>(false);
  const [loginEmail, setLoginEmail] = useState<string>("");
  const [loginPassword, setLoginPassword] = useState<string>("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [registerName, setRegisterName] = useState<string>("");
  const [registerConfirmPassword, setRegisterConfirmPassword] = useState<string>("");
  const [avatarKey, setAvatarKey] = useState<number>(Date.now());
  const [isEditingProfile, setIsEditingProfile] = useState<boolean>(false);
  const [editProfileName, setEditProfileName] = useState<string>("");
  const [editProfileAvatar, setEditProfileAvatar] = useState<string | null>(null);
  const [isSavingProfile, setIsSavingProfile] = useState<boolean>(false);

  const [isLoadingRelated, setIsLoadingRelated] = useState<boolean>(false);

  const shuffleList = (items: Track[]) => {
    const arr = [...items];
    for (let i = arr.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [arr[i], arr[j]] = [arr[j], arr[i]];
    }
    return arr;
  };

  const fetchRelatedRandom = async (track: Track) => {
    if (!track?.id) return;
    setIsLoadingRelated(true);
    try {
      const q = `${track.title || ""} ${track.author || ""}`.trim();
      const res = await fetch(
        `${API_BASE}/api/related?id=${encodeURIComponent(track.id)}&q=${encodeURIComponent(q)}`
      );
      if (res.ok) {
        const data = await res.json();
        if (Array.isArray(data.tracks) && data.tracks.length > 0) {
          setRandomTracks(shuffleList(data.tracks));
          setIsLoadingRelated(false);
          return;
        }
      }
    } catch { }
    setIsLoadingRelated(false);
  };

  const shuffleRandomTracks = () => {
    if (randomTracks.length > 0) {
      setRandomTracks((prev) => shuffleList(prev));
      return;
    }
    if (currentTrack) {
      fetchRelatedRandom(currentTrack);
      return;
    }
    const poolMap = new Map<string, Track>();
    [
      ...trendingTracks,
      ...newReleaseTracks,
      ...libraryData.history,
      ...libraryData.favorites,
      ...playlist,
      ...searchResults,
    ].forEach((t) => {
      if (t && t.id && !poolMap.has(t.id)) {
        poolMap.set(t.id, t);
      }
    });
    setRandomTracks(shuffleList(Array.from(poolMap.values())));
  };

  useEffect(() => {
    if (randomTracks.length === 0) {
      if (currentTrack) {
        fetchRelatedRandom(currentTrack);
      } else if (
        trendingTracks.length > 0 ||
        newReleaseTracks.length > 0 ||
        libraryData.history.length > 0 ||
        libraryData.favorites.length > 0 ||
        playlist.length > 0
      ) {
        shuffleRandomTracks();
      }
    }
  }, [trendingTracks, newReleaseTracks, libraryData.history.length, libraryData.favorites.length, playlist.length]);

  // Top Search State & History
  const [topSearchText, setTopSearchText] = useState<string>("");
  const [searchQueryTitle, setSearchQueryTitle] = useState<string>("");
  const [isSearchOpen, setIsSearchOpen] = useState<boolean>(false);
  const [searchResults, setSearchResults] = useState<Track[]>([]);
  const [searchStructured, setSearchStructured] = useState<StructuredSearchResult | null>(null);
  const [activeSearchTab, setActiveSearchTab] = useState<"all" | "videos" | "tracks" | "artists" | "playlists">("all");
  const [isSearching, setIsSearching] = useState<boolean>(false);
  const [searchHistory, setSearchHistory] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem("kahoii_search_history");
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });
  const [isSearchFocused, setIsSearchFocused] = useState<boolean>(false);
  const searchDropdownRef = useRef<HTMLDivElement | null>(null);

  const addToSearchHistory = (query: string) => {
    const trimmed = query.trim();
    if (!trimmed) return;
    setSearchHistory((prev) => {
      const filtered = prev.filter((item) => item.toLowerCase() !== trimmed.toLowerCase());
      const updated = [trimmed, ...filtered].slice(0, 10);
      try {
        localStorage.setItem("kahoii_search_history", JSON.stringify(updated));
      } catch { }
      return updated;
    });
  };

  const removeFromSearchHistory = (itemToRemove: string, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setSearchHistory((prev) => {
      const updated = prev.filter((item) => item !== itemToRemove);
      try {
        localStorage.setItem("kahoii_search_history", JSON.stringify(updated));
      } catch { }
      return updated;
    });
  };

  const clearSearchHistory = (e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setSearchHistory([]);
    try {
      localStorage.removeItem("kahoii_search_history");
    } catch { }
  };

  const executeSearch = async (query: string) => {
    const trimmed = query.trim();
    if (!trimmed) return;

    setTopSearchText(trimmed);
    setSearchQueryTitle(trimmed);
    addToSearchHistory(trimmed);
    setIsSearchFocused(false);
    setIsSearchOpen(true);
    setIsSearching(true);
    setActiveSearchTab("all");
    setSearchResults([]);
    setSearchStructured(null);

    try {
      const res = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(trimmed)}`);
      const data = await res.json();
      const tracksList: Track[] = data.results || data.videos || data.tracks || [];
      setSearchResults(tracksList);
      setSearchStructured({
        query: data.query || trimmed,
        topResult: data.topResult || null,
        tracks: data.tracks || tracksList,
        videos: data.videos || [],
        artists: data.artists || [],
        playlists: data.playlists || [],
      });
    } catch (err) {
      console.error("Search error:", err);
    } finally {
      setIsSearching(false);
    }
  };

  const handleHistoryItemClick = (query: string) => {
    executeSearch(query);
  };

  const playPlaylistItem = async (playlistItem: SearchPlaylistItem) => {
    try {
      const res = await fetch(`${API_BASE}/api/playlist?id=${encodeURIComponent(playlistItem.id)}`);
      const data = await res.json();
      if (data.tracks && data.tracks.length > 0) {
        setPlaylist(data.tracks);
        setQueueTracks(data.tracks);
        setCurrentIndex(0);
        loadAndPlayTrack(data.tracks[0], true);
      } else {
        executeSearch(playlistItem.title);
      }
    } catch {
      executeSearch(playlistItem.title);
    }
  };

  // Multi-platform App Downloads (Auto-synced with latest GitHub releases)
  const [showDownloadMenu, setShowDownloadMenu] = useState<boolean>(false);
  const downloadMenuRef = useRef<HTMLDivElement | null>(null);
  const [downloadData, setDownloadData] = useState({
    pc: {
      version: "v1.0.34",
      fileName: "KahoiiMusic_Setup_v1.0.34.exe",
      downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/v1.0.34/KahoiiMusic_Setup_v1.0.34.exe",
      size: "24.2 MB",
    },
    android: {
      version: "Bản mới nhất",
      fileName: "KahoiiMusic.apk",
      downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/KahoiiMusic.apk",
      size: "14.6 MB",
    },
    ios: {
      version: "Bản mới nhất",
      fileName: "KahoiiMusic.ipa",
      downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-ios/KahoiiMusic.ipa",
      size: "1.4 MB",
    },
    extension: {
      version: "Bản mới nhất",
      fileName: "Extension.zip",
      downloadUrl: "https://github.com/Iiwa-San/KahoiiMusic/archive/refs/heads/Extension.zip",
      size: "Source ZIP",
    },
  });

  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (downloadMenuRef.current && !downloadMenuRef.current.contains(e.target as Node)) {
        setShowDownloadMenu(false);
      }
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  useEffect(() => {
    const fetchLatestDownloads = async () => {
      try {
        const ghRes = await fetch("https://api.github.com/repos/Iiwa-San/KahoiiMusic/releases");
        if (!ghRes.ok) return;
        const releases = (await ghRes.json()) as any[];
        const pcRelease = releases.find((r) => /^v\d+/i.test(r.tag_name));
        const pcAsset = pcRelease?.assets?.find((a: any) => a.name.endsWith(".exe"));
        const androidRelease = releases.find(
          (r) => r.tag_name === "latest-android" || r.assets?.some((a: any) => a.name.endsWith(".apk"))
        );
        const androidAsset = androidRelease?.assets?.find((a: any) => a.name.endsWith(".apk"));
        const iosRelease = releases.find(
          (r) => r.tag_name === "latest-ios" || r.assets?.some((a: any) => a.name.endsWith(".ipa"))
        );
        const iosAsset = iosRelease?.assets?.find((a: any) => a.name.endsWith(".ipa"));
        const extRelease = releases.find((r) =>
          r.assets?.some((a: any) => a.name.toLowerCase().includes("extension") && a.name.endsWith(".zip"))
        );
        const extAsset = extRelease?.assets?.find(
          (a: any) => a.name.toLowerCase().includes("extension") && a.name.endsWith(".zip")
        );
        const formatBytes = (bytes: number) => (bytes ? (bytes / (1024 * 1024)).toFixed(1) + " MB" : "");

        setDownloadData((prev) => ({
          pc: {
            version: pcRelease?.tag_name || prev.pc.version,
            fileName: pcAsset?.name || prev.pc.fileName,
            downloadUrl: pcAsset?.browser_download_url || prev.pc.downloadUrl,
            size: pcAsset?.size ? formatBytes(pcAsset.size) : prev.pc.size,
          },
          android: {
            version: "Bản mới nhất",
            fileName: androidAsset?.name || prev.android.fileName,
            downloadUrl: androidAsset?.browser_download_url || prev.android.downloadUrl,
            size: androidAsset?.size ? formatBytes(androidAsset.size) : prev.android.size,
          },
          ios: {
            version: "Bản mới nhất",
            fileName: iosAsset?.name || prev.ios.fileName,
            downloadUrl: iosAsset?.browser_download_url || prev.ios.downloadUrl,
            size: iosAsset?.size ? formatBytes(iosAsset.size) : prev.ios.size,
          },
          extension: {
            version: "Bản mới nhất",
            fileName: extAsset?.name || prev.extension.fileName,
            downloadUrl: extAsset?.browser_download_url || prev.extension.downloadUrl,
            size: "Source ZIP",
          },
        }));
      } catch { }
    };
    fetchLatestDownloads();
  }, []);

  // Sleep Timer State & Controller
  const [sleepTimerMinutes, setSleepTimerMinutes] = useState<number | null>(null);
  const sleepTimerRef = useRef<any>(null);

  const startSleepTimer = (minutes: number | null) => {
    if (sleepTimerRef.current) {
      clearTimeout(sleepTimerRef.current);
      sleepTimerRef.current = null;
    }
    setSleepTimerMinutes(minutes);
    if (minutes && minutes > 0) {
      sleepTimerRef.current = setTimeout(() => {
        if (audioRef.current) {
          audioRef.current.pause();
          setIsPlaying(false);
        }
        setSleepTimerMinutes(null);
      }, minutes * 60 * 1000);
    }
  };

  // Favorite toggle for current track
  const toggleCurrentFavorite = () => {
    if (!currentTrack) return;
    setLibraryData((prev) => {
      const isFav = prev.favorites.some((t) => t.id === currentTrack.id);
      let updatedFavs: Track[];
      if (isFav) {
        updatedFavs = prev.favorites.filter((t) => t.id !== currentTrack.id);
      } else {
        updatedFavs = [currentTrack, ...prev.favorites];
      }
      return { ...prev, favorites: updatedFavs };
    });
  };

  // Close search history dropdown on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (searchDropdownRef.current && !searchDropdownRef.current.contains(e.target as Node)) {
        setIsSearchFocused(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Dynamic Theme Palette matching KahoiiMusic desktop ColorExtractorService
  const [currentTheme, setCurrentTheme] = useState<ThemePalette>(DEFAULT_THEME);

  const audioRef = useRef<HTMLAudioElement | null>(null);

  const currentTrack = playlist[currentIndex] || null;
  const isCurrentTrackMusic = currentTrack
    ? isTrackMusic(
        trackMusicMap[currentTrack.id] !== undefined
          ? { ...currentTrack, isMusic: trackMusicMap[currentTrack.id] }
          : currentTrack
      )
    : true;

  const handleSwitchToKahoii = () => {
    if (currentTrack && !isCurrentTrackMusic) return;
    if (!currentTrack) {
      window.location.href = KAHOII_URL;
      return;
    }

    // Pause local playback before handing off to Kahoii Music
    try { audioRef.current?.pause(); } catch {}
    try { videoRef.current?.pause(); } catch {}
    try { ytPlayerRef.current?.pauseVideo?.(); } catch {}
    setIsPlaying(false);

    const params = new URLSearchParams({
      handoff: "1",
      id: currentTrack.id,
      title: currentTrack.title || "",
      author: currentTrack.author || "",
      duration: String(Math.floor(currentTrack.duration || duration || 0)),
      thumbnail: currentTrack.thumbnail || "",
      t: String(Math.floor(currentTime || 0)),
      autoplay: isPlaying ? "1" : "0",
    });
    window.location.href = `${KAHOII_URL}?${params.toString()}`;
  };

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

  // Persist volume & sync across Audio, Video, and YouTube players
  useEffect(() => {
    localStorage.setItem("kahoii_web_volume", volume.toString());
    if (audioRef.current) {
      audioRef.current.volume = isMuted ? 0 : volume;
      audioRef.current.muted = isMuted;
    }
    if (videoRef.current) {
      videoRef.current.volume = isMuted ? 0 : volume;
      videoRef.current.muted = isAdaptiveSyncedRef.current ? true : isMuted;
    }
    if (ytPlayerRef.current && typeof ytPlayerRef.current.setVolume === "function") {
      try {
        ytPlayerRef.current.setVolume(isMuted ? 0 : volume * 100);
      } catch { }
    }
  }, [volume, isMuted]);

  // Keep audio continuous in background tabs; resync video frame when returning to tab
  useEffect(() => {
    const onVisibilityChange = () => {
      if (
        !document.hidden &&
        activePlayerType.current === "video" &&
        isAdaptiveSyncedRef.current &&
        videoRef.current &&
        audioRef.current
      ) {
        const target = audioRef.current.currentTime;
        if (Math.abs(videoRef.current.currentTime - target) > 0.4) {
          videoRef.current.currentTime = target;
        }
        if (!audioRef.current.paused && videoRef.current.paused) {
          videoRef.current.play().catch(() => { });
        }
      }
    };
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => document.removeEventListener("visibilitychange", onVisibilityChange);
  }, []);

  // Fetch User Session & Library Data
  const fetchUserSession = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/user/session`);
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
      const res = await fetch(`${API_BASE}/api/user/library`);
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
        setLibraryData((prev) => {
          const mergeTracks = (primary: Track[], secondary: Track[]) => {
            const seen = new Set(primary.map((t) => t.id));
            return [...primary, ...secondary.filter((t) => !seen.has(t.id))];
          };
          return {
            favorites: mappedFavs.length > 0 ? mergeTracks(mappedFavs, prev.favorites) : prev.favorites,
            history: mappedHist.length > 0 ? mergeTracks(mappedHist, prev.history) : prev.history,
            playlists: rawPlaylists.length > 0 ? rawPlaylists : prev.playlists,
          };
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

        if (initialTracks.length > 0 && libraryData.history.length === 0 && playlist.length === 0) {
          setPlaylist((prev) => (prev.length === 0 ? initialTracks : prev));
          setQueueTracks((prev) => (prev.length === 0 ? initialTracks : prev));
          loadAndPlayTrack(initialTracks[0], false);
        }
      }
    } catch (err) {
      console.error("Library fetch failed:", err);
    }
  };

  const searchArtistSongs = (artistName: string) => {
    executeSearch(artistName);
  };

  // Preload initial track metadata, user data, home feed & YouTube Player API on mount
  useEffect(() => {
    const urlParams = new URLSearchParams(window.location.search);
    const handoffId = urlParams.get("id");
    if (handoffId) {
      const handoffTrack: Track = {
        id: handoffId,
        title: urlParams.get("title") || "Đang phát",
        author: urlParams.get("author") || "YouTube",
        duration: Number(urlParams.get("duration") || 0) || 0,
        thumbnail:
          urlParams.get("thumbnail") ||
          `https://i.ytimg.com/vi/${handoffId}/hqdefault.jpg`,
        isMusic: true,
      };
      const startAt = Math.max(0, Number(urlParams.get("t") || 0) || 0);
      const shouldAutoPlay = urlParams.get("autoplay") !== "0";

      try {
        window.history.replaceState({}, "", window.location.pathname);
      } catch {}

      setPlaylist((prev) => [handoffTrack, ...prev.filter((t) => t.id !== handoffId)]);
      setQueueTracks((prev) => [handoffTrack, ...prev.filter((t) => t.id !== handoffId)]);
      setCurrentIndex(0);
      setTrackMusicMap((prev) => ({ ...prev, [handoffId]: true }));
      loadAndPlayTrack(handoffTrack, shouldAutoPlay, { startTime: startAt });
    } else {
      const mostRecent = libraryData.history[0] || playlist[currentIndex] || playlist[0];
      if (mostRecent) {
        const idx = playlist.findIndex((t) => t.id === mostRecent.id);
        if (idx >= 0) {
          setCurrentIndex(idx);
        } else {
          setPlaylist((prev) => [mostRecent, ...prev.filter((t) => t.id !== mostRecent.id)]);
          setQueueTracks((prev) => [mostRecent, ...prev.filter((t) => t.id !== mostRecent.id)]);
          setCurrentIndex(0);
        }
        loadAndPlayTrack(mostRecent, false);
      }
    }

    fetchUserSession();
    fetchLibraryData();

    // Fetch Live Home Feed (Trending & New Releases)
    fetch(`${API_BASE}/api/home`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) {
          if (Array.isArray(data.trending) && data.trending.length > 0) {
            setTrendingTracks(data.trending);
            setPlaylist((prev) => (prev.length === 0 ? data.trending : prev));
            setQueueTracks((prev) => (prev.length === 0 ? data.trending : prev));
          }
          if (Array.isArray(data.newReleases) && data.newReleases.length > 0) {
            setNewReleaseTracks(data.newReleases);
          }
        }
      })
      .catch(() => { });

    // Initialize YouTube Iframe Player
    const loadYtScript = () => {
      if (!(window as any).YT) {
        const tag = document.createElement("script");
        tag.src = "https://www.youtube.com/iframe_api";
        const firstScript = document.getElementsByTagName("script")[0];
        firstScript.parentNode?.insertBefore(tag, firstScript);
      }
    };

    const setupYtPlayer = () => {
      if ((window as any).YT && (window as any).YT.Player) {
        try {
          ytPlayerRef.current = new (window as any).YT.Player("kahoii-yt-iframe", {
            height: "100%",
            width: "100%",
            playerVars: {
              autoplay: 0,
              controls: 0,
              disablekb: 1,
              fs: 0,
              rel: 0,
              modestbranding: 1,
              playsinline: 1,
              origin: window.location.origin,
            },
            events: {
              onReady: () => {
                if (typeof ytPlayerRef.current?.setVolume === "function") {
                  ytPlayerRef.current.setVolume(isMuted ? 0 : volume * 100);
                }
                if (pendingYtTrackRef.current && typeof ytPlayerRef.current?.loadVideoById === "function") {
                  const pending = pendingYtTrackRef.current;
                  pendingYtTrackRef.current = null;
                  try {
                    ytPlayerRef.current.loadVideoById({
                      videoId: pending.videoId,
                      startSeconds: pending.startSeconds,
                    });
                    if (pending.autoPlay) {
                      ytPlayerRef.current.playVideo();
                      setIsPlaying(true);
                    } else {
                      ytPlayerRef.current.pauseVideo();
                      setIsPlaying(false);
                    }
                  } catch {}
                }
              },
              onStateChange: (event: any) => {
                if (event.data === 1) {
                  setIsPlaying(true);
                  setIsLoading(false);
                  setErrorMessage(null);
                } else if (event.data === 2) {
                  setIsPlaying(false);
                } else if (event.data === 0) {
                  handleTrackEnded();
                } else if (event.data === 3) {
                  setIsLoading(true);
                }
              },
              onError: () => {
                setIsLoading(false);
              },
            },
          });
        } catch (e) {
          console.warn("YouTube Player init error:", e);
        }
      }
    };

    if ((window as any).YT && (window as any).YT.Player) {
      setupYtPlayer();
    } else {
      (window as any).onYouTubeIframeAPIReady = setupYtPlayer;
      loadYtScript();
    }
  }, []);

  // Time Tracker ticker for YouTube Player
  useEffect(() => {
    const timer = setInterval(() => {
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.getCurrentTime === "function"
      ) {
        try {
          const ct = ytPlayerRef.current.getCurrentTime() || 0;
          const dur = ytPlayerRef.current.getDuration() || 0;
          if (ct > 0) setCurrentTime(ct);
          if (dur > 0 && Math.abs(dur - duration) > 1) setDuration(dur);
        } catch { }
      }
    }, 250);
    return () => clearInterval(timer);
  }, [duration]);

  // Sync volume with YouTube Player
  useEffect(() => {
    if (
      activePlayerType.current === "youtube" &&
      ytPlayerRef.current &&
      typeof ytPlayerRef.current.setVolume === "function"
    ) {
      try {
        ytPlayerRef.current.setVolume(isMuted ? 0 : volume * 100);
        if (isMuted) ytPlayerRef.current.mute();
        else ytPlayerRef.current.unMute();
      } catch { }
    }
  }, [volume, isMuted]);

  const handleSyncCloud = async () => {
    setIsSyncing(true);
    setSyncSuccessMsg(null);
    try {
      const res = await fetch(`${API_BASE}/api/user/sync`, { method: "POST" });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Đồng bộ thất bại");
      setUserSession(data);
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setSyncSuccessMsg("Đã đồng bộ dữ liệu mới nhất từ Cloud thành công!");
      setTimeout(() => setSyncSuccessMsg(null), 4000);
    } catch (err: any) {
      setConfirmDialog({
        isOpen: true,
        title: "Lỗi đồng bộ",
        message: err.message || "Không thể đồng bộ dữ liệu Cloud.",
        confirmText: "Đóng",
        variant: "primary",
        showCancel: false,
        onConfirm: () => setConfirmDialog(null),
      });
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
      const res = await fetch(`${API_BASE}/api/user/login`, {
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

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!loginEmail || !loginPassword) {
      setLoginError("Vui lòng nhập đầy đủ email và mật khẩu");
      return;
    }
    if (loginPassword.length < 6) {
      setLoginError("Mật khẩu phải có ít nhất 6 ký tự");
      return;
    }
    if (loginPassword !== registerConfirmPassword) {
      setLoginError("Mật khẩu xác nhận không khớp");
      return;
    }
    setIsLoggingIn(true);
    setLoginError(null);
    try {
      const res = await fetch(`${API_BASE}/api/user/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email: loginEmail,
          password: loginPassword,
          displayName: registerName.trim() || undefined,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Đăng ký thất bại");
      setUserSession(data);
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setLoginPassword("");
      setRegisterConfirmPassword("");
      setRegisterName("");
      setSyncSuccessMsg("Đăng ký tài khoản thành công!");
      setTimeout(() => setSyncSuccessMsg(null), 3000);
    } catch (err: any) {
      setLoginError(err.message || "Lỗi đăng ký");
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleAvatarFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        const canvas = document.createElement("canvas");
        const size = 160;
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext("2d");
        if (!ctx) return;
        const minSide = Math.min(img.width, img.height);
        const sx = (img.width - minSide) / 2;
        const sy = (img.height - minSide) / 2;
        ctx.drawImage(img, sx, sy, minSide, minSide, 0, 0, size, size);
        setEditProfileAvatar(canvas.toDataURL("image/png"));
      };
      img.src = reader.result as string;
    };
    reader.readAsDataURL(file);
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSavingProfile(true);
    setLoginError(null);
    try {
      const res = await fetch(`${API_BASE}/api/user/profile`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: editProfileName.trim() || undefined,
          avatarBase64: editProfileAvatar || undefined,
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Cập nhật thất bại");
      setUserSession(data);
      setAvatarKey(Date.now());
      setIsEditingProfile(false);
      setEditProfileAvatar(null);
      setSyncSuccessMsg("Đã cập nhật thông tin người dùng!");
      setTimeout(() => setSyncSuccessMsg(null), 3000);
    } catch (err: any) {
      setLoginError(err.message || "Lỗi cập nhật hồ sơ");
    } finally {
      setIsSavingProfile(false);
    }
  };

  const handleLogout = () => {
    setConfirmDialog({
      isOpen: true,
      title: "Đăng xuất",
      message: "Bạn có chắc chắn muốn đăng xuất khỏi tài khoản hiện tại?",
      confirmText: "Đăng xuất",
      cancelText: "Ở lại",
      variant: "danger",
      onConfirm: async () => {
        setConfirmDialog(null);
        try {
          await fetch(`${API_BASE}/api/user/logout`, { method: "POST" });
          setUserSession({ isLoggedIn: false, user: null });
          setLibraryData({ favorites: [], history: [], playlists: [] });
          showToast("Đã đăng xuất");
        } catch (err) {
          console.error("Logout error:", err);
        }
      },
    });
  };

  const toPlayableProxyUrl = (rawUrl: string) => {
    if (!rawUrl) return "";
    return rawUrl.includes("googlevideo.com")
      ? `${API_BASE}/api/proxy-audio?url=${encodeURIComponent(rawUrl)}`
      : rawUrl;
  };

  const applyQualityOption = (option: VideoQualityOption, autoPlay: boolean, resumeTime = 0) => {
    if (audioRef.current) {
      try { audioRef.current.pause(); } catch { }
    }
    if (videoRef.current) {
      try { videoRef.current.pause(); } catch { }
    }

    setSelectedQuality(option.quality);

    // Case 1: Audio-Only mode
    if (option.height === 0 || !option.videoUrl) {
      isAdaptiveSyncedRef.current = false;
      activePlayerType.current = "audio";
      setPlayerMode("audio");
      const audioSrc = toPlayableProxyUrl(option.audioUrl || "");
      if (audioRef.current && audioSrc) {
        audioRef.current.src = audioSrc;
        audioRef.current.playbackRate = playbackRate;
        audioRef.current.volume = isMuted ? 0 : volume;
        audioRef.current.muted = isMuted;
        audioRef.current.currentTime = resumeTime;
        if (autoPlay) {
          audioRef.current.play().then(() => setIsPlaying(true)).catch(() => setIsPlaying(false));
        } else {
          setIsPlaying(false);
        }
      }
      return;
    }

    // Case 2: Video mode (Muxed or Adaptive Synced)
    activePlayerType.current = "video";
    setPlayerMode("video");
    const videoSrc = toPlayableProxyUrl(option.videoUrl);
    if (!videoRef.current || !videoSrc) return;

    if (option.isMuxed || !option.audioUrl) {
      isAdaptiveSyncedRef.current = false;
      videoRef.current.src = videoSrc;
      videoRef.current.muted = isMuted;
      videoRef.current.volume = isMuted ? 0 : volume;
      videoRef.current.playbackRate = playbackRate;
      videoRef.current.currentTime = resumeTime;
      if (autoPlay) {
        videoRef.current.play().then(() => setIsPlaying(true)).catch(() => setIsPlaying(false));
      } else {
        setIsPlaying(false);
      }
    } else {
      // Adaptive HD: muted <video> + synced <audio>
      isAdaptiveSyncedRef.current = true;
      const audioSrc = toPlayableProxyUrl(option.audioUrl);
      videoRef.current.src = videoSrc;
      videoRef.current.muted = true;
      videoRef.current.playbackRate = playbackRate;
      videoRef.current.currentTime = resumeTime;

      if (audioRef.current) {
        audioRef.current.src = audioSrc;
        audioRef.current.muted = isMuted;
        audioRef.current.volume = isMuted ? 0 : volume;
        audioRef.current.playbackRate = playbackRate;
        audioRef.current.currentTime = resumeTime;
      }

      if (autoPlay) {
        Promise.all([
          videoRef.current.play().catch(() => { }),
          audioRef.current ? audioRef.current.play().catch(() => { }) : Promise.resolve(),
        ]).then(() => setIsPlaying(true));
      } else {
        setIsPlaying(false);
      }
    }
  };

  const handleSelectQuality = (option: VideoQualityOption) => {
    const resumeTime =
      activePlayerType.current === "video" && videoRef.current
        ? videoRef.current.currentTime
        : activePlayerType.current === "audio" && audioRef.current
          ? audioRef.current.currentTime
          : currentTime;
    applyQualityOption(option, isPlaying, resumeTime);
  };

  const handleSelectSpeed = (speed: number) => {
    setPlaybackRate(speed);
    if (videoRef.current) {
      videoRef.current.playbackRate = speed;
    }
    if (audioRef.current) {
      audioRef.current.playbackRate = speed;
    }
    if (activePlayerType.current === "youtube" && ytPlayerRef.current?.setPlaybackRate) {
      try {
        ytPlayerRef.current.setPlaybackRate(speed);
      } catch { }
    }
  };

  const refreshDiscoveryFeed = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/home?refresh=1`);
      if (res.ok) {
        const data = await res.json();
        if (Array.isArray(data.trending) && data.trending.length > 0) {
          setTrendingTracks(data.trending);
        }
        if (Array.isArray(data.newReleases) && data.newReleases.length > 0) {
          setNewReleaseTracks(data.newReleases);
        }
        showToast("Đã làm mới gợi ý Khám phá!");
      }
    } catch { }
  };

  const playAudioInDiscovery = (track: Track, list?: Track[]) => {
    if (list && list.length > 0) {
      setPlaylist(list);
      setQueueTracks(list);
      const idx = list.findIndex((t) => t.id === track.id);
      setCurrentIndex(idx >= 0 ? idx : 0);
    } else {
      setPlaylist((prev) => {
        const exists = prev.findIndex((t) => t.id === track.id);
        if (exists >= 0) {
          setCurrentIndex(exists);
          return prev;
        }
        setCurrentIndex(0);
        return [track, ...prev];
      });
      setQueueTracks((prev) => (prev.some((t) => t.id === track.id) ? prev : [track, ...prev]));
    }
    loadAndPlayTrack(track, true, { stayInView: true, audioOnly: true });
  };

  const loadAndPlayTrack = async (
    track: Track,
    autoPlay = true,
    options?: { stayInView?: boolean; audioOnly?: boolean; startTime?: number }
  ) => {
    if (!track) return;
    const initialStartTime = Math.max(0, options?.startTime || 0);
    setIsLoading(true);
    setErrorMessage(null);
    setCurrentTime(initialStartTime);
    setAvailableQualities([]);
    if (!options?.stayInView) {
      setShowDiscovery(false);
      setIsSearchOpen(false);
    }

    // Record to watched history (persisted automatically to localStorage)
    setLibraryData((prev) => ({
      ...prev,
      history: [track, ...prev.history.filter((h) => h.id !== track.id)].slice(0, 100),
    }));

    // Fetch YouTube related videos for the right random panel
    fetchRelatedRandom(track);

    // Stop current audio, video, and YouTube player
    if (audioRef.current) {
      try { audioRef.current.pause(); } catch { }
    }
    if (videoRef.current) {
      try { videoRef.current.pause(); } catch { }
    }
    if (ytPlayerRef.current && typeof ytPlayerRef.current.stopVideo === "function") {
      try { ytPlayerRef.current.stopVideo(); } catch { }
    }

    // Strategy 1: Resolve direct video + audio streams from server
    let streamPlayed = false;
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 20000);

      const res = await fetch(`${API_BASE}/api/stream?id=${track.id}`, {
        signal: controller.signal,
      });
      clearTimeout(timeoutId);

      if (res.ok) {
        const data = await res.json();
        if (typeof data.isMusic === "boolean") {
          setTrackMusicMap((prev) => ({ ...prev, [track.id]: data.isMusic }));
        }
        const qualities: VideoQualityOption[] = Array.isArray(data.qualities) ? data.qualities : [];
        if (qualities.length > 0) {
          setAvailableQualities(qualities);
          const preferred = options?.audioOnly
            ? qualities.find((q) => q.height === 0) || qualities[qualities.length - 1]
            : qualities.find((q) => q.quality === selectedQuality) ||
            qualities.find((q) => q.height === 720) ||
            qualities.find((q) => q.height > 0) ||
            qualities[0];
          applyQualityOption(preferred, autoPlay, initialStartTime);
          streamPlayed = true;
        } else if (data.streamUrl && audioRef.current) {
          const fallbackOpt: VideoQualityOption = {
            quality: "Chỉ âm thanh",
            height: 0,
            videoUrl: "",
            audioUrl: data.streamUrl,
            isMuxed: false,
          };
          setAvailableQualities([fallbackOpt]);
          applyQualityOption(fallbackOpt, autoPlay, initialStartTime);
          streamPlayed = true;
        }
      }
    } catch {
      // Backend stream unavailable or blocked
    }

    // Strategy 2: Zero-failure Visible YouTube Video Player fallback
    if (!streamPlayed) {
      isAdaptiveSyncedRef.current = false;
      activePlayerType.current = "youtube";
      setPlayerMode("youtube");
      setSelectedQuality("Auto");
      if (ytPlayerRef.current && typeof ytPlayerRef.current.loadVideoById === "function") {
        try {
          ytPlayerRef.current.loadVideoById({
            videoId: track.id,
            startSeconds: initialStartTime,
          });
          if (ytPlayerRef.current.setPlaybackRate) {
            ytPlayerRef.current.setPlaybackRate(playbackRate);
          }
          if (autoPlay) {
            ytPlayerRef.current.playVideo();
            setIsPlaying(true);
          } else {
            ytPlayerRef.current.pauseVideo();
            setIsPlaying(false);
          }
          setErrorMessage(null);
        } catch (ytErr) {
          console.warn("YouTube player fallback warning:", ytErr);
        }
      } else {
        pendingYtTrackRef.current = {
          videoId: track.id,
          startSeconds: initialStartTime,
          autoPlay,
        };
      }
    }

    // Fetch Lyrics in background
    try {
      const lyricsRes = await fetch(
        `${API_BASE}/api/lyrics?title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
      );
      if (lyricsRes.ok) {
        const lyricsData = await lyricsRes.json();
        setLyrics(lyricsData);
      }
    } catch { }

    setIsLoading(false);
  };

  const handleTimeUpdate = () => {
    if (activePlayerType.current === "video") {
      if (isAdaptiveSyncedRef.current && audioRef.current && videoRef.current) {
        const aTime = audioRef.current.currentTime;
        setCurrentTime(aTime);
        if (
          !document.hidden &&
          !videoRef.current.seeking &&
          !audioRef.current.seeking &&
          videoRef.current.readyState >= 3
        ) {
          const drift = aTime - videoRef.current.currentTime;
          const now = Date.now();
          if (Math.abs(drift) > 1.0 && now - lastSyncTimeRef.current > 2000) {
            lastSyncTimeRef.current = now;
            videoRef.current.currentTime = aTime;
            videoRef.current.playbackRate = playbackRate;
          } else if (drift > 0.15) {
            videoRef.current.playbackRate = Math.min(2.5, playbackRate * 1.06);
          } else if (drift < -0.15) {
            videoRef.current.playbackRate = Math.max(0.25, playbackRate * 0.94);
          } else if (videoRef.current.playbackRate !== playbackRate) {
            videoRef.current.playbackRate = playbackRate;
          }
        }
      } else if (!isAdaptiveSyncedRef.current && videoRef.current) {
        setCurrentTime(videoRef.current.currentTime);
      }
    } else if (activePlayerType.current === "audio" && audioRef.current) {
      setCurrentTime(audioRef.current.currentTime);
    }
  };

  const togglePlayPause = () => {
    if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
      try {
        if (isPlaying) {
          ytPlayerRef.current.pauseVideo();
          setIsPlaying(false);
        } else {
          ytPlayerRef.current.playVideo();
          setIsPlaying(true);
        }
        return;
      } catch { }
    }

    if (activePlayerType.current === "video" && videoRef.current) {
      if (isPlaying) {
        videoRef.current.pause();
        if (isAdaptiveSyncedRef.current && audioRef.current) audioRef.current.pause();
        setIsPlaying(false);
      } else {
        if (!videoRef.current.src && currentTrack) {
          loadAndPlayTrack(currentTrack, true);
          return;
        }
        videoRef.current.play().then(() => setIsPlaying(true)).catch(() => { });
        if (isAdaptiveSyncedRef.current && audioRef.current) {
          audioRef.current.currentTime = videoRef.current.currentTime;
          audioRef.current.play().catch(() => { });
        }
      }
      return;
    }

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
          .catch(() => {
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
      if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
        try { ytPlayerRef.current.seekTo(0, true); } catch { }
      } else if (activePlayerType.current === "video" && videoRef.current) {
        videoRef.current.currentTime = 0;
        if (isAdaptiveSyncedRef.current && audioRef.current) audioRef.current.currentTime = 0;
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
      }
      setCurrentTime(0);
    } else {
      const prevIdx = (currentIndex - 1 + playlist.length) % playlist.length;
      setCurrentIndex(prevIdx);
      loadAndPlayTrack(playlist[prevIdx], true);
    }
  };

  const handleSeek = (newTime: number) => {
    setCurrentTime(newTime);
    lastSyncTimeRef.current = Date.now();
    if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
      try { ytPlayerRef.current.seekTo(newTime, true); } catch { }
    } else if (activePlayerType.current === "video" && videoRef.current) {
      const video = videoRef.current;
      const audio = audioRef.current;

      if (isAdaptiveSyncedRef.current && audio) {
        const token = ++seekTokenRef.current;
        const shouldResume = isPlaying && !document.hidden;

        if (shouldResume) {
          video.pause();
          audio.pause();
        }

        video.playbackRate = playbackRate;
        audio.playbackRate = playbackRate;
        video.currentTime = newTime;
        audio.currentTime = newTime;

        if (shouldResume) {
          const waitSeeked = (el: HTMLMediaElement) =>
            new Promise<void>((resolve) => {
              if (!el.seeking && el.readyState >= 3) {
                resolve();
                return;
              }
              const done = () => {
                el.removeEventListener("canplay", done);
                el.removeEventListener("seeked", done);
                resolve();
              };
              el.addEventListener("canplay", done, { once: true });
              el.addEventListener("seeked", done, { once: true });
              setTimeout(done, 1500);
            });

          Promise.all([waitSeeked(video), waitSeeked(audio)]).then(() => {
            if (token !== seekTokenRef.current) return;
            video.play().catch(() => { });
            audio.play().catch(() => { });
          });
        }
      } else {
        video.currentTime = newTime;
      }
    } else if (audioRef.current) {
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
      if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
        try {
          ytPlayerRef.current.seekTo(0, true);
          ytPlayerRef.current.playVideo();
        } catch { }
      } else if (activePlayerType.current === "video" && videoRef.current) {
        videoRef.current.currentTime = 0;
        videoRef.current.play().catch(() => { });
        if (isAdaptiveSyncedRef.current && audioRef.current) {
          audioRef.current.currentTime = 0;
          audioRef.current.play().catch(() => { });
        }
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
        audioRef.current.play().catch(() => { });
      }
    } else {
      handleNext();
    }
  };

  // Global Keyboard Shortcuts (Space, K, Arrows, J/L, M, F, N/P)
  const keyboardHandlersRef = useRef({
    togglePlayPause,
    handleSeek,
    handleNext,
    handlePrev,
    currentTime,
    duration,
    volume,
    isMuted,
  });
  keyboardHandlersRef.current = {
    togglePlayPause,
    handleSeek,
    handleNext,
    handlePrev,
    currentTime,
    duration,
    volume,
    isMuted,
  };

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        // Allow Space/Arrows on range sliders ONLY if not typing text; actually on range sliders let Left/Right work naturally,
        // and block Space if user is typing in text inputs
        const inputEl = target as HTMLInputElement;
        if (target.tagName !== "INPUT" || inputEl.type !== "range") {
          return;
        }
      }

      const h = keyboardHandlersRef.current;
      const key = e.key.toLowerCase();

      if (e.code === "Space" || key === "k") {
        e.preventDefault();
        h.togglePlayPause();
      } else if (e.key === "ArrowLeft") {
        e.preventDefault();
        h.handleSeek(Math.max(0, h.currentTime - 5));
      } else if (e.key === "ArrowRight") {
        e.preventDefault();
        h.handleSeek(Math.min(h.duration || h.currentTime + 5, h.currentTime + 5));
      } else if (key === "j") {
        e.preventDefault();
        h.handleSeek(Math.max(0, h.currentTime - 10));
      } else if (key === "l") {
        e.preventDefault();
        h.handleSeek(Math.min(h.duration || h.currentTime + 10, h.currentTime + 10));
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        setIsMuted(false);
        setVolume((v) => Math.min(1, Math.round((v + 0.05) * 100) / 100));
      } else if (e.key === "ArrowDown") {
        e.preventDefault();
        setVolume((v) => Math.max(0, Math.round((v - 0.05) * 100) / 100));
      } else if (key === "m") {
        e.preventDefault();
        setIsMuted((m) => !m);
      } else if (key === "f") {
        e.preventDefault();
        const container = document.getElementById("thuongtube-video-container");
        if (!document.fullscreenElement && container) {
          container.requestFullscreen().catch(() => { });
        } else if (document.fullscreenElement) {
          document.exitFullscreen().catch(() => { });
        }
      } else if (key === "i") {
        e.preventDefault();
        if (activePlayerType.current === "video" && videoRef.current && document.pictureInPictureEnabled) {
          if (document.pictureInPictureElement) {
            document.exitPictureInPicture().catch(() => { });
          } else if (videoRef.current.readyState > 0) {
            videoRef.current.requestPictureInPicture().catch(() => { });
          }
        }
      } else if (key === "n" && e.shiftKey) {
        e.preventDefault();
        h.handleNext();
      } else if (key === "p" && e.shiftKey) {
        e.preventDefault();
        h.handlePrev();
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

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

    executeSearch(topSearchText);
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
    // Keep search view open so user can continue viewing and listening without disruption
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
    <div className="w-screen min-h-screen md:h-screen flex flex-col justify-between md:overflow-hidden text-white select-none relative font-sans">
      <audio
        ref={audioRef}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={() => {
          if (activePlayerType.current === "audio") {
            setDuration(audioRef.current?.duration || currentTrack?.duration || 207);
          }
        }}
        onEnded={() => {
          if (activePlayerType.current === "audio") {
            handleTrackEnded();
          }
        }}
        onPlay={() => {
          if (activePlayerType.current === "audio") setIsPlaying(true);
        }}
        onPause={() => {
          if (activePlayerType.current === "audio") setIsPlaying(false);
        }}
        onError={() => {
          if (activePlayerType.current !== "youtube" && currentTrack && ytPlayerRef.current?.loadVideoById) {
            isAdaptiveSyncedRef.current = false;
            activePlayerType.current = "youtube";
            setPlayerMode("youtube");
            setSelectedQuality("Auto");
            try {
              ytPlayerRef.current.loadVideoById({ videoId: currentTrack.id, startSeconds: currentTime || 0 });
              ytPlayerRef.current.playVideo();
            } catch { }
          }
        }}
      />

      {/* ========================================================================= */}
      {/* 1. TOP BAR (ThuongTube Header + Search + Khám phá + Account)              */}
      {/* ========================================================================= */}
      <header className="h-[54px] w-full px-3 md:px-5 flex items-center justify-between gap-2 md:gap-4 flex-shrink-0 z-30">
        {/* Left: Mobile Sidebar Toggle + ThuongTube Brand */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <button
            type="button"
            onClick={() => setIsMobileSidebarOpen((v) => !v)}
            className="lg:hidden p-1.5 rounded-lg bg-white/5 hover:bg-white/10 border border-white/10 text-white cursor-pointer"
            title="Mở thư viện"
          >
            <Menu className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => {
              setIsSearchOpen(false);
              setShowDiscovery(false);
            }}
            className="flex items-center gap-2 cursor-pointer group"
          >
            <img
              src="./app-logo.png"
              alt="ThuongTube"
              onError={(e) => { e.currentTarget.src = "./app.ico"; }}
              className="w-8 h-8 rounded-lg object-cover shadow-lg group-hover:scale-105 transition-transform"
            />
            <span className="font-extrabold text-sm md:text-base tracking-tight bg-gradient-to-r from-pink-400 to-purple-300 bg-clip-text text-transparent hidden sm:inline">
              ThuongTube
            </span>
          </button>
          <button
            type="button"
            onClick={() => {
              setShowDiscovery((v) => !v);
              setIsSearchOpen(false);
            }}
            title="Khám phá video & nhạc thịnh hành"
            className={`hidden md:flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border transition-all cursor-pointer ml-1 ${showDiscovery && !isSearchOpen
                ? "bg-pink-500/25 border-pink-500/60 text-pink-300"
                : "bg-white/5 border-white/10 text-white/75 hover:text-white hover:bg-white/10"
              }`}
          >
            <Compass className="w-3.5 h-3.5 text-pink-400" />
            <span>Khám phá</span>
          </button>
        </div>

        {/* Center: Search Pill with Search History Suggestions Dropdown */}
        <div ref={searchDropdownRef} className="relative flex-1 max-w-[560px]">
          <form
            onSubmit={handleTopSearchSubmit}
            className="w-full h-[36px] px-3 md:px-4 rounded-full bg-[#390f8d]/60 border border-purple-500/40 hover:border-pink-500/70 focus-within:border-pink-500 flex items-center gap-2 shadow-inner transition-all"
          >
            <Search className="w-4 h-4 text-pink-400 flex-shrink-0" />
            <input
              type="text"
              placeholder="Tìm video, bài hát hoặc dán link YouTube..."
              value={topSearchText}
              onFocus={() => setIsSearchFocused(true)}
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
                  setIsSearchFocused(false);
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

          {/* Quick Search History Dropdown Popover */}
          {isSearchFocused && searchHistory.length > 0 && (
            <div className="absolute top-[42px] left-0 right-0 rounded-2xl bg-[#160430]/95 border border-purple-500/50 backdrop-blur-xl shadow-2xl p-3 z-50 animate-fade-in flex flex-col gap-1.5">
              <div className="flex items-center justify-between px-2 pb-1.5 border-b border-white/10 text-[11px] font-semibold text-white/60">
                <span className="flex items-center gap-1.5 text-pink-300">
                  <Clock className="w-3.5 h-3.5 text-pink-400" />
                  Lịch sử tìm kiếm
                </span>
                <button
                  type="button"
                  onClick={clearSearchHistory}
                  className="text-white/40 hover:text-red-400 text-[10px] transition-colors cursor-pointer"
                >
                  Xóa tất cả
                </button>
              </div>
              <div className="max-h-[250px] overflow-y-auto flex flex-col gap-0.5 pt-1">
                {searchHistory.map((query, idx) => (
                  <div
                    key={idx}
                    onClick={() => handleHistoryItemClick(query)}
                    className="flex items-center justify-between px-2.5 py-1.5 rounded-xl hover:bg-white/10 text-xs text-white/90 hover:text-pink-300 transition-colors cursor-pointer group"
                  >
                    <div className="flex items-center gap-2 truncate flex-1 min-w-0 pr-2">
                      <Clock className="w-3.5 h-3.5 text-white/30 group-hover:text-pink-400 flex-shrink-0" />
                      <span className="truncate">{query}</span>
                    </div>
                    <button
                      type="button"
                      onClick={(e) => removeFromSearchHistory(query, e)}
                      title="Xóa mục này"
                      className="text-white/20 hover:text-white transition-colors p-1 flex-shrink-0"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Right: Switch to Kahoii Music, Download Apps & User Avatar Pill Buttons */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <button
            type="button"
            onClick={handleSwitchToKahoii}
            disabled={Boolean(currentTrack && !isCurrentTrackMusic)}
            title={
              currentTrack && !isCurrentTrackMusic
                ? "Không thể chuyển sang Kahoii Music vì video đang phát không phải là nhạc"
                : currentTrack
                ? `Nghe tiếp "${currentTrack.title}" bên Kahoii Music`
                : "Chuyển sang Kahoii Music (Nghe Nhạc Chuyên Dụng)"
            }
            className={`flex items-center gap-1.5 px-2.5 py-1.5 sm:px-3 sm:py-1.5 rounded-full border transition-all shadow-md ${
              currentTrack && !isCurrentTrackMusic
                ? "bg-white/5 border-white/10 text-white/35 opacity-45 cursor-not-allowed"
                : "bg-white/5 hover:bg-pink-500/25 border-white/15 hover:border-pink-500/50 text-white/90 hover:text-white cursor-pointer hover:scale-105"
            }`}
          >
            <Music
              className={`w-3.5 h-3.5 ${
                currentTrack && !isCurrentTrackMusic ? "text-white/40" : "text-pink-400"
              }`}
            />
            <span className="hidden sm:inline text-xs font-semibold">Kahoii Music</span>
          </button>

          {/* Download App Button & Dropdown */}
          <div className="relative" ref={downloadMenuRef}>
            <button
              type="button"
              onClick={() => setShowDownloadMenu((v) => !v)}
              title="Tải ứng dụng Kahoii Music (PC, Extension, Android, iOS)"
              className={`flex items-center gap-1.5 px-2.5 py-1.5 sm:px-3 sm:py-1.5 rounded-full transition-all cursor-pointer shadow-md border ${showDownloadMenu
                  ? "bg-pink-500/30 border-pink-500/60 text-white scale-105"
                  : "bg-white/5 hover:bg-pink-500/25 border-white/15 hover:border-pink-500/50 text-white/90 hover:text-white hover:scale-105"
                }`}
            >
              <Download className="w-3.5 h-3.5 text-pink-400" />
              <span className="hidden sm:inline text-xs font-semibold">Tải ứng dụng</span>
            </button>

            {showDownloadMenu && (
              <div className="absolute top-[44px] right-0 z-50 w-[310px] sm:w-[350px] bg-[#160430]/95 backdrop-blur-2xl rounded-2xl shadow-2xl p-3 border border-pink-500/40 flex flex-col gap-2">
                <div className="flex items-center justify-between px-1 pb-1.5 border-b border-white/10">
                  <div>
                    <p className="text-xs font-bold text-white flex items-center gap-1.5">
                      <Download className="w-3.5 h-3.5 text-pink-400" />
                      <span>Tải ứng dụng Kahoii Music</span>
                    </p>
                    <p className="text-[10px] text-white/50">Tự động lấy bản mới nhất từ GitHub</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowDownloadMenu(false)}
                    className="p-1 text-white/40 hover:text-white transition-colors cursor-pointer"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                </div>

                <div className="flex flex-col gap-1.5">
                  <a
                    href={downloadData.pc.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-blue-500/20 text-blue-400 flex items-center justify-center flex-shrink-0">
                        <Laptop className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white flex items-center gap-1.5 truncate">
                          <span>Windows PC</span>
                          <span className="text-[9px] px-1.5 rounded-full bg-blue-500/20 text-blue-300 font-mono">
                            {downloadData.pc.version}
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          {downloadData.pc.fileName} {downloadData.pc.size ? `• ${downloadData.pc.size}` : ""}
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:bg-pink-500 text-white/60 group-hover:text-white transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  <a
                    href={downloadData.extension.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-amber-500/20 text-amber-400 flex items-center justify-center flex-shrink-0">
                        <Puzzle className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white flex items-center gap-1.5 truncate">
                          <span>Extension</span>
                          <span className="text-[9px] px-1.5 rounded-full bg-amber-500/20 text-amber-300 font-mono">
                            {downloadData.extension.version}
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">Chrome, Edge, Cốc Cốc (.zip)</p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:bg-pink-500 text-white/60 group-hover:text-white transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  <a
                    href={downloadData.android.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center flex-shrink-0">
                        <Smartphone className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white flex items-center gap-1.5 truncate">
                          <span>Android</span>
                          <span className="text-[9px] px-1.5 rounded-full bg-emerald-500/20 text-emerald-300 font-mono">
                            APK
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          Cài trực tiếp {downloadData.android.size ? `• ${downloadData.android.size}` : ""}
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:bg-pink-500 text-white/60 group-hover:text-white transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  <a
                    href={downloadData.ios.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-purple-500/20 text-purple-400 flex items-center justify-center flex-shrink-0">
                        <Tablet className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white flex items-center gap-1.5 truncate">
                          <span>iOS (iPhone/iPad)</span>
                          <span className="text-[9px] px-1.5 rounded-full bg-purple-500/20 text-purple-300 font-mono">
                            IPA
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">Sideloadly / AltStore / TrollStore</p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:bg-pink-500 text-white/60 group-hover:text-white transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>
                </div>

                <div className="pt-1.5 border-t border-white/10 flex items-center justify-between text-[10px] text-white/40 px-1">
                  <span className="flex items-center gap-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                    Tự động lấy bản mới nhất
                  </span>
                  <a
                    href="https://github.com/Iiwa-San/KahoiiMusic/releases"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-pink-400 hover:underline flex items-center gap-0.5"
                  >
                    <span>Releases</span>
                    <ExternalLink className="w-2.5 h-2.5" />
                  </a>
                </div>
              </div>
            )}
          </div>

          {userSession?.isLoggedIn ? (
            <button
              onClick={() => setIsAccountOpen(true)}
              title={`Tài khoản: ${userSession.user?.name || "Kahoii User"}`}
              className="flex items-center gap-2 pl-1 pr-3 py-1 rounded-full bg-[#f1f1f5] hover:bg-white text-black font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 group"
            >
              <img
                src={`${API_BASE}/api/user/avatar?t=${avatarKey}`}
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
      <main className="flex-1 w-full px-3 md:px-5 pb-3 md:pb-5 pt-1 flex flex-col lg:flex-row gap-3 md:gap-4 md:overflow-hidden min-h-0 relative">
        {/* Mobile Sidebar Backdrop */}
        {isMobileSidebarOpen && (
          <div
            onClick={() => setIsMobileSidebarOpen(false)}
            className="fixed inset-0 bg-black/70 backdrop-blur-sm z-40 lg:hidden"
          />
        )}

        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 1 (LEFT): Sidebar / Library Panel (Responsive Drawer on Mobile)  */}
        {/* ----------------------------------------------------------------------- */}
        <aside
          className={`${isMobileSidebarOpen
              ? "fixed inset-y-3 left-3 w-[84vw] max-w-[320px] z-50 flex"
              : "hidden lg:flex lg:w-[290px] xl:w-[320px]"
            } flex-shrink-0 figma-panel flex-col min-h-0 overflow-hidden`}
          style={{ padding: "24px 16px 18px 16px" }}
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
                className={`relative z-20 py-2 px-1 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer truncate ${activeLeftTab === tab.key ? "text-white" : "text-white/70 hover:text-white"
                  }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Search Box in Sidebar + Create Playlist Button */}
          <div className="flex items-center gap-2 flex-shrink-0" style={{ marginBottom: "14px" }}>
            <div className="relative flex-1 flex items-center h-9 px-3 rounded-full bg-[#1e053a] border border-purple-500/30 hover:border-pink-500/50 focus-within:border-pink-500 transition-all">
              <Search className="w-3.5 h-3.5 text-[#e0007b] flex-shrink-0" />
              <input
                type="text"
                placeholder="Tìm trong danh sách..."
                value={leftSearchQuery}
                onChange={(e) => setLeftSearchQuery(e.target.value)}
                className="flex-1 bg-transparent text-xs font-medium text-white placeholder-white/50 focus:outline-none px-2 min-w-0"
              />
              <ArrowDownAZ className="w-3.5 h-3.5 text-[#e0007b] flex-shrink-0" />
            </div>
            {activeLeftTab === "library" && (
              <button
                type="button"
                onClick={() => {
                  setSelectedPlaylistId(null);
                  setIsCreatePlaylistOpen((v) => !v);
                }}
                title="Tạo danh sách phát mới"
                className="h-9 px-2.5 rounded-full bg-pink-500/20 hover:bg-pink-500 border border-pink-500/40 text-pink-300 hover:text-white text-xs font-bold flex items-center gap-1 transition-colors cursor-pointer flex-shrink-0"
              >
                <FolderPlus className="w-3.5 h-3.5" />
                <span className="hidden sm:inline">Tạo</span>
              </button>
            )}
          </div>

          {/* Inline Create Playlist Form */}
          {activeLeftTab === "library" && isCreatePlaylistOpen && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleCreatePlaylist(newPlaylistName);
              }}
              className="flex items-center gap-1.5 mb-3 p-2 rounded-xl bg-black/30 border border-pink-500/40 flex-shrink-0"
            >
              <input
                type="text"
                placeholder="Tên danh sách phát..."
                value={newPlaylistName}
                onChange={(e) => setNewPlaylistName(e.target.value)}
                autoFocus
                className="flex-1 bg-transparent text-xs text-white placeholder-white/40 focus:outline-none px-1.5 min-w-0"
              />
              <button
                type="submit"
                className="px-2.5 py-1 rounded-lg bg-pink-500 hover:bg-pink-400 text-white text-[11px] font-bold cursor-pointer"
              >
                Lưu
              </button>
            </form>
          )}

          {/* Selected Playlist Detail View */}
          {activeLeftTab === "library" && selectedPlaylistId ? (
            (() => {
              const activePl = libraryData.playlists.find((p) => p.id === selectedPlaylistId);
              const plItems: Track[] = activePl?.items || [];
              return (
                <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
                  <div className="flex items-center justify-between pb-2 mb-2 border-b border-white/10 flex-shrink-0">
                    <button
                      type="button"
                      onClick={() => setSelectedPlaylistId(null)}
                      className="text-xs text-pink-300 hover:text-white flex items-center gap-1 cursor-pointer font-semibold"
                    >
                      <ArrowLeft className="w-3.5 h-3.5" />
                      <span className="truncate max-w-[130px]">{activePl?.name || "Danh sách"}</span>
                    </button>
                    {plItems.length > 0 && (
                      <button
                        type="button"
                        onClick={() => {
                          setPlaylist(plItems);
                          setQueueTracks(plItems);
                          setCurrentIndex(0);
                          loadAndPlayTrack(plItems[0], true);
                        }}
                        className="px-2 py-1 rounded-lg bg-pink-500/20 hover:bg-pink-500 text-pink-300 hover:text-white text-[10px] font-bold cursor-pointer flex items-center gap-1"
                      >
                        <Play className="w-3 h-3 fill-current" />
                        <span>Phát tất cả</span>
                      </button>
                    )}
                  </div>
                  <div className="flex-1 overflow-y-auto flex flex-col gap-2 min-h-0 pr-1">
                    {plItems.length > 0 ? (
                      plItems.map((tr, i) => (
                        <div
                          key={`${tr.id}-${i}`}
                          onClick={() => playTrackDirectly(tr)}
                          className="flex items-center gap-2.5 p-1.5 rounded-xl hover:bg-white/10 cursor-pointer group"
                        >
                          <img
                            src={tr.thumbnail || `https://i.ytimg.com/vi/${tr.id}/mqdefault.jpg`}
                            alt=""
                            onError={(e) => { e.currentTarget.src = DEFAULT_TRACK_ART; }}
                            className="w-9 h-9 rounded-lg object-cover flex-shrink-0"
                          />
                          <div className="flex-1 min-w-0">
                            <p className="text-xs font-semibold text-white truncate">{tr.title}</p>
                            <p className="text-[10px] text-white/50 truncate">{tr.author}</p>
                          </div>
                          <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                            <button
                              type="button"
                              onClick={(e) => handleAddToQueue(tr, e)}
                              title="Thêm vào hàng chờ"
                              className="p-1 rounded-md hover:bg-pink-500/30 text-white/70 hover:text-pink-300 cursor-pointer"
                            >
                              <ListEnd className="w-3.5 h-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={(e) => handleRemoveTrackFromPlaylist(selectedPlaylistId, tr.id, e)}
                              title="Xóa khỏi danh sách"
                              className="p-1 rounded-md hover:bg-red-500/30 text-white/70 hover:text-red-400 cursor-pointer"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="py-12 text-center text-white/40 text-xs">
                        Danh sách trống. Bấm biểu tượng + trên video bất kỳ để thêm vào đây!
                      </div>
                    )}
                  </div>
                </div>
              );
            })()
          ) : (
            /* Vertical List of Playlist / Favorites / History Items */
            <div
              className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1"
              style={{ gap: "10px" }}
            >
              {getSidebarItems().length > 0 ? (
                getSidebarItems().map((item, idx) => (
                  <div
                    key={item.id + idx}
                    onClick={() => {
                      if (item.rawTrack) {
                        playTrackDirectly(item.rawTrack);
                      } else if (item.rawPlaylist) {
                        setSelectedPlaylistId(item.rawPlaylist.id);
                      } else if (playlist[idx % playlist.length]) {
                        playTrackDirectly(playlist[idx % playlist.length]);
                      }
                    }}
                    className="flex items-center gap-3 p-2 rounded-2xl hover:bg-white/10 transition-all duration-200 cursor-pointer group"
                  >
                    <img
                      src={item.thumb}
                      alt=""
                      onError={(e) => {
                        e.currentTarget.src = DEFAULT_TRACK_ART;
                      }}
                      className="w-10 h-10 rounded-[12px] object-cover flex-shrink-0 border border-white/10 shadow-sm"
                    />
                    <div className="flex-1 min-w-0">
                      <p className="text-[13px] font-semibold text-white group-hover:text-pink-300 truncate transition-colors">
                        {item.title}
                      </p>
                      <p className="text-[11px] text-purple-200/60 truncate mt-0.5">
                        {item.subtitle}
                      </p>
                    </div>
                    {item.rawTrack ? (
                      <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0">
                        <button
                          type="button"
                          onClick={(e) => handleAddToQueue(item.rawTrack, e)}
                          title="Thêm vào hàng chờ"
                          className="p-1 rounded-md hover:bg-pink-500/30 text-white/70 hover:text-pink-300 cursor-pointer"
                        >
                          <ListEnd className="w-3.5 h-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setPlaylistTargetTrack(item.rawTrack);
                          }}
                          title="Thêm vào danh sách phát"
                          className="p-1 rounded-md hover:bg-purple-500/30 text-white/70 hover:text-purple-300 cursor-pointer"
                        >
                          <ListPlus className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    ) : item.rawPlaylist ? (
                      <button
                        type="button"
                        onClick={(e) => handleDeletePlaylist(item.rawPlaylist.id, e)}
                        title="Xóa danh sách phát"
                        className="p-1 rounded-md opacity-0 group-hover:opacity-100 hover:bg-red-500/30 text-white/60 hover:text-red-400 transition-all cursor-pointer flex-shrink-0"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    ) : null}
                  </div>
                ))
              ) : (
                <div className="flex flex-col items-center justify-center py-12 text-center text-white/40 text-xs px-2 gap-2">
                  <p>
                    {activeLeftTab === "library"
                      ? "Chưa có danh sách phát nào"
                      : activeLeftTab === "favorites"
                        ? "Chưa có bài hát yêu thích nào"
                        : "Chưa có lịch sử phát nhạc"}
                  </p>
                  {activeLeftTab === "library" && (
                    <button
                      type="button"
                      onClick={() => setIsCreatePlaylistOpen(true)}
                      className="px-3 py-1.5 rounded-full bg-pink-500/20 hover:bg-pink-500 text-pink-300 hover:text-white text-xs font-semibold cursor-pointer transition-colors"
                    >
                      + Tạo danh sách phát đầu tiên
                    </button>
                  )}
                </div>
              )}
            </div>
          )}
        </aside>

        {/* ----------------------------------------------------------------------- */}
        {/* RIGHT MAIN AREA: Video Stage + Compact Queue (or Search/Discovery)      */}
        {/* ----------------------------------------------------------------------- */}
        <div className="flex-1 flex flex-col gap-3 min-h-0 overflow-hidden">
          {/* Search / Discovery View (shown when searching or clicking Khám phá) */}
          <section
            className={
              isSearchOpen || showDiscovery
                ? "flex-1 h-full figma-panel flex flex-col justify-between min-h-0 overflow-y-auto"
                : "hidden"
            }
            style={{ padding: "20px 24px" }}
          >
            {isSearchOpen ? (
              /* ===================================================================== */
              /* SEARCH VIEW (Dynamically transforms Center Canvas when searching)     */
              /* ===================================================================== */
              <div className="flex flex-col h-full min-h-0 overflow-y-auto pr-1 custom-scrollbar">
                {/* Header with Back button, search title and result count */}
                <div className="flex items-center justify-between pb-3.5 border-b border-white/10 mb-4 flex-shrink-0">
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      onClick={() => {
                        setIsSearchOpen(false);
                        setTopSearchText("");
                        setSearchQueryTitle("");
                        setActiveSearchTab("all");
                      }}
                      className="px-3 py-1.5 rounded-xl bg-white/5 hover:bg-pink-500/20 text-white/80 hover:text-white border border-white/10 transition-all cursor-pointer flex items-center gap-2 text-xs font-semibold group"
                    >
                      <ArrowLeft className="w-4 h-4 text-pink-400 group-hover:-translate-x-0.5 transition-transform" />
                      <span>Quay lại Khám phá</span>
                    </button>
                    <h2 className="text-[20px] font-bold text-white tracking-wide truncate">
                      {searchQueryTitle ? `Kết quả: "${searchQueryTitle}"` : "Kết quả tìm kiếm"}
                    </h2>
                  </div>
                  {searchResults.length > 0 && (
                    <span className="text-xs text-purple-200/60 font-mono">
                      {searchResults.length} kết quả
                    </span>
                  )}
                </div>

                {/* Navigation Tabs: Tất cả / Video / Nhạc / Kênh / Playlist */}
                <div className="flex items-center gap-2 mb-5 flex-wrap flex-shrink-0">
                  {[
                    { key: "all", label: "Tất cả" },
                    {
                      key: "videos",
                      label: "Video",
                      count: searchStructured?.videos?.length || 0,
                    },
                    {
                      key: "tracks",
                      label: "Nhạc",
                      count: searchStructured?.tracks?.length || 0,
                    },
                    {
                      key: "artists",
                      label: "Kênh / Nghệ sĩ",
                      count: searchStructured?.artists?.length || 0,
                    },
                    {
                      key: "playlists",
                      label: "Playlist",
                      count: searchStructured?.playlists?.length || 0,
                    },
                  ].map((tab) => {
                    const isActive = activeSearchTab === tab.key;
                    return (
                      <button
                        key={tab.key}
                        type="button"
                        onClick={() =>
                          setActiveSearchTab(tab.key as "all" | "videos" | "tracks" | "artists" | "playlists")
                        }
                        className={`px-4 py-1.5 rounded-full text-xs font-semibold transition-all flex items-center gap-2 cursor-pointer ${isActive
                            ? "bg-gradient-to-r from-pink-500 to-purple-600 text-white shadow-lg shadow-pink-500/25 scale-[1.02]"
                            : "bg-white/5 hover:bg-white/10 text-white/70 hover:text-white border border-white/5"
                          }`}
                      >
                        <span>{tab.label}</span>
                        {typeof tab.count === "number" && tab.count > 0 && (
                          <span
                            className={`px-1.5 py-0.2 rounded-full text-[10px] font-mono ${isActive ? "bg-white/20 text-white" : "bg-white/10 text-white/60"
                              }`}
                          >
                            {tab.count}
                          </span>
                        )}
                      </button>
                    );
                  })}
                </div>

                {/* Body: Loading / Results Multi-tier Grid / Empty state */}
                {isSearching ? (
                  <div className="flex-1 flex flex-col items-center justify-center py-24 text-center gap-3">
                    <Loader2 className="w-8 h-8 animate-spin text-pink-400" />
                    <p className="text-sm font-medium text-purple-200/80">Đang tải dữ liệu...</p>
                  </div>
                ) : searchResults.length > 0 || (searchStructured && (searchStructured.artists.length > 0 || searchStructured.playlists.length > 0)) ? (
                  <div className="flex flex-col gap-6 pb-8">
                    {/* ========================================================================= */}
                    {/* TAB: TẤT CẢ                                                               */}
                    {/* ========================================================================= */}
                    {activeSearchTab === "all" && (
                      <>
                        {/* TẦNG 1: Hero Top Match Card (Kết quả khớp nhất / Bài hát phù hợp nhất) */}
                        {(() => {
                          const top =
                            searchStructured?.topResult ||
                            (searchResults.length > 0
                              ? {
                                id: searchResults[0].id,
                                title: searchResults[0].title,
                                author: searchResults[0].author,
                                subtitle: `${searchResults[0].author}${searchResults[0].duration > 0
                                    ? " • " + formatTime(searchResults[0].duration)
                                    : ""
                                  }`,
                                duration: searchResults[0].duration,
                                durationText:
                                  searchResults[0].duration > 0
                                    ? formatTime(searchResults[0].duration)
                                    : "",
                                thumbnail: searchResults[0].thumbnail,
                                type: "song" as const,
                              }
                              : null);

                          if (!top) return null;
                          const isCurrentPlaying = currentTrack?.id === top.id;

                          return (
                            <div className="w-full sticky top-0 z-30 pt-0.5 pb-1">
                              <div
                                onClick={() => {
                                  if (isCurrentPlaying) {
                                    togglePlayPause();
                                  } else {
                                    playSearchResult({
                                      id: top.id,
                                      title: top.title,
                                      author: top.author,
                                      duration: top.duration,
                                      thumbnail: top.thumbnail,
                                    });
                                  }
                                }}
                                className="w-full rounded-[20px] p-3.5 bg-gradient-to-r from-[#2f1647]/95 via-[#1e1335]/95 to-[#140d24]/95 backdrop-blur-xl border border-pink-500/45 hover:border-pink-500/75 shadow-2xl shadow-black/50 transition-all flex items-center justify-between gap-4 group cursor-pointer"
                              >
                                <div className="flex items-center gap-4 min-w-0">
                                  {/* Large Artwork */}
                                  <div className="relative w-28 h-20 sm:w-36 sm:h-24 rounded-xl overflow-hidden shadow-md flex-shrink-0 border border-white/10 bg-purple-950">
                                    <img
                                      src={top.thumbnail || DEFAULT_TRACK_ART}
                                      alt={top.title}
                                      onError={(e) => {
                                        e.currentTarget.src = DEFAULT_TRACK_ART;
                                      }}
                                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                    />
                                    {top.duration > 0 && (
                                      <span className="absolute bottom-1 right-1 px-1.5 py-0.5 rounded bg-black/80 text-[10px] font-mono text-white/90">
                                        {top.durationText || formatTime(top.duration)}
                                      </span>
                                    )}
                                  </div>

                                  {/* Track Info */}
                                  <div className="min-w-0 flex flex-col justify-center">
                                    <span className="text-[10px] uppercase font-bold tracking-widest text-pink-400 mb-1 flex items-center gap-1.5">
                                      <Sparkles className="w-3 h-3 text-pink-400" />
                                      BÀI HÁT PHÙ HỢP NHẤT
                                    </span>
                                    <h3 className="text-base sm:text-lg font-bold text-white tracking-wide truncate group-hover:text-pink-300 transition-colors">
                                      {top.title}
                                    </h3>
                                    <p className="text-xs text-purple-200/70 truncate mt-1">
                                      {top.author} •{" "}
                                      {top.durationText ||
                                        (top.duration > 0
                                          ? formatTime(top.duration)
                                          : "YouTube Music")}
                                    </p>
                                  </div>
                                </div>

                                {/* Play Action Button */}
                                <div className="flex items-center gap-3 flex-shrink-0">
                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      if (isCurrentPlaying) {
                                        togglePlayPause();
                                      } else {
                                        playSearchResult({
                                          id: top.id,
                                          title: top.title,
                                          author: top.author,
                                          duration: top.duration,
                                          thumbnail: top.thumbnail,
                                        });
                                      }
                                    }}
                                    className="px-5 py-2.5 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 hover:from-pink-600 hover:to-purple-700 text-white font-bold text-xs sm:text-sm flex items-center gap-2 shadow-lg shadow-pink-500/30 hover:scale-105 active:scale-95 transition-all cursor-pointer"
                                  >
                                    {isCurrentPlaying && isPlaying ? (
                                      <>
                                        <Pause className="w-4 h-4 fill-current" />
                                        <span>Tạm dừng</span>
                                      </>
                                    ) : (
                                      <>
                                        <Play className="w-4 h-4 fill-current translate-x-0.5" />
                                        <span>Phát ngay</span>
                                      </>
                                    )}
                                  </button>
                                </div>
                              </div>
                            </div>
                          );
                        })()}

                        {/* TẦNG 2: Nhạc sĩ (Row of Artists - Enlarged Items) */}
                        {searchStructured?.artists && searchStructured.artists.length > 0 && (
                          <div>
                            <div className="flex items-center justify-between mb-3">
                              <h3
                                onClick={() => setActiveSearchTab("artists")}
                                className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:text-pink-300 transition-colors"
                              >
                                <span>Nhạc sĩ</span>
                              </h3>
                              <button
                                type="button"
                                onClick={() => setActiveSearchTab("artists")}
                                className="text-xs text-pink-400 hover:text-pink-300 font-medium transition-colors cursor-pointer"
                              >
                                Xem tất cả ({searchStructured.artists.length})
                              </button>
                            </div>
                            <div className="flex items-start gap-7 overflow-x-auto pb-2 custom-scrollbar">
                              {searchStructured.artists.map((artist) => (
                                <div
                                  key={artist.id || artist.name}
                                  onClick={() => executeSearch(artist.name)}
                                  className="flex flex-col items-center p-3 rounded-2xl hover:bg-white/5 transition-all cursor-pointer group flex-shrink-0"
                                  style={{ width: "170px" }}
                                >
                                  <div className="w-28 h-28 sm:w-32 sm:h-32 rounded-full overflow-hidden border-[3px] border-pink-500/30 group-hover:border-pink-500 group-hover:shadow-[0_0_22px_rgba(236,72,153,0.45)] group-hover:scale-105 transition-all duration-300 shadow-xl bg-purple-950/60 mb-2.5 relative">
                                    <img
                                      src={artist.thumbnail || DEFAULT_TRACK_ART}
                                      alt={artist.name}
                                      onError={(e) => {
                                        e.currentTarget.src = DEFAULT_TRACK_ART;
                                      }}
                                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                    />
                                    <div className="absolute inset-0 bg-black/35 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                                      <div className="w-9 h-9 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg">
                                        <Search className="w-4 h-4 text-white" />
                                      </div>
                                    </div>
                                  </div>
                                  <p className="text-sm font-bold text-white group-hover:text-pink-300 text-center truncate w-full">
                                    {artist.name}
                                  </p>
                                  <p className="text-[11px] text-purple-200/60 text-center truncate w-full mt-0.5">
                                    {artist.subscribers || "Nghệ sĩ"}
                                  </p>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}

                        {/* TẦNG 3: Playlist (Row of Playlists) */}
                        {searchStructured?.playlists && searchStructured.playlists.length > 0 && (
                          <div>
                            <div className="flex items-center justify-between mb-3">
                              <h3
                                onClick={() => setActiveSearchTab("playlists")}
                                className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:text-pink-300 transition-colors"
                              >
                                <span>Playlist</span>
                              </h3>
                              <button
                                type="button"
                                onClick={() => setActiveSearchTab("playlists")}
                                className="text-xs text-pink-400 hover:text-pink-300 font-medium transition-colors cursor-pointer"
                              >
                                Xem tất cả ({searchStructured.playlists.length})
                              </button>
                            </div>
                            <div className="grid grid-cols-6 gap-4">
                              {searchStructured.playlists.slice(0, 6).map((pl) => (
                                <div
                                  key={pl.id || pl.title}
                                  onClick={() => playPlaylistItem(pl)}
                                  className="flex flex-col cursor-pointer group"
                                >
                                  <div className="w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border border-white/10 bg-purple-900/30">
                                    <img
                                      src={pl.thumbnail || DEFAULT_TRACK_ART}
                                      alt={pl.title}
                                      onError={(e) => {
                                        e.currentTarget.src = DEFAULT_TRACK_ART;
                                      }}
                                      className="w-full h-full object-cover"
                                    />
                                    <div className="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                                      <button
                                        type="button"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          playPlaylistItem(pl);
                                        }}
                                        className="w-11 h-11 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg hover:scale-110 active:scale-95 transition-transform cursor-pointer"
                                        title="Phát danh sách này"
                                      >
                                        <Play className="w-5 h-5 text-white fill-current translate-x-0.5" />
                                      </button>
                                    </div>
                                    <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                      Playlist
                                    </span>
                                  </div>
                                  <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-1.5">
                                    {pl.title}
                                  </p>
                                  <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                    {pl.author || "YouTube Music"}
                                  </p>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}

                        {/* TẦNG 4: Bài hát (Songs Grid) */}
                        {searchResults.length > 0 && (
                          <div>
                            <div className="flex items-center justify-between mb-3">
                              <h3
                                onClick={() => setActiveSearchTab("tracks")}
                                className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:text-pink-300 transition-colors"
                              >
                                <span>Bài hát</span>
                              </h3>
                              <button
                                type="button"
                                onClick={() => setActiveSearchTab("tracks")}
                                className="text-xs text-pink-400 hover:text-pink-300 font-medium transition-colors cursor-pointer"
                              >
                                Xem tất cả ({searchResults.length})
                              </button>
                            </div>
                            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 w-full" style={{ gap: "16px" }}>
                              {searchResults.slice(0, 12).map((item) => {
                                const isCurrentPlaying = currentTrack?.id === item.id;
                                return (
                                  <div
                                    key={item.id}
                                    onClick={() => {
                                      if (isCurrentPlaying) {
                                        togglePlayPause();
                                      } else {
                                        playSearchResult(item);
                                      }
                                    }}
                                    className="w-full flex flex-col cursor-pointer group"
                                  >
                                    <div
                                      className={`w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${isCurrentPlaying
                                          ? "border-pink-500 shadow-pink-500/30"
                                          : "border-white/5 bg-purple-900/30"
                                        }`}
                                    >
                                      <img
                                        src={
                                          item.thumbnail ||
                                          `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`
                                        }
                                        alt={item.title}
                                        onError={(e) => {
                                          e.currentTarget.src = DEFAULT_TRACK_ART;
                                        }}
                                        className="w-full h-full object-cover"
                                      />
                                      <div className="absolute top-1.5 right-1.5 flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity z-20">
                                        <button
                                          type="button"
                                          onClick={(e) => handleAddToQueue(item, e)}
                                          title="Thêm vào hàng chờ"
                                          className="p-1.5 rounded-full bg-black/75 hover:bg-pink-500 text-white cursor-pointer transition-colors"
                                        >
                                          <ListEnd className="w-3.5 h-3.5" />
                                        </button>
                                        <button
                                          type="button"
                                          onClick={(e) => {
                                            e.stopPropagation();
                                            setPlaylistTargetTrack(item);
                                          }}
                                          title="Thêm vào danh sách phát"
                                          className="p-1.5 rounded-full bg-black/75 hover:bg-purple-500 text-white cursor-pointer transition-colors"
                                        >
                                          <ListPlus className="w-3.5 h-3.5" />
                                        </button>
                                      </div>
                                      <div
                                        className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity duration-200 ${isCurrentPlaying
                                            ? "opacity-100"
                                            : "opacity-0 group-hover:opacity-100"
                                          }`}
                                      >
                                        <button
                                          type="button"
                                          onClick={(e) => {
                                            e.stopPropagation();
                                            if (isCurrentPlaying) {
                                              togglePlayPause();
                                            } else {
                                              playSearchResult(item);
                                            }
                                          }}
                                          className="w-10 h-10 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg hover:scale-110 active:scale-95 transition-transform cursor-pointer"
                                        >
                                          {isCurrentPlaying && isPlaying ? (
                                            <Pause className="w-5 h-5 text-white fill-current" />
                                          ) : (
                                            <Play className="w-5 h-5 text-white fill-current translate-x-0.5" />
                                          )}
                                        </button>
                                      </div>
                                      {item.duration > 0 && (
                                        <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                          {formatTime(item.duration)}
                                        </span>
                                      )}
                                    </div>
                                    <p
                                      className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${isCurrentPlaying
                                          ? "text-pink-300 font-bold"
                                          : "text-white/90 group-hover:text-pink-300"
                                        }`}
                                    >
                                      {item.title}
                                    </p>
                                    <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                      {item.author || "Kahoii Music"}
                                    </p>
                                  </div>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </>
                    )}

                    {/* ========================================================================= */}
                    {/* TAB: NHẠC (Tất cả bài hát)                                                */}
                    {/* ========================================================================= */}
                    {(activeSearchTab === "videos" || activeSearchTab === "tracks") && (() => {
                      const itemsToRender =
                        activeSearchTab === "videos"
                          ? searchStructured?.videos || searchResults
                          : searchStructured?.tracks || searchResults;
                      return (
                        <div>
                          <div className="flex items-center justify-between mb-4">
                            <h3 className="text-[18px] font-bold text-white tracking-wide">
                              {activeSearchTab === "videos"
                                ? `Tất cả Video (${itemsToRender.length})`
                                : `Tất cả bài hát (${itemsToRender.length})`}
                            </h3>
                          </div>
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 w-full" style={{ gap: "16px" }}>
                            {itemsToRender.map((item) => {
                              const isCurrentPlaying = currentTrack?.id === item.id;
                              return (
                                <div
                                  key={item.id}
                                  onClick={() => {
                                    if (isCurrentPlaying) {
                                      togglePlayPause();
                                    } else {
                                      playSearchResult(item);
                                    }
                                  }}
                                  className="w-full flex flex-col cursor-pointer group"
                                >
                                  <div
                                    className={`w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${isCurrentPlaying
                                        ? "border-pink-500 shadow-pink-500/30"
                                        : "border-white/5 bg-purple-900/30"
                                      }`}
                                  >
                                    <img
                                      src={
                                        item.thumbnail ||
                                        `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`
                                      }
                                      alt={item.title}
                                      onError={(e) => {
                                        e.currentTarget.src = DEFAULT_TRACK_ART;
                                      }}
                                      className="w-full h-full object-cover"
                                    />
                                    <div
                                      className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity duration-200 ${isCurrentPlaying
                                          ? "opacity-100"
                                          : "opacity-0 group-hover:opacity-100"
                                        }`}
                                    >
                                      <button
                                        type="button"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          if (isCurrentPlaying) {
                                            togglePlayPause();
                                          } else {
                                            playSearchResult(item);
                                          }
                                        }}
                                        className="w-10 h-10 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg hover:scale-110 active:scale-95 transition-transform cursor-pointer"
                                      >
                                        {isCurrentPlaying && isPlaying ? (
                                          <Pause className="w-5 h-5 text-white fill-current" />
                                        ) : (
                                          <Play className="w-5 h-5 text-white fill-current translate-x-0.5" />
                                        )}
                                      </button>
                                    </div>
                                    {item.duration > 0 && (
                                      <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                        {formatTime(item.duration)}
                                      </span>
                                    )}
                                  </div>
                                  <p
                                    className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${isCurrentPlaying
                                        ? "text-pink-300 font-bold"
                                        : "text-white/90 group-hover:text-pink-300"
                                      }`}
                                  >
                                    {item.title}
                                  </p>
                                  <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                    {item.author || "Kahoii Music"}
                                  </p>
                                </div>
                              );
                            })}
                          </div>
                        </div>
                      );
                    })()}

                    {/* ========================================================================= */}
                    {/* TAB: NHẠC SĨ (Tất cả nghệ sĩ - Enlarged Grid)                             */}
                    {/* ========================================================================= */}
                    {activeSearchTab === "artists" && (
                      <div>
                        <div className="flex items-center justify-between mb-4">
                          <h3 className="text-[18px] font-bold text-white tracking-wide">
                            Tất cả nhạc sĩ ({searchStructured?.artists?.length || 0})
                          </h3>
                        </div>
                        {searchStructured?.artists && searchStructured.artists.length > 0 ? (
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-6">
                            {searchStructured.artists.map((artist) => (
                              <div
                                key={artist.id || artist.name}
                                onClick={() => executeSearch(artist.name)}
                                className="flex flex-col items-center p-4 rounded-2xl hover:bg-white/5 transition-all cursor-pointer group"
                              >
                                <div className="w-32 h-32 sm:w-36 sm:h-36 rounded-full overflow-hidden border-[3px] border-pink-500/30 group-hover:border-pink-500 group-hover:shadow-[0_0_25px_rgba(236,72,153,0.45)] group-hover:scale-105 transition-all duration-300 shadow-xl bg-purple-950/60 mb-3 relative">
                                  <img
                                    src={artist.thumbnail || DEFAULT_TRACK_ART}
                                    alt={artist.name}
                                    onError={(e) => {
                                      e.currentTarget.src = DEFAULT_TRACK_ART;
                                    }}
                                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                  />
                                  <div className="absolute inset-0 bg-black/35 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                                    <div className="w-10 h-10 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg">
                                      <Search className="w-5 h-5 text-white" />
                                    </div>
                                  </div>
                                </div>
                                <p className="text-base font-bold text-white group-hover:text-pink-300 text-center truncate w-full">
                                  {artist.name}
                                </p>
                                <p className="text-xs text-purple-200/60 text-center truncate w-full mt-1">
                                  {artist.subscribers || "Nghệ sĩ"}
                                </p>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <p className="text-sm text-white/50 italic py-10 text-center">
                            Không tìm thấy thông tin nhạc sĩ phù hợp.
                          </p>
                        )}
                      </div>
                    )}

                    {/* ========================================================================= */}
                    {/* TAB: PLAYLIST (Tất cả danh sách phát)                                      */}
                    {/* ========================================================================= */}
                    {activeSearchTab === "playlists" && (
                      <div>
                        <div className="flex items-center justify-between mb-4">
                          <h3 className="text-[18px] font-bold text-white tracking-wide">
                            Tất cả danh sách phát ({searchStructured?.playlists?.length || 0})
                          </h3>
                        </div>
                        {searchStructured?.playlists && searchStructured.playlists.length > 0 ? (
                          <div className="grid grid-cols-6 gap-4">
                            {searchStructured.playlists.map((pl) => (
                              <div
                                key={pl.id || pl.title}
                                onClick={() => playPlaylistItem(pl)}
                                className="flex flex-col cursor-pointer group"
                              >
                                <div className="w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border border-white/10 bg-purple-900/30">
                                  <img
                                    src={pl.thumbnail || DEFAULT_TRACK_ART}
                                    alt={pl.title}
                                    onError={(e) => {
                                      e.currentTarget.src = DEFAULT_TRACK_ART;
                                    }}
                                    className="w-full h-full object-cover"
                                  />
                                  <div className="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                                    <button
                                      type="button"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        playPlaylistItem(pl);
                                      }}
                                      className="w-11 h-11 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center shadow-lg hover:scale-110 active:scale-95 transition-transform cursor-pointer"
                                      title="Phát danh sách này"
                                    >
                                      <Play className="w-5 h-5 text-white fill-current translate-x-0.5" />
                                    </button>
                                  </div>
                                  <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                    Playlist
                                  </span>
                                </div>
                                <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-1.5">
                                  {pl.title}
                                </p>
                                <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                  {pl.author || "YouTube Music"}
                                </p>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <p className="text-sm text-white/50 italic py-10 text-center">
                            Không tìm thấy danh sách phát phù hợp.
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="flex-1 flex flex-col items-center justify-center py-20 text-center gap-3">
                    <p className="text-sm text-white/50 italic">Không tìm thấy dữ liệu phù hợp.</p>
                    <button
                      type="button"
                      onClick={() => {
                        setIsSearchOpen(false);
                        setTopSearchText("");
                        setSearchQueryTitle("");
                        setActiveSearchTab("all");
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
              /* DISCOVERY VIEW (Nhạc Trending, Mới Phát Hành, Nhạc Sĩ Nổi Bật, ...)   */
              /* ===================================================================== */
              <div className="flex flex-col gap-8 min-h-0 overflow-y-auto pr-1 custom-scrollbar pb-6">
                {/* Sticky Audio-Only Mini-Player Card while browsing Discovery */}
                <div className="sticky top-0 z-30 flex flex-col gap-2 p-3 rounded-2xl bg-[#1a0836]/95 backdrop-blur-xl border border-pink-500/45 shadow-2xl shadow-black/50">
                  <div className="flex items-center justify-between gap-3 flex-wrap">
                    <div className="flex items-center gap-2.5 min-w-0 flex-1">
                      {currentTrack ? (
                        <>
                          <img
                            src={currentTrack.thumbnail || `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg`}
                            alt=""
                            onError={(e) => { e.currentTarget.src = DEFAULT_TRACK_ART; }}
                            className="w-11 h-11 rounded-xl object-cover flex-shrink-0 border border-pink-500/40 shadow"
                          />
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-1.5">
                              <span className="px-1.5 py-0.5 rounded bg-pink-500/20 text-pink-300 text-[9px] font-bold uppercase tracking-wider flex-shrink-0">
                                {playerMode === "audio" ? "Chỉ âm thanh" : "Đang phát"}
                              </span>
                              <p className="text-xs md:text-sm font-bold text-white truncate">{currentTrack.title}</p>
                            </div>
                            <p className="text-[11px] text-white/60 truncate mt-0.5">
                              {currentTrack.author} • {formatTime(currentTime)} / {formatTime(duration)}
                            </p>
                          </div>
                        </>
                      ) : (
                        <div className="text-xs text-white/70 font-semibold">
                          Chọn bài hát bất kỳ bên dưới để nghe nhạc khi lướt Khám phá
                        </div>
                      )}
                    </div>

                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      {currentTrack && (
                        <>
                          <button
                            type="button"
                            onClick={handlePrev}
                            title="Bài trước"
                            className="p-2 rounded-lg bg-white/5 hover:bg-white/15 text-white/80 hover:text-pink-300 cursor-pointer transition-colors"
                          >
                            <SkipBack className="w-4 h-4 fill-current" />
                          </button>
                          <button
                            type="button"
                            onClick={togglePlayPause}
                            className="w-9 h-9 rounded-full bg-gradient-to-r from-pink-500 to-purple-600 flex items-center justify-center text-white shadow-lg cursor-pointer hover:scale-105 transition-transform"
                            title={isPlaying ? "Tạm dừng" : "Phát"}
                          >
                            {isLoading ? (
                              <Loader2 className="w-4 h-4 animate-spin" />
                            ) : isPlaying ? (
                              <Pause className="w-4 h-4 fill-current" />
                            ) : (
                              <Play className="w-4 h-4 fill-current translate-x-0.5" />
                            )}
                          </button>
                          <button
                            type="button"
                            onClick={handleNext}
                            title="Bài kế tiếp"
                            className="p-2 rounded-lg bg-white/5 hover:bg-white/15 text-white/80 hover:text-pink-300 cursor-pointer transition-colors"
                          >
                            <SkipForward className="w-4 h-4 fill-current" />
                          </button>
                        </>
                      )}

                      <button
                        type="button"
                        onClick={refreshDiscoveryFeed}
                        title="Đổi danh sách gợi ý Khám phá"
                        className="px-2.5 py-1.5 rounded-xl bg-white/5 hover:bg-pink-500/20 text-white/80 hover:text-pink-300 border border-white/10 text-xs font-semibold flex items-center gap-1 cursor-pointer transition-colors"
                      >
                        <RefreshCw className="w-3.5 h-3.5" />
                        <span className="hidden md:inline">Đổi gợi ý</span>
                      </button>

                      <button
                        type="button"
                        onClick={() => {
                          setShowDiscovery(false);
                          if (currentTrack && playerMode === "audio") {
                            const hdOpt =
                              availableQualities.find((q) => q.height === 720) ||
                              availableQualities.find((q) => q.height > 0);
                            if (hdOpt) {
                              handleSelectQuality(hdOpt);
                            }
                          }
                        }}
                        className="px-3 py-1.5 rounded-xl bg-pink-500/20 hover:bg-pink-500 text-pink-200 hover:text-white border border-pink-500/40 transition-all cursor-pointer flex items-center gap-1.5 text-xs font-bold"
                      >
                        <Film className="w-3.5 h-3.5" />
                        <span>Xem Video</span>
                      </button>
                    </div>
                  </div>

                  {currentTrack && (
                    <input
                      type="range"
                      min={0}
                      max={duration || 100}
                      step={0.5}
                      value={currentTime}
                      onChange={(e) => handleSeek(parseFloat(e.target.value))}
                      className="w-full h-1 rounded-full appearance-none cursor-pointer accent-pink-500 bg-white/15"
                    />
                  )}
                </div>

                {/* Section 1: Nhạc Trending */}
                <div>
                  <div className="flex items-center justify-between mb-3.5">
                    <div className="flex items-center gap-2.5">
                      <Sparkles className="w-5 h-5 text-pink-400" />
                      <h2 className="text-[20px] font-bold text-white tracking-wide">
                        Nhạc Trending hôm nay
                      </h2>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        if (trendingTracks.length > 0) {
                          playAudioInDiscovery(trendingTracks[0], trendingTracks);
                        }
                      }}
                      className="text-xs font-semibold px-3 py-1.5 rounded-full bg-pink-500/10 hover:bg-pink-500 text-pink-400 hover:text-white border border-pink-500/30 transition-all cursor-pointer flex items-center gap-1.5"
                    >
                      <Play className="w-3.5 h-3.5 fill-current" />
                      <span>Phát tất cả</span>
                    </button>
                  </div>
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {trendingTracks.slice(0, 12).map((item) => (
                      <div
                        key={item.id}
                        onClick={() => playAudioInDiscovery(item, trendingTracks)}
                        className="w-full flex flex-col cursor-pointer group"
                      >
                        <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                          <img
                            src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                            alt={item.title}
                            onError={(e) => handleThumbError(e, item.id)}
                            className="w-full h-full object-cover"
                          />
                          <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                            <div className="w-10 h-10 rounded-full bg-pink-500 flex items-center justify-center shadow-lg shadow-pink-500/40 text-white">
                              <Play className="w-5 h-5 fill-white ml-0.5" />
                            </div>
                          </div>
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-2 transition-colors">
                          {item.title}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                          {item.author || "Kahoii Music"}
                        </p>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Section 2: Mới Phát Hành */}
                <div>
                  <div className="flex items-center justify-between mb-3.5">
                    <div className="flex items-center gap-2.5">
                      <Flame className="w-5 h-5 text-orange-400" />
                      <h2 className="text-[20px] font-bold text-white tracking-wide">
                        Mới Phát Hành
                      </h2>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        if (newReleaseTracks.length > 0) {
                          playAudioInDiscovery(newReleaseTracks[0], newReleaseTracks);
                        }
                      }}
                      className="text-xs font-semibold px-3 py-1.5 rounded-full bg-orange-500/10 hover:bg-orange-500 text-orange-400 hover:text-white border border-orange-500/30 transition-all cursor-pointer flex items-center gap-1.5"
                    >
                      <Play className="w-3.5 h-3.5 fill-current" />
                      <span>Phát tất cả</span>
                    </button>
                  </div>
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {newReleaseTracks.slice(0, 12).map((item) => (
                      <div
                        key={item.id}
                        onClick={() => playAudioInDiscovery(item, newReleaseTracks)}
                        className="w-full flex flex-col cursor-pointer group"
                      >
                        <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                          <img
                            src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                            alt={item.title}
                            onError={(e) => handleThumbError(e, item.id)}
                            className="w-full h-full object-cover"
                          />
                          <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                            <div className="w-10 h-10 rounded-full bg-orange-500 flex items-center justify-center shadow-lg shadow-orange-500/40 text-white">
                              <Play className="w-5 h-5 fill-white ml-0.5" />
                            </div>
                          </div>
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-2 transition-colors">
                          {item.title}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                          {item.author || "Kahoii Music"}
                        </p>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Section 3: Nhạc Sĩ Nổi Bật (Đổi từ Nhạc Sĩ) */}
                <div>
                  <div className="flex items-center gap-2.5 mb-3.5">
                    <Mic2 className="w-5 h-5 text-purple-400" />
                    <h2 className="text-[20px] font-bold text-white tracking-wide">
                      Nhạc Sĩ Nổi Bật
                    </h2>
                  </div>
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {FEATURED_ARTISTS.map((artist, idx) => (
                      <div
                        key={idx}
                        onClick={() => searchArtistSongs(artist.query)}
                        className="w-full flex flex-col items-center cursor-pointer group"
                      >
                        <div className="w-full aspect-square rounded-full overflow-hidden bg-purple-900/30 shadow-md border-2 border-white/10 group-hover:border-pink-500 group-hover:scale-[1.04] transition-all duration-200">
                          <img
                            src={artist.avatar}
                            alt={artist.name}
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-2 text-center w-full transition-colors">
                          {artist.name}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate text-center w-full">
                          {artist.genre}
                        </p>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Section 4 & 5: Gần đây & Có thể bạn sẽ thích (CHỈ XUẤT HIỆN KHI ĐÃ ĐĂNG NHẬP) */}
                {userSession?.isLoggedIn && (
                  <>
                    {/* Section 4: Gần đây */}
                    <div>
                      <div className="flex items-center gap-2.5 mb-3.5">
                        <Clock className="w-5 h-5 text-emerald-400" />
                        <h2 className="text-[20px] font-bold text-white tracking-wide">
                          Gần đây
                        </h2>
                      </div>
                      {recentTracksToDisplay.length > 0 ? (
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
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
                              <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-2 transition-colors">
                                {item.title}
                              </p>
                              <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                {item.author || "Kahoii Music"}
                              </p>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <div className="py-6 text-center text-white/40 text-xs italic bg-white/5 rounded-2xl border border-white/5">
                          Chưa có bài hát nào gần đây. Hãy chọn hoặc tìm kiếm bài hát để phát.
                        </div>
                      )}
                    </div>

                    {/* Section 5: Có thể bạn sẽ thích */}
                    <div>
                      <div className="flex items-center gap-2.5 mb-3.5">
                        <Heart className="w-5 h-5 text-rose-400 fill-rose-500/20" />
                        <h2 className="text-[20px] font-bold text-white tracking-wide">
                          Có thể bạn sẽ thích
                        </h2>
                      </div>
                      {recommendedTracksToDisplay.length > 0 ? (
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
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
                              <p className="text-[12px] font-semibold text-white/90 group-hover:text-pink-300 truncate mt-2 transition-colors">
                                {item.title}
                              </p>
                              <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
                                {item.author || "Kahoii Music"}
                              </p>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <div className="py-6 text-center text-white/40 text-xs italic bg-white/5 rounded-2xl border border-white/5">
                          Chưa có bài hát yêu thích nào.
                        </div>
                      )}
                    </div>
                  </>
                )}
              </div>
            )}
          </section>

          {/* Main Video Stage + Compact Queue + Right Random Playlist */}
          <div
            className={
              isSearchOpen || showDiscovery
                ? "hidden"
                : "flex-1 flex flex-col lg:flex-row gap-3 min-h-0 overflow-hidden"
            }
          >
            {/* CENTER COLUMN: Video Player (shrunk width) + Compact Queue */}
            <div className="flex-1 flex flex-col gap-3 min-w-0 min-h-0 overflow-hidden">
              <VideoPlayerStage
                currentTrack={currentTrack}
                isPlaying={isPlaying}
                isLoading={isLoading}
                currentTime={currentTime}
                duration={duration}
                volume={volume}
                isMuted={isMuted}
                repeatMode={repeatMode}
                activePlayerType={playerMode}
                availableQualities={availableQualities}
                selectedQuality={selectedQuality}
                playbackRate={playbackRate}
                isFavorite={
                  !!(currentTrack && libraryData.favorites.some((t) => t.id === currentTrack.id))
                }
                lyricsText={lyrics?.plainLyrics || lyrics?.syncedLyrics || null}
                sleepTimerMinutes={sleepTimerMinutes}
                videoRef={videoRef}
                onTogglePlayPause={togglePlayPause}
                onNext={handleNext}
                onPrev={handlePrev}
                onSeek={handleSeek}
                onVolumeChange={(val) => {
                  setIsMuted(false);
                  setVolume(val);
                }}
                onToggleMute={() => setIsMuted((m) => !m)}
                onCycleRepeat={cycleRepeatMode}
                onSelectQuality={handleSelectQuality}
                onSelectSpeed={handleSelectSpeed}
                onToggleFavorite={toggleCurrentFavorite}
                onSelectSleepTimer={(m) => startSleepTimer(m)}
                onVideoTimeUpdate={handleTimeUpdate}
                onVideoLoadedMetadata={() => {
                  if (activePlayerType.current === "video" && videoRef.current) {
                    setDuration(videoRef.current.duration || currentTrack?.duration || 207);
                  }
                }}
                onVideoEnded={() => {
                  if (activePlayerType.current === "video") {
                    handleTrackEnded();
                  }
                }}
                onVideoPlay={() => {
                  if (activePlayerType.current === "video") {
                    setIsPlaying(true);
                    if (isAdaptiveSyncedRef.current && audioRef.current && audioRef.current.paused) {
                      if (videoRef.current) audioRef.current.currentTime = videoRef.current.currentTime;
                      audioRef.current.play().catch(() => { });
                    }
                  }
                }}
                onVideoPause={() => {
                  if (activePlayerType.current === "video") {
                    setIsPlaying(false);
                    if (isAdaptiveSyncedRef.current && audioRef.current && !audioRef.current.paused && !videoRef.current?.seeking) {
                      audioRef.current.pause();
                    }
                  }
                }}
                onAddToPlaylist={() => currentTrack && setPlaylistTargetTrack(currentTrack)}
                onVideoError={() => {
                  if (activePlayerType.current === "video" && currentTrack && ytPlayerRef.current?.loadVideoById) {
                    isAdaptiveSyncedRef.current = false;
                    if (audioRef.current) {
                      try { audioRef.current.pause(); } catch { }
                    }
                    activePlayerType.current = "youtube";
                    setPlayerMode("youtube");
                    setSelectedQuality("Auto");
                    try {
                      ytPlayerRef.current.loadVideoById({ videoId: currentTrack.id, startSeconds: currentTime || 0 });
                      ytPlayerRef.current.playVideo();
                    } catch { }
                  }
                }}
              />

              {/* Compact Queue Panel (Shrunk below Video Player matching layout) */}
              <div className="h-[160px] lg:h-[185px] flex-shrink-0 figma-panel flex flex-col min-h-0 overflow-hidden p-3">
                <div className="flex items-center justify-between pb-1.5 mb-1.5 border-b border-white/10 flex-shrink-0">
                  <span className="text-xs font-bold text-white/90">
                    Hàng đợi ({queueTracks.length})
                  </span>
                  <div className="flex items-center gap-1.5">
                    {queueTracks.length > 1 && (
                      <button
                        type="button"
                        onClick={handleShuffleQueue}
                        title="Trộn hàng đợi"
                        className="p-1 rounded-md hover:bg-white/10 text-white/60 hover:text-pink-300 transition-colors cursor-pointer"
                      >
                        <Shuffle className="w-3.5 h-3.5" />
                      </button>
                    )}
                    {queueTracks.length > 0 && (
                      <button
                        type="button"
                        onClick={handleClearQueue}
                        title="Xóa toàn bộ hàng đợi"
                        className="p-1 rounded-md hover:bg-red-500/20 text-white/60 hover:text-red-400 transition-colors cursor-pointer"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => setShowDiscovery(true)}
                      className="text-[11px] text-pink-400 hover:text-pink-300 font-semibold cursor-pointer ml-1"
                    >
                      + Khám phá thêm
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
                          className={`flex items-center gap-2.5 px-2.5 py-1.5 transition-all cursor-pointer group ${isCurrent ? "active-queue-beam shadow-md" : "hover:bg-white/5 rounded-xl"
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
                          <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0">
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                setPlaylistTargetTrack(track);
                              }}
                              title="Thêm vào danh sách phát"
                              className="p-1 rounded-md hover:bg-purple-500/30 text-white/70 hover:text-purple-300 cursor-pointer"
                            >
                              <ListPlus className="w-3.5 h-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={(e) => handleRemoveFromQueue(i, e)}
                              title="Xóa khỏi hàng đợi"
                              className="p-1 rounded-md hover:bg-red-500/30 text-white/70 hover:text-red-400 cursor-pointer"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                          <span className="text-[10px] text-white/60 font-mono pr-1 flex-shrink-0">
                            {track.duration && track.duration > 0 ? formatTime(track.duration) : "--:--"}
                          </span>
                        </div>
                      );
                    })
                  ) : (
                    <div className="flex flex-col items-center justify-center py-6 text-center text-white/40 text-xs">
                      <p>Hàng đợi đang trống</p>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* RIGHT COLUMN: Random Playlist Panel */}
            <aside className="w-full lg:w-[275px] xl:w-[310px] flex-shrink-0 figma-panel flex flex-col min-h-[210px] lg:min-h-0 overflow-hidden p-3">
              <div className="flex items-center justify-between pb-2 mb-2 border-b border-white/10 flex-shrink-0">
                <div className="flex items-center gap-1.5 min-w-0">
                  <Shuffle className="w-3.5 h-3.5 text-pink-400 flex-shrink-0" />
                  <span className="text-xs font-bold text-white/95 truncate">
                    Gợi ý liên quan ({randomTracks.length})
                  </span>
                </div>
                <div className="flex items-center gap-1 flex-shrink-0">
                  <button
                    type="button"
                    onClick={shuffleRandomTracks}
                    title="Trộn lại danh sách gợi ý"
                    className="p-1.5 rounded-lg bg-white/5 hover:bg-pink-500/20 text-white/75 hover:text-pink-300 transition-colors cursor-pointer"
                  >
                    <RefreshCw className={`w-3.5 h-3.5 ${isLoadingRelated ? "animate-spin text-pink-400" : ""}`} />
                  </button>
                  {randomTracks.length > 0 && (
                    <button
                      type="button"
                      onClick={() => {
                        setPlaylist(randomTracks);
                        setQueueTracks(randomTracks);
                        setCurrentIndex(0);
                        loadAndPlayTrack(randomTracks[0], true);
                      }}
                      title="Phát toàn bộ danh sách ngẫu nhiên"
                      className="px-2 py-1 rounded-lg bg-pink-500/20 hover:bg-pink-500 text-pink-300 hover:text-white text-[10px] font-bold transition-colors cursor-pointer flex items-center gap-1"
                    >
                      <Play className="w-3 h-3 fill-current" />
                      <span>Phát</span>
                    </button>
                  )}
                </div>
              </div>

              <div className="flex-1 overflow-y-auto flex flex-col gap-1.5 min-h-0 pr-1">
                {randomTracks.length > 0 ? (
                  randomTracks.map((track, idx) => {
                    const isCurrent = currentTrack?.id === track.id;
                    return (
                      <div
                        key={`rand-${track.id}-${idx}`}
                        onClick={() => {
                          setPlaylist(randomTracks);
                          setQueueTracks(randomTracks);
                          setCurrentIndex(idx);
                          loadAndPlayTrack(track, true);
                        }}
                        className={`flex items-center gap-2.5 p-1.5 rounded-xl transition-all cursor-pointer group ${isCurrent ? "active-queue-beam shadow-md" : "hover:bg-white/5"
                          }`}
                      >
                        <div className="relative w-14 h-9 rounded-lg overflow-hidden flex-shrink-0 bg-black/40">
                          <img
                            src={track.thumbnail || `https://i.ytimg.com/vi/${track.id}/mqdefault.jpg`}
                            alt=""
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-xs font-semibold text-white truncate group-hover:text-pink-300 transition-colors">
                            {track.title}
                          </p>
                          <p className="text-[10px] text-white/50 truncate">
                            {track.author || "ThuongTube"}
                          </p>
                        </div>
                        <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0">
                          <button
                            type="button"
                            onClick={(e) => handleAddToQueue(track, e)}
                            title="Thêm vào hàng chờ"
                            className="p-1 rounded-md hover:bg-pink-500/30 text-white/70 hover:text-pink-300 cursor-pointer"
                          >
                            <ListEnd className="w-3.5 h-3.5" />
                          </button>
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              setPlaylistTargetTrack(track);
                            }}
                            title="Thêm vào danh sách phát"
                            className="p-1 rounded-md hover:bg-purple-500/30 text-white/70 hover:text-purple-300 cursor-pointer"
                          >
                            <ListPlus className="w-3.5 h-3.5" />
                          </button>
                        </div>
                        <span className="text-[10px] text-white/55 font-mono flex-shrink-0">
                          {track.duration && track.duration > 0 ? formatTime(track.duration) : "--:--"}
                        </span>
                      </div>
                    );
                  })
                ) : (
                  <div className="flex-1 flex flex-col items-center justify-center text-center text-white/40 text-xs px-3 py-8">
                    <p>Chưa có video để trộn ngẫu nhiên</p>
                    <button
                      type="button"
                      onClick={() => setShowDiscovery(true)}
                      className="mt-2 px-3 py-1 rounded-full bg-pink-500/20 text-pink-300 hover:bg-pink-500 hover:text-white text-[11px] font-semibold cursor-pointer transition-colors"
                    >
                      Khám phá ngay
                    </button>
                  </div>
                )}
              </div>
            </aside>
          </div>
        </div>
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
                      src={`${API_BASE}/api/user/avatar?t=${avatarKey}`}
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

                {/* Edit Profile Form */}
                {isEditingProfile && (
                  <form
                    onSubmit={handleSaveProfile}
                    className="flex flex-col gap-3 p-3 rounded-xl bg-black/35 border border-pink-500/35"
                  >
                    {loginError && (
                      <div className="p-2 rounded-lg bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-center gap-1.5">
                        <AlertCircle className="w-3.5 h-3.5 text-red-400 flex-shrink-0" />
                        <span>{loginError}</span>
                      </div>
                    )}
                    <div className="flex flex-col gap-1">
                      <label className="text-[11px] font-semibold text-white/80">Tên hiển thị mới</label>
                      <input
                        type="text"
                        placeholder={userSession.user?.name || "Nhập tên mới..."}
                        value={editProfileName}
                        onChange={(e) => setEditProfileName(e.target.value)}
                        className="w-full px-3 py-1.5 rounded-lg bg-black/45 border border-purple-500/30 focus:border-pink-500 text-xs text-white placeholder-white/35 focus:outline-none"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[11px] font-semibold text-white/80">Ảnh đại diện (Avatar)</label>
                      <div className="flex items-center gap-2.5">
                        {editProfileAvatar && (
                          <img
                            src={editProfileAvatar}
                            alt="Preview"
                            className="w-9 h-9 rounded-full object-cover border border-pink-500/50"
                          />
                        )}
                        <input
                          type="file"
                          accept="image/*"
                          onChange={handleAvatarFileChange}
                          className="text-[11px] text-white/75 file:mr-2 file:py-1 file:px-2.5 file:rounded-lg file:border-0 file:text-[11px] file:font-semibold file:bg-pink-500/25 file:text-pink-300 hover:file:bg-pink-500/40 cursor-pointer"
                        />
                      </div>
                    </div>
                    <div className="flex items-center justify-end gap-2 pt-1">
                      <button
                        type="button"
                        onClick={() => {
                          setIsEditingProfile(false);
                          setEditProfileAvatar(null);
                          setLoginError(null);
                        }}
                        className="px-3 py-1.5 rounded-lg bg-white/5 hover:bg-white/15 text-xs text-white/75 cursor-pointer"
                      >
                        Hủy
                      </button>
                      <button
                        type="submit"
                        disabled={isSavingProfile}
                        className="px-3.5 py-1.5 rounded-lg bg-gradient-to-r from-pink-500 to-purple-600 text-white text-xs font-bold cursor-pointer disabled:opacity-50"
                      >
                        {isSavingProfile ? "Đang lưu..." : "Lưu thay đổi"}
                      </button>
                    </div>
                  </form>
                )}

                {/* Action Buttons */}
                <div className="flex flex-col gap-2 pt-1">
                  {!isEditingProfile && (
                    <button
                      type="button"
                      onClick={() => {
                        setEditProfileName(userSession.user?.name || "");
                        setEditProfileAvatar(null);
                        setLoginError(null);
                        setIsEditingProfile(true);
                      }}
                      className="w-full py-2 rounded-xl bg-white/5 hover:bg-pink-500/20 text-white/85 hover:text-pink-300 font-semibold text-xs border border-white/10 hover:border-pink-500/40 transition-all flex items-center justify-center gap-2 cursor-pointer"
                    >
                      <User className="w-3.5 h-3.5" />
                      <span>Thay đổi thông tin cá nhân</span>
                    </button>
                  )}

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
              /* Login / Register Form for Non-Logged-in State */
              <form
                onSubmit={authMode === "login" ? handleLogin : handleRegister}
                className="flex flex-col gap-3.5 mt-4"
              >
                {/* Auth Mode Switcher */}
                <div className="grid grid-cols-2 gap-2 p-1 rounded-xl bg-black/40 border border-white/10">
                  <button
                    type="button"
                    onClick={() => {
                      setAuthMode("login");
                      setLoginError(null);
                    }}
                    className={`py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${authMode === "login"
                        ? "bg-gradient-to-r from-pink-500 to-purple-600 text-white shadow"
                        : "text-white/60 hover:text-white"
                      }`}
                  >
                    Đăng nhập
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setAuthMode("register");
                      setLoginError(null);
                    }}
                    className={`py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${authMode === "register"
                        ? "bg-gradient-to-r from-pink-500 to-purple-600 text-white shadow"
                        : "text-white/60 hover:text-white"
                      }`}
                  >
                    Đăng ký
                  </button>
                </div>

                <p className="text-xs text-white/70">
                  {authMode === "login"
                    ? "Đăng nhập tài khoản ThuongTube / Kahoii để đồng bộ danh sách phát và yêu thích."
                    : "Tạo tài khoản mới để lưu và đồng bộ danh sách phát của bạn trên Cloud."}
                </p>

                {loginError && (
                  <div className="p-2.5 rounded-xl bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 text-red-400 flex-shrink-0" />
                    <span>{loginError}</span>
                  </div>
                )}

                {authMode === "register" && (
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[11px] font-semibold text-white/80">
                      Tên hiển thị
                    </label>
                    <input
                      type="text"
                      placeholder="VD: Thuong"
                      value={registerName}
                      onChange={(e) => setRegisterName(e.target.value)}
                      className="w-full px-3.5 py-2 rounded-xl bg-black/40 border border-purple-500/30 focus:border-pink-500 text-xs text-white placeholder-white/30 focus:outline-none"
                    />
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

                {authMode === "register" && (
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[11px] font-semibold text-white/80">
                      Xác nhận mật khẩu
                    </label>
                    <input
                      type="password"
                      placeholder="Nhập lại mật khẩu..."
                      value={registerConfirmPassword}
                      onChange={(e) => setRegisterConfirmPassword(e.target.value)}
                      className="w-full px-3.5 py-2 rounded-xl bg-black/40 border border-purple-500/30 focus:border-pink-500 text-xs text-white placeholder-white/30 focus:outline-none"
                    />
                  </div>
                )}

                <button
                  type="submit"
                  disabled={isLoggingIn}
                  className="w-full mt-2 py-2.5 rounded-xl bg-gradient-to-r from-pink-500 to-purple-600 hover:opacity-95 text-white font-bold text-xs shadow-lg shadow-pink-500/30 transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                >
                  {isLoggingIn ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin text-white" />
                      <span>{authMode === "login" ? "Đang xác thực..." : "Đang tạo tài khoản..."}</span>
                    </>
                  ) : (
                    <>
                      <LogIn className="w-4 h-4" />
                      <span>{authMode === "login" ? "Đăng nhập" : "Đăng ký tài khoản"}</span>
                    </>
                  )}
                </button>
              </form>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* 4. ADD TO PLAYLIST MODAL                                                  */}
      {/* ========================================================================= */}
      {playlistTargetTrack && (
        <div
          onClick={() => setPlaylistTargetTrack(null)}
          className="fixed inset-0 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 z-50"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="figma-panel w-full max-w-[380px] flex flex-col p-5 shadow-2xl border border-pink-500/50 gap-3"
          >
            <div className="flex items-center justify-between pb-2.5 border-b border-white/10">
              <span className="font-bold text-sm text-pink-300 flex items-center gap-2">
                <ListPlus className="w-4 h-4 text-pink-400" />
                Thêm vào Danh sách phát
              </span>
              <button
                type="button"
                onClick={() => setPlaylistTargetTrack(null)}
                className="p-1 rounded-lg text-white/60 hover:text-white hover:bg-white/10 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="flex items-center gap-2.5 p-2 rounded-xl bg-white/5 border border-white/10">
              <img
                src={playlistTargetTrack.thumbnail || `https://i.ytimg.com/vi/${playlistTargetTrack.id}/mqdefault.jpg`}
                alt=""
                onError={(e) => { e.currentTarget.src = DEFAULT_TRACK_ART; }}
                className="w-10 h-10 rounded-lg object-cover flex-shrink-0"
              />
              <div className="min-w-0 flex-1">
                <p className="text-xs font-semibold text-white truncate">{playlistTargetTrack.title}</p>
                <p className="text-[10px] text-white/50 truncate">{playlistTargetTrack.author}</p>
              </div>
            </div>

            <div className="max-h-[200px] overflow-y-auto flex flex-col gap-1.5 pr-1">
              {libraryData.playlists.length > 0 ? (
                libraryData.playlists.map((pl) => {
                  const alreadyIn = (pl.items || []).some((t: Track) => t.id === playlistTargetTrack.id);
                  return (
                    <button
                      key={pl.id}
                      type="button"
                      onClick={() => handleAddTrackToPlaylist(pl.id, playlistTargetTrack)}
                      className="flex items-center justify-between px-3 py-2 rounded-xl bg-white/5 hover:bg-pink-500/20 border border-white/10 hover:border-pink-500/40 text-left transition-all cursor-pointer"
                    >
                      <div className="min-w-0 flex-1 pr-2">
                        <p className="text-xs font-semibold text-white truncate">{pl.name}</p>
                        <p className="text-[10px] text-white/50">{(pl.items || []).length} mục</p>
                      </div>
                      {alreadyIn ? (
                        <Check className="w-4 h-4 text-emerald-400 flex-shrink-0" />
                      ) : (
                        <Plus className="w-4 h-4 text-pink-400 flex-shrink-0" />
                      )}
                    </button>
                  );
                })
              ) : (
                <p className="text-xs text-white/45 text-center py-3">
                  Chưa có danh sách phát nào. Tạo mới bên dưới:
                </p>
              )}
            </div>

            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleCreatePlaylist(newPlaylistName, playlistTargetTrack);
              }}
              className="flex items-center gap-2 pt-2 border-t border-white/10"
            >
              <input
                type="text"
                placeholder="Tên danh sách phát mới..."
                value={newPlaylistName}
                onChange={(e) => setNewPlaylistName(e.target.value)}
                className="flex-1 px-3 py-2 rounded-xl bg-black/40 border border-white/15 focus:border-pink-500 text-xs text-white placeholder-white/40 focus:outline-none"
              />
              <button
                type="submit"
                className="px-3.5 py-2 rounded-xl bg-gradient-to-r from-pink-500 to-purple-600 hover:opacity-90 text-white text-xs font-bold cursor-pointer flex items-center gap-1"
              >
                <FolderPlus className="w-3.5 h-3.5" />
                <span>Tạo & Thêm</span>
              </button>
            </form>
          </div>
        </div>
      )}

      {/* Custom Yes/No Confirmation & Alert Modal */}
      {confirmDialog && (
        <ConfirmDialog
          isOpen={confirmDialog.isOpen}
          title={confirmDialog.title}
          message={confirmDialog.message}
          confirmText={confirmDialog.confirmText}
          cancelText={confirmDialog.cancelText}
          variant={confirmDialog.variant}
          showCancel={confirmDialog.showCancel}
          onConfirm={confirmDialog.onConfirm}
          onCancel={() => setConfirmDialog(null)}
        />
      )}

      {/* Friendly Feedback Toast */}
      {toastMessage && (
        <div className="fixed bottom-5 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-full bg-[#190736]/95 border border-pink-500/60 text-pink-200 text-xs font-semibold shadow-2xl backdrop-blur-md flex items-center gap-2">
          <Check className="w-3.5 h-3.5 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}
    </div>
  );
}
