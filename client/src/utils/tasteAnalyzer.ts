import type { Track } from "../types";

export interface TasteTag {
  key: string;
  label: string;
  type: "all" | "artist" | "genre" | "country" | "style";
  querySeed: string;
  count?: number;
}

export interface TasteProfile {
  hasFavorites: boolean;
  totalFavorites: number;
  topArtists: string[];
  genres: string[];
  countries: string[];
  styles: string[];
  tags: TasteTag[];
  summary: {
    artistsText: string;
    genresText: string;
    countriesText: string;
    stylesText: string;
  };
}

const VIETNAMESE_REGEX = /[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ]/i;
const JAPANESE_REGEX = /[\u3040-\u309F\u30A0-\u30FF]/;
const KOREAN_REGEX = /[\uAC00-\uD7AF\u1100-\u11FF]/;
const CHINESE_REGEX = /[\u4E00-\u9FFF]/;

const GENRE_RULES: { genre: string; regex: RegExp; querySeed: string }[] = [
  {
    genre: "Rap / Hip-Hop",
    regex: /\b(rap|hiphop|hip-hop|drill|trap|cypher|freestyle|flow|đen|mck|tlinh|b ray|16 typh|gducky|hieuthuhai|andree|binz|rhymastic|obito|24k|wxrdie|pháp kiều|hurrykng|dế choắt)\b/i,
    querySeed: "Rap Việt thịnh hành hay nhất",
  },
  {
    genre: "Indie / Acoustic",
    regex: /\b(indie|acoustic|guitar|piano|unplugged|mộc|vũ\b|ngọt|cá hồi hoang|chillies|thịnh suy|hoàng dũng|thế bảo|trang|marzuz|khiem)\b/i,
    querySeed: "Indie Việt acoustic chill nhẹ nhàng",
  },
  {
    genre: "R&B / Soul",
    regex: /\b(r&b|rnb|soul|groove|soobin|wren evans|grey d|hngle|mỹ anh|pixel neko|parys|vstra|52hz|nie)\b/i,
    querySeed: "R&B Việt thịnh hành chill",
  },
  {
    genre: "Ballad / Tâm trạng",
    regex: /\b(ballad|nỗi đau|nước mắt|chia tay|buông|quên|cô đơn|tổn thương|mưa|trung quân|hoài lâm|bảo anh|mr siro|hương tràm|đức phúc|erik|khói lam)\b/i,
    querySeed: "Ballad Việt tâm trạng buồn sâu lắng",
  },
  {
    genre: "EDM / Remix",
    regex: /\b(edm|remix|vinahouse|house|club|bass|bounce|speed up|dj|dance)\b/i,
    querySeed: "Nhạc trẻ remix bass cực căng",
  },
  {
    genre: "Lofi / Chill",
    regex: /\b(lofi|lo-fi|chill|thư giãn|ngủ ngon|slowed|reverb|nightcore|study|relax|thời gian ngưng đọng)\b/i,
    querySeed: "Lofi chill Việt Nam thư giãn",
  },
  {
    genre: "V-Pop Hiện Đại",
    regex: /\b(v-pop|vpop|sơn tùng|mono|amee|min|hòa minzy|jack|văn mai hương|dương domic|quang hùng)\b/i,
    querySeed: "V-Pop thịnh hành tuyển chọn mới nhất",
  },
  {
    genre: "J-Pop / Anime",
    regex: /\b(j-pop|jpop|anime|vocaloid|miku|yoasobi|ado|kenshi yonezu|eve|aimer|lisa|radwimps|zutomayo|fujii kaze|vaundy)\b/i,
    querySeed: "J-Pop Anime hits hay nhất",
  },
  {
    genre: "K-Pop",
    regex: /\b(k-pop|kpop|bts|blackpink|newjeans|aespa|ive|twice|iu|seventeen|stray kids|le sserafim|exo|nct|itzy|txt|enhypen|taeyeon)\b/i,
    querySeed: "K-Pop trending hits",
  },
  {
    genre: "US-UK / Quốc tế",
    regex: /\b(taylor swift|the weeknd|billie eilish|bruno mars|ed sheeran|ariana grande|justin bieber|drake|post malone|dua lipa|olivia rodrigo|lady gaga|maroon 5|adele|coldplay|imagine dragons|charlie puth|eminem|rihanna|sabrina carpenter|chappell roan)\b/i,
    querySeed: "US-UK Billboard top hits",
  },
];

const STYLE_RULES: { style: string; regex: RegExp; querySeed: string }[] = [
  {
    style: "Chill / Thư giãn",
    regex: /\b(chill|lofi|acoustic|nhẹ nhàng|mộc mạc|bình yên|thư thái|cà phê|chiều tà)\b/i,
    querySeed: "Nhạc chill thư giãn cà phê chiều",
  },
  {
    style: "Suy / Tâm trạng",
    regex: /\b(buồn|suy|cô đơn|đêm muộn|khóc|nước mắt|chia tay|rời xa|quên|buông|đau)\b/i,
    querySeed: "Nhạc suy tâm trạng đêm khuya",
  },
  {
    style: "Sôi động / Bốc lửa",
    regex: /\b(remix|edm|quẩy|bốc|năng lượng|dance|vinahouse|căng|cháy)\b/i,
    querySeed: "Nhạc sôi động năng lượng cao",
  },
  {
    style: "Tình cảm / Ngọt ngào",
    regex: /\b(yêu|thương|ngọt ngào|tình yêu|hạnh phúc|cưới|crush|tỏ tình|nụ cười|sweet)\b/i,
    querySeed: "Tình ca ngọt ngào đáng yêu",
  },
  {
    style: "Trẻ trung / Gen Z",
    regex: /\b(speed up|drill|trap|gen z|trendy|bắt trend|flex|chill out)\b/i,
    querySeed: "Nhạc Gen Z bắt trend thịnh hành",
  },
];

export function analyzeTasteProfile(favorites: Track[]): TasteProfile {
  if (!favorites || favorites.length === 0) {
    // Default discovery profile for fresh accounts
    return {
      hasFavorites: false,
      totalFavorites: 0,
      topArtists: [],
      genres: ["V-Pop Hiện Đại", "Indie / Acoustic", "Rap / Hip-Hop", "Lofi / Chill"],
      countries: ["Việt Nam 🇻🇳", "Âu Mỹ 🇺🇸", "Nhật Bản 🇯🇵"],
      styles: ["Chill / Thư giãn", "Sôi động / Bốc lửa", "Suy / Tâm trạng"],
      tags: [
        { key: "all", label: "✨ Tất cả gợi ý", type: "all", querySeed: "Nhạc thịnh hành Việt Nam mới nhất" },
        { key: "vpop", label: "🇻🇳 V-Pop Mới", type: "genre", querySeed: "V-Pop thịnh hành tuyển chọn mới nhất" },
        { key: "indie", label: "☕ Indie / Chill", type: "genre", querySeed: "Indie Việt acoustic chill nhẹ nhàng" },
        { key: "rap", label: "🎤 Rap Việt", type: "genre", querySeed: "Rap Việt thịnh hành hay nhất" },
        { key: "usuk", label: "🇺🇸 US-UK Hits", type: "country", querySeed: "US-UK Billboard top hits" },
        { key: "jpop", label: "🇯🇵 J-Pop / Anime", type: "genre", querySeed: "J-Pop Anime hits hay nhất" },
      ],
      summary: {
        artistsText: "Chưa phân tích",
        genresText: "V-Pop, Indie, Rap, Lofi",
        countriesText: "Việt Nam, Quốc tế",
        stylesText: "Chill, Sôi động, Tâm trạng",
      },
    };
  }

  // 1. Artist extraction and frequency count
  const artistCounts = new Map<string, number>();
  const trackTexts: string[] = [];

  for (const track of favorites) {
    const fullText = `${track.title || ""} ${track.author || ""}`.toLowerCase();
    trackTexts.push(fullText);

    const author = track.author?.trim();
    if (author && author !== "Nghệ sĩ" && author !== "Kahoii Music") {
      const parts = author
        .split(/[,&+/]|(?:\s+(?:và|feat\.?|ft\.?|cùng với|x|vs\.?)\s+)/i)
        .map((p) => p.trim())
        .filter((p) => p.length >= 2 && !/^(official|channel|topic|various artists)$/i.test(p));

      for (const p of parts) {
        artistCounts.set(p, (artistCounts.get(p) || 0) + 1);
      }
    }
  }

  const sortedArtists = Array.from(artistCounts.entries())
    .sort((a, b) => b[1] - a[1])
    .map(([name]) => name);
  const topArtists = sortedArtists.slice(0, 4);

  // 2. Genre detection
  const genreCounts = new Map<string, { count: number; querySeed: string }>();
  for (const text of trackTexts) {
    for (const rule of GENRE_RULES) {
      if (rule.regex.test(text)) {
        const cur = genreCounts.get(rule.genre) || { count: 0, querySeed: rule.querySeed };
        genreCounts.set(rule.genre, { count: cur.count + 1, querySeed: rule.querySeed });
      }
    }
  }

  const sortedGenres = Array.from(genreCounts.entries())
    .sort((a, b) => b[1].count - a[1].count)
    .map(([genre, data]) => ({ genre, ...data }));
  const topGenres = sortedGenres.slice(0, 4);

  // 3. Country / Language detection
  const countryCounts = new Map<string, number>();
  for (const text of trackTexts) {
    if (JAPANESE_REGEX.test(text) || /\b(miku|anime|jpop|j-pop|yoasobi|ado)\b/i.test(text)) {
      countryCounts.set("Nhật Bản 🇯🇵", (countryCounts.get("Nhật Bản 🇯🇵") || 0) + 1);
    } else if (KOREAN_REGEX.test(text) || /\b(kpop|k-pop|bts|blackpink|newjeans|aespa|ive)\b/i.test(text)) {
      countryCounts.set("Hàn Quốc 🇰🇷", (countryCounts.get("Hàn Quốc 🇰🇷") || 0) + 1);
    } else if (CHINESE_REGEX.test(text) || /\b(cpop|c-pop|nhạc hoa|douyin)\b/i.test(text)) {
      countryCounts.set("Trung Quốc 🇨🇳", (countryCounts.get("Trung Quốc 🇨🇳") || 0) + 1);
    } else if (VIETNAMESE_REGEX.test(text) || /\b(sơn tùng|hngle|vũ|đen|soobin|vpop|v-pop)\b/i.test(text)) {
      countryCounts.set("Việt Nam 🇻🇳", (countryCounts.get("Việt Nam 🇻🇳") || 0) + 1);
    } else if (/\b(the|and|you|me|love|night|taylor swift|weeknd|ed sheeran)\b/i.test(text)) {
      countryCounts.set("Âu Mỹ 🇺🇸", (countryCounts.get("Âu Mỹ 🇺🇸") || 0) + 1);
    }
  }

  // Ensure at least Vietnam or US-UK if ambiguous
  if (countryCounts.size === 0) {
    countryCounts.set("Việt Nam 🇻🇳", 1);
  }

  const topCountries = Array.from(countryCounts.entries())
    .sort((a, b) => b[1] - a[1])
    .map(([country]) => country)
    .slice(0, 3);

  // 4. Style detection
  const styleCounts = new Map<string, { count: number; querySeed: string }>();
  for (const text of trackTexts) {
    for (const rule of STYLE_RULES) {
      if (rule.regex.test(text)) {
        const cur = styleCounts.get(rule.style) || { count: 0, querySeed: rule.querySeed };
        styleCounts.set(rule.style, { count: cur.count + 1, querySeed: rule.querySeed });
      }
    }
  }

  const sortedStyles = Array.from(styleCounts.entries())
    .sort((a, b) => b[1].count - a[1].count)
    .map(([style, data]) => ({ style, ...data }));
  const topStyles = sortedStyles.slice(0, 3);

  // 5. Build dynamic taste filter tags
  const tags: TasteTag[] = [
    {
      key: "all",
      label: "✨ Tất cả gợi ý",
      type: "all",
      querySeed: topArtists[0] ? `${topArtists[0]} bài hát hay nhất` : "Nhạc thịnh hành hay nhất",
    },
  ];

  // Artist tags
  for (const a of topArtists.slice(0, 2)) {
    tags.push({
      key: `artist-${a}`,
      label: `🎙️ ${a}`,
      type: "artist",
      querySeed: `${a} bài hát hay nhất`,
      count: artistCounts.get(a),
    });
  }

  // Genre tags
  for (const g of topGenres.slice(0, 2)) {
    tags.push({
      key: `genre-${g.genre}`,
      label: `🎵 ${g.genre}`,
      type: "genre",
      querySeed: g.querySeed,
      count: g.count,
    });
  }

  // Country tags
  for (const c of topCountries.slice(0, 1)) {
    const isVN = c.includes("Việt Nam");
    tags.push({
      key: `country-${c}`,
      label: `🌏 ${c}`,
      type: "country",
      querySeed: isVN ? "V-Pop nhạc Việt chọn lọc hay nhất" : `${c} top hits hay nhất`,
    });
  }

  // Style tags
  for (const s of topStyles.slice(0, 1)) {
    tags.push({
      key: `style-${s.style}`,
      label: `✨ ${s.style}`,
      type: "style",
      querySeed: s.querySeed,
      count: s.count,
    });
  }

  return {
    hasFavorites: true,
    totalFavorites: favorites.length,
    topArtists,
    genres: topGenres.map((g) => g.genre),
    countries: topCountries,
    styles: topStyles.map((s) => s.style),
    tags,
    summary: {
      artistsText: topArtists.length > 0 ? topArtists.join(", ") : "Đang cập nhật",
      genresText: topGenres.length > 0 ? topGenres.map((g) => g.genre).join(", ") : "Pop, Chill",
      countriesText: topCountries.join(", "),
      stylesText: topStyles.length > 0 ? topStyles.map((s) => s.style).join(", ") : "Chill, Thư giãn",
    },
  };
}
