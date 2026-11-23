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
  FileText,
  Maximize2,
  ChevronRight,
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
import { TrackCard } from "./components/TrackCard";
import { ArtistCard } from "./components/ArtistCard";
import { PlaylistCard } from "./components/PlaylistCard";
import { analyzeTasteProfile, type TasteProfile } from "./utils/tasteAnalyzer";

// Official App Logo & Favicon placeholder
const DEFAULT_TRACK_ART = "./logo_icon.png";

// Reliable, zero-black-bar thumbnail resolver matching PC App
const getTrackThumbnail = (track: Track | null, quality: "hd" | "sd" = "hd"): string => {
  if (!track) return DEFAULT_TRACK_ART;
  if (track.thumbnail && !track.thumbnail.includes("figma") && !track.thumbnail.includes("i.ytimg.com")) {
    return track.thumbnail;
  }
  const id = track.id;
  if (!id) return DEFAULT_TRACK_ART;
  // Prioritize hq720.jpg (pure 16:9, zero black bars) or mqdefault.jpg fallback
  // NEVER default to hqdefault.jpg because YouTube bakes 4:3 letterbox black bars into it!
  return quality === "hd"
    ? `https://i.ytimg.com/vi/${id}/hq720.jpg`
    : `https://i.ytimg.com/vi/${id}/mqdefault.jpg`;
};

// Base API URL for flexible deployment (GitHub Pages + Remote Backend or Monolithic Express)
const API_BASE = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/$/, "");
const STORAGE_USER_SESSION_KEY = "kahoii_user_session";
const STORAGE_LIBRARY_KEY = "kahoii_cached_library";

const FIREBASE_API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8";

const parseJwtExp = (token: string): number | null => {
  try {
    const parts = token.split(".");
    if (parts.length < 2) return null;
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
    const jsonStr = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join("")
    );
    const parsed = JSON.parse(jsonStr);
    return typeof parsed.exp === "number" ? parsed.exp : null;
  } catch {
    return null;
  }
};

const getAuthSession = (): { idToken: string | null; refreshToken: string | null; expiryTimestamp: number | null } => {
  try {
    const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
    if (!raw) return { idToken: null, refreshToken: null, expiryTimestamp: null };
    const parsed = JSON.parse(raw);
    const idToken = parsed?.user?.idToken || parsed?.idToken || null;
    const refreshToken = parsed?.user?.refreshToken || parsed?.refreshToken || null;
    let expiryTimestamp = parsed?.user?.expiryTimestamp || parsed?.expiryTimestamp || null;
    if (!expiryTimestamp && idToken) {
      expiryTimestamp = parseJwtExp(idToken);
    }
    return { idToken, refreshToken, expiryTimestamp };
  } catch {
    return { idToken: null, refreshToken: null, expiryTimestamp: null };
  }
};

const getAuthToken = (): string | null => getAuthSession().idToken;
const getRefreshToken = (): string | null => getAuthSession().refreshToken;

// Helper to save session updates safely across both formats and notify React components
const saveAuthSession = (newTokens: { idToken?: string; refreshToken?: string; expiryTimestamp?: number }) => {
  try {
    const raw = localStorage.getItem(STORAGE_USER_SESSION_KEY);
    let session = raw ? JSON.parse(raw) : { isLoggedIn: true, user: {} };
    if (!session || typeof session !== "object") session = { isLoggedIn: true, user: {} };
    if (!session.user) session.user = {};
    if (newTokens.idToken) {
      session.user.idToken = newTokens.idToken;
      session.idToken = newTokens.idToken;
    }
    if (newTokens.refreshToken) {
      session.user.refreshToken = newTokens.refreshToken;
      session.refreshToken = newTokens.refreshToken;
    }
    if (newTokens.expiryTimestamp) {
      session.user.expiryTimestamp = newTokens.expiryTimestamp;
      session.expiryTimestamp = newTokens.expiryTimestamp;
    }
    session.isLoggedIn = true;
    localStorage.setItem(STORAGE_USER_SESSION_KEY, JSON.stringify(session));
    if (typeof window !== "undefined") {
      window.dispatchEvent(new CustomEvent("kahoii_auth_updated", { detail: session }));
    }
  } catch { }
};

// Singleton promise to prevent multiple concurrent token refresh calls
let tokenRefreshPromise: Promise<string | null> | null = null;

const doRefreshToken = async (refreshToken: string): Promise<string | null> => {
  const nowSec = Math.floor(Date.now() / 1000);
  // Try 1: Call WebApp backend /api/user/refresh
  try {
    const res = await fetch(`${API_BASE}/api/user/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Refresh-Token": refreshToken },
      body: JSON.stringify({ refreshToken }),
    });
    if (res.ok) {
      const data = await res.json();
      const freshId = data.idToken || data.token;
      const freshRef = data.refreshToken || refreshToken;
      const freshExp = data.expiryTimestamp || (data.expiresIn ? nowSec + Number(data.expiresIn) : (parseJwtExp(freshId) || nowSec + 3600));
      if (freshId) {
        saveAuthSession({ idToken: freshId, refreshToken: freshRef, expiryTimestamp: freshExp });
        return freshId;
      }
    }
  } catch (backendErr) {
    console.warn("[doRefreshToken] Backend refresh error, trying direct Google SecureToken fallback:", backendErr);
  }

  // Try 2: Direct Google SecureToken API fallback (CORS-enabled from all origins)
  try {
    const googleUrl = `https://securetoken.googleapis.com/v1/token?key=${FIREBASE_API_KEY}`;
    const params = new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: refreshToken.trim(),
    });
    const gRes = await fetch(googleUrl, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: params.toString(),
    });
    if (gRes.ok) {
      const gData = await gRes.json();
      const freshId = gData.id_token;
      const freshRef = gData.refresh_token || refreshToken;
      const expIn = Number(gData.expires_in) || 3600;
      const freshExp = nowSec + expIn;
      if (freshId) {
        saveAuthSession({ idToken: freshId, refreshToken: freshRef, expiryTimestamp: freshExp });
        return freshId;
      }
    }
  } catch (gErr) {
    console.error("[doRefreshToken] Direct Google SecureToken failed:", gErr);
  }

  return null;
};

// Proactively returns a valid idToken, refreshing it if expired or expiring within 120s
const getValidAuthToken = async (): Promise<string | null> => {
  const { idToken, refreshToken, expiryTimestamp } = getAuthSession();
  if (!idToken && !refreshToken) return null;

  const nowSec = Math.floor(Date.now() / 1000);
  const exp = expiryTimestamp || (idToken ? parseJwtExp(idToken) : null);

  // If idToken exists and has more than 120 seconds left before expiration, use it directly!
  if (idToken && exp && nowSec < exp - 120) {
    return idToken;
  }

  // If token is expired or expiring soon, and we have a refreshToken, proactively refresh!
  if (refreshToken) {
    if (!tokenRefreshPromise) {
      tokenRefreshPromise = doRefreshToken(refreshToken).finally(() => {
        tokenRefreshPromise = null;
      });
    }
    const refreshedToken = await tokenRefreshPromise;
    if (refreshedToken) return refreshedToken;
  }

  return idToken;
};

// Helper to make authenticated requests with transparent proactive refresh & 401 retry
const authenticatedFetch = async (url: string, options: RequestInit = {}): Promise<Response> => {
  let idToken = await getValidAuthToken();
  let refreshToken = getRefreshToken();
  const headers = new Headers(options.headers || {});
  if (idToken) {
    headers.set("Authorization", `Bearer ${idToken}`);
  }
  if (refreshToken) {
    headers.set("X-Refresh-Token", refreshToken);
  }

  let res = await fetch(url, { ...options, headers });

  // If 401 Unauthorized and we have a refreshToken, refresh once and retry!
  if (res.status === 401 && refreshToken) {
    try {
      const freshToken = await doRefreshToken(refreshToken);
      if (freshToken) {
        idToken = freshToken;
        refreshToken = getRefreshToken() || refreshToken;
        headers.set("Authorization", `Bearer ${idToken}`);
        if (refreshToken) headers.set("X-Refresh-Token", refreshToken);
        res = await fetch(url, { ...options, headers });
      }
    } catch (err) {
      console.warn("[authenticatedFetch] Auto-refresh retry error:", err);
    }
  }

  // If server auto-refreshed the token, capture it and update localStorage
  const refreshedIdToken = res.headers.get("x-refreshed-id-token");
  const refreshedRefreshToken = res.headers.get("x-refreshed-refresh-token");
  if (refreshedIdToken) {
    saveAuthSession({
      idToken: refreshedIdToken,
      refreshToken: refreshedRefreshToken || undefined,
      expiryTimestamp: parseJwtExp(refreshedIdToken) || undefined,
    });
  }

  return res;
};

// Initial Curated Tracks (Empty by default, skeleton loader displays until live data arrives)
const INITIAL_TRENDING_TRACKS: Track[] = [];
const INITIAL_NEW_RELEASES: Track[] = [];

const BOLERO_UNWANTED_REGEX =
  /bolero|nhạc vàng|trữ tình|nhạc lính|nhạc sống|quê hương|đám cưới|hát xẩm|xẩm đời|chachacha|nhạc chế|sáo trúc|tây bắc|thôn quê|dân ca|tuyệt phẩm|tâm trạng ai nghe cũng khóc|đò bỏ bến|kiếp người|bể dâu|nghèo|bạc bẽo|rumba|phố nhạc việt|lk nhạc|liên khúc nhạc|liên khúc ca nhạc|liên khúc bolero|tone nam|tone nữ|karaoke|nhạc thiền|nhạc phật/i;

function isCleanModernTrack(t: Track): boolean {
  if (!t) return false;
  const str = `${t.title || ""} ${t.author || ""}`.toLowerCase();
  return !BOLERO_UNWANTED_REGEX.test(str);
}

// Nhạc Sĩ Nổi Bật (Featured Artists - High-res local avatars & rotating pool)
const CURATED_FEATURED_ARTISTS = [
  { name: "Sơn Tùng M-TP", genre: "Pop / R&B", avatar: "./artists/son_tung.jpg", query: "Sơn Tùng M-TP" },
  { name: "Vũ.", genre: "Indie / Acoustic", avatar: "./artists/vu.jpg", query: "Vũ" },
  { name: "Đen", genre: "Rap / Hip-Hop", avatar: "./artists/den.jpg", query: "Đen Vâu" },
  { name: "Soobin Hoàng Sơn", genre: "R&B / Ballad", avatar: "./artists/soobin.jpg", query: "Soobin Hoàng Sơn" },
  { name: "Taylor Swift", genre: "Pop Superstar", avatar: "./artists/taylor_swift.jpg", query: "Taylor Swift" },
  { name: "The Weeknd", genre: "R&B / Pop", avatar: "./artists/the_weeknd.jpg", query: "The Weeknd" },
  { name: "HIEUTHUHAI", genre: "Rap / Hip-Hop", avatar: "https://i.ytimg.com/vi/bY8o44A_aI4/hqdefault.jpg", query: "HIEUTHUHAI" },
  { name: "Wren Evans", genre: "Pop / R&B", avatar: "https://i.ytimg.com/vi/571_T9gKrqU/hqdefault.jpg", query: "Wren Evans" },
  { name: "tuki.", genre: "J-Pop / Acoustic", avatar: "https://i.ytimg.com/vi/kJQP7kiw5Fk/hqdefault.jpg", query: "tuki." },
  { name: "MCK", genre: "Hip-Hop / Trap", avatar: "https://i.ytimg.com/vi/Wk9Uj4Qk6q4/hqdefault.jpg", query: "MCK" },
  { name: "tlinh", genre: "R&B / Hip-Hop", avatar: "https://i.ytimg.com/vi/lJgK34lZ7Zk/hqdefault.jpg", query: "tlinh" },
  { name: "Charlie Puth", genre: "Pop / Funk", avatar: "https://i.ytimg.com/vi/3AtDnEC4zak/hqdefault.jpg", query: "Charlie Puth" },
  { name: "MONO", genre: "Dance-Pop / R&B", avatar: "https://i.ytimg.com/vi/lX_q2rZ5Yh4/hqdefault.jpg", query: "MONO" },
  { name: "GREY D", genre: "Pop / Acoustic", avatar: "https://i.ytimg.com/vi/E5t0k7d09m0/hqdefault.jpg", query: "GREY D" },
  { name: "Phương Ly", genre: "Pop / R&B", avatar: "https://i.ytimg.com/vi/L4a024Wk8c4/hqdefault.jpg", query: "Phương Ly" },
  { name: "Hoàng Dũng", genre: "Indie / Ballad", avatar: "https://i.ytimg.com/vi/2v5r8e4k_08/hqdefault.jpg", query: "Hoàng Dũng" },
  { name: "Amee", genre: "Teen-Pop / R&B", avatar: "https://i.ytimg.com/vi/K0t2Z4o2r9s/hqdefault.jpg", query: "Amee" },
  { name: "Bruno Mars", genre: "Funk / Pop / R&B", avatar: "https://i.ytimg.com/vi/PMivT7MJ41M/hqdefault.jpg", query: "Bruno Mars" },
  { name: "Billie Eilish", genre: "Alt-Pop / Indie", avatar: "https://i.ytimg.com/vi/4i_T4vPzN5E/hqdefault.jpg", query: "Billie Eilish" },
  { name: "NewJeans", genre: "K-Pop / R&B", avatar: "https://i.ytimg.com/vi/ArmDp-zijuc/hqdefault.jpg", query: "NewJeans" },
  { name: "Rosé", genre: "K-Pop / Pop", avatar: "https://i.ytimg.com/vi/ekr2nIex040/hqdefault.jpg", query: "Rosé" },
  { name: "Vũ Cát Tường", genre: "Pop / Ballad", avatar: "https://i.ytimg.com/vi/v5k-8s2Q7fI/hqdefault.jpg", query: "Vũ Cát Tường" },
  { name: "B Ray", genre: "Rap / Hip-Hop", avatar: "https://i.ytimg.com/vi/N7vX8Zq9j5I/hqdefault.jpg", query: "B Ray" },
  { name: "JustaTee", genre: "R&B / Hip-Hop", avatar: "https://i.ytimg.com/vi/6jK39tZk8c8/hqdefault.jpg", query: "JustaTee" },
  { name: "Quốc Thiên", genre: "Vocal / Ballad", avatar: "https://i.ytimg.com/vi/7j5h2Y6h8c8/hqdefault.jpg", query: "Quốc Thiên" },
  { name: "Rhymastic", genre: "Rap / Producer", avatar: "https://i.ytimg.com/vi/8j5h2Y6h8c8/hqdefault.jpg", query: "Rhymastic" },
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
  const cleanAuthor = (author || "").replace(/[\u00A0\u200B\u3000]/g, " ").trim();
  const rawTitle = (title || "").replace(/[\u00A0\u200B\u3000]/g, " ").trim();

  // Check if author is a generic placeholder or video metadata tag
  const strippedAuthor = cleanAuthor.replace(/[【】\[\]\(\)]/g, " ").trim();
  const isGeneric =
    !cleanAuthor ||
    !strippedAuthor ||
    cleanAuthor === "Kahoii Music" ||
    cleanAuthor === "YouTube Music" ||
    cleanAuthor === "Nghệ sĩ" ||
    cleanAuthor === "Artist" ||
    cleanAuthor === "Various Artists" ||
    /^(mv|official|video|audio|lyrics?|hd|4k|m\/v|teaser|visualizer|channel)[】\]\)]*$/i.test(strippedAuthor);

  // 1. Try extracting artist from title with smart patterns
  if (rawTitle) {
    // Strip leading and trailing video metadata like 【MV】, [Official Video], (4K) before parsing
    const titleCleaned = rawTitle
      .replace(/^([【\[\(](?:MV|Official|Video|Audio|HD|4K|Music\s+Video|Lyric\s+Video|Visualizer|Teaser)[】\]\)]\s*)+/gi, "")
      .replace(/(\s*[【\[\(](?:MV|Official|Video|Audio|HD|4K|Music\s+Video|Lyric\s+Video|Visualizer|Teaser)[】\]\)])+$/gi, "")
      .trim();

    // " / ": e.g. "『ラベンダー』 / MIMI feat. saewool (Music Video)" -> "MIMI feat. saewool"
    // "【MV】「言っといてよ」 / 9Lana" -> "9Lana"
    if (titleCleaned.includes(" / ") || /\s+\/\s+/.test(titleCleaned)) {
      const parts = titleCleaned.split(/\s+\/\s+/);
      if (parts.length >= 2) {
        let cand = parts[1].replace(/\(Music Video\)/gi, "").replace(/【.*?】/g, "").replace(/\(.*?\)/g, "").trim();
        cand = cand.replace(/^[【\[\(]+|[】\]\)]+$/g, "").trim();
        if (cand && !/^(mv|official|video|audio|lyrics?|hd|4k)$/i.test(cand)) return cand;
      }
    }

    // " - ": e.g. "Orangestar - 花筏 (feat. 夏背)" -> "Orangestar"
    if (titleCleaned.includes(" - ") || /\s+-\s+/.test(titleCleaned)) {
      const parts = titleCleaned.split(/\s+-\s+/);
      if (parts.length >= 2) {
        let cand = parts[0].trim();
        cand = cand.replace(/^\[.*?\]\s*/, "").replace(/^【.*?】\s*/, "").trim();
        cand = cand.replace(/^[【\[\(]+|[】\]\)]+$/g, "").trim();
        if (cand && !/^(mv|official|lyric|audio|video|teaser|hd|4k)$/i.test(cand)) {
          return cand;
        }
      }
    }

    // " | ": e.g. "Title | Artist"
    if (titleCleaned.includes(" | ") || /\s+\|\s+/.test(titleCleaned)) {
      const parts = titleCleaned.split(/\s+\|\s+/);
      if (parts.length >= 2) {
        let cand = parts[1].trim();
        cand = cand.replace(/^[【\[\(]+|[】\]\)]+$/g, "").trim();
        if (cand && !/^(mv|official|lyric|audio|video)$/i.test(cand)) return cand;
      }
    }

    // "Covered by X" / "Cover by X"
    const mCover = titleCleaned.match(/(?:covered\s+by|cover\s+by|vocal\s*:?)\s*([^\s\(\)\[\]【】\/|]+)/i);
    if (mCover && mCover[1]) {
      const cand = mCover[1].replace(/^[【\[\(]+|[】\]\)]+$/g, "").trim();
      if (cand && !/^(mv|official|video|audio|lyrics?)$/i.test(cand)) return cand;
    }

    // Japanese quotes: 「Track」Artist or Artist「Track」
    const mQuotes = titleCleaned.match(/([^\s\u300C\u300E\u3010\u300D\u300F\u3011\[\]\(\)]+)\s*[\u300C\u300E\u3010][^\u300D\u300F\u3011]+[\u300D\u300F\u3011]/);
    if (mQuotes && mQuotes[1]) {
      const cleanCand = mQuotes[1].replace(/[【】\[\]\(\)]/g, "").trim();
      if (cleanCand && !/^(mv|official|video|audio|lyrics?|弾き語り|歌ってみた)$/i.test(cleanCand)) {
        return cleanCand;
      }
    }

    // Check brackets like 【Artist】 if not standard tags
    const bracketMatch = titleCleaned.match(/【([^】]+)】/);
    if (bracketMatch && bracketMatch[1] && !/歌ってみた|弾き語り|MV|Official|Lyric|Audio|HD|4K|Visualizer|Teaser/i.test(bracketMatch[1])) {
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
    const cleanCand = cleanAuthor.replace(/^[【\[\(]+|[】\]\)]+$/g, "").trim();
    if (cleanCand && !/^(mv|official|video|audio|lyrics?)$/i.test(cleanCand)) {
      return cleanCand;
    }
  }

  // 3. Fallback: Never return generic "Nghệ sĩ" or placeholder
  return "";
}

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
  const lastEndedTrackIdRef = useRef<string | null>(null);
  const trackPlayStartTimeRef = useRef<number>(0);
  const handleTrackEndedRef = useRef<() => void>(() => { });
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
  const [homeArtists, setHomeArtists] = useState<any[]>(() =>
    [...CURATED_FEATURED_ARTISTS].sort(() => Math.random() - 0.5).slice(0, 12)
  );

  // Dual-Engine Playback (Audio Element + Zero-Fail YouTube Iframe Player Fallback)
  const ytPlayerRef = useRef<any>(null);
  const activePlayerType = useRef<"audio" | "youtube">("youtube");
  const lastPlayPositionRef = useRef<number>(0);
  const lastLocalPersistRef = useRef<number>(0);
  const isUserPausedRef = useRef<boolean>(false);
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
    } catch { }
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
    } catch { }
    return {
      favorites: [],
      savedTracks: [],
      history: [],
      playlists: [],
    };
  });

  // Personalized Taste-Based Recommendations
  const [selectedTasteTagKey, setSelectedTasteTagKey] = useState<string>("all");
  const [recommendedTracks, setRecommendedTracks] = useState<Track[]>([]);
  const [isRecommendationsLoading, setIsRecommendationsLoading] = useState<boolean>(false);

  // Computed Taste Profile from user's Favorites
  const tasteProfile = useMemo<TasteProfile>(() => {
    return analyzeTasteProfile(libraryData.favorites);
  }, [libraryData.favorites]);

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
      } catch { }
    }
    if (ytPlayerRef.current && typeof ytPlayerRef.current.pauseVideo === "function") {
      try {
        ytPlayerRef.current.pauseVideo();
      } catch { }
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

  // Center Now Playing Player View (Expanded view triggered by clicking Thumbnail)
  const [isNowPlayingViewOpen, setIsNowPlayingViewOpen] = useState<boolean>(false);
  const [showCenterLyrics, setShowCenterLyrics] = useState<boolean>(true);
  const [centerLyricFontSize, setCenterLyricFontSize] = useState<number>(1);
  const [isLoadingLyrics, setIsLoadingLyrics] = useState<boolean>(false);
  const centerActiveLyricRef = useRef<HTMLDivElement | null>(null);
  const centerLyricsContainerRef = useRef<HTMLDivElement | null>(null);
  const centerPlayerMenuRef = useRef<HTMLDivElement | null>(null);

  const activeLyricRef = useRef<HTMLDivElement | null>(null);
  const lyricsContainerRef = useRef<HTMLDivElement | null>(null);
  const miniActiveLyricRef = useRef<HTMLDivElement | null>(null);
  const miniLyricsContainerRef = useRef<HTMLDivElement | null>(null);
  const mobileActiveQueueRef = useRef<HTMLDivElement | null>(null);
  const [showFullQueueMobile, setShowFullQueueMobile] = useState<boolean>(false);
  const [miniLyricFontSize, setMiniLyricFontSize] = useState<number>(1);

  // Romanization display mode matching PC App (LyricsPanel.cs:11-16):
  // "dual" = Dual (1, Romaji 2 dòng) | "romanized" = RomanizedOnly (2, Romaji thuần) | "original" = Original (0, Chữ gốc)
  const [lyricDisplayMode, setLyricDisplayMode] = useState<"dual" | "original" | "romanized">(() => {
    try {
      return (localStorage.getItem("kahoii_lyric_display_mode") as any) || "dual";
    } catch {
      return "dual";
    }
  });

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

  // Detected language for Romanization support matching PC App (LyricsService.cs:1394-1407)
  const detectedLang = useMemo(() => {
    if (_lyrics?.detectedLang === "ja" || _lyrics?.detectedLang === "zh") {
      return _lyrics.detectedLang;
    }
    const fullText = (parsedLyrics || []).map((l) => l.text || "").join(" ");
    const kanaCount = (fullText.match(/[\u3040-\u309F\u30A0-\u30FF]/g) || []).length;
    const hanziCount = (fullText.match(/[\u4E00-\u9FFF]/g) || []).length;
    if (kanaCount >= 2) return "ja";
    if (hanziCount >= 4) return "zh";
    return null;
  }, [_lyrics, parsedLyrics]);

  const hasRomanizationSupport = Boolean(
    (detectedLang === "ja" || detectedLang === "zh") && parsedLyrics.length > 0
  );

  // Exact button labels matching PC App (LyricsPanel.cs:945-967)
  const romaziPillLabel = useMemo(() => {
    const isJa = detectedLang === "ja";
    if (lyricDisplayMode === "original") {
      return isJa ? "Aa Romaji" : "Aa Pinyin";
    }
    if (lyricDisplayMode === "dual") {
      return isJa ? "Romaji • 2 dòng" : "Pinyin • 2 dòng";
    }
    return isJa ? "Romaji thuần" : "Pinyin thuần";
  }, [detectedLang, lyricDisplayMode]);

  // Cycle matching PC App: Original (0) -> Dual (1) -> RomanizedOnly (2) -> Original (0)
  const toggleLyricDisplayMode = () => {
    setLyricDisplayMode((prev) => {
      const next = prev === "dual" ? "romanized" : prev === "romanized" ? "original" : "dual";
      try {
        localStorage.setItem("kahoii_lyric_display_mode", next);
      } catch { }
      return next;
    });
  };

  // Next track in queue for Mini Player ("Bài tiếp theo")
  const nextTrackIndex = useMemo(() => {
    if (!queueTracks || queueTracks.length === 0) return -1;
    if (currentIndex + 1 < queueTracks.length) return currentIndex + 1;
    if (repeatMode === "all") return 0;
    return -1;
  }, [queueTracks, currentIndex, repeatMode]);

  const nextTrack = useMemo(() => {
    if (nextTrackIndex >= 0 && nextTrackIndex < queueTracks.length) {
      return queueTracks[nextTrackIndex];
    }
    return null;
  }, [queueTracks, nextTrackIndex]);

  const handleReloadLyrics = () => {
    if (!currentTrack) return;
    showToast("Đang đồng bộ lại lời bài hát...", "info");
    setIsLoadingLyrics(true);
    fetch(
      `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(currentTrack.id)}&title=${encodeURIComponent(currentTrack.title)}&artist=${encodeURIComponent(currentTrack.author || "")}&duration=${currentTrack.duration || ""}&refresh=true`
    )
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data?.lines && data.lines.length > 0) {
          setLyrics(data);
          showToast("Đã cập nhật lời bài hát mới!", "success");
        } else {
          showToast("Không tìm thấy lời bài hát mới", "warning");
        }
      })
      .catch(() => {
        showToast("Lỗi khi tải lại lời bài hát", "warning");
      })
      .finally(() => {
        setIsLoadingLyrics(false);
      });
  };

  const scrollToActiveLyric = useCallback((smooth = true) => {
    // 1. Center Now Playing View lyrics container
    if (centerLyricsContainerRef.current && centerActiveLyricRef.current) {
      const container = centerLyricsContainerRef.current;
      const activeEl = centerActiveLyricRef.current;
      const containerRect = container.getBoundingClientRect();
      const activeRect = activeEl.getBoundingClientRect();
      const relativeTop = activeRect.top - containerRect.top;
      const targetScroll = container.scrollTop + relativeTop - (container.clientHeight / 2) + (activeRect.height / 2);
      container.scrollTo({
        top: Math.max(0, targetScroll),
        behavior: smooth ? "smooth" : "auto",
      });
    }

    // 2. Desktop full lyrics container
    if (lyricsContainerRef.current && activeLyricRef.current) {
      const container = lyricsContainerRef.current;
      const activeEl = activeLyricRef.current;
      const containerRect = container.getBoundingClientRect();
      const activeRect = activeEl.getBoundingClientRect();
      const relativeTop = activeRect.top - containerRect.top;
      const targetScroll = container.scrollTop + relativeTop - (container.clientHeight / 2) + (activeRect.height / 2);
      container.scrollTo({
        top: Math.max(0, targetScroll),
        behavior: smooth ? "smooth" : "auto",
      });
    }

    // 3. Mini lyrics container (PC Mini App mode)
    if (miniLyricsContainerRef.current && miniActiveLyricRef.current) {
      const container = miniLyricsContainerRef.current;
      const activeEl = miniActiveLyricRef.current;
      const containerRect = container.getBoundingClientRect();
      const activeRect = activeEl.getBoundingClientRect();
      const relativeTop = activeRect.top - containerRect.top;
      const targetScroll = container.scrollTop + relativeTop - (container.clientHeight / 2) + (activeRect.height / 2);
      container.scrollTo({
        top: Math.max(0, targetScroll),
        behavior: smooth ? "smooth" : "auto",
      });
    }
  }, []);

  useEffect(() => {
    if (activeLyricIndex < 0) return;
    const animId = requestAnimationFrame(() => {
      scrollToActiveLyric(true);
    });
    return () => cancelAnimationFrame(animId);
  }, [activeLyricIndex, isNowPlayingViewOpen, showCenterLyrics, centerLyricFontSize, mobileTab, lyricDisplayMode, _lyrics, scrollToActiveLyric]);

  // Reset lyrics scroll to 0 whenever lyrics are cleared or empty
  useEffect(() => {
    if (centerLyricsContainerRef.current && (parsedLyrics.length === 0 || !_lyrics)) {
      centerLyricsContainerRef.current.scrollTop = 0;
    }
    if (lyricsContainerRef.current && (parsedLyrics.length === 0 || !_lyrics)) {
      lyricsContainerRef.current.scrollTop = 0;
    }
    if (miniLyricsContainerRef.current && (parsedLyrics.length === 0 || !_lyrics)) {
      miniLyricsContainerRef.current.scrollTop = 0;
    }
  }, [parsedLyrics.length, _lyrics, currentTrack?.id]);

  // Auto-scroll mobile queue to current playing track when expanded
  useEffect(() => {
    if (showFullQueueMobile && mobileActiveQueueRef.current) {
      mobileActiveQueueRef.current.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }
  }, [showFullQueueMobile, currentIndex]);

  // Automatically fetch full enriched lyrics whenever currentTrack changes
  useEffect(() => {
    const trackId = currentTrack?.id;
    if (!trackId) {
      setIsLoadingLyrics(false);
      return;
    }

    let isCancelled = false;
    setIsLoadingLyrics(true);

    fetch(
      `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(trackId)}&title=${encodeURIComponent(currentTrack.title)}&artist=${encodeURIComponent(currentTrack.author || "")}&duration=${currentTrack.duration || ""}`
    )
      .then((res) => (res.ok ? res.json() : null))
      .then((lyricsData) => {
        if (!isCancelled && lyricsData?.lines && lyricsData.lines.length > 0) {
          setLyrics(lyricsData);
          if (lyricsData.artist && (!currentTrack.author || currentTrack.author === "Nghệ sĩ" || currentTrack.author === "Kahoii Music")) {
            setCurrentTrack((prev) => (prev && prev.id === trackId ? { ...prev, author: lyricsData.artist } : prev));
          }
        }
      })
      .catch(() => { })
      .finally(() => {
        if (!isCancelled) {
          setIsLoadingLyrics(false);
        }
      });

    return () => {
      isCancelled = true;
    };
  }, [currentTrack?.id]);

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
      const target = e.target as Node;
      const insidePlayerMenu = playerMenuRef.current?.contains(target);
      const insideCenterMenu = centerPlayerMenuRef.current?.contains(target);
      if (!insidePlayerMenu && !insideCenterMenu) {
        setShowPlayerMenu(false);
        setShowLibrarySubmenu(false);
        setIsCreatingPlaylist(false);
        setNewPlaylistName("");
      }
      if (downloadMenuRef.current && !downloadMenuRef.current.contains(target)) {
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
      } catch { }
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
      } catch { }
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
      } catch { }

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
    lastLocalPersistRef.current = Date.now();
    const payload = {
      favorites: updated.favorites !== undefined ? updated.favorites : libraryData.favorites,
      savedTracks: updated.savedTracks !== undefined ? updated.savedTracks : libraryData.savedTracks,
      history: updated.history !== undefined ? updated.history : libraryData.history,
      playlists: updated.playlists !== undefined ? updated.playlists : libraryData.playlists,
    };

    console.log(`[Library Persist] 💾 Saving library to local cache & Firestore... (favorites: ${payload.favorites?.length || 0}, saved: ${payload.savedTracks?.length || 0}, history: ${payload.history?.length || 0}, playlists: ${payload.playlists?.length || 0})`);

    try {
      localStorage.setItem(STORAGE_LIBRARY_KEY, JSON.stringify(payload));
    } catch { }

    try {
      const res = await authenticatedFetch(`${API_BASE}/api/user/library`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (res.ok) {
        console.log("[Library Persist] ✅ Successfully saved library to cloud Firestore!");
      } else {
        console.warn("[Library Persist] ⚠️ Server save returned status:", res.status);
      }
    } catch (err) {
      console.error("[Library Persist Error]:", err);
    }
  };

  // Favorite toggle for current track
  const toggleCurrentFavorite = async () => {
    if (!currentTrack) return;
    if (!userSession?.isLoggedIn) {
      console.warn("[Favorites] ⚠️ User not logged in, cannot toggle favorites.");
      showToast("Vui lòng đăng nhập để lưu bài hát vào danh sách yêu thích!", "warning");
      setIsAccountOpen(true);
      return;
    }

    const isFav = libraryData.favorites.some((t) => t.id === currentTrack.id);
    let updatedFavs: Track[];

    // Immediately lock silent sync to avoid race conditions with background polling
    lastLocalPersistRef.current = Date.now();

    if (isFav) {
      updatedFavs = libraryData.favorites.filter((t) => t.id !== currentTrack.id);
      console.log(`[Favorites] 💔 Removed "${currentTrack.title}" (${currentTrack.id}) from favorites. Remaining: ${updatedFavs.length}`);
      showToast(`Đã xóa "${currentTrack.title}" khỏi bài hát yêu thích`, "info");
    } else {
      updatedFavs = [currentTrack, ...libraryData.favorites.filter((t) => t.id !== currentTrack.id)];
      console.log(`[Favorites] ❤️ Added "${currentTrack.title}" (${currentTrack.id}) to favorites. Total: ${updatedFavs.length}`);
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
        } catch { }
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
      } catch { }
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
  const wakeLockRef = useRef<any>(null);

  const requestWakeLock = useCallback(async () => {
    if (typeof navigator !== "undefined" && "wakeLock" in navigator && !wakeLockRef.current) {
      try {
        wakeLockRef.current = await (navigator as any).wakeLock.request("screen");
        wakeLockRef.current.addEventListener("release", () => {
          wakeLockRef.current = null;
        });
      } catch { }
    }
  }, []);

  const releaseWakeLock = useCallback(() => {
    if (wakeLockRef.current) {
      try {
        wakeLockRef.current.release();
      } catch { }
      wakeLockRef.current = null;
    }
  }, []);

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
    try {
      console.log("[Auth Session] 👤 Checking user authentication session...");
      const res = await authenticatedFetch(`${API_BASE}/api/user/session`);
      if (res.ok) {
        const data = await res.json();
        if (data.isLoggedIn && data.user) {
          const freshIdToken = res.headers.get("x-refreshed-id-token") || data.user.idToken || getAuthToken();
          const freshRefreshToken = res.headers.get("x-refreshed-refresh-token") || userSession?.user?.refreshToken || data.user.refreshToken || getRefreshToken();
          const freshExpiry = data.user.expiryTimestamp || (freshIdToken ? parseJwtExp(freshIdToken) : null);
          const updatedSession = {
            ...data,
            user: {
              ...data.user,
              idToken: freshIdToken,
              refreshToken: freshRefreshToken,
              expiryTimestamp: freshExpiry,
            },
          };
          console.log(`[Auth Session] ✅ Logged in as: ${data.user.email || data.user.username || data.user.id} (${data.user.displayName || "User"})`);
          setUserSession(updatedSession);
          saveAuthSession({
            idToken: freshIdToken,
            refreshToken: freshRefreshToken,
            expiryTimestamp: freshExpiry || undefined,
          });
        } else {
          // If response says not logged in, but we have a refreshToken, proactively refresh and retry!
          const rf = getRefreshToken();
          if (rf) {
            console.log("[Auth Session] Attempting token refresh via refreshToken...");
            const refreshedId = await doRefreshToken(rf);
            if (refreshedId) {
              const retryRes = await authenticatedFetch(`${API_BASE}/api/user/session`);
              if (retryRes.ok) {
                const retryData = await retryRes.json();
                if (retryData.isLoggedIn && retryData.user) {
                  console.log(`[Auth Session] ✅ Session recovered via token refresh: ${retryData.user.email}`);
                  setUserSession(retryData);
                  return;
                }
              }
            }
          }
          console.log("[Auth Session] ℹ️ Guest mode / Not logged in.");
          try {
            localStorage.removeItem(STORAGE_USER_SESSION_KEY);
          } catch { }
          setUserSession({ isLoggedIn: false, user: null });
        }
      } else if (res.status === 401) {
        const rf = getRefreshToken();
        if (rf) {
          console.log("[Auth Session] 401 received. Attempting token refresh...");
          const refreshedId = await doRefreshToken(rf);
          if (refreshedId) {
            const retryRes = await authenticatedFetch(`${API_BASE}/api/user/session`);
            if (retryRes.ok) {
              const retryData = await retryRes.json();
              if (retryData.isLoggedIn && retryData.user) {
                console.log(`[Auth Session] ✅ Session restored after 401: ${retryData.user.email}`);
                setUserSession(retryData);
                return;
              }
            }
          }
        }
        console.log("[Auth Session] Session expired or invalid.");
        try {
          localStorage.removeItem(STORAGE_USER_SESSION_KEY);
        } catch { }
        setUserSession({ isLoggedIn: false, user: null });
      }
    } catch (err) {
      console.warn("[Auth Session Error]:", err);
    }
  };

  const fetchLibraryData = async (silent = false) => {
    // Avoid overwriting freshly triggered local user actions before cloud write finishes
    const elapsedSinceLocal = Date.now() - lastLocalPersistRef.current;
    if (silent && elapsedSinceLocal < 15000) {
      console.log(`[Library Sync] ⏳ Skipped silent sync: local change was ${elapsedSinceLocal}ms ago (cooldown: 15s).`);
      return;
    }
    if (!silent) setIsLibraryLoading(true);
    console.log(`[Library Sync] 🔄 Fetching user library from server (silent: ${silent})...`);
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

        console.log(`[Library Sync] 📥 Server returned: ${mappedFavs.length} favorites, ${mappedSaved.length} saved, ${mappedHist.length} history, ${rawPlaylists.length} playlists.`);

        // Don't overwrite existing local cached library with empty data if unauthenticated response
        if (mappedFavs.length > 0 || mappedSaved.length > 0 || mappedHist.length > 0 || rawPlaylists.length > 0) {
          setLibraryData((prev) => {
            // If local changes were made within 30s, protect any locally favorited track that hasn't synced to cloud yet
            let finalFavs = nextLibrary.favorites;
            if (Date.now() - lastLocalPersistRef.current < 30000) {
              const cloudIds = new Set(nextLibrary.favorites.map((f: Track) => f.id));
              const pendingLocalFavs = prev.favorites.filter((f) => !cloudIds.has(f.id));
              if (pendingLocalFavs.length > 0) {
                console.log(`[Library Sync] 🛡️ Preserving ${pendingLocalFavs.length} recently added local favorites pending cloud commit:`, pendingLocalFavs.map(t => t.title));
                finalFavs = [...pendingLocalFavs, ...nextLibrary.favorites];
              }
            }

            // Accurate deep check to prevent unnecessary re-renders
            const isSameFavs = prev.favorites.length === finalFavs.length &&
              prev.favorites.every((t, i) => t.id === finalFavs[i].id);
            const isSameSaved = prev.savedTracks.length === nextLibrary.savedTracks.length &&
              prev.savedTracks.every((t, i) => t.id === nextLibrary.savedTracks[i].id);
            const isSameHist = prev.history.length === nextLibrary.history.length &&
              prev.history.every((t, i) => t.id === nextLibrary.history[i].id);
            const isSamePl = prev.playlists.length === nextLibrary.playlists.length &&
              prev.playlists.every((p, i) => p.id === nextLibrary.playlists[i].id && (p.items?.length || 0) === (nextLibrary.playlists[i].items?.length || 0));

            if (isSameFavs && isSameSaved && isSameHist && isSamePl) {
              console.log("[Library Sync] ✨ Cloud library identical to current local state. Skipping state update.");
              return prev;
            }

            console.log(`[Library Sync] 🔄 Updating state with synced library (favorites: ${finalFavs.length})...`);
            const mergedLibrary = {
              ...nextLibrary,
              favorites: finalFavs,
            };
            try {
              localStorage.setItem(STORAGE_LIBRARY_KEY, JSON.stringify(mergedLibrary));
            } catch { }
            return mergedLibrary;
          });
        } else {
          const sess = getAuthSession();
          if (!sess.idToken && !sess.refreshToken) {
            console.log("[Library Sync] Cleared library for logged out user.");
            setLibraryData(nextLibrary);
            try {
              localStorage.setItem(STORAGE_LIBRARY_KEY, JSON.stringify(nextLibrary));
            } catch { }
          }
        }
      }
    } catch (err) {
      console.error("[Library Sync Error]:", err);
    } finally {
      if (!silent) setIsLibraryLoading(false);
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
      let effectiveBrowseId = artist.id;
      const lower = artist.name.toLowerCase().trim();

      // If we don't have a browseId or it's not a channel UC.../MPREb..., search first to discover official channel ID
      if (!effectiveBrowseId || (!effectiveBrowseId.startsWith("UC") && !effectiveBrowseId.startsWith("MPREb"))) {
        try {
          const sRes = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(artist.name)}`);
          if (sRes.ok) {
            const sData: StructuredSearchResult = await sRes.json();
            const matchedArtist = (sData.artists || []).find(
              (a) => a.name.toLowerCase() === lower || lower.includes(a.name.toLowerCase()) || a.name.toLowerCase().includes(lower)
            ) || sData.artists?.[0];
            if (matchedArtist?.id && (matchedArtist.id.startsWith("UC") || matchedArtist.id.startsWith("MPREb"))) {
              effectiveBrowseId = matchedArtist.id;
            } else {
              data = sData;
            }
          }
        } catch { }
      }

      // If we have a valid browseId, query official channel uploads and releases
      if (effectiveBrowseId && (effectiveBrowseId.startsWith("UC") || effectiveBrowseId.startsWith("MPREb"))) {
        const res = await fetch(
          `${API_BASE}/api/artist?id=${encodeURIComponent(effectiveBrowseId)}&name=${encodeURIComponent(artist.name)}`
        );
        if (res.ok) {
          data = await res.json();
        } else {
          const sRes = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(artist.name)}`);
          if (!sRes.ok) throw new Error("Artist search failed");
          data = await sRes.json();
        }
      } else if (!data!) {
        const res = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(artist.name)}`);
        if (!res.ok) throw new Error("Artist search failed");
        data = await res.json();
      }

      const rawTracks: Track[] = data.tracks || [];
      const escaped = lower.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
      const artistInTitleRegex = new RegExp(`(\\b|\\()(?:cùng với|ft\\.?|feat\\.?|x|vs\\.?)\\s*.*${escaped}`, "i");
      const artistFirstRegex = new RegExp(`^${escaped}\\s*[-–|:]`, "i");

      // Strictly verify that tracks belong to this artist (official release, feat or artist's own cover)
      const authenticTracks = rawTracks.filter((t) => {
        const a = (t.author || "").toLowerCase().trim();
        const tit = (t.title || "").toLowerCase().trim();

        // 1. Exclude unwanted/bolero/karaoke content
        if (/\b(bolero|karaoke|liên khúc bolero|hát nhép)\b/i.test(tit)) {
          return false;
        }

        // 2. Exclude if someone ELSE covered it
        const coverByMatch = tit.match(/(?:cover(?:ed)?\s+by|vocal\s*:?|ca\s*sĩ\s*:?|thể\s*hiện\s*:?)\s*([^\s\(\)\[\]【】\/|,]+)/i);
        if (coverByMatch && coverByMatch[1]) {
          const coverSinger = coverByMatch[1].toLowerCase().trim();
          if (!coverSinger.includes(lower) && !lower.includes(coverSinger)) {
            return false;
          }
        }

        // 3. Author match
        if (a && a !== "nghệ sĩ" && a !== "kahoii music") {
          const authorTokens = a
            .split(/[,&+/]|(?:\s+(?:và|feat\.?|ft\.?|x|vs\.?)\s+)/i)
            .map((s) => s.trim())
            .filter(Boolean);

          const matchesAuthor = authorTokens.some(
            (token) => token === lower || token.includes(lower) || (token.length >= 3 && lower.includes(token))
          );
          if (matchesAuthor) {
            return true;
          }
        }

        // 4. Title match
        return artistInTitleRegex.test(tit) || artistFirstRegex.test(tit);
      });

      // Filtered fallback: only retain items having artist name in title, NEVER dump unfiltered junk
      const safeTracks = authenticTracks.length > 0
        ? authenticTracks
        : rawTracks.filter((t) => t.title.toLowerCase().includes(lower) && !/\b(bolero|karaoke)\b/i.test(t.title));

      setArtistTracks(safeTracks);

      // Playlists / albums: strictly require playlist to belong to this artist
      const authenticPlaylists = (data.playlists || []).filter((p) => {
        if (!p.id || p.id.startsWith("UC") || p.id.startsWith("RD")) return false;
        const countText = (p.trackCountText || "").toLowerCase();
        if (countText.includes("đĩa đơn") || countText.includes("single")) return false;

        const titleLower = (p.title || "").toLowerCase();
        const authorLower = (p.author || "").toLowerCase();

        // Must either match valid playlist ID format AND relate to the artist
        const isIdValid =
          p.id.startsWith("VL") ||
          p.id.startsWith("PL") ||
          p.id.startsWith("MPREb") ||
          p.id.startsWith("OLAK5uy_");

        if (!isIdValid) return false;

        // Ensure playlist belongs to or features this artist, not completely unrelated artists
        const isRelated = titleLower.includes(lower) || authorLower.includes(lower) || authorLower === "kahoii music" || authorLower === "youtube music";
        return isRelated;
      });
      setArtistPlaylists(authenticPlaylists);

      // Enrich artist info if available
      if (data.artists && data.artists.length > 0) {
        const matchedArtist =
          data.artists.find(
            (a) => (a.id && (a.id === artist.id || a.id === effectiveBrowseId)) || a.name.toLowerCase() === lower || lower.includes(a.name.toLowerCase())
          ) || data.artists[0];

        if (matchedArtist) {
          setSelectedArtist((prev) =>
            prev
              ? {
                ...prev,
                id: matchedArtist.id || prev.id,
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

  // Listen for background token updates to update React userSession state in real time
  useEffect(() => {
    const handleAuthUpdated = (e: any) => {
      if (e.detail?.user) {
        setUserSession((prev) => {
          if (!prev) return e.detail;
          return {
            ...prev,
            isLoggedIn: true,
            user: {
              ...prev.user,
              ...e.detail.user,
            },
          };
        });
      }
    };
    window.addEventListener("kahoii_auth_updated", handleAuthUpdated);
    return () => window.removeEventListener("kahoii_auth_updated", handleAuthUpdated);
  }, []);

  // Proactive background keep-alive interval (runs every 5 minutes to keep Firebase token fresh)
  useEffect(() => {
    const interval = setInterval(() => {
      getValidAuthToken().catch(() => { });
    }, 5 * 60 * 1000);
    return () => clearInterval(interval);
  }, []);

  // Real-time Cloud Sync with Firestore / Android App / PC App
  useEffect(() => {
    if (!userSession?.isLoggedIn) return;

    // 1. Periodic background sync polling every 60 seconds (prevents UI flicker, quota burn & race conditions)
    const syncInterval = setInterval(() => {
      console.log("[Auto-Sync] ⏰ Periodic 60s background sync checking for cloud updates...");
      fetchLibraryData(true);
    }, 60000);

    // 2. Instant sync when returning to the tab / window focus
    const handleVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        console.log("[Auto-Sync] 👁️ Tab became active/visible, syncing library...");
        fetchLibraryData(true);
        // Sync current playback position immediately from player
        if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
          try {
            const cur = ytPlayerRef.current.getCurrentTime?.() || 0;
            const dur = ytPlayerRef.current.getDuration?.() || 0;
            if (cur > 0) {
              setCurrentTime(cur);
              lastPlayPositionRef.current = cur;
            }
            if (dur > 0) {
              setDuration(Math.round(dur));
            }
            if (!isUserPausedRef.current && currentTrackRef.current) {
              if (typeof ytPlayerRef.current.getPlayerState === "function" && ytPlayerRef.current.getPlayerState() !== 1) {
                ytPlayerRef.current.playVideo();
              }
              setIsPlaying(true);
            }
          } catch { }
        } else if (activePlayerType.current === "audio" && audioRef.current) {
          const cur = audioRef.current.currentTime || 0;
          if (cur > 0) {
            setCurrentTime(cur);
            lastPlayPositionRef.current = cur;
          }
          if (!isUserPausedRef.current && currentTrackRef.current && audioRef.current.paused) {
            audioRef.current.play().then(() => setIsPlaying(true)).catch(() => { });
          }
        }
      }
    };
    const handleFocus = () => {
      console.log("[Auto-Sync] 🔍 Window focused, checking for library updates...");
      fetchLibraryData(true);
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);
    window.addEventListener("focus", handleFocus);

    return () => {
      clearInterval(syncInterval);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      window.removeEventListener("focus", handleFocus);
    };
  }, [userSession?.isLoggedIn]);

  // Synchronize Screen Wake Lock with Playback State
  useEffect(() => {
    if (isPlaying) {
      requestWakeLock();
    } else {
      releaseWakeLock();
    }
  }, [isPlaying, requestWakeLock, releaseWakeLock]);

  // Refresh Discovery / Home Feed (Fetches randomized new trending & new release tracks)
  const refreshHomeFeed = useCallback(async (showNotice = false) => {
    setIsHomeLoading(true);
    try {
      const res = await fetch(`${API_BASE}/api/home?_t=${Date.now()}`, { cache: "no-store" });
      if (res.ok) {
        const data = await res.json();
        if (data) {
          if (Array.isArray(data.trending) && data.trending.length > 0) {
            const filtered = data.trending.filter(isCleanModernTrack);
            setTrendingTracks(filtered.length > 0 ? filtered : data.trending);
          } else {
            setTrendingTracks(INITIAL_TRENDING_TRACKS);
          }
          if (Array.isArray(data.newReleases) && data.newReleases.length > 0) {
            const filtered = data.newReleases.filter(isCleanModernTrack);
            setNewReleaseTracks(filtered.length > 0 ? filtered : data.newReleases);
          } else {
            setNewReleaseTracks(INITIAL_NEW_RELEASES);
          }
          if (Array.isArray(data.artists) && data.artists.length > 0) {
            setHomeArtists(data.artists);
          }
          if (showNotice) {
            showToast("Đã làm mới danh sách bài hát khám phá!", "success");
          }
          return;
        }
      }
      setTrendingTracks(INITIAL_TRENDING_TRACKS);
      setNewReleaseTracks(INITIAL_NEW_RELEASES);
    } catch {
      setTrendingTracks(INITIAL_TRENDING_TRACKS);
      setNewReleaseTracks(INITIAL_NEW_RELEASES);
      if (showNotice) {
        showToast("Không thể làm mới khám phá, vui lòng thử lại.", "warning");
      }
    } finally {
      setIsHomeLoading(false);
    }
  }, [showToast]);

  // Fetch Personalized Recommendations based on Taste Profile & Selected Tag
  const fetchRecommendations = useCallback(
    async (tagKey?: string, isUserAction = false) => {
      const activeKey = tagKey || selectedTasteTagKey;
      const currentTag = tasteProfile.tags.find((t) => t.key === activeKey) || tasteProfile.tags[0];

      setIsRecommendationsLoading(true);
      try {
        const excludeIds = libraryData.favorites.map((t) => t.id);
        const res = await fetch(`${API_BASE}/api/recommendations`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            querySeed: currentTag?.querySeed,
            artists:
              currentTag?.type === "artist"
                ? [currentTag.label.replace("🎙️", "").trim()]
                : tasteProfile.topArtists,
            genres:
              currentTag?.type === "genre"
                ? [currentTag.label.replace("🎵", "").trim()]
                : tasteProfile.genres,
            excludeIds,
          }),
        });

        if (res.ok) {
          const data = await res.json();
          if (Array.isArray(data.tracks) && data.tracks.length > 0) {
            setRecommendedTracks(data.tracks);
            if (isUserAction) {
              showToast(`Đã làm mới gợi ý theo ${currentTag?.label || "gu của bạn"}!`, "success");
            }
            return;
          }
        }

        // Fallback: search via YouTube Music search
        const fallbackQuery =
          currentTag?.querySeed ||
          (tasteProfile.topArtists[0]
            ? `${tasteProfile.topArtists[0]} bài hát hay nhất`
            : "Nhạc thịnh hành Việt Nam mới nhất");
        const sRes = await fetch(`${API_BASE}/api/search?q=${encodeURIComponent(fallbackQuery)}`);
        if (sRes.ok) {
          const sData = await sRes.json();
          const candidateTracks = (sData.tracks || []).filter((t: any) => !excludeIds.includes(t.id));
          if (candidateTracks.length > 0) {
            setRecommendedTracks(candidateTracks.slice(0, 24));
            if (isUserAction) {
              showToast(`Đã làm mới gợi ý theo ${currentTag?.label || "gu của bạn"}!`, "success");
            }
            return;
          }
        }

        // Fallback to trending tracks
        const fallbackTrending = trendingTracks.filter((t) => !excludeIds.includes(t.id));
        setRecommendedTracks(fallbackTrending.slice(0, 18));
      } catch (err: any) {
        console.warn("[Recommendations Fetch Error]:", err?.message);
      } finally {
        setIsRecommendationsLoading(false);
      }
    },
    [selectedTasteTagKey, tasteProfile, libraryData.favorites, trendingTracks, showToast]
  );

  // Auto-refresh recommendations when taste profile updates
  useEffect(() => {
    if (tasteProfile.topArtists.length > 0) {
      fetchRecommendations(selectedTasteTagKey);
    }
  }, [tasteProfile.topArtists.join(","), tasteProfile.genres.join(",")]);

  // Preload initial track metadata, user data, home feed & YouTube Player API on mount
  useEffect(() => {
    fetchUserSession();
    fetchLibraryData();
    refreshHomeFeed(false);
    fetchRecommendations("all");

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
            height: "140",
            width: "240",
            playerVars: {
              autoplay: 0,
              controls: 0,
              disablekb: 1,
              fs: 0,
              rel: 0,
              modestbranding: 1,
              playsinline: 1,
              enablejsapi: 1,
              origin: window.location.origin,
            },
            events: {
              onReady: () => {
                const iframe = document.getElementById("kahoii-yt-iframe") as HTMLIFrameElement;
                if (iframe && iframe.tagName === "IFRAME") {
                  const cur = iframe.getAttribute("allow") || "";
                  if (!cur.includes("mediasession 'none'")) {
                    iframe.setAttribute("allow", `${cur}; mediasession 'none'`);
                  }
                }
                if (typeof ytPlayerRef.current?.setVolume === "function") {
                  ytPlayerRef.current.setVolume(isMuted ? 0 : volume * 100);
                }
              },
              onStateChange: (event: any) => {
                if (event.data === 1) {
                  setIsPlaying(true);
                  setIsLoading(false);
                  isUserPausedRef.current = false;
                  setErrorMessage(null);
                  trackPlayStartTimeRef.current = Date.now();
                  setTimeout(() => {
                    isTransitioningRef.current = false;
                  }, 2000);
                } else if (event.data === 2) {
                  // Only reflect paused if user explicitly pressed pause or app is visible in foreground
                  if (isUserPausedRef.current || document.visibilityState === "visible") {
                    setIsPlaying(false);
                  }
                  setIsLoading(false);
                } else if (event.data === 0) {
                  // Only advance track if in YouTube engine, NOT in transition, and track actually played to near end
                  if (!isTransitioningRef.current && activePlayerType.current === "youtube") {
                    const ytVid = ytPlayerRef.current?.getVideoData?.()?.video_id;
                    const currentId = currentTrackRef.current?.id;
                    if (ytVid && currentId && ytVid !== currentId) return; // Still old video!
                    const ct = ytPlayerRef.current?.getCurrentTime?.() || 0;
                    const dur = ytPlayerRef.current?.getDuration?.() || 0;
                    if (dur > 5 && ct >= dur - 3 && Date.now() - trackPlayStartTimeRef.current > 4000) {
                      handleTrackEndedRef.current();
                    }
                  }
                } else if (event.data === 3) {
                  // Buffer event: clear loading spinner after 2s max to prevent freeze on iOS
                  setIsLoading(true);
                  setTimeout(() => setIsLoading(false), 2000);
                } else if (event.data === -1) {
                  // Unstarted
                }
              },
              onError: (err: any) => {
                console.warn("[YouTube Player Error Event]:", err);
                setIsLoading(false);
                const errCode = err?.data;
                if ((errCode === 100 || errCode === 101 || errCode === 150) && !isTransitioningRef.current && activePlayerType.current === "youtube") {
                  const currentId = currentTrackRef.current?.id;
                  if (currentId && lastEndedTrackIdRef.current !== currentId && Date.now() - trackPlayStartTimeRef.current > 4000) {
                    isTransitioningRef.current = true;
                    setTimeout(() => {
                      handleTrackEndedRef.current();
                    }, 1500);
                  }
                }
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

  // Ensure YouTube iframe never hijacks system MediaSession controls
  useEffect(() => {
    const applyIframePolicy = () => {
      const iframe = document.getElementById("kahoii-yt-iframe") as HTMLIFrameElement;
      if (iframe && iframe.tagName === "IFRAME") {
        const cur = iframe.getAttribute("allow") || "";
        if (!cur.includes("mediasession 'none'")) {
          iframe.setAttribute("allow", `${cur}; mediasession 'none'`);
        }
      }
    };

    applyIframePolicy();
    const obs = new MutationObserver(applyIframePolicy);
    obs.observe(document.body, { childList: true, subtree: true });
    return () => obs.disconnect();
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
          if (ct > 0) {
            setCurrentTime(ct);
            lastPlayPositionRef.current = ct;
          }
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

          // Fallback: If video reaches within 0.5s of the end and actually ended, advance to next track
          // NOTE: Never advance if player is paused by user, or if this track was already marked ended
          const currentId = currentTrackRef.current?.id;
          const ytVid = ytPlayerRef.current?.getVideoData ? ytPlayerRef.current.getVideoData()?.video_id : null;
          if (
            dur > 5 &&
            ct >= dur - 0.5 &&
            !isTransitioningRef.current &&
            !isUserPausedRef.current &&
            currentId &&
            (!ytVid || ytVid === currentId) &&
            lastEndedTrackIdRef.current !== currentId &&
            Date.now() - trackPlayStartTimeRef.current > 4000
          ) {
            const playerState = ytPlayerRef.current.getPlayerState ? ytPlayerRef.current.getPlayerState() : -1;
            if (playerState === 0) {
              handleTrackEndedRef.current();
            }
          }
        } catch { }
      }
    }, 500);
    return () => clearInterval(timer);
  }, [duration, isPlaying]);

  // Sync volume with Audio element and YouTube Player
  useEffect(() => {
    if (audioRef.current) {
      audioRef.current.volume = volume;
      audioRef.current.muted = isMuted;
    }
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
      } catch { }
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
      saveAuthSession({
        idToken: data.user?.idToken,
        refreshToken: data.user?.refreshToken,
        expiryTimestamp: data.user?.expiryTimestamp,
      });
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
    } catch { }
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
      saveAuthSession({
        idToken: data.user?.idToken,
        refreshToken: data.user?.refreshToken,
        expiryTimestamp: data.user?.expiryTimestamp,
      });
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

  const loadAndPlayTrack = async (track: Track, autoPlay = true, _immediate = true) => {
    if (!track) return;

    isUserPausedRef.current = !autoPlay;
    lastEndedTrackIdRef.current = null;
    trackPlayStartTimeRef.current = Date.now();

    // Lock transitioning to prevent spurious ENDED events from old track while fetching stream
    isTransitioningRef.current = true;
    setTimeout(() => {
      isTransitioningRef.current = false;
    }, 10000);

    // Instant UI update: title, artwork, active queue beam update immediately with zero lag
    setCurrentTrack(track);
    currentTrackRef.current = track;
    setIsLoading(true);
    setIsPlaying(autoPlay);
    setErrorMessage(null);
    setCurrentTime(0);
    lastPlayPositionRef.current = 0;
    setLyrics(null);
    setIsLoadingLyrics(true);

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

    // 1. Direct Native Audio Stream Initiation (Immediate Synchronous Execution to keep iOS/Android User-Gesture valid)
    const directStreamUrl = `${API_BASE}/api/stream-audio?id=${track.id}`;
    if (audioRef.current) {
      if (ytPlayerRef.current && typeof ytPlayerRef.current.stopVideo === "function") {
        try { ytPlayerRef.current.stopVideo(); } catch { }
      }
      audioRef.current.src = directStreamUrl;
      audioRef.current.loop = false;
      audioRef.current.load();
      activePlayerType.current = "audio";
      if (autoPlay) {
        const playPromise = audioRef.current.play();
        if (playPromise !== undefined) {
          playPromise
            .then(() => {
              setIsPlaying(true);
              setIsLoading(false);
              isUserPausedRef.current = false;
            })
            .catch((err) => {
              console.warn("[Player] Direct stream play promise catch:", err);
            });
        }
      } else {
        setIsPlaying(false);
        setIsLoading(false);
      }
    }

    const executePlayback = async () => {
      console.log(`[Player] ▶️ Loading & playing track: "${track.title}" (${track.id}) by "${track.author || "Unknown"}"`);

      // Record to history if user is logged in
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
          console.log(`[Player] 📦 Playing "${track.title}" from local IndexedDB offline storage.`);
          const blobUrl = URL.createObjectURL(offlineItem.audioBlob);
          audioRef.current.src = blobUrl;
          audioRef.current.loop = false;
          audioRef.current.load();
          if (autoPlay) {
            audioRef.current.play().then(() => {
              setIsPlaying(true);
              setIsLoading(false);
              isUserPausedRef.current = false;
            }).catch(() => {});
          }
          if (offlineItem.lyrics) {
            setLyrics(offlineItem.lyrics);
            setIsLoadingLyrics(false);
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

      // Safety watchdog: ensure loading spinner is cleared after 4s max if stalled
      setTimeout(() => {
        setIsLoading(false);
      }, 4000);

      // Fetch Lyrics in background without delaying playback
      try {
        const lyricsRes = await fetch(
          `${API_BASE}/api/lyrics?videoId=${encodeURIComponent(track.id)}&title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.author || "")}&duration=${track.duration || ""}`
        );
        if (lyricsRes.ok) {
          const lyricsData = await lyricsRes.json();
          if (lyricsData?.lines && lyricsData.lines.length > 0) {
            setLyrics(lyricsData);
          }
          if (lyricsData?.artist && (!track.author || track.author === "Nghệ sĩ" || track.author === "Kahoii Music")) {
            track.author = lyricsData.artist;
            setCurrentTrack((prev) => (prev && prev.id === track.id ? { ...prev, author: lyricsData.artist } : prev));
          }
        }
      } catch { } finally {
        setIsLoadingLyrics(false);
      }
    };

    // Execute background tasks immediately
    executePlayback();
  };

  const handleTimeUpdate = () => {
    if (activePlayerType.current === "audio" && audioRef.current) {
      const cur = audioRef.current.currentTime;
      setCurrentTime(cur);
      lastPlayPositionRef.current = cur;
      const dur = audioRef.current.duration;
      if (dur > 0 && !isNaN(dur) && dur !== duration) {
        setDuration(Math.round(dur));
      }
    }
  };

  const togglePlayPause = () => {
    if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
      try {
        if (isPlaying) {
          isUserPausedRef.current = true;
          ytPlayerRef.current.pauseVideo();
          setIsPlaying(false);
          if (audioRef.current) audioRef.current.pause();
        } else {
          isUserPausedRef.current = false;
          ytPlayerRef.current.playVideo();
          setIsPlaying(true);
        }
        return;
      } catch { }
    }

    if (!currentTrack) {
      return;
    }

    if (!audioRef.current) return;
    if (isPlaying) {
      isUserPausedRef.current = true;
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      isUserPausedRef.current = false;
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
    if (!currentTrack) return;
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
    const currentId = currentTrackRef.current?.id;
    if (isTransitioningRef.current || !currentId) return;

    // Prevent double trigger on the same track (stops the 1s skip bug)
    if (lastEndedTrackIdRef.current === currentId) return;

    // Disallow ending a track that has not been playing for at least 4 seconds
    if (Date.now() - trackPlayStartTimeRef.current < 4000) return;

    lastEndedTrackIdRef.current = currentId;
    isTransitioningRef.current = true;
    setTimeout(() => {
      isTransitioningRef.current = false;
    }, 3000);

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
        } catch { }
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
        audioRef.current.play().catch(() => { });
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
    if (!currentTrack) return;
    const activeQueue =
      queueTracks.length > 0
        ? queueTracks
        : playlist.length > 0
          ? playlist
          : trendingTracks.length > 0
            ? trendingTracks
            : libraryData.history;
    if (activeQueue.length === 0) return;
    if (activeQueue.length === 1) {
      seekToTime(0);
      return;
    }

    let curIdx = activeQueue.findIndex((t) => t.id === currentTrack?.id);
    if (curIdx === -1) curIdx = currentIndex;
    if (curIdx < 0 || curIdx >= activeQueue.length) curIdx = 0;

    if (repeatModeRef.current === "shuffle") {
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
    if (!currentTrack) return;
    const activeQueue =
      queueTracks.length > 0
        ? queueTracks
        : playlist.length > 0
          ? playlist
          : trendingTracks.length > 0
            ? trendingTracks
            : libraryData.history;
    if (activeQueue.length === 0) return;

    if (currentTime > 3) {
      if (
        activePlayerType.current === "youtube" &&
        ytPlayerRef.current &&
        typeof ytPlayerRef.current.seekTo === "function"
      ) {
        try { ytPlayerRef.current.seekTo(0, true); } catch { }
      } else if (audioRef.current) {
        audioRef.current.currentTime = 0;
      }
      setCurrentTime(0);
      return;
    }

    if (activeQueue.length === 1) {
      seekToTime(0);
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
    const clamped = Math.max(0, Math.min(newTime, duration || newTime));
    setCurrentTime(clamped);
    lastPlayPositionRef.current = clamped;
    if (activePlayerType.current === "youtube" && ytPlayerRef.current && typeof ytPlayerRef.current.seekTo === "function") {
      try { ytPlayerRef.current.seekTo(clamped, true); } catch { }
    } else if (audioRef.current) {
      audioRef.current.currentTime = clamped;
    }
    if ("mediaSession" in navigator && "setPositionState" in navigator.mediaSession && duration > 0) {
      try {
        navigator.mediaSession.setPositionState({
          duration,
          playbackRate: isPlaying ? 1 : 0,
          position: clamped,
        });
      } catch { }
    }
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!currentTrack) return;
    seekToTime(parseFloat(e.target.value));
  };

  const handleSeekBackward = (offset = 10) => {
    if (!currentTrack) return;
    const cur = activePlayerType.current === "audio" && audioRef.current
      ? audioRef.current.currentTime
      : currentTime;
    seekToTime(Math.max(0, cur - offset));
  };

  const handleSeekForward = (offset = 10) => {
    if (!currentTrack) return;
    const cur = activePlayerType.current === "audio" && audioRef.current
      ? audioRef.current.currentTime
      : currentTime;
    const dur = duration > 0 ? duration : 3600;
    seekToTime(Math.min(dur, cur + offset));
  };

  const handleStop = () => {
    isUserPausedRef.current = true;
    if (activePlayerType.current === "audio" && audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
    } else if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
      try { ytPlayerRef.current.pauseVideo(); } catch { }
    }
    setIsPlaying(false);
    setCurrentTime(0);
  };

  // Mutable refs for MediaSession action callbacks so handlers are bound ONCE and never torn down
  const handlePlayRef = useRef<() => void>(() => { });
  const handlePauseRef = useRef<() => void>(() => { });
  const handlePrevRef = useRef<() => void>(() => { });
  const handleNextRef = useRef<() => void>(() => { });
  const handleSeekBackwardRef = useRef<(offset?: number) => void>(() => { });
  const handleSeekForwardRef = useRef<(offset?: number) => void>(() => { });
  const seekToTimeRef = useRef<(newTime: number) => void>(() => { });
  const handleStopRef = useRef<() => void>(() => { });

  useEffect(() => {
    handlePlayRef.current = () => {
      isUserPausedRef.current = false;
      if (activePlayerType.current === "audio" && audioRef.current) {
        audioRef.current.play().catch(() => { });
      } else if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
        try { ytPlayerRef.current.playVideo(); } catch { }
      }
      setIsPlaying(true);
    };
    handlePauseRef.current = () => {
      isUserPausedRef.current = true;
      if (activePlayerType.current === "audio" && audioRef.current) {
        audioRef.current.pause();
      } else if (activePlayerType.current === "youtube" && ytPlayerRef.current) {
        try { ytPlayerRef.current.pauseVideo(); } catch { }
        if (audioRef.current) audioRef.current.pause();
      }
      setIsPlaying(false);
    };
    handlePrevRef.current = handlePrev;
    handleNextRef.current = handleNext;
    handleSeekBackwardRef.current = handleSeekBackward;
    handleSeekForwardRef.current = handleSeekForward;
    seekToTimeRef.current = seekToTime;
    handleStopRef.current = handleStop;
  });

  // 1. MediaSession API: Register Standard Actions ONCE on Mount (Ensures Back/Next, Seek 10s & Seekbar appear on Android/iOS Lock Screen & Desktop GMC)
  useEffect(() => {
    if (!("mediaSession" in navigator)) return;

    try { navigator.mediaSession.setActionHandler("stop", null); } catch { }

    const actionList: [MediaSessionAction, MediaSessionActionHandler][] = [
      ["play", () => handlePlayRef.current()],
      ["pause", () => handlePauseRef.current()],
      ["previoustrack", () => handlePrevRef.current()],
      ["nexttrack", () => handleNextRef.current()],
      ["seekbackward", (details) => handleSeekBackwardRef.current(details?.seekOffset || 10)],
      ["seekforward", (details) => handleSeekForwardRef.current(details?.seekOffset || 10)],
      ["seekto", (details) => {
        if (details.seekTime !== undefined && details.seekTime !== null) {
          seekToTimeRef.current(details.seekTime);
        }
      }],
    ];

    for (const [action, handler] of actionList) {
      try {
        navigator.mediaSession.setActionHandler(action, handler);
      } catch (e) {
        console.warn(`[MediaSession] Action "${action}" registration:`, e);
      }
    }
  }, []);

  // 2. MediaSession API: Synchronize Track Metadata when Current Track Changes
  useEffect(() => {
    if (!("mediaSession" in navigator)) return;

    if (!currentTrack) {
      try {
        navigator.mediaSession.metadata = null;
      } catch { }
      return;
    }

    const artUrl = getTrackThumbnail(currentTrack, "hd");

    try {
      navigator.mediaSession.metadata = new MediaMetadata({
        title: currentTrack.title || "Kahoii Music",
        artist: formatTrackArtist(currentTrack.title, currentTrack.author) || "Kahoii Music",
        album: "Kahoii Music",
        artwork: [
          { src: artUrl, sizes: "96x96", type: "image/jpeg" },
          { src: artUrl, sizes: "128x128", type: "image/jpeg" },
          { src: artUrl, sizes: "192x192", type: "image/jpeg" },
          { src: artUrl, sizes: "256x256", type: "image/jpeg" },
          { src: artUrl, sizes: "512x512", type: "image/jpeg" },
        ],
      });
    } catch (err) {
      console.warn("[MediaSession] Metadata update error:", err);
    }
  }, [currentTrack?.id, currentTrack?.title, currentTrack?.author, currentTrack?.thumbnail]);

  // Synchronize Browser Tab Title (YouTube & YouTube Music style: song title first)
  useEffect(() => {
    if (currentTrack?.title) {
      const artist = formatTrackArtist(currentTrack.title, currentTrack.author);
      document.title = artist
        ? `${currentTrack.title} • ${artist} - Kahoii Music`
        : `${currentTrack.title} - Kahoii Music`;
    } else {
      document.title = "Kahoii Music";
    }
  }, [currentTrack?.id, currentTrack?.title, currentTrack?.author]);

  // 3. MediaSession API: Synchronize Playback State & Position on State/Track/Duration Changes
  useEffect(() => {
    if (!("mediaSession" in navigator)) return;

    const effectivePlaying = isPlaying || (!isUserPausedRef.current && !!currentTrack);
    try {
      navigator.mediaSession.playbackState = effectivePlaying ? "playing" : "paused";
    } catch { }

    if ("setPositionState" in navigator.mediaSession && duration > 0) {
      try {
        const cur = activePlayerType.current === "audio" && audioRef.current
          ? audioRef.current.currentTime
          : currentTime;
        navigator.mediaSession.setPositionState({
          duration: duration,
          playbackRate: effectivePlaying ? 1 : 0,
          position: Math.min(Math.max(0, cur), duration),
        });
      } catch (err) {
        console.warn("[MediaSession] Position state error:", err);
      }
    }
  }, [isPlaying, currentTrack?.id, duration]);

  // Top Search Submit
  const handleTopSearchSubmit = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!topSearchText.trim()) return;

    const ytMatch = topSearchText.match(
      /(?:youtube\.com\/(?:[^/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?/\s]{11})/i
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
            /(?:youtube\.com\/(?:[^/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?/\s]{11})/i
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
  const recommendedTracksToDisplay =
    recommendedTracks.length > 0
      ? recommendedTracks
      : libraryData.favorites.length > 0
        ? libraryData.favorites.slice(0, 12)
        : trendingTracks.slice(0, 12);

  return (
    <div className="w-full max-w-full h-screen h-[100dvh] flex flex-col justify-between overflow-hidden text-white select-none relative font-sans">
      <audio
        ref={audioRef}
        preload="auto"
        playsInline
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={() => {
          if (activePlayerType.current === "audio") {
            setDuration(audioRef.current?.duration || currentTrack?.duration || 207);
            setIsLoading(false);
          }
        }}
        onCanPlay={() => {
          if (activePlayerType.current === "audio") {
            setIsLoading(false);
          }
        }}
        onPlaying={() => {
          if (activePlayerType.current === "audio") {
            setIsPlaying(true);
            setIsLoading(false);
            trackPlayStartTimeRef.current = Date.now();
            setTimeout(() => {
              isTransitioningRef.current = false;
            }, 2000);
          }
        }}
        onEnded={() => {
          if (activePlayerType.current === "audio" && !isTransitioningRef.current) {
            const a = audioRef.current;
            if (a && a.src && !a.src.startsWith("data:") && a.currentTime > 5 && Date.now() - trackPlayStartTimeRef.current > 4000) {
              handleTrackEndedRef.current();
            }
          }
        }}
        onPlay={() => {
          if (activePlayerType.current === "audio") {
            setIsPlaying(true);
            setIsLoading(false);
          }
        }}
        onPause={() => {
          if (activePlayerType.current === "audio") {
            if (isUserPausedRef.current) {
              setIsPlaying(false);
            }
          }
        }}
        onError={() => {
          if (activePlayerType.current === "audio") {
            console.warn("[Stream Audio Error] Direct stream failed, falling back to YouTube Player engine...");
            const currentId = currentTrackRef.current?.id;
            if (currentId && ytPlayerRef.current && typeof ytPlayerRef.current.loadVideoById === "function") {
              activePlayerType.current = "youtube";
              ytPlayerRef.current.loadVideoById({
                videoId: currentId,
                startSeconds: lastPlayPositionRef.current || 0,
              });
              if (!isUserPausedRef.current) {
                try { ytPlayerRef.current.playVideo(); } catch { }
              }
            }
            setIsLoading(false);
          }
        }}
      />

      {/* Hidden YouTube Iframe Container for 100% Reliable Playback (Satisfies iOS WebKit & Android visibility checks) */}
      <div
        id="kahoii-yt-iframe"
        style={{
          position: "fixed",
          bottom: "10px",
          right: "10px",
          width: "240px",
          height: "140px",
          opacity: 0.02,
          pointerEvents: "none",
          zIndex: 0,
        }}
      />

      {/* ========================================================================= */}
      {/* 1. TOP BAR (Seamless & Centered matching Figma mockup, Safe-Area aware)     */}
      {/* ========================================================================= */}
      <header className="w-full flex flex-col justify-end flex-shrink-0 z-30 safe-header">
        <div className="w-full h-[54px] flex items-center justify-between min-w-0 gap-1 sm:gap-3">
          {/* Left: App Logo & Brand Identity */}
          <div className="flex items-center gap-1 sm:gap-2 flex-shrink-0">
            <img
              src="./logo_icon.png"
              alt="Kahoii Music"
              className="w-6 h-6 sm:w-7 sm:h-7 rounded-lg object-contain shadow-md shadow-black/40"
            />
            <span className="hidden sm:inline font-extrabold text-sm tracking-wide bg-gradient-to-r from-pink-400 via-purple-300 to-indigo-300 bg-clip-text text-transparent">
              Kahoii Music
            </span>
          </div>

          {/* Center: Search Pill with Search History Suggestions Dropdown */}
          <div ref={searchDropdownRef} className="relative flex-1 min-w-0 max-w-[580px] mx-1 sm:mx-4">
            <form
              onSubmit={handleTopSearchSubmit}
              className="w-full min-w-0 h-[34px] sm:h-[36px] px-2 sm:px-4 rounded-full theme-input-pill flex items-center gap-1.5 sm:gap-2.5 shadow-inner transition-all overflow-hidden"
            >
              <Search className="w-3.5 h-3.5 sm:w-4 sm:h-4 theme-accent-text flex-shrink-0" />
              <input
                type="text"
                placeholder="Search"
                value={topSearchText}
                onFocus={() => setIsSearchFocused(true)}
                onChange={(e) => setTopSearchText(e.target.value)}
                className="w-full min-w-0 flex-1 bg-transparent text-xs sm:text-sm text-white placeholder-white/40 focus:outline-none"
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
          <div className="flex items-center gap-1 sm:gap-2 flex-shrink-0">
            {!isOnline && (
              <div
                title="Bạn đang ngoại tuyến. Các bài hát đã lưu trong mục 'Đã tải' vẫn nghe được bình thường!"
                className="flex items-center gap-1 px-2 py-1 rounded-full bg-amber-500/20 border border-amber-500/40 text-amber-300 text-[10px] sm:text-[11px] font-medium animate-pulse cursor-pointer flex-shrink-0"
                onClick={() => setActiveLeftTab("offline")}
              >
                <WifiOff className="w-3 h-3 sm:w-3.5 sm:h-3.5" />
                <span className="hidden sm:inline">Ngoại tuyến (Offline)</span>
              </div>
            )}

            {/* Download App Button & Dropdown */}
            <div className="relative flex-shrink-0" ref={downloadMenuRef}>
              <button
                type="button"
                onClick={() => setShowDownloadMenu(!showDownloadMenu)}
                title="Tải ứng dụng Kahoii Music (PC, Extension, Android, iOS, PWA)"
                className={`flex items-center justify-center p-1.5 sm:px-3 sm:py-1.5 rounded-full transition-all cursor-pointer shadow-md border ${showDownloadMenu
                  ? "theme-badge-active scale-105"
                  : "theme-badge hover:theme-btn-primary hover:text-white text-white/90 hover:scale-105 border-white/10"
                  }`}
              >
                <Download className="w-3.5 h-3.5 theme-accent-text" />
                <span className="hidden sm:inline text-xs font-semibold ml-1.5">Tải ứng dụng</span>
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
                className="flex items-center gap-1.5 p-1 sm:pl-1.5 sm:pr-3.5 sm:py-1 rounded-full theme-badge hover:theme-btn-primary hover:text-white text-white font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 border theme-accent-border group flex-shrink-0"
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
                className="p-1.5 sm:px-3.5 sm:py-1.5 rounded-full theme-btn-primary hover:opacity-90 text-white font-semibold text-xs cursor-pointer shadow-md transition-all hover:scale-105 flex items-center justify-center gap-1.5 flex-shrink-0"
              >
                <User className="w-3.5 h-3.5" />
                <span className="hidden sm:inline">Đăng nhập</span>
              </button>
            )}
          </div>
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
          className={`absolute top-16 left-1/2 -translate-x-1/2 z-50 px-4 py-2.5 rounded-xl border backdrop-blur-xl shadow-2xl flex items-center gap-2.5 text-xs font-medium animate-fade-in ${toast.type === "warning"
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
      <main className="flex-1 w-full px-2 sm:px-4 lg:px-5 pb-[calc(76px+var(--safe-bottom))] lg:pb-5 pt-1 flex gap-3 lg:gap-4 overflow-hidden min-h-0 relative">
        {/* ----------------------------------------------------------------------- */}
        {/* COLUMN 1 (LEFT): Sidebar / Library Panel (~19% Width)                   */}
        {/* ----------------------------------------------------------------------- */}
        <aside
          className={`${mobileTab === "library" ? "flex w-full" : "hidden"
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
                className={`relative z-20 py-2 px-0.5 rounded-xl text-xs font-semibold text-center transition-colors duration-200 cursor-pointer truncate flex items-center justify-center gap-1 ${activeLeftTab === tab.key ? "text-white" : "text-white/70 hover:text-white"
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
                  className={`p-1 rounded-md transition-all cursor-pointer flex-shrink-0 ${isLeftSorted ? "theme-badge-active" : "theme-accent-text hover:text-white"
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
                key={activeLeftTab}
                className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1 item-slide-up"
                style={{ gap: "12px" }}
              >
                {getSidebarItems().length > 0 ? (
                  getSidebarItems().map((item, idx) => {
                    const itemId = item.id || (item.rawPlaylist?.id || item.rawPlaylist?.name) || `sidebar-item-${idx}`;
                    return (
                      <div
                        key={itemId}
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
                        className={`flex items-center gap-3.5 p-2 rounded-2xl transition-all duration-200 cursor-pointer group ${(activeLeftTab === "library" && activeLibraryPlaylistId === (item.rawPlaylist?.id || item.rawPlaylist?.name)) ||
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
                    );
                  })
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
          className={`${mobileTab === "explore" ? "flex w-full" : "hidden"
            } lg:flex flex-1 h-full figma-panel flex-col min-h-0 overflow-hidden ${
              isNowPlayingViewOpen && currentTrack ? "p-0 relative" : "p-2.5 sm:p-4 lg:p-7"
            }`}
        >
          {isNowPlayingViewOpen && currentTrack ? (
            /* ===================================================================== */
            /* NOW PLAYING EXPANDED PLAYER VIEW (Wireframe layout)                  */
            /* ===================================================================== */
            <div className="relative w-full h-full flex flex-col justify-between overflow-hidden select-none animate-fade-in">
              {/* Background cover artwork + dynamic color/vignette overlay */}
              <div className="absolute inset-0 pointer-events-none overflow-hidden select-none z-0">
                <img
                  src={getTrackThumbnail(currentTrack, "hd")}
                  alt={currentTrack.title || ""}
                  onError={(e) => {
                    const target = e.currentTarget;
                    if (target.src.includes("hq720.jpg") && currentTrack?.id) {
                      target.src = `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg`;
                    } else if (!target.src.includes(DEFAULT_TRACK_ART)) {
                      target.src = DEFAULT_TRACK_ART;
                    }
                  }}
                  className={`w-full h-full object-cover scale-105 transition-all duration-700 ease-in-out ${
                    showCenterLyrics
                      ? "brightness-[0.65] contrast-[1.05]"
                      : "brightness-100 contrast-100"
                  }`}
                />

                {/* 1. Full dark backdrop for lyrics legibility (Fades out when lyrics are OFF to show original colors) */}
                <div
                  className={`absolute inset-0 bg-gradient-to-b from-black/85 via-black/60 to-black/90 transition-opacity duration-700 ease-in-out ${
                    showCenterLyrics ? "opacity-100" : "opacity-0"
                  }`}
                />

                {/* 2. Ambient neon glow accent (Only when lyrics are enabled) */}
                <div
                  className={`absolute inset-0 bg-[radial-gradient(circle_at_center,rgba(236,72,153,0.14)_0%,transparent_70%)] transition-opacity duration-700 ease-in-out ${
                    showCenterLyrics ? "opacity-100" : "opacity-0"
                  }`}
                />

                {/* 3. Subtle edge vignette for UI clarity when lyrics are OFF (Original artwork color in center) */}
                <div
                  className={`absolute inset-0 transition-opacity duration-700 ease-in-out pointer-events-none ${
                    !showCenterLyrics ? "opacity-100" : "opacity-0"
                  }`}
                >
                  <div className="absolute top-0 inset-x-0 h-28 bg-gradient-to-b from-black/75 via-black/25 to-transparent" />
                  <div className="absolute bottom-0 inset-x-0 h-40 bg-gradient-to-t from-black/80 via-black/30 to-transparent" />
                </div>
              </div>

              {/* 1. TOP HEADER CLUSTER (z-30) */}
              <div
                className={`relative z-30 flex items-center justify-between px-5 sm:px-8 pt-5 pb-2 flex-shrink-0 transition-all duration-700 ${
                  showCenterLyrics ? "border-b border-white/5 bg-black/20" : "border-b border-transparent bg-transparent"
                }`}
              >
                {/* Back button */}
                <button
                  type="button"
                  onClick={() => setIsNowPlayingViewOpen(false)}
                  className="px-4 py-1.5 rounded-full bg-purple-700/80 hover:bg-purple-600 text-white text-xs font-semibold flex items-center gap-1.5 transition-all shadow-lg hover:scale-105 cursor-pointer active:scale-95 border border-purple-400/30 flex-shrink-0"
                >
                  <ArrowLeft className="w-3.5 h-3.5" />
                  <span>back</span>
                </button>

                {/* Song Name & Author */}
                <div className="flex-1 min-w-0 flex flex-col items-center justify-center px-4 text-center">
                  <h2
                    className="text-xl sm:text-2xl lg:text-3xl font-extrabold text-white truncate max-w-full drop-shadow-[0_2px_12px_rgba(0,0,0,0.95)] tracking-tight"
                    title={currentTrack.title}
                  >
                    {currentTrack.title}
                  </h2>
                  <p className="text-xs sm:text-sm text-white/80 font-medium truncate max-w-full drop-shadow-md mt-0.5">
                    {formatTrackArtist(currentTrack.title, currentTrack.author)}
                  </p>
                </div>

                {/* Right controls: Synced badge, Aa size, Romanji */}
                <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
                  {isLoadingLyrics ? (
                    <span className="px-3 py-0.5 rounded-full text-[10px] sm:text-[11px] font-semibold bg-purple-900/60 text-purple-200 border border-purple-500/30 flex items-center gap-1.5 animate-pulse shadow-sm">
                      <Loader2 className="w-3 h-3 animate-spin text-pink-400" />
                      <span>Đang tải lời...</span>
                    </span>
                  ) : isLyricsSynced ? (
                    <span className="px-3 py-0.5 rounded-full text-[10px] sm:text-[11px] font-bold uppercase tracking-wider bg-pink-500 text-white shadow-[0_0_12px_rgba(236,72,153,0.85)] flex items-center gap-1">
                      <Sparkles className="w-3 h-3 text-white" />
                      Synced
                    </span>
                  ) : parsedLyrics.length > 0 || _lyrics?.plainLyrics ? (
                    <span className="px-3 py-0.5 rounded-full text-[10px] sm:text-[11px] font-medium bg-purple-900/80 text-purple-200 border border-purple-500/30">
                      Non synced
                    </span>
                  ) : (
                    <span className="px-2.5 py-0.5 rounded-full text-[10px] text-white/40 bg-black/40">
                      Không có lời
                    </span>
                  )}

                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      onClick={() => setCenterLyricFontSize((prev) => (prev % 3) + 1)}
                      title="Đổi cỡ chữ lời bài hát"
                      className="px-2.5 py-1 rounded-full bg-purple-950/80 hover:bg-purple-800/80 text-purple-200 hover:text-white border border-purple-500/40 text-[11px] font-semibold transition-all cursor-pointer shadow-md flex items-center gap-1 active:scale-95"
                    >
                      <span>Aa {centerLyricFontSize}</span>
                    </button>

                    {hasRomanizationSupport && (
                      <button
                        type="button"
                        onClick={toggleLyricDisplayMode}
                        title="Đổi phiên âm Romaji / Pinyin"
                        className="px-3 py-1 rounded-full bg-purple-700/80 hover:bg-purple-600 text-white border border-purple-400/40 text-[11px] font-semibold transition-all cursor-pointer shadow-md flex items-center gap-1 active:scale-95"
                      >
                        <Languages className="w-3 h-3" />
                        <span>{romaziPillLabel}</span>
                      </button>
                    )}
                  </div>
                </div>
              </div>

              {/* 2. MIDDLE LYRICS AREA (z-10) */}
              <div className="relative flex-1 min-h-0 overflow-hidden flex flex-col items-center justify-center z-10 select-none">
                {showCenterLyrics ? (
                  isLoadingLyrics ? (
                    <div className="flex-1 flex flex-col items-center justify-center text-center text-white/70 gap-3 my-auto animate-fade-in select-none">
                      <div className="w-12 h-12 rounded-full bg-pink-500/20 border border-pink-500/40 flex items-center justify-center shadow-lg animate-pulse">
                        <Loader2 className="w-6 h-6 animate-spin text-pink-400" />
                      </div>
                      <div className="flex flex-col gap-1">
                        <p className="text-sm font-bold text-white tracking-wide">Đang tải lời bài hát...</p>
                        <p className="text-xs text-white/40">Đang tìm kiếm và đồng bộ lời bài hát</p>
                      </div>
                    </div>
                  ) : parsedLyrics.length > 0 ? (
                    <div
                      ref={centerLyricsContainerRef}
                      className="w-full h-full overflow-y-auto scroll-smooth custom-scrollbar px-6 flex flex-col items-center text-center"
                      style={{
                        paddingTop: "calc(45vh - 70px)",
                        paddingBottom: "calc(45vh - 70px)",
                        maskImage: "linear-gradient(to bottom, transparent 0%, black 15%, black 85%, transparent 100%)",
                        WebkitMaskImage: "linear-gradient(to bottom, transparent 0%, black 15%, black 85%, transparent 100%)",
                        gap: centerLyricFontSize === 1 ? "16px" : centerLyricFontSize === 2 ? "22px" : "30px",
                      }}
                    >
                      {parsedLyrics.map((line, idx) => {
                        const isActive = isLyricsSynced && idx === activeLyricIndex;
                        let primaryText = line.text || "♪";
                        let secondaryText: string | null = null;
                        if (lyricDisplayMode === "romanized" && line.romanized) {
                          primaryText = line.romanized;
                        } else if (lyricDisplayMode === "dual" && line.romanized) {
                          primaryText = line.text;
                          secondaryText = line.romanized;
                        }

                        return (
                          <div
                            key={idx}
                            ref={isActive ? centerActiveLyricRef : undefined}
                            onClick={() => {
                              if (line.time > 0) seekToTime(line.time);
                            }}
                            className={`select-none cursor-pointer text-center rounded-2xl px-5 py-2 max-w-3xl transform-gpu font-bold border ${
                              isActive
                                ? "opacity-100 scale-[1.03] text-white drop-shadow-[0_0_20px_rgba(236,72,153,0.95)] bg-white/10 backdrop-blur-sm border-pink-500/35 shadow-xl"
                                : "opacity-40 text-white hover:opacity-85 hover:scale-[1.01] border-transparent"
                            } ${
                              centerLyricFontSize === 1
                                ? "text-base sm:text-lg"
                                : centerLyricFontSize === 2
                                  ? "text-lg sm:text-xl"
                                  : "text-xl sm:text-2xl"
                            }`}
                            style={{
                              transition: "transform 0.45s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.4s cubic-bezier(0.22, 1, 0.36, 1), background-color 0.35s ease, border-color 0.35s ease",
                              willChange: "transform, opacity",
                            }}
                          >
                            <p className="leading-relaxed drop-shadow-md">{primaryText}</p>
                            {secondaryText && (
                              <p className={`mt-0.5 text-white/50 font-normal ${
                                centerLyricFontSize === 1 ? "text-xs sm:text-sm" : "text-sm sm:text-base"
                              }`}>
                                {secondaryText}
                              </p>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  ) : _lyrics?.plainLyrics ? (
                    <div className="max-w-2xl text-center text-white/80 whitespace-pre-line leading-loose text-base font-medium py-10">
                      {_lyrics.plainLyrics}
                    </div>
                  ) : (
                    <div className="flex-1 flex flex-col items-center justify-center text-center text-white/40 gap-2 select-none">
                      <Mic2 className="w-10 h-10 opacity-30" />
                      <p className="text-sm font-medium">Chưa có lời bài hát cho ca khúc này</p>
                    </div>
                  )
                ) : null}
              </div>

              {/* 3. BOTTOM FOOTER CONTROLS & SEEKBAR (z-20 - Lớp phía trên lyrics) */}
              <div
                className={`relative z-20 w-full px-5 sm:px-8 pb-6 pt-3 flex flex-col gap-2.5 transition-all duration-700 ${
                  showCenterLyrics
                    ? "bg-gradient-to-t from-black/95 via-black/75 to-transparent"
                    : "bg-gradient-to-t from-black/85 via-black/40 to-transparent"
                }`}
              >
                {/* Row 1: 2 Function Buttons Above Progress Bar */}
                <div className="flex items-center justify-between w-full px-1">
                  {/* Left: Menu */}
                  <div className="relative" ref={centerPlayerMenuRef}>
                    <button
                      type="button"
                      onClick={() => {
                        setShowPlayerMenu(!showPlayerMenu);
                        if (showPlayerMenu) {
                          setShowLibrarySubmenu(false);
                          setIsCreatingPlaylist(false);
                        }
                      }}
                      className={`w-9 h-9 rounded-xl backdrop-blur-md flex items-center justify-center transition-all cursor-pointer ${
                        showPlayerMenu
                          ? "theme-badge-active shadow-md"
                          : "bg-black/50 text-white/80 hover:text-white hover:bg-black/70 border border-white/10"
                      }`}
                      title="Tùy chọn bài hát"
                    >
                      <Menu className="w-4 h-4" />
                    </button>

                    {showPlayerMenu && currentTrack && (
                      <div className="absolute bottom-11 left-0 z-40 bg-[#150428]/98 backdrop-blur-2xl rounded-2xl shadow-[0_12px_45px_rgba(0,0,0,0.85)] p-2 min-w-[220px] max-w-[260px] flex flex-col gap-1 text-xs animate-fade-in border border-purple-500/40">
                        {showLibrarySubmenu ? (
                          /* Submenu View: Drill-down in-place (No separate popup, 0% overlap) */
                          <div className="flex flex-col gap-1.5 animate-fade-in">
                            <div className="flex items-center justify-between px-1 pb-1 border-b border-white/10 text-[10px] font-bold uppercase tracking-wider theme-accent-text">
                              <button
                                type="button"
                                onClick={() => {
                                  setShowLibrarySubmenu(false);
                                  setIsCreatingPlaylist(false);
                                }}
                                className="flex items-center gap-1 text-white/70 hover:text-white transition-colors cursor-pointer text-[10.5px] font-semibold group/back"
                              >
                                <ChevronLeft className="w-3.5 h-3.5 group-hover/back:-translate-x-0.5 transition-transform" />
                                <span>Quay lại</span>
                              </button>
                              <span>Thêm vào playlist</span>
                            </div>

                            {!userSession?.isLoggedIn ? (
                              <div className="p-2.5 text-center flex flex-col gap-2">
                                <p className="text-[11px] text-white/60">Đăng nhập để quản lý danh sách phát của bạn</p>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setShowPlayerMenu(false);
                                    setShowLibrarySubmenu(false);
                                    setIsAccountOpen(true);
                                  }}
                                  className="w-full py-1.5 rounded-lg theme-btn-primary text-white font-bold text-xs cursor-pointer"
                                >
                                  Đăng nhập ngay
                                </button>
                              </div>
                            ) : (
                              <>
                                <div className="max-h-48 overflow-y-auto flex flex-col gap-1 pr-0.5 custom-scrollbar">
                                  {libraryData.playlists.length > 0 ? (
                                    libraryData.playlists.map((pl) => {
                                      const isInPl = pl.items?.some((it: any) => it.id === currentTrack.id);
                                      return (
                                        <button
                                          key={pl.id}
                                          type="button"
                                          onClick={() => handleToggleTrackInPlaylist(pl.id)}
                                          className={`flex items-center justify-between px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer text-left ${
                                            isInPl ? "bg-white/15 text-white font-medium shadow-sm" : "hover:bg-white/5 text-white/80"
                                          }`}
                                        >
                                          <span className="truncate max-w-[150px] text-[11.5px]">{pl.name}</span>
                                          {isInPl && <Check className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0 ml-1" />}
                                        </button>
                                      );
                                    })
                                  ) : (
                                    <span className="text-[11px] text-white/40 italic px-2 py-1 text-center">Chưa có playlist nào</span>
                                  )}
                                </div>

                                <div className="pt-1.5 border-t border-white/10">
                                  {!isCreatingPlaylist ? (
                                    <button
                                      type="button"
                                      onClick={() => setIsCreatingPlaylist(true)}
                                      className="w-full py-1 text-center text-[11px] font-semibold text-white/70 hover:theme-accent-text hover:bg-white/5 rounded transition-colors flex items-center justify-center gap-1 cursor-pointer"
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
                                </div>
                              </>
                            )}
                          </div>
                        ) : (
                          /* Main Menu View: 3 Options */
                          <div className="flex flex-col gap-1">
                            {/* Option 1: Lưu vào playlist */}
                            <button
                              type="button"
                              onClick={() => setShowLibrarySubmenu(true)}
                              className="flex items-center justify-between w-full px-2.5 py-2 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                            >
                              <span className="flex items-center gap-2">
                                <FolderPlus className="w-4 h-4 theme-accent-text" />
                                <span>Lưu vào playlist</span>
                              </span>
                              <div className="flex items-center gap-1">
                                {libraryData.playlists.some((pl) => pl.items?.some((it: any) => it.id === currentTrack.id)) && (
                                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                                )}
                                <ChevronRight className="w-3.5 h-3.5 text-white/40" />
                              </div>
                            </button>

                            {/* Option 2: Lưu ngoại tuyến */}
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
                                    ? "Đang lưu..."
                                    : offlineTrackIds.has(currentTrack.id)
                                      ? "Đã lưu offline (Xóa)"
                                      : "Lưu nghe offline"}
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
                      </div>
                    )}
                  </div>

                  {/* Right: Nút Lyrics (Phía trên bên phải thanh tiến trình) */}
                  <button
                    type="button"
                    onClick={() => setShowCenterLyrics(!showCenterLyrics)}
                    className={`w-10 h-10 rounded-xl flex items-center justify-center transition-all cursor-pointer active:scale-95 ${
                      showCenterLyrics
                        ? "text-white shadow-[0_0_16px_rgba(236,72,153,0.85)] border border-pink-400/50"
                        : "bg-black/50 text-white/50 border border-white/20 hover:text-white hover:bg-black/70"
                    }`}
                    style={showCenterLyrics ? { background: "var(--theme-btn-gradient)" } : undefined}
                    title={showCenterLyrics ? "Ẩn lời bài hát" : "Hiện lời bài hát"}
                  >
                    <FileText className="w-5 h-5" />
                  </button>
                </div>

                {/* Row 2: Thanh tiến trình (Seekbar) */}
                <div className="flex flex-col gap-1 w-full">
                  <div className="relative w-full h-4 flex items-center group/seek touch-none">
                    <div className="absolute left-0 right-0 h-1.5 rounded-full bg-white/20" />
                    <div
                      className="absolute left-0 h-1.5 rounded-full"
                      style={{
                        width: `${duration ? Math.min(100, (currentTime / duration) * 100) : 0}%`,
                        background: "var(--theme-btn-gradient)",
                        boxShadow: "0 0 10px rgba(236,72,153,0.6)",
                      }}
                    />
                    <input
                      type="range"
                      min={0}
                      max={duration || 100}
                      step={0.5}
                      value={currentTime}
                      onChange={handleSeek}
                      disabled={!currentTrack}
                      className={`w-full h-full opacity-0 z-10 touch-none ${!currentTrack ? "cursor-not-allowed pointer-events-none" : "cursor-pointer"}`}
                    />
                    <div
                      className="absolute w-4 h-4 rounded-full bg-white shadow-md pointer-events-none transition-transform group-hover/seek:scale-125"
                      style={{
                        left: `calc(${duration ? Math.min(100, (currentTime / duration) * 100) : 0}% - 8px)`,
                        boxShadow: "0 0 12px var(--theme-accent)",
                      }}
                    />
                  </div>
                  <div className="flex justify-between text-[11px] text-white/70 font-mono px-0.5">
                    <span>{formatTime(currentTime)}</span>
                    <span>{formatTime(duration)}</span>
                  </div>
                </div>

                {/* Row 3: Hàng 7 nút điều khiển dưới cùng */}
                <div className="flex items-center justify-between px-2 w-full pt-1">
                  {/* 1. Volume */}
                  <div className="relative">
                    <button
                      type="button"
                      onClick={() => setShowVolumeSlider(!showVolumeSlider)}
                      className="p-2 rounded-xl bg-black/40 hover:bg-black/60 text-white/80 hover:text-white transition-all cursor-pointer"
                      title="Âm lượng"
                    >
                      {isMuted || volume === 0 ? <VolumeX className="w-5 h-5" /> : <Volume2 className="w-5 h-5" />}
                    </button>
                    {showVolumeSlider && (
                      <div className="absolute bottom-11 left-0 p-2.5 rounded-xl theme-menu-glass shadow-2xl flex items-center gap-2 z-30 border theme-accent-border/40">
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
                          className="w-24 h-1 bg-white/30 rounded appearance-none cursor-pointer theme-slider"
                        />
                        <span className="text-[10px] theme-accent-text font-mono font-bold">
                          {Math.round((isMuted ? 0 : volume) * 100)}%
                        </span>
                      </div>
                    )}
                  </div>

                  {/* 2. Repeat / Shuffle */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={cycleRepeatMode}
                    title={
                      !currentTrack
                        ? "Chưa chọn bài hát"
                        : repeatMode === "all"
                          ? "Lặp danh sách"
                          : repeatMode === "one"
                            ? "Lặp 1 bài"
                            : repeatMode === "shuffle"
                              ? "Phát ngẫu nhiên"
                              : "Tắt lặp lại"
                    }
                    className={`p-2 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : repeatMode === "all" || repeatMode === "one"
                          ? "theme-accent-text drop-shadow-[0_0_8px_var(--theme-accent)] cursor-pointer hover:scale-115 active:scale-90"
                          : repeatMode === "shuffle"
                            ? "theme-sec-accent-text drop-shadow-[0_0_8px_var(--theme-sec-accent)] cursor-pointer hover:scale-115 active:scale-90"
                            : "text-white/60 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
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

                  {/* 3. Prev */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={handlePrev}
                    title={!currentTrack ? "Chưa chọn bài hát" : "Bài trước"}
                    className={`p-2 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : "text-white/80 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    <SkipBack className="w-6 h-6 fill-current" />
                  </button>

                  {/* 4. Large Play/Pause in center */}
                  <button
                    type="button"
                    onClick={togglePlayPause}
                    disabled={!currentTrack || isLoading}
                    title={!currentTrack ? "Chưa chọn bài hát" : isPlaying ? "Tạm dừng" : "Phát"}
                    className={`w-13 h-13 sm:w-14 sm:h-14 rounded-full flex items-center justify-center text-white transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none shadow-none"
                        : "shadow-2xl hover:scale-110 active:scale-95 cursor-pointer"
                    }`}
                    style={{
                      background: "var(--theme-btn-gradient)",
                      boxShadow: !currentTrack ? "none" : "0 4px 24px rgba(236,72,153,0.6)",
                    }}
                  >
                    {isLoading ? (
                      <Loader2 className="w-7 h-7 animate-spin text-white" />
                    ) : isPlaying ? (
                      <Pause className="w-7 h-7 fill-current" />
                    ) : (
                      <Play className="w-7 h-7 fill-current translate-x-0.5" />
                    )}
                  </button>

                  {/* 5. Next */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={handleNext}
                    title={!currentTrack ? "Chưa chọn bài hát" : "Bài kế tiếp"}
                    className={`p-2 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : "text-white/80 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    <SkipForward className="w-6 h-6 fill-current" />
                  </button>

                  {/* 6. Sleep Timer */}
                  <div className="relative">
                    <button
                      type="button"
                      disabled={!currentTrack}
                      onClick={() => setShowSleepTimerMenu(!showSleepTimerMenu)}
                      title={
                        !currentTrack
                          ? "Chưa chọn bài hát"
                          : sleepTimerSecondsLeft
                            ? `Hẹn giờ tắt nhạc: còn ${Math.floor(sleepTimerSecondsLeft / 60)}p ${sleepTimerSecondsLeft % 60}s`
                            : "Hẹn giờ tắt nhạc"
                      }
                      className={`p-2 transition-all relative ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                          : sleepTimerSecondsLeft
                            ? "theme-accent-text font-bold cursor-pointer hover:scale-110 active:scale-90"
                            : "text-white/60 hover:text-white cursor-pointer hover:scale-110 active:scale-90"
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
                      <div className="absolute bottom-11 right-0 p-2 rounded-xl theme-menu-glass shadow-2xl flex flex-col gap-1 z-30 min-w-[130px] animate-fade-in border theme-accent-border/40">
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

                  {/* 7. Heart */}
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
                    className={`p-2 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : "cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    <Heart
                      className={`w-6 h-6 transition-transform ${
                        userSession?.isLoggedIn && libraryData.favorites.some((t) => t.id === currentTrack.id)
                          ? "fill-red-500 text-red-500 drop-shadow-[0_0_12px_rgba(239,68,68,0.9)]"
                          : "text-white/60 hover:text-red-400"
                      }`}
                    />
                  </button>
                </div>
              </div>
            </div>
          ) : selectedArtist ? (
            /* ===================================================================== */
            /* ARTIST DETAIL VIEW (Session giữa lấy data của đúng artist đó)         */
            /* ===================================================================== */
            <div className="flex flex-col h-full min-h-0 overflow-y-auto lg:overflow-hidden custom-scrollbar">
              {/* FIXED ARTIST HEADER CLUSTER (Back Bar + Hero Banner + Tags) */}
              <div className="lg:flex-shrink-0">
                {/* Back Bar */}
                <div className="flex items-center justify-between pb-2 sm:pb-3.5 border-b border-white/10 mb-3 sm:mb-5 lg:flex-shrink-0">
                  <button
                    type="button"
                    onClick={() => setSelectedArtist(null)}
                    className="px-3 py-1.5 sm:px-3.5 sm:py-1.5 rounded-xl theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-1.5 sm:gap-2 text-xs font-semibold group"
                  >
                    <ArrowLeft className="w-4 h-4 theme-accent-text group-hover:-translate-x-0.5 transition-transform" />
                    <span>Quay lại</span>
                  </button>
                  <span className="text-[11px] sm:text-xs text-white/50 font-mono">
                    Thông tin Nghệ sĩ
                  </span>
                </div>

                {/* Artist Hero Header Card */}
                <div className="w-full rounded-2xl sm:rounded-[24px] p-3.5 sm:p-5 lg:p-6 theme-card-bg shadow-xl sm:shadow-2xl mb-3 sm:mb-4 lg:mb-6 flex flex-col sm:flex-row items-center sm:items-start gap-3.5 sm:gap-5 lg:gap-6 border theme-accent-border relative overflow-hidden lg:flex-shrink-0">
                  <div className="absolute -top-12 -right-12 w-48 h-48 rounded-full blur-3xl opacity-20 pointer-events-none theme-accent-bg" />

                  {/* Big Avatar */}
                  <div className="relative w-20 h-20 sm:w-28 sm:h-28 lg:w-32 lg:h-32 rounded-full overflow-hidden shadow-xl flex-shrink-0 border-2 sm:border-[3px] theme-accent-border bg-black/40">
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
                    <div className="flex items-center gap-1.5 sm:gap-2 mb-0.5 sm:mb-1">
                      <span className="px-2 py-0.5 rounded-full text-[9px] sm:text-[10px] font-bold uppercase tracking-wider theme-badge flex items-center gap-1">
                        <Sparkles className="w-2.5 h-2.5 sm:w-3 sm:h-3 theme-accent-text" />
                        Nghệ sĩ chính thức
                      </span>
                      {selectedArtist.genre && (
                        <span className="text-[10px] sm:text-[11px] text-white/60">
                          • {selectedArtist.genre}
                        </span>
                      )}
                    </div>

                    <h1 className="text-lg sm:text-2xl lg:text-3xl font-extrabold text-white tracking-wide truncate max-w-full">
                      {selectedArtist.name}
                    </h1>

                    <p className="text-[11px] sm:text-xs text-white/60 mt-0.5 sm:mt-1 mb-2.5 sm:mb-4">
                      {selectedArtist.subscribers || "Nghệ sĩ trên YouTube Music"}
                      {artistTracks.length > 0 && ` • ${artistTracks.length} bài hát single`}
                      {artistPlaylists.length > 0 && ` • ${artistPlaylists.length} danh sách phát`}
                    </p>

                    {/* Play Actions */}
                    <div className="flex items-center gap-2 sm:gap-3 flex-wrap justify-center sm:justify-start">
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
                        className="px-3.5 py-1.5 sm:px-5 sm:py-2.5 rounded-full theme-btn-primary text-white font-bold text-xs sm:text-sm flex items-center gap-1.5 sm:gap-2 shadow-lg hover:scale-105 active:scale-95 transition-all cursor-pointer disabled:opacity-50"
                      >
                        <Play className="w-3.5 h-3.5 sm:w-4 sm:h-4 fill-current translate-x-0.5" />
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
                        className="px-3 py-1.5 sm:px-4 sm:py-2.5 rounded-full theme-badge hover:theme-btn-primary hover:text-white transition-all text-xs font-semibold flex items-center gap-1.5 sm:gap-2 cursor-pointer disabled:opacity-50"
                      >
                        <Shuffle className="w-3.5 h-3.5 sm:w-4 sm:h-4" />
                        <span>Phát ngẫu nhiên</span>
                      </button>
                    </div>
                  </div>
                </div>

                {/* Tag Selection Bar (Fixed together in this header cluster) */}
                <div className="py-1.5 sm:py-2.5 mb-2.5 sm:mb-4 border-b border-white/10 flex items-center gap-1.5 sm:gap-2 flex-wrap lg:flex-shrink-0">
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
                        className={`px-3 py-1 sm:px-4 sm:py-1.5 rounded-full text-[11px] sm:text-xs font-semibold transition-all flex items-center gap-1.5 sm:gap-2 cursor-pointer ${isActive
                          ? "theme-badge-active scale-[1.02]"
                          : "theme-badge hover:theme-accent-border"
                          }`}
                      >
                        <span>{tab.label}</span>
                        {typeof tab.count === "number" && tab.count > 0 && (
                          <span
                            className={`px-1.5 py-0.2 rounded-full text-[9px] sm:text-[10px] font-mono ${isActive ? "bg-white/20 text-white" : "bg-white/10 text-white/60"
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
              <div className="flex-1 lg:min-h-0 lg:overflow-y-auto pr-1 custom-scrollbar pb-28 lg:pb-8">
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
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                            {artistTracks.map((item, idx) => (
                              <TrackCard
                                key={item.id + idx}
                                index={idx}
                                track={item}
                                isCurrent={currentTrack?.id === item.id}
                                isPlaying={isPlaying}
                                isLoading={isLoading}
                                onPlay={() => {
                                  if (currentTrack?.id === item.id) {
                                    togglePlayPause();
                                  } else {
                                    setPlaylist(artistTracks);
                                    setQueueTracks(artistTracks);
                                    setCurrentIndex(idx);
                                    loadAndPlayTrack(item, true);
                                  }
                                }}
                              />
                            ))}
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
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                            {artistPlaylists.map((pl, idx) => (
                              <PlaylistCard
                                key={pl.id || idx}
                                index={idx}
                                playlist={pl}
                                isLoading={loadingPlaylistId === pl.id}
                                onClick={() => playPlaylistItem(pl)}
                              />
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
            <div className="flex flex-col h-full min-h-0 overflow-y-auto lg:overflow-hidden custom-scrollbar">
              {/* FIXED SEARCH HEADER CLUSTER (Back Bar + Navigation Tabs + Top Match) */}
              <div className="lg:flex-shrink-0">
                {/* Header with Back button, search title and result count */}
                <div className="flex items-center justify-between pb-2 sm:pb-3.5 border-b border-white/10 mb-2.5 sm:mb-4 lg:flex-shrink-0">
                  <div className="flex items-center gap-2 sm:gap-3 min-w-0">
                    <button
                      type="button"
                      onClick={() => {
                        setIsSearchOpen(false);
                        setTopSearchText("");
                        setSearchQueryTitle("");
                        setActiveSearchTab("all");
                      }}
                      className="px-2.5 py-1.5 sm:px-3 sm:py-1.5 rounded-xl theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-1.5 sm:gap-2 text-xs font-semibold group flex-shrink-0"
                    >
                      <ArrowLeft className="w-4 h-4 theme-accent-text group-hover:-translate-x-0.5 transition-transform" />
                      <span>Quay lại</span>
                    </button>
                    <h2 className="text-base sm:text-[20px] font-bold text-white tracking-wide truncate">
                      {searchQueryTitle ? `Kết quả: "${searchQueryTitle}"` : "Kết quả tìm kiếm"}
                    </h2>
                  </div>
                  {searchResults.length > 0 && (
                    <span className="text-[11px] sm:text-xs text-purple-200/60 font-mono flex-shrink-0 ml-2">
                      {searchResults.length} bài hát
                    </span>
                  )}
                </div>

                {/* Navigation Tabs */}
                <div className="py-1.5 sm:py-2.5 mb-2.5 sm:mb-4 border-b border-white/10 flex items-center gap-1.5 sm:gap-2 flex-wrap lg:flex-shrink-0">
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
                        className={`px-3 py-1 sm:px-4 sm:py-1.5 rounded-full text-[11px] sm:text-xs font-semibold transition-all flex items-center gap-1.5 sm:gap-2 cursor-pointer ${isActive
                          ? "theme-badge-active scale-[1.02]"
                          : "bg-white/5 hover:bg-white/10 text-white/70 hover:text-white border border-white/5"
                          }`}
                      >
                        <span>{tab.label}</span>
                        {typeof tab.count === "number" && tab.count > 0 && (
                          <span
                            className={`px-1.5 py-0.2 rounded-full text-[9px] sm:text-[10px] font-mono ${isActive ? "bg-white/20 text-white" : "bg-white/10 text-white/60"
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
                    <div className="w-full mb-3 sm:mb-4 lg:flex-shrink-0">
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
                        className="w-full rounded-2xl sm:rounded-[20px] p-2.5 sm:p-4 theme-card-bg shadow-lg sm:shadow-xl transition-all flex items-center justify-between gap-3 sm:gap-4 group cursor-pointer item-slide-up"
                      >
                        <div className="flex items-center gap-2.5 sm:gap-4 min-w-0">
                          {/* Artwork */}
                          <div className="relative w-16 h-16 sm:w-36 sm:h-24 rounded-xl overflow-hidden shadow-md flex-shrink-0 border border-white/10 bg-purple-950">
                            <img
                              src={top.thumbnail || DEFAULT_TRACK_ART}
                              alt={top.title}
                              onError={(e) => {
                                e.currentTarget.src = DEFAULT_TRACK_ART;
                              }}
                              className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                            />
                            {(top.duration > 0 || !!top.durationText) && (
                              <span className="absolute bottom-1 right-1 px-1.5 py-0.5 rounded bg-black/80 text-[9px] sm:text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow">
                                {top.isMusic !== false && (
                                  <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
                                )}
                                <span>{top.durationText || formatTime(top.duration)}</span>
                              </span>
                            )}
                          </div>

                          {/* Track Info */}
                          <div className="min-w-0 flex flex-col justify-center">
                            <span className="text-[9px] sm:text-[10px] uppercase font-bold tracking-widest theme-accent-text mb-0.5 sm:mb-1 flex items-center gap-1.5">
                              <Sparkles className="w-3 h-3 theme-accent-text" />
                              BÀI HÁT PHÙ HỢP NHẤT
                            </span>
                            <h3 className="text-sm sm:text-lg font-bold text-white tracking-wide truncate group-hover:theme-accent-text transition-colors">
                              {top.title}
                            </h3>
                            <p className="text-[11px] sm:text-xs text-purple-200/70 truncate mt-0.5 sm:mt-1">
                              {top.author} •{" "}
                              {top.durationText ||
                                (top.duration > 0
                                  ? formatTime(top.duration)
                                  : "YouTube Music")}
                            </p>
                          </div>
                        </div>

                        {/* Play Action Button */}
                        <div className="flex items-center gap-2 sm:gap-3 flex-shrink-0">
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
                            className="p-2 sm:px-5 sm:py-2.5 rounded-full theme-btn-primary text-white font-bold text-xs sm:text-sm flex items-center gap-1.5 sm:gap-2 shadow-lg hover:scale-105 active:scale-95 transition-all cursor-pointer"
                          >
                            {isCurrentPlaying && isPlaying ? (
                              <>
                                <Pause className="w-4 h-4 fill-current" />
                                <span className="hidden sm:inline">Tạm dừng</span>
                              </>
                            ) : (
                              <>
                                <Play className="w-4 h-4 fill-current translate-x-0.5" />
                                <span className="hidden sm:inline">Phát ngay</span>
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
              <div className="flex-1 lg:min-h-0 lg:overflow-y-auto pr-1 custom-scrollbar pb-28 lg:pb-8">
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
                            <div className="flex items-start gap-3 sm:gap-7 overflow-x-auto pb-2 custom-scrollbar">
                              {searchStructured.artists.map((artist, idx) => (
                                <div
                                  key={artist.id || artist.name}
                                  className="flex-shrink-0 w-[110px] sm:w-[140px] lg:w-[150px]"
                                >
                                  <ArtistCard
                                    index={idx}
                                    artist={{
                                      id: artist.id,
                                      name: artist.name,
                                      avatar: artist.thumbnail,
                                      subscribers: artist.subscribers,
                                    }}
                                    onClick={() =>
                                      openArtistDetail({
                                        id: artist.id,
                                        name: artist.name,
                                        avatar: artist.thumbnail,
                                        subscribers: artist.subscribers,
                                      })
                                    }
                                  />
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
                            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4">
                              {searchStructured.playlists.slice(0, 6).map((pl, idx) => (
                                <PlaylistCard
                                  key={pl.id || pl.title}
                                  index={idx}
                                  playlist={pl}
                                  isLoading={loadingPlaylistId === pl.id}
                                  onClick={() => playPlaylistItem(pl)}
                                />
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
                            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                              {searchResults.slice(0, 12).map((item, idx) => (
                                <TrackCard
                                  key={item.id}
                                  index={idx}
                                  track={item}
                                  isCurrent={currentTrack?.id === item.id}
                                  isPlaying={isPlaying}
                                  isLoading={isLoading}
                                  onPlay={() => playSearchResult(item)}
                                />
                              ))}
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
                        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                          {searchResults.map((item, idx) => (
                            <TrackCard
                              key={item.id}
                              index={idx}
                              track={item}
                              isCurrent={currentTrack?.id === item.id}
                              isPlaying={isPlaying}
                              isLoading={isLoading}
                              onPlay={() => playSearchResult(item)}
                            />
                          ))}
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
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4 lg:gap-6">
                            {searchStructured.artists.map((artist, idx) => (
                              <ArtistCard
                                key={artist.id || artist.name}
                                index={idx}
                                artist={{
                                  id: artist.id,
                                  name: artist.name,
                                  avatar: artist.thumbnail,
                                  subscribers: artist.subscribers,
                                }}
                                onClick={() =>
                                  openArtistDetail({
                                    id: artist.id,
                                    name: artist.name,
                                    avatar: artist.thumbnail,
                                    subscribers: artist.subscribers,
                                  })
                                }
                              />
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
                          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4">
                            {searchStructured.playlists.map((pl, idx) => (
                              <PlaylistCard
                                key={pl.id || pl.title}
                                index={idx}
                                playlist={pl}
                                isLoading={loadingPlaylistId === pl.id}
                                onClick={() => playPlaylistItem(pl)}
                              />
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
            <div className="flex flex-col gap-5 sm:gap-8 flex-1 min-h-0 overflow-y-auto pr-1 custom-scrollbar pb-28 lg:pb-6">
              {/* Section 1: Nhạc Trending */}
              <div>
                <div className="flex items-center justify-between mb-3 sm:mb-3.5">
                  <div className="flex items-center gap-2 sm:gap-2.5">
                    <Sparkles className="w-4 h-4 sm:w-5 sm:h-5 theme-accent-text" />
                    <h2 className="text-base sm:text-[20px] font-bold text-white tracking-wide">
                      Nhạc Trending
                    </h2>
                    {isHomeLoading && (
                      <div className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full theme-badge animate-pulse font-medium">
                        <Loader2 className="w-3 h-3 animate-spin theme-accent-text" />
                        <span>Đang tải...</span>
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-1.5 sm:gap-2">
                    <button
                      type="button"
                      onClick={() => refreshHomeFeed(true)}
                      disabled={isHomeLoading}
                      title="Làm mới khám phá (đổi bài hát ngẫu nhiên)"
                      className="text-xs font-semibold px-2.5 sm:px-3 py-1.5 rounded-full theme-badge hover:theme-btn-primary hover:text-white transition-all cursor-pointer flex items-center gap-1.5 disabled:opacity-50"
                    >
                      <RefreshCw className={`w-3.5 h-3.5 ${isHomeLoading ? "animate-spin theme-accent-text" : ""}`} />
                      <span className="hidden sm:inline">Làm mới</span>
                    </button>
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
                </div>

                {isHomeLoading ? (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                    {Array.from({ length: 6 }).map((_, idx) => (
                      <div key={idx} className="w-full flex flex-col animate-pulse">
                        <div className="relative w-full aspect-square rounded-xl sm:rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-purple-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                          <Sparkles className="w-8 h-8 text-white/20 animate-pulse" />
                        </div>
                        <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                        <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                    {trendingTracks.slice(0, 12).map((item, idx) => (
                      <TrackCard
                        key={item.id + "-" + idx}
                        index={idx}
                        track={item}
                        isCurrent={currentTrack?.id === item.id}
                        isPlaying={isPlaying}
                        isLoading={isLoading}
                        onPlay={() => handlePlaySongItem(item, trendingTracks, idx)}
                      />
                    ))}
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
                  <div className="flex items-center gap-1.5 sm:gap-2">
                    <button
                      type="button"
                      onClick={() => refreshHomeFeed(true)}
                      disabled={isHomeLoading}
                      title="Làm mới bài hát mới ngẫu nhiên"
                      className="text-xs font-semibold px-2.5 sm:px-3 py-1.5 rounded-full bg-orange-500/10 hover:bg-orange-500 text-orange-400 hover:text-white border border-orange-500/30 transition-all cursor-pointer flex items-center gap-1.5 disabled:opacity-50"
                    >
                      <RefreshCw className={`w-3.5 h-3.5 ${isHomeLoading ? "animate-spin text-orange-400" : ""}`} />
                      <span className="hidden sm:inline">Làm mới</span>
                    </button>
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
                </div>

                {isHomeLoading ? (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                    {Array.from({ length: 6 }).map((_, idx) => (
                      <div key={idx} className="w-full flex flex-col animate-pulse">
                        <div className="relative w-full aspect-square rounded-xl sm:rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-orange-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                          <Flame className="w-8 h-8 text-orange-400/20 animate-pulse" />
                        </div>
                        <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                        <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                    {newReleaseTracks.slice(0, 12).map((item, idx) => (
                      <TrackCard
                        key={item.id + "-" + idx}
                        index={idx}
                        track={item}
                        isCurrent={currentTrack?.id === item.id}
                        isPlaying={isPlaying}
                        isLoading={isLoading}
                        onPlay={() => handlePlaySongItem(item, newReleaseTracks, idx)}
                      />
                    ))}
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
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
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
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                    {homeArtists.slice(0, 6).map((artist, idx) => (
                      <ArtistCard
                        key={artist.id || artist.name || idx}
                        index={idx}
                        artist={{
                          id: artist.id,
                          name: artist.name,
                          avatar: artist.avatar || artist.thumbnail,
                          genre: artist.genre,
                          subscribers: artist.subscribers,
                        }}
                        onClick={() =>
                          openArtistDetail({
                            id: artist.id,
                            name: artist.name,
                            avatar: artist.avatar || artist.thumbnail,
                            genre: artist.genre,
                            subscribers: artist.subscribers,
                          })
                        }
                      />
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
                        {recentTracksToDisplay.map((item, idx) => (
                          <TrackCard
                            key={item.id + "-" + idx}
                            index={idx}
                            track={item}
                            isCurrent={currentTrack?.id === item.id}
                            isPlaying={isPlaying}
                            isLoading={isLoading}
                            onPlay={() => handlePlaySongItem(item, recentTracksToDisplay, idx)}
                          />
                        ))}
                      </div>
                    ) : (
                      <div className="py-6 text-center text-white/40 text-xs italic bg-white/5 rounded-2xl border border-white/5">
                        Chưa có bài hát nào gần đây. Hãy chọn hoặc tìm kiếm bài hát để phát.
                      </div>
                    )}
                  </div>

                  {/* Section 5: Có thể bạn sẽ thích (Taste Profile Personalized AI Recommendations) */}
                  <div className="flex flex-col gap-3.5">
                    {/* Header Bar */}
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                      <div className="flex items-center gap-2.5 flex-wrap">
                        <div className="w-8 h-8 rounded-xl bg-gradient-to-br from-purple-500/20 via-pink-500/20 to-rose-500/20 border border-white/10 flex items-center justify-center">
                          <Sparkles className="w-4 h-4 theme-accent-text" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <h2 className="text-[19px] sm:text-[20px] font-bold text-white tracking-wide">
                              Có thể bạn sẽ thích
                            </h2>
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-mono bg-purple-500/20 text-purple-300 border border-purple-500/30">
                              AI Gợi ý
                            </span>
                          </div>
                          <p className="text-[11px] text-white/50">
                            Cá nhân hoá từ gu nghệ sĩ, thể loại, ngôn ngữ & phong cách trong Yêu thích
                          </p>
                        </div>
                      </div>

                      {/* Action buttons: Refresh + Play All */}
                      <div className="flex items-center gap-2 self-start sm:self-auto">
                        <button
                          type="button"
                          onClick={() => fetchRecommendations(selectedTasteTagKey, true)}
                          disabled={isRecommendationsLoading}
                          className="px-3 py-1.5 rounded-xl theme-badge hover:theme-accent-border text-xs font-semibold text-white/80 hover:text-white transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                          title="Làm mới danh sách gợi ý"
                        >
                          <RefreshCw className={`w-3.5 h-3.5 ${isRecommendationsLoading ? "animate-spin theme-accent-text" : ""}`} />
                          <span className="hidden sm:inline">Làm mới</span>
                        </button>

                        <button
                          type="button"
                          disabled={recommendedTracksToDisplay.length === 0}
                          onClick={() => {
                            if (recommendedTracksToDisplay.length > 0) {
                              setPlaylist(recommendedTracksToDisplay);
                              setQueueTracks(recommendedTracksToDisplay);
                              setCurrentIndex(0);
                              loadAndPlayTrack(recommendedTracksToDisplay[0], true);
                            }
                          }}
                          className="px-3.5 py-1.5 rounded-xl theme-btn-primary text-xs font-semibold text-white shadow-lg flex items-center gap-1.5 cursor-pointer disabled:opacity-50 transition-all hover:scale-[1.02] active:scale-95"
                        >
                          <Play className="w-3.5 h-3.5 fill-current" />
                          <span>Phát tất cả</span>
                        </button>
                      </div>
                    </div>

                    {/* Taste Profile Summary Banner (if user has favorites analyzed) */}
                    {libraryData.favorites.length > 0 && (
                      <div className="w-full rounded-2xl p-3 sm:p-3.5 bg-gradient-to-r from-purple-950/40 via-purple-900/20 to-black/30 border border-purple-500/20 backdrop-blur-sm flex flex-col gap-2">
                        <div className="flex items-center justify-between text-xs text-purple-200/90 font-medium">
                          <span className="flex items-center gap-1.5">
                            <Heart className="w-3.5 h-3.5 text-rose-400 fill-current" />
                            <span>Hồ sơ gu nhạc của bạn ({libraryData.favorites.length} bài đã thích)</span>
                          </span>
                          {tasteProfile.summary.countriesText && (
                            <span className="text-[11px] text-white/50 font-mono">
                              🌏 {tasteProfile.summary.countriesText}
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-2 flex-wrap text-[11px]">
                          {tasteProfile.summary.artistsText && (
                            <span className="px-2.5 py-0.5 rounded-lg bg-white/5 border border-white/10 text-white/80">
                              🎙️ <strong className="text-white">{tasteProfile.summary.artistsText}</strong>
                            </span>
                          )}
                          {tasteProfile.summary.genresText && (
                            <span className="px-2.5 py-0.5 rounded-lg bg-white/5 border border-white/10 text-white/80">
                              🎵 {tasteProfile.summary.genresText}
                            </span>
                          )}
                          {tasteProfile.summary.stylesText && (
                            <span className="px-2.5 py-0.5 rounded-lg bg-white/5 border border-white/10 text-white/80">
                              ✨ {tasteProfile.summary.stylesText}
                            </span>
                          )}
                        </div>
                      </div>
                    )}

                    {/* Taste Filter Pills Carousel */}
                    {tasteProfile.tags && tasteProfile.tags.length > 0 && (
                      <div className="flex items-center gap-2 overflow-x-auto pb-1 scrollbar-none">
                        {tasteProfile.tags.map((tag) => {
                          const isSelected = selectedTasteTagKey === tag.key;
                          return (
                            <button
                              key={tag.key}
                              type="button"
                              onClick={() => {
                                setSelectedTasteTagKey(tag.key);
                                fetchRecommendations(tag.key, true);
                              }}
                              className={`px-3 py-1.5 rounded-full text-xs font-medium whitespace-nowrap transition-all cursor-pointer flex items-center gap-1.5 ${isSelected
                                ? "theme-badge-active scale-[1.02] shadow-md shadow-purple-500/20 font-semibold"
                                : "theme-badge hover:theme-accent-border text-white/70 hover:text-white"
                                }`}
                            >
                              <span>{tag.label}</span>
                            </button>
                          );
                        })}
                      </div>
                    )}

                    {/* Tracks Grid */}
                    {isRecommendationsLoading ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                        {Array.from({ length: 6 }).map((_, idx) => (
                          <div key={idx} className="w-full flex flex-col animate-pulse">
                            <div className="relative w-full aspect-square rounded-xl sm:rounded-[18px] xl:rounded-[22px] overflow-hidden bg-gradient-to-br from-purple-900/30 via-rose-950/20 to-black/40 border border-white/5 flex items-center justify-center">
                              <Sparkles className="w-8 h-8 text-purple-400/20 animate-pulse" />
                            </div>
                            <div className="h-3.5 w-4/5 rounded-md bg-white/10 mt-2.5 animate-pulse" />
                            <div className="h-2.5 w-1/2 rounded-md bg-white/5 mt-1.5 animate-pulse" />
                          </div>
                        ))}
                      </div>
                    ) : recommendedTracksToDisplay.length > 0 ? (
                      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2.5 sm:gap-3.5 lg:gap-4 w-full">
                        {recommendedTracksToDisplay.map((item, idx) => (
                          <TrackCard
                            key={item.id + "-" + idx}
                            index={idx}
                            track={item}
                            isCurrent={currentTrack?.id === item.id}
                            isPlaying={isPlaying}
                            isLoading={isLoading}
                            onPlay={() => handlePlaySongItem(item, recommendedTracksToDisplay, idx)}
                          />
                        ))}
                      </div>
                    ) : (
                      <div className="py-8 px-4 text-center bg-white/5 rounded-2xl border border-white/5 flex flex-col items-center justify-center gap-2">
                        <Heart className="w-8 h-8 text-rose-400/50 mb-1" />
                        <p className="text-sm text-white/70 font-medium">Chưa có bài hát gợi ý phù hợp</p>
                        <p className="text-xs text-white/40 max-w-sm">
                          Hãy thả tim ❤️ vào các bài hát bạn yêu thích để hệ thống phân tích thể loại, ngôn ngữ và nghệ sĩ yêu thích của bạn nhé!
                        </p>
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
          className={`${mobileTab === "player" ? "flex w-full h-full" : "hidden"
            } lg:flex lg:w-[310px] xl:w-[340px] flex-shrink-0 figma-panel flex-col min-h-0 relative z-20 overflow-y-auto lg:overflow-visible p-3 sm:p-4 lg:px-[18px] lg:pt-[26px] pb-3 lg:pb-[20px]`}
          style={{ backdropFilter: "none", WebkitBackdropFilter: "none" }}
        >
          {/* Mobile Collapse / Back Header */}
          <div className="lg:hidden flex items-center justify-between pb-2 mb-2 border-b border-white/10 flex-shrink-0">
            <button
              type="button"
              onClick={() => setMobileTab("explore")}
              className="flex items-center gap-1.5 text-xs text-white/70 hover:text-white cursor-pointer group"
            >
              <ChevronDown className="w-4 h-4 group-hover:translate-y-0.5 transition-transform" />
              <span>Thu nhỏ trình phát</span>
            </button>
            <span className="text-[11px] font-bold theme-accent-text uppercase tracking-wider">Đang phát</span>
          </div>

          {/* ========================================================================= */}
          {/* 1. MOBILE TAB "ĐANG PHÁT" (EXACT PC MINI APP MODE, lg:hidden)             */}
          {/* ========================================================================= */}
          <div className="lg:hidden flex flex-col w-full h-full flex-1 min-h-0 gap-2.5 sm:gap-3">
            {/* TOP CARD: PLAYER CONTROLS + LIVE LYRICS SPLIT */}
            <div className="w-full rounded-[20px] p-3 sm:p-3.5 theme-card-bg shadow-xl border theme-accent-border/40 flex flex-col min-[380px]:flex-row gap-3 sm:gap-3.5 items-start relative overflow-hidden flex-shrink-0">
              {/* Left Column: Artwork with Player Controls Inside (Square 1:1, Rounded Corners) */}
              <div className="w-[170px] h-[170px] min-[420px]:w-[185px] min-[420px]:h-[185px] sm:w-[195px] sm:h-[195px] aspect-square rounded-[18px] sm:rounded-[22px] relative overflow-hidden flex-shrink-0 self-start shadow-xl bg-black/60 border border-white/10 select-none mx-auto min-[380px]:mx-0">
                {/* Artwork Image */}
                {!currentTrack ? (
                  <div className="w-full h-full flex flex-col items-center justify-center bg-gradient-to-br from-[#240a50] to-[#120324] relative overflow-hidden">
                    <img src="./logo_icon.png" alt="Kahoii Music" className="w-10 h-10 object-contain drop-shadow mb-1" />
                    <span className="text-[11px] font-bold text-white/80">Kahoii Music</span>
                  </div>
                ) : (
                  <img
                    src={getTrackThumbnail(currentTrack, "hd")}
                    alt={currentTrack.title || ""}
                    onError={(e) => {
                      const target = e.currentTarget;
                      if (target.src.includes("hq720.jpg") && currentTrack?.id) {
                        target.src = `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg`;
                      } else if (!target.src.includes(DEFAULT_TRACK_ART)) {
                        target.src = DEFAULT_TRACK_ART;
                      }
                    }}
                    className="w-full h-full object-cover scale-[1.10] select-none"
                  />
                )}

                {/* Dark gradient overlay */}
                <div className="absolute inset-0 bg-gradient-to-t from-black/90 via-black/45 to-transparent pointer-events-none" />

                {/* Top-left Menu & Volume Controls */}
                <div className="absolute top-2.5 left-2.5 z-30 flex items-center gap-1.5" ref={playerMenuRef}>
                  <button
                    type="button"
                    onClick={() => {
                      if (!currentTrack) {
                        showToast("Vui lòng phát một bài hát trước khi tùy chọn!", "warning");
                        return;
                      }
                      setShowPlayerMenu(!showPlayerMenu);
                    }}
                    className="w-7 h-7 rounded-lg bg-black/50 hover:bg-black/75 backdrop-blur-md flex items-center justify-center text-white/90 hover:text-white cursor-pointer transition-all border border-white/15 shadow-sm"
                    title="Tùy chọn bài hát"
                  >
                    <Menu className="w-4 h-4" />
                  </button>

                  <button
                    type="button"
                    onClick={() => setIsMuted(!isMuted)}
                    title={isMuted ? "Bật âm thanh" : "Tắt tiếng"}
                    className="w-7 h-7 rounded-lg bg-black/50 hover:bg-black/75 backdrop-blur-md flex items-center justify-center text-white/90 hover:text-white cursor-pointer transition-all border border-white/15 shadow-sm"
                  >
                    {isMuted ? (
                      <VolumeX className="w-4 h-4 text-red-400" />
                    ) : (
                      <Volume2 className="w-4 h-4" />
                    )}
                  </button>

                  {/* Menu Popup */}
                  {showPlayerMenu && currentTrack && (
                    <div className="absolute top-8 left-0 z-50 theme-menu-glass rounded-xl shadow-2xl p-1.5 min-w-[190px] flex flex-col gap-1 text-xs animate-fade-in border theme-accent-border/40" onClick={(e) => e.stopPropagation()}>
                      <button
                        type="button"
                        onClick={() => setShowLibrarySubmenu(!showLibrarySubmenu)}
                        className={`flex items-center justify-between w-full px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer text-left ${showLibrarySubmenu ? "bg-white/15 theme-accent-text font-bold" : "hover:bg-white/10 text-white/90"
                          }`}
                      >
                        <span className="flex items-center gap-2">
                          <FolderPlus className="w-3.5 h-3.5 theme-accent-text" />
                          <span>Lưu vào playlist</span>
                        </span>
                        <ChevronLeft className="w-3.5 h-3.5 text-white/40" />
                      </button>

                      {showLibrarySubmenu && (
                        <div className="flex flex-col gap-1 pl-2 pr-1 py-1 bg-black/40 rounded-lg max-h-[120px] overflow-y-auto custom-scrollbar border border-white/5">
                          {libraryData.playlists.map((pl) => {
                            const hasSong = pl.items?.some((it: any) => it.id === currentTrack.id);
                            return (
                              <button
                                key={pl.id}
                                type="button"
                                onClick={() => handleToggleTrackInPlaylist(pl.id)}
                                className="flex items-center justify-between px-2 py-1 rounded text-[11px] hover:bg-white/10 text-white/80 transition-colors text-left"
                              >
                                <span className="truncate pr-1">{pl.title}</span>
                                {hasSong && <Check className="w-3 h-3 text-emerald-400 flex-shrink-0" />}
                              </button>
                            );
                          })}
                        </div>
                      )}

                      <button
                        type="button"
                        disabled={savingOfflineTrackId === currentTrack.id}
                        onClick={() => handleSaveOfflineTrack(currentTrack)}
                        className="flex items-center justify-between w-full px-2.5 py-1.5 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                      >
                        <span className="flex items-center gap-2">
                          {savingOfflineTrackId === currentTrack.id ? (
                            <Loader2 className="w-3.5 h-3.5 theme-accent-text animate-spin" />
                          ) : (
                            <HardDriveDownload className="w-3.5 h-3.5 theme-accent-text" />
                          )}
                          <span className="text-[11px]">
                            {savingOfflineTrackId === currentTrack.id
                              ? "Đang lưu..."
                              : offlineTrackIds.has(currentTrack.id)
                                ? "Đã lưu offline"
                                : "Lưu nghe offline"}
                          </span>
                        </span>
                        {offlineTrackIds.has(currentTrack.id) && (
                          <Check className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
                        )}
                      </button>
                    </div>
                  )}
                </div>

                {/* Top-right Favorite Heart Button */}
                <button
                  type="button"
                  disabled={!currentTrack}
                  onClick={toggleCurrentFavorite}
                  className={`absolute top-2.5 right-2.5 w-7 h-7 rounded-lg backdrop-blur-md flex items-center justify-center z-30 transition-all border border-white/15 shadow-sm ${
                    !currentTrack
                      ? "opacity-35 cursor-not-allowed pointer-events-none bg-black/20"
                      : "bg-black/50 hover:bg-black/75 cursor-pointer"
                  }`}
                  title={!currentTrack ? "Chưa chọn bài hát" : "Yêu thích"}
                >
                  <Heart
                    className={`w-4 h-4 transition-transform ${userSession?.isLoggedIn && currentTrack && libraryData.favorites.some((t) => t.id === currentTrack.id)
                      ? "fill-red-500 text-red-500 scale-110 drop-shadow-[0_0_8px_rgba(239,68,68,0.9)]"
                      : "text-white/70 hover:text-red-400"
                      }`}
                  />
                </button>

                {/* Seekbar Progress Line with Timestamp Indicators (sleek & compact right above controls, matching PC App) */}
                <div className="absolute left-2.5 right-2.5 bottom-[31px] flex flex-col gap-0.5 z-20">
                  <div className="relative w-full h-3.5 flex items-center group/seek touch-none">
                    <div className="absolute left-0 right-0 h-1 rounded-full bg-white/25 overflow-hidden" />
                    <div
                      className="absolute left-0 h-1 rounded-full"
                      style={{
                        width: `${duration ? Math.min(100, (currentTime / duration) * 100) : 0}%`,
                        background: "var(--theme-btn-gradient)",
                      }}
                    />
                    <div
                      className="absolute w-2.5 h-2.5 rounded-full bg-white shadow-md pointer-events-none transition-transform"
                      style={{
                        left: `calc(${duration ? Math.min(100, (currentTime / duration) * 100) : 0}% - 5px)`,
                        boxShadow: "0 0 6px var(--theme-accent)",
                      }}
                    />
                    <input
                      type="range"
                      min={0}
                      max={duration || 100}
                      step={0.5}
                      value={currentTime}
                      onChange={handleSeek}
                      disabled={!currentTrack}
                      className={`w-full h-full opacity-0 z-10 touch-none ${!currentTrack ? "cursor-not-allowed pointer-events-none" : "cursor-pointer"}`}
                    />
                  </div>
                  <div className="flex justify-between text-[8.5px] text-white/70 font-mono px-0.5 -mt-1 font-medium select-none drop-shadow">
                    <span>{formatTime(currentTime)}</span>
                    <span>{formatTime(duration)}</span>
                  </div>
                </div>

                {/* 5 Playback Control Buttons Row */}
                <div className="absolute bottom-1 left-2 right-2 flex items-center justify-between px-0.5 z-20">
                  {/* Loop / Shuffle */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={cycleRepeatMode}
                    title={!currentTrack ? "Chưa chọn bài hát" : "Chế độ lặp"}
                    className={`p-1 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : repeatMode === "all" || repeatMode === "one"
                          ? "theme-accent-text cursor-pointer hover:scale-115 active:scale-90"
                          : repeatMode === "shuffle"
                            ? "theme-sec-accent-text cursor-pointer hover:scale-115 active:scale-90"
                            : "text-white/60 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    {repeatMode === "one" ? (
                      <Repeat1 className="w-4 h-4" />
                    ) : repeatMode === "shuffle" ? (
                      <Shuffle className="w-4 h-4" />
                    ) : (
                      <Repeat className="w-4 h-4" />
                    )}
                  </button>

                  {/* Prev */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={handlePrev}
                    title={!currentTrack ? "Chưa chọn bài hát" : "Bài trước"}
                    className={`p-1 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : "text-white/80 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    <SkipBack className="w-4.5 h-4.5 fill-current" />
                  </button>

                  {/* Play / Pause (Circular Gradient Button) */}
                  <button
                    type="button"
                    onClick={togglePlayPause}
                    disabled={!currentTrack || isLoading}
                    title={!currentTrack ? "Chưa chọn bài hát" : isPlaying ? "Tạm dừng" : "Phát"}
                    className={`w-8 h-8 min-[420px]:w-9 min-[420px]:h-9 rounded-full flex items-center justify-center text-white transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none shadow-none"
                        : "shadow-lg hover:scale-105 active:scale-95 cursor-pointer"
                    }`}
                    style={{ background: "var(--theme-btn-gradient)" }}
                  >
                    {isLoading ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : isPlaying ? (
                      <Pause className="w-4 h-4 fill-current" />
                    ) : (
                      <Play className="w-4 h-4 fill-current translate-x-0.5" />
                    )}
                  </button>

                  {/* Next */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={handleNext}
                    title={!currentTrack ? "Chưa chọn bài hát" : "Bài tiếp theo"}
                    className={`p-1 transition-all ${
                      !currentTrack
                        ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                        : "text-white/80 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
                    }`}
                  >
                    <SkipForward className="w-4.5 h-4.5 fill-current" />
                  </button>

                  {/* Timer */}
                  <div className="relative">
                    <button
                      type="button"
                      disabled={!currentTrack}
                      onClick={() => setShowSleepTimerMenu(!showSleepTimerMenu)}
                      title={!currentTrack ? "Chưa chọn bài hát" : "Hẹn giờ tắt"}
                      className={`p-1 transition-all relative ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                          : sleepTimerSecondsLeft
                            ? "theme-accent-text font-bold cursor-pointer hover:scale-110 active:scale-90"
                            : "text-white/60 hover:text-white cursor-pointer hover:scale-110 active:scale-90"
                      }`}
                    >
                      <Clock className="w-4 h-4" />
                      {sleepTimerMinutes && (
                        <span className="absolute -top-1 -right-1 px-1 rounded-full theme-accent-bg text-[7px] text-white font-mono font-bold animate-pulse">
                          {sleepTimerMinutes}m
                        </span>
                      )}
                    </button>

                    {showSleepTimerMenu && (
                      <div className="absolute bottom-8 right-0 p-2 rounded-xl theme-menu-glass shadow-2xl flex flex-col gap-1 z-50 min-w-[125px] animate-fade-in border theme-accent-border/40" onClick={(e) => e.stopPropagation()}>
                        <span className="text-[10px] uppercase font-bold theme-accent-text tracking-wider px-2 py-0.5">
                          Hẹn giờ tắt
                        </span>
                        {[15, 30, 45, 60].map((m) => (
                          <button
                            key={m}
                            type="button"
                            onClick={() => startSleepTimer(m)}
                            className={`text-left text-xs px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer ${sleepTimerMinutes === m ? "theme-badge-active" : "hover:bg-white/10 text-white/80"
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

              {/* Right Column: Song Metadata, Toolbar Pills & Live Lyrics Card (Height aligned with Thumbnail) */}
              <div className="w-full min-[380px]:w-auto min-[380px]:h-[170px] min-[420px]:h-[185px] sm:h-[195px] flex-1 min-w-0 flex flex-col justify-between">
                {/* Title & Artist */}
                <div className="min-w-0">
                  <h3
                    className="text-base min-[400px]:text-lg sm:text-xl font-black text-white tracking-tight truncate leading-snug"
                    title={currentTrack?.title}
                  >
                    {currentTrack?.title || "Chưa phát hiện Audio"}
                  </h3>
                  <p className="text-xs min-[400px]:text-[13px] text-white/70 font-semibold truncate mt-0.5">
                    {currentTrack ? formatTrackArtist(currentTrack.title, currentTrack.author) : "Kahoii Music"}
                  </p>
                </div>

                {/* Toolbar Action Pills (Directly matching PC Mini App) */}
                <div className="flex items-center gap-1.5 sm:gap-2 mt-1.5 flex-wrap">
                  {/* Refresh / Re-sync button */}
                  <button
                    type="button"
                    onClick={handleReloadLyrics}
                    title="Tải lại / đồng bộ lời bài hát"
                    className="w-6 h-6 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white/80 hover:text-white transition-all cursor-pointer shadow-sm"
                  >
                    <RefreshCw className="w-3 h-3" />
                  </button>

                  {/* Font size button: Aa 1 / Aa 2 / Aa 3 */}
                  <button
                    type="button"
                    onClick={() => setMiniLyricFontSize((f) => (f % 3) + 1)}
                    title="Đổi kích thước chữ lời bài hát"
                    className="px-2.5 py-0.5 rounded-full bg-white/10 hover:bg-white/20 text-xs font-mono font-bold text-white/90 cursor-pointer transition-all shadow-sm"
                  >
                    Aa {miniLyricFontSize}
                  </button>

                  {/* Romanization / Language pill */}
                  {hasRomanizationSupport && (
                    <button
                      type="button"
                      onClick={toggleLyricDisplayMode}
                      title="Chuyển chế độ: Song ngữ → Romaji thuần → Gốc"
                      className="px-3 py-0.5 rounded-full text-xs font-bold border transition-all cursor-pointer theme-badge-active shadow-sm"
                    >
                      {romaziPillLabel}
                    </button>
                  )}
                </div>

                {/* Live Lyrics Box (_lyricsCard, fills remaining height) */}
                <div
                  ref={miniLyricsContainerRef}
                  className="rounded-[14px] bg-black/40 border border-white/10 p-2 sm:p-2.5 mt-2 flex-1 min-h-[90px] overflow-y-auto custom-scrollbar flex flex-col relative"
                >
                  {parsedLyrics.length > 0 ? (
                    parsedLyrics.map((line, idx) => {
                      const isActive = isLyricsSynced && idx === activeLyricIndex;
                      let primaryText = line.text || "♪";
                      let secondaryText: string | null = null;
                      if (lyricDisplayMode === "romanized" && line.romanized) {
                        primaryText = line.romanized;
                      } else if (lyricDisplayMode === "dual" && line.romanized) {
                        primaryText = line.text;
                        secondaryText = line.romanized;
                      }

                      const fontClass =
                        miniLyricFontSize === 2
                          ? isActive ? "text-[15px] min-[400px]:text-base sm:text-[17px]" : "text-xs min-[400px]:text-[13px] sm:text-sm"
                          : miniLyricFontSize === 3
                            ? isActive ? "text-xs min-[400px]:text-[13px]" : "text-[10px] min-[400px]:text-[11px]"
                            : isActive ? "text-[13px] min-[400px]:text-sm sm:text-[15px]" : "text-[11px] min-[400px]:text-xs sm:text-[13px]";

                      return (
                        <div
                          key={idx}
                          ref={isActive ? miniActiveLyricRef : undefined}
                          onClick={() => {
                            if (line.time > 0) seekToTime(line.time);
                          }}
                          className={`flex flex-col py-0.5 transition-all cursor-pointer ${isActive
                            ? "font-bold text-white opacity-100"
                            : "text-white/50 hover:text-white/80 opacity-70 font-medium"
                            }`}
                        >
                          <div className="flex items-start gap-1.5 leading-snug">
                            {isActive && (
                              <span className="w-1.5 min-h-[16px] rounded-full flex-shrink-0 mt-0.5 shadow-sm" style={{ backgroundColor: 'var(--theme-accent)' }} />
                            )}
                            <p className={fontClass}>{primaryText}</p>
                          </div>
                          {secondaryText && (
                            <p className={`pl-3 text-[11px] text-white/70 mt-0.5 leading-tight ${isActive ? "opacity-95 font-semibold" : "opacity-60"}`}>
                              {secondaryText}
                            </p>
                          )}
                        </div>
                      );
                    })
                  ) : _lyrics?.plainLyrics ? (
                    <div className="text-xs min-[420px]:text-[13px] leading-relaxed text-white/80 whitespace-pre-line my-auto text-center font-medium">
                      {_lyrics.plainLyrics}
                    </div>
                  ) : (
                    <div className="my-auto text-center text-xs text-white/40 italic">
                      {currentTrack ? "Chưa có lời bài hát" : "Chưa phát bài hát nào"}
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* BOTTOM CARD: "BÀI TIẾP THEO" (NEXT TRACK IN QUEUE) */}
            <div
              className={`w-full rounded-[20px] p-3 sm:p-3.5 theme-card-bg shadow-xl border theme-accent-border/40 ${showFullQueueMobile ? "flex-1 min-h-0 flex flex-col" : "flex-shrink-0"
                }`}
            >
              <div className="flex items-center justify-between pb-2 border-b border-white/10 mb-2 flex-shrink-0">
                <span className="text-sm min-[400px]:text-[15px] font-bold text-white tracking-wide">
                  {showFullQueueMobile ? `Hàng đợi (${queueTracks.length})` : "Bài tiếp theo"}
                </span>
                {queueTracks.length > 0 && (
                  <button
                    type="button"
                    onClick={() => setShowFullQueueMobile(!showFullQueueMobile)}
                    className="text-xs theme-accent-text hover:brightness-125 font-semibold cursor-pointer transition-colors"
                  >
                    {showFullQueueMobile ? "Chỉ hiện bài kế" : `Xem tất cả (${queueTracks.length})`}
                  </button>
                )}
              </div>

              {!showFullQueueMobile ? (
                nextTrack ? (
                  <div
                    onClick={() => {
                      if (nextTrackIndex >= 0) {
                        setCurrentIndex(nextTrackIndex);
                        currentIndexRef.current = nextTrackIndex;
                        loadAndPlayTrack(nextTrack, true);
                      }
                    }}
                    className="flex items-center gap-2.5 p-1.5 rounded-xl hover:bg-white/5 transition-all cursor-pointer group"
                  >
                    <div className="w-1.5 h-9 rounded-full bg-[var(--theme-accent)] flex-shrink-0 group-hover:scale-y-110 transition-transform shadow-sm" />
                    <img
                      src={nextTrack.thumbnail || DEFAULT_TRACK_ART}
                      alt={nextTrack.title}
                      onError={(e) => {
                        e.currentTarget.src = DEFAULT_TRACK_ART;
                      }}
                      className="w-11 h-11 rounded-lg object-cover flex-shrink-0 border border-white/10 shadow-sm"
                    />
                    <div className="flex-1 min-w-0 pr-1">
                      <p className="text-sm sm:text-[15px] font-bold text-white truncate group-hover:theme-accent-text transition-colors">
                        {nextTrack.title}
                      </p>
                      <p className="text-xs text-white/60 truncate mt-0.5">
                        {formatTrackArtist(nextTrack.title, nextTrack.author)}
                      </p>
                    </div>
                    <span className="text-xs text-white/70 font-mono flex-shrink-0">
                      {nextTrack.duration && nextTrack.duration > 0 ? formatTime(nextTrack.duration) : "--:--"}
                    </span>
                  </div>
                ) : (
                  <p className="text-xs text-white/40 italic py-3 text-center">
                    Chưa có bài tiếp theo
                  </p>
                )
              ) : (
                <div className="flex-1 min-h-0 flex flex-col gap-1.5 overflow-y-auto custom-scrollbar pr-1">
                  {queueTracks.map((track, i) => {
                    const isCurrent = currentTrack ? currentTrack.id === track.id : i === currentIndex;
                    return (
                      <div
                        key={track.id + i}
                        ref={isCurrent ? mobileActiveQueueRef : undefined}
                        style={{ animationDelay: `${Math.min(i * 20, 200)}ms` }}
                        onClick={() => {
                          setCurrentIndex(i);
                          currentIndexRef.current = i;
                          loadAndPlayTrack(track, true);
                        }}
                        className={`flex items-center gap-2.5 px-2 py-1.5 transition-all cursor-pointer rounded-xl group item-slide-up ${isCurrent ? "theme-badge-active shadow-md" : "hover:bg-white/5"
                          }`}
                      >
                        {isCurrent && (
                          <div className="w-1.5 h-8 rounded-full bg-[var(--theme-accent)] flex-shrink-0" />
                        )}
                        <img
                          src={track.thumbnail || DEFAULT_TRACK_ART}
                          alt=""
                          onError={(e) => { e.currentTarget.src = DEFAULT_TRACK_ART; }}
                          className="w-9 h-9 rounded-md object-cover flex-shrink-0"
                        />
                        <div className="flex-1 min-w-0">
                          <p className="text-[13px] sm:text-sm font-semibold text-white truncate group-hover:theme-accent-text transition-colors">
                            {track.title}
                          </p>
                          <p className="text-[11px] sm:text-xs text-white/60 truncate mt-0.5">
                            {formatTrackArtist(track.title, track.author)}
                          </p>
                        </div>
                        <span className="text-xs text-white/70 font-mono flex-shrink-0">
                          {track.duration && track.duration > 0 ? formatTime(track.duration) : "--:--"}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* ========================================================================= */}
          {/* 2. DESKTOP RIGHT SIDEBAR (EXACT FIGMA 3-COLUMN RIGHT PANEL, hidden lg:flex) */}
          {/* ========================================================================= */}
          <div className="hidden lg:flex flex-col flex-1 min-h-0">
            {/* Top Part: Now Playing Artwork Card (Click to expand Center Now Playing View) */}
            {!isNowPlayingViewOpen && (
              <div
                onClick={() => {
                  if (currentTrack) setIsNowPlayingViewOpen(true);
                }}
                className="w-full aspect-square rounded-[22px] relative shadow-2xl bg-[#1b0833] flex-shrink-0 group max-w-[340px] mx-auto lg:max-w-none cursor-pointer"
                style={{ marginBottom: "18px" }}
                title={currentTrack ? "Bấm vào ảnh bìa để mở trình phát toàn màn hình" : undefined}
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
                    src={getTrackThumbnail(currentTrack, "hd")}
                    alt={currentTrack.title || "Track Artwork"}
                    onError={(e) => {
                      const target = e.currentTarget;
                      if (target.src.includes("hq720.jpg") && currentTrack?.id) {
                        target.src = `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg`;
                      } else if (!target.src.includes(DEFAULT_TRACK_ART)) {
                        target.src = DEFAULT_TRACK_ART;
                      }
                    }}
                    className="w-full h-full object-cover scale-[1.08] select-none transition-all duration-700 group-hover:scale-115"
                  />
                )}

                {/* Dark gradient fade over lower half to mask controls & provide high contrast */}
                <div className="absolute inset-x-0 bottom-0 h-[62%] bg-gradient-to-t from-black/95 via-black/60 to-transparent pointer-events-none z-10" />
              </div>

              {/* Hover expand hint */}
              {currentTrack && (
                <div className="absolute top-2.5 right-12 z-20 opacity-0 group-hover:opacity-100 transition-opacity bg-black/60 backdrop-blur-md px-2 py-1 rounded-lg text-[10px] text-white/90 flex items-center gap-1 pointer-events-none border border-white/10 shadow-md">
                  <Maximize2 className="w-3 h-3 text-pink-400" />
                  <span>Mở rộng</span>
                </div>
              )}

              {/* Structured Body: Top Header Session, Middle Space, and Bottom Player Session (Flexbox, zero overlapping) */}
              <div className="absolute inset-0 p-3 sm:p-3.5 flex flex-col justify-between z-20">
                {/* 1. TOP HEADER SESSION: Menu, Volume, and Favorite Heart */}
                <div className="flex items-center justify-between w-full">
                  {/* Left: Menu & Volume Controls */}
                  <div className="flex items-center gap-1.5">
                    <div className="relative" ref={playerMenuRef}>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
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
                        className={`w-7 h-7 rounded-lg backdrop-blur-md flex items-center justify-center transition-all cursor-pointer ${showPlayerMenu
                          ? "theme-badge-active shadow-md"
                          : "bg-black/40 text-white/80 hover:text-white hover:bg-black/60"
                          }`}
                        title="Tùy chọn bài hát"
                      >
                        <Menu className="w-4 h-4" />
                      </button>

                      {/* Menu Combobox: Drill-down in-place (Zero overlapping popups) */}
                      {showPlayerMenu && currentTrack && (
                        <div
                          onClick={(e) => e.stopPropagation()}
                          className="absolute top-9 left-0 z-40 bg-[#16042a]/98 backdrop-blur-2xl rounded-xl shadow-2xl p-1.5 min-w-[210px] max-w-[250px] flex flex-col gap-1 text-xs animate-fade-in border border-purple-500/40"
                        >
                          {showLibrarySubmenu ? (
                            /* Submenu View */
                            <div className="flex flex-col gap-1.5 animate-fade-in">
                              <div className="flex items-center justify-between px-1 pb-1 border-b border-white/10 text-[10px] font-bold uppercase tracking-wider theme-accent-text">
                                <button
                                  type="button"
                                  onClick={() => {
                                    setShowLibrarySubmenu(false);
                                    setIsCreatingPlaylist(false);
                                  }}
                                  className="flex items-center gap-1 text-white/70 hover:text-white transition-colors cursor-pointer text-[10.5px] font-semibold group/back"
                                >
                                  <ChevronLeft className="w-3.5 h-3.5 group-hover/back:-translate-x-0.5 transition-transform" />
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
                                  <div className="max-h-40 overflow-y-auto flex flex-col gap-1 pr-0.5 custom-scrollbar">
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
                                      <span className="text-[11px] text-white/40 italic px-2 py-1 text-center">Chưa có playlist nào</span>
                                    )}
                                  </div>

                                  <div className="pt-1 border-t border-white/10">
                                    {!isCreatingPlaylist ? (
                                      <button
                                        type="button"
                                        onClick={() => setIsCreatingPlaylist(true)}
                                        className="w-full flex items-center justify-center gap-1.5 px-2 py-1 rounded-lg hover:bg-white/10 theme-accent-text font-medium transition-colors cursor-pointer text-left text-[11px]"
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
                                  </div>
                                </>
                              )}
                            </div>
                          ) : (
                            /* Main Menu View: 3 Options */
                            <div className="flex flex-col gap-1">
                              {/* Option 1: Lưu vào playlist */}
                              <button
                                type="button"
                                onClick={() => setShowLibrarySubmenu(true)}
                                className="flex items-center justify-between w-full px-2.5 py-2 rounded-lg hover:bg-white/10 text-white/90 transition-colors cursor-pointer text-left"
                              >
                                <span className="flex items-center gap-2">
                                  <FolderPlus className="w-4 h-4 theme-accent-text" />
                                  <span>Lưu vào playlist</span>
                                </span>
                                <div className="flex items-center gap-1">
                                  {libraryData.playlists.some((pl) => pl.items?.some((it: any) => it.id === currentTrack.id)) && (
                                    <Check className="w-3.5 h-3.5 text-emerald-400" />
                                  )}
                                  <ChevronRight className="w-3.5 h-3.5 text-white/40" />
                                </div>
                              </button>

                              {/* Option 2: Lưu ngoại tuyến */}
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
                                      ? "Đang lưu..."
                                      : offlineTrackIds.has(currentTrack.id)
                                        ? "Đã lưu offline (Bấm để xóa)"
                                        : "Lưu nghe offline"}
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
                        </div>
                      )}
                    </div>

                    <div className="relative">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          setShowVolumeSlider(!showVolumeSlider);
                        }}
                        className="w-7 h-7 rounded-lg bg-black/40 backdrop-blur-md flex items-center justify-center text-white/70 hover:theme-accent-text hover:bg-black/60 transition-all cursor-pointer"
                        title="Âm lượng"
                      >
                        {isMuted || volume === 0 ? <VolumeX className="w-4 h-4" /> : <Volume2 className="w-4 h-4" />}
                      </button>
                      {showVolumeSlider && (
                        <div
                          onClick={(e) => e.stopPropagation()}
                          className="absolute top-9 left-0 p-2.5 rounded-xl theme-menu-glass shadow-2xl flex items-center gap-2 z-30 border theme-accent-border/40"
                        >
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

                  {/* Right: Bright Red Heart Icon (Matching Figma) */}
                  <button
                    type="button"
                    disabled={!currentTrack}
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleCurrentFavorite();
                    }}
                    title={
                      !currentTrack
                        ? "Chưa phát bài hát nào"
                        : !userSession?.isLoggedIn
                          ? "Đăng nhập để yêu thích"
                          : libraryData.favorites.some((t) => t.id === currentTrack.id)
                            ? "Bỏ yêu thích"
                            : "Thêm vào yêu thích"
                    }
                    className={`w-8 h-8 rounded-lg backdrop-blur-md flex items-center justify-center transition-all ${!currentTrack
                      ? "opacity-30 cursor-not-allowed bg-black/20"
                      : "bg-black/40 hover:bg-black/60 cursor-pointer group"
                      }`}
                  >
                    <Heart
                      className={`w-5 h-5 transition-transform ${!currentTrack
                        ? "text-white/40"
                        : userSession?.isLoggedIn && libraryData.favorites.some((t) => t.id === currentTrack.id)
                          ? "fill-red-500 text-red-500 drop-shadow-[0_0_10px_rgba(239,68,68,0.9)] group-hover:scale-115"
                          : "text-white/60 hover:text-red-400 group-hover:scale-115"
                        }`}
                    />
                  </button>
                </div>

                {/* 2. MIDDLE SESSION: Space for artwork visibility */}
                <div className="flex-1 min-h-0 pointer-events-none" />

                {/* 3. BOTTOM FOOTER SESSION: Title/Artist, Seekbar, and Controls in vertical Flex flow */}
                <div className="w-full flex flex-col gap-2">
                  {/* Row 1: Track Title and Artist */}
                  {currentTrack && (
                    <div className="min-w-0 px-0.5">
                      <p className="text-[13px] font-bold text-white truncate drop-shadow-md leading-tight" title={currentTrack.title}>
                        {currentTrack.title}
                      </p>
                      <p className="text-[11px] text-white/70 truncate drop-shadow mt-0.5">
                        {formatTrackArtist(currentTrack.title, currentTrack.author)}
                      </p>
                    </div>
                  )}

                  {/* Row 2: Seekbar Progress Line with Scrubber Handle & Time */}
                  <div className="flex flex-col gap-0.5 w-full">
                    <div className="relative w-full h-4 flex items-center group/seek touch-none">
                      <div className="absolute left-0 right-0 h-1.5 rounded-full bg-white/20" />
                      <div
                        className="absolute left-0 h-1.5 rounded-full"
                        style={{
                          width: `${duration ? Math.min(100, (currentTime / duration) * 100) : 0}%`,
                          background: "var(--theme-btn-gradient)",
                        }}
                      />
                      <input
                        type="range"
                        min={0}
                        max={duration || 100}
                        step={0.5}
                        value={currentTime}
                        onChange={handleSeek}
                        onClick={(e) => e.stopPropagation()}
                        disabled={!currentTrack}
                        className={`w-full h-full opacity-0 z-10 touch-none ${!currentTrack ? "cursor-not-allowed pointer-events-none" : "cursor-pointer"}`}
                      />
                      <div
                        className="absolute w-3.5 h-3.5 rounded-full bg-white shadow-md pointer-events-none transition-transform group-hover/seek:scale-125"
                        style={{
                          left: `calc(${duration ? Math.min(100, (currentTime / duration) * 100) : 0}% - 7px)`,
                          boxShadow: "0 0 10px var(--theme-accent)",
                        }}
                      />
                    </div>
                    <div className="flex justify-between text-[10px] text-white/60 font-mono px-0.5">
                      <span>{formatTime(currentTime)}</span>
                      <span>{formatTime(duration)}</span>
                    </div>
                  </div>

                  {/* Row 3: 5 Playback Control Buttons Row - Enlarged & Beautifully Spaced */}
                  <div className="flex items-center justify-between px-0.5 w-full">
                    {/* Loop / Shuffle Button */}
                    <button
                      type="button"
                      disabled={!currentTrack}
                      onClick={(e) => {
                        e.stopPropagation();
                        cycleRepeatMode();
                      }}
                      title={
                        !currentTrack
                          ? "Chưa chọn bài hát"
                          : repeatMode === "all"
                            ? "Lặp danh sách"
                            : repeatMode === "one"
                              ? "Lặp 1 bài"
                              : repeatMode === "shuffle"
                                ? "Phát ngẫu nhiên"
                                : "Tắt lặp lại"
                      }
                      className={`p-1.5 transition-all ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                          : repeatMode === "all" || repeatMode === "one"
                            ? "theme-accent-text drop-shadow-[0_0_8px_var(--theme-accent)] cursor-pointer hover:scale-115 active:scale-90"
                            : repeatMode === "shuffle"
                              ? "theme-sec-accent-text drop-shadow-[0_0_8px_var(--theme-sec-accent)] cursor-pointer hover:scale-115 active:scale-90"
                              : "text-white/40 hover:text-white cursor-pointer hover:scale-115 active:scale-90"
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
                      disabled={!currentTrack}
                      onClick={(e) => {
                        e.stopPropagation();
                        handlePrev();
                      }}
                      title={!currentTrack ? "Chưa chọn bài hát" : "Bài trước"}
                      className={`p-1.5 transition-all ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                          : "text-white/70 hover:theme-accent-text cursor-pointer hover:scale-115 active:scale-90"
                      }`}
                    >
                      <SkipBack className="w-6 h-6 fill-current" />
                    </button>

                    {/* Large Dynamic Play / Pause Circle (w-12 h-12) */}
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        togglePlayPause();
                      }}
                      disabled={!currentTrack || isLoading}
                      title={!currentTrack ? "Chưa chọn bài hát" : isPlaying ? "Tạm dừng" : "Phát"}
                      className={`w-11 h-11 min-[340px]:w-12 min-[340px]:h-12 rounded-full flex items-center justify-center text-white transition-all ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none shadow-none"
                          : "shadow-xl hover:scale-110 active:scale-95 cursor-pointer"
                      }`}
                      style={{
                        background: "var(--theme-btn-gradient)",
                        boxShadow: !currentTrack ? "none" : "0 4px 18px var(--theme-panel-border)",
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
                      disabled={!currentTrack}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleNext();
                      }}
                      title={!currentTrack ? "Chưa chọn bài hát" : "Bài kế tiếp"}
                      className={`p-1.5 transition-all ${
                        !currentTrack
                          ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                          : "text-white/70 hover:theme-accent-text cursor-pointer hover:scale-115 active:scale-90"
                      }`}
                    >
                      <SkipForward className="w-6 h-6 fill-current" />
                    </button>

                    {/* Timer */}
                    <div className="relative">
                      <button
                        type="button"
                        disabled={!currentTrack}
                        onClick={(e) => {
                          e.stopPropagation();
                          setShowSleepTimerMenu(!showSleepTimerMenu);
                        }}
                        title={
                          !currentTrack
                            ? "Chưa chọn bài hát"
                            : sleepTimerSecondsLeft
                              ? `Hẹn giờ tắt nhạc: còn ${Math.floor(sleepTimerSecondsLeft / 60)}p ${sleepTimerSecondsLeft % 60}s`
                              : "Hẹn giờ tắt nhạc"
                        }
                        className={`p-1.5 transition-all relative ${
                          !currentTrack
                            ? "opacity-35 cursor-not-allowed pointer-events-none text-white/40"
                            : sleepTimerSecondsLeft
                              ? "theme-accent-text font-bold cursor-pointer hover:scale-110 active:scale-90"
                              : "text-white/60 hover:text-white cursor-pointer hover:scale-110 active:scale-90"
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
                        <div
                          onClick={(e) => e.stopPropagation()}
                          className="absolute bottom-9 right-0 p-2 rounded-xl theme-menu-glass shadow-2xl flex flex-col gap-1 z-30 min-w-[130px] animate-fade-in border theme-accent-border/40"
                        >
                          <span className="text-[10px] uppercase font-bold theme-accent-text tracking-wider px-2 py-0.5">
                            Hẹn giờ tắt
                          </span>
                          {[15, 30, 45, 60].map((m) => (
                            <button
                              key={m}
                              type="button"
                              onClick={() => startSleepTimer(m)}
                              className={`text-left text-xs px-2.5 py-1.5 rounded-lg transition-colors cursor-pointer ${sleepTimerMinutes === m ? "theme-badge-active" : "hover:bg-white/10 text-white/80"
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
              </div>
            </div>
          )}

          {/* Hàng đợi phát (Queue List) - Sidebar 2 chuyên dụng 100% cho Hàng đợi */}
          <div className="flex items-center justify-between px-1 pb-2 flex-shrink-0">
            <span className="text-xs font-semibold text-white/80 flex items-center gap-1.5">
              <ListMusic className="w-4 h-4 theme-accent-text" />
              <span>Hàng đợi {queueTracks.length > 0 && `(${queueTracks.length})`}</span>
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
            className="flex-1 overflow-y-auto flex flex-col min-h-0 pr-1 custom-scrollbar"
            style={{ gap: "8px" }}
          >
            {queueTracks.length > 0 ? (
              queueTracks.map((track, i) => {
                const isCurrent = currentTrack ? currentTrack.id === track.id : i === currentIndex;
                const artistLabel = formatTrackArtist(track.title, track.author);
                return (
                  <div
                    key={track.id + i}
                    style={{ animationDelay: `${Math.min(i * 20, 200)}ms` }}
                    onClick={() => {
                      setCurrentIndex(i);
                      currentIndexRef.current = i;
                      loadAndPlayTrack(track, true);
                    }}
                    className={`flex items-center gap-2.5 px-2.5 py-1.5 transition-all cursor-pointer group item-slide-up ${
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
          </div>
        </aside>
      </main>

      {/* ========================================================================= */}
      {/* MOBILE MINI-PLAYER BAR (Floating above bottom nav when not in 'player' tab)*/}
      {/* ========================================================================= */}
      {currentTrack && mobileTab !== "player" && (
        <div
          onClick={() => setMobileTab("player")}
          className="lg:hidden fixed safe-mini-player left-2.5 right-2.5 z-40 bg-[var(--theme-panel-bg)]/95 backdrop-blur-2xl border theme-accent-border rounded-2xl p-2 flex items-center gap-2.5 shadow-2xl cursor-pointer animate-fade-in"
        >
          <img
            src={
              currentTrack.thumbnail && !currentTrack.thumbnail.includes("figma")
                ? (currentTrack.thumbnail.includes("hqdefault.jpg")
                  ? currentTrack.thumbnail.replace("hqdefault.jpg", "mqdefault.jpg")
                  : currentTrack.thumbnail)
                : (currentTrack.id ? `https://i.ytimg.com/vi/${currentTrack.id}/mqdefault.jpg` : DEFAULT_TRACK_ART)
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
          <div className="flex items-center gap-1 sm:gap-1.5 flex-shrink-0" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              onClick={toggleCurrentFavorite}
              className="p-1 sm:p-1.5 text-white/70 hover:text-white cursor-pointer hover:scale-110 active:scale-95 transition-transform"
              title="Yêu thích"
            >
              <Heart
                className={`w-4 h-4 ${userSession?.isLoggedIn && libraryData.favorites.some((t) => t.id === currentTrack.id)
                  ? "fill-red-500 text-red-500"
                  : "text-white/60"
                  }`}
              />
            </button>
            <button
              type="button"
              onClick={handlePrev}
              className="p-1 sm:p-1.5 text-white/70 hover:text-white cursor-pointer hover:scale-110 active:scale-90 transition-transform"
              title="Bài trước đó"
            >
              <SkipBack className="w-4 h-4 fill-current" />
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
              className="p-1 sm:p-1.5 text-white/70 hover:text-white cursor-pointer hover:scale-110 active:scale-90 transition-transform"
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
      <nav className="lg:hidden fixed bottom-0 left-0 right-0 safe-bottom-nav bg-[var(--theme-panel-bg)]/95 backdrop-blur-2xl border-t border-white/10 z-40 flex items-center justify-around px-2 shadow-2xl">
        <button
          type="button"
          onClick={() => {
            if (mobileTab === "explore" && !isSearchOpen && !selectedArtist) {
              refreshHomeFeed(true);
            } else {
              setMobileTab("explore");
              setIsSearchOpen(false);
              setSelectedArtist(null);
            }
          }}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer ${mobileTab === "explore" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
            }`}
        >
          <Compass className="w-5 h-5" />
          <span className="text-[10px]">Khám phá</span>
        </button>

        <button
          type="button"
          onClick={() => setMobileTab("library")}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer ${mobileTab === "library" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
            }`}
        >
          <FolderHeart className="w-5 h-5" />
          <span className="text-[10px]">Thư viện</span>
        </button>

        <button
          type="button"
          onClick={() => setMobileTab("player")}
          className={`flex flex-col items-center justify-center gap-1 flex-1 py-1 transition-all cursor-pointer relative ${mobileTab === "player" ? "theme-accent-text font-bold" : "text-white/50 hover:text-white"
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
                    className={`relative z-20 flex-1 py-2 rounded-xl text-xs font-bold transition-all duration-200 cursor-pointer text-center ${authMode === "login" ? "text-white scale-[1.02]" : "text-white/60 hover:text-white"
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
                    className={`relative z-20 flex-1 py-2 rounded-xl text-xs font-bold transition-all duration-200 cursor-pointer text-center ${authMode === "register" ? "text-white scale-[1.02]" : "text-white/60 hover:text-white"
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
                      className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-base sm:text-xs text-white placeholder-white/30 focus:outline-none"
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
                      className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-base sm:text-xs text-white placeholder-white/30 focus:outline-none"
                    />
                  </div>

                  {/* Confirm Password Field with Smooth Jelly Accordion Animation */}
                  <div
                    className={`transition-all duration-300 overflow-hidden ${authMode === "register"
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
                        className="w-full px-3.5 py-2.5 rounded-xl theme-input-pill text-base sm:text-xs text-white placeholder-white/30 focus:outline-none"
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
