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
  videoId?: string,
  refresh?: boolean
): Promise<LyricsResponse> {
  const cleanT = (title || "").trim();
  const cleanA = (artist || "").trim();
  const vid = (videoId || "").trim();

  if (!cleanT && !vid) {
    return { syncedLyrics: null, plainLyrics: null, instrumental: false, lines: [] };
  }

  const cacheKey = `${vid || cleanT.toLowerCase()}::${cleanA.toLowerCase()}::${Math.round(duration || 0)}`;
  if (!refresh && lyricsMemoryCache.has(cacheKey)) {
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

  const isGenericWord = (w: string) =>
    !w ||
    /^(mv|official|video|audio|lyrics?|hd|4k|m\/v|teaser|visualizer|full|trailer|remix|topic)[】\]\)]*$/i.test(w.trim()) ||
    w.trim().length < 2;

  const cleanT = cleanTitleForSearch(title);
  const cleanA = cleanArtistForSearch(artist);

  // Check hashtag in title, e.g. #KingGnu
  const mHash = title.match(/#([^\s#]+)/);
  let hashArtist = cleanA;
  if (mHash && !cleanA) {
    hashArtist = cleanArtistForSearch(mHash[1]);
  }
  const effectiveArtist = !isGenericWord(cleanA) ? cleanA : !isGenericWord(hashArtist) ? hashArtist : "";

  // 1. Japanese quotes: 「Track」 or 『Track』 (Never match 【MV】 as track name)
  const mQuotes = cleanT.match(/[「『]([^」』]+)[」』]/) || title.match(/[「『]([^」』]+)[」』]/);
  if (mQuotes) {
    const inner = mQuotes[1].trim();
    if (!isGenericWord(inner)) {
      if (inner.includes("/")) {
        const parts = inner.split("/");
        const p0 = parts[0].trim();
        const p1 = parts[1].trim();
        if (!isGenericWord(p0) && !isGenericWord(p1)) {
          list.push({ track: p0, artist: p1 });
          list.push({ track: p1, artist: p0 });
        }
      } else {
        list.push({ track: inner, artist: effectiveArtist });
        list.push({ track: inner, artist: "" });
      }
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
        const gen0 = isGenericWord(p0);
        const gen1 = isGenericWord(p1);
        if (!gen0 && !gen1) {
          list.push({ track: p1, artist: p0 });
          list.push({ track: p0, artist: p1 });
          if (effectiveArtist) {
            list.push({ track: p0, artist: effectiveArtist });
            list.push({ track: p1, artist: effectiveArtist });
          }
        } else if (!gen0 && gen1) {
          list.push({ track: p0, artist: effectiveArtist });
        } else if (gen0 && !gen1) {
          list.push({ track: p1, artist: effectiveArtist });
        }
      }
      break;
    }
  }

  // 3. Featured artist (feat. / ft.)
  const mFeat = title.match(/(?:feat\.|ft\.|featuring)\s*([^/|()\[\]–—]+)/i);
  if (mFeat) {
    const featArtist = mFeat[1].trim();
    if (featArtist && effectiveArtist) {
      list.push({ track: cleanT, artist: `${effectiveArtist} feat. ${featArtist}` });
      list.push({ track: cleanT, artist: `${effectiveArtist} & ${featArtist}` });
    }
  }

  // 4. Clean full title without brackets or quotes
  const unquoted = cleanT.replace(/[「」『』"']/g, "").trim();
  if (unquoted && !isGenericWord(unquoted)) {
    if (effectiveArtist) {
      list.push({ track: unquoted, artist: effectiveArtist });
    }
    list.push({ track: unquoted, artist: "" });
  }

  // Deduplicate and filter out any generic tokens
  const seen = new Set<string>();
  const unique: SearchCandidate[] = [];
  for (const c of list) {
    if (isGenericWord(c.track)) continue;
    const finalArtist = isGenericWord(c.artist) ? "" : c.artist;
    const key = `${c.track.toLowerCase()}||${finalArtist.toLowerCase()}`;
    if (!seen.has(key)) {
      seen.add(key);
      unique.push({ track: c.track, artist: finalArtist });
    }
  }
  return unique;
}

function isCandidateMatch(searchTrack: string, resultTrack: string, targetArtist?: string, resultArtist?: string): boolean {
  if (!searchTrack || !resultTrack) return false;
  const s1 = searchTrack.toLowerCase().replace(/[\s\-_/|–—「」『』【】\(\)\[\]]+/g, "");
  const s2 = resultTrack.toLowerCase().replace(/[\s\-_/|–—「」『』【】\(\)\[\]]+/g, "");
  if (!s1 || !s2) return false;

  // CJK Safeguard: If target title has CJK (Japanese/Chinese/Korean),
  // resultTrack MUST ALSO contain CJK characters! Never match a CJK title to pure Latin ASCII like "STRESSY"!
  const hasCjk1 = /[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\uAC00-\uD7AF]/.test(searchTrack);
  const hasCjk2 = /[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\uAC00-\uD7AF]/.test(resultTrack);
  if (hasCjk1 && !hasCjk2) {
    return false;
  }

  // If artist names are provided and completely conflicting, reject
  if (targetArtist && resultArtist) {
    const a1 = targetArtist.toLowerCase().replace(/[\s\-_/|–—【】\(\)\[\]]+/g, "");
    const a2 = resultArtist.toLowerCase().replace(/[\s\-_/|–—【】\(\)\[\]]+/g, "");
    if (a1 && a2 && a1.length > 2 && a2.length > 2) {
      if (!a1.includes(a2) && !a2.includes(a1)) {
        // Different artists -> require exact track title
        if (s1 !== s2) return false;
      }
    }
  }

  // Exact normalized title match
  if (s1 === s2) return true;

  // Short titles (<= 4 chars) MUST match exactly to avoid false positives (e.g. "MV", "Tu", "Me")
  if (s1.length <= 4 || s2.length <= 4) {
    return s1 === s2;
  }

  // Substring match only if length ratio >= 60%
  if (s1.includes(s2) || s2.includes(s1)) {
    const ratio = Math.min(s1.length, s2.length) / Math.max(s1.length, s2.length);
    if (ratio >= 0.6) return true;
  }

  return false;
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

  // Pass 2: Try /api/search?track_name=...&artist_name=...
  for (const cand of candidates.slice(0, 4)) {
    try {
      const searchUrl = new URL("https://lrclib.net/api/search");
      searchUrl.searchParams.set("track_name", cand.track);
      if (cand.artist) {
        searchUrl.searchParams.set("artist_name", cand.artist);
      }
      let res = await fetch(searchUrl.toString(), {
        headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
      });
      let list = res.ok ? ((await res.json()) as any[]) : [];

      // If empty and artist was specified, retry with track_name alone
      if ((!list || list.length === 0) && cand.artist) {
        const fallbackUrl = new URL("https://lrclib.net/api/search");
        fallbackUrl.searchParams.set("track_name", cand.track);
        res = await fetch(fallbackUrl.toString(), {
          headers: { "User-Agent": "KahoiiMusic-Web/1.0" },
        });
        if (res.ok) {
          list = (await res.json()) as any[];
        }
      }

      if (Array.isArray(list) && list.length > 0) {
        // Strictly validate title and artist match
        const matchingItems = list.filter((item) =>
          isCandidateMatch(cand.track, item.trackName || "", cand.artist, item.artistName)
        );
        if (matchingItems.length > 0) {
          // Find closest duration match within 12 seconds
          let match = matchingItems.find((item) => {
            if (!duration || duration <= 0) return true;
            return item.duration && Math.abs(item.duration - duration) <= 12;
          });
          // Fallback to within 25 seconds
          if (!match && duration && duration > 0) {
            match = matchingItems.find((item) => {
              return item.duration && Math.abs(item.duration - duration) <= 25;
            });
          }
          if (!match && (!duration || duration <= 0)) {
            match = matchingItems.find((item) => item.syncedLyrics) || matchingItems[0];
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
  let s = (title || "").replace(/[\u00A0\u200B\u3000]/g, " ");
  s = s
    .replace(/^([【\[\(](?:MV|Official|Video|Audio|HD|4K|Music\s+Video|Lyric\s+Video|Visualizer|Teaser)[】\]\)]\s*)+/gi, "")
    .replace(/[\(\[\{【『](?:Official|Audio|Video|MV|Lyric|Lyrics|HD|4K|M\/V|Visualizer|Teaser|Full)[^\)\]\}】』]*[\)\]\}】』]/gi, "")
    .replace(/歌ってみた(?:のさ)?|Cover|弾き語り/gi, "")
    .replace(/[【】\[\]]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  return s;
}

function cleanArtistForSearch(artist: string): string {
  let s = (artist || "").replace(/[\u00A0\u200B\u3000]/g, " ");
  s = s
    .replace(/\s*-\s*Topic\b/gi, "")
    .replace(/\b(?:Official\s+(?:Channel|Music|Audio|YouTube)|VEVO|Channel)\b/gi, "")
    .replace(/(?:公式チャンネル|Official\s*Channel)/gi, "")
    .replace(/[\(\[\{【『][^\)\]\}】』]*[\)\]\}】』]/g, "")
    .replace(/[【】\[\]]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  if (/^(mv|official|video|audio|lyrics?|hd|4k|m\/v|teaser|visualizer)$/i.test(s)) {
    return "";
  }
  return s;
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
