import { Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { PlaylistItem, ResolvedAudioStream } from '../types/music';

interface CacheEntry {
  stream: ResolvedAudioStream;
  cachedAt: number;
}

// Fallback high-entropy YouTube visitor token to ensure 0ms cold-start playback
const FALLBACK_VISITOR_DATA =
  'CgtkQTNlZU9ucUxRNCiV4arXBjIKCgJWThIEGgAgUmKrAgqoAkN0c0JDQVVTalFFQkRCNlBERzBsUUVpbF8yZWx3NWJQZUdEZWthZDBNZ1pZZFRQaDdrZGVwVUY2dHFFTjY1UmM0U0JxVHVxMk41VVBCeWltek1OU1d2bDlvLW9YREFxQ3FkSzJRdHNVR3lRbGI2VzFyUlZENWxZdGJWM3NJRVVqNTVxSWlCSzFmNHRwUjBRbEI2ODNRR2xfTkZPZHBZVFVWdUcxQlUzUHVPWDV4UzAwbDBjNzR5bnN4SWZKa25ORVY4RHNqNDhvQlRKRkFSeFk5V29mZFJTeWdrUXZySGticks0d0pSY3NWV3FLMVh0NnNCTkZvcjU4ZWdjRHlVYUVMaXZyOVVFeG9yeFdjMVlQVGljOVVMTEltM0tMRTdfVjEtOFVIcXdQ';

export class YouTubeResolver {
  private static instance: YouTubeResolver;
  private cache: Map<string, CacheEntry> = new Map();
  private visitorData: string = FALLBACK_VISITOR_DATA;
  private visitorDataTime: number = 0;
  private warmupPromise: Promise<string> | null = null;

  private constructor() {
    this.initVisitorData();
  }

  public static getInstance(): YouTubeResolver {
    if (!YouTubeResolver.instance) {
      YouTubeResolver.instance = new YouTubeResolver();
    }
    return YouTubeResolver.instance;
  }

  // Load persisted visitorData and silently pre-warm fresh token in background
  private async initVisitorData(): Promise<void> {
    try {
      const saved = await AsyncStorage.getItem('@kahoii_yt_visitor_data');
      if (saved && saved.startsWith('Cg') && saved.length > 50) {
        this.visitorData = saved;
        this.visitorDataTime = Date.now();
      }
    } catch {
      // non-fatal
    }
    this.warmupVisitorData();
  }

  // Extract 11-char Video ID
  public extractVideoId(input: string): string | null {
    const trimmed = input.trim();
    if (!trimmed) return null;

    if (/^[a-zA-Z0-9_-]{11}$/.test(trimmed)) {
      return trimmed;
    }

    const patterns = [
      /(?:v=|\/v\/|embed\/|youtu\.be\/|\/shorts\/)([a-zA-Z0-9_-]{11})/,
      /[?&]v=([a-zA-Z0-9_-]{11})/,
    ];

    for (const pattern of patterns) {
      const match = trimmed.match(pattern);
      if (match && match[1]) {
        return match[1];
      }
    }
    return null;
  }

  // Extract Playlist ID
  public extractPlaylistId(input: string): string | null {
    const trimmed = input.trim();
    const match = trimmed.match(/[?&]list=([a-zA-Z0-9_-]+)/i);
    return match ? match[1] : null;
  }

  public isUrl(input: string): boolean {
    const trimmed = input.trim().toLowerCase();
    return (
      trimmed.startsWith('http://') ||
      trimmed.startsWith('https://') ||
      trimmed.includes('youtu.be') ||
      trimmed.includes('youtube.com')
    );
  }

  // Recursive search for 400+ char Cg... visitor data string in parsed JSON
  private findVisitorDataInJson(obj: any): string | null {
    if (typeof obj === 'string') {
      if (obj.startsWith('Cg') && obj.length > 50) return obj;
    }
    if (Array.isArray(obj)) {
      for (const item of obj) {
        const found = this.findVisitorDataInJson(item);
        if (found) return found;
      }
    } else if (typeof obj === 'object' && obj !== null) {
      for (const k of Object.keys(obj)) {
        const found = this.findVisitorDataInJson(obj[k]);
        if (found) return found;
      }
    }
    return null;
  }

  // Silently refresh visitor token from YouTube sw.js_data or homepage
  private async fetchVisitorDataFromWeb(): Promise<string | null> {
    // 1. Primary: YouTube sw.js_data (responds in ~300ms with full token)
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 4000);
      const res = await fetch('https://www.youtube.com/sw.js_data', {
        signal: controller.signal,
        headers: {
          'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
          'Accept-Language': 'vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7',
        },
      });
      clearTimeout(timeoutId);
      if (res.ok) {
        const text = await res.text();
        const clean = text.replace(/^\)\]\}'/, '').trim();
        const parsed = JSON.parse(clean);
        const token = this.findVisitorDataInJson(parsed);
        if (token) return token;
      }
    } catch {
      // try fallback
    }

    // 2. Secondary: YouTube homepage
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 4000);
      const res = await fetch('https://www.youtube.com', {
        signal: controller.signal,
        headers: {
          'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
          'Accept-Language': 'vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7',
        },
      });
      clearTimeout(timeoutId);
      if (res.ok) {
        const html = await res.text();
        const match =
          html.match(/"(?:VISITOR_DATA|visitorData)":"([^"]+)"/) ||
          html.match(/ytcfg\.set\(\{"VISITOR_DATA":"([^"]+)"/);
        if (match && match[1] && match[1].length > 50) {
          return match[1];
        }
      }
    } catch {
      // non-fatal
    }

    return null;
  }

  // Pre-warm visitorData in background
  public async warmupVisitorData(force = false): Promise<string> {
    if (!force && this.visitorData && Date.now() - this.visitorDataTime < 12 * 3600 * 1000) {
      return this.visitorData;
    }
    if (this.warmupPromise) return this.warmupPromise;

    this.warmupPromise = (async () => {
      try {
        const token = await this.fetchVisitorDataFromWeb();
        if (token && token.startsWith('Cg') && token.length > 50) {
          this.visitorData = token;
          this.visitorDataTime = Date.now();
          AsyncStorage.setItem('@kahoii_yt_visitor_data', token).catch(() => {});
          return token;
        }
      } catch (e) {
        console.warn('[YouTubeResolver] Warmup visitorData error:', e);
      } finally {
        this.warmupPromise = null;
      }
      return this.visitorData;
    })();

    return this.warmupPromise;
  }

  private async getVisitorData(): Promise<string> {
    if (this.visitorData && this.visitorData.length > 50) {
      return this.visitorData;
    }
    return this.warmupVisitorData();
  }

  // Primary: VisionOS InnerTube Client
  private async resolveVisionOS(videoId: string, visitorData: string): Promise<ResolvedAudioStream> {
    const client: Record<string, any> = {
      clientName: 'VISIONOS',
      clientVersion: '1.02',
      deviceMake: 'Apple',
      deviceModel: 'RealityDevice17,1',
      userAgent:
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15',
      osName: 'visionOS',
      osVersion: '26.5.23O471',
      hl: 'vi',
      timeZone: 'UTC',
      utcOffsetMinutes: 0,
      visitorData,
    };

    const payload = {
      context: { client },
      videoId,
      playbackContext: {
        contentPlaybackContext: {
          html5Preference: 'HTML5_PREF_WANTS',
          signatureTimestamp: 20752,
        },
      },
      contentCheckOk: true,
      racyCheckOk: true,
    };

    const res = await fetch('https://www.youtube.com/youtubei/v1/player?prettyPrint=false', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'User-Agent': client.userAgent,
        'X-Youtube-Client-Name': '101',
        'X-Youtube-Client-Version': '1.02',
        Origin: 'https://www.youtube.com',
        'X-Goog-Visitor-Id': visitorData,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) throw new Error(`VisionOS HTTP ${res.status}`);

    const json = await res.json();
    return this.parseInnerTubeResponse(json, videoId);
  }

  // Secondary: Android VR Client
  private async resolveAndroidVR(videoId: string, visitorData: string): Promise<ResolvedAudioStream> {
    const client: Record<string, any> = {
      clientName: 'ANDROID_VR',
      clientVersion: '1.61.48',
      deviceMake: 'Oculus',
      deviceModel: 'Quest 3',
      osName: 'Android',
      osVersion: '12',
      hl: 'vi',
      gl: 'VN',
      visitorData,
    };

    const payload = {
      videoId,
      context: { client },
      contentCheckOk: true,
      racyCheckOk: true,
    };

    const res = await fetch('https://www.youtube.com/youtubei/v1/player?prettyPrint=false', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'User-Agent':
          'Mozilla/5.0 (Linux; Android 12; Quest 3) AppleWebKit/537.36 (KHTML, like Gecko) OculusBrowser/32.0.0.12.39',
        'X-Goog-Visitor-Id': visitorData,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) throw new Error(`AndroidVR HTTP ${res.status}`);

    const json = await res.json();
    return this.parseInnerTubeResponse(json, videoId);
  }

  // Parse InnerTube response format list
  private parseInnerTubeResponse(json: any, videoId: string): ResolvedAudioStream {
    if (json?.playabilityStatus?.status && json.playabilityStatus.status !== 'OK') {
      const reason = json.playabilityStatus.reason || `Video không khả dụng (${json.playabilityStatus.status})`;
      throw new Error(reason);
    }

    const videoDetails = json?.videoDetails || {};
    const title = videoDetails.title || 'Bài hát YouTube';
    const author = videoDetails.author || 'YouTube';
    const durationSeconds = parseInt(videoDetails.lengthSeconds || '0', 10);
    const thumbnailUrl = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;

    const formats = json?.streamingData?.adaptiveFormats || [];
    let bestUrl: string | null = null;
    let highestBitrate = 0;
    let contentLength: number | undefined;

    const hlsManifest = json?.streamingData?.hlsManifestUrl;

    // Critical fix for iOS:
    // If the track is longer than 10 minutes (600s), YouTube aggressively throttles raw m4a/webm streams down to 32KB/s.
    // iOS AVPlayer cannot maintain a single progressive MP4 buffer under 32KB/s and drops connection after ~10 minutes.
    // But Apple's native HLS (.m3u8) streams in tiny 2-5 second segments, allowing iOS to stream for hours effortlessly!
    if (Platform.OS === 'ios' && durationSeconds > 600 && hlsManifest) {
      bestUrl = hlsManifest;
    } else {
      // Pass 1: Prioritize AAC m4a (audio/mp4)
      for (const f of formats) {
        const mime = f.mimeType || '';
        const bitrate = f.bitrate || 0;
        if (
          (mime.includes('audio/mp4') || mime.includes('m4a')) &&
          f.url &&
          bitrate > highestBitrate
        ) {
          highestBitrate = bitrate;
          bestUrl = f.url;
          contentLength = f.contentLength ? parseInt(f.contentLength, 10) : undefined;
        }
      }

      // Pass 2: Any audio format (webm/opus)
      if (!bestUrl) {
        for (const f of formats) {
          const mime = f.mimeType || '';
          const bitrate = f.bitrate || 0;
          if (mime.includes('audio') && f.url && bitrate > highestBitrate) {
            highestBitrate = bitrate;
            bestUrl = f.url;
            contentLength = f.contentLength ? parseInt(f.contentLength, 10) : undefined;
          }
        }
      }

      // Pass 3: HLS Manifest
      if (!bestUrl && hlsManifest) {
        bestUrl = hlsManifest;
      }
    }

    if (!bestUrl) {
      throw new Error('Không tìm thấy luồng phát khả dụng cho bài hát.');
    }

    return {
      streamUrl: bestUrl,
      hlsUrl: hlsManifest,
      title,
      author,
      durationSeconds,
      thumbnailUrl,
      contentLength,
    };
  }

  // Fallback: Invidious Instances
  private async resolveInvidious(videoId: string): Promise<ResolvedAudioStream> {
    const instances = [
      'https://inv.tux.pizza',
      'https://invidious.nerdvpn.de',
      'https://yt.artemislena.eu',
    ];

    for (const inst of instances) {
      try {
        const res = await fetch(`${inst}/api/v1/videos/${videoId}`);
        if (!res.ok) continue;
        const data = await res.json();
        const audioStreams = (data.adaptiveFormats || []).filter((f: any) =>
          (f.type || '').includes('audio')
        );
        audioStreams.sort((a: any, b: any) => (b.bitrate || 0) - (a.bitrate || 0));

        if (audioStreams.length > 0 && audioStreams[0].url) {
          return {
            streamUrl: audioStreams[0].url,
            title: data.title || 'Bài hát YouTube',
            author: data.author || 'YouTube',
            durationSeconds: data.lengthSeconds || 0,
            thumbnailUrl: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
          };
        }
      } catch {
        continue;
      }
    }
    throw new Error('Invidious resolution failed');
  }

  // Main stream resolver
  public async resolveAudioStream(videoId: string, isRetry = false): Promise<ResolvedAudioStream> {
    // 0. Cache check (2 hours)
    const cached = this.cache.get(videoId);
    if (cached && Date.now() - cached.cachedAt < 2 * 3600 * 1000) {
      return cached.stream;
    }

    // Web / PWA: Route via /api/player proxy to bypass browser CORS restrictions
    if (Platform.OS === 'web') {
      try {
        const res = await fetch(`/api/player?id=${videoId}`);
        if (res.ok) {
          const stream: ResolvedAudioStream = await res.json();
          this.cache.set(videoId, { stream, cachedAt: Date.now() });
          return stream;
        }
      } catch (e) {
        console.warn('[YouTubeResolver Web] /api/player failed, falling back to direct:', e);
      }
    }

    const visitorData = await this.getVisitorData();

    // 1. Try VisionOS
    try {
      const stream = await this.resolveVisionOS(videoId, visitorData);
      this.cache.set(videoId, { stream, cachedAt: Date.now() });
      return stream;
    } catch (e: any) {
      console.warn('[YouTubeResolver] VisionOS failed:', e?.message || e);
      // If token expired or robot check encountered, force refresh token and retry
      if (
        !isRetry &&
        (e?.message?.includes('robot') ||
          e?.message?.includes('LOGIN_REQUIRED') ||
          e?.message?.includes('Sign in') ||
          e?.message?.includes('Đăng nhập'))
      ) {
        console.log('[YouTubeResolver] Refreshing visitorData token and retrying...');
        await this.warmupVisitorData(true);
        return this.resolveAudioStream(videoId, true);
      }
    }

    // 2. Try Android VR
    try {
      const stream = await this.resolveAndroidVR(videoId, visitorData);
      this.cache.set(videoId, { stream, cachedAt: Date.now() });
      return stream;
    } catch (e: any) {
      console.warn('[YouTubeResolver] AndroidVR failed:', e?.message || e);
    }

    // 3. Fallback: Invidious
    try {
      const stream = await this.resolveInvidious(videoId);
      this.cache.set(videoId, { stream, cachedAt: Date.now() });
      return stream;
    } catch (e) {
      console.warn('[YouTubeResolver] Invidious failed:', e);
    }

    throw new Error('Không thể phân giải luồng phát âm thanh cho bài hát này.');
  }

  // YouTube Search API
  public async search(query: string): Promise<PlaylistItem[]> {
    const trimmed = query.trim();
    if (!trimmed) return [];

    // Web / PWA: Route via /api/search proxy to bypass browser CORS limitations
    if (Platform.OS === 'web') {
      try {
        const res = await fetch(`/api/search?q=${encodeURIComponent(trimmed)}`);
        if (res.ok) {
          return await res.json();
        }
      } catch (e) {
        console.warn('[YouTubeResolver Web] /api/search proxy failed:', e);
      }
    }

    try {
      const payload = {
        query: trimmed,
        context: {
          client: {
            clientName: 'WEB',
            clientVersion: '2.20240101.00.00',
            hl: 'vi',
            gl: 'VN',
          },
        },
      };

      const res = await fetch('https://www.youtube.com/youtubei/v1/search', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'User-Agent':
            'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1',
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) return [];

      const json = await res.json();
      const sections =
        json?.contents?.twoColumnSearchResultsRenderer?.primaryContents?.sectionListRenderer
          ?.contents || [];

      const results: PlaylistItem[] = [];

      for (const section of sections) {
        const items = section?.itemSectionRenderer?.contents || [];
        for (const item of items) {
          const video = item.videoRenderer;
          if (!video || !video.videoId) continue;

          const title = video.title?.runs?.[0]?.text || 'Video YouTube';
          const author = video.ownerText?.runs?.[0]?.text || '';
          const lengthText = video.lengthText?.simpleText || '';
          const durationSeconds = this.parseDuration(lengthText);
          const thumbnailUrl = `https://i.ytimg.com/vi/${video.videoId}/hqdefault.jpg`;

          results.push({
            id: video.videoId,
            title,
            author,
            durationSeconds,
            thumbnailUrl,
          });
        }
      }

      return results;
    } catch (e) {
      console.error('[YouTubeResolver] Search error:', e);
      return [];
    }
  }

  // Load Playlist from YouTube
  public async loadPlaylist(playlistId: string): Promise<{ title: string; items: PlaylistItem[] }> {
    try {
      const browseId = playlistId.startsWith('VL') ? playlistId : `VL${playlistId}`;
      const payload = {
        browseId,
        context: {
          client: {
            clientName: 'WEB',
            clientVersion: '2.20240101.00.00',
            hl: 'vi',
            gl: 'VN',
          },
        },
      };

      const res = await fetch('https://www.youtube.com/youtubei/v1/browse', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) return { title: 'Danh sách phát', items: [] };

      const json = await res.json();
      const items: PlaylistItem[] = [];
      const tabs = json?.contents?.twoColumnBrowseResultsRenderer?.tabs || [];
      const tabContent = tabs[0]?.tabRenderer?.content;
      const sectionContents =
        tabContent?.sectionListRenderer?.contents?.[0]?.itemSectionRenderer?.contents?.[0]
          ?.playlistVideoListRenderer?.contents || [];

      for (const item of sectionContents) {
        const vr = item.playlistVideoRenderer;
        if (!vr || !vr.videoId) continue;

        const title = vr.title?.runs?.[0]?.text || 'Bài hát';
        const author = vr.shortBylineText?.runs?.[0]?.text || '';
        const lengthText = vr.lengthText?.simpleText || '';
        const durationSeconds = this.parseDuration(lengthText);

        items.push({
          id: vr.videoId,
          title,
          author,
          durationSeconds,
          thumbnailUrl: `https://i.ytimg.com/vi/${vr.videoId}/hqdefault.jpg`,
        });
      }

      return {
        title: json?.header?.playlistHeaderRenderer?.title?.simpleText || 'Danh sách phát YouTube',
        items,
      };
    } catch (e) {
      console.error('[YouTubeResolver] loadPlaylist error:', e);
      return { title: 'Danh sách phát', items: [] };
    }
  }

  private parseDuration(text: string): number {
    if (!text) return 0;
    const parts = text.split(':').map((p) => parseInt(p, 10));
    if (parts.length === 2 && !isNaN(parts[0]) && !isNaN(parts[1])) {
      return parts[0] * 60 + parts[1];
    }
    if (parts.length === 3 && !isNaN(parts[0]) && !isNaN(parts[1]) && !isNaN(parts[2])) {
      return parts[0] * 3600 + parts[1] * 60 + parts[2];
    }
    return 0;
  }
}

