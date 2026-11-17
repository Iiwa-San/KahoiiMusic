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

function requestYTM(query?: string, params?: string, continuation?: string): Promise<any> {
  return new Promise((resolve) => {
    const payload = JSON.stringify({
      query: continuation ? undefined : query,
      params: continuation ? undefined : params,
      continuation,
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
      "https://music.youtube.com/youtubei/v1/search",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Referer: "https://music.youtube.com/",
          Origin: "https://music.youtube.com",
        },
        timeout: 9000,
      },
      (res) => {
        let body = "";
        res.on("data", (chunk) => (body += chunk));
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
    req.write(payload);
    req.end();
  });
}

function parseSongItem(renderer: any): SearchTrackResult | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const titleCol = flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs;
  const title = extractRunsText(titleCol);
  if (!title) return null;

  const descRuns = flexCols[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs || [];
  const author = descRuns[0]?.text || "Nghệ sĩ";

  let duration = 0;
  let durationText = "";
  for (let i = descRuns.length - 1; i >= 0; i--) {
    const txt = descRuns[i]?.text?.trim();
    if (txt && /^\d+:\d+(?::\d+)?$/.test(txt)) {
      durationText = txt;
      duration = parseDuration(txt);
      break;
    }
  }

  // Check fixedColumns for duration (standard in YouTube Music playlist items)
  if (!durationText) {
    const fixedCols = renderer.fixedColumns || [];
    for (const fCol of fixedCols) {
      const runs = fCol?.musicResponsiveListItemFixedColumnRenderer?.text?.runs || [];
      for (const r of runs) {
        const txt = r?.text?.trim();
        if (txt && /^\d+:\d+(?::\d+)?$/.test(txt)) {
          durationText = txt;
          duration = parseDuration(txt);
          break;
        }
      }
      if (durationText) break;
    }
  }

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

  return { id: videoId, title, author, duration, durationText, thumbnail };
}

function parseArtistItem(renderer: any): SearchArtistItem | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const name = extractRunsText(
    flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs
  );
  if (!name) return null;

  const subText = extractRunsText(
    flexCols[1]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs
  );
  const thumbs = renderer.thumbnail?.musicThumbnailRenderer?.thumbnail?.thumbnails || [];
  const thumbnail = thumbs[thumbs.length - 1]?.url || "";
  const id = renderer.navigationEndpoint?.browseEndpoint?.browseId || "";

  return { id, name, subscribers: subText, thumbnail };
}

function parsePlaylistItem(renderer: any): SearchPlaylistItem | null {
  if (!renderer) return null;
  const flexCols = renderer.flexColumns || [];
  const title = extractRunsText(
    flexCols[0]?.musicResponsiveListItemFlexColumnRenderer?.text?.runs
  );
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
    "D:/KahoiiProjects/MusicApp/KahoiiMusic/yt-dlp.exe",
    path.resolve(process.cwd(), "yt-dlp.exe"),
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
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
            const duration =
              durRaw && !isNaN(Number(durRaw)) ? Math.round(Number(durRaw)) : 0;
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
    // Run 4 queries in parallel for high speed (~700ms total)
    const [overviewData, songsData, artistsData, playlistsData] = await Promise.all([
      requestYTM(trimmed, undefined),
      requestYTM(trimmed, "EgWKAQIIAWoSEAUQAxAJEAQQChAVEBAQERAO"), // Songs filter
      requestYTM(trimmed, "EgWKAQIgAWoSEAUQAxAJEAQQChAVEBAQERAO"), // Artists filter
      requestYTM(trimmed, "EgeKAQQoAEABahIQBRADEAkQBBAKEBUQEBAREA4%3D"), // Playlists filter
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
          const contData = await requestYTM(undefined, undefined, nextCont);
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

    // Also check overview sections for artists
    const overviewSections =
      overviewData?.contents?.tabbedSearchResultsRenderer?.tabs?.[0]?.tabRenderer?.content
        ?.sectionListRenderer?.contents || [];

    for (const sec of overviewSections) {
      const item = sec.itemSectionRenderer?.contents?.[0]?.musicResponsiveListItemRenderer;
      if (item) {
        const parsed = parseArtistItem(item);
        if (
          parsed &&
          parsed.subscribers?.toLowerCase().includes("nghệ sĩ") &&
          !seenArtistNames.has(parsed.name.toLowerCase())
        ) {
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
        if (parsed && parsed.id && !seenPlaylistIds.has(parsed.id)) {
          seenPlaylistIds.add(parsed.id);
          playlists.push(parsed);
        }
      }
    }

    // Also check overview sections for playlists
    for (const sec of overviewSections) {
      const item = sec.itemSectionRenderer?.contents?.[0]?.musicResponsiveListItemRenderer;
      if (item) {
        const parsed = parsePlaylistItem(item);
        if (
          parsed &&
          (parsed.trackCountText?.toLowerCase().includes("danh sách phát") ||
            parsed.trackCountText?.toLowerCase().includes("playlist") ||
            parsed.trackCountText?.toLowerCase().includes("album")) &&
          !seenPlaylistIds.has(parsed.id)
        ) {
          seenPlaylistIds.add(parsed.id);
          playlists.push(parsed);
        }
      }
    }

    // 4. Parse Top Result (Hero card)
    let topResult: SearchTopResult | null = null;
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

      // Extract duration from subtitle if available
      let cardDur = 0;
      let cardDurText = "";
      const mDur = cardSubtitle.match(/(\d+:\d+(?::\d+)?)/);
      if (mDur) {
        cardDurText = mDur[1];
        cardDur = parseDuration(mDur[1]);
      }

      // Extract author
      const subParts = cardSubtitle.split("•").map((s) => s.trim());
      const author = subParts[1] || subParts[0] || "Nghệ sĩ";

      // If top card is a song and has videoId:
      if (videoId) {
        topResult = {
          id: videoId,
          title: cardTitle,
          subtitle: cardSubtitle,
          author,
          duration: cardDur,
          durationText: cardDurText,
          thumbnail: bestThumb || `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
          type: "song",
        };
      } else if (isArtist) {
        // If top card is an artist, we add the artist to the top of artists if not present
        if (!seenArtistNames.has(cardTitle.toLowerCase())) {
          artists.unshift({
            id: card.title?.runs?.[0]?.navigationEndpoint?.browseEndpoint?.browseId || "",
            name: cardTitle,
            subscribers: cardSubtitle,
            thumbnail: bestThumb,
          });
        }
      }
    }

    // If no song topResult from card, pick the first track from tracks list
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
        id: `artist-${t.author}`,
        name: t.author,
        subscribers: "Nghệ sĩ",
        thumbnail: t.thumbnail,
      });
    }
  }

  const top = ytDlpTracks.length > 0 ? {
    id: ytDlpTracks[0].id,
    title: ytDlpTracks[0].title,
    subtitle: `${ytDlpTracks[0].author}`,
    author: ytDlpTracks[0].author,
    duration: ytDlpTracks[0].duration,
    durationText: "",
    thumbnail: ytDlpTracks[0].thumbnail,
    type: "song" as const,
  } : null;

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
  const browseId = browseOrPlaylistId.startsWith("VL")
    ? browseOrPlaylistId
    : `VL${browseOrPlaylistId}`;

  return new Promise((resolve) => {
    const payload = JSON.stringify({
      browseId,
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
      "https://music.youtube.com/youtubei/v1/browse",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Referer: "https://music.youtube.com/",
        },
        timeout: 9000,
      },
      (res) => {
        let body = "";
        res.on("data", (c) => (body += c));
        res.on("end", () => {
          try {
            const data = JSON.parse(body);
            const sec =
              data.contents?.twoColumnBrowseResultsRenderer?.secondaryContents
                ?.sectionListRenderer?.contents?.[0]?.musicPlaylistShelfRenderer;
            const items = sec?.contents || [];
            const results: SearchTrackResult[] = [];
            for (const item of items) {
              const parsed = parseSongItem(item.musicResponsiveListItemRenderer);
              if (parsed) results.push(parsed);
            }
            resolve(results);
          } catch {
            resolve([]);
          }
        });
      }
    );

    req.on("error", () => resolve([]));
    req.on("timeout", () => {
      req.destroy();
      resolve([]);
    });
    req.write(payload);
    req.end();
  });
}

/**
 * Backward compatibility function
 */
export async function searchYouTubeTracks(query: string): Promise<SearchTrackResult[]> {
  const structured = await searchYouTubeMusicStructured(query);
  return structured.tracks;
}

export async function getRelatedVideos(
  videoId: string,
  fallbackQuery?: string
): Promise<SearchTrackResult[]> {
  const results: SearchTrackResult[] = [];
  const seen = new Set<string>([videoId]);

  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 7000);
    const res = await fetch("https://www.youtube.com/youtubei/v1/next", {
      method: "POST",
      signal: controller.signal,
      headers: {
        "Content-Type": "application/json",
        "User-Agent":
          "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
      },
      body: JSON.stringify({
        context: {
          client: {
            clientName: "WEB",
            clientVersion: "2.20241101.00.00",
            hl: "vi",
            gl: "VN",
          },
        },
        videoId,
      }),
    });
    clearTimeout(timer);

    if (res.ok) {
      const data: any = await res.json();
      const rawItems: any[] =
        data?.contents?.twoColumnWatchNextResults?.secondaryResults?.secondaryResults?.results || [];

      for (const entry of rawItems) {
        if (entry?.lockupViewModel) {
          const lvm = entry.lockupViewModel;
          const id = lvm.contentId;
          const title = lvm.metadata?.lockupMetadataViewModel?.title?.content;
          if (!id || !title || seen.has(id) || lvm.contentType === "LOCKUP_CONTENT_TYPE_PLAYLIST") {
            continue;
          }
          seen.add(id);

          const author =
            lvm.metadata?.lockupMetadataViewModel?.metadata?.contentMetadataViewModel
              ?.metadataRows?.[0]?.metadataParts?.[0]?.text?.content || "YouTube";

          let durationText = "";
          const overlays: any[] = lvm.contentImage?.thumbnailViewModel?.overlays || [];
          for (const ov of overlays) {
            const badges: any[] =
              ov?.thumbnailOverlayBadgeViewModel?.thumbnailBadges ||
              ov?.thumbnailBottomOverlayViewModel?.badges ||
              [];
            for (const b of badges) {
              const txt = b?.thumbnailBadgeViewModel?.text;
              if (txt && /^\d+:\d{2}(:\d{2})?$/.test(txt.trim())) {
                durationText = txt.trim();
                break;
              }
            }
            if (durationText) break;
          }

          results.push({
            id,
            title,
            author,
            duration: parseDuration(durationText),
            durationText,
            thumbnail: `https://i.ytimg.com/vi/${id}/mqdefault.jpg`,
          });
        } else if (entry?.compactVideoRenderer) {
          const cvr = entry.compactVideoRenderer;
          const id = cvr.videoId;
          const title = cvr.title?.simpleText || cvr.title?.runs?.[0]?.text;
          if (!id || !title || seen.has(id)) continue;
          seen.add(id);

          const author =
            cvr.longBylineText?.runs?.[0]?.text ||
            cvr.shortBylineText?.runs?.[0]?.text ||
            "YouTube";
          const durationText = cvr.lengthText?.simpleText || "";

          results.push({
            id,
            title,
            author,
            duration: parseDuration(durationText),
            durationText,
            thumbnail: `https://i.ytimg.com/vi/${id}/mqdefault.jpg`,
          });
        }
      }
    }
  } catch (err) {
    console.warn("[RelatedVideos] YouTube next API warning:", err);
  }

  if (results.length < 3 && fallbackQuery) {
    try {
      const searched = await searchYouTubeTracks(fallbackQuery);
      for (const s of searched) {
        if (!seen.has(s.id)) {
          seen.add(s.id);
          results.push(s);
        }
      }
    } catch {}
  }

  return results;
}
