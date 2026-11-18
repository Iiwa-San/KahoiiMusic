import React, { useState, useEffect, useRef, useMemo, useCallback } from "react";
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
  Languages,
  Flame,
  Mic2,
  FolderPlus,
  FolderHeart,
  ListMusic,
  ChevronLeft,
  ChevronDown,
  Compass,
  Download,
  Plus,
  Music,
  Laptop,
  Smartphone,
  Tablet,
  Puzzle,
  ExternalLink,
  HardDriveDownload,
  WifiOff,
  Trash2,
} from "lucide-react";
import type {
  Track,
  StructuredSearchResult,
  SearchPlaylistItem,
  OfflineTrackItem,
} from "./types";
import {
  saveTrackOffline,
  getOfflineTrack,
  getAllOfflineTracks,
  deleteOfflineTrack,
  formatBytes,
} from "./utils/offlineStorage";
import { formatTime, parseLrc } from "./utils/lyricsParser";
import { loadAndExtractTheme, type ThemePalette, DEFAULT_THEME } from "./utils/colorExtractor";

// Official App Logo & Favicon placeholder
const DEFAULT_TRACK_ART = "./logo_icon.png";

// Base API URL for flexible deployment (GitHub Pages + Remote Backend or Monolithic Express)
const API_BASE = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/$/, "");
const STORAGE_USER_SESSION_KEY = "kahoii_user_session";
const STORAGE_LIBRARY_KEY = "kahoii_cached_library";

const getAuthSession = (): { idToken: string | null; refreshToken: string | null } => {
  try {
    const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
    if (!raw) return { idToken: null, refreshToken: null };
    const parsed = JSON.parse(raw);
    return {
      idToken: parsed?.user?.idToken || parsed?.idToken || null,
      refreshToken: parsed?.user?.refreshToken || parsed?.refreshToken || null,
    };
  } catch {
    return { idToken: null, refreshToken: null };
  }
};

const getAuthToken = (): string | null => getAuthSession().idToken;
const getRefreshToken = (): string | null => getAuthSession().refreshToken;

// Helper to make authenticated requests with transparent token auto-refresh & 401 retry
const authenticatedFetch = async (url: string, options: RequestInit = {}): Promise<Response> => {
  let { idToken, refreshToken } = getAuthSession();
  const headers = new Headers(options.headers || {});
  if (idToken) {
    headers.set("Authorization", `Bearer ${idToken}`);
  }
  if (refreshToken) {
    headers.set("X-Refresh-Token", refreshToken);
  }

  let res = await fetch(url, { ...options, headers });

  // If 401 Unauthorized and we have a refreshToken, proactively refresh and retry the request once!
  if (res.status === 401 && refreshToken) {
    try {
      const refreshRes = await fetch(`${API_BASE}/api/user/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-Refresh-Token": refreshToken },
        body: JSON.stringify({ refreshToken }),
      });
      if (refreshRes.ok) {
        const refreshData = await refreshRes.json();
        if (refreshData?.idToken) {
          idToken = refreshData.idToken;
          refreshToken = refreshData.refreshToken || refreshToken;

          // Save new tokens into localStorage immediately
          try {
            const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
            if (raw) {
              const session = JSON.parse(raw);
              if (session && session.user) {
                session.user.idToken = idToken;
                session.user.refreshToken = refreshToken;
                localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(session));
              }
            }
          } catch {}

          // Retry the request with the fresh token
          headers.set("Authorization", `Bearer ${idToken}`);
          if (refreshToken) {
            headers.set("X-Refresh-Token", refreshToken);
          }
          res = await fetch(url, { ...options, headers });
        }
      }
    } catch (err) {
      console.warn("[authenticatedFetch] Auto-refresh retry error:", err);
    }
  }

  // If server auto-refreshed the token, capture it and update localStorage
  const refreshedIdToken = res.headers.get("x-refreshed-id-token");
  const refreshedRefreshToken = res.headers.get("x-refreshed-refresh-token");
  if (refreshedIdToken) {
    try {
      const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
      if (raw) {
        const session = JSON.parse(raw);
        if (session && session.user) {
          session.user.idToken = refreshedIdToken;
          if (refreshedRefreshToken) {
            session.user.refreshToken = refreshedRefreshToken;
          }
          localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(session));
        }
      }
    } catch {}
  }

  return res;
};

// Initial Curated Tracks (Empty by default, skeleton loader displays until live data arrives)
const INITIAL_TRENDING_TRACKS: Track[] = [];
const INITIAL_NEW_RELEASES: Track[] = [];

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

type LeftTab = "library" | "favorites" | "offline" | "history";

const SIDEBAR_TABS: { key: LeftTab; label: string }[] = [
  { key: "library", label: "Thư viện" },
  { key: "favorites", label: "Yêu thích" },
  { key: "offline", label: "Đã tải" },
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


// Smart Artist Extractor: extracts artist from title, description/author, or channel name
function formatTrackArtist(title?: string, author?: string): string {
  const cleanAuthor = (author || "").trim();
  const rawTitle = (title || "").trim();

  // Check if author is a generic placeholder
  const isGeneric =
    !cleanAuthor ||
    cleanAuthor === "Kahoii Music" ||
    cleanAuthor === "YouTube Music" ||
    cleanAuthor === "Nghệ sĩ" ||
    cleanAuthor === "Artist" ||
    cleanAuthor === "Various Artists";

  // 1. Try extracting artist from title with smart patterns
  if (rawTitle) {
    // " / ": e.g. "『ラベンダー』 / MIMI feat. saewool (Music Video)" -> "MIMI feat. saewool"
    // "私は、私達は / Guiano - Coverd by しほ" -> "Guiano - Coverd by しほ"
    // "【MV】「言っといてよ」 / 9Lana" -> "9Lana"
    if (rawTitle.includes(" / ")) {
      const parts = rawTitle.split(" / ");
      if (parts.length >= 2) {
        let cand = parts[1].replace(/\(Music Video\)/gi, "").replace(/【.*?】/g, "").replace(/\(.*?\)/g, "").trim();
        if (cand) return cand;
      }
    }

    // " - ": e.g. "Orangestar - 花筏 (feat. 夏背)" -> "Orangestar"
    if (rawTitle.includes(" - ")) {
      const parts = rawTitle.split(" - ");
      if (parts.length >= 2) {
        let cand = parts[0].trim();
        cand = cand.replace(/^\[.*?\]\s*/, "").replace(/^【.*?】\s*/, "").trim();
        if (cand && !/^(mv|official|lyric|audio|video|teaser|hd|4k)$/i.test(cand)) {
          return cand;
        }
      }
    }

    // " | ": e.g. "Title | Artist"
    if (rawTitle.includes(" | ")) {
      const parts = rawTitle.split(" | ");
      if (parts.length >= 2) {
        const cand = parts[1].trim();
        if (cand) return cand;
      }
    }

    // "Covered by X" / "Cover by X"
    const mCover = rawTitle.match(/(?:covered\s+by|cover\s+by|vocal\s*:?)\s*([^\s\(\)\[\]【】\/|]+)/i);
    if (mCover && mCover[1]) {
      return mCover[1].trim();
    }

    // Japanese quotes: 「Track」Artist or Artist「Track」
    const mQuotes = rawTitle.match(/([^\s\u300C\u300E\u3010]+)\s*[\u300C\u300E\u3010][^\u300D\u300F\u3011]+[\u300D\u300F\u3011]/);
    if (mQuotes && mQuotes[1] && !/^(mv|official|video|audio|lyrics?|弾き語り|歌ってみた)$/i.test(mQuotes[1])) {
      return mQuotes[1].trim();
    }

    // Check brackets like 【Artist】 if not standard tags
    const bracketMatch = rawTitle.match(/【([^】]+)】/);
    if (bracketMatch && bracketMatch[1] && !/歌ってみた|弾き語り|MV|Official|Lyric|Audio|HD|4K/i.test(bracketMatch[1])) {
      return bracketMatch[1].trim();
    }

    // Hashtag #Artist (e.g. #椎名林檎, #Michita, #KingGnu)
    const hashMatch = rawTitle.match(/#([^\s#]+)/);
    if (hashMatch && hashMatch[1]) {
      return hashMatch[1].trim();
    }
  }

  // 2. If not found in title, use author (channel name / uploader / description) if not generic
  if (!isGeneric && cleanAuthor) {
    return cleanAuthor;
  }

  // 3. Fallback: Never return generic "Nghệ sĩ" or placeholder
  return "";
}

interface UnifiedPlayOverlayProps {
  isCurrent: boolean;
  isPlaying: boolean;
  isLoading?: boolean;
  onToggle: (e: React.MouseEvent) => void;
  size?: "sm" | "md" | "lg";
  className?: string;
  buttonClassName?: string;
}

const UnifiedPlayOverlay: React.FC<UnifiedPlayOverlayProps> = ({
  isCurrent,
  isPlaying,
  isLoading = false,
  onToggle,
  size = "md",
  className = "",
  buttonClassName = "",
}) => {
  const sizeClasses = {
    sm: "w-8 h-8",
    md: "w-10 h-10",
    lg: "w-12 h-12",
  }[size];

  const iconSizes = {
    sm: "w-4 h-4",
    md: "w-5 h-5",
    lg: "w-6 h-6",
  }[size];

  return (
    <div
      className={`absolute inset-0 bg-black/45 flex items-center justify-center transition-all duration-200 z-10 ${
        isCurrent ? "opacity-100 backdrop-blur-[1px]" : "opacity-0 group-hover:opacity-100"
      } ${className}`}
    >
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onToggle(e);
        }}
        className={`${sizeClasses} rounded-full theme-btn-primary flex items-center justify-center shadow-xl text-white hover:scale-110 active:scale-95 transition-all duration-150 cursor-pointer ${buttonClassName}`}
        title={isLoading ? "Đang tải..." : isCurrent && isPlaying ? "Tạm dừng" : "Phát"}
      >
        {isLoading ? (
          <Loader2 className={`${iconSizes} animate-spin text-white`} />
        ) : isCurrent && isPlaying ? (
          <Pause className={`${iconSizes} fill-current text-white`} />
        ) : (
          <Play className={`${iconSizes} fill-current text-white translate-x-0.5`} />
        )}
      </button>

    </div>
  );
};

export default function App() {
  const [playlist, setPlaylist] = useState<Track[]>([]);
  const [queueTracks, setQueueTracks] = useState<Track[]>([]);
  const [currentIndex, setCurrentIndex] = useState<number>(0);
  const [currentTrack, setCurrentTrack] = useState<Track | null>(null);
  const currentTrackRef = useRef<Track | null>(null);
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
  const isTransitioningRef = useRef<boolean>(false);
  const handleTrackEndedRef = useRef<() => void>(() => {});
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Mobile Navigation Tab: 'explore' (Center) | 'library' (Left Sidebar) | 'player' (Right Player)
  const [mobileTab, setMobileTab] = useState<"explore" | "library" | "player">("explore");

  // Playback Debounce Timer & Abort Controller (Prevents rapid track spam stutter)
  const playDebounceTimerRef = useRef<any>(null);
  const playAbortControllerRef = useRef<AbortController | null>(null);

  // Home Discovery Feed (Trending & New Releases)
  const [isHomeLoading, setIsHomeLoading] = useState<boolean>(true);
  const [isLibraryLoading, setIsLibraryLoading] = useState<boolean>(true);
  const [trendingTracks, setTrendingTracks] = useState<Track[]>([]);
  const [newReleaseTracks, setNewReleaseTracks] = useState<Track[]>([]);

  // Dual-Engine Playback (Audio Element + Zero-Fail YouTube Iframe Player Fallback)
  const ytPlayerRef = useRef<any>(null);
  const activePlayerType = useRef<"audio" | "youtube">("youtube");
  const [_lyrics, setLyrics] = useState<any>(null);

  // Left Sidebar
  const [activeLeftTab, setActiveLeftTab] = useState<LeftTab>("library");
  const [leftSearchQuery, setLeftSearchQuery] = useState<string>("");
  const activeTabIndex = Math.max(0, SIDEBAR_TABS.findIndex((t) => t.key === activeLeftTab));

  // Offline Music & PWA Management (YouTube-Style In-Browser Local Storage)
  const [offlineTracks, setOfflineTracks] = useState<OfflineTrackItem[]>([]);
  const [offlineTrackIds, setOfflineTrackIds] = useState<Set<string>>(new Set());
  const [savingOfflineTrackId, setSavingOfflineTrackId] = useState<string | null>(null);
  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== "undefined" ? navigator.onLine : true);
  const [deferredPrompt, setDeferredPrompt] = useState<any>(null);

  // User Profile & Real Database Session (Secured & Isolated per-user)
  const [isAccountOpen, setIsAccountOpen] = useState<boolean>(false);
  const [userSession, setUserSession] = useState<{
    isLoggedIn: boolean;
    user: {
      localId?: string;
      email?: string;
      name?: string;
      avatarUrl?: string;
      avatarBase64?: string | null;
      idToken?: string;
      refreshToken?: string;
      stats?: {
        favoritesCount: number;
        savedTracksCount?: number;
        historyCount: number;
        playlistsCount: number;
      };
    } | null;
  } | null>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed?.isLoggedIn && (parsed?.user?.idToken || parsed?.idToken)) {
          return parsed;
        }
      }
    } catch {}
    return null;
  });

  const [libraryData, setLibraryData] = useState<{
    favorites: Track[];
    savedTracks: Track[];
    history: Track[];
    playlists: any[];
  }>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_LIBRARY_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        return {
          favorites: Array.isArray(parsed.favorites) ? parsed.favorites : [],
          savedTracks: Array.isArray(parsed.savedTracks) ? parsed.savedTracks : [],
          history: Array.isArray(parsed.history) ? parsed.history : [],
          playlists: Array.isArray(parsed.playlists) ? parsed.playlists : [],
        };
      }
    } catch {}
    return {
      favorites: [],
      savedTracks: [],
      history: [],
      playlists: [],
    };
  });

  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [syncSuccessMsg, setSyncSuccessMsg] = useState<string | null>(null);
  const [isLoggingIn, setIsLoggingIn] = useState<boolean>(false);
  const [isRegistering, setIsRegistering] = useState<boolean>(false);
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [loginEmail, setLoginEmail] = useState<string>("");
  const [loginPassword, setLoginPassword] = useState<string>("");
  const [registerConfirmPassword, setRegisterConfirmPassword] = useState<string>("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [showLogoutConfirm, setShowLogoutConfirm] = useState<boolean>(false);
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

  // Artist Detail Session State
  const [selectedArtist, setSelectedArtist] = useState<{
    id?: string;
    name: string;
    avatar: string;
    subscribers?: string;
    genre?: string;
  } | null>(null);
  const [isArtistLoading, setIsArtistLoading] = useState<boolean>(false);
  const [artistTracks, setArtistTracks] = useState<Track[]>([]);
  const [artistPlaylists, setArtistPlaylists] = useState<SearchPlaylistItem[]>([]);
  const [activeArtistTab, setActiveArtistTab] = useState<"all" | "singles" | "playlists">("all");

  // Playlist and Library playback tracking state
  const [loadingPlaylistId, setLoadingPlaylistId] = useState<string | null>(null);
  const [activePlayingPlaylistId, setActivePlayingPlaylistId] = useState<string | null>(null);
  const [activeLibraryPlaylistId, setActiveLibraryPlaylistId] = useState<string | null>(null);

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
    setSelectedArtist(null);
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
    // If this playlist is already the active playlist, toggle play/pause
    if (activePlayingPlaylistId === playlistItem.id) {
      togglePlayPause();
      return;
    }

    setLoadingPlaylistId(playlistItem.id);
    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/playlist?id=${encodeURIComponent(playlistItem.id)}`);
      const data = await res.json();
      if (data.tracks && data.tracks.length > 0) {
        setPlaylist(data.tracks);
        setQueueTracks(data.tracks);
        setCurrentIndex(0);
        currentIndexRef.current = 0;
        setActivePlayingPlaylistId(playlistItem.id);
        loadAndPlayTrack(data.tracks[0], true);
      } else {
        executeSearch(playlistItem.title);
      }
    } catch {
      executeSearch(playlistItem.title);
    } finally {
      setLoadingPlaylistId(null);
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

  useEffect(() => {
    currentTrackRef.current = currentTrack;
  }, [currentTrack]);

  useEffect(() => {
    handleTrackEndedRef.current = handleTrackEnded;
  });

  // Sorting state for Left Sidebar and Right Queue
  const [isLeftSorted, setIsLeftSorted] = useState<boolean>(false);
  const [isQueueSorted, setIsQueueSorted] = useState<boolean>(false);

  // Right Sidebar (Sidebar 2) Tabs: Queue (Hàng đợi) vs Lyrics (Lời bài hát)
  const [sidebar2Tab, setSidebar2Tab] = useState<"queue" | "lyrics">("queue");
  const activeLyricRef = useRef<HTMLDivElement | null>(null);
  const lyricsContainerRef = useRef<HTMLDivElement | null>(null);

  // Romanization / Translation display mode: "dual" (song ngữ) | "original" (chữ gốc) | "romanized" (phiên âm thuần)
  const [lyricDisplayMode, setLyricDisplayMode] = useState<"dual" | "original" | "romanized">(() => {
    try {
      return (localStorage.getItem("kahoii_lyric_display_mode") as any) || "dual";
    } catch {
      return "dual";
    }
  });

  const ensureEnrichedLyrics = useCallback(async () => {
    if (!currentTrack?.id) return;
    if (_lyrics?.lines && _lyrics.lines.length > 0 && _lyrics.lines.some((l: any) => l.romanized || l.translation)) {
      return;
    }
    try {
      const res = await fetch(
        `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(currentTrack.id)}&title=${encodeURIComponent(currentTrack.title)}&artist=${encodeURIComponent(currentTrack.author || "")}&duration=${currentTrack.duration || ""}`
      );
      if (res.ok) {
        const lyricsData = await res.json();
        if (lyricsData?.lines && lyricsData.lines.length > 0) {
          setLyrics(lyricsData);
          if (lyricsData.artist && (!currentTrack.author || currentTrack.author === "Nghệ sĩ" || currentTrack.author === "Kahoii Music")) {
            setCurrentTrack((prev) => (prev && prev.id === currentTrack.id ? { ...prev, author: lyricsData.artist } : prev));
          }
        }
      }
    } catch {}
  }, [currentTrack, _lyrics]);

  const toggleLyricDisplayMode = () => {
    ensureEnrichedLyrics();
    setLyricDisplayMode((prev) => {
      const next = prev === "dual" ? "romanized" : prev === "romanized" ? "original" : "dual";
      try {
        localStorage.setItem("kahoii_lyric_display_mode", next);
      } catch {}
      return next;
    });
  };

  const parsedLyrics = useMemo(() => {
    if (_lyrics?.lines && _lyrics.lines.length > 0) {
      return _lyrics.lines as { time: number; text: string; romanized?: string; translation?: string }[];
    }
    if (!_lyrics?.syncedLyrics) return [];
    return parseLrc(_lyrics.syncedLyrics);
  }, [_lyrics]);

  const isLyricsSynced = useMemo(() => {
    return Boolean(_lyrics?.syncedLyrics) || (parsedLyrics.length > 0 && parsedLyrics.some((l) => l.time > 0));
  }, [_lyrics, parsedLyrics]);

  const activeLyricIndex = useMemo(() => {
    if (!parsedLyrics.length || !isLyricsSynced) return -1;
    let idx = -1;
    for (let i = 0; i < parsedLyrics.length; i++) {
      if (parsedLyrics[i].time <= currentTime) {
        idx = i;
      } else {
        break;
      }
    }
    return idx;
  }, [parsedLyrics, currentTime, isLyricsSynced]);

  const romaziPillLabel = useMemo(() => {
    const lang = _lyrics?.detectedLang;
    const isJa = lang === "ja";
    const isZh = lang === "zh";

    if (lyricDisplayMode === "dual") {
      return isJa ? "Song ngữ (Romaji)" : isZh ? "Song ngữ (Pinyin)" : "Song ngữ";
    }
    if (lyricDisplayMode === "romanized") {
      return isJa ? "Romaji" : isZh ? "Pinyin" : "Bản dịch";
    }
    return "Chữ gốc";
  }, [_lyrics, lyricDisplayMode]);

  const scrollToActiveLyric = useCallback((smooth = true) => {
    if (sidebar2Tab !== "lyrics" || !lyricsContainerRef.current || !activeLyricRef.current) return;
    const container = lyricsContainerRef.current;
    const activeEl = activeLyricRef.current;

    const containerRect = container.getBoundingClientRect();
    const activeRect = activeEl.getBoundingClientRect();

    // Exact relative top offset inside lyricsContainer
    const relativeTop = activeRect.top - containerRect.top;
    const targetScroll = container.scrollTop + relativeTop - (container.clientHeight / 2) + (activeRect.height / 2);

    container.scrollTo({
      top: Math.max(0, targetScroll),
      behavior: smooth ? "smooth" : "auto",
    });
  }, [sidebar2Tab]);

  useEffect(() => {
    if (sidebar2Tab !== "lyrics" || activeLyricIndex < 0) return;
    const animId = requestAnimationFrame(() => {
      scrollToActiveLyric(true);
    });
    return () => cancelAnimationFrame(animId);
  }, [activeLyricIndex, sidebar2Tab, lyricDisplayMode, _lyrics, scrollToActiveLyric]);

  // Reset lyrics scroll to 0 whenever lyrics are cleared or empty
  useEffect(() => {
    if (lyricsContainerRef.current && (parsedLyrics.length === 0 || !_lyrics)) {
      lyricsContainerRef.current.scrollTop = 0;
    }
  }, [parsedLyrics.length, _lyrics, currentTrack?.id]);

  // Automatically fetch/refresh full enriched lyrics whenever currentTrack changes
  useEffect(() => {
    if (!currentTrack?.id) return;
    ensureEnrichedLyrics();
  }, [currentTrack?.id, ensureEnrichedLyrics]);

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
      if (downloadMenuRef.current && !downloadMenuRef.current.contains(e.target as Node)) {
        setShowDownloadMenu(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutsideMenu);
    return () => document.removeEventListener("mousedown", handleClickOutsideMenu);
  }, []);

  // Multi-platform App Downloads (Auto-synced with latest GitHub releases)
  const [showDownloadMenu, setShowDownloadMenu] = useState<boolean>(false);
  const downloadMenuRef = useRef<HTMLDivElement | null>(null);

  const [downloadData, setDownloadData] = useState<{
    pc: { version: string; fileName: string; downloadUrl: string; size?: string };
    android: { version: string; fileName: string; downloadUrl: string; size?: string };
    ios: { version: string; fileName: string; downloadUrl: string; size?: string };
    extension: { version: string; fileName: string; downloadUrl: string; size?: string };
  }>({
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
    const fetchLatestDownloads = async () => {
      try {
        const res = await fetch(`${API_BASE}/api/downloads`);
        if (res.ok) {
          const json = await res.json();
          if (json?.pc?.downloadUrl) {
            setDownloadData(json);
            return;
          }
        }
        // Direct GitHub API fallback
        const ghRes = await fetch("https://api.github.com/repos/Iiwa-San/KahoiiMusic/releases");
        if (ghRes.ok) {
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

          const formatBytes = (bytes: number) => {
            if (!bytes) return "";
            return (bytes / (1024 * 1024)).toFixed(1) + " MB";
          };

          setDownloadData({
            pc: {
              version: pcRelease?.tag_name || "v1.0.34",
              fileName: pcAsset?.name || "KahoiiMusic_Setup.exe",
              downloadUrl:
                pcAsset?.browser_download_url ||
                "https://github.com/Iiwa-San/KahoiiMusic/releases/download/v1.0.34/KahoiiMusic_Setup_v1.0.34.exe",
              size: pcAsset?.size ? formatBytes(pcAsset.size) : "24.2 MB",
            },
            android: {
              version: "Bản mới nhất",
              fileName: androidAsset?.name || "KahoiiMusic.apk",
              downloadUrl:
                androidAsset?.browser_download_url ||
                "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/KahoiiMusic.apk",
              size: androidAsset?.size ? formatBytes(androidAsset.size) : "14.6 MB",
            },
            ios: {
              version: "Bản mới nhất",
              fileName: iosAsset?.name || "KahoiiMusic.ipa",
              downloadUrl:
                iosAsset?.browser_download_url ||
                "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-ios/KahoiiMusic.ipa",
              size: iosAsset?.size ? formatBytes(iosAsset.size) : "1.4 MB",
            },
            extension: {
              version: "Bản mới nhất",
              fileName: extAsset?.name || "Extension.zip",
              downloadUrl:
                extAsset?.browser_download_url ||
                "https://github.com/Iiwa-San/KahoiiMusic/archive/refs/heads/Extension.zip",
              size: "Source ZIP",
            },
          });
        }
      } catch {}
    };

    fetchLatestDownloads();
  }, []);

  // Load Offline Tracks from IndexedDB on startup & attach online/offline/pwa event listeners
  useEffect(() => {
    getAllOfflineTracks()
      .then((items) => {
        setOfflineTracks(items);
        setOfflineTrackIds(new Set(items.map((it) => it.id)));
      })
      .catch((err) => {
        console.warn("[Offline DB] Failed to load offline tracks:", err);
      });

    const handleOnline = () => {
      setIsOnline(true);
      showToast("Đã khôi phục kết nối Internet", "success");
    };
    const handleOffline = () => {
      setIsOnline(false);
      showToast("Bạn đang ngoại tuyến. Các bài hát trong mục 'Đã tải' vẫn phát bình thường!", "info");
    };

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    const handleBeforeInstall = (e: any) => {
      e.preventDefault();
      setDeferredPrompt(e);
    };
    window.addEventListener("beforeinstallprompt", handleBeforeInstall);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      window.removeEventListener("beforeinstallprompt", handleBeforeInstall);
    };
  }, []);

  const offlineTotalBytes = offlineTracks.reduce(
    (sum, item) => sum + (item.sizeBytes || item.audioBlob?.size || 0),
    0
  );

  // Install PWA
  const handleInstallPwa = async () => {
    if (deferredPrompt) {
      try {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        if (outcome === "accepted") {
          showToast("Đang cài đặt ứng dụng Kahoii Music...", "success");
        }
      } catch {}
      setDeferredPrompt(null);
    } else {
      showToast("Để cài đặt: Bấm biểu tượng 'Cài đặt' trên thanh địa chỉ URL của trình duyệt!", "info");
    }
  };

  // Play All Offline Tracks
  const handlePlayAllOffline = () => {
    if (offlineTracks.length === 0) return;
    const tracks: Track[] = offlineTracks.map((it) => ({
      id: it.id,
      title: it.title,
      author: it.author,
      duration: it.duration,
      durationText: it.durationText,
      thumbnail: it.thumbnail,
    }));
    setPlaylist(tracks);
    playlistRef.current = tracks;
    setQueueTracks(tracks);
    queueTracksRef.current = tracks;
    setCurrentIndex(0);
    currentIndexRef.current = 0;
    loadAndPlayTrack(tracks[0], true);
  };

  // Delete Track from IndexedDB Offline Storage & sync with Firebase
  const handleDeleteOfflineTrack = async (id: string, title?: string) => {
    try {
      await deleteOfflineTrack(id);
      const updated = await getAllOfflineTracks();
      setOfflineTracks(updated);
      setOfflineTrackIds(new Set(updated.map((t) => t.id)));

      if (userSession?.isLoggedIn) {
        const updatedCloudSaved = libraryData.savedTracks.filter((t) => t.id !== id);
        setLibraryData((prev) => ({ ...prev, savedTracks: updatedCloudSaved }));
        persistLibraryData({ savedTracks: updatedCloudSaved });
      }

      showToast(`Đã xóa "${title || "bài hát"}" khỏi bộ nhớ ngoại tuyến`, "info");
    } catch {
      showToast("Lỗi khi xóa bài hát khỏi bộ nhớ", "warning");
    }
  };

  // Save Track into IndexedDB for 100% Offline YouTube-Style Playback & sync with Firebase
  const handleSaveOfflineTrack = async (track: Track) => {
    if (!track) return;

    if (!userSession?.isLoggedIn) {
      showToast("Vui lòng đăng nhập để lưu bài hát nghe ngoại tuyến!", "warning");
      setShowPlayerMenu(false);
      setIsAccountOpen(true);
      return;
    }

    const isSaved = offlineTrackIds.has(track.id);
    if (isSaved) {
      await handleDeleteOfflineTrack(track.id, track.title);
      return;
    }

    setSavingOfflineTrackId(track.id);
    showToast(`Đang tải & lưu "${track.title}" vào bộ nhớ ngoại tuyến...`, "info");

    try {
      // 1. Fetch audio stream as Blob
      const downloadUrl = `${API_BASE}/api/download-audio?id=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&author=${encodeURIComponent(track.author || "")}`;
      const audioRes = await fetch(downloadUrl);
      if (!audioRes.ok) {
        throw new Error("Không thể tải luồng âm thanh");
      }
      const audioBlob = await audioRes.blob();

      // 2. Fetch lyrics in background
      let lyricsData = _lyrics;
      try {
        const lyrRes = await fetch(
          `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
        );
        if (lyrRes.ok) {
          lyricsData = await lyrRes.json();
        }
      } catch {}

      // 3. Store into IndexedDB
      await saveTrackOffline(track, audioBlob, lyricsData);

      // 4. Update UI state
      const updated = await getAllOfflineTracks();
      setOfflineTracks(updated);
      setOfflineTrackIds(new Set(updated.map((t) => t.id)));

      // 5. Sync to Firebase Cloud if logged in
      if (userSession?.isLoggedIn) {
        const filtered = libraryData.savedTracks.filter((t) => t.id !== track.id);
        const updatedCloudSaved = [track, ...filtered];
        setLibraryData((prev) => ({ ...prev, savedTracks: updatedCloudSaved }));
        persistLibraryData({ savedTracks: updatedCloudSaved });
      }

      showToast(`Đã lưu thành công "${track.title}"! Bạn có thể nghe khi không có mạng.`, "success");
    } catch (err: any) {
      console.error("[Offline Save Error]", err);
      showToast(`Không thể lưu ngoại tuyến: ${err.message || "Vui lòng thử lại"}`, "warning");
    } finally {
      setSavingOfflineTrackId(null);
    }
  };

  // Save library data to server & cloud (Authenticated per user with offline cache)
  const persistLibraryData = async (updated: {
    favorites?: Track[];
    savedTracks?: Track[];
    history?: Track[];
    playlists?: any[];
  }) => {
    const payload = {
      favorites: updated.favorites || libraryData.favorites,
      savedTracks: updated.savedTracks || libraryData.savedTracks,
      history: updated.history || libraryData.history,
      playlists: updated.playlists || libraryData.playlists,
    };
    try {
      localStorage.setItem(STORAGE_LIBRARY_KEY, JSON.stringify(payload));
    } catch {}

    try {
      await authenticatedFetch(`${API_BASE}/api/user/library`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
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

    if (!userSession?.isLoggedIn) {
      showToast("Vui lòng đăng nhập để tải bài hát về máy!", "warning");
      setShowPlayerMenu(false);
      setIsAccountOpen(true);
      return;
    }

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

    showToast(`Đang chuẩn bị tải xuống file MP3 "${track.title}"...`, "success");

    try {
      const downloadUrl = `${API_BASE}/api/download-audio?id=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&author=${encodeURIComponent(track.author || "")}`;
      const link = document.createElement("a");
      link.href = downloadUrl;
      link.setAttribute("download", `${track.title} - ${track.author}.mp3`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      showToast(`Đã bắt đầu tải xuống file MP3 "${track.title}"!`, "success");
    } catch {
      window.open(`https://www.youtube.com/watch?v=${track.id}`, "_blank");
    }
  };

  // Create playlist and add current track
  const handleCreatePlaylistAndAddTrack = async () => {
    if (!newPlaylistName.trim() || !currentTrack) return;

    if (!userSession?.isLoggedIn) {
      showToast("Vui lòng đăng nhập để tạo và lưu danh sách phát!", "warning");
      setIsCreatingPlaylist(false);
      setNewPlaylistName("");
      setIsAccountOpen(true);
      return;
    }

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

    if (!userSession?.isLoggedIn) {
      showToast("Vui lòng đăng nhập để lưu bài hát vào playlist!", "warning");
      setIsAccountOpen(true);
      return;
    }

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

  // Fetch User Session & Library Data (Token-authenticated & Multi-User Isolated with auto-refresh)
  const fetchUserSession = async () => {
    const token = getAuthToken();
    const refreshToken = getRefreshToken();

    try {
      const res = await authenticatedFetch(`${API_BASE}/api/user/session`);
      if (res.ok) {
        const data = await res.json();
        if (data.isLoggedIn && data.user) {
          const freshIdToken = res.headers.get("x-refreshed-id-token") || data.user.idToken || token;
          const freshRefreshToken = res.headers.get("x-refreshed-refresh-token") || userSession?.user?.refreshToken || data.user.refreshToken || refreshToken;
          const updatedSession = {
            ...data,
            user: {
              ...data.user,
              idToken: freshIdToken,
              refreshToken: freshRefreshToken,
            },
          };
          setUserSession(updatedSession);
          try {
            localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(updatedSession));
          } catch {}
        } else {
          if (!getRefreshToken()) {
            try {
              localStorage.removeItem(STORAGE_USER_SESSION_KEY);
            } catch {}
            setUserSession({ isLoggedIn: false, user: null });
          }
        }
      } else if (res.status === 401 && !getRefreshToken()) {
        try {
          localStorage.removeItem(STORAGE_USER_SESSION_KEY);
        } catch {}
        setUserSession({ isLoggedIn: false, user: null });
      }
    } catch (err) {
      console.warn("Session fetch failed:", err);
    }
  };

  const fetchLibraryData = async () => {
    setIsLibraryLoading(true);
    try {
      const res = await authenticatedFetch(`${API_BASE}/api/user/library`);
      if (res.ok) {
        const data = await res.json();
        const cleanAuthorField = (obj: any) => {
          const raw = (obj?.author || obj?.Author || obj?.Artist || obj?.channel || "").trim();
          return (raw && raw !== "Nghệ sĩ" && raw !== "Kahoii Music" && raw !== "YouTube Music") ? raw : "";
        };
        const mappedFavs: Track[] = (data.favorites || []).map((f: any) => ({
          id: f.id || f.Id,
          title: f.title || f.Title,
          author: cleanAuthorField(f),
          duration: Number(f.DurationSeconds ?? f.duration ?? f.durationSeconds ?? f.Duration ?? 0),
          thumbnail: `https://i.ytimg.com/vi/${f.id || f.Id}/mqdefault.jpg`,
        }));
        const mappedSaved: Track[] = (data.savedTracks || data.SavedTracks || []).map((s: any) => ({
          id: s.id || s.Id,
          title: s.title || s.Title,
          author: cleanAuthorField(s),
          duration: Number(s.DurationSeconds ?? s.duration ?? s.durationSeconds ?? s.Duration ?? 0),
          thumbnail: `https://i.ytimg.com/vi/${s.id || s.Id}/mqdefault.jpg`,
        }));
        const mappedHist: Track[] = (data.history || []).map((h: any) => ({
          id: h.id || h.Id,
          title: h.title || h.Title,
          author: cleanAuthorField(h),
          duration: Number(h.DurationSeconds ?? h.duration ?? h.durationSeconds ?? h.Duration ?? 0),
          thumbnail: `https://i.ytimg.com/vi/${h.id || h.Id}/mqdefault.jpg`,
        }));
        const rawPlaylists = (data.playlists || data.collections || []).map((pl: any) => {
          const rawItems = pl.Items || pl.items || pl.Tracks || pl.tracks || [];
          const mapped = rawItems.map((it: any) => ({
            id: it.id || it.Id,
            title: it.title || it.Title,
            author: it.author || it.Author || it.Artist || "",
            duration: Number(it.DurationSeconds ?? it.duration ?? it.durationSeconds ?? it.Duration ?? 0),
            thumbnail: it.thumbnail || `https://i.ytimg.com/vi/${it.id || it.Id}/mqdefault.jpg`,
          }));
          return {
            id: pl.id || pl.Id || pl.Name || pl.name,
            name: pl.name || pl.Name || "Danh sách phát",
            created: pl.created || pl.CreatedAt,
            items: mapped,
            tracks: mapped,
          };
        });
        const nextLibrary = {
          favorites: mappedFavs,
          savedTracks: mappedSaved,
          history: mappedHist,
          playlists: rawPlaylists,
        };
        setLibraryData(nextLibrary);
        try {
          localStorage.setItem(STORAGE_LIBRARY_KEY, JSON.stringify(nextLibrary));
        } catch {}
      }
    } catch (err) {
      console.error("Library fetch failed:", err);
    } finally {
      setIsLibraryLoading(false);
    }
  };

  const openArtistDetail = async (artist: {
    id?: string;
    name: string;
    avatar?: string;
    subscribers?: string;
    genre?: string;
  }) => {
    setSelectedArtist({
      id: artist.id,
      name: artist.name,
      avatar: artist.avatar || DEFAULT_TRACK_ART,
      subscribers: artist.subscribers || "Nghệ sĩ",
      genre: artist.genre,
    });
    setActiveArtistTab("all");
    setIsArtistLoading(true);
    setArtistTracks([]);
    setArtistPlaylists([]);

    try {
      let data: StructuredSearchResult;

      // If we have a browseId (starts with UC or valid channel ID), query authentic channel uploads
      if (artist.id && (artist.id.startsWith("UC") || artist.id.startsWith("MPREb"))) {
        const res = await fetch(
          `${API_BASE}/api/artist?id=${encodeURIComponent(artist.id)}&name=${encodeURIComponent(artist.name)}`
        );
        if (res.ok) {
          data = await res.json();
        } else {
          // Fallback to text search if /api/artist failed
          const sRes = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(artist.name)}`);
          if (!sRes.ok) throw new Error("Artist search failed");
          data = await sRes.json();
        }
      } else {
        const res = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(artist.name)}`);
        if (!res.ok) throw new Error("Artist search failed");
        data = await res.json();
      }

      const rawTracks: Track[] = data.tracks || [];
      const lower = artist.name.toLowerCase().trim();

      // If loaded directly from channel endpoint, rawTracks are already authentic channel uploads!
      if (artist.id && (artist.id.startsWith("UC") || artist.id.startsWith("MPREb")) && rawTracks.length > 0) {
        setArtistTracks(rawTracks);
      } else {
        // Refine tracks: singles/tracks specifically by this artist
        const matched = rawTracks.filter((t) => {
          const a = (t.author || "").toLowerCase();
          const tit = (t.title || "").toLowerCase();
          return a.includes(lower) || tit.includes(lower);
        });
        setArtistTracks(matched.length >= 4 ? matched : rawTracks);
      }

      // Playlists / albums
      const rawPlaylists = data.playlists || [];
      if (artist.id && (artist.id.startsWith("UC") || artist.id.startsWith("MPREb")) && rawPlaylists.length > 0) {
        setArtistPlaylists(rawPlaylists);
      } else {
        const matchedPl = rawPlaylists.filter((p) => {
          const a = (p.author || "").toLowerCase();
          const tit = (p.title || "").toLowerCase();
          return a.includes(lower) || tit.includes(lower);
        });
        setArtistPlaylists(matchedPl.length > 0 ? matchedPl : rawPlaylists);
      }

      // Enrich artist info if available
      if (data.artists && data.artists.length > 0) {
        const matchedArtist =
          data.artists.find(
            (a) => (a.id && a.id === artist.id) || a.name.toLowerCase() === lower || lower.includes(a.name.toLowerCase())
          ) || data.artists[0];

        if (matchedArtist) {
          setSelectedArtist((prev) =>
            prev
              ? {
                  ...prev,
                  avatar:
                    prev.avatar && !prev.avatar.includes("logo_icon")
                      ? prev.avatar
                      : (matchedArtist.thumbnail || prev.avatar),
                  subscribers: matchedArtist.subscribers || prev.subscribers || "Nghệ sĩ chính thức",
                }
              : null
          );
        }
      }
    } catch (err) {
      console.error("Artist detail fetch error:", err);
    } finally {
      setIsArtistLoading(false);
    }
  };

  // Preload initial track metadata, user data, home feed & YouTube Player API on mount
  useEffect(() => {
    fetchUserSession();
    fetchLibraryData();

    // Fetch Live Home Feed (Trending & New Releases)
    setIsHomeLoading(true);
    fetch(`${API_BASE}/api/home?_t=${Date.now()}`, { cache: "no-store" })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data) {
          if (Array.isArray(data.trending) && data.trending.length > 0) {
            setTrendingTracks(data.trending);
          } else {
            setTrendingTracks(INITIAL_TRENDING_TRACKS);
          }
          if (Array.isArray(data.newReleases) && data.newReleases.length > 0) {
            setNewReleaseTracks(data.newReleases);
          } else {
            setNewReleaseTracks(INITIAL_NEW_RELEASES);
          }
        } else {
          setTrendingTracks(INITIAL_TRENDING_TRACKS);
          setNewReleaseTracks(INITIAL_NEW_RELEASES);
        }
      })
      .catch(() => {
        setTrendingTracks(INITIAL_TRENDING_TRACKS);
        setNewReleaseTracks(INITIAL_NEW_RELEASES);
      })
      .finally(() => {
        setIsHomeLoading(false);
      });

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
                  handleTrackEndedRef.current();
                } else if (event.data === 3) {
                  setIsLoading(true);
                }
              },
              onError: () => {
                setIsLoading(false);
                setTimeout(() => {
                  handleTrackEndedRef.current();
                }, 1000);
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

  // Time Tracker ticker for YouTube Player with natural end detection fallback
  useEffect(() => {
    const timer = setInterval(() => {
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.getCurrentTime === "function"
      ) {
        try {
          const ct = ytPlayerRef.current.getCurrentTime() || 0;
          const rawDur = ytPlayerRef.current.getDuration() || 0;
          const dur = Math.round(rawDur);
          if (ct > 0) setCurrentTime(ct);
          if (dur > 0 && Math.abs(dur - duration) > 1) {
            setDuration(dur);
            setCurrentTrack((prev) => (prev && (!prev.duration || prev.duration <= 0) ? { ...prev, duration: dur } : prev));
            setQueueTracks((prev) => {
              const curIdx = currentIndexRef.current;
              if (prev[curIdx] && (!prev[curIdx].duration || prev[curIdx].duration <= 0)) {
                const copy = [...prev];
                copy[curIdx] = { ...copy[curIdx], duration: dur };
                return copy;
              }
              return prev;
            });
          }

          // Fallback: If video reaches within 0.5s of the end and stops or pauses, advance to next track
          if (dur > 5 && ct >= dur - 0.5 && !isTransitioningRef.current) {
            const playerState = ytPlayerRef.current.getPlayerState ? ytPlayerRef.current.getPlayerState() : -1;
            if (playerState === 0 || playerState === 2) {
              handleTrackEndedRef.current();
            }
          }
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
    const token = getAuthToken();
    const refreshToken = getRefreshToken();
    if (!token && !refreshToken) {
      showToast("Vui lòng đăng nhập để đồng bộ Cloud", "warning");
      return;
    }
    setIsSyncing(true);
    setSyncSuccessMsg(null);
    try {
      const res = await authenticatedFetch(`${API_BASE}/api/user/sync`, {
        method: "POST",
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Đồng bộ thất bại");
      const freshIdToken = res.headers.get("x-refreshed-id-token") || data.user?.idToken || token;
      const freshRefreshToken = res.headers.get("x-refreshed-refresh-token") || userSession?.user?.refreshToken || data.user?.refreshToken || refreshToken;
      const updatedSession = {
        ...data,
        user: {
          ...data.user,
          idToken: freshIdToken,
          refreshToken: freshRefreshToken,
        },
      };
      setUserSession(updatedSession);
      try {
        localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(updatedSession));
      } catch {}
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setSyncSuccessMsg("Đã đồng bộ dữ liệu mới nhất từ Cloud thành công!");
      showToast("Đã đồng bộ dữ liệu từ Cloud Firestore!", "success");
      setTimeout(() => setSyncSuccessMsg(null), 4000);
    } catch (err: any) {
      showToast("Lỗi đồng bộ: " + err.message, "warning");
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
      try {
        localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(data));
      } catch {}
      setAvatarKey(Date.now());

      await fetchLibraryData();
      setLoginPassword("");
      setSyncSuccessMsg("Đăng nhập và tự động đồng bộ Cloud thành công!");
      showToast("Đã đăng nhập và đồng bộ thư viện cá nhân!", "success");
      setTimeout(() => {
        setSyncSuccessMsg(null);
        setIsAccountOpen(false);
      }, 1500);
    } catch (err: any) {
      setLoginError(err.message || "Lỗi đăng nhập");
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleLogout = () => {
    setShowLogoutConfirm(true);
  };

  const confirmLogout = async () => {
    setShowLogoutConfirm(false);
    const token = getAuthToken();
    try {
      await fetch(`${API_BASE}/api/user/logout`, {
        method: "POST",
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
    } catch (err) {
      console.warn("Logout error:", err);
    }
    try {
      localStorage.removeItem(STORAGE_USER_SESSION_KEY);
      localStorage.removeItem(STORAGE_LIBRARY_KEY);
    } catch {}
    setUserSession({ isLoggedIn: false, user: null });
    setLibraryData({ favorites: [], savedTracks: [], history: [], playlists: [] });
    pausePlayback();
    setCurrentTrack(null);
    currentTrackRef.current = null;
    setQueueTracks([]);
    queueTracksRef.current = [];
    setPlaylist([]);
    playlistRef.current = [];
    setIsAccountOpen(false);
    showToast("Đã đăng xuất tài khoản thành công!", "info");
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!loginEmail.trim() || !loginPassword.trim()) {
      setLoginError("Vui lòng điền đầy đủ email và mật khẩu.");
      return;
    }
    if (loginPassword.length < 6) {
      setLoginError("Mật khẩu phải có tối thiểu 6 ký tự.");
      return;
    }
    if (loginPassword !== registerConfirmPassword) {
      setLoginError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsRegistering(true);
    setLoginError(null);
    try {
      const res = await fetch(`${API_BASE}/api/user/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: loginEmail.trim(), password: loginPassword }),
      });
      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || "Đăng ký thất bại");
      }
      setUserSession(data);
      try {
        localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(data));
      } catch {}
      setAvatarKey(Date.now());
      await fetchLibraryData();
      setLoginPassword("");
      setRegisterConfirmPassword("");
      showToast("Đăng ký tài khoản thành công!", "success");
      setSyncSuccessMsg("Đăng ký tài khoản mới thành công!");
      setTimeout(() => {
        setSyncSuccessMsg(null);
        setIsAccountOpen(false);
      }, 1500);
    } catch (err: any) {
      setLoginError(err.message || "Đăng ký thất bại");
    } finally {
      setIsRegistering(false);
    }
  };

  const loadAndPlayTrack = async (track: Track, autoPlay = true, immediate = false) => {
    if (!track) return;

    // Instant UI update: title, artwork, active queue beam update immediately with zero lag
    setCurrentTrack(track);
    currentTrackRef.current = track;
    setIsLoading(true);
    setErrorMessage(null);
    setCurrentTime(0);
    setLyrics(null);

    // Cancel existing pending debounce timer
    if (playDebounceTimerRef.current) {
      clearTimeout(playDebounceTimerRef.current);
      playDebounceTimerRef.current = null;
    }

    // Cancel in-flight audio stream fetch
    if (playAbortControllerRef.current) {
      playAbortControllerRef.current.abort();
      playAbortControllerRef.current = null;
    }

    const executePlayback = async () => {
      // Record to history if user is logged in (only when actually played)
      if (userSession?.isLoggedIn) {
        setLibraryData((prev) => {
          const filtered = prev.history.filter((h) => h.id !== track.id);
          const updated = [track, ...filtered].slice(0, 100);
          persistLibraryData({ history: updated });
          return { ...prev, history: updated };
        });
      }

      // Strategy 0: Offline IndexedDB Playback (Zero network bytes, 100% offline)
      try {
        const offlineItem = await getOfflineTrack(track.id);
        if (offlineItem && offlineItem.audioBlob && audioRef.current) {
          if (ytPlayerRef.current && typeof ytPlayerRef.current.stopVideo === "function") {
            try {
              ytPlayerRef.current.stopVideo();
            } catch {}
          }
          const blobUrl = URL.createObjectURL(offlineItem.audioBlob);
          audioRef.current.src = blobUrl;
          audioRef.current.load();
          activePlayerType.current = "audio";
          if (autoPlay) {
            const playPromise = audioRef.current.play();
            if (playPromise !== undefined) {
              playPromise
                .then(() => {
                  setIsPlaying(true);
                  setIsLoading(false);
                })
                .catch(() => {
                  setIsPlaying(false);
                  setIsLoading(false);
                });
            }
          } else {
            setIsPlaying(false);
            setIsLoading(false);
          }
          if (offlineItem.lyrics) {
            setLyrics(offlineItem.lyrics);
          }
          // If offline lyrics don't have .lines, fetch enriched lyrics in background and update IndexedDB
          if ((!(offlineItem.lyrics as any)?.lines || (offlineItem.lyrics as any).lines.length === 0) && typeof navigator !== "undefined" && navigator.onLine) {
            fetch(
              `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
            )
              .then((res) => (res.ok ? res.json() : null))
              .then((lyricsData) => {
                if (lyricsData?.lines && lyricsData.lines.length > 0) {
                  setLyrics(lyricsData);
                  saveTrackOffline(track, offlineItem.audioBlob, lyricsData);
                }
              })
              .catch(() => {});
          }
          setErrorMessage(null);
          return;
        }
      } catch (offlineErr) {
        console.warn("[Offline Playback Error]", offlineErr);
      }

      // Check if user is offline without internet and the track is not downloaded
      if (typeof navigator !== "undefined" && !navigator.onLine) {
        setIsLoading(false);
        setErrorMessage("Bạn đang ngoại tuyến. Hãy mở tab 'Đã tải' để nghe các bài đã lưu.");
        showToast("Bạn đang ngoại tuyến! Hãy chọn bài hát trong mục 'Đã tải'", "info");
        setActiveLeftTab("offline");
        return;
      }

      // If active player is already YouTube, transition track smoothly
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.loadVideoById === "function"
      ) {
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
          setIsLoading(false);

          const updateAuthorAcrossApp = (newAuthor: string) => {
            if (!newAuthor || newAuthor === "Nghệ sĩ" || newAuthor === "Kahoii Music") return;
            track.author = newAuthor;
            setCurrentTrack((prev) => (prev && prev.id === track.id ? { ...prev, author: newAuthor } : prev));
            setLibraryData((prev) => {
              const repl = (list: Track[]) =>
                list.map((it) => (it.id === track.id ? { ...it, author: newAuthor } : it));
              const updated = {
                ...prev,
                favorites: repl(prev.favorites),
                savedTracks: repl(prev.savedTracks),
                history: repl(prev.history),
              };
              persistLibraryData(updated);
              return updated;
            });
          };

          // Fetch Lyrics in background without delaying playback
          fetch(
            `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
          )
            .then((res) => (res.ok ? res.json() : null))
            .then((lyricsData) => {
              if (lyricsData) {
                setLyrics(lyricsData);
                if (lyricsData.artist && (!track.author || track.author === "Nghệ sĩ" || track.author === "Kahoii Music")) {
                  updateAuthorAcrossApp(lyricsData.artist);
                }
              }
            })
            .catch(() => {});

          return;
        } catch (ytErr) {
          console.warn("YouTube instant transition warning:", ytErr);
        }
      }

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
        playAbortControllerRef.current = controller;
        const timeoutId = setTimeout(() => controller.abort(), 2000); // 2s fast check

        const res = await fetch(`${API_BASE}/api/stream?id=${track.id}`, {
          signal: controller.signal,
        });
        clearTimeout(timeoutId);

        if (res.ok) {
          const data = await res.json();
          if (data.author && (!track.author || track.author === "Nghệ sĩ" || track.author === "Kahoii Music")) {
            track.author = data.author;
            setCurrentTrack((prev) => (prev && prev.id === track.id ? { ...prev, author: data.author } : prev));
          }
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
                  .then(() => {
                    setIsPlaying(true);
                    setIsLoading(false);
                  })
                  .catch(() => {
                    setIsPlaying(false);
                    setIsLoading(false);
                  });
              }
            } else {
              setIsPlaying(false);
              setIsLoading(false);
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
            setIsLoading(false);
          } catch (ytErr) {
            console.warn("YouTube player fallback warning:", ytErr);
            setIsLoading(false);
          }
        } else {
          setIsLoading(false);
        }
      }

      // Fetch Lyrics in background
      try {
        const lyricsRes = await fetch(
          `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
        );
        if (lyricsRes.ok) {
          const lyricsData = await lyricsRes.json();
          setLyrics(lyricsData);
          if (lyricsData?.artist && (!track.author || track.author === "Nghệ sĩ" || track.author === "Kahoii Music")) {
            track.author = lyricsData.artist;
            setCurrentTrack((prev) => (prev && prev.id === track.id ? { ...prev, author: lyricsData.artist } : prev));
          }
        }
      } catch {}
    };

    if (immediate) {
      executePlayback();
    } else {
      // 600ms debounce ensures rapid clicks/spam settle smoothly before playing sound
      playDebounceTimerRef.current = setTimeout(() => {
        executePlayback();
      }, 600);
    }
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
      } else {
        showToast("Đang tải dữ liệu, vui lòng đợi trong giây lát...", "info");
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
    if (isTransitioningRef.current) return;
    isTransitioningRef.current = true;
    setTimeout(() => {
      isTransitioningRef.current = false;
    }, 1200);

    const mode = repeatModeRef.current;
    const activeQueue = queueTracksRef.current.length > 0 ? queueTracksRef.current : playlistRef.current;
    if (activeQueue.length === 0) return;

    let curIdx = activeQueue.findIndex((t) => t.id === currentTrackRef.current?.id);
    if (curIdx === -1) curIdx = currentIndexRef.current;
    if (curIdx < 0 || curIdx >= activeQueue.length) curIdx = 0;

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
      if (activeQueue.length <= 1) {
        loadAndPlayTrack(activeQueue[0], true, true);
        return;
      }
      let randIdx = Math.floor(Math.random() * activeQueue.length);
      let attempts = 0;
      while (randIdx === curIdx && attempts < 15) {
        randIdx = Math.floor(Math.random() * activeQueue.length);
        attempts++;
      }
      shuffleHistoryRef.current.push(curIdx);
      setCurrentIndex(randIdx);
      currentIndexRef.current = randIdx;
      loadAndPlayTrack(activeQueue[randIdx], true, true);
      return;
    }

    if (mode === "off") {
      if (curIdx >= activeQueue.length - 1) {
        pausePlayback();
        showToast("Đã phát hết danh sách bài hát", "info");
        return;
      }
      const nextIdx = curIdx + 1;
      setCurrentIndex(nextIdx);
      currentIndexRef.current = nextIdx;
      loadAndPlayTrack(activeQueue[nextIdx], true, true);
      return;
    }

    // Default: mode === "all" (Sequential playback through the queue)
    const nextIdx = (curIdx + 1) % activeQueue.length;
    setCurrentIndex(nextIdx);
    currentIndexRef.current = nextIdx;
    loadAndPlayTrack(activeQueue[nextIdx], true, true);
  };

  const handleNext = () => {
    const activeQueue = queueTracks.length > 0 ? queueTracks : playlist;
    if (activeQueue.length === 0) return;

    let curIdx = activeQueue.findIndex((t) => t.id === currentTrack?.id);
    if (curIdx === -1) curIdx = currentIndex;
    if (curIdx < 0 || curIdx >= activeQueue.length) curIdx = 0;

    if (repeatModeRef.current === "shuffle") {
      if (activeQueue.length <= 1) {
        loadAndPlayTrack(activeQueue[0], true);
        return;
      }
      let randIdx = Math.floor(Math.random() * activeQueue.length);
      let attempts = 0;
      while (randIdx === curIdx && attempts < 15) {
        randIdx = Math.floor(Math.random() * activeQueue.length);
        attempts++;
      }
      shuffleHistoryRef.current.push(curIdx);
      setCurrentIndex(randIdx);
      currentIndexRef.current = randIdx;
      loadAndPlayTrack(activeQueue[randIdx], true);
      return;
    }

    if (repeatModeRef.current === "off") {
      if (curIdx + 1 < activeQueue.length) {
        const nextIdx = curIdx + 1;
        setCurrentIndex(nextIdx);
        currentIndexRef.current = nextIdx;
        loadAndPlayTrack(activeQueue[nextIdx], true);
      } else {
        showToast("Đã đến cuối danh sách phát", "info");
      }
      return;
    }

    const nextIdx = (curIdx + 1) % activeQueue.length;
    setCurrentIndex(nextIdx);
    currentIndexRef.current = nextIdx;
    loadAndPlayTrack(activeQueue[nextIdx], true);
  };

  const handlePrev = () => {
    const activeQueue = queueTracks.length > 0 ? queueTracks : playlist;
    if (activeQueue.length === 0) return;

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

    let curIdx = activeQueue.findIndex((t) => t.id === currentTrack?.id);
    if (curIdx === -1) curIdx = currentIndex;
    if (curIdx < 0 || curIdx >= activeQueue.length) curIdx = 0;

    if (repeatModeRef.current === "shuffle" && shuffleHistoryRef.current.length > 0) {
      const prevIdx = shuffleHistoryRef.current.pop()!;
      setCurrentIndex(prevIdx);
      currentIndexRef.current = prevIdx;
      loadAndPlayTrack(activeQueue[prevIdx], true);
      return;
    }

    const prevIdx = (curIdx - 1 + activeQueue.length) % activeQueue.length;
    setCurrentIndex(prevIdx);
    currentIndexRef.current = prevIdx;
    loadAndPlayTrack(activeQueue[prevIdx], true);
  };

  const seekToTime = (newTime: number) => {
    setCurrentTime(newTime);
    if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
      try { ytPlayerRef.current.seekTo(newTime, true); } catch {}
    } else if (audioRef.current) {
      audioRef.current.currentTime = newTime;
    }
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    seekToTime(parseFloat(e.target.value));
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

  const handlePlaySongItem = (
    track: Track,
    contextList?: Track[],
    startIndex?: number
  ) => {
    if (currentTrack?.id === track.id) {
      togglePlayPause();
      return;
    }
    const list = contextList && contextList.length > 0 ? contextList : [track, ...queueTracks];
    setPlaylist(list);
    playlistRef.current = list;
    setQueueTracks(list);
    queueTracksRef.current = list;
    const idx = startIndex !== undefined && startIndex >= 0
      ? startIndex
      : list.findIndex((t) => t.id === track.id);
    const playIdx = idx !== -1 ? idx : 0;
    setCurrentIndex(playIdx);
    currentIndexRef.current = playIdx;
    loadAndPlayTrack(track, true);
  };

  const playSearchResult = (track: Track) => {
    if (currentTrack?.id === track.id) {
      togglePlayPause();
      return;
    }
    if (searchResults && searchResults.length > 0) {
      setPlaylist(searchResults);
      playlistRef.current = searchResults;
      setQueueTracks(searchResults);
      queueTracksRef.current = searchResults;
      const idx = searchResults.findIndex((t) => t.id === track.id);
      const playIdx = idx !== -1 ? idx : 0;
      setCurrentIndex(playIdx);
      currentIndexRef.current = playIdx;
      loadAndPlayTrack(track, true);
    } else {
      const existingIdx = queueTracks.findIndex((t) => t.id === track.id);
      if (existingIdx !== -1) {
        setCurrentIndex(existingIdx);
        currentIndexRef.current = existingIdx;
        loadAndPlayTrack(track, true);
      } else {
        const updated = [track, ...queueTracks];
        setQueueTracks(updated);
        queueTracksRef.current = updated;
        setPlaylist(updated);
        playlistRef.current = updated;
        setCurrentIndex(0);
        currentIndexRef.current = 0;
        loadAndPlayTrack(track, true);
      }
    }
  };

  const playTrackDirectly = (track: Track) => {
    if (currentTrack?.id === track.id) {
      togglePlayPause();
      return;
    }
    const existingIdx = queueTracks.findIndex((t) => t.id === track.id);
    if (existingIdx !== -1) {
      setCurrentIndex(existingIdx);
      currentIndexRef.current = existingIdx;
      loadAndPlayTrack(track, true);
    } else {
      const updated = [track, ...queueTracks];
      setQueueTracks(updated);
      queueTracksRef.current = updated;
      setPlaylist(updated);
      playlistRef.current = updated;
      setCurrentIndex(0);
      currentIndexRef.current = 0;
      loadAndPlayTrack(track, true);
    }
  };

  // Filtered Left list based on active tab and query
  const getSidebarItems = () => {
    const q = leftSearchQuery.toLowerCase().trim();
    let items: any[] = [];

    if (activeLeftTab === "favorites") {
      const favs = libraryData.favorites;
      if (favs.length > 0) {
        items = favs
          .filter((t) => !q || t.title.toLowerCase().includes(q) || (t.author && t.author.toLowerCase().includes(q)))
          .map((t) => {
            const artistName = formatTrackArtist(t.title, t.author);
            return {
              id: t.id,
              title: t.title,
              subtitle: t.duration && t.duration > 0
                ? (artistName ? `${artistName} • ${formatTime(t.duration)}` : formatTime(t.duration))
                : (artistName || "Bài hát"),
              thumb: t.thumbnail || `https://i.ytimg.com/vi/${t.id}/mqdefault.jpg`,
              rawTrack: t,
              rawPlaylist: null as any,
            };
          });
      }
    } else if (activeLeftTab === "offline") {
      const mergedMap = new Map<string, any>();

      // 1. Add all tracks from Firebase cloud (synced from PC / Android / Web)
      libraryData.savedTracks.forEach((t) => {
        const localOffline = offlineTracks.find((ot) => ot.id === t.id);
        const isLocalCached = !!localOffline;
        const sizeStr = isLocalCached
          ? formatBytes(localOffline?.sizeBytes || localOffline?.audioBlob?.size || 0)
          : null;
        mergedMap.set(t.id, {
          id: t.id,
          title: t.title,
          author: t.author,
          duration: t.duration,
          durationText: t.durationText,
          thumbnail: t.thumbnail || `https://i.ytimg.com/vi/${t.id}/mqdefault.jpg`,
          isLocalCached,
          sizeStr,
        });
      });

      // 2. Add local IndexedDB tracks that might not be in cloud
      offlineTracks.forEach((ot) => {
        if (!mergedMap.has(ot.id)) {
          const sizeStr = formatBytes(ot.sizeBytes || ot.audioBlob?.size || 0);
          mergedMap.set(ot.id, {
            id: ot.id,
            title: ot.title,
            author: ot.author,
            duration: ot.duration,
            durationText: ot.durationText,
            thumbnail: ot.thumbnail || `https://i.ytimg.com/vi/${ot.id}/mqdefault.jpg`,
            isLocalCached: true,
            sizeStr,
          });
        }
      });

      const allSaved = Array.from(mergedMap.values());
      if (allSaved.length > 0) {
        items = allSaved
          .filter((t) => !q || t.title.toLowerCase().includes(q) || (t.author && t.author.toLowerCase().includes(q)))
          .map((t) => {
            const artistName = formatTrackArtist(t.title, t.author);
            const prefix = artistName ? `${artistName} • ` : "";
            const dur = t.duration && t.duration > 0 ? `${formatTime(t.duration)} • ` : "";
            const sub = t.isLocalCached ? `${prefix}${dur}${t.sizeStr}` : `${prefix}${dur}Đám mây`;
            return {
              id: t.id,
              title: t.title,
              subtitle: sub,
              thumb: t.thumbnail,
              rawTrack: {
                id: t.id,
                title: t.title,
                author: t.author,
                duration: t.duration,
                durationText: t.durationText,
                thumbnail: t.thumbnail,
              },
              isOffline: true,
              isLocalCached: t.isLocalCached,
              rawPlaylist: null as any,
            };
          });
      }
    } else if (activeLeftTab === "history") {
      const hist = libraryData.history;
      if (hist.length > 0) {
        items = hist
          .filter((t) => !q || t.title.toLowerCase().includes(q) || (t.author && t.author.toLowerCase().includes(q)))
          .map((t) => {
            const artistName = formatTrackArtist(t.title, t.author);
            return {
              id: t.id,
              title: t.title,
              subtitle: t.duration && t.duration > 0
                ? (artistName ? `${artistName} • ${formatTime(t.duration)}` : formatTime(t.duration))
                : (artistName || "Bài hát"),
              thumb: t.thumbnail || `https://i.ytimg.com/vi/${t.id}/mqdefault.jpg`,
              rawTrack: t,
              rawPlaylist: null as any,
            };
          });
      }
    } else if (libraryData.playlists.length > 0) {
      items = libraryData.playlists
        .filter((p) => !q || (p.name && p.name.toLowerCase().includes(q)))
        .map((p, idx) => {
          const list = p.items || p.tracks || [];
          return {
            id: p.id || `pl-${idx}`,
            title: p.name || "Danh sách phát",
            subtitle: `${list.length} bài hát`,
            thumb: list[0]?.thumbnail || (list[0]?.id ? `https://i.ytimg.com/vi/${list[0].id}/mqdefault.jpg` : DEFAULT_TRACK_ART),
            rawTrack: null as any,
            rawPlaylist: p,
          };
        });
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
    <div className="w-screen h-screen h-[100dvh] flex flex-col justify-between overflow-hidden text-white select-none relative font-sans">
      <audio
        ref={audioRef}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={() => setDuration(audioRef.current?.duration || currentTrack?.duration || 207)}
        onEnded={() => handleTrackEndedRef.current()}
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
      <header className="h-[54px] w-full px-3 lg:px-5 flex items-center justify-between flex-shrink-0 z-30">
        {/* Left: App Logo & Brand Identity */}
        <div className="flex items-center gap-2 flex-shrink-0">
          <img
            src="./logo_icon.png"
            alt="Kahoii Music"
            className="w-7 h-7 rounded-lg object-contain shadow-md shadow-black/40"
          />
          <span className="hidden sm:inline font-extrabold text-sm tracking-wide bg-gradient-to-r from-pink-400 via-purple-300 to-indigo-300 bg-clip-text text-transparent">
            Kahoii Music
          </span>
        </div>

        {/* Center: Search Pill with Search History Suggestions Dropdown */}
        <div ref={searchDropdownRef} className="relative flex-1 max-w-[580px] mx-2 sm:mx-4">
          <form
            onSubmit={handleTopSearchSubmit}
            className="w-full h-[36px] px-3 sm:px-4 rounded-full theme-input-pill flex items-center gap-2 sm:gap-2.5 shadow-inner transition-all"
          >
            <Search className="w-4 h-4 theme-accent-text flex-shrink-0" />
            <input
              type="text"
              placeholder="Search"
              value={topSearchText}
              onFocus={() => setIsSearchFocused(true)}
              onChange={(e) => setTopSearchText(e.target.value)}
              className="flex-1 bg-transparent text-xs sm:text-sm text-white placeholder-white/40 focus:outline-none min-w-0"
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
              className="hidden sm:flex px-4 py-0.5 rounded-full bg-white/10 hover:theme-accent-bg hover:text-white border border-white/20 text-xs font-semibold text-white transition-all cursor-pointer items-center gap-1"
            >
              <ClipboardPaste className="w-3 h-3 text-white/80" />
              <span>Dán</span>
            </button>
          </form>

          {/* Quick Search History Dropdown Popover */}
          {isSearchFocused && searchHistory.length > 0 && (
            <div className="absolute top-[42px] left-0 right-0 rounded-2xl theme-menu-glass shadow-2xl p-3 z-50 animate-fade-in flex flex-col gap-1.5 border border-white/15">
              <div className="flex items-center justify-between px-2 pb-1.5 border-b border-white/10 text-[11px] font-semibold text-white/60">
                <span className="flex items-center gap-1.5 theme-accent-text">
                  <Clock className="w-3.5 h-3.5 theme-accent-text" />
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
                    className="flex items-center justify-between px-2.5 py-1.5 rounded-xl hover:bg-white/10 text-xs text-white/90 hover:theme-accent-text transition-colors cursor-pointer group"
                  >
                    <div className="flex items-center gap-2 truncate flex-1 min-w-0 pr-2">
                      <Clock className="w-3.5 h-3.5 text-white/30 group-hover:theme-accent-text flex-shrink-0" />
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

        {/* Right: Offline Indicator, Download Apps & User Avatar Pill Buttons */}
        <div className="flex items-center gap-2 flex-shrink-0">
          {!isOnline && (
            <div
              title="Bạn đang ngoại tuyến. Các bài hát đã lưu trong mục 'Đã tải' vẫn nghe được bình thường!"
              className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-amber-500/20 border border-amber-500/40 text-amber-300 text-[11px] font-medium animate-pulse cursor-pointer"
              onClick={() => setActiveLeftTab("offline")}
            >
              <WifiOff className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">Ngoại tuyến (Offline)</span>
            </div>
          )}

          {/* Download App Button & Dropdown */}
          <div className="relative" ref={downloadMenuRef}>
            <button
              type="button"
              onClick={() => setShowDownloadMenu(!showDownloadMenu)}
              title="Tải ứng dụng Kahoii Music (PC, Extension, Android, iOS, PWA)"
              className={`flex items-center gap-1.5 px-2.5 py-1.5 sm:px-3 sm:py-1.5 rounded-full transition-all cursor-pointer shadow-md border ${
                showDownloadMenu
                  ? "theme-badge-active scale-105"
                  : "theme-badge hover:theme-btn-primary hover:text-white text-white/90 hover:scale-105 border-white/10"
              }`}
            >
              <Download className="w-3.5 h-3.5 theme-accent-text" />
              <span className="hidden sm:inline text-xs font-semibold">Tải ứng dụng</span>
            </button>

            {/* Download Dropdown Popover */}
            {showDownloadMenu && (
              <div className="absolute top-[44px] right-0 z-50 w-[310px] sm:w-[350px] theme-menu-glass rounded-2xl shadow-2xl p-3 border theme-accent-border animate-fade-in flex flex-col gap-2">
                <div className="flex items-center justify-between px-1 pb-1.5 border-b border-white/10">
                  <div>
                    <p className="text-xs font-bold text-white flex items-center gap-1.5">
                      <Download className="w-3.5 h-3.5 theme-accent-text" />
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

                {/* Platforms List */}
                <div className="flex flex-col gap-1.5">
                  {/* 0. Cài đặt Web App (PWA) */}
                  <button
                    type="button"
                    onClick={() => {
                      setShowDownloadMenu(false);
                      handleInstallPwa();
                    }}
                    className="flex items-center justify-between p-2 rounded-xl bg-gradient-to-r from-pink-500/15 via-purple-500/15 to-indigo-500/15 hover:from-pink-500/25 hover:to-purple-500/25 transition-all group border border-pink-500/30 hover:border-pink-500/60 cursor-pointer text-left w-full"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-pink-500/20 text-pink-400 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                        <Sparkles className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white group-hover:theme-accent-text transition-colors flex items-center gap-1.5 truncate">
                          <span>Cài đặt Web App (PWA)</span>
                          <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-pink-500/30 text-pink-300 font-semibold flex-shrink-0">
                            Mở Offline
                          </span>
                        </p>
                        <p className="text-[10px] text-white/60 truncate">
                          Mở ngay trên máy tính/điện thoại khi mất mạng
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/10 group-hover:theme-btn-primary group-hover:text-white text-white/80 transition-all flex-shrink-0 ml-2">
                      <Plus className="w-3.5 h-3.5" />
                    </div>
                  </button>
                  {/* 1. PC (Windows) */}
                  <a
                    href={downloadData.pc.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-blue-500/20 text-blue-400 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                        <Laptop className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white group-hover:theme-accent-text transition-colors flex items-center gap-1.5 truncate">
                          <span>Windows PC</span>
                          <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-blue-500/20 text-blue-300 font-mono flex-shrink-0">
                            {downloadData.pc.version}
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          {downloadData.pc.fileName} {downloadData.pc.size ? `• ${downloadData.pc.size}` : ""}
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:theme-btn-primary group-hover:text-white text-white/60 transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  {/* 2. Chrome Extension */}
                  <a
                    href={downloadData.extension.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-amber-500/20 text-amber-400 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                        <Puzzle className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white group-hover:theme-accent-text transition-colors flex items-center gap-1.5 truncate">
                          <span>Extension</span>
                          <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-amber-500/20 text-amber-300 font-mono flex-shrink-0">
                            {downloadData.extension.version}
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          Chrome, Edge, Cốc Cốc (.zip)
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:theme-btn-primary group-hover:text-white text-white/60 transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  {/* 3. Android */}
                  <a
                    href={downloadData.android.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                        <Smartphone className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white group-hover:theme-accent-text transition-colors flex items-center gap-1.5 truncate">
                          <span>Android</span>
                          <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-emerald-500/20 text-emerald-300 font-mono flex-shrink-0">
                            APK
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          Cài trực tiếp {downloadData.android.size ? `• ${downloadData.android.size}` : ""}
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:theme-btn-primary group-hover:text-white text-white/60 transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>

                  {/* 4. iOS */}
                  <a
                    href={downloadData.ios.downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-between p-2 rounded-xl hover:bg-white/10 transition-all group border border-white/5 hover:border-white/15 cursor-pointer"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className="w-8 h-8 rounded-lg bg-purple-500/20 text-purple-400 flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                        <Tablet className="w-4 h-4" />
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-white group-hover:theme-accent-text transition-colors flex items-center gap-1.5 truncate">
                          <span>iOS (iPhone/iPad)</span>
                          <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-purple-500/20 text-purple-300 font-mono flex-shrink-0">
                            IPA
                          </span>
                        </p>
                        <p className="text-[10px] text-white/50 truncate">
                          Sideloadly / AltStore / TrollStore
                        </p>
                      </div>
                    </div>
                    <div className="p-1.5 rounded-lg bg-white/5 group-hover:theme-btn-primary group-hover:text-white text-white/60 transition-all flex-shrink-0 ml-2">
                      <Download className="w-3.5 h-3.5" />
                    </div>
                  </a>
                </div>

                {/* Footer */}
                <div className="pt-1.5 border-t border-white/10 flex items-center justify-between text-[10px] text-white/40 px-1">
                  <span className="flex items-center gap-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                    Tự động lấy bản mới nhất
                  </span>
                  <a
                    href="https://github.com/Iiwa-San/KahoiiMusic/releases"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="theme-accent-text hover:underline flex items-center gap-0.5"
                  >
                    <span>Releases</span>
                    <ExternalLink className="w-2.5 h-2.5" />
                  </a>
                </div>
              </div>
            )}
          </div>

          {/* User Avatar Pill Button (Real avatar from database) */}
          {userSession?.isLoggedIn ? (
            <button
              onClick={() => setIsAccountOpen(true)}
              title={`Tài khoản: ${userSession.user?.name || "Kahoii User"}`}
              className="flex items-center gap-1.5 p-1 sm:pl-1.5 sm:pr-3.5 sm:py-1 rounded-full theme-badge hover:theme-btn-primary hover:text-white text-white font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 border theme-accent-border group"
            >
              <img
                src={
                  userSession?.user?.avatarBase64
                    ? `data:image/png;base64,${userSession.user.avatarBase64}`
                    : `${API_BASE}/api/user/avatar?t=${avatarKey}${getAuthToken() ? `&token=${getAuthToken()}` : ""}`
                }
                alt="Avatar"
                onError={(e) => {
                  e.currentTarget.src = DEFAULT_TRACK_ART;
                }}
                className="w-6 h-6 rounded-full object-cover border border-white/20 shadow-sm"
              />
              <span className="hidden sm:inline max-w-[75px] truncate text-white/90 group-hover:text-white font-semibold">
                {userSession.user?.name || "avatar"}
              </span>
            </button>
          ) : (
            <button
              onClick={() => setIsAccountOpen(true)}
              title="Đăng nhập"
              className="px-2.5 py-1.5 sm:px-3.5 sm:py-1.5 rounded-full theme-btn-primary hover:opacity-90 text-white font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 flex items-center gap-1.5"
            >
              <User className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">Đăng nhập</span>
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
              : "theme-card-bg theme-accent-text shadow-xl"
          }`}
        >
          {toast.type === "warning" && <AlertCircle className="w-4 h-4 text-amber-400 flex-shrink-0" />}
          {toast.type === "success" && <Check className="w-4 h-4 text-emerald-400 flex-shrink-0" />}
          {toast.type === "info" && <Sparkles className="w-4 h-4 theme-accent-text flex-shrink-0" />}
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
      <main className="flex-1 w-full px-2 sm:px-4 lg:px-5 pb-[76px] lg:pb-5 pt-1 flex gap-3 lg:gap-4 overflow-hidden min-h-0 relative">
        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 1 (LEFT): Sidebar / Library Panel (~19% Width)                   */}
        {/* ----------------------------------------------------------------------- */}
        <aside
          className={`${
            mobileTab === "library" ? "flex w-full" : "hidden"
          } lg:flex lg:w-[290px] xl:w-[320px] flex-shrink-0 figma-panel flex-col min-h-0 overflow-hidden p-4 sm:p-5 lg:px-[18px] lg:pt-[30px] lg:pb-[22px]`}
        >
          {/* Top 4 Tabs Row: Silky Smooth Sliding Indicator Animation */}
          <div className="relative grid grid-cols-4 gap-1.5" style={{ marginBottom: "20px" }}>
            {/* Static Capsule Backgrounds */}
            <div className="absolute inset-0 grid grid-cols-4 gap-1.5 pointer-events-none">
              <div className="rounded-xl theme-tab-inactive" />
              <div className="rounded-xl theme-tab-inactive" />
              <div className="rounded-xl theme-tab-inactive" />
              <div className="rounded-xl theme-tab-inactive" />
            </div>

            {/* Active Sliding Glowing Pill */}
            <div
              className="absolute top-0 bottom-0 rounded-xl pointer-events-none z-10"
              style={{
                width: "calc((100% - 18px) / 4)",
                left: 0,
                transform: `translateX(calc(${activeTabIndex} * (100% + 6px)))`,
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
                className={`relative z-20 py-2 px-0.5 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer truncate flex items-center justify-center gap-1 ${
                  activeLeftTab === tab.key ? "text-white" : "text-white/70 hover:text-white"
                }`}
              >
                <span>{tab.label}</span>
                {tab.key === "offline" && Math.max(libraryData.savedTracks.length, offlineTracks.length) > 0 && (
                  <span className="text-[9px] px-1 py-0.2 rounded-full bg-white/20 font-mono">
                    {Math.max(libraryData.savedTracks.length, offlineTracks.length)}
                  </span>
                )}
              </button>
            ))}
          </div>

          {!userSession?.isLoggedIn ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-4 gap-3 bg-white/5 rounded-2xl border border-white/10 my-auto">
              <div className="w-12 h-12 rounded-full theme-badge flex items-center justify-center theme-accent-text shadow-md">
                <FolderHeart className="w-6 h-6" />
              </div>
              <div>
                <p className="text-sm font-semibold text-white">Thư viện cá nhân</p>
                <p className="text-xs text-white/50 mt-1 leading-relaxed">
                  Đăng nhập để xem danh sách phát, bài hát yêu thích, lịch sử nghe nhạc và các bài hát đã tải về của bạn.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsAccountOpen(true)}
                className="mt-2 px-4 py-2 rounded-xl theme-btn-primary text-white text-xs font-semibold hover:opacity-95 active:scale-95 transition-all cursor-pointer shadow-lg"
              >
                Đăng nhập ngay
              </button>
            </div>
          ) : (
            <>
              {/* Search Box in Sidebar with Sort Icon (Matching Figma: Centered text, pink icons) */}
              <div
                className="relative flex items-center h-10 px-3.5 rounded-full theme-input-pill transition-all flex-shrink-0"
                style={{ marginBottom: "16px" }}
              >
                <Search className="w-4 h-4 theme-accent-text flex-shrink-0" />
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
                    isLeftSorted ? "theme-badge-active" : "theme-accent-text hover:text-white"
                  }`}
                >
                  <ArrowDownAZ className="w-4 h-4" />
                </button>
              </div>

              {/* Offline Storage Summary & Play All Action */}
              {activeLeftTab === "offline" && getSidebarItems().length > 0 && (
                <div className="flex items-center justify-between px-1 mb-2.5 text-xs flex-shrink-0">
                  <span className="text-white/60 text-[11px]">
                    {getSidebarItems().length} bài hát {offlineTracks.length > 0 ? `• ${formatBytes(offlineTotalBytes)} offline` : "• Đám mây"}
                  </span>
                  <button
                    type="button"
                    onClick={handlePlayAllOffline}
                    className="flex items-center gap-1 px-2.5 py-1 rounded-lg theme-btn-primary text-white text-[11px] font-semibold hover:opacity-90 active:scale-95 transition-all cursor-pointer shadow-sm"
                  >
                    <Play className="w-3 h-3 fill-current" />
                    <span>Phát tất cả</span>
                  </button>
                </div>
              )}

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
                          if (activeLeftTab === "favorites") {
                            const favList = libraryData.favorites;
                            handlePlaySongItem(item.rawTrack, favList);
                          } else if (activeLeftTab === "offline") {
                            const offlineList = getSidebarItems().map((it) => it.rawTrack).filter(Boolean);
                            handlePlaySongItem(item.rawTrack, offlineList);
                          } else if (activeLeftTab === "history") {
                            const histList = libraryData.history;
                            handlePlaySongItem(item.rawTrack, histList);
                          } else {
                            playTrackDirectly(item.rawTrack);
                          }
                        } else if (item.rawPlaylist?.items?.length) {
                          const plTracks: Track[] = item.rawPlaylist.items.map((it: any) => ({
                            id: it.id || it.Id,
                            title: it.title || it.Title,
                            author: it.author || it.Author || it.Artist || "",
                            duration: Number(it.DurationSeconds ?? it.duration ?? it.durationSeconds ?? it.Duration ?? 0),
                            thumbnail: `https://i.ytimg.com/vi/${it.id || it.Id}/mqdefault.jpg`,
                          }));
                          setActiveLibraryPlaylistId(item.rawPlaylist.id || item.rawPlaylist.name);
                          setActivePlayingPlaylistId(item.rawPlaylist.id || item.rawPlaylist.name);
                          setPlaylist(plTracks);
                          setCurrentIndex(0);
                          currentIndexRef.current = 0;
                          loadAndPlayTrack(plTracks[0], true);
                          setQueueTracks(plTracks);
                        } else if (playlist[idx % playlist.length]) {
                          playTrackDirectly(playlist[idx % playlist.length]);
                        }
                      }}
                      className={`flex items-center gap-3.5 p-2 rounded-2xl transition-all duration-200 cursor-pointer group ${
                        (activeLeftTab === "library" && activeLibraryPlaylistId === (item.rawPlaylist?.id || item.rawPlaylist?.name)) ||
                        ((activeLeftTab === "favorites" || activeLeftTab === "history" || activeLeftTab === "offline") && currentTrack && currentTrack.id === item.id)
                          ? "active-queue-beam shadow-md scale-[1.01]"
                          : "hover:bg-white/10 hover:translate-x-1"
                      }`}
                      style={{ marginBottom: "2px" }}
                    >
                      <div className="relative w-11 h-11 flex-shrink-0">
                        <img
                          src={item.thumb}
                          alt=""
                          onError={(e) => {
                            e.currentTarget.src = DEFAULT_TRACK_ART;
                          }}
                          className="w-full h-full rounded-[14px] object-cover border border-white/10 shadow-sm"
                        />
                        {((activeLeftTab === "library" && activeLibraryPlaylistId === (item.rawPlaylist?.id || item.rawPlaylist?.name)) ||
                          ((activeLeftTab === "favorites" || activeLeftTab === "history" || activeLeftTab === "offline") && currentTrack && currentTrack.id === item.id)) && (
                          <div className="absolute inset-0 bg-black/55 rounded-[14px] flex items-center justify-center">
                            {isPlaying ? (
                              <Volume2 className="w-5 h-5 theme-accent-text animate-pulse" />
                            ) : (
                              <Play className="w-4 h-4 fill-current text-white/90" />
                            )}
                          </div>
                        )}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center justify-between gap-1">
                          <p className="text-[13.5px] font-semibold text-white group-hover:theme-accent-text truncate transition-colors">
                            {item.title}
                          </p>
                          {((activeLeftTab === "library" && activeLibraryPlaylistId === (item.rawPlaylist?.id || item.rawPlaylist?.name)) ||
                            ((activeLeftTab === "favorites" || activeLeftTab === "history" || activeLeftTab === "offline") && currentTrack && currentTrack.id === item.id)) && (
                            <span className="text-[10px] uppercase font-bold theme-accent-text tracking-wider flex-shrink-0">
                              {isPlaying ? "Đang phát" : "Tạm dừng"}
                            </span>
                          )}
                        </div>
                        <p className="text-[11.5px] text-purple-200/60 truncate mt-1">
                          {item.subtitle}
                        </p>
                      </div>
                      {activeLeftTab === "offline" && (
                        <div className="flex items-center gap-1 flex-shrink-0">
                          {!item.isLocalCached ? (
                            <button
                              type="button"
                              title="Tải về bộ nhớ máy này (để nghe offline không cần mạng)"
                              onClick={(e) => {
                                e.stopPropagation();
                                if (item.rawTrack) handleSaveOfflineTrack(item.rawTrack);
                              }}
                              className="p-1.5 rounded-lg hover:bg-white/10 text-white/50 hover:theme-accent-text transition-colors cursor-pointer"
                            >
                              <HardDriveDownload className="w-3.5 h-3.5" />
                            </button>
                          ) : (
                            <span title="Đã lưu offline trên máy này" className="p-1 text-emerald-400">
                              <Check className="w-3.5 h-3.5" />
                            </span>
                          )}
                          <button
                            type="button"
                            title="Xóa khỏi danh sách đã tải"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleDeleteOfflineTrack(item.id, item.title);
                            }}
                            className="p-1.5 rounded-lg hover:bg-white/10 text-white/40 hover:text-rose-400 transition-colors cursor-pointer"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      )}
                    </div>
                  ))
                ) : (
                  <div className="flex flex-col items-center justify-center py-16 text-center text-white/40 text-xs px-2">
                    <p>
                      {activeLeftTab === "library"
                        ? "Chưa có danh sách phát nào"
                        : activeLeftTab === "favorites"
                        ? "Chưa có bài hát yêu thích nào"
                        : activeLeftTab === "offline"
                        ? "Chưa có bài hát ngoại tuyến nào"
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
          className={`${
            mobileTab === "explore" ? "flex w-full" : "hidden"
          } lg:flex flex-1 h-full figma-panel flex-col min-h-0 overflow-hidden p-3.5 sm:p-5 lg:p-7`}
        >
          {selectedArtist ? (
            /* ===================================================================== */
            /* ARTIST DETAIL VIEW (Session giữa lấy data của đúng artist đó)         */
            /* ===================================================================== */
            <div className="flex flex-col h-full min-h-0 overflow-hidden">
              {/* FIXED ARTIST HEADER CLUSTER (Back Bar + Hero Banner + Tags) */}
              <div className="flex-shrink-0">
                {/* Back Bar */}
              <div className="flex items-center justify-between pb-3.5 border-b border-white/10 mb-5 flex-shrink-0">
                <button
                  type="button"
                  onClick={() => setSelectedArtist(null)}
                  className="px-3.5 py-1.5 rounded-xl theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-2 text-xs font-semibold group"
                >
                  <ArrowLeft className="w-4 h-4 theme-accent-text group-hover:-translate-x-0.5 transition-transform" />
                  <span>Quay lại</span>
                </button>
                <span className="text-xs text-white/50 font-mono">
                  Thông tin Nghệ sĩ
                </span>
              </div>

              {/* Artist Hero Header Card */}
              <div className="w-full rounded-[24px] p-6 theme-card-bg shadow-2xl mb-6 flex flex-col sm:flex-row items-center sm:items-start gap-6 border theme-accent-border relative overflow-hidden flex-shrink-0">
                <div className="absolute -top-12 -right-12 w-48 h-48 rounded-full blur-3xl opacity-20 pointer-events-none theme-accent-bg" />

                {/* Big Avatar */}
                <div className="relative w-28 h-28 sm:w-32 sm:h-32 rounded-full overflow-hidden shadow-2xl flex-shrink-0 border-[3px] theme-accent-border bg-black/40">
                  <img
                    src={selectedArtist.avatar}
                    alt={selectedArtist.name}
                    onError={(e) => {
                      e.currentTarget.src = DEFAULT_TRACK_ART;
                    }}
                    className="w-full h-full object-cover"
                  />
                </div>

                {/* Info & Action Buttons */}
                <div className="flex-1 min-w-0 flex flex-col items-center sm:items-start text-center sm:text-left justify-center">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider theme-badge flex items-center gap-1">
                      <Sparkles className="w-3 h-3 theme-accent-text" />
                      Nghệ sĩ chính thức
                    </span>
                    {selectedArtist.genre && (
                      <span className="text-[11px] text-white/60">
                        • {selectedArtist.genre}
                      </span>
                    )}
                  </div>

                  <h1 className="text-2xl sm:text-4xl font-extrabold text-white tracking-wide truncate max-w-full">
                    {selectedArtist.name}
                  </h1>

                  <p className="text-xs text-white/60 mt-1 mb-4">
                    {selectedArtist.subscribers || "Nghệ sĩ trên YouTube Music"}
                    {artistTracks.length > 0 && ` • ${artistTracks.length} bài hát single`}
                    {artistPlaylists.length > 0 && ` • ${artistPlaylists.length} danh sách phát`}
                  </p>

                  {/* Play Actions */}
                  <div className="flex items-center gap-3 flex-wrap justify-center sm:justify-start">
                    <button
                      type="button"
                      disabled={artistTracks.length === 0}
                      onClick={() => {
                        if (artistTracks.length > 0) {
                          setPlaylist(artistTracks);
                          setQueueTracks(artistTracks);
                          setCurrentIndex(0);
                          loadAndPlayTrack(artistTracks[0], true);
                        }
                      }}
                      className="px-5 py-2.5 rounded-full theme-btn-primary text-white font-bold text-xs sm:text-sm flex items-center gap-2 shadow-xl hover:scale-105 active:scale-95 transition-all cursor-pointer disabled:opacity-50"
                    >
                      <Play className="w-4 h-4 fill-current translate-x-0.5" />
                      <span>Phát tất cả</span>
                    </button>

                    <button
                      type="button"
                      disabled={artistTracks.length === 0}
                      onClick={() => {
                        if (artistTracks.length > 0) {
                          const shuffled = [...artistTracks].sort(() => Math.random() - 0.5);
                          setPlaylist(shuffled);
                          setQueueTracks(shuffled);
                          setCurrentIndex(0);
                          loadAndPlayTrack(shuffled[0], true);
                          setRepeatMode("shuffle");
                        }
                      }}
                      className="px-4 py-2.5 rounded-full theme-badge hover:theme-btn-primary hover:text-white transition-all text-xs font-semibold flex items-center gap-2 cursor-pointer disabled:opacity-50"
                    >
                      <Shuffle className="w-4 h-4" />
                      <span>Phát ngẫu nhiên</span>
                    </button>
                  </div>
                </div>
              </div>

              {/* Tag Selection Bar (Fixed together in this header cluster) */}
                <div className="py-2.5 mb-4 border-b border-white/10 flex items-center gap-2 flex-wrap flex-shrink-0">
                  {[
                    { key: "all", label: "Tất cả", count: artistTracks.length + artistPlaylists.length },
                    { key: "singles", label: "Nhạc Single / Bài hát", count: artistTracks.length },
                    { key: "playlists", label: "Danh sách phát", count: artistPlaylists.length },
                  ].map((tab) => {
                    const isActive = activeArtistTab === tab.key;
                    return (
                      <button
                        key={tab.key}
                        type="button"
                        onClick={() => setActiveArtistTab(tab.key as any)}
                        className={`px-4 py-1.5 rounded-full text-xs font-semibold transition-all flex items-center gap-2 cursor-pointer ${
                          isActive
                            ? "theme-badge-active scale-[1.02]"
                            : "theme-badge hover:theme-accent-border"
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
              </div>

              {/* SCROLLABLE BODY CONTENT (Singles & Playlists) */}
              <div className="flex-1 min-h-0 overflow-y-auto pr-1 custom-scrollbar pb-8">
                {isArtistLoading ? (
                  <div className="flex-1 flex flex-col items-center justify-center py-20 text-center gap-3">
                    <Loader2 className="w-8 h-8 animate-spin theme-accent-text" />
                    <p className="text-sm font-medium text-white/70">
                      Đang tải dữ liệu của {selectedArtist.name}...
                    </p>
                  </div>
                ) : (
                  <div className="flex flex-col gap-7">
                  {/* Section Singles */}
                  {(activeArtistTab === "all" || activeArtistTab === "singles") && (
                    <div>
                      <div className="flex items-center justify-between mb-3.5">
                        <div className="flex items-center gap-2">
                          <Music className="w-4 h-4 theme-accent-text" />
                          <h3 className="text-[17px] font-bold text-white tracking-wide">
                            Nhạc Single & Bài hát ({artistTracks.length})
                          </h3>
                        </div>
                      </div>

                      {artistTracks.length > 0 ? (
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                          {artistTracks.map((item, idx) => {
                            const isCurrentPlaying = currentTrack?.id === item.id;
                            return (
                              <div
                                key={item.id + idx}
                                onClick={() => {
                                  if (isCurrentPlaying) {
                                    togglePlayPause();
                                  } else {
                                    setPlaylist(artistTracks);
                                    setQueueTracks(artistTracks);
                                    setCurrentIndex(idx);
                                    loadAndPlayTrack(item, true);
                                  }
                                }}
                                className="w-full flex flex-col cursor-pointer group"
                              >
                                <div
                                  className={`relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-all duration-200 border ${
                                    isCurrentPlaying
                                      ? "theme-accent-border shadow-lg"
                                      : "border-white/5 bg-black/30"
                                  }`}
                                >
                                  <img
                                    src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                                    alt={item.title}
                                    onError={(e) => {
                                      e.currentTarget.src = DEFAULT_TRACK_ART;
                                    }}
                                    className="w-full h-full object-cover"
                                  />
                                  <UnifiedPlayOverlay
                                    isCurrent={isCurrentPlaying}
                                    isPlaying={isCurrentPlaying && isPlaying}
                                    onToggle={() => {
                                      if (isCurrentPlaying) {
                                        togglePlayPause();
                                      } else {
                                        setPlaylist(artistTracks);
                                        setQueueTracks(artistTracks);
                                        setCurrentIndex(idx);
                                        loadAndPlayTrack(item, true);
                                      }
                                    }}
                                  />
                                  {(item.duration > 0 || !!item.durationText) && (
                                    <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow">
                                      {item.isMusic !== false && (
                                        <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                      )}
                                      <span>{item.durationText || formatTime(item.duration)}</span>
                                    </span>
                                  )}
                                </div>
                                <p
                                  className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
                                    isCurrentPlaying
                                      ? "theme-accent-text font-bold"
                                      : "text-white/90 group-hover:theme-accent-text"
                                  }`}
                                >
                                  {item.title}
                                </p>
                                <p className="text-[10px] text-white/50 truncate mt-0.5">
                                  {item.author || selectedArtist.name}
                                </p>
                              </div>
                            );
                          })}
                        </div>
                      ) : (
                        <p className="text-xs text-white/40 italic py-6 text-center bg-white/5 rounded-xl border border-white/5">
                          Chưa tìm thấy bài hát single nào của nghệ sĩ này.
                        </p>
                      )}
                    </div>
                  )}

                  {/* Section Playlists */}
                  {(activeArtistTab === "all" || activeArtistTab === "playlists") && (
                    <div>
                      <div className="flex items-center justify-between mb-3.5">
                        <div className="flex items-center gap-2">
                          <ListMusic className="w-4 h-4 theme-accent-text" />
                          <h3 className="text-[17px] font-bold text-white tracking-wide">
                            Danh sách phát & Album ({artistPlaylists.length})
                          </h3>
                        </div>
                      </div>

                      {artistPlaylists.length > 0 ? (
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                          {artistPlaylists.map((pl, idx) => (
                            <div
                              key={pl.id || idx}
                              onClick={() => playPlaylistItem(pl)}
                              className="w-full flex flex-col cursor-pointer group"
                            >
                              <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-all duration-200 border border-white/10 bg-black/30">
                                <img
                                  src={pl.thumbnail || DEFAULT_TRACK_ART}
                                  alt={pl.title}
                                  onError={(e) => {
                                    e.currentTarget.src = DEFAULT_TRACK_ART;
                                  }}
                                  className="w-full h-full object-cover"
                                />
                                <UnifiedPlayOverlay
                                  isCurrent={activePlayingPlaylistId === pl.id}
                                  isPlaying={activePlayingPlaylistId === pl.id && isPlaying}
                                  isLoading={loadingPlaylistId === pl.id}
                                  onToggle={() => playPlaylistItem(pl)}
                                />
                                <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                  Playlist
                                </span>
                              </div>
                              <p className="text-[12px] font-semibold text-white/90 group-hover:theme-accent-text truncate mt-2">
                                {pl.title}
                              </p>
                              <p className="text-[10px] text-white/50 truncate mt-0.5">
                                {pl.author || selectedArtist.name}
                              </p>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs text-white/40 italic py-6 text-center bg-white/5 rounded-xl border border-white/5">
                          Chưa có danh sách phát nào của nghệ sĩ này.
                        </p>
                      )}
                    </div>
                  )}
                </div>
              )}
              </div>
            </div>
          ) : isSearchOpen ? (
            /* ===================================================================== */
            /* SEARCH VIEW (Dynamically transforms Center Canvas when searching)     */
            /* ===================================================================== */
            <div className="flex flex-col h-full min-h-0 overflow-hidden">
              {/* FIXED SEARCH HEADER CLUSTER (Back Bar + Navigation Tabs + Top Match) */}
              <div className="flex-shrink-0">
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
                    className="px-3 py-1.5 rounded-xl theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-2 text-xs font-semibold group"
                  >
                    <ArrowLeft className="w-4 h-4 theme-accent-text group-hover:-translate-x-0.5 transition-transform" />
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

              {/* Navigation Tabs */}
                <div className="py-2.5 mb-4 border-b border-white/10 flex items-center gap-2 flex-wrap flex-shrink-0">
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
                          ? "theme-badge-active scale-[1.02]"
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

                {/* Fixed Top Match Card (Pinned at top together with header in "Tất cả" tab) */}
                {!isSearching && activeSearchTab === "all" && (() => {
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
                    <div className="w-full mb-4 flex-shrink-0">
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
                        className="w-full rounded-[20px] p-4 theme-card-bg shadow-xl transition-all flex items-center justify-between gap-4 group cursor-pointer"
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
                            {(top.duration > 0 || !!top.durationText) && (
                              <span className="absolute bottom-1 right-1 px-1.5 py-0.5 rounded bg-black/80 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow">
                                {top.isMusic !== false && (
                                  <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                )}
                                <span>{top.durationText || formatTime(top.duration)}</span>
                              </span>
                            )}
                          </div>

                          {/* Track Info */}
                          <div className="min-w-0 flex flex-col justify-center">
                            <span className="text-[10px] uppercase font-bold tracking-widest theme-accent-text mb-1 flex items-center gap-1.5">
                              <Sparkles className="w-3 h-3 theme-accent-text" />
                              BÀI HÁT PHÙ HỢP NHẤT
                            </span>
                            <h3 className="text-base sm:text-lg font-bold text-white tracking-wide truncate group-hover:theme-accent-text transition-colors">
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
                            className="px-5 py-2.5 rounded-full theme-btn-primary text-white font-bold text-xs sm:text-sm flex items-center gap-2 shadow-lg hover:scale-105 active:scale-95 transition-all cursor-pointer"
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
              </div>

              {/* SCROLLABLE SEARCH BODY (Artists, Playlists, Tracks) */}
              <div className="flex-1 min-h-0 overflow-y-auto pr-1 custom-scrollbar pb-8">
                {isSearching ? (
                  <div className="flex-1 flex flex-col items-center justify-center py-24 text-center gap-3">
                    <Loader2 className="w-8 h-8 animate-spin theme-accent-text" />
                    <p className="text-sm font-medium text-purple-200/80">Đang tải dữ liệu...</p>
                  </div>
                ) : searchResults.length > 0 || (searchStructured && (searchStructured.artists.length > 0 || searchStructured.playlists.length > 0)) ? (
                  <div className="flex flex-col gap-6">
                    {/* ========================================================================= */}
                    {/* TAB: TẤT CẢ                                                               */}
                    {/* ========================================================================= */}
                    {activeSearchTab === "all" && (
                      <>


                      {/* TẦNG 2: Nhạc sĩ (Row of Artists - Enlarged Items) */}
                      {searchStructured?.artists && searchStructured.artists.length > 0 && (
                        <div>
                          <div className="flex items-center justify-between mb-3">
                            <h3
                              onClick={() => setActiveSearchTab("artists")}
                              className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:theme-accent-text transition-colors"
                            >
                              <span>Nhạc sĩ</span>
                            </h3>
                            <button
                              type="button"
                              onClick={() => setActiveSearchTab("artists")}
                              className="text-xs theme-accent-text hover:brightness-125 font-medium transition-colors cursor-pointer"
                            >
                              Xem tất cả ({searchStructured.artists.length})
                            </button>
                          </div>
                          <div className="flex items-start gap-7 overflow-x-auto pb-2 custom-scrollbar">
                            {searchStructured.artists.map((artist) => (
                              <div
                                key={artist.id || artist.name}
                                onClick={() => openArtistDetail({ id: artist.id, name: artist.name, avatar: artist.thumbnail, subscribers: artist.subscribers })}
                                className="flex flex-col items-center p-3 rounded-2xl hover:bg-white/5 transition-all cursor-pointer group flex-shrink-0"
                                style={{ width: "170px" }}
                              >
                                <div className="w-28 h-28 sm:w-32 sm:h-32 rounded-full overflow-hidden border-[3px] border-white/20 group-hover:theme-accent-border group-hover:scale-105 transition-all duration-300 shadow-xl bg-black/40 mb-2.5 relative">
                                  <img
                                    src={artist.thumbnail || DEFAULT_TRACK_ART}
                                    alt={artist.name}
                                    onError={(e) => {
                                      e.currentTarget.src = DEFAULT_TRACK_ART;
                                    }}
                                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                  />
                                  <div className="absolute inset-0 bg-black/35 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                                    <div className="w-9 h-9 rounded-full theme-btn-primary flex items-center justify-center shadow-lg">
                                      <Search className="w-4 h-4 text-white" />
                                    </div>
                                  </div>
                                </div>
                                <p className="text-sm font-bold text-white group-hover:theme-accent-text text-center truncate w-full">
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
                              className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:theme-accent-text transition-colors"
                            >
                              <span>Playlist</span>
                            </h3>
                            <button
                              type="button"
                              onClick={() => setActiveSearchTab("playlists")}
                              className="text-xs theme-accent-text hover:brightness-125 font-medium transition-colors cursor-pointer"
                            >
                              Xem tất cả ({searchStructured.playlists.length})
                            </button>
                          </div>
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4">
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
                                  <UnifiedPlayOverlay
                                    isCurrent={activePlayingPlaylistId === pl.id}
                                    isPlaying={activePlayingPlaylistId === pl.id && isPlaying}
                                    isLoading={loadingPlaylistId === pl.id}
                                    onToggle={() => playPlaylistItem(pl)}
                                  />
                                  <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                    Playlist
                                  </span>
                                </div>
                                <p className="text-[12px] font-semibold text-white/90 group-hover:theme-accent-text truncate mt-1.5">
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
                              className="text-[17px] font-bold text-white tracking-wide flex items-center gap-2 cursor-pointer hover:theme-accent-text transition-colors"
                            >
                              <span>Bài hát</span>
                            </h3>
                            <button
                              type="button"
                              onClick={() => setActiveSearchTab("tracks")}
                              className="text-xs theme-accent-text hover:brightness-125 font-medium transition-colors cursor-pointer"
                            >
                              Xem tất cả ({searchResults.length})
                            </button>
                          </div>
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
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
                                        ? "theme-accent-border shadow-lg"
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
                                    <UnifiedPlayOverlay
                                      isCurrent={isCurrentPlaying}
                                      isPlaying={isCurrentPlaying && isPlaying}
                                      onToggle={() => {
                                        if (isCurrentPlaying) {
                                          togglePlayPause();
                                        } else {
                                          playSearchResult(item);
                                        }
                                      }}
                                    />
                                    {(item.duration > 0 || !!item.durationText) && (
                                      <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow">
                                        {item.isMusic !== false && (
                                          <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                        )}
                                        <span>{item.durationText || formatTime(item.duration)}</span>
                                      </span>
                                    )}
                                  </div>
                                  <p
                                    className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${
                                      isCurrentPlaying
                                        ? "theme-accent-text font-bold"
                                        : "text-white/90 group-hover:theme-accent-text"
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
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
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
                                    ? "theme-accent-border shadow-lg"
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
                                <UnifiedPlayOverlay
                                  isCurrent={isCurrentPlaying}
                                  isPlaying={isCurrentPlaying && isPlaying}
                                  onToggle={() => {
                                    if (isCurrentPlaying) {
                                      togglePlayPause();
                                    } else {
                                      playSearchResult(item);
                                    }
                                  }}
                                />
                                {(item.duration > 0 || !!item.durationText) && (
                                  <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow">
                                    {item.isMusic !== false && (
                                      <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                    )}
                                    <span>{item.durationText || formatTime(item.duration)}</span>
                                  </span>
                                )}
                              </div>
                              <p
                                className={`text-[12px] font-semibold truncate mt-1.5 transition-colors ${
                                  isCurrentPlaying
                                    ? "theme-accent-text font-bold"
                                    : "text-white/90 group-hover:theme-accent-text"
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
                              onClick={() => openArtistDetail({ id: artist.id, name: artist.name, avatar: artist.thumbnail, subscribers: artist.subscribers })}
                              className="flex flex-col items-center p-4 rounded-2xl hover:bg-white/5 transition-all cursor-pointer group"
                            >
                              <div className="w-32 h-32 sm:w-36 sm:h-36 rounded-full overflow-hidden border-[3px] border-white/20 group-hover:theme-accent-border group-hover:scale-105 transition-all duration-300 shadow-xl bg-black/40 mb-3 relative">
                                <img
                                  src={artist.thumbnail || DEFAULT_TRACK_ART}
                                  alt={artist.name}
                                  onError={(e) => {
                                    e.currentTarget.src = DEFAULT_TRACK_ART;
                                  }}
                                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                />
                                <div className="absolute inset-0 bg-black/35 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                                  <div className="w-10 h-10 rounded-full theme-btn-primary flex items-center justify-center shadow-lg">
                                    <Search className="w-5 h-5 text-white" />
                                  </div>
                                </div>
                              </div>
                              <p className="text-base font-bold text-white group-hover:theme-accent-text text-center truncate w-full">
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
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4">
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
                                <UnifiedPlayOverlay
                                  isCurrent={activePlayingPlaylistId === pl.id}
                                  isPlaying={activePlayingPlaylistId === pl.id && isPlaying}
                                  isLoading={loadingPlaylistId === pl.id}
                                  onToggle={() => playPlaylistItem(pl)}
                                />
                                <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90">
                                  Playlist
                                </span>
                              </div>
                              <p className="text-[12px] font-semibold text-white/90 group-hover:theme-accent-text truncate mt-1.5">
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
                    className="px-4 py-2 rounded-xl bg-white/10 hover:theme-accent-bg text-xs font-semibold text-white transition-all cursor-pointer"
                  >
                    Quay lại Khám phá
                  </button>
                </div>
              )}
              </div>
            </div>
          ) : (
            /* ===================================================================== */
            /* DISCOVERY VIEW (Nhạc Trending, Mới Phát Hành, Nhạc Sĩ Nổi Bật, ...)   */
            /* ===================================================================== */
            <div className="flex flex-col gap-8 flex-1 min-h-0 overflow-y-auto pr-1 custom-scrollbar pb-6">
              {/* Section 1: Nhạc Trending */}
              <div>
                <div className="flex items-center justify-between mb-3.5">
                  <div className="flex items-center gap-2.5">
                    <Sparkles className="w-5 h-5 theme-accent-text" />
                    <h2 className="text-[20px] font-bold text-white tracking-wide">
                      Nhạc Trending
                    </h2>
                    {isHomeLoading && (
                      <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full theme-badge animate-pulse font-medium">
                        <Loader2 className="w-3 h-3 animate-spin theme-accent-text" />
                        <span>Đang tải...</span>
                      </div>
                    )}
                  </div>
                  {!isHomeLoading && trendingTracks.length > 0 && (
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
                      className="text-xs font-semibold px-3 py-1.5 rounded-full theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-1.5"
                    >
                      <Play className="w-3.5 h-3.5 fill-current" />
                      <span>Phát tất cả</span>
                    </button>
                  )}
                </div>

                {isHomeLoading ? (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {Array.from({ length: 6 }).map((_, idx) => (
                      <div key={idx} className="w-full flex flex-col animate-pulse">
                        <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-purple-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                          <Sparkles className="w-8 h-8 text-white/20 animate-pulse" />
                        </div>
                        <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                        <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {trendingTracks.slice(0, 12).map((item, idx) => {
                      const isCurrent = currentTrack?.id === item.id;
                      const isThisPlaying = isCurrent && isPlaying;
                      return (
                        <div
                          key={item.id + "-" + idx}
                          onClick={() => handlePlaySongItem(item, trendingTracks, idx)}
                          className="w-full flex flex-col cursor-pointer group"
                        >
                          <div
                            className={`relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border ${
                              isCurrent ? "theme-accent-border shadow-lg" : "border-white/5 bg-purple-900/30"
                            }`}
                          >
                            <img
                              src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                              alt={item.title}
                              onError={(e) => {
                                e.currentTarget.src = DEFAULT_TRACK_ART;
                              }}
                              className="w-full h-full object-cover"
                            />
                            <UnifiedPlayOverlay
                              isCurrent={isCurrent}
                              isPlaying={isThisPlaying}
                              onToggle={() => handlePlaySongItem(item, trendingTracks, idx)}
                            />
                            {(item.duration > 0 || !!item.durationText) && (
                              <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
                                {item.isMusic !== false && (
                                  <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                )}
                                <span>{item.durationText || formatTime(item.duration)}</span>
                              </span>
                            )}
                          </div>
                          <p
                            className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
                              isCurrent ? "theme-accent-text font-bold" : "text-white/90 group-hover:theme-accent-text"
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
                )}
              </div>

              {/* Section 2: Mới Phát Hành */}
              <div>
                <div className="flex items-center justify-between mb-3.5">
                  <div className="flex items-center gap-2.5">
                    <Flame className="w-5 h-5 text-orange-400" />
                    <h2 className="text-[20px] font-bold text-white tracking-wide">
                      Mới Phát Hành
                    </h2>
                    {isHomeLoading && (
                      <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-orange-500/10 border border-orange-500/30 text-[11px] text-orange-300 animate-pulse font-medium">
                        <Loader2 className="w-3 h-3 animate-spin text-orange-400" />
                        <span>Đang tải...</span>
                      </div>
                    )}
                  </div>
                  {!isHomeLoading && newReleaseTracks.length > 0 && (
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
                  )}
                </div>

                {isHomeLoading ? (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {Array.from({ length: 6 }).map((_, idx) => (
                      <div key={idx} className="w-full flex flex-col animate-pulse">
                        <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-orange-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                          <Flame className="w-8 h-8 text-orange-400/20 animate-pulse" />
                        </div>
                        <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                        <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {newReleaseTracks.slice(0, 12).map((item, idx) => {
                      const isCurrent = currentTrack?.id === item.id;
                      const isThisPlaying = isCurrent && isPlaying;
                      return (
                        <div
                          key={item.id + "-" + idx}
                          onClick={() => handlePlaySongItem(item, newReleaseTracks, idx)}
                          className="w-full flex flex-col cursor-pointer group"
                        >
                          <div
                            className={`relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border ${
                              isCurrent ? "theme-accent-border shadow-lg" : "border-white/5 bg-purple-900/30"
                            }`}
                          >
                            <img
                              src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                              alt={item.title}
                              onError={(e) => {
                                e.currentTarget.src = DEFAULT_TRACK_ART;
                              }}
                              className="w-full h-full object-cover"
                            />
                            <UnifiedPlayOverlay
                              isCurrent={isCurrent}
                              isPlaying={isThisPlaying}
                              onToggle={() => handlePlaySongItem(item, newReleaseTracks, idx)}
                            />
                            {(item.duration > 0 || !!item.durationText) && (
                              <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
                                {item.isMusic !== false && (
                                  <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                )}
                                <span>{item.durationText || formatTime(item.duration)}</span>
                              </span>
                            )}
                          </div>
                          <p
                            className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
                              isCurrent ? "theme-accent-text font-bold" : "text-white/90 group-hover:theme-accent-text"
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
                )}
              </div>

              {/* Section 3: Nhạc Sĩ Nổi Bật (Đổi từ Nhạc Sĩ) */}
              <div>
                <div className="flex items-center gap-2.5 mb-3.5">
                  <Mic2 className="w-5 h-5 theme-sec-accent-text" />
                  <h2 className="text-[20px] font-bold text-white tracking-wide">
                    Nhạc Sĩ Nổi Bật
                  </h2>
                  {isHomeLoading && (
                    <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full theme-badge animate-pulse font-medium">
                      <Loader2 className="w-3 h-3 animate-spin theme-accent-text" />
                      <span>Đang tải...</span>
                    </div>
                  )}
                </div>

                {isHomeLoading ? (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {Array.from({ length: 6 }).map((_, idx) => (
                      <div key={idx} className="w-full flex flex-col items-center animate-pulse">
                        <div className="w-full aspect-square rounded-full bg-gradient-to-br from-purple-900/30 via-purple-950/20 to-black/40 border-2 border-white/5 flex items-center justify-center">
                          <Mic2 className="w-8 h-8 text-purple-400/20 animate-pulse" />
                        </div>
                        <div className="h-3 w-1/2 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                        <div className="h-2.5 w-1/3 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                    {FEATURED_ARTISTS.map((artist, idx) => (
                      <div
                        key={idx}
                        onClick={() => openArtistDetail({ name: artist.name, avatar: artist.avatar, genre: artist.genre })}
                        className="w-full flex flex-col items-center cursor-pointer group"
                      >
                        <div className="w-full aspect-square rounded-full overflow-hidden bg-purple-900/30 shadow-md border-2 border-white/10 group-hover:theme-accent-border group-hover:scale-[1.04] transition-all duration-200">
                          <img
                            src={artist.avatar}
                            alt={artist.name}
                            onError={(e) => {
                              e.currentTarget.src = DEFAULT_TRACK_ART;
                            }}
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <p className="text-[12px] font-semibold text-white/90 group-hover:theme-accent-text truncate mt-2 text-center w-full transition-colors">
                          {artist.name}
                        </p>
                        <p className="text-[10px] text-purple-200/60 truncate text-center w-full">
                          {artist.genre}
                        </p>
                      </div>
                    ))}
                  </div>
                )}
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
                      {isLibraryLoading && (
                        <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-[11px] text-emerald-300 animate-pulse font-medium">
                          <Loader2 className="w-3 h-3 animate-spin text-emerald-400" />
                          <span>Đang tải...</span>
                        </div>
                      )}
                    </div>

                    {isLibraryLoading ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                        {Array.from({ length: 6 }).map((_, idx) => (
                          <div key={idx} className="w-full flex flex-col animate-pulse">
                            <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-emerald-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                              <Clock className="w-8 h-8 text-emerald-400/20 animate-pulse" />
                            </div>
                            <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                            <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                          </div>
                        ))}
                      </div>
                    ) : recentTracksToDisplay.length > 0 ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                        {recentTracksToDisplay.map((item, idx) => {
                          const isCurrent = currentTrack?.id === item.id;
                          const isThisPlaying = isCurrent && isPlaying;
                          return (
                            <div
                              key={item.id + "-" + idx}
                              onClick={() => handlePlaySongItem(item, recentTracksToDisplay, idx)}
                              className="w-full flex flex-col cursor-pointer group"
                            >
                              <div
                                className={`relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border ${
                                  isCurrent ? "theme-accent-border shadow-lg" : "border-white/5 bg-purple-900/30"
                                }`}
                              >
                                <img
                                  src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                                  alt={item.title}
                                  onError={(e) => {
                                    e.currentTarget.src = DEFAULT_TRACK_ART;
                                  }}
                                  className="w-full h-full object-cover"
                                />
                                <UnifiedPlayOverlay
                                  isCurrent={isCurrent}
                                  isPlaying={isThisPlaying}
                                  onToggle={() => handlePlaySongItem(item, recentTracksToDisplay, idx)}
                                />
                                {(item.duration > 0 || !!item.durationText) && (
                                  <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
                                    {item.isMusic !== false && (
                                      <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                    )}
                                    <span>{item.durationText || formatTime(item.duration)}</span>
                                  </span>
                                )}
                              </div>
                              <p
                                className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
                                  isCurrent ? "theme-accent-text font-bold" : "text-white/90 group-hover:theme-accent-text"
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
                      {isLibraryLoading && (
                        <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-rose-500/10 border border-rose-500/30 text-[11px] text-rose-300 animate-pulse font-medium">
                          <Loader2 className="w-3 h-3 animate-spin text-rose-400" />
                          <span>Đang tải...</span>
                        </div>
                      )}
                    </div>

                    {isLibraryLoading ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                        {Array.from({ length: 6 }).map((_, idx) => (
                          <div key={idx} className="w-full flex flex-col animate-pulse">
                            <div className="relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-rose-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                              <Heart className="w-8 h-8 text-rose-400/20 animate-pulse" />
                            </div>
                            <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                            <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                          </div>
                        ))}
                      </div>
                    ) : recommendedTracksToDisplay.length > 0 ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4 w-full">
                        {recommendedTracksToDisplay.map((item, idx) => {
                          const isCurrent = currentTrack?.id === item.id;
                          const isThisPlaying = isCurrent && isPlaying;
                          return (
                            <div
                              key={item.id + "-" + idx}
                              onClick={() => handlePlaySongItem(item, recommendedTracksToDisplay, idx)}
                              className="w-full flex flex-col cursor-pointer group"
                            >
                              <div
                                className={`relative w-full aspect-square rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border ${
                                  isCurrent ? "theme-accent-border shadow-lg" : "border-white/5 bg-purple-900/30"
                                }`}
                              >
                                <img
                                  src={item.thumbnail || `https://i.ytimg.com/vi/${item.id}/mqdefault.jpg`}
                                  alt={item.title}
                                  onError={(e) => {
                                    e.currentTarget.src = DEFAULT_TRACK_ART;
                                  }}
                                  className="w-full h-full object-cover"
                                />
                                <UnifiedPlayOverlay
                                  isCurrent={isCurrent}
                                  isPlaying={isThisPlaying}
                                  onToggle={() => handlePlaySongItem(item, recommendedTracksToDisplay, idx)}
                                />
                                {(item.duration > 0 || !!item.durationText) && (
                                  <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
                                    {item.isMusic !== false && (
                                      <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                    )}
                                    <span>{item.durationText || formatTime(item.duration)}</span>
                                  </span>
                                )}
                              </div>
                              <p
                                className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
                                  isCurrent ? "theme-accent-text font-bold" : "text-white/90 group-hover:theme-accent-text"
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
          className={`${
            mobileTab === "player" ? "flex w-full" : "hidden"
          } lg:flex lg:w-[310px] xl:w-[340px] flex-shrink-0 figma-panel flex-col min-h-0 relative z-20 overflow-y-auto lg:overflow-visible p-4 sm:p-5 lg:px-[18px] lg:pt-[26px] lg:pb-[20px]`}
          style={{ backdropFilter: "none", WebkitBackdropFilter: "none" }}
        >
          {/* Mobile Collapse / Back Header */}
          <div className="lg:hidden flex items-center justify-between pb-2 mb-2 border-b border-white/10 flex-shrink-0">
            <button
              type="button"
              onClick={() => setMobileTab("explore")}
              className="flex items-center gap-1.5 text-xs text-white/70 hover:text-white cursor-pointer"
            >
              <ChevronDown className="w-4 h-4" />
              <span>Thu nhỏ trình phát</span>
            </button>
            <span className="text-[11px] font-bold theme-accent-text uppercase tracking-wider">Đang phát</span>
          </div>

          {/* Top Part: Now Playing Artwork Card with Dynamic Artwork & Glow */}
          <div
            className="w-full aspect-square rounded-[22px] relative shadow-2xl bg-[#1b0833] flex-shrink-0 group max-w-[340px] mx-auto lg:max-w-none"
            style={{ marginBottom: "18px" }}
          >
            {/* Clamped container for artwork & gradient so popups can overflow cleanly */}
            <div className="absolute inset-0 rounded-[22px] overflow-hidden pointer-events-none">
              {/* Real Dynamic Artwork or Idle Standby Display */}
              {!currentTrack ? (
                <div className="w-full h-full flex flex-col items-center justify-center bg-gradient-to-br from-[#240a50] to-[#120324] relative overflow-hidden select-none">
                  <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(236,72,153,0.15)_0%,transparent_70%)]" />
                  <div className="w-16 h-16 rounded-full theme-badge flex items-center justify-center theme-accent-text mb-2.5 p-3 overflow-hidden shadow-md">
                    <img src="./logo_icon.png" alt="Kahoii Music" className="w-full h-full object-contain drop-shadow" />
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
            </div>

            {/* Track Title and Artist floating gently over the gradient (only rendered when track is active) */}
            {currentTrack && (
              <div className="absolute bottom-[92px] left-4 right-4 z-20 pointer-events-none">
                <p className="text-[13px] font-bold text-white truncate drop-shadow-md">
                  {currentTrack.title}
                </p>
                <p className="text-[11px] text-white/70 truncate drop-shadow">
                  {formatTrackArtist(currentTrack.title, currentTrack.author)}
                </p>
              </div>
            )}

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
                      ? "theme-badge-active shadow-md"
                      : "bg-black/40 text-white/80 hover:text-white hover:bg-black/60"
                  }`}
                  title="Tùy chọn bài hát"
                >
                  <Menu className="w-4 h-4" />
                </button>

                {/* Popup 1 (Menu chính: Combobox) */}
                {showPlayerMenu && currentTrack && (
                  <div className="absolute top-9 left-0 z-40 theme-menu-glass rounded-xl shadow-2xl p-1.5 min-w-[205px] flex flex-col gap-1 text-xs animate-fade-in border theme-accent-border/40">
                    {/* Option 1: Lưu vào playlist */}
                    <button
                      type="button"
                      onClick={() => {
                        setShowLibrarySubmenu(!showLibrarySubmenu);
                      }}
                      className={`flex items-center justify-between w-full px-2.5 py-2 rounded-lg transition-colors cursor-pointer text-left ${
                        showLibrarySubmenu ? "bg-white/15 theme-accent-text font-bold" : "hover:bg-white/10 text-white/90"
                      }`}
                    >
                      <span className="flex items-center gap-2">
                        <FolderPlus className="w-4 h-4 theme-accent-text" />
                        <span>Lưu vào playlist</span>
                      </span>
                      <div className="flex items-center gap-1">
                        {libraryData.playlists.some((pl) => pl.items?.some((it: any) => it.id === currentTrack.id)) && (
                          <Check className="w-3.5 h-3.5 text-emerald-400" />
                        )}
                        <ChevronLeft className="w-3.5 h-3.5 text-white/40" />
                      </div>
                    </button>

                    {/* Option 2: Lưu ngoại tuyến (nghe trong app như YouTube) */}
                    <button
                      type="button"
                      disabled={savingOfflineTrackId === currentTrack.id}
                      onClick={() => handleSaveOfflineTrack(currentTrack)}
                      className="flex items-center justify-between w-full px-2.5 py-2 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                    >
                      <span className="flex items-center gap-2">
                        {savingOfflineTrackId === currentTrack.id ? (
                          <Loader2 className="w-4 h-4 theme-accent-text animate-spin" />
                        ) : (
                          <HardDriveDownload className="w-4 h-4 theme-accent-text" />
                        )}
                        <span>
                          {savingOfflineTrackId === currentTrack.id
                            ? "Đang lưu ngoại tuyến..."
                            : offlineTrackIds.has(currentTrack.id)
                            ? "Đã lưu offline (Bấm để xóa)"
                            : "Lưu nghe offline (như YouTube)"}
                        </span>
                      </span>
                      {offlineTrackIds.has(currentTrack.id) && (
                        <Check className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
                      )}
                    </button>

                    {/* Option 3: Tải file MP3 về máy */}
                    <button
                      type="button"
                      onClick={() => handleDownloadTrack(currentTrack)}
                      className="flex items-center justify-between w-full px-2.5 py-2 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                    >
                      <span className="flex items-center gap-2">
                        <Download className="w-4 h-4 theme-accent-text" />
                        <span>Tải file MP3 về máy</span>
                      </span>
                      {downloadedTrackIds.has(currentTrack.id) && (
                        <Check className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
                      )}
                    </button>
                  </div>
                )}

                {/* Popup 2 (Submenu mở sang bên trái trên Desktop, và responsive trên Mobile) */}
                {showPlayerMenu && showLibrarySubmenu && currentTrack && (
                  <div className="absolute top-9 left-0 lg:left-auto lg:right-[calc(100%+8px)] z-50 theme-menu-glass rounded-xl shadow-2xl p-2 min-w-[200px] max-w-[230px] flex flex-col gap-1.5 text-xs animate-fade-in border theme-accent-border">
                    <div className="flex items-center justify-between px-1 pb-1 border-b border-white/10 text-[10px] font-bold uppercase tracking-wider theme-accent-text">
                      <button
                        type="button"
                        onClick={() => setShowLibrarySubmenu(false)}
                        className="lg:hidden flex items-center gap-1 text-[10px] text-white/60 hover:text-white cursor-pointer mr-1"
                      >
                        <ChevronLeft className="w-3 h-3" />
                        <span>Quay lại</span>
                      </button>
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
                          className="px-3 py-1.5 rounded-lg theme-btn-primary text-white text-xs font-semibold cursor-pointer transition-colors"
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
                            className="flex items-center gap-1.5 px-2 py-1.5 rounded-lg hover:bg-white/10 theme-accent-text font-medium transition-colors cursor-pointer text-left"
                          >
                            <Plus className="w-3.5 h-3.5" />
                            <span>Tạo playlist mới</span>
                          </button>
                        ) : (
                          <div className="flex items-center gap-1 p-1 bg-black/40 rounded-lg theme-accent-border">
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
                  className="w-7 h-7 rounded-lg bg-black/40 backdrop-blur-md flex items-center justify-center text-white/70 hover:theme-accent-text hover:bg-black/60 transition-all cursor-pointer"
                  title="Âm lượng"
                >
                  {isMuted || volume === 0 ? <VolumeX className="w-4 h-4" /> : <Volume2 className="w-4 h-4" />}
                </button>
                {showVolumeSlider && (
                  <div className="absolute top-9 left-0 p-2.5 rounded-xl theme-menu-glass shadow-2xl flex items-center gap-2 z-30 border theme-accent-border/40">
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
                      className="w-20 h-1 bg-white/30 rounded appearance-none cursor-pointer theme-slider"
                    />
                    <span className="text-[10px] theme-accent-text font-mono font-bold">
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
                  repeatMode === "all" || repeatMode === "one" ? "theme-accent-text drop-shadow-[0_0_8px_var(--theme-accent)]" : repeatMode === "shuffle" ? "theme-sec-accent-text drop-shadow-[0_0_8px_var(--theme-sec-accent)]" : "text-white/40 hover:text-white"
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
                className="p-1.5 text-white/70 hover:theme-accent-text transition-all cursor-pointer hover:scale-115 active:scale-90"
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
                className="p-1.5 text-white/70 hover:theme-accent-text transition-all cursor-pointer hover:scale-115 active:scale-90"
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
                    sleepTimerSecondsLeft ? "theme-accent-text font-bold" : "text-white/60 hover:text-white"
                  }`}
                >
                  <Clock className="w-5 h-5" />
                  {sleepTimerMinutes && (
                    <span className="absolute -top-1 -right-1 px-1 rounded-full theme-accent-bg text-[8px] text-white font-mono font-bold animate-pulse">
                      {sleepTimerMinutes}m
                    </span>
                  )}
                </button>
                {showSleepTimerMenu && (
                  <div className="absolute bottom-9 right-0 p-2 rounded-xl theme-menu-glass shadow-2xl flex flex-col gap-1 z-30 min-w-[130px] animate-fade-in border theme-accent-border/40">
                    <span className="text-[10px] uppercase font-bold theme-accent-text tracking-wider px-2 py-0.5">
                      Hẹn giờ tắt
                    </span>
                    {[15, 30, 45, 60].map((m) => (
                      <button
                        key={m}
                        type="button"
                        onClick={() => startSleepTimer(m)}
                        className={`text-left text-xs px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer ${
                          sleepTimerMinutes === m ? "theme-badge-active" : "hover:bg-white/10 text-white/80"
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

          {/* Sidebar 2 Tab Switcher: Hàng đợi (Queue) & Lời bài hát (Lyrics) */}
          <div className="relative mb-3 flex-shrink-0">
            {/* Inactive Background Capsules matching app theme but darker */}
            <div className="absolute inset-0 grid grid-cols-2 gap-1.5 pointer-events-none">
              <div className="rounded-xl theme-tab-inactive" />
              <div className="rounded-xl theme-tab-inactive" />
            </div>

            {/* Active Sliding Glowing Pill */}
            <div
              className="absolute top-0 bottom-0 rounded-xl pointer-events-none z-10"
              style={{
                width: "calc((100% - 6px) / 2)",
                left: 0,
                transform: `translateX(${sidebar2Tab === "queue" ? "0%" : "calc(100% + 6px)"})`,
                transition: "transform 0.32s cubic-bezier(0.22, 1, 0.36, 1), background 0.8s ease, box-shadow 0.8s ease",
                background: "var(--theme-btn-gradient)",
                boxShadow: "0 4px 18px var(--theme-panel-border)",
              }}
            />

            {/* Clickable Buttons */}
            <div className="relative z-20 grid grid-cols-2 gap-1.5">
              <button
                type="button"
                onClick={() => setSidebar2Tab("queue")}
                className={`py-1.5 px-2 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer flex items-center justify-center gap-1.5 ${
                  sidebar2Tab === "queue" ? "text-white" : "text-white/70 hover:text-white"
                }`}
              >
                <ListMusic className="w-3.5 h-3.5" />
                <span>Hàng đợi {queueTracks.length > 0 && `(${queueTracks.length})`}</span>
              </button>
              <button
                type="button"
                onClick={() => setSidebar2Tab("lyrics")}
                className={`py-1.5 px-2 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer flex items-center justify-center gap-1.5 ${
                  sidebar2Tab === "lyrics" ? "text-white" : "text-white/70 hover:text-white"
                }`}
              >
                <Mic2 className="w-3.5 h-3.5" />
                <span>Lời bài hát</span>
              </button>
            </div>
          </div>

          {/* Tab 1: Hàng đợi phát (Queue List) */}
          {sidebar2Tab === "queue" && (
            <>
              <div className="flex items-center justify-between px-1 pb-2 flex-shrink-0">
                <span className="text-xs font-semibold text-white/70">
                  Danh sách phát {queueTracks.length > 0 && `(${queueTracks.length})`}
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
                      isQueueSorted ? "theme-badge-active" : "border-white/10 hover:border-white/30 text-white/60 hover:text-white"
                    }`}
                  >
                    <ArrowDownAZ className="w-3.5 h-3.5" />
                    <span>Sort</span>
                  </button>
                )}
              </div>

              <div
                className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1"
                style={{ gap: "8px" }}
              >
                {queueTracks.length > 0 ? (
                  queueTracks.map((track, i) => {
                    const isCurrent = currentTrack ? currentTrack.id === track.id : i === currentIndex;
                    const artistLabel = formatTrackArtist(track.title, track.author);
                    return (
                      <div
                        key={track.id + i}
                        onClick={() => {
                          setCurrentIndex(i);
                          currentIndexRef.current = i;
                          loadAndPlayTrack(track, true);
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
                          <p className="text-xs font-semibold text-white truncate group-hover:theme-accent-text transition-colors">
                            {track.title}
                          </p>
                          {artistLabel && (
                            <p className="text-[10px] text-white/50 truncate">
                              {artistLabel}
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
            </>
          )}

          {/* Tab 2: Lời bài hát (Lyrics) */}
          {sidebar2Tab === "lyrics" && (
            <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
              {/* Lyrics Sub-header: Sync Status Badge & Dual/Romaji/Pinyin/Song ngữ Toggle Pill Button */}
              <div className="flex items-center justify-between px-1 pb-2 flex-shrink-0 border-b border-white/10 mb-1">
                <div className="flex items-center gap-1.5">
                  {isLyricsSynced ? (
                    <span className="flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider theme-badge-active shadow-sm">
                      <Sparkles className="w-3 h-3 theme-accent-text" />
                      Audio synced
                    </span>
                  ) : parsedLyrics.length > 0 || _lyrics?.plainLyrics ? (
                    <span className="px-2.5 py-0.5 rounded-full text-[10px] font-medium bg-white/10 text-white/60">
                      Non synced
                    </span>
                  ) : (
                    <span className="text-[11px] text-white/40 font-medium">
                      Lời bài hát
                    </span>
                  )}
                </div>

                {(parsedLyrics.length > 0 || Boolean(_lyrics?.plainLyrics)) && (
                  <button
                    type="button"
                    onClick={toggleLyricDisplayMode}
                    title="Nhấn để đổi: Song ngữ (2 dòng) → Phiên âm thuần → Chữ gốc"
                    className={`flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-semibold transition-all cursor-pointer shadow-md border ${
                      lyricDisplayMode === "dual"
                        ? "theme-btn-primary text-white border-transparent scale-[1.02]"
                        : lyricDisplayMode === "romanized"
                        ? "bg-cyan-500/20 text-cyan-300 border-cyan-500/40 hover:bg-cyan-500/30"
                        : "theme-badge hover:theme-accent-border text-white/70"
                    }`}
                  >
                    <Languages className="w-3.5 h-3.5" />
                    <span>{romaziPillLabel}</span>
                  </button>
                )}
              </div>

              {/* Scrollable lyrics area */}
              <div
                ref={lyricsContainerRef}
                className={`relative flex-1 flex flex-col min-h-0 pr-1 text-center ${
                  parsedLyrics.length > 0 ? "overflow-y-auto custom-scrollbar" : "overflow-hidden"
                }`}
                style={{
                  paddingTop: parsedLyrics.length > 0 ? "110px" : "0px",
                  paddingBottom: parsedLyrics.length > 0 ? "150px" : "0px",
                  gap: parsedLyrics.length > 0 ? "8px" : "0px",
                }}
              >
                {parsedLyrics.length > 0 ? (
                  parsedLyrics.map((line, idx) => {
                    const isActive = isLyricsSynced && idx === activeLyricIndex;
                    const isRomanizedOnly = lyricDisplayMode === "romanized";
                    const isDual = lyricDisplayMode === "dual";

                    let primaryText = line.text;
                    let secondaryText: string | null = null;

                    if (isRomanizedOnly) {
                      primaryText = line.romanized || line.translation || line.text;
                    } else if (isDual) {
                      primaryText = line.text;
                      secondaryText = line.romanized || line.translation || null;
                    }

                    return (
                      <div
                        key={idx}
                        ref={isActive ? activeLyricRef : undefined}
                        onClick={() => {
                          if (line.time > 0) seekToTime(line.time);
                        }}
                        className={`transition-all select-none cursor-pointer text-center rounded-2xl px-4 py-2.5 ${
                          isActive
                            ? "scale-[1.04] theme-accent-text font-bold drop-shadow-md bg-white/10 border border-white/10 shadow-lg"
                            : "scale-100 font-medium opacity-35 hover:opacity-80 hover:bg-white/5 border border-transparent"
                        }`}
                        style={{
                          transition: "transform 0.4s cubic-bezier(0.2, 0.8, 0.2, 1), opacity 0.35s ease, background-color 0.3s ease, color 0.3s ease",
                        }}
                      >
                        <p className={`leading-snug tracking-wide ${isActive ? "text-[15px]" : "text-[13px] text-white"}`}>
                          {primaryText}
                        </p>
                        {secondaryText && (
                          <p
                            className={`mt-1 font-normal leading-normal transition-opacity ${
                              isActive ? "text-xs text-white/80 opacity-95" : "text-[11px] text-white/50 opacity-70"
                            }`}
                          >
                            {secondaryText}
                          </p>
                        )}
                      </div>
                    );
                  })
                ) : _lyrics?.plainLyrics ? (
                  <div className="py-4 text-xs leading-relaxed text-white/80 whitespace-pre-line text-center font-medium my-auto">
                    {_lyrics.plainLyrics}
                  </div>
                ) : (
                  <div className="h-full w-full flex-1 flex flex-col items-center justify-center p-6 text-center text-white/40 text-xs gap-3 my-auto">
                    <Mic2 className="w-10 h-10 opacity-35 theme-accent-text" />
                    <p className="font-semibold text-white/70 text-sm">
                      {currentTrack ? "Chưa có lời bài hát" : "Chưa phát bài hát nào"}
                    </p>
                    <p className="text-[11px] text-white/40 max-w-[210px] leading-relaxed">
                      {currentTrack
                        ? "Không tìm thấy lời đồng bộ cho bài hát này trên hệ thống"
                        : "Chọn một bài hát để bắt đầu theo dõi lời bài hát"}
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}
        </aside>
      </main>

      {/* ========================================================================= */}
      {/* MOBILE MINI-PLAYER BAR (Floating above bottom nav when not in 'player' tab)*/}
      {/* ========================================================================= */}
      {currentTrack && mobileTab !== "player" && (
        <div
          onClick={() => setMobileTab("player")}
          className="lg:hidden fixed bottom-[62px] left-2.5 right-2.5 z-40 bg-[var(--theme-panel-bg)]/95 backdrop-blur-2xl border theme-accent-border rounded-2xl p-2 flex items-center gap-2.5 shadow-2xl cursor-pointer animate-fade-in"
        >
          <img
            src={
              currentTrack.thumbnail && !currentTrack.thumbnail.includes("figma")
                ? (currentTrack.thumbnail.includes("i.ytimg.com")
                    ? currentTrack.thumbnail.replace("mqdefault.jpg", "hqdefault.jpg")
                    : currentTrack.thumbnail)
                : (currentTrack.id ? `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg` : DEFAULT_TRACK_ART)
            }
            alt=""
            onError={(e) => {
              e.currentTarget.src = DEFAULT_TRACK_ART;
            }}
            className="w-10 h-10 rounded-xl object-cover flex-shrink-0 shadow-md"
          />
          <div className="flex-1 min-w-0 pr-1">
            <p className="text-xs font-bold text-white truncate drop-shadow-sm">{currentTrack.title}</p>
            <p className="text-[10px] text-white/60 truncate">{formatTrackArtist(currentTrack.title, currentTrack.author)}</p>
          </div>
          <div className="flex items-center gap-1.5 flex-shrink-0" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              onClick={toggleCurrentFavorite}
              className="p-1.5 text-white/70 hover:text-white cursor-pointer"
              title="Yêu thích"
            >
              <Heart
                className={`w-4 h-4 ${
                  userSession?.isLoggedIn && libraryData.favorites.some((t) => t.id === currentTrack.id)
                    ? "fill-red-500 text-red-500"
                    : "text-white/60"
                }`}
              />
            </button>
            <button
              type="button"
              onClick={togglePlayPause}
              className="w-8 h-8 rounded-full theme-btn-primary flex items-center justify-center text-white shadow-md cursor-pointer hover:scale-105 active:scale-95 transition-all"
              title={isPlaying ? "Tạm dừng" : "Phát"}
            >
              {isLoading ? (
                <Loader2 className="w-4 h-4 animate-spin text-white" />
              ) : isPlaying ? (
                <Pause className="w-4 h-4 fill-current" />
              ) : (
                <Play className="w-4 h-4 fill-current translate-x-0.5" />
              )}
            </button>
            <button
              type="button"
              onClick={handleNext}
              className="p-1.5 text-white/70 hover:text-white cursor-pointer"
              title="Bài tiếp theo"
            >
              <SkipForward className="w-4 h-4 fill-current" />
            </button>
          </div>
          {/* Micro progress line at the bottom */}
          <div className="absolute bottom-0 left-3 right-3 h-[2px] bg-white/10 rounded-full overflow-hidden">
            <div
              className="h-full"
              style={{
                width: `${duration ? Math.min(100, (currentTime / duration) * 100) : 0}%`,
                background: "var(--theme-btn-gradient)",
              }}
            />
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MOBILE BOTTOM NAVIGATION BAR (lg:hidden)                                  */}
      {/* ========================================================================= */}
      <nav className="lg:hidden fixed bottom-0 left-0 right-0 h-[56px] bg-[var(--theme-panel-bg)]/95 backdrop-blur-2xl border-t border-white/10 z-40 flex items-center justify-around px-2 shadow-2xl">
        <button
          type="button"
          onClick={() => setMobileTab("explore")}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer ${
            mobileTab === "explore" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
          }`}
        >
          <Compass className="w-5 h-5" />
          <span className="text-[10px]">Khám phá</span>
        </button>

        <button
          type="button"
          onClick={() => setMobileTab("library")}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer ${
            mobileTab === "library" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
          }`}
        >
          <FolderHeart className="w-5 h-5" />
          <span className="text-[10px]">Thư viện</span>
        </button>

        <button
          type="button"
          onClick={() => setMobileTab("player")}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer relative ${
            mobileTab === "player" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
          }`}
        >
          <div className="relative">
            <Music className="w-5 h-5" />
            {isPlaying && (
              <span className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            )}
          </div>
          <span className="text-[10px]">Đang phát</span>
        </button>
      </nav>

      {/* ========================================================================= */}
      {/* 3. USER PROFILE MODAL (Clicked from "avatar" button)                      */}
      {/* ========================================================================= */}
      {isAccountOpen && (
        <div className="fixed inset-0 bg-black/75 backdrop-blur-xl flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="w-full max-w-[430px] flex flex-col p-6 rounded-[28px] relative shadow-2xl border theme-accent-border overflow-hidden backdrop-blur-2xl bg-[var(--theme-panel-bg)]/90 transition-all duration-300 max-h-[90vh] overflow-y-auto">
            {/* Ambient dynamic glowing orbs */}
            <div className="absolute -top-16 -right-16 w-52 h-52 rounded-full blur-3xl opacity-35 pointer-events-none theme-accent-bg" />
            <div className="absolute -bottom-16 -left-16 w-52 h-52 rounded-full blur-3xl opacity-20 pointer-events-none theme-accent-bg" />

            <div className="flex items-center justify-between pb-3.5 border-b border-white/10 relative z-20">
              <span className="font-extrabold text-sm theme-accent-text flex items-center gap-2">
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
                <div className="flex items-center gap-3.5 p-3.5 rounded-2xl bg-black/25 border border-white/10 backdrop-blur-md shadow-inner">
                  <div className="relative flex-shrink-0">
                    <img
                      src={
                        userSession.user?.avatarBase64
                          ? `data:image/png;base64,${userSession.user.avatarBase64}`
                          : `${API_BASE}/api/user/avatar?t=${avatarKey}${getAuthToken() ? `&token=${getAuthToken()}` : ""}`
                      }
                      alt="Avatar"
                      onError={(e) => {
                        e.currentTarget.src = DEFAULT_TRACK_ART;
                      }}
                      className="w-14 h-14 rounded-full object-cover border-2 theme-accent-border shadow-lg shadow-black/40"
                    />
                    <div
                      className="absolute bottom-0 right-0 w-3.5 h-3.5 rounded-full bg-emerald-500 border-2 border-[var(--theme-panel-bg)] shadow-sm"
                      title="Đang hoạt động"
                    />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5">
                      <h3 className="font-bold text-base text-white truncate">
                        {userSession.user?.name || "Kahoii User"}
                      </h3>
                      <ShieldCheck className="w-4 h-4 theme-accent-text flex-shrink-0" />
                    </div>
                    <p className="text-xs text-white/70 truncate mt-0.5">
                      {userSession.user?.email || "danlanga26@gmail.com"}
                    </p>
                    <span className="inline-block mt-1 text-[10px] font-semibold px-2 py-0.5 rounded-full theme-badge">
                      Kahoii Member • Cloud Synced
                    </span>
                  </div>
                </div>

                {/* User Stats Grid (Synced Live with Library & Dynamic Theme Palette) */}
                <div className="grid grid-cols-4 gap-2 text-center">
                  <button
                    type="button"
                    onClick={() => {
                      setActiveLeftTab("favorites");
                      setIsAccountOpen(false);
                    }}
                    className="p-2.5 rounded-2xl bg-black/25 border theme-accent-border/40 hover:border-[var(--theme-accent)] transition-all duration-300 backdrop-blur-md cursor-pointer hover:scale-[1.03] active:scale-[0.98] group flex flex-col items-center justify-center shadow-md relative overflow-hidden"
                    title="Xem danh sách bài hát yêu thích"
                  >
                    <div className="absolute inset-0 opacity-0 group-hover:opacity-15 transition-opacity duration-300 pointer-events-none theme-accent-bg" />
                    <p className="text-lg font-black theme-accent-text tracking-wide transition-transform group-hover:scale-110">
                      {libraryData.favorites.length || userSession?.user?.stats?.favoritesCount || 0}
                    </p>
                    <p className="text-[10.5px] font-semibold text-white/80 mt-0.5 group-hover:text-white transition-colors truncate">
                      Yêu thích
                    </p>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setActiveLeftTab("offline");
                      setIsAccountOpen(false);
                    }}
                    className="p-2.5 rounded-2xl bg-black/25 border theme-accent-border/40 hover:border-[var(--theme-accent)] transition-all duration-300 backdrop-blur-md cursor-pointer hover:scale-[1.03] active:scale-[0.98] group flex flex-col items-center justify-center shadow-md relative overflow-hidden"
                    title="Xem danh sách bài hát đã tải"
                  >
                    <div className="absolute inset-0 opacity-0 group-hover:opacity-15 transition-opacity duration-300 pointer-events-none theme-accent-bg" />
                    <p className="text-lg font-black theme-accent-text tracking-wide transition-transform group-hover:scale-110">
                      {libraryData.savedTracks.length || userSession?.user?.stats?.savedTracksCount || offlineTracks.length || 0}
                    </p>
                    <p className="text-[10.5px] font-semibold text-white/80 mt-0.5 group-hover:text-white transition-colors truncate">
                      Đã tải
                    </p>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setActiveLeftTab("history");
                      setIsAccountOpen(false);
                    }}
                    className="p-2.5 rounded-2xl bg-black/25 border theme-accent-border/40 hover:border-[var(--theme-accent)] transition-all duration-300 backdrop-blur-md cursor-pointer hover:scale-[1.03] active:scale-[0.98] group flex flex-col items-center justify-center shadow-md relative overflow-hidden"
                    title="Xem lịch sử phát nhạc"
                  >
                    <div className="absolute inset-0 opacity-0 group-hover:opacity-15 transition-opacity duration-300 pointer-events-none theme-accent-bg" />
                    <p className="text-lg font-black theme-accent-text tracking-wide transition-transform group-hover:scale-110">
                      {libraryData.history.length || userSession?.user?.stats?.historyCount || 0}
                    </p>
                    <p className="text-[10.5px] font-semibold text-white/80 mt-0.5 group-hover:text-white transition-colors truncate">
                      Lịch sử
                    </p>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setActiveLeftTab("library");
                      setIsAccountOpen(false);
                    }}
                    className="p-2.5 rounded-2xl bg-black/25 border theme-accent-border/40 hover:border-[var(--theme-accent)] transition-all duration-300 backdrop-blur-md cursor-pointer hover:scale-[1.03] active:scale-[0.98] group flex flex-col items-center justify-center shadow-md relative overflow-hidden"
                    title="Xem danh sách phát cá nhân"
                  >
                    <div className="absolute inset-0 opacity-0 group-hover:opacity-15 transition-opacity duration-300 pointer-events-none theme-accent-bg" />
                    <p className="text-lg font-black theme-accent-text tracking-wide transition-transform group-hover:scale-110">
                      {libraryData.playlists.length || userSession?.user?.stats?.playlistsCount || 0}
                    </p>
                    <p className="text-[10.5px] font-semibold text-white/80 mt-0.5 group-hover:text-white transition-colors truncate">
                      Playlists
                    </p>
                  </button>
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
                    className="w-full py-2.5 rounded-xl theme-btn-primary hover:opacity-95 text-white font-bold text-xs shadow-lg transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
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
              /* Auth Form (Login or Register with Extension-style Jelly Fluid Switch & Glass Blur) */
              <div className="flex flex-col gap-3.5 mt-3 relative z-20">
                {/* Segmented Liquid Tabs Container */}
                <div className="relative flex items-center w-full p-1 rounded-2xl bg-black/40 border border-white/10 overflow-hidden mb-1">
                  {/* Active Jelly Liquid Pill: Glides with smooth elastic cubic-bezier */}
                  <div
                    className="absolute top-1 bottom-1 rounded-xl pointer-events-none z-10"
                    style={{
                      width: "calc(50% - 4px)",
                      left: "4px",
                      transform: authMode === "login" ? "translateX(0%)" : "translateX(100%)",
                      transition: "transform 0.35s cubic-bezier(0.25, 1.3, 0.45, 1), background 0.8s ease, box-shadow 0.8s ease",
                      background: "var(--theme-btn-gradient)",
                      boxShadow: "0 0 16px var(--theme-panel-border), 0 4px 18px rgba(0,0,0,0.4)",
                    }}
                  />

                  {/* Tab 1: Đăng nhập */}
                  <button
                    type="button"
                    onClick={() => {
                      setAuthMode("login");
                      setLoginError(null);
                    }}
                    className={`relative z-20 flex-1 py-2 rounded-xl text-xs font-bold transition-all duration-200 cursor-pointer text-center ${
                      authMode === "login" ? "text-white scale-[1.02]" : "text-white/60 hover:text-white"
                    }`}
                  >
                    Đăng nhập
                  </button>

                  {/* Tab 2: Đăng ký */}
                  <button
                    type="button"
                    onClick={() => {
                      setAuthMode("register");
                      setLoginError(null);
                    }}
                    className={`relative z-20 flex-1 py-2 rounded-xl text-xs font-bold transition-all duration-200 cursor-pointer text-center ${
                      authMode === "register" ? "text-white scale-[1.02]" : "text-white/60 hover:text-white"
                    }`}
                  >
                    Đăng ký
                  </button>
                </div>

                <p className="text-xs text-white/70 transition-all">
                  {authMode === "login"
                    ? "Đăng nhập tài khoản Kahoii Music để đồng bộ danh sách phát và bài hát yêu thích."
                    : "Tạo tài khoản Kahoii Music mới để lưu trữ và đồng bộ kho nhạc của bạn lên Cloud."}
                </p>

                {loginError && (
                  <div className="p-2.5 rounded-xl bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 text-red-400 flex-shrink-0" />
                    <span>{loginError}</span>
                  </div>
                )}

                <form onSubmit={authMode === "login" ? handleLogin : handleRegister} className="flex flex-col gap-3">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[11px] font-semibold text-white/80">Email</label>
                    <input
                      type="email"
                      required
                      placeholder="nhap-email@gmail.com"
                      value={loginEmail}
                      onChange={(e) => setLoginEmail(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-xs text-white placeholder-white/30 focus:outline-none"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-[11px] font-semibold text-white/80">Mật khẩu</label>
                    <input
                      type="password"
                      required
                      placeholder="••••••••"
                      value={loginPassword}
                      onChange={(e) => setLoginPassword(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-xs text-white placeholder-white/30 focus:outline-none"
                    />
                  </div>

                  {/* Confirm Password Field with Smooth Jelly Accordion Animation */}
                  <div
                    className={`transition-all duration-300 overflow-hidden ${
                      authMode === "register"
                        ? "max-h-24 opacity-100 translate-y-0 pointer-events-auto"
                        : "max-h-0 opacity-0 -translate-y-2 pointer-events-none"
                    }`}
                  >
                    <div className="flex flex-col gap-1.5 pt-1">
                      <label className="text-[11px] font-semibold text-white/80">Nhập lại mật khẩu</label>
                      <input
                        type="password"
                        placeholder="••••••••"
                        value={registerConfirmPassword}
                        onChange={(e) => setRegisterConfirmPassword(e.target.value)}
                        className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-xs text-white placeholder-white/30 focus:outline-none"
                      />
                    </div>
                  </div>

                  <button
                    type="submit"
                    disabled={isLoggingIn || isRegistering}
                    className="w-full mt-2 py-2.5 rounded-xl theme-btn-primary hover:opacity-95 text-white font-bold text-xs shadow-lg hover:scale-[1.02] active:scale-[0.98] transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                  >
                    {isLoggingIn || isRegistering ? (
                      <>
                        <Loader2 className="w-4 h-4 animate-spin text-white" />
                        <span>{authMode === "login" ? "Đang xác thực..." : "Đang tạo tài khoản..."}</span>
                      </>
                    ) : authMode === "login" ? (
                      <>
                        <LogIn className="w-4 h-4" />
                        <span>Đăng nhập</span>
                      </>
                    ) : (
                      <>
                        <User className="w-4 h-4" />
                        <span>Đăng ký tài khoản</span>
                      </>
                    )}
                  </button>

                  <div className="text-center mt-1">
                    {authMode === "login" ? (
                      <p className="text-[11px] text-white/60">
                        Chưa có tài khoản?{" "}
                        <button
                          type="button"
                          onClick={() => {
                            setAuthMode("register");
                            setLoginError(null);
                          }}
                          className="theme-accent-text hover:underline font-semibold cursor-pointer"
                        >
                          Đăng ký ngay
                        </button>
                      </p>
                    ) : (
                      <p className="text-[11px] text-white/60">
                        Đã có tài khoản?{" "}
                        <button
                          type="button"
                          onClick={() => {
                            setAuthMode("login");
                            setLoginError(null);
                          }}
                          className="theme-accent-text hover:underline font-semibold cursor-pointer"
                        >
                          Đăng nhập ngay
                        </button>
                      </p>
                    )}
                  </div>
                </form>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Custom Sleek Logout Confirmation Dialog */}
      {showLogoutConfirm && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 z-[60] animate-fade-in">
          <div className="figma-panel w-full max-w-[360px] flex flex-col p-6 relative shadow-2xl border theme-accent-border text-center gap-4">
            <div className="w-12 h-12 rounded-full theme-badge border theme-accent-border flex items-center justify-center mx-auto text-red-400 shadow-lg">
              <LogOut className="w-6 h-6" />
            </div>
            <div>
              <h3 className="text-base font-bold text-white">Xác nhận đăng xuất</h3>
              <p className="text-xs text-white/70 mt-1.5 leading-relaxed">
                Bạn có chắc chắn muốn đăng xuất khỏi tài khoản Kahoii Music?
              </p>
            </div>
            <div className="flex items-center gap-3 mt-1">
              <button
                type="button"
                onClick={() => setShowLogoutConfirm(false)}
                className="flex-1 py-2 rounded-xl bg-white/10 hover:bg-white/20 text-white font-semibold text-xs transition-all cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={confirmLogout}
                className="flex-1 py-2 rounded-xl bg-red-600 hover:bg-red-500 text-white font-bold text-xs shadow-lg transition-all cursor-pointer"
              >
                Đăng xuất
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
