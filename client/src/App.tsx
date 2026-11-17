import React, { useState, useEffect, useRef } from "react";
import {
  Play,
  Pause,
  SkipBack,
  SkipForward,
  Repeat,
  Repeat1,
  Shuffle,
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
  Sparkles,
  Flame,
  Mic2,
  FolderPlus,
  FolderHeart,
  ListMusic,
  ChevronRight,
  Download,
  Plus,
  Music2,
} from "lucide-react";
import type {
  Track,
  StructuredSearchResult,
  SearchPlaylistItem,
} from "./types";
import { formatTime } from "./utils/lyricsParser";
import { loadAndExtractTheme, type ThemePalette, DEFAULT_THEME } from "./utils/colorExtractor";

// Clean, robust SVG placeholder used only when a song has no thumbnail
const DEFAULT_TRACK_ART =
  "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect width='100' height='100' rx='16' fill='%23240a50'/%3E%3Ccircle cx='50' cy='50' r='30' fill='%23381078'/%3E%3Cpath d='M44 36v20.5a6.5 6.5 0 1 1-3-5.5V40l18-4v16.5a6.5 6.5 0 1 1-3-5.5V32z' fill='%23e6007e'/%3E%3C/svg%3E";

// Base API URL for flexible deployment (GitHub Pages + Remote Backend or Monolithic Express)
const API_BASE = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/$/, "");

// Curated Initial Trending Tracks (Preloaded immediately before network returns)
const INITIAL_TRENDING_TRACKS: Track[] = [
  {
    id: "d3WWp_yU2L4",
    title: "Nơi Này Có Anh",
    author: "Sơn Tùng M-TP",
    duration: 260,
    thumbnail: "https://i.ytimg.com/vi/d3WWp_yU2L4/mqdefault.jpg",
  },
  {
    id: "cAmZ_k2n9yA",
    title: "Chúng Ta Của Tương Lai",
    author: "Sơn Tùng M-TP",
    duration: 254,
    thumbnail: "https://i.ytimg.com/vi/cAmZ_k2n9yA/mqdefault.jpg",
  },
  {
    id: "oZpLwV8H_F4",
    title: "Đúng Người Đúng Thời Điểm",
    author: "Thanh Hưng",
    duration: 250,
    thumbnail: "https://i.ytimg.com/vi/oZpLwV8H_F4/mqdefault.jpg",
  },
  {
    id: "q0m0o5H0uS8",
    title: "Từng Là",
    author: "Vũ Cát Tường",
    duration: 236,
    thumbnail: "https://i.ytimg.com/vi/q0m0o5H0uS8/mqdefault.jpg",
  },
  {
    id: "1J5eCsqcW_M",
    title: "Ngày Mai Người Ta Lấy Chồng",
    author: "Thành Đạt",
    duration: 320,
    thumbnail: "https://i.ytimg.com/vi/1J5eCsqcW_M/mqdefault.jpg",
  },
  {
    id: "tP3r3r34wQA",
    title: "Cắt Đôi Nỗi Sầu",
    author: "Tăng Duy Tân",
    duration: 188,
    thumbnail: "https://i.ytimg.com/vi/tP3r3r34wQA/mqdefault.jpg",
  },
];

// Curated Initial New Releases
const INITIAL_NEW_RELEASES: Track[] = [
  {
    id: "abPmZCZZrFA",
    title: "Đừng Làm Trái Tim Anh Đau",
    author: "Sơn Tùng M-TP",
    duration: 326,
    thumbnail: "https://i.ytimg.com/vi/abPmZCZZrFA/mqdefault.jpg",
  },
  {
    id: "Jg9Z6n2Wn1o",
    title: "Bầu Trời Mới",
    author: "Da LAB ft. Minh Tốc & Lam",
    duration: 218,
    thumbnail: "https://i.ytimg.com/vi/Jg9Z6n2Wn1o/mqdefault.jpg",
  },
  {
    id: "uB1T1VbZt9g",
    title: "Nâng Chén Tiêu Sầu",
    author: "Bích Phương",
    duration: 228,
    thumbnail: "https://i.ytimg.com/vi/uB1T1VbZt9g/mqdefault.jpg",
  },
  {
    id: "8zV_x8G9L3w",
    title: "Thủy Triều",
    author: "Quang Hùng MasterD",
    duration: 204,
    thumbnail: "https://i.ytimg.com/vi/8zV_x8G9L3w/mqdefault.jpg",
  },
  {
    id: "yV9w8k2n1pM",
    title: "Giá Như",
    author: "Soobin Hoàng Sơn",
    duration: 245,
    thumbnail: "https://i.ytimg.com/vi/yV9w8k2n1pM/mqdefault.jpg",
  },
  {
    id: "oK8v3W9mZ1p",
    title: "Từng Quen",
    author: "Wren Evans",
    duration: 178,
    thumbnail: "https://i.ytimg.com/vi/oK8v3W9mZ1p/mqdefault.jpg",
  },
];

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

// Check if a character belongs to Latin script (includes Vietnamese diacritics, European accents)
function isLatinLetter(char: string): boolean {
  return /^\p{Script=Latin}$/u.test(char);
}

// Check if a string is composed primarily of Latin characters
function isLatinText(str: string): boolean {
  if (!str || !str.trim()) return false;
  const trimmed = str.trim();
  const lettersOnly = trimmed.replace(/[\p{P}\p{S}\p{N}\s]/gu, "");
  if (!lettersOnly) return false;
  if (!isLatinLetter(lettersOnly[0])) return false;
  const hasNonLatinAlphabet =
    /[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Hangul}\p{Script=Cyrillic}\p{Script=Arabic}\p{Script=Thai}]/u.test(
      lettersOnly
    );
  return !hasNonLatinAlphabet;
}

// Sort criteria:
// 1. Non-Latin characters go to the top (Group 0).
// 2. If song/playlist title != Latin but author == Latin, sort in Latin section by author (Group 1).
// 3. Otherwise, sort all Latin by title A-Z (Group 1).
function getSortCriteria(item: { title?: string; name?: string; author?: string }) {
  const title = (item.title || item.name || "").trim();
  const author = (item.author || "").trim();

  const titleFirstChar = title ? title[0] : "";
  const titleIsLatin = titleFirstChar && isLatinLetter(titleFirstChar) && isLatinText(title);

  if (titleIsLatin) {
    return { group: 1, key: title };
  }

  const authorFirstChar = author ? author[0] : "";
  const authorIsLatin = authorFirstChar && isLatinLetter(authorFirstChar) && isLatinText(author);

  if (authorIsLatin) {
    return { group: 1, key: `${author} - ${title}` };
  }

  return { group: 0, key: title || author };
}

function compareSortItems(a: any, b: any): number {
  const critA = getSortCriteria(a);
  const critB = getSortCriteria(b);

  if (critA.group !== critB.group) {
    return critA.group - critB.group;
  }

  return critA.key.localeCompare(critB.key, "vi", {
    sensitivity: "base",
    numeric: true,
  });
}

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
  const [repeatMode, setRepeatMode] = useState<"all" | "one" | "shuffle" | "off">("all");
  const repeatModeRef = useRef<"all" | "one" | "shuffle" | "off">("all");
  const playlistRef = useRef<Track[]>([]);
  const queueTracksRef = useRef<Track[]>([]);
  const currentIndexRef = useRef<number>(0);
  const shuffleHistoryRef = useRef<number[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Home Discovery Feed (Trending & New Releases)
  const [trendingTracks, setTrendingTracks] = useState<Track[]>(INITIAL_TRENDING_TRACKS);
  const [newReleaseTracks, setNewReleaseTracks] = useState<Track[]>(INITIAL_NEW_RELEASES);

  // Dual-Engine Playback (Audio Element + Zero-Fail YouTube Iframe Player Fallback)
  const ytPlayerRef = useRef<any>(null);
  const activePlayerType = useRef<"audio" | "youtube">("youtube");
  const [_lyrics, setLyrics] = useState<any>(null);

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

  // Top Search State & History
  const [topSearchText, setTopSearchText] = useState<string>("");
  const [searchQueryTitle, setSearchQueryTitle] = useState<string>("");
  const [isSearchOpen, setIsSearchOpen] = useState<boolean>(false);
  const [searchResults, setSearchResults] = useState<Track[]>([]);
  const [searchStructured, setSearchStructured] = useState<StructuredSearchResult | null>(null);
  const [activeSearchTab, setActiveSearchTab] = useState<"all" | "tracks" | "artists" | "playlists">("all");
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
      } catch {}
      return updated;
    });
  };

  const removeFromSearchHistory = (itemToRemove: string, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setSearchHistory((prev) => {
      const updated = prev.filter((item) => item !== itemToRemove);
      try {
        localStorage.setItem("kahoii_search_history", JSON.stringify(updated));
      } catch {}
      return updated;
    });
  };

  const clearSearchHistory = (e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    setSearchHistory([]);
    try {
      localStorage.removeItem("kahoii_search_history");
    } catch {}
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
      const tracksList: Track[] = data.tracks || data.results || [];
      setSearchResults(tracksList);
      setSearchStructured({
        query: data.query || trimmed,
        topResult: data.topResult || null,
        tracks: tracksList,
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

  // Pause any active playback safely (both Audio element and YouTube player)
  const pausePlayback = () => {
    if (audioRef.current) {
      try {
        audioRef.current.pause();
      } catch {}
    }
    if (ytPlayerRef.current && typeof ytPlayerRef.current.pauseVideo === "function") {
      try {
        ytPlayerRef.current.pauseVideo();
      } catch {}
    }
    setIsPlaying(false);
  };

  // Sleep Timer State & Controller (with live countdown and full audio+yt shutdown)
  const [showSleepTimerMenu, setShowSleepTimerMenu] = useState<boolean>(false);
  const [sleepTimerMinutes, setSleepTimerMinutes] = useState<number | null>(null);
  const [sleepTimerSecondsLeft, setSleepTimerSecondsLeft] = useState<number | null>(null);
  const sleepTimerIntervalRef = useRef<any>(null);

  const startSleepTimer = (minutes: number | null) => {
    if (sleepTimerIntervalRef.current) {
      clearInterval(sleepTimerIntervalRef.current);
      sleepTimerIntervalRef.current = null;
    }
    setShowSleepTimerMenu(false);

    if (!minutes || minutes <= 0) {
      setSleepTimerMinutes(null);
      setSleepTimerSecondsLeft(null);
      showToast("Đã tắt hẹn giờ tắt nhạc", "info");
      return;
    }

    const targetEndTime = Date.now() + minutes * 60 * 1000;
    setSleepTimerMinutes(minutes);
    setSleepTimerSecondsLeft(minutes * 60);
    showToast(`Đã hẹn giờ tắt nhạc sau ${minutes} phút`, "success");

    sleepTimerIntervalRef.current = setInterval(() => {
      const remainingMs = targetEndTime - Date.now();
      if (remainingMs <= 0) {
        clearInterval(sleepTimerIntervalRef.current);
        sleepTimerIntervalRef.current = null;
        setSleepTimerMinutes(null);
        setSleepTimerSecondsLeft(null);
        pausePlayback();
        showToast("Hẹn giờ đã hết. Nhạc đã tự động dừng!", "info");
      } else {
        const remainingSec = Math.ceil(remainingMs / 1000);
        setSleepTimerSecondsLeft(remainingSec);
        setSleepTimerMinutes(Math.ceil(remainingSec / 60));
      }
    }, 1000);
  };

  useEffect(() => {
    return () => {
      if (sleepTimerIntervalRef.current) {
        clearInterval(sleepTimerIntervalRef.current);
      }
    };
  }, []);

  // Sync refs to avoid stale closure in YouTube Player event listeners
  useEffect(() => {
    repeatModeRef.current = repeatMode;
  }, [repeatMode]);

  useEffect(() => {
    playlistRef.current = playlist;
  }, [playlist]);

  useEffect(() => {
    queueTracksRef.current = queueTracks;
  }, [queueTracks]);

  useEffect(() => {
    currentIndexRef.current = currentIndex;
  }, [currentIndex]);

  // Sorting state for Left Sidebar and Right Queue
  const [isLeftSorted, setIsLeftSorted] = useState<boolean>(false);
  const [isQueueSorted, setIsQueueSorted] = useState<boolean>(false);

  // Hamburger popups on player card
  const [showPlayerMenu, setShowPlayerMenu] = useState<boolean>(false);
  const [showLibrarySubmenu, setShowLibrarySubmenu] = useState<boolean>(false);
  const [isCreatingPlaylist, setIsCreatingPlaylist] = useState<boolean>(false);
  const [newPlaylistName, setNewPlaylistName] = useState<string>("");
  const playerMenuRef = useRef<HTMLDivElement | null>(null);

  // Downloaded track IDs (persisted locally)
  const [downloadedTrackIds, setDownloadedTrackIds] = useState<Set<string>>(() => {
    try {
      const saved = localStorage.getItem("kahoii_downloaded_track_ids");
      return saved ? new Set(JSON.parse(saved)) : new Set();
    } catch {
      return new Set();
    }
  });

  // Modern Toast notification
  const [toast, setToast] = useState<{ text: string; type: "info" | "success" | "warning" } | null>(null);
  const toastTimeoutRef = useRef<any>(null);

  const showToast = (text: string, type: "info" | "success" | "warning" = "info") => {
    if (toastTimeoutRef.current) clearTimeout(toastTimeoutRef.current);
    setToast({ text, type });
    toastTimeoutRef.current = setTimeout(() => {
      setToast(null);
    }, 3200);
  };

  // Close player popup menu on click outside
  useEffect(() => {
    const handleClickOutsideMenu = (e: MouseEvent) => {
      if (playerMenuRef.current && !playerMenuRef.current.contains(e.target as Node)) {
        setShowPlayerMenu(false);
        setShowLibrarySubmenu(false);
        setIsCreatingPlaylist(false);
        setNewPlaylistName("");
      }
    };
    document.addEventListener("mousedown", handleClickOutsideMenu);
    return () => document.removeEventListener("mousedown", handleClickOutsideMenu);
  }, []);

  // Save library data to server & cloud
  const persistLibraryData = async (updated: {
    favorites?: Track[];
    history?: Track[];
    playlists?: any[];
  }) => {
    try {
      await fetch(`${API_BASE}/api/user/library`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          favorites: updated.favorites || libraryData.favorites,
          history: updated.history || libraryData.history,
          playlists: updated.playlists || libraryData.playlists,
        }),
      });
    } catch (err) {
      console.warn("Library persist failed:", err);
    }
  };

  // Favorite toggle for current track
  const toggleCurrentFavorite = async () => {
    if (!currentTrack) return;
    if (!userSession?.isLoggedIn) {
      showToast("Vui lòng đăng nhập để lưu bài hát vào danh sách yêu thích!", "warning");
      setIsAccountOpen(true);
      return;
    }

    const isFav = libraryData.favorites.some((t) => t.id === currentTrack.id);
    let updatedFavs: Track[];

    if (isFav) {
      updatedFavs = libraryData.favorites.filter((t) => t.id !== currentTrack.id);
      showToast(`Đã xóa "${currentTrack.title}" khỏi bài hát yêu thích`, "info");
    } else {
      updatedFavs = [currentTrack, ...libraryData.favorites];
      showToast(`Đã thêm "${currentTrack.title}" vào bài hát yêu thích`, "success");
    }

    setLibraryData((prev) => ({ ...prev, favorites: updatedFavs }));
    await persistLibraryData({ favorites: updatedFavs });
  };

  // Download track audio
  const handleDownloadTrack = async (track: Track) => {
    if (!track) return;
    const isDownloaded = downloadedTrackIds.has(track.id);
    if (isDownloaded) {
      setDownloadedTrackIds((prev) => {
        const next = new Set(prev);
        next.delete(track.id);
        try {
          localStorage.setItem("kahoii_downloaded_track_ids", JSON.stringify(Array.from(next)));
        } catch {}
        return next;
      });
      showToast(`Đã gỡ bài hát "${track.title}" khỏi danh sách tải xuống`, "info");
      return;
    }

    setDownloadedTrackIds((prev) => {
      const next = new Set(prev);
      next.add(track.id);
      try {
        localStorage.setItem("kahoii_downloaded_track_ids", JSON.stringify(Array.from(next)));
      } catch {}
      return next;
    });

    showToast(`Đang chuẩn bị tải xuống "${track.title}"...`, "success");

    try {
      const res = await fetch(`${API_BASE}/api/stream?id=${track.id}`);
      if (res.ok) {
        const data = await res.json();
        if (data.url) {
          const link = document.createElement("a");
          link.href = data.url;
          link.target = "_blank";
          link.download = `${track.title} - ${track.author}.mp3`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          showToast(`Đã bắt đầu tải xuống "${track.title}"!`, "success");
          return;
        }
      }
      window.open(`https://www.youtube.com/watch?v=${track.id}`, "_blank");
    } catch {
      window.open(`https://www.youtube.com/watch?v=${track.id}`, "_blank");
    }
  };

  // Create playlist and add current track
  const handleCreatePlaylistAndAddTrack = async () => {
    if (!newPlaylistName.trim() || !currentTrack) return;
    const newPl = {
      id: `pl_${Date.now()}`,
      name: newPlaylistName.trim(),
      created: new Date().toISOString(),
      items: [currentTrack],
    };
    const updatedPlaylists = [newPl, ...libraryData.playlists];
    setLibraryData((prev) => ({ ...prev, playlists: updatedPlaylists }));
    setIsCreatingPlaylist(false);
    setNewPlaylistName("");
    showToast(`Đã tạo playlist "${newPl.name}" và thêm bài hát!`, "success");
    await persistLibraryData({ playlists: updatedPlaylists });
  };

  // Toggle track inside a playlist
  const handleToggleTrackInPlaylist = async (playlistId: string) => {
    if (!currentTrack) return;
    const targetPl = libraryData.playlists.find((pl) => pl.id === playlistId);
    if (!targetPl) return;

    const isInPl = targetPl.items?.some((it: any) => it.id === currentTrack.id);
    let updatedPlaylists: any[];

    if (isInPl) {
      updatedPlaylists = libraryData.playlists.map((pl) => {
        if (pl.id === playlistId) {
          return {
            ...pl,
            items: (pl.items || []).filter((it: any) => it.id !== currentTrack.id),
          };
        }
        return pl;
      });
      showToast(`Đã xóa khỏi playlist "${targetPl.name}"`, "info");
    } else {
      updatedPlaylists = libraryData.playlists.map((pl) => {
        if (pl.id === playlistId) {
          return {
            ...pl,
            items: [currentTrack, ...(pl.items || [])],
          };
        }
        return pl;
      });
      showToast(`Đã thêm vào playlist "${targetPl.name}"`, "success");
    }

    setLibraryData((prev) => ({ ...prev, playlists: updatedPlaylists }));
    await persistLibraryData({ playlists: updatedPlaylists });
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
        setLibraryData({
          favorites: mappedFavs,
          history: mappedHist,
          playlists: rawPlaylists,
        });
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
    fetchUserSession();
    fetchLibraryData();

    // Fetch Live Home Feed (Trending & New Releases)
    fetch(`${API_BASE}/api/home`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) {
          if (Array.isArray(data.trending) && data.trending.length > 0) {
            setTrendingTracks(data.trending);
          }
          if (Array.isArray(data.newReleases) && data.newReleases.length > 0) {
            setNewReleaseTracks(data.newReleases);
          }
        }
      })
      .catch(() => {});

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
            height: "1",
            width: "1",
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
        } catch {}
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
      } catch {}
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

  const handleLogout = async () => {
    if (!window.confirm("Bạn có chắc chắn muốn đăng xuất?")) return;
    try {
      await fetch(`${API_BASE}/api/user/logout`, { method: "POST" });
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

    // Stop current audio and YouTube player
    if (audioRef.current) {
      try {
        audioRef.current.pause();
      } catch {}
    }
    if (ytPlayerRef.current && typeof ytPlayerRef.current.stopVideo === "function") {
      try {
        ytPlayerRef.current.stopVideo();
      } catch {}
    }

    // Strategy 1: Attempt to resolve direct audio stream from server
    let streamPlayed = false;
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 2500); // 2.5s fast check

      const res = await fetch(`${API_BASE}/api/stream?id=${track.id}`, {
        signal: controller.signal,
      });
      clearTimeout(timeoutId);

      if (res.ok) {
        const data = await res.json();
        if (data.streamUrl && audioRef.current) {
          const playableUrl = data.streamUrl.includes("googlevideo.com")
            ? `${API_BASE}/api/proxy-audio?url=${encodeURIComponent(data.streamUrl)}`
            : data.streamUrl;

          audioRef.current.src = playableUrl;
          audioRef.current.load();
          activePlayerType.current = "audio";
          streamPlayed = true;

          if (autoPlay) {
            const playPromise = audioRef.current.play();
            if (playPromise !== undefined) {
              playPromise
                .then(() => setIsPlaying(true))
                .catch(() => setIsPlaying(false));
            }
          } else {
            setIsPlaying(false);
          }
        }
      }
    } catch {
      // Backend stream unavailable or blocked
    }

    // Strategy 2: Zero-failure YouTube Web Player fallback (plays 100% on Vercel)
    if (!streamPlayed) {
      activePlayerType.current = "youtube";
      if (ytPlayerRef.current && typeof ytPlayerRef.current.loadVideoById === "function") {
        try {
          ytPlayerRef.current.loadVideoById({
            videoId: track.id,
            startSeconds: 0,
          });
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
    } catch {}

    setIsLoading(false);
  };

  const handleTimeUpdate = () => {
    if (activePlayerType.current === "audio" && audioRef.current) {
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
      } catch {}
    }

    if (!currentTrack) {
      if (trendingTracks.length > 0) {
        setPlaylist(trendingTracks);
        setQueueTracks(trendingTracks);
        setCurrentIndex(0);
        loadAndPlayTrack(trendingTracks[0], true);
      }
      return;
    }

    if (!audioRef.current) return;
    if (isPlaying) {
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      if (!audioRef.current.src) {
        loadAndPlayTrack(currentTrack, true);
        return;
      }
      const playPromise = audioRef.current.play();
      if (playPromise !== undefined) {
        playPromise
          .then(() => setIsPlaying(true))
          .catch(() => {
            loadAndPlayTrack(currentTrack, true);
          });
      }
    }
  };

  const cycleRepeatMode = () => {
    let nextMode: "all" | "one" | "shuffle" | "off";
    if (repeatMode === "all") nextMode = "one";
    else if (repeatMode === "one") nextMode = "shuffle";
    else if (repeatMode === "shuffle") nextMode = "off";
    else nextMode = "all";

    setRepeatMode(nextMode);
    repeatModeRef.current = nextMode;

    const labelMap = {
      all: "Lặp lại toàn bộ danh sách",
      one: "Lặp lại bài hát hiện tại",
      shuffle: "Phát ngẫu nhiên (Shuffle)",
      off: "Tắt chế độ lặp lại",
    };
    showToast(labelMap[nextMode], "info");
  };

  const handleTrackEnded = () => {
    const mode = repeatModeRef.current;
    const currentList = playlistRef.current.length > 0 ? playlistRef.current : queueTracksRef.current;
    const curIdx = currentIndexRef.current;

    if (currentList.length === 0) return;

    if (mode === "one") {
      // Loop current track at position 0
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.seekTo === "function"
      ) {
        try {
          ytPlayerRef.current.seekTo(0, true);
          ytPlayerRef.current.playVideo();
          setIsPlaying(true);
        } catch {}
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
        audioRef.current.play().catch(() => {});
        setIsPlaying(true);
      }
      return;
    }

    if (mode === "shuffle") {
      if (currentList.length <= 1) {
        loadAndPlayTrack(currentList[0], true);
        return;
      }
      let randIdx = Math.floor(Math.random() * currentList.length);
      let attempts = 0;
      while (randIdx === curIdx && attempts < 10) {
        randIdx = Math.floor(Math.random() * currentList.length);
        attempts++;
      }
      shuffleHistoryRef.current.push(curIdx);
      setCurrentIndex(randIdx);
      loadAndPlayTrack(currentList[randIdx], true);
      return;
    }

    if (mode === "off") {
      if (curIdx >= currentList.length - 1) {
        pausePlayback();
        showToast("Đã phát hết danh sách bài hát", "info");
        return;
      }
      const nextIdx = curIdx + 1;
      setCurrentIndex(nextIdx);
      loadAndPlayTrack(currentList[nextIdx], true);
      return;
    }

    // Default: mode === "all"
    const nextIdx = (curIdx + 1) % currentList.length;
    setCurrentIndex(nextIdx);
    loadAndPlayTrack(currentList[nextIdx], true);
  };

  const handleNext = () => {
    const currentList = playlist.length > 0 ? playlist : queueTracks;
    if (currentList.length === 0) return;

    if (repeatModeRef.current === "shuffle") {
      if (currentList.length <= 1) {
        loadAndPlayTrack(currentList[0], true);
        return;
      }
      let randIdx = Math.floor(Math.random() * currentList.length);
      let attempts = 0;
      while (randIdx === currentIndex && attempts < 10) {
        randIdx = Math.floor(Math.random() * currentList.length);
        attempts++;
      }
      shuffleHistoryRef.current.push(currentIndex);
      setCurrentIndex(randIdx);
      loadAndPlayTrack(currentList[randIdx], true);
      return;
    }

    if (repeatModeRef.current === "off") {
      if (currentIndex + 1 < currentList.length) {
        const nextIdx = currentIndex + 1;
        setCurrentIndex(nextIdx);
        loadAndPlayTrack(currentList[nextIdx], true);
      } else {
        showToast("Đã đến cuối danh sách phát", "info");
      }
      return;
    }

    const nextIdx = (currentIndex + 1) % currentList.length;
    setCurrentIndex(nextIdx);
    loadAndPlayTrack(currentList[nextIdx], true);
  };

  const handlePrev = () => {
    const currentList = playlist.length > 0 ? playlist : queueTracks;
    if (currentList.length === 0) return;

    if (currentTime > 3) {
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.seekTo === "function"
      ) {
        try { ytPlayerRef.current.seekTo(0, true); } catch {}
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
      }
      setCurrentTime(0);
      return;
    }

    if (repeatModeRef.current === "shuffle" && shuffleHistoryRef.current.length > 0) {
      const prevIdx = shuffleHistoryRef.current.pop()!;
      setCurrentIndex(prevIdx);
      loadAndPlayTrack(currentList[prevIdx], true);
      return;
    }

    const prevIdx = (currentIndex - 1 + currentList.length) % currentList.length;
    setCurrentIndex(prevIdx);
    loadAndPlayTrack(currentList[prevIdx], true);
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newTime = parseFloat(e.target.value);
    setCurrentTime(newTime);
    if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
      try { ytPlayerRef.current.seekTo(newTime, true); } catch {}
    } else if (audioRef.current) {
      audioRef.current.currentTime = newTime;
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
    let items: any[] = [];

    if (activeLeftTab === "favorites") {
      const favs = libraryData.favorites;
      if (favs.length > 0) {
        items = favs
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
    } else if (activeLeftTab === "history") {
      const hist = libraryData.history;
      if (hist.length > 0) {
        items = hist
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
    } else if (libraryData.playlists.length > 0) {
      items = libraryData.playlists
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

    if (isLeftSorted && items.length > 0) {
      items = [...items].sort((a, b) => {
        const itemA = {
          title: a.title,
          name: a.rawPlaylist?.name || a.title,
          author: a.rawTrack?.author || "",
        };
        const itemB = {
          title: b.title,
          name: b.rawPlaylist?.name || b.title,
          author: b.rawTrack?.author || "",
        };
        return compareSortItems(itemA, itemB);
      });
    }

    return items;
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

      {/* Hidden YouTube Iframe Container for 100% Reliable Playback */}
      <div
        id="kahoii-yt-iframe"
        style={{
          position: "fixed",
          bottom: -9999,
          right: -9999,
          width: "1px",
          height: "1px",
          opacity: 0.001,
          pointerEvents: "none",
        }}
      />

      {/* ========================================================================= */}
      {/* 1. TOP BAR (Seamless & Centered matching Figma mockup)                     */}
      {/* ========================================================================= */}
      <header className="h-[54px] w-full px-5 flex items-center justify-between flex-shrink-0 z-30">
        {/* Left spacer for perfect balance */}
        <div className="w-[140px]" />

        {/* Center: Search Pill with Search History Suggestions Dropdown */}
        <div ref={searchDropdownRef} className="relative flex-1 max-w-[580px]">
          <form
            onSubmit={handleTopSearchSubmit}
            className="w-full h-[36px] px-4 rounded-full bg-[#390f8d]/60 border border-purple-500/40 hover:border-pink-500/70 focus-within:border-pink-500 flex items-center gap-2.5 shadow-inner transition-all"
          >
            <Search className="w-4 h-4 text-pink-400 flex-shrink-0" />
            <input
              type="text"
              placeholder="Search"
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

        {/* Right: User Avatar Pill Button (Real avatar from database) */}
        <div className="w-[140px] flex justify-end">
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

      {/* Global Interactive Notification Toast */}
      {toast && (
        <div
          className={`absolute top-16 left-1/2 -translate-x-1/2 z-50 px-4 py-2.5 rounded-xl border backdrop-blur-xl shadow-2xl flex items-center gap-2.5 text-xs font-medium animate-fade-in ${
            toast.type === "warning"
              ? "bg-amber-950/90 border-amber-500/60 text-amber-200 shadow-amber-500/20"
              : toast.type === "success"
              ? "bg-emerald-950/90 border-emerald-500/60 text-emerald-200 shadow-emerald-500/20"
              : "bg-purple-950/90 border-pink-500/60 text-pink-200 shadow-pink-500/20"
          }`}
        >
          {toast.type === "warning" && <AlertCircle className="w-4 h-4 text-amber-400 flex-shrink-0" />}
          {toast.type === "success" && <Check className="w-4 h-4 text-emerald-400 flex-shrink-0" />}
          {toast.type === "info" && <Sparkles className="w-4 h-4 text-pink-400 flex-shrink-0" />}
          <span>{toast.text}</span>
          <button
            onClick={() => setToast(null)}
            className="ml-1 text-white/50 hover:text-white cursor-pointer"
          >
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

          {!userSession?.isLoggedIn ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-4 gap-3 bg-white/5 rounded-2xl border border-white/10 my-auto">
              <div className="w-12 h-12 rounded-full bg-pink-500/20 border border-pink-500/40 flex items-center justify-center text-pink-400 shadow-[0_0_15px_rgba(236,72,153,0.3)]">
                <FolderHeart className="w-6 h-6" />
              </div>
              <div>
                <p className="text-sm font-semibold text-white">Thư viện cá nhân</p>
                <p className="text-xs text-white/50 mt-1 leading-relaxed">
                  Đăng nhập để xem danh sách phát, bài hát yêu thích và lịch sử nghe nhạc của bạn.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsAccountOpen(true)}
                className="mt-2 px-4 py-2 rounded-xl bg-gradient-to-r from-pink-500 to-purple-600 text-white text-xs font-semibold hover:opacity-95 active:scale-95 transition-all cursor-pointer shadow-lg shadow-pink-500/30"
              >
                Đăng nhập ngay
              </button>
            </div>
          ) : (
            <>
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
                  onClick={() => setIsLeftSorted(!isLeftSorted)}
                  title={isLeftSorted ? "Tắt sắp xếp (Hiện theo thứ tự mặc định)" : "Sắp xếp (Ký tự khác lên đầu, Latin A-Z)"}
                  className={`p-1 rounded-md transition-all cursor-pointer flex-shrink-0 ${
                    isLeftSorted ? "text-white bg-pink-500/40 shadow-sm" : "text-[#e0007b] hover:text-white"
                  }`}
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
            </>
          )}
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
                    {searchResults.length} bài hát
                  </span>
                )}
              </div>

              {/* Navigation Tabs: Tất cả / Nhạc / Nhạc sĩ / Playlist */}
              <div className="flex items-center gap-2 mb-5 flex-wrap flex-shrink-0">
                {[
                  { key: "all", label: "Tất cả" },
                  {
                    key: "tracks",
                    label: "Nhạc",
                    count: searchStructured?.tracks?.length || searchResults.length,
                  },
                  {
                    key: "artists",
                    label: "Nhạc sĩ",
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
                        setActiveSearchTab(tab.key as "all" | "tracks" | "artists" | "playlists")
                      }
                      className={`px-4 py-1.5 rounded-full text-xs font-semibold transition-all flex items-center gap-2 cursor-pointer ${
                        isActive
                          ? "bg-gradient-to-r from-pink-500 to-purple-600 text-white shadow-lg shadow-pink-500/25 scale-[1.02]"
                          : "bg-white/5 hover:bg-white/10 text-white/70 hover:text-white border border-white/5"
                      }`}
                    >
                      <span>{tab.label}</span>
                      {typeof tab.count === "number" && tab.count > 0 && (
                        <span
                          className={`px-1.5 py-0.2 rounded-full text-[10px] font-mono ${
                            isActive ? "bg-white/20 text-white" : "bg-white/10 text-white/60"
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
                                subtitle: `${searchResults[0].author}${
                                  searchResults[0].duration > 0
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
                          <div className="w-full">
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
                              className="w-full rounded-[20px] p-4 bg-gradient-to-r from-[#2f1647]/95 via-[#1e1335]/95 to-[#140d24]/95 border border-pink-500/35 hover:border-pink-500/70 shadow-xl shadow-pink-500/10 transition-all flex items-center justify-between gap-4 group cursor-pointer"
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
                          <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
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
                                    className={`w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${
                                      isCurrentPlaying
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
                                      className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity duration-200 ${
                                        isCurrentPlaying
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
                                    className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${
                                      isCurrentPlaying
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
                  {activeSearchTab === "tracks" && (
                    <div>
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-[18px] font-bold text-white tracking-wide">
                          Tất cả bài hát ({searchResults.length})
                        </h3>
                      </div>
                      <div className="grid grid-cols-6 w-full" style={{ gap: "16px" }}>
                        {searchResults.map((item) => {
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
                                className={`w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden relative shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${
                                  isCurrentPlaying
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
                                  className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity duration-200 ${
                                    isCurrentPlaying
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
                                className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${
                                  isCurrentPlaying
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
              {/* Section 1: Nhạc Trending */}
              <div>
                <div className="flex items-center justify-between mb-3.5">
                  <div className="flex items-center gap-2.5">
                    <Sparkles className="w-5 h-5 text-pink-400" />
                    <h2 className="text-[20px] font-bold text-white tracking-wide">
                      Nhạc Trending
                    </h2>
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      if (trendingTracks.length > 0) {
                        setPlaylist(trendingTracks);
                        setQueueTracks(trendingTracks);
                        setCurrentIndex(0);
                        loadAndPlayTrack(trendingTracks[0], true);
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
                      onClick={() => {
                        setPlaylist(trendingTracks);
                        setQueueTracks(trendingTracks);
                        const idx = trendingTracks.findIndex((t) => t.id === item.id);
                        setCurrentIndex(idx !== -1 ? idx : 0);
                        loadAndPlayTrack(item, true);
                      }}
                      className="w-full flex flex-col cursor-pointer group"
                    >
                      <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                        <img
                          src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                          alt={item.title}
                          onError={(e) => {
                            e.currentTarget.src = DEFAULT_TRACK_ART;
                          }}
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
                        setPlaylist(newReleaseTracks);
                        setQueueTracks(newReleaseTracks);
                        setCurrentIndex(0);
                        loadAndPlayTrack(newReleaseTracks[0], true);
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
                      onClick={() => {
                        setPlaylist(newReleaseTracks);
                        setQueueTracks(newReleaseTracks);
                        const idx = newReleaseTracks.findIndex((t) => t.id === item.id);
                        setCurrentIndex(idx !== -1 ? idx : 0);
                        loadAndPlayTrack(item, true);
                      }}
                      className="w-full flex flex-col cursor-pointer group"
                    >
                      <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-purple-900/30 shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5">
                        <img
                          src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                          alt={item.title}
                          onError={(e) => {
                            e.currentTarget.src = DEFAULT_TRACK_ART;
                          }}
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
            {/* Real Dynamic Artwork or Idle Standby Display */}
            {!currentTrack ? (
              <div className="w-full h-full flex flex-col items-center justify-center bg-gradient-to-br from-[#240a50] to-[#120324] relative overflow-hidden select-none">
                <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(236,72,153,0.15)_0%,transparent_70%)]" />
                <div className="w-16 h-16 rounded-full bg-pink-500/10 border border-pink-500/30 flex items-center justify-center text-pink-400 shadow-[0_0_25px_rgba(236,72,153,0.25)] mb-2.5">
                  <Music2 className="w-8 h-8" />
                </div>
                <p className="text-xs font-bold text-white tracking-wide">Kahoii Music</p>
                <p className="text-[10.5px] text-white/50 mt-0.5">Sẵn sàng phát nhạc</p>
              </div>
            ) : (
              <img
                src={
                  currentTrack.thumbnail && !currentTrack.thumbnail.includes("figma")
                    ? (currentTrack.thumbnail.includes("i.ytimg.com")
                        ? currentTrack.thumbnail.replace("mqdefault.jpg", "hqdefault.jpg")
                        : currentTrack.thumbnail)
                    : (currentTrack.id ? `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg` : DEFAULT_TRACK_ART)
                }
                alt={currentTrack.title || "Track Artwork"}
                onError={(e) => {
                  e.currentTarget.src = DEFAULT_TRACK_ART;
                }}
                className="w-full h-full object-cover transition-all duration-700 group-hover:scale-105"
              />
            )}

            {/* Dark gradient fade over lower half to mask controls & provide high contrast */}
            <div className="absolute inset-x-0 bottom-0 h-[52%] bg-gradient-to-t from-black/95 via-black/60 to-transparent pointer-events-none z-10" />

            {/* Track Title and Artist floating gently over the gradient */}
            <div className="absolute bottom-[92px] left-4 right-4 z-20 pointer-events-none">
              <p className="text-[13px] font-bold text-white truncate drop-shadow-md">
                {currentTrack ? currentTrack.title : "Chưa chọn bài hát"}
              </p>
              <p className="text-[11px] text-white/70 truncate drop-shadow">
                {currentTrack ? (currentTrack.author || "Kahoii Music") : "Chọn bài hát bất kỳ để phát"}
              </p>
            </div>

            {/* Top-left Menu & Volume Controls */}
            <div className="absolute top-3 left-3 flex items-center gap-1.5 z-20">
              <div className="relative" ref={playerMenuRef}>
                <button
                  type="button"
                  onClick={() => {
                    if (!currentTrack) {
                      showToast("Vui lòng phát một bài hát trước khi lưu hoặc tải xuống!", "warning");
                      return;
                    }
                    setShowPlayerMenu(!showPlayerMenu);
                    if (showPlayerMenu) {
                      setShowLibrarySubmenu(false);
                      setIsCreatingPlaylist(false);
                    }
                  }}
                  className={`w-7 h-7 rounded-lg backdrop-blur-md flex items-center justify-center transition-all cursor-pointer ${
                    showPlayerMenu
                      ? "bg-pink-600 text-white shadow-[0_0_12px_rgba(236,72,153,0.6)]"
                      : "bg-black/40 text-white/80 hover:text-white hover:bg-black/60"
                  }`}
                  title="Tùy chọn bài hát"
                >
                  <Menu className="w-4 h-4" />
                </button>

                {/* Popup 1 (Menu chính: Combobox) */}
                {showPlayerMenu && currentTrack && (
                  <div className="absolute top-9 left-0 z-40 bg-[#160430]/95 border border-pink-500/50 backdrop-blur-xl rounded-xl shadow-2xl p-1.5 min-w-[175px] flex flex-col gap-1 text-xs animate-fade-in">
                    {/* Option 1: Lưu vào thư viện */}
                    <button
                      type="button"
                      onClick={() => {
                        setShowLibrarySubmenu(!showLibrarySubmenu);
                      }}
                      className={`flex items-center justify-between w-full px-2.5 py-2 rounded-lg transition-colors cursor-pointer text-left ${
                        showLibrarySubmenu ? "bg-white/15 text-pink-300" : "hover:bg-white/10 text-white/90"
                      }`}
                    >
                      <span className="flex items-center gap-2">
                        <FolderPlus className="w-4 h-4 text-pink-400" />
                        <span>Lưu vào thư viện</span>
                      </span>
                      <div className="flex items-center gap-1">
                        {libraryData.playlists.some((pl) => pl.items?.some((it: any) => it.id === currentTrack.id)) && (
                          <Check className="w-3.5 h-3.5 text-emerald-400" />
                        )}
                        <ChevronRight className="w-3.5 h-3.5 text-white/40" />
                      </div>
                    </button>

                    {/* Option 2: Tải xuống */}
                    <button
                      type="button"
                      onClick={() => handleDownloadTrack(currentTrack)}
                      className="flex items-center justify-between w-full px-2.5 py-2 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                    >
                      <span className="flex items-center gap-2">
                        <Download className="w-4 h-4 text-pink-400" />
                        <span>Tải xuống</span>
                      </span>
                      {downloadedTrackIds.has(currentTrack.id) && (
                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                      )}
                    </button>
                  </div>
                )}

                {/* Popup 2 (Submenu cạnh bên - side-by-side) */}
                {showPlayerMenu && showLibrarySubmenu && currentTrack && (
                  <div className="absolute top-9 left-[185px] z-40 bg-[#160430]/95 border border-purple-500/50 backdrop-blur-xl rounded-xl shadow-2xl p-2 min-w-[200px] max-w-[230px] flex flex-col gap-1.5 text-xs animate-fade-in">
                    <div className="flex items-center justify-between px-1 pb-1 border-b border-white/10 text-[10px] font-bold uppercase tracking-wider text-purple-300">
                      <span>Thêm vào playlist</span>
                    </div>

                    {!userSession?.isLoggedIn ? (
                      <div className="p-2 text-center flex flex-col gap-2">
                        <p className="text-[11px] text-white/60">Đăng nhập để quản lý danh sách phát của bạn</p>
                        <button
                          type="button"
                          onClick={() => {
                            setShowPlayerMenu(false);
                            setShowLibrarySubmenu(false);
                            setIsAccountOpen(true);
                          }}
                          className="px-3 py-1.5 rounded-lg bg-pink-500 hover:bg-pink-600 text-white text-xs font-semibold cursor-pointer transition-colors"
                        >
                          Đăng nhập ngay
                        </button>
                      </div>
                    ) : (
                      <>
                        {/* Option Tạo playlist mới */}
                        {!isCreatingPlaylist ? (
                          <button
                            type="button"
                            onClick={() => setIsCreatingPlaylist(true)}
                            className="flex items-center gap-1.5 px-2 py-1.5 rounded-lg hover:bg-white/10 text-pink-300 font-medium transition-colors cursor-pointer text-left"
                          >
                            <Plus className="w-3.5 h-3.5" />
                            <span>Tạo playlist mới</span>
                          </button>
                        ) : (
                          <div className="flex items-center gap-1 p-1 bg-black/40 rounded-lg border border-pink-500/40">
                            <input
                              type="text"
                              placeholder="Tên playlist..."
                              value={newPlaylistName}
                              onChange={(e) => setNewPlaylistName(e.target.value)}
                              onKeyDown={(e) => {
                                if (e.key === "Enter") handleCreatePlaylistAndAddTrack();
                              }}
                              className="flex-1 bg-transparent text-[11px] text-white focus:outline-none px-1"
                              autoFocus
                            />
                            <button
                              type="button"
                              onClick={handleCreatePlaylistAndAddTrack}
                              className="p-1 text-emerald-400 hover:text-emerald-300 cursor-pointer"
                              title="Tạo"
                            >
                              <Check className="w-3.5 h-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setIsCreatingPlaylist(false);
                                setNewPlaylistName("");
                              }}
                              className="p-1 text-red-400 hover:text-red-300 cursor-pointer"
                              title="Hủy"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        )}

                        <div className="h-[1px] bg-white/10 my-0.5" />

                        {/* List of existing playlists */}
                        <div className="max-h-40 overflow-y-auto flex flex-col gap-1 pr-0.5">
                          {libraryData.playlists.length > 0 ? (
                            libraryData.playlists.map((pl) => {
                              const isInPl = pl.items?.some((it: any) => it.id === currentTrack.id);
                              return (
                                <button
                                  key={pl.id}
                                  type="button"
                                  onClick={() => handleToggleTrackInPlaylist(pl.id)}
                                  className="flex items-center justify-between w-full px-2 py-1.5 rounded-lg hover:bg-white/10 text-white/90 transition-colors text-left group cursor-pointer"
                                >
                                  <span className="truncate max-w-[140px] text-[11.5px]">{pl.name}</span>
                                  {isInPl && <Check className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0 ml-1" />}
                                </button>
                              );
                            })
                          ) : (
                            <span className="text-[11px] text-white/40 italic px-2 py-1">Chưa có playlist nào</span>
                          )}
                        </div>
                      </>
                    )}
                  </div>
                )}
              </div>

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
            <button
              type="button"
              disabled={!currentTrack}
              onClick={toggleCurrentFavorite}
              title={
                !currentTrack
                  ? "Chưa phát bài hát nào"
                  : !userSession?.isLoggedIn
                  ? "Đăng nhập để yêu thích"
                  : libraryData.favorites.some((t) => t.id === currentTrack.id)
                  ? "Bỏ yêu thích"
                  : "Thêm vào yêu thích"
              }
              className={`absolute top-3 right-3 w-8 h-8 rounded-lg backdrop-blur-md flex items-center justify-center z-20 transition-all ${
                !currentTrack
                  ? "opacity-30 cursor-not-allowed bg-black/20"
                  : "bg-black/40 hover:bg-black/60 cursor-pointer group"
              }`}
            >
              <Heart
                className={`w-5 h-5 transition-transform ${
                  !currentTrack
                    ? "text-white/40"
                    : userSession?.isLoggedIn && libraryData.favorites.some((t) => t.id === currentTrack.id)
                    ? "fill-red-500 text-red-500 drop-shadow-[0_0_10px_rgba(239,68,68,0.9)] group-hover:scale-115"
                    : "text-white/60 hover:text-red-400 group-hover:scale-115"
                }`}
              />
            </button>

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
              {/* Loop / Shuffle Button */}
              <button
                type="button"
                onClick={cycleRepeatMode}
                title={
                  repeatMode === "all"
                    ? "Lặp danh sách"
                    : repeatMode === "one"
                    ? "Lặp 1 bài"
                    : repeatMode === "shuffle"
                    ? "Phát ngẫu nhiên"
                    : "Tắt lặp lại"
                }
                className={`p-1.5 transition-all cursor-pointer hover:scale-115 active:scale-90 ${
                  repeatMode === "all"
                    ? "text-pink-400 drop-shadow-[0_0_8px_rgba(236,72,153,0.6)]"
                    : repeatMode === "one"
                    ? "text-pink-400 drop-shadow-[0_0_8px_rgba(236,72,153,0.6)]"
                    : repeatMode === "shuffle"
                    ? "text-purple-300 drop-shadow-[0_0_8px_rgba(168,85,247,0.8)]"
                    : "text-white/40 hover:text-white"
                }`}
              >
                {repeatMode === "one" ? (
                  <Repeat1 className="w-5 h-5" />
                ) : repeatMode === "shuffle" ? (
                  <Shuffle className="w-5 h-5" />
                ) : (
                  <Repeat className="w-5 h-5" />
                )}
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
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setShowSleepTimerMenu(!showSleepTimerMenu)}
                  title={
                    sleepTimerSecondsLeft
                      ? `Hẹn giờ tắt nhạc: còn ${Math.floor(sleepTimerSecondsLeft / 60)}p ${sleepTimerSecondsLeft % 60}s`
                      : "Hẹn giờ tắt nhạc"
                  }
                  className={`p-1.5 transition-all cursor-pointer hover:scale-110 active:scale-90 relative ${
                    sleepTimerSecondsLeft ? "text-pink-400 font-bold" : "text-white/60 hover:text-white"
                  }`}
                >
                  <Clock className="w-5 h-5" />
                  {sleepTimerMinutes && (
                    <span className="absolute -top-1 -right-1 px-1 rounded-full bg-pink-500 text-[8px] text-white font-mono font-bold animate-pulse">
                      {sleepTimerMinutes}m
                    </span>
                  )}
                </button>
                {showSleepTimerMenu && (
                  <div className="absolute bottom-9 right-0 p-2 rounded-xl bg-black/95 border border-pink-500/50 backdrop-blur-xl shadow-2xl flex flex-col gap-1 z-30 min-w-[130px] animate-fade-in">
                    <span className="text-[10px] uppercase font-bold text-pink-400 tracking-wider px-2 py-0.5">
                      Hẹn giờ tắt
                    </span>
                    {[15, 30, 45, 60].map((m) => (
                      <button
                        key={m}
                        type="button"
                        onClick={() => startSleepTimer(m)}
                        className={`text-left text-xs px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer ${
                          sleepTimerMinutes === m
                            ? "bg-pink-500 text-white font-bold"
                            : "hover:bg-white/10 text-white/80"
                        }`}
                      >
                        {m} phút
                      </button>
                    ))}
                    {sleepTimerMinutes && (
                      <button
                        type="button"
                        onClick={() => startSleepTimer(null)}
                        className="text-left text-xs px-2.5 py-1.5 rounded-lg hover:bg-red-500/20 text-red-400 font-semibold transition-colors cursor-pointer border-t border-white/10 mt-1"
                      >
                        Tắt hẹn giờ
                      </button>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Bottom Part: Queue Header with Track Count & Sorter */}
          <div className="flex items-center justify-between px-1 pb-2 flex-shrink-0">
            <span className="text-xs font-semibold text-white/70">
              Hàng đợi phát {queueTracks.length > 0 && `(${queueTracks.length})`}
            </span>
            {queueTracks.length > 0 && (
              <button
                type="button"
                onClick={() => {
                  const nextSorted = !isQueueSorted;
                  setIsQueueSorted(nextSorted);
                  if (nextSorted) {
                    setQueueTracks((prev) => [...prev].sort(compareSortItems));
                  } else {
                    setQueueTracks(playlist.length > 0 ? [...playlist] : [...queueTracks]);
                  }
                }}
                title={isQueueSorted ? "Tắt sắp xếp (Hiện theo thứ tự mặc định)" : "Sắp xếp (Ký tự khác lên đầu, Latin A-Z)"}
                className={`flex items-center gap-1 px-2 py-0.5 rounded-lg border transition-all text-[11px] cursor-pointer ${
                  isQueueSorted
                    ? "border-pink-500 bg-pink-500/30 text-pink-300 shadow-[0_0_10px_rgba(236,72,153,0.4)]"
                    : "border-white/10 hover:border-white/30 text-white/60 hover:text-white"
                }`}
              >
                <ArrowDownAZ className="w-3.5 h-3.5" />
                <span>Sort</span>
              </button>
            )}
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
              <div className="flex-1 flex flex-col items-center justify-center py-12 text-center text-white/40 text-xs gap-2">
                <ListMusic className="w-8 h-8 opacity-30" />
                <p className="font-medium">Hàng đợi đang trống</p>
                <p className="text-[11px] text-white/30">Chọn bài hát bất kỳ từ trang chủ để bắt đầu nghe nhạc</p>
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
