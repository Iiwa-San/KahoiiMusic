import https from "https";

export interface LyricLine {
  time: number;
  text: string;
  romanized?: string;
  translation?: string;
}

export interface LyricsResponse {
  syncedLyrics: string | null;
  plainLyrics: string | null;
  instrumental: boolean;
  detectedLang?: "ja" | "zh" | null;
  lines?: LyricLine[];
}

interface SearchCandidate {
  track: string;
  artist: string;
}

// Memory cache to respond in 0ms for repeated requests
const lyricsMemoryCache = new Map<string, LyricsResponse>();

/**
 * Fetch synchronized or plain lyrics:
 * 1. Multi-candidate LRCLIB search (exact match + cleaned variants)
 * 2. Fallback to YouTube Music InnerTube Official Lyrics
 * 3. Language detection & Batch Romanization / Translation (Romaji for JA, Pinyin for ZH, Vietnamese translation for others)
 */
export async function fetchLyrics(
  title: string,
  artist?: string,
  duration?: number,
  videoId?: string
): Promise<LyricsResponse> {
  const cleanT = (title || "").trim();
  const cleanA = (artist || "").trim();
  const vid = (videoId || "").trim();

  if (!cleanT && !vid) {
    return { syncedLyrics: null, plainLyrics: null, instrumental: false, lines: [] };
  }

  const cacheKey = `${vid || cleanT.toLowerCase()}::${cleanA.toLowerCase()}::${Math.round(duration || 0)}`;
  if (lyricsMemoryCache.has(cacheKey)) {
    return lyricsMemoryCache.get(cacheKey)!;
  }

  let result: LyricsResponse | null = null;

  // 1. Try LRCLIB with smart multi-candidate search
  try {
    result = await tryFetchFromLrclib(cleanT, cleanA, duration);
  } catch (err) {
    console.warn("[Lyrics] LRCLIB fetch error:", err);
  }

  // 2. If LRCLIB failed or returned no lyrics, fallback to YouTube Music Official Lyrics via InnerTube
  if ((!result || (!result.syncedLyrics && !result.plainLyrics)) && vid) {
    try {
      const ytmLyrics = await tryFetchFromYouTubeMusic(vid);
      if (ytmLyrics) {
        result = {
          syncedLyrics: null,
          plainLyrics: ytmLyrics,
          instrumental: false,
        };
      }
    } catch (err) {
      console.warn("[Lyrics] YouTube Music lyrics error:", err);
    }
  }

  if (!result || (!result.syncedLyrics && !result.plainLyrics)) {
    const emptyRes: LyricsResponse = {
      syncedLyrics: null,
      plainLyrics: null,
      instrumental: false,
      lines: [],
    };
    lyricsMemoryCache.set(cacheKey, emptyRes);
    return emptyRes;
  }

  // 3. Parse lines
  let lines: LyricLine[] = [];
  if (result.syncedLyrics) {
    lines = parseLrcToLines(result.syncedLyrics);
  } else if (result.plainLyrics) {
    lines = parsePlainTextToLines(result.plainLyrics);
  }

  // 4. Romanization & Translation (Pinyin, Romaji, Vietnamese translation)
  if (lines.length > 0) {
    try {
      const { enrichedLines, detectedLang } = await enrichLinesWithRomanizationAndTranslation(lines);
      result.lines = enrichedLines;
      result.detectedLang = detectedLang;
    } catch (err) {
      console.warn("[Lyrics] Romanization error:", err);
      result.lines = lines;
    }
  } else {
    result.lines = [];
  }

  lyricsMemoryCache.set(cacheKey, result);
  return result;
}

// -------------------------------------------------------------
// LRCLIB Fetching with smart candidates
// -------------------------------------------------------------

function buildSearchCandidates(title: string, artist: string): SearchCandidate[] {
  const list: SearchCandidate[] = [];
  if (!title) return list;

  const cleanT = cleanTitleForSearch(title);
  const cleanA = cleanArtistForSearch(artist);

  // Check hashtag in title, e.g. #KingGnu
  const mHash = title.match(/#([^\s#]+)/);
  let hashArtist = cleanA;
  if (mHash && !cleanA) {
    hashArtist = cleanArtistForSearch(mHash[1]);
  }

  // 1. Japanese quotes: 「Track」 or 【Track】
  const mQuotes = title.match(/[「『【]([^」』】]+)[」』】]/);
  if (mQuotes) {
    const inner = mQuotes[1].trim();
    if (inner.includes("/")) {
      const parts = inner.split("/");
      list.push({ track: parts[0].trim(), artist: parts[1].trim() });
      list.push({ track: parts[1].trim(), artist: parts[0].trim() });
    } else {
      list.push({ track: inner, artist: cleanA || hashArtist });
    }
  }

  // 2. Separators: " - ", " | ", " / ", " – "
  const separators = [" - ", " | ", " / ", " – ", " — "];
  for (const sep of separators) {
    if (cleanT.includes(sep)) {
      const parts = cleanT.split(sep);
      if (parts.length >= 2) {
        const p0 = parts[0].trim();
        const p1 = parts[1].trim();
        list.push({ track: p1, artist: p0 });
        list.push({ track: p0, artist: p1 });
        if (cleanA) {
          list.push({ track: p0, artist: cleanA });
          list.push({ track: p1, artist: cleanA });
        }
      }
      break;
    }
  }

  // 3. Featured artist (feat. / ft.)
  const mFeat = title.match(/(?:feat\.|ft\.|featuring)\s*([^/|()\[\]–—]+)/i);
  if (mFeat) {
    const featArtist = mFeat[1].trim();
    if (featArtist && cleanA) {
      list.push({ track: cleanT, artist: `${cleanA} feat. ${featArtist}` });
      list.push({ track: cleanT, artist: `${cleanA} & ${featArtist}` });
    }
  }

  // 4. Default clean track with clean artist
  if (cleanT) {
    if (cleanA) {
      if (cleanT.toLowerCase().includes(cleanA.toLowerCase())) {
        const withoutA = cleanT.replace(new RegExp(cleanA, "gi"), "").replace(/^[\s\-_/|–—]+|[\s\-_/|–—]+$/g, "").trim();
        if (withoutA) {
          list.push({ track: withoutA, artist: cleanA });
        }
      }
      list.push({ track: cleanT, artist: cleanA });
    }
    list.push({ track: cleanT, artist: "" });
  }

  // Deduplicate
  const seen = new Set<string>();
  const unique: SearchCandidate[] = [];
  for (const c of list) {
    const key = `${c.track.toLowerCase()}||${c.artist.toLowerCase()}`;
    if (!seen.has(key) && c.track) {
      seen.add(key);
      unique.push(c);
    }
  }
  return unique;
}

async function tryFetchFromLrclib(title: string, artist: string, duration?: number): Promise<LyricsResponse | null> {
  const candidates = buildSearchCandidates(title, artist);
  if (candidates.length === 0) return null;

  // Pass 1: Try exact match on /api/get for top candidates
  for (const cand of candidates.slice(0, 3)) {
    if (!cand.artist) continue;
    try {
      const url = new URL("https://lrclib.net/api/get");
      url.searchParams.set("track_name", cand.track);
      url.searchParams.set("artist_name", cand.artist);
      if (duration && duration > 0) {
        url.searchParams.set("duration", Math.round(duration).toString());
      }
      const res = await fetch(url.toString(), {
        headers: { "User-Agent": "KahoiiMusic-Web/1.0 (https://github.com/Iiwa-San/KahoiiMusic)" },
      });
      if (res.ok) {
        const data = (await res.json()) as any;
        if (data.syncedLyrics || data.plainLyrics) {
          return {
            syncedLyrics: data.syncedLyrics || null,
            plainLyrics: data.plainLyrics || null,
            instrumental: data.instrumental || false,
          };
        }
      }
    } catch {}
  }

  // Pass 2: Try search on /api/search with best candidate query
  for (const cand of candidates.slice(0, 2)) {
    try {
      const q = cand.artist ? `${cand.track} ${cand.artist}` : cand.track;
      const searchUrl = `https://lrclib.net/api/search?q=${encodeURIComponent(q)}`;
      const res = await fetch(searchUrl, {
        headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
      });
      if (res.ok) {
        const list = (await res.json()) as any[];
        if (Array.isArray(list) && list.length > 0) {
          // If duration is provided, find item with closest duration (+- 4s)
          let match = list.find((item) => {
            if (!duration || duration <= 0) return true;
            return item.duration && Math.abs(item.duration - duration) <= 4;
          });
          if (!match) {
            match = list.find((item) => item.syncedLyrics) || list[0];
          }
          if (match && (match.syncedLyrics || match.plainLyrics)) {
            return {
              syncedLyrics: match.syncedLyrics || null,
              plainLyrics: match.plainLyrics || null,
              instrumental: match.instrumental || false,
            };
          }
        }
      }
    } catch {}
  }

  return null;
}

// -------------------------------------------------------------
// YouTube Music InnerTube Official Lyrics Fallback
// -------------------------------------------------------------

function postJson(hostname: string, path: string, payload: any): Promise<any> {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify(payload);
    const req = https.request(
      {
        hostname,
        path,
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        },
      },
      (res) => {
        let body = "";
        res.on("data", (chunk) => (body += chunk));
        res.on("end", () => {
          try {
            resolve(JSON.parse(body));
          } catch {
            resolve(body);
          }
        });
      }
    );
    req.on("error", reject);
    req.write(data);
    req.end();
  });
}

async function tryFetchFromYouTubeMusic(videoId: string): Promise<string | null> {
  if (!videoId || videoId.length !== 11) return null;

  const nextRes = await postJson("music.youtube.com", "/youtubei/v1/next", {
    context: {
      client: {
        clientName: "WEB_REMIX",
        clientVersion: "1.20230515.01.00",
        hl: "vi",
        gl: "VN",
      },
    },
    videoId,
  });

  const raw = JSON.stringify(nextRes);
  const m = raw.match(/"browseId"\s*:\s*"(MPLY[a-zA-Z0-9_\-]+)"/);
  if (!m || !m[1]) return null;

  const browseRes = await postJson("music.youtube.com", "/youtubei/v1/browse", {
    context: {
      client: {
        clientName: "WEB_REMIX",
        clientVersion: "1.20230515.01.00",
        hl: "vi",
        gl: "VN",
      },
    },
    browseId: m[1],
  });

  const runs =
    browseRes?.contents?.sectionListRenderer?.contents?.[0]?.musicDescriptionShelfRenderer?.description?.runs ||
    browseRes?.contents?.sectionListRenderer?.contents?.[0]?.musicDescriptionShelfRenderer?.runs;

  if (Array.isArray(runs) && runs.length > 0) {
    const text = runs.map((r: any) => r.text || "").join("").trim();
    return text || null;
  }

  return null;
}

// -------------------------------------------------------------
// Parsing LRC and Plain Text into Lines
// -------------------------------------------------------------

function parseLrcToLines(lrc: string): LyricLine[] {
  const lines: LyricLine[] = [];
  const regex = /\[(\d{1,2}):(\d{1,2})(?:\.(\d{1,3}))?\](.*)/;

  for (const rawLine of lrc.split("\n")) {
    const match = rawLine.match(regex);
    if (match) {
      const minutes = parseInt(match[1], 10);
      const seconds = parseInt(match[2], 10);
      const millis = match[3] ? parseInt(match[3].padEnd(3, "0").slice(0, 3), 10) : 0;
      const text = match[4].trim();
      if (text) {
        lines.push({
          time: minutes * 60 + seconds + millis / 1000,
          text,
        });
      }
    }
  }
  return lines;
}

function parsePlainTextToLines(txt: string): LyricLine[] {
  const lines: LyricLine[] = [];
  for (const rawLine of txt.split("\n")) {
    const t = rawLine.trim();
    if (t) {
      lines.push({
        time: 0,
        text: t,
      });
    }
  }
  return lines;
}

// -------------------------------------------------------------
// Romanization & Translation
// -------------------------------------------------------------

async function enrichLinesWithRomanizationAndTranslation(
  lines: LyricLine[]
): Promise<{ enrichedLines: LyricLine[]; detectedLang: "ja" | "zh" | null }> {
  if (lines.length === 0) {
    return { enrichedLines: lines, detectedLang: null };
  }

  // 1. Detect language matching PC App exactly (LyricsService.cs:1394-1407)
  const fullText = lines.map((l) => l.text || "").join(" ");
  const kanaCount = (fullText.match(/[\u3040-\u309F\u30A0-\u30FF]/g) || []).length;
  const hanziCount = (fullText.match(/[\u4E00-\u9FFF]/g) || []).length;

  let langTag: "ja" | "zh" | null = null;
  if (kanaCount >= 2) {
    langTag = "ja";
  } else if (hanziCount >= 4) {
    langTag = "zh";
  }

  // PC App: if not Japanese and not Chinese, return lines directly without Romanization
  if (!langTag) {
    return { enrichedLines: lines, detectedLang: null };
  }

  const sl = langTag === "ja" ? "ja" : "zh-CN";

  // 2. Query Google Translate Romanization in batches of 25 lines matching PC App (LyricsService.cs:1439-1478)
  const BATCH_SIZE = 25;
  const enriched: LyricLine[] = [];

  for (let i = 0; i < lines.length; i += BATCH_SIZE) {
    const chunk = lines.slice(i, i + BATCH_SIZE);
    const batchLines = chunk.map((l) => (l.text && l.text.trim() ? l.text.trim() : "♪"));
    const delim = " ::: ";
    const query = batchLines.join(delim);

    try {
      const url = `https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=${sl}&tl=en&dt=rm&q=${encodeURIComponent(query)}`;
      const res = await fetch(url, {
        headers: {
          "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        },
      });

      if (res.ok) {
        const data = (await res.json()) as any;
        const rawItems = data?.[0];
        let romStr = "";
        if (rawItems && rawItems[0] && typeof rawItems[0][3] === "string") {
          romStr = rawItems[0][3];
        }

        const parts = romStr ? romStr.split(/:\s*:\s*:/) : [];

        chunk.forEach((l, idx) => {
          let r = parts[idx] ? parts[idx].trim() : "";
          if (r && r !== "♪") {
            if (langTag === "ja") r = formatAnimeRomaji(r);
            else if (langTag === "zh") r = formatChinesePinyin(r);
          }
          enriched.push({
            ...l,
            romanized: r && r !== "♪" ? r : undefined,
          });
        });
      } else {
        enriched.push(...chunk);
      }
    } catch {
      enriched.push(...chunk);
    }
  }

  return { enrichedLines: enriched, detectedLang: langTag };
}

// -------------------------------------------------------------
// Helpers
// -------------------------------------------------------------

function cleanTitleForSearch(title: string): string {
  return title
    .replace(/[\(\[\{【『](?:Official|Audio|Video|MV|Lyric|Lyrics|HD|4K|M\/V|Visualizer|Teaser)[^\)\]\}】』]*[\)\]\}】』]/gi, "")
    .replace(/歌ってみた(?:のさ)?|Cover|弾き語り/gi, "")
    .replace(/\s+/g, " ")
    .trim();
}

function cleanArtistForSearch(artist: string): string {
  return (artist || "")
    .replace(/\s*-\s*Topic\b/gi, "")
    .replace(/\b(?:Official\s+(?:Channel|Music|Audio|YouTube)|VEVO|Channel)\b/gi, "")
    .replace(/(?:公式チャンネル|Official\s*Channel)/gi, "")
    .replace(/[\(\[\{【『][^\)\]\}】』]*[\)\]\}】』]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function formatAnimeRomaji(text: string): string {
  let s = text
    .replace(/ō/g, "ou").replace(/Ō/g, "Ou")
    .replace(/ū/g, "uu").replace(/Ū/g, "Uu")
    .replace(/ā/g, "aa").replace(/Ā/g, "Aa")
    .replace(/ī/g, "ii").replace(/Ī/g, "Ii")
    .replace(/ē/g, "ee").replace(/Ē/g, "Ee");

  s = s.replace(/\b[Oo]\b/g, "wo");
  s = s.replace(/\(\s*wa\s*\)/gi, "(Wa)");
  s = s.replace(/\s+/g, " ").trim();
  if (s.length > 0 && s[0] === s[0].toLowerCase()) {
    s = s[0].toUpperCase() + s.slice(1);
  }
  return s;
}

function formatChinesePinyin(text: string): string {
  let s = text.replace(/\s+/g, " ").trim();
  if (s.length > 0 && s[0] === s[0].toLowerCase()) {
    s = s[0].toUpperCase() + s.slice(1);
  }
  return s;
}
