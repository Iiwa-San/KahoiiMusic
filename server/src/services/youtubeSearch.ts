import { execFile } from "child_process";
import path from "path";
import fs from "fs";
import https from "https";

export interface SearchTrackResult {
  id: string;
  title: string;
  author: string;
  duration: number; // in seconds
  durationText?: string;
  thumbnail: string;
  artistBrowseId?: string;
  isMusic?: boolean;
}

export interface SearchTopResult {
  id: string;
  title: string;
  subtitle: string;
  author: string;
  duration: number;
  durationText?: string;
  thumbnail: string;
  type: "song" | "artist" | "playlist" | "video";
  isMusic?: boolean;
}

export interface SearchArtistItem {
  id: string;
  name: string;
  subscribers?: string;
  thumbnail: string;
}

export interface SearchPlaylistItem {
  id: string;
  title: string;
  author: string;
  trackCountText?: string;
  thumbnail: string;
}

export interface StructuredSearchResponse {
  query: string;
  topResult: SearchTopResult | null;
  tracks: SearchTrackResult[];
  artists: SearchArtistItem[];
  playlists: SearchPlaylistItem[];
  results: SearchTrackResult[]; // backward compatibility
}

const GENERIC_EXCLUDED_ARTISTS = new Set([
  "nghệ sĩ",
  "youtube music",
  "sound effects",
  "sound effect",
  "sound effect library",
  "various artists",
  "chủ đề",
  "topic",
  "karaoke",
  "nhạc cụ",
  "instrumental",
  "chưa có tác giả",
  "kahoii music",
]);

const artistProfileCache = new Map<string, SearchArtistItem>();

function cleanArtistName(raw: string): string {
  if (!raw) return "";
  return raw.replace(/\s*-\s*Topic$/i, "").replace(/\s+Topic$/i, "").trim();
}

function parseDuration(text: string): number {
  if (!text) return 0;
  const parts = text.split(":");
  if (parts.length === 2) {
    return (parseInt(parts[0], 10) || 0) * 60 + (parseInt(parts[1], 10) || 0);
  }
  if (parts.length === 3) {
    return (
      (parseInt(parts[0], 10) || 0) * 3600 +
      (parseInt(parts[1], 10) || 0) * 60 +
      (parseInt(parts[2], 10) || 0)
    );
  }
  return 0;
}

function extractRunsText(runs: any[]): string {
  if (!runs || !Array.isArray(runs)) return "";
  return runs.map((r) => r?.text || "").join("");
}

/**
 * Shared POST helper for YouTube Music InnerTube API (WEB_REMIX client)
 */
function postYTM(endpoint: string, payload: Record<string, any>, timeout = 9000): Promise<any> {
  return new Promise((resolve) => {
    const postData = JSON.stringify({
      ...payload,
      context: {
        client: {
          clientName: "WEB_REMIX",
          clientVersion: "1.20240101.01.00",
          hl: "vi",
          gl: "VN",
        },
      },
    });

    const req = https.request(
      `https://music.youtube.com/youtubei/v1/${endpoint}`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Referer: "https://music.youtube.com/",
          Origin: "https://music.youtube.com",
        },
        timeout,
      },
      (res) => {
        let body = "";
        res.on("data", (c) => (body += c));
        res.on("end", () => {
          try {
            resolve(JSON.parse(body));
          } catch {
            resolve(null);
          }
        });
      }
    );

    req.on("error", () => resolve(null));
    req.on("timeout", () => {
      req.destroy();
      resolve(null);
    });
    req.write(postData);
    req.end();
  });
}

/**
 * Shared POST helper for standard YouTube InnerTube API (WEB client)
 */
function postYouTubeWeb(endpoint: string, payload: Record<string, any>, timeout = 9000): Promise<any> {
  return new Promise((resolve) => {
    const postData = JSON.stringify({
      ...payload,
      context: {
        client: {
          clientName: "WEB",
          clientVersion: "2.20240101.01.00",
          hl: "vi",
          gl: "VN",
        },
      },
    });

    const req = https.request(
      `https://www.youtube.com/youtubei/v1/${endpoint}`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Referer: "https://www.youtube.com/",
          Origin: "https://www.youtube.com",
        },
        timeout,
      },
      (res) => {
        let body = "";
        res.on("data", (c) => (body += c));
        res.on("end", () => {
          try {
            resolve(JSON.parse(body));
          } catch {
            resolve(null);
          }
        });
      }
    );

    req.on("error", () => resolve(null));
    req.on("timeout", () => {
      req.destroy();
      resolve(null);
    });
    req.write(postData);
    req.end();
  });
}

const artistMemoryCache = new Map<string, string>();

/**
 * Smart Multi-Tier Artist Resolver:
 * 1. Title extraction (Artist - Title, Title / Artist, Covered by, quotes, brackets, hashtag)
 * 2. YouTube Video Details & Description probing (Title · Artist standard pattern, Vocal:, Artist:, Singer:, Ca sĩ:)
 * 3. YouTube Channel name fallback (minus Topic, Chủ đề, Official, etc.)
 */
export async function resolveTrackArtist(videoId: string, rawTitle?: string): Promise<string> {
  const vid = (videoId || "").trim();
  const title = (rawTitle || "").trim();

  // Check in-memory cache first
  if (vid && artistMemoryCache.has(vid)) {
    return artistMemoryCache.get(vid)!;
  }

  // Tier 1: Check title pattern
  if (title) {
    if (title.includes(" / ")) {
      const parts = title.split(" / ");
      if (parts.length >= 2) {
        const cand = parts[1].replace(/\(Music Video\)/gi, "").replace(/【.*?】/g, "").replace(/\(.*?\)/g, "").trim();
        if (cand && !/^(mv|official|lyric|audio|video)$/i.test(cand)) {
          if (vid) artistMemoryCache.set(vid, cand);
          return cand;
        }
      }
    }
    if (title.includes(" - ")) {
      const parts = title.split(" - ");
      if (parts.length >= 2) {
        let cand = parts[0].trim().replace(/^\[.*?\]\s*/, "").replace(/^【.*?】\s*/, "").trim();
        if (cand && !/^(mv|official|lyric|audio|video|teaser|hd|4k)$/i.test(cand)) {
          if (vid) artistMemoryCache.set(vid, cand);
          return cand;
        }
      }
    }
    if (title.includes(" | ")) {
      const parts = title.split(" | ");
      if (parts.length >= 2) {
        const cand = parts[1].trim();
        if (cand) {
          if (vid) artistMemoryCache.set(vid, cand);
          return cand;
        }
      }
    }
    const mCover = title.match(/(?:covered\s+by|cover\s+by|vocal\s*:?)\s*([^\s\(\)\[\]【】\/|]+)/i);
    if (mCover && mCover[1]) {
      const cand = mCover[1].trim();
      if (vid) artistMemoryCache.set(vid, cand);
      return cand;
    }
    const mQuotes = title.match(/([^\s\u300C\u300E\u3010]+)\s*[\u300C\u300E\u3010][^\u300D\u300F\u3011]+[\u300D\u300F\u3011]/);
    if (mQuotes && mQuotes[1] && !/^(mv|official|video|audio|lyrics?|弾き語り|歌ってみた)$/i.test(mQuotes[1])) {
      const cand = mQuotes[1].trim();
      if (vid) artistMemoryCache.set(vid, cand);
      return cand;
    }
  }

  // Tier 2: Probe YouTube Innertube Player (Description & Channel)
  if (vid && vid.length === 11) {
    try {
      const d = await postYouTubeWeb("player", { videoId: vid }, 5000);
      const desc = d?.videoDetails?.shortDescription || "";
      const channel = (d?.videoDetails?.author || "").trim();

      // Probe description for "Title · Artist" or "Artist: ..."
      if (desc) {
        const lines = desc.split("\n").map((l: string) => l.trim()).filter(Boolean);
        for (const line of lines) {
          if (line.includes("·")) {
            const parts = line.split("·").map((p: string) => p.trim());
            if (parts.length >= 2 && parts[1]) {
              const cand = parts[1].replace(/\s*-\s*Topic$/i, "").trim();
              if (cand) {
                artistMemoryCache.set(vid, cand);
                return cand;
              }
            }
          }
          const mVoc = line.match(/(?:vocal|artist|singer|ca sĩ|trình bày|cover by|nhạc sĩ)\s*[:：]\s*([^\n\r,\/]+)/i);
          if (mVoc && mVoc[1]) {
            const cand = mVoc[1].trim();
            if (cand) {
              artistMemoryCache.set(vid, cand);
              return cand;
            }
          }
        }
      }

      // Probe channel name
      if (channel) {
        const cleanChan = channel
          .replace(/\s*-\s*Topic$/i, "")
          .replace(/\s*-\s*Chủ đề$/i, "")
          .replace(/\s*Official Channel$/i, "")
          .replace(/\s*Official$/i, "")
          .trim();
        if (cleanChan && cleanChan !== "Nghệ sĩ" && cleanChan !== "YouTube") {
          artistMemoryCache.set(vid, cleanChan);
          return cleanChan;
        }
      }
    } catch {}
  }

  return "";
}

function extractDuration(renderer: any, descRuns: any[]): { duration: number; durationText: string } {
  for (let i = descRuns.length - 1; i >= 0; i--) {
    const txt = descRuns[i]?.text?.trim();
    if (txt && /^\d+:\d+(?::\d+)?$/.test(txt)) {
      return { duration: parseDuration(txt), durationText: txt };
    }
  }
  for (const fCol of renderer.fixedColumns || []) {
    for (const r of fCol?.musicResponsiveListItemFixedColumnRenderer?.text?.runs || []) {
      const txt = r?.text?.trim();
      if (txt && /^\d+:\d+(?::\d+)?$/.test(txt)) {
        return { duration: parseDuration(txt), durationText: txt };
      }
    }
  }
  return { duration: 0, durationText: "" };
}

function parseSongItem(renderer: any, fallbackAuthor = ""): SearchTrackResult | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const title = extractRunsText(flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs);
  if (!title) return null;

  const descRuns = flexCols[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs || [];
  let author = "";
  let artistBrowseId: string | undefined = undefined;

  const artistNames: string[] = [];
  for (const r of descRuns) {
    if (!r?.text) continue;
    const pageType =
      r.navigationEndpoint?.browseEndpoint?.browseEndpointContextSupportedConfigs
        ?.browseEndpointContextMusicConfig?.pageType;
    const browseId = r.navigationEndpoint?.browseEndpoint?.browseId;
    const isArtist =
      pageType === "MUSIC_PAGE_TYPE_ARTIST" ||
      pageType === "MUSIC_PAGE_TYPE_USER_CHANNEL" ||
      (browseId && browseId.startsWith("UC"));
    if (isArtist) {
      const aName = r.text.trim();
      if (aName && !artistNames.includes(aName)) {
        artistNames.push(aName);
      }
      if (!artistBrowseId) artistBrowseId = browseId;
    }
  }
  if (artistNames.length > 0) {
    author = artistNames.join(", ");
  }

  if (!author) {
    for (const r of descRuns) {
      const txt = (r?.text || "").trim();
      if (
        txt &&
        txt !== "•" &&
        txt !== "·" &&
        txt !== "Bài hát" &&
        txt !== "Video" &&
        txt !== "Song" &&
        txt !== "Nghệ sĩ" &&
        txt !== "Artist" &&
        txt !== "Kênh" &&
        txt !== "Channel" &&
        !/^\d+[\s\u00a0]*(Tr|T|k|N|M|B)?\s*(lượt|views|plays)/i.test(txt) &&
        !/^\d+:\d+(?::\d+)?$/.test(txt) &&
        !/^\d{4}$/.test(txt)
      ) {
        author = txt;
        break;
      }
    }
  }

  // Check title for artist patterns
  if (!author && title) {
    if (title.includes(" / ")) {
      const parts = title.split(" / ");
      if (parts.length >= 2) author = parts[1].replace(/\(Music Video\)/gi, "").replace(/【.*?】/g, "").trim();
    } else if (title.includes(" - ")) {
      const parts = title.split(" - ");
      if (parts.length >= 2) {
        const p0 = parts[0].trim().replace(/^\[.*?\]\s*/, "").replace(/^【.*?】\s*/, "");
        if (p0 && !/^(mv|official|lyric|audio|video)$/i.test(p0)) author = p0;
      }
    } else if (title.includes(" | ")) {
      const parts = title.split(" | ");
      if (parts.length >= 2) author = parts[1].trim();
    }
    if (!author) {
      const mCover = title.match(/(?:covered\s+by|cover\s+by|vocal\s*:?)\s*([^\s\(\)\[\]【】\/|]+)/i);
      if (mCover && mCover[1]) author = mCover[1].trim();
    }
    if (!author) {
      const hashMatch = title.match(/#([^\s#]+)/);
      if (hashMatch && hashMatch[1]) author = hashMatch[1].trim();
    }
    if (!author) {
      const bMatch = title.match(/【([^】]+)】/);
      if (bMatch && bMatch[1] && !/歌ってみた|弾き語り|MV|Official|Lyric|Audio/i.test(bMatch[1])) {
        author = bMatch[1].trim();
      }
    }
  }

  if (!author && fallbackAuthor && fallbackAuthor !== "Nghệ sĩ") {
    author = fallbackAuthor;
  }

  const { duration, durationText } = extractDuration(renderer, descRuns);

  const videoId =
    renderer.playlistItemData?.videoId ||
    renderer.overlay?.musicItemThumbnailOverlayRenderer?.content?.musicPlayButtonRenderer
      ?.playNavigationEndpoint?.watchEndpoint?.videoId;

  if (!videoId) return null;

  const thumbs = renderer.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
  const rawThumb = thumbs[thumbs.length - 1]?.url;
  const thumbnail =
    rawThumb && rawThumb.startsWith("http")
      ? rawThumb
      : `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`;

  return { id: videoId, title, author: author || "", duration, durationText, thumbnail, artistBrowseId, isMusic: true };
}

function parseArtistItem(renderer: any): SearchArtistItem | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const name = extractRunsText(flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs);
  if (!name) return null;

  const subText = extractRunsText(flexCols[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs);
  const thumbs = renderer.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
  const thumbnail = thumbs[thumbs.length - 1]?.url || "";
  const id = renderer.navigationEndpoint?.browseEndpoint?.browseId || "";

  return { id, name, subscribers: subText, thumbnail };
}

function parsePlaylistItem(renderer: any): SearchPlaylistItem | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const title = extractRunsText(flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs);
  if (!title) return null;

  const descRuns = flexCols[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs || [];
  const author = descRuns[0]?.text || "YouTube Music";
  const trackCountText = extractRunsText(descRuns);

  const thumbs = renderer.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
  const thumbnail = thumbs[thumbs.length - 1]?.url || "";
  const id =
    renderer.navigationEndpoint?.browseEndpoint?.browseId ||
    renderer.overlay?.musicItemThumbnailOverlayRenderer?.content?.musicPlayButtonRenderer
      ?.playNavigationEndpoint?.watchEndpoint?.playlistId ||
    "";

  return { id, title, author, trackCountText, thumbnail };
}

function getLocalYtDlpPath(): string | null {
  const possiblePaths = [
    path.resolve(process.cwd(), "../../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "../KahoiiMusic/yt-dlp.exe"),
    path.resolve(process.cwd(), "yt-dlp.exe"),
  ];
  return possiblePaths.find((p) => fs.existsSync(p)) || null;
}

async function searchViaYtDlp(query: string, maxResults = 50): Promise<SearchTrackResult[]> {
  const ytDlpPath = getLocalYtDlpPath();
  if (!ytDlpPath) return [];

  return new Promise((resolve) => {
    const args = [
      "--flat-playlist",
      "--encoding",
      "utf-8",
      "--no-warnings",
      "--print",
      "%(id)s||%(title)s||%(uploader)s||%(duration)s||%(thumbnail)s",
      `ytsearch${maxResults}:${query}`,
    ];

    execFile(
      ytDlpPath,
      args,
      {
        timeout: 25000,
        encoding: "utf8",
        env: { ...process.env, PYTHONIOENCODING: "utf-8" },
        maxBuffer: 10 * 1024 * 1024,
      },
      (error, stdout) => {
        if (error && (!stdout || stdout.trim().length === 0)) {
          resolve([]);
          return;
        }

        const lines = (stdout || "").trim().split("\n");
        const results: SearchTrackResult[] = [];
        const seenIds = new Set<string>();

        for (const line of lines) {
          const parts = line.split("||");
          if (parts.length >= 2 && parts[0].trim().length > 0) {
            const id = parts[0].trim();
            if (id.startsWith("UC") || seenIds.has(id)) continue;
            seenIds.add(id);

            const title = parts[1]?.trim() || "Chưa có tiêu đề";
            const author = parts[2]?.trim() || "Kahoii Music";
            const durRaw = parts[3]?.trim();
            const duration = durRaw && !isNaN(Number(durRaw)) ? Math.round(Number(durRaw)) : 0;
            const rawThumb = parts[4]?.trim();
            const thumbnail =
              rawThumb && rawThumb !== "NA" && rawThumb.startsWith("http")
                ? rawThumb
                : `https://i.ytimg.com/vi/${id}/mqdefault.jpg`;

            results.push({ id, title, author, duration, thumbnail });
          }
        }
        resolve(results);
      }
    );
  });
}

async function getChannelUploadTracks(browseId: string, fallbackAuthor: string): Promise<SearchTrackResult[]> {
  try {
    const data = await postYTM("browse", { browseId }, 5000);
    const tabs = data?.contents?.singleColumnBrowseResultsRenderer?.tabs || [];
    const secList = tabs[0]?.tabRenderer?.content?.sectionListRenderer?.contents || [];
    const tracks: SearchTrackResult[] = [];
    const seenIds = new Set<string>();

    for (const s of secList) {
      const shelf = s.musicShelfRenderer || s.musicCarouselShelfRenderer;
      const items = shelf?.contents || [];
      for (const it of items) {
        const rend = it.musicResponsiveListItemRenderer || it.musicTwoRowItemRenderer;
        if (!rend) continue;
        const title = extractRunsText(
          rend.flexColumns?.[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs ||
          rend.title?.runs
        );
        const sub = extractRunsText(
          rend.flexColumns?.[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs ||
          rend.subtitle?.runs
        );
        const videoId =
          rend.navigationEndpoint?.watchEndpoint?.videoId ||
          rend.playlistItemData?.videoId ||
          rend.overlay?.musicItemThumbnailOverlayRenderer?.content?.musicPlayButtonRenderer
            ?.playNavigationEndpoint?.watchEndpoint?.videoId;
        if (!videoId || seenIds.has(videoId)) continue;
        seenIds.add(videoId);

        const thumbs =
          rend.thumbnailRenderer?.musicThumbnailRenderer?.thumbnail?.thumbnails ||
          rend.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
        const thumbnail =
          thumbs[thumbs.length - 1]?.url ||
          `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg`;

        const author = sub ? sub.split("•")[0].trim() : fallbackAuthor;
        tracks.push({
          id: videoId,
          title,
          author: author || fallbackAuthor,
          duration: 0,
          durationText: "",
          thumbnail,
          artistBrowseId: browseId,
        });
      }
    }
    return tracks;
  } catch {
    return [];
  }
}

async function fetchArtistProfile(
  browseId: string,
  fallbackName: string,
  fallbackThumb: string
): Promise<SearchArtistItem> {
  const cacheKey = browseId || fallbackName.toLowerCase();
  const cached = artistProfileCache.get(cacheKey);
  if (cached) return cached;

  const fallback: SearchArtistItem = {
    id: browseId || `artist-${encodeURIComponent(fallbackName)}`,
    name: fallbackName,
    subscribers: "Nghệ sĩ",
    thumbnail: fallbackThumb,
  };

  if (!browseId || !browseId.startsWith("UC")) {
    artistProfileCache.set(cacheKey, fallback);
    return fallback;
  }

  try {
    const data = await postYTM("browse", { browseId }, 2500);
    const header = data?.header?.musicImmersiveHeaderRenderer || data?.header?.musicVisualHeaderRenderer;
    if (!header) {
      artistProfileCache.set(cacheKey, fallback);
      return fallback;
    }

    const name = header.title?.runs?.[0]?.text || fallbackName;
    const subText = header.subscriptionButton?.subscribeButtonRenderer?.subscriberCountText?.runs?.[0]?.text;
    const thumbs = header.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
    const thumbnail = thumbs[thumbs.length - 1]?.url || thumbs[0]?.url || fallbackThumb;

    const item: SearchArtistItem = {
      id: browseId,
      name,
      subscribers: subText ? `Nghệ sĩ • ${subText}` : "Nghệ sĩ",
      thumbnail,
    };
    artistProfileCache.set(cacheKey, item);
    return item;
  } catch {
    artistProfileCache.set(cacheKey, fallback);
    return fallback;
  }
}

/**
 * Main structured search endpoint:
 * Strictly retrieves 100% YouTube Music categorized items (songs, artists, playlists, hero card).
 */
export async function searchYouTubeMusicStructured(query: string): Promise<StructuredSearchResponse> {
  const trimmed = query.trim();
  if (!trimmed) {
    return {
      query: "",
      topResult: null,
      tracks: [],
      artists: [],
      playlists: [],
      results: [],
    };
  }

  try {
    const isHandleQuery = trimmed.startsWith("@") || (trimmed.includes("_") && !trimmed.includes(" "));
    const effectiveQuery = trimmed.startsWith("@") ? trimmed : (isHandleQuery ? `@${trimmed}` : trimmed);

    // Run 4 queries in parallel for high speed (~700ms total)
    const [overviewData, songsData, artistsData, playlistsData] = await Promise.all([
      postYTM("search", { query: effectiveQuery }),
      postYTM("search", { query: effectiveQuery, params: "EgWKAQIIAWoSEAUQAxAJEAQQChAVEBAQERAO" }), // Songs
      postYTM("search", { query: effectiveQuery, params: "EgWKAQIgAWoSEAUQAxAJEAQQChAVEBAQERAO" }), // Artists
      postYTM("search", { query: effectiveQuery, params: "EgeKAQQoAEABahIQBRADEAkQBBAKEBUQEBAREA4%3D" }), // Playlists
    ]);

    // 1. Parse Songs
    const tracks: SearchTrackResult[] = [];
    const seenTrackIds = new Set<string>();

    const songShelf =
      songsData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents?.[0]?.musicShelfRenderer;

    if (songShelf?.contents) {
      for (const item of songShelf.contents) {
        const parsed = parseSongItem(item.musicResponsiveListItemRenderer);
        if (parsed && !seenTrackIds.has(parsed.id)) {
          seenTrackIds.add(parsed.id);
          tracks.push(parsed);
        }
      }

      // If there's a continuation token, fetch next batch to reach ~40 songs
      const nextCont = songShelf.continuations?.[0]?.nextContinuationData?.continuation;
      if (nextCont && tracks.length < 40) {
        try {
          const contData = await postYTM("search", { continuation: nextCont });
          const nextItems = contData?.continuationContents?.musicShelfContinuation?.contents;
          if (Array.isArray(nextItems)) {
            for (const item of nextItems) {
              const parsed = parseSongItem(item.musicResponsiveListItemRenderer);
              if (parsed && !seenTrackIds.has(parsed.id)) {
                seenTrackIds.add(parsed.id);
                tracks.push(parsed);
              }
            }
          }
        } catch {}
      }
    }

    // 2. Parse Artists
    const artists: SearchArtistItem[] = [];
    const seenArtistNames = new Set<string>();

    const artistShelf =
      artistsData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents?.[0]?.musicShelfRenderer;

    if (artistShelf?.contents) {
      for (const item of artistShelf.contents) {
        const parsed = parseArtistItem(item.musicResponsiveListItemRenderer);
        if (parsed && !seenArtistNames.has(parsed.name.toLowerCase())) {
          seenArtistNames.add(parsed.name.toLowerCase());
          artists.push(parsed);
        }
      }
    }

    // 3. Parse Playlists
    const playlists: SearchPlaylistItem[] = [];
    const seenPlaylistIds = new Set<string>();

    const playlistShelf =
      playlistsData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents?.[0]?.musicShelfRenderer;

    if (playlistShelf?.contents) {
      for (const item of playlistShelf.contents) {
        const parsed = parsePlaylistItem(item.musicResponsiveListItemRenderer);
        if (parsed?.id && !seenPlaylistIds.has(parsed.id)) {
          seenPlaylistIds.add(parsed.id);
          playlists.push(parsed);
        }
      }
    }

    // Single pass over overview sections for artists, handles and playlists
    const overviewSections =
      overviewData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents || [];

    let matchedHandleArtist: SearchArtistItem | null = null;

    for (const sec of overviewSections) {
      const shelf = sec.musicShelfRenderer;
      const items = shelf?.contents || (sec.itemSectionRenderer ? [sec.itemSectionRenderer.contents?.[0]] : []);
      if (!items) continue;

      for (const it of items) {
        const item = it?.musicResponsiveListItemRenderer;
        if (!item) continue;

        const isCircle = item.thumbnail?.musicThumbnailRenderer?.thumbnailCrop === "MUSIC_THUMBNAIL_CROP_CIRCLE";
        const sub = extractRunsText(item.flexColumns?.[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs);
        const isArtistOrProfile = isCircle || /nghệ sĩ|hồ sơ|profile|kênh|channel|@/i.test(sub);

        if (isArtistOrProfile) {
          const artist = parseArtistItem(item);
          if (artist) {
            const key = artist.name.toLowerCase();
            if (!seenArtistNames.has(key)) {
              seenArtistNames.add(key);
              artists.push(artist);
              if (isHandleQuery && !matchedHandleArtist) {
                matchedHandleArtist = artist;
              }
            }
          }
        }

        const playlist = parsePlaylistItem(item);
        if (
          playlist?.id &&
          !seenPlaylistIds.has(playlist.id) &&
          /danh sách phát|playlist|album/i.test(playlist.trackCountText || "")
        ) {
          seenPlaylistIds.add(playlist.id);
          playlists.push(playlist);
        }
      }
    }

    // 4. Parse Top Result (Hero card)
    let topResult: SearchTopResult | null = null;
    let cardArtistBrowseId = "";
    const card =
      overviewSections[0]?.musicCardShelfRenderer ||
      overviewData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents?.[0]?.musicCardShelfRenderer;

    if (card) {
      const cardTitle = extractRunsText(card.title?.runs);
      const cardSubtitle = extractRunsText(card.subtitle?.runs);
      const cardThumbs = card.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
      const bestThumb = cardThumbs[cardThumbs.length - 1]?.url || "";
      const videoId =
        card.title?.runs?.[0]?.navigationEndpoint?.watchEndpoint?.videoId ||
        card.buttons?.[0]?.buttonRenderer?.command?.watchEndpoint?.videoId;

      const isArtist = cardSubtitle.toLowerCase().includes("nghệ sĩ");

      let cardDur = 0;
      let cardDurText = "";
      const mDur = cardSubtitle.match(/(\d+:\d+(?::\d+)?)/);
      if (mDur) {
        cardDurText = mDur[1];
        cardDur = parseDuration(mDur[1]);
      }

      // Extract artist browseId from subtitle runs
      for (const r of card.subtitle?.runs || []) {
        const isArt =
          r.navigationEndpoint?.browseEndpoint?.browseEndpointContextSupportedConfigs
            ?.browseEndpointContextMusicConfig?.pageType === "MUSIC_PAGE_TYPE_ARTIST" ||
          (r.navigationEndpoint?.browseEndpoint?.browseId &&
            r.navigationEndpoint.browseEndpoint.browseId.startsWith("UC"));
        if (isArt && r.navigationEndpoint?.browseEndpoint?.browseId) {
          cardArtistBrowseId = r.navigationEndpoint.browseEndpoint.browseId;
          break;
        }
      }

      const subParts = cardSubtitle.split("•").map((s) => s.trim());
      let author = "";
      for (const p of subParts) {
        if (p && !/^(bài hát|video|song|nghệ sĩ|artist|\d+:\d+)$/i.test(p) && !/lượt/i.test(p)) {
          author = p;
          break;
        }
      }
      if (!author) author = subParts[1] || subParts[0] || "";

      if (videoId) {
        topResult = {
          id: videoId,
          title: cardTitle,
          subtitle: cardSubtitle,
          author,
          duration: cardDur,
          durationText: cardDurText,
          thumbnail: bestThumb || `https://i.ytimg.com/vi/${videoId}/hq720.jpg`,
          type: "song",
        };
      } else if (isArtist && !seenArtistNames.has(cardTitle.toLowerCase())) {
        artists.unshift({
          id: card.title?.runs?.[0]?.navigationEndpoint?.browseEndpoint?.browseId || "",
          name: cardTitle,
          subscribers: cardSubtitle,
          thumbnail: bestThumb,
        });
      }
    }

    // Fallback to first track if topResult not found from card
    if (!topResult && tracks.length > 0) {
      const first = tracks[0];
      topResult = {
        id: first.id,
        title: first.title,
        subtitle: `${first.author}${first.durationText ? " • " + first.durationText : ""}`,
        author: first.author,
        duration: first.duration,
        durationText: first.durationText,
        thumbnail: first.thumbnail,
        type: "song",
      };
    }

    // Handle-based search enrichment: if user searched by @handle and a channel was resolved (e.g. @aruca_aiwa -> 愛羽あるか)
    if (isHandleQuery && matchedHandleArtist) {
      const hIdx = artists.findIndex((a) => a.id === matchedHandleArtist?.id);
      if (hIdx > 0) {
        const [h] = artists.splice(hIdx, 1);
        artists.unshift(h);
      }

      if (matchedHandleArtist.id) {
        const channelTracks = await getChannelUploadTracks(matchedHandleArtist.id, matchedHandleArtist.name);
        if (channelTracks.length > 0) {
          tracks.unshift(...channelTracks);
          topResult = {
            id: channelTracks[0].id,
            title: channelTracks[0].title,
            subtitle: `${matchedHandleArtist.name} • ${matchedHandleArtist.subscribers || "Kênh chính thức"}`,
            author: matchedHandleArtist.name,
            duration: channelTracks[0].duration,
            durationText: channelTracks[0].durationText,
            thumbnail: channelTracks[0].thumbnail,
            type: "song",
          };
        }
      }
    }

    // 5. Enrich Artists with artists from matching songs (e.g. Nowlu for "swallows")
    const songArtistCandidateMap = new Map<
      string,
      { name: string; browseId?: string; thumbnail: string; score: number }
    >();
    const qLower = trimmed.toLowerCase();

    // Priority 1: Top Result song's artist
    if (topResult?.type === "song" && topResult.author) {
      const name = cleanArtistName(topResult.author);
      const key = name.toLowerCase();
      if (name && !GENERIC_EXCLUDED_ARTISTS.has(key) && !seenArtistNames.has(key)) {
        songArtistCandidateMap.set(key, {
          name,
          browseId: cardArtistBrowseId || tracks[0]?.artistBrowseId,
          thumbnail: topResult.thumbnail,
          score: 100,
        });
      }
    }

    // Priority 2: Artists of songs matching the query
    for (const track of tracks) {
      if (!track.author) continue;
      const name = cleanArtistName(track.author);
      const key = name.toLowerCase();
      if (!name || GENERIC_EXCLUDED_ARTISTS.has(key) || seenArtistNames.has(key)) continue;

      const t = track.title.toLowerCase();
      const score =
        t === qLower
          ? 60
          : t.startsWith(qLower) || qLower.startsWith(t)
          ? 40
          : t.includes(qLower) || qLower.includes(t)
          ? 25
          : songArtistCandidateMap.size < 2
          ? 10
          : 0;

      if (score > 0) {
        const prev = songArtistCandidateMap.get(key);
        if (!prev || prev.score < score) {
          songArtistCandidateMap.set(key, {
            name,
            browseId: track.artistBrowseId,
            thumbnail: track.thumbnail,
            score,
          });
        }
      }
    }

    const topSongCandidates = Array.from(songArtistCandidateMap.values())
      .sort((a, b) => b.score - a.score)
      .slice(0, 6);

    if (topSongCandidates.length > 0) {
      const resolvedSongArtists = await Promise.all(
        topSongCandidates.map((c) => fetchArtistProfile(c.browseId || "", c.name, c.thumbnail))
      );

      const exactMatchIdx = artists.findIndex((a) => a.name.toLowerCase() === qLower);
      if (exactMatchIdx >= 0) {
        const exactArtist = artists[exactMatchIdx];
        const otherDirect = artists.filter((_, idx) => idx !== exactMatchIdx);
        artists.length = 0;
        artists.push(exactArtist, ...resolvedSongArtists, ...otherDirect);
      } else {
        artists.unshift(...resolvedSongArtists);
      }

      for (const sa of resolvedSongArtists) {
        seenArtistNames.add(sa.name.toLowerCase());
      }
    }

    // Ensure matched handle artist stays at #1 for handle queries
    if (isHandleQuery && matchedHandleArtist) {
      const hIdx = artists.findIndex((a) => a.id === matchedHandleArtist?.id || a.name === matchedHandleArtist?.name);
      if (hIdx > 0) {
        const [h] = artists.splice(hIdx, 1);
        artists.unshift(h);
      }
    }

    if (tracks.length > 0 || artists.length > 0 || playlists.length > 0) {
      return {
        query: trimmed,
        topResult,
        tracks,
        artists,
        playlists,
        results: tracks,
      };
    }
  } catch (err: any) {
    console.warn("[YTM Search Warning]:", err?.message);
  }

  // Fallback to yt-dlp
  const ytDlpTracks = await searchViaYtDlp(trimmed, 50);
  const fallbackArtists: SearchArtistItem[] = [];
  const artistMap = new Map<string, string>();
  for (const t of ytDlpTracks) {
    if (t.author && !artistMap.has(t.author.toLowerCase())) {
      artistMap.set(t.author.toLowerCase(), t.thumbnail);
      fallbackArtists.push({
        id: `artist-${encodeURIComponent(t.author)}`,
        name: t.author,
        subscribers: "Nghệ sĩ",
        thumbnail: t.thumbnail,
      });
    }
  }

  const top =
    ytDlpTracks.length > 0
      ? {
          id: ytDlpTracks[0].id,
          title: ytDlpTracks[0].title,
          subtitle: `${ytDlpTracks[0].author}`,
          author: ytDlpTracks[0].author,
          duration: ytDlpTracks[0].duration,
          durationText: "",
          thumbnail: ytDlpTracks[0].thumbnail,
          type: "song" as const,
        }
      : null;

  return {
    query: trimmed,
    topResult: top,
    tracks: ytDlpTracks,
    artists: fallbackArtists.slice(0, 10),
    playlists: [],
    results: ytDlpTracks,
  };
}

/**
 * Fetch tracks belonging to a playlist
 */
export async function getPlaylistTracks(browseOrPlaylistId: string): Promise<SearchTrackResult[]> {
  if (!browseOrPlaylistId) return [];

  const rawId = browseOrPlaylistId.replace(/^VL/, "");
  const browseId =
    browseOrPlaylistId.startsWith("VL") || browseOrPlaylistId.startsWith("MPREb")
      ? browseOrPlaylistId
      : `VL${browseOrPlaylistId}`;

  const results: SearchTrackResult[] = [];
  const seenTrackIds = new Set<string>();

  try {
    const data = await postYTM("browse", { browseId }, 8000);

    const header =
      data?.contents?.twoColumnBrowseResultsRenderer?.tabs?.[0]?.tabRenderer?.content?.sectionListRenderer?.contents?.[0]?.musicResponsiveHeaderRenderer ||
      data?.header?.musicDetailHeaderRenderer ||
      data?.header?.musicResponsiveHeaderRenderer;
    const headerAuthor =
      header?.straplineTextOne?.runs?.[0]?.text ||
      header?.subtitle?.runs?.find((r: any) => r?.navigationEndpoint?.browseEndpoint?.browseId?.startsWith("UC"))?.text ||
      "";

    // Recursively check all possible sections
    const secList: any[] =
      data?.contents?.twoColumnBrowseResultsRenderer?.secondaryContents?.sectionListRenderer?.contents ||
      data?.contents?.singleColumnBrowseResultsRenderer?.tabs?.[0]?.tabRenderer?.content?.sectionListRenderer?.contents ||
      data?.contents?.twoColumnBrowseResultsRenderer?.tabs?.[0]?.tabRenderer?.content?.sectionListRenderer?.contents ||
      data?.contents?.sectionListRenderer?.contents ||
      [];

    for (const sec of secList) {
      const shelf = sec?.musicPlaylistShelfRenderer || sec?.musicShelfRenderer;
      if (shelf?.contents && Array.isArray(shelf.contents)) {
        for (const item of shelf.contents) {
          const parsed = parseSongItem(item.musicResponsiveListItemRenderer, headerAuthor);
          if (parsed && !seenTrackIds.has(parsed.id)) {
            seenTrackIds.add(parsed.id);
            results.push(parsed);
          }
        }
      }
    }
  } catch (err: any) {
    console.warn("[getPlaylistTracks] Browse error:", err?.message);
  }

  // Fallback: If browse returned 0 items and we have a valid playlist ID, call YouTube Music 'next' endpoint!
  if (results.length === 0 && rawId && !rawId.startsWith("UC")) {
    try {
      const nextData = await postYTM("next", { playlistId: rawId }, 8000);
      const queueItems =
        nextData?.contents?.singleColumnMusicWatchNextResultsRenderer?.tabbedRenderer
          ?.watchNextTabbedResultsRenderer?.tabs?.[0]?.tabRenderer?.content
          ?.musicQueueRenderer?.content?.playlistPanelRenderer?.contents || [];

      for (const item of queueItems) {
        const r = item.playlistPanelVideoRenderer;
        if (!r?.videoId) continue;
        if (!seenTrackIds.has(r.videoId)) {
          seenTrackIds.add(r.videoId);
          const title = r.title?.runs?.[0]?.text || "Track";
          const author = r.shortBylineText?.runs?.map((x: any) => x.text).join("") || "";
          const durText = r.lengthText?.runs?.[0]?.text || "";
          const thumbs = r.thumbnail?.thumbnails || [];
          const thumb = thumbs[thumbs.length - 1]?.url || `https://i.ytimg.com/vi/${r.videoId}/mqdefault.jpg`;
          results.push({
            id: r.videoId,
            title,
            author,
            duration: parseDuration(durText),
            durationText: durText,
            thumbnail: thumb,
            isMusic: true,
          });
        }
      }
    } catch (nextErr: any) {
      console.warn("[getPlaylistTracks] Next fallback error:", nextErr?.message);
    }
  }

  return results;
}

/**
 * Filter to ensure item is music:
 * 1. Has explicit YouTube 'MUSIC' badge.
 * 2. Or matches music terms (cover, bài hát, mv, acoustic, remix, 歌ってみた, 弾き語り, 歌枠...).
 * 3. Filters out non-music videos (vlog, gaming, podcast, chém gió, livestream talk, tutorial...).
 */
export function isMusicContent(title: string, hasMusicTag = false): boolean {
  if (hasMusicTag) return true;
  if (!title) return false;

  const nonMusicPatterns = [
    /\b(vlog|q&a|gaming|gameplay|minecraft|roblox|valorant|pubg|genshin|honkai|unboxing|review|reaction|podcast|tâm sự|chém gió|trò chuyện|thông báo|hướng dẫn|tutorial|highlight|highlights)\b/i,
    /【(雑談|ゲーム|配信|告知|生放送|作業用|振り返り)】/,
  ];
  for (const p of nonMusicPatterns) {
    if (p.test(title)) return false;
  }

  const musicPatterns = [
    /(歌ってみた|弾き語り|語り弾き|歌枠|歌|曲|オリジナル曲|cover|mv|music|song|track|audio|remix|acoustic|official|ost|soundtrack|bài hát|ca khúc|nhạc|hát|karaoke|instrumental|feat\.|ft\.)/i,
    /【(歌|歌枠|cover|mv|original|sing|singing)】/i,
  ];
  for (const p of musicPatterns) {
    if (p.test(title)) return true;
  }

  return false;
}

/**
 * Fetch video uploads from channel uploads playlist (VL + UU...) via YouTube Web Innertube.
 * Returns exact duration, durationText and isMusic (music note) tag!
 */
async function fetchUploadsPlaylist(
  browseId: string,
  fallbackAuthor = ""
): Promise<SearchTrackResult[]> {
  if (!browseId || !browseId.startsWith("UC")) return [];
  const uploadsPlaylistId = "VL" + "UU" + browseId.slice(2);
  try {
    const d = await postYouTubeWeb("browse", { browseId: uploadsPlaylistId }, 6000);
    const isr =
      d?.contents?.twoColumnBrowseResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents?.[0]?.itemSectionRenderer;
    const items = isr?.contents || [];
    const tracks: SearchTrackResult[] = [];
    for (const item of items) {
      const vm = item.lockupViewModel;
      if (!vm) continue;
      const title = vm.metadata?.lockupMetadataViewModel?.title?.content;
      const videoId =
        vm.rendererContext?.commandContext?.onTap?.innertubeCommand?.watchEndpoint?.videoId;
      const overlays = vm.contentImage?.thumbnailViewModel?.overlays || [];
      let durText = "";
      let isMusic = false;
      for (const ov of overlays) {
        const badges = ov.thumbnailBottomOverlayViewModel?.badges || [];
        for (const b of badges) {
          const tb = b.thumbnailBadgeViewModel;
          if (tb?.text) durText = tb.text;
          if (tb?.icon?.sources?.[0]?.clientResource?.imageName === "MUSIC") isMusic = true;
        }
      }

      // Only take items that have a music tag or are considered music
      if (!isMusicContent(title, isMusic)) {
        continue;
      }

      let rowAuthor = "";
      const metadataRows = vm.metadata?.lockupMetadataViewModel?.metadata?.contentMetadataViewModel?.metadataRows || [];
      for (const row of metadataRows) {
        const parts = row.metadataParts || [];
        for (const p of parts) {
          const run = p.text?.runs?.[0]?.text;
          if (run && !run.includes("lượt xem") && !run.includes("views") && !run.includes("trước") && !run.includes("ago")) {
            rowAuthor = run;
          }
        }
      }

      const thumbs = vm.contentImage?.thumbnailViewModel?.image?.sources || [];
      const thumbnail =
        thumbs[thumbs.length - 1]?.url ||
        (videoId ? `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg` : "");
      if (videoId && title) {
        tracks.push({
          id: videoId,
          title,
          author: rowAuthor || fallbackAuthor || "",
          duration: parseDuration(durText),
          durationText: durText,
          thumbnail,
          artistBrowseId: browseId,
          isMusic: true,
        });
      }
    }
    return tracks;
  } catch {
    return [];
  }
}

/**
 * Fetch dedicated artist/channel details, tracks and playlists by browseId (UC...)
 */
export async function getArtistDetails(
  browseId: string,
  fallbackName = ""
): Promise<StructuredSearchResponse> {
  if (!browseId || !browseId.startsWith("UC")) {
    return searchYouTubeMusicStructured(fallbackName || "Nghệ sĩ");
  }

  try {
    const data = await postYTM("browse", { browseId }, 8000);

    const header =
      data?.header?.musicImmersiveHeaderRenderer ||
      data?.header?.musicVisualHeaderRenderer;
    const artistName = cleanArtistName(header?.title?.runs?.[0]?.text || fallbackName || "");
    const subText =
      header?.subscriptionButton?.subscribeButtonRenderer?.subscriberCountText?.runs?.[0]?.text;
    const thumbs = header?.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
    const avatar = thumbs[thumbs.length - 1]?.url || thumbs[0]?.url || "";

    const tabs = data?.contents?.singleColumnBrowseResultsRenderer?.tabs || [];
    const secList: any[] = tabs[0]?.tabRenderer?.content?.sectionListRenderer?.contents || [];

    const tracks: SearchTrackResult[] = [];
    const playlists: SearchPlaylistItem[] = [];
    const seenTrackIds = new Set<string>();
    const seenPlaylistIds = new Set<string>();

    const lowerArtist = artistName.toLowerCase().trim();
    const escapedArtist = lowerArtist.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const artistInTitleRegex = new RegExp(`(\\b|\\()(?:cùng với|ft\\.?|feat\\.?|x|vs\\.?)\\s*.*${escapedArtist}`, "i");
    const artistFirstRegex = new RegExp(`^${escapedArtist}\\s*[-–|:]`, "i");

    const isAuthenticArtistTrack = (t: SearchTrackResult) => {
      const a = (t.author || "").toLowerCase().trim();
      const tit = (t.title || "").toLowerCase().trim();

      // 1. Exclude if someone ELSE covered it ("cover by [someone else]", "covered by [someone else]", etc.)
      const coverByMatch = tit.match(/(?:cover(?:ed)?\s+by|vocal\s*:?|ca\s*sĩ\s*:?|thể\s*hiện\s*:?)\s*([^\s\(\)\[\]【】\/|,]+)/i);
      if (coverByMatch && coverByMatch[1]) {
        const coverSinger = coverByMatch[1].toLowerCase().trim();
        if (!coverSinger.includes(lowerArtist) && !lowerArtist.includes(coverSinger)) {
          return false;
        }
      }

      // Check bracketed cover e.g. "(Hương Ly Cover)" or "[Linh Cover]"
      const bracketCoverMatch = tit.match(/[\(\[\{【]([^\(\[\{【\)\]\}】]+)\s+cover[\)\]\}】]/i);
      if (bracketCoverMatch && bracketCoverMatch[1]) {
        const coverSinger = bracketCoverMatch[1].toLowerCase().trim();
        if (!coverSinger.includes(lowerArtist) && !lowerArtist.includes(coverSinger)) {
          return false;
        }
      }

      // Check "Cover [Song] - [Name]" at end of title
      const endCoverMatch = tit.match(/cover\s*[-–|:]\s*([^\(\[\{【\)\]\}】]+)$/i);
      if (endCoverMatch && endCoverMatch[1]) {
        const coverSinger = endCoverMatch[1].toLowerCase().trim();
        if (!coverSinger.includes(lowerArtist) && !lowerArtist.includes(coverSinger)) {
          return false;
        }
      }

      // 2. Author check
      if (a && a !== "nghệ sĩ" && a !== "kahoii music") {
        const authorTokens = a
          .split(/[,&+/]|(?:\s+(?:và|feat\.?|ft\.?|x|vs\.?)\s+)/i)
          .map((s) => s.trim())
          .filter(Boolean);

        const matchesAuthor = authorTokens.some(
          (token) => token === lowerArtist || token.includes(lowerArtist) || (token.length >= 3 && lowerArtist.includes(token))
        );

        if (matchesAuthor) {
          return true;
        }
      }

      // 3. Title check: Title explicitly states artist is singer/collaborator/performer
      if (artistInTitleRegex.test(tit) || artistFirstRegex.test(tit)) {
        return true;
      }

      return false;
    };

    // 1. Fetch ALL top songs via Section 0's bottomEndpoint ("Xem tất cả" / View All)
    const s0 = secList[0]?.musicShelfRenderer;
    const be = s0?.bottomEndpoint?.browseEndpoint;
    if (be?.browseId) {
      try {
        const allSongsData = await postYTM("browse", { browseId: be.browseId, params: be.params }, 8000);
        const sc = allSongsData?.contents?.singleColumnBrowseResultsRenderer;
        const songSecs: any[] =
          sc?.tabs?.[0]?.tabRenderer?.content?.sectionListRenderer?.contents ||
          allSongsData?.contents?.twoColumnBrowseResultsRenderer?.secondaryContents?.sectionListRenderer?.contents ||
          allSongsData?.contents?.sectionListRenderer?.contents ||
          [];
        const songShelf = songSecs[0]?.musicShelfRenderer || songSecs[0]?.musicPlaylistShelfRenderer;
        const items = songShelf?.contents || [];
        for (const it of items) {
          const parsed = parseSongItem(it.musicResponsiveListItemRenderer, artistName);
          if (parsed && !seenTrackIds.has(parsed.id)) {
            if (!parsed.author || parsed.author === "Nghệ sĩ") {
              parsed.author = artistName;
            }
            parsed.artistBrowseId = browseId;
            if (isAuthenticArtistTrack(parsed)) {
              seenTrackIds.add(parsed.id);
              tracks.push(parsed);
            }
          }
        }
      } catch (err: any) {
        console.warn("[getArtistDetails] All songs fetch error:", err?.message);
      }
    }

    // 2. Extract songs and playlists from artist page shelves
    for (let i = 0; i < secList.length; i++) {
      const s = secList[i];
      const shelf = s.musicShelfRenderer || s.musicCarouselShelfRenderer;
      if (!shelf) continue;

      const shelfTitle = (
        shelf.header?.musicHeaderRenderer?.title?.runs?.[0]?.text ||
        shelf.header?.musicCarouselShelfBasicHeaderRenderer?.title?.runs?.[0]?.text ||
        ""
      ).toLowerCase();

      // STRICT EXCLUSION: Skip Similar Artists ("Người hâm mộ có thể cũng thích" / "Fans also like"), Featured on ("Xuất hiện trên"), etc.
      if (
        shelfTitle.includes("người hâm mộ") ||
        shelfTitle.includes("fans also like") ||
        shelfTitle.includes("tương tự") ||
        shelfTitle.includes("similar") ||
        shelfTitle.includes("nghệ sĩ liên quan") ||
        shelfTitle.includes("related artists") ||
        shelfTitle.includes("xuất hiện trên") ||
        shelfTitle.includes("featured on")
      ) {
        continue;
      }

      const items = shelf.contents || [];
      for (const it of items) {
        const rend = it.musicResponsiveListItemRenderer || it.musicTwoRowItemRenderer;
        if (!rend) continue;

        const title = extractRunsText(
          rend.flexColumns?.[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs ||
          rend.title?.runs
        );
        const sub = extractRunsText(
          rend.flexColumns?.[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs ||
          rend.subtitle?.runs
        );

        const videoId =
          rend.navigationEndpoint?.watchEndpoint?.videoId ||
          rend.playlistItemData?.videoId ||
          rend.overlay?.musicItemThumbnailOverlayRenderer?.content?.musicPlayButtonRenderer
            ?.playNavigationEndpoint?.watchEndpoint?.videoId;

        const playlistId =
          rend.navigationEndpoint?.browseEndpoint?.browseId ||
          rend.overlay?.musicItemThumbnailOverlayRenderer?.content?.musicPlayButtonRenderer
            ?.playNavigationEndpoint?.watchEndpoint?.playlistId;

        const itemThumbs =
          rend.thumbnailRenderer?.musicThumbnailRenderer?.thumbnail?.thumbnails ||
          rend.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails ||
          rend.thumbnail?.thumbnails ||
          [];
        const thumbnail =
          itemThumbs[itemThumbs.length - 1]?.url ||
          (videoId ? `https://i.ytimg.com/vi/${videoId}/mqdefault.jpg` : "");

        // A. Video / Song item (belongs to tracks)
        if (videoId && title && !seenTrackIds.has(videoId)) {
          const parsed = parseSongItem(rend, artistName);
          if (parsed && !seenTrackIds.has(parsed.id)) {
            if (!parsed.author || parsed.author === "Nghệ sĩ") {
              parsed.author = artistName;
            }
            parsed.artistBrowseId = browseId;
            if (isAuthenticArtistTrack(parsed)) {
              seenTrackIds.add(parsed.id);
              tracks.push(parsed);
            }
          } else if (!parsed) {
            const rawItem: SearchTrackResult = {
              id: videoId,
              title,
              author: sub ? sub.split("•")[0].trim() : artistName,
              duration: 0,
              durationText: "",
              thumbnail,
              artistBrowseId: browseId,
              isMusic: true,
            };
            if (isAuthenticArtistTrack(rawItem)) {
              seenTrackIds.add(videoId);
              tracks.push(rawItem);
            }
          }
        }
        // B. Playlist or Album item (MUST NOT be a channel/artist UC... or radio mix RD...)
        else if (
          playlistId &&
          title &&
          !videoId &&
          !playlistId.startsWith("UC") &&
          !playlistId.startsWith("RD") &&
          (playlistId.startsWith("VL") ||
            playlistId.startsWith("PL") ||
            playlistId.startsWith("MPREb") ||
            playlistId.startsWith("OLAK5uy_")) &&
          !seenPlaylistIds.has(playlistId)
        ) {
          // EXCLUDE single tracks ("Đĩa đơn" / "Single") - they belong to songs/tracks, not playlists!
          const subLower = (sub || "").toLowerCase();
          if (subLower.includes("đĩa đơn") || subLower.includes("single")) {
            continue;
          }
          seenPlaylistIds.add(playlistId);
          playlists.push({
            id: playlistId,
            title,
            author: artistName,
            trackCountText: sub,
            thumbnail,
          });
        }
      }
    }

    // 3. Fallback only if tracks is STILL empty: try channel uploads playlist
    if (tracks.length === 0) {
      const channelUploads = await fetchUploadsPlaylist(browseId, artistName);
      if (channelUploads && channelUploads.length > 0) {
        channelUploads.forEach((t) => {
          if (!t.author || t.author === "Nghệ sĩ") t.author = artistName;
          t.artistBrowseId = browseId;
          if (isAuthenticArtistTrack(t) && !seenTrackIds.has(t.id)) {
            seenTrackIds.add(t.id);
            tracks.push(t);
          }
        });
      }
    }

    // 4. Last resort: search, but STRICTLY filter by artist name to prevent other artists leaking in
    if (tracks.length === 0) {
      const searchRes = await searchYouTubeMusicStructured(artistName);
      const filtered = (searchRes.tracks || []).filter((t) => isAuthenticArtistTrack(t));
      filtered.forEach((t) => {
        if (!seenTrackIds.has(t.id)) {
          seenTrackIds.add(t.id);
          tracks.push(t);
        }
      });
    }

    const artistItem: SearchArtistItem = {
      id: browseId,
      name: artistName,
      subscribers: subText ? `Nghệ sĩ • ${subText}` : "Nghệ sĩ",
      thumbnail: avatar,
    };

    return {
      query: artistName,
      topResult: tracks[0]
        ? {
            id: tracks[0].id,
            title: tracks[0].title,
            subtitle: `${artistName}${tracks[0].durationText ? " • " + tracks[0].durationText : ""}`,
            author: artistName,
            duration: tracks[0].duration,
            durationText: tracks[0].durationText,
            thumbnail: tracks[0].thumbnail,
            type: "song",
            isMusic: tracks[0].isMusic,
          }
        : null,
      tracks,
      artists: [artistItem],
      playlists,
      results: tracks,
    };
  } catch {
    return searchYouTubeMusicStructured(fallbackName);
  }
}

/**
 * Backward compatibility function
 */
export async function searchYouTubeTracks(query: string): Promise<SearchTrackResult[]> {
  const structured = await searchYouTubeMusicStructured(query);
  return structured.tracks;
}

export const BOLERO_UNWANTED_REGEX =
  /bolero|nhạc vàng|trữ tình|nhạc lính|nhạc sống|quê hương|đám cưới|hát xẩm|xẩm đời|chachacha|nhạc chế|sáo trúc|tây bắc|thôn quê|dân ca|tuyệt phẩm|tâm trạng ai nghe cũng khóc|đò bỏ bến|kiếp người|bể dâu|nghèo|bạc bẽo|rumba|phố nhạc việt|lk nhạc|liên khúc nhạc|liên khúc ca nhạc|liên khúc bolero|tone nam|tone nữ|karaoke|nhạc thiền|nhạc phật|nhạc niệm phật/i;

export function isUnwantedTrack(title: string, author: string): boolean {
  if (!title && !author) return false;
  const str = `${title} ${author}`.toLowerCase();
  return BOLERO_UNWANTED_REGEX.test(str);
}

interface OfficialTrendingResult {
  tracks: SearchTrackResult[];
  artists: SearchArtistItem[];
  timestamp: number;
}

let cachedOfficialTrending: OfficialTrendingResult | null = null;
const CACHE_TTL_MS = 15 * 60 * 1000; // 15 minutes

export async function getOfficialTrendingTracks(): Promise<{ tracks: SearchTrackResult[]; artists: SearchArtistItem[] }> {
  const now = Date.now();
  if (cachedOfficialTrending && now - cachedOfficialTrending.timestamp < CACHE_TTL_MS && cachedOfficialTrending.tracks.length > 0) {
    return {
      tracks: [...cachedOfficialTrending.tracks],
      artists: [...cachedOfficialTrending.artists],
    };
  }

  const chartPlaylistIds = [
    "VLOLAK5uy_lEos0zuYBvGC9C0FSGG3pZ6gO4a82P6zg", // 20 video thịnh hành tại Việt Nam (Official Top 20 Trending VN)
    "VLPL4fGSI1pDJn4FPCRZtojwqQro5GPY6cuV",         // 100 video nhạc hàng đầu tại Việt Nam (Official Top 100 VN)
    "VLPL4fGSI1pDJn57DkisEwlIpcs9FAt5yudJ",         // Các video nhạc hàng đầu trong ngày – Việt Nam
  ];

  const tracks: SearchTrackResult[] = [];
  const artists: SearchArtistItem[] = [];
  const seenIds = new Set<string>();
  const seenArtistNames = new Set<string>();

  for (const plId of chartPlaylistIds) {
    try {
      const res = await postYTM("browse", { browseId: plId }, 8000);
      const twoCol = res?.contents?.twoColumnBrowseResultsRenderer?.secondaryContents?.sectionListRenderer?.contents?.[0];
      const items = twoCol?.musicPlaylistShelfRenderer?.contents || [];

      for (const it of items) {
        const r = it.musicResponsiveListItemRenderer;
        if (!r) continue;

        const title = r.flexColumns?.[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs?.[0]?.text || "";
        const authorRuns = r.flexColumns?.[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs || [];
        const author = authorRuns.map((x: any) => x.text || "").join("").trim();
        const videoId =
          r.playlistItemData?.videoId ||
          r.flexColumns?.[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs?.[0]?.navigationEndpoint?.watchEndpoint?.videoId;

        if (!videoId || !title || seenIds.has(videoId)) continue;

        // Discard any bolero / unwanted tracks
        if (isUnwantedTrack(title, author)) continue;

        seenIds.add(videoId);
        const durationText =
          r.fixedColumns?.[0]?.musicResponsiveListItemFixedColumnRenderer?.text?.runs?.[0]?.text || "3:30";
        const thumbs = r.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
        const thumbnail =
          thumbs[thumbs.length - 1]?.url || `https://i.ytimg.com/vi/${videoId}/hq720.jpg`;

        let artistBrowseId: string | undefined = undefined;
        for (const ar of authorRuns) {
          const bid = ar.navigationEndpoint?.browseEndpoint?.browseId;
          if (bid && bid.startsWith("UC")) {
            artistBrowseId = bid;
            const aName = (ar.text || "").trim();
            if (aName && aName.length > 1 && !seenArtistNames.has(aName.toLowerCase())) {
              seenArtistNames.add(aName.toLowerCase());
              artists.push({
                id: bid,
                name: aName,
                subscribers: "Nghệ sĩ",
                thumbnail,
              });
            }
          }
        }

        tracks.push({
          id: videoId,
          title,
          author,
          duration: parseDuration(durationText),
          durationText,
          thumbnail,
          artistBrowseId,
          isMusic: true,
        });
      }
    } catch (e: any) {
      console.warn(`[getOfficialTrendingTracks] Error fetching chart ${plId}:`, e.message);
    }
  }

  if (tracks.length > 0) {
    cachedOfficialTrending = { tracks, artists, timestamp: now };
    return { tracks: [...tracks], artists: [...artists] };
  }

  // Fallback if charts unavailable
  const fallbackQuery = "top vpop hits";
  const fallbackRes = await searchYouTubeMusicStructured(fallbackQuery);
  const cleanFallback = fallbackRes.tracks.filter((t) => !isUnwantedTrack(t.title, t.author));
  return { tracks: cleanFallback, artists: fallbackRes.artists };
}
