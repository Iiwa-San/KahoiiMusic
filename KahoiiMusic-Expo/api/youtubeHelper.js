// Shared YouTube helper for Web API endpoints & Metro middleware
const FALLBACK_VISITOR_DATA =
  'CgtkQTNlZU9ucUxRNCiV4arXBjIKCgJWThIEGgAgUmKrAgqoAkN0c0JDQVVTalFFQkRCNlBERzBsUUVpbF8yZWx3NWJQZUdEZWthZDBNZ1pZZFRQaDdrZGVwVUY2dHFFTjY1UmM0U0JxVHVxMk41VVBCeWltek1OU1d2bDlvLW9YREFxQ3FkSzJRdHNVR3lRbGI2VzFyUlZENWxZdGJWM3NJRVVqNTVxSWlCSzFmNHRwUjBRbEI2ODNRR2xfTkZPZHBZVFVWdUcxQlUzUHVPWDV4UzAwbDBjNzR5bnN4SWZKa25ORVY4RHNqNDhvQlRKRkFSeFk5V29mZFJTeWdrUXZySGticks0d0pSY3NWV3FLMVh0NnNCTkZvcjU4ZWdjRHlVYUVMaXZyOVVFeG9yeFdjMVlQVGljOVVMTEltM0tMRTdfVjEtOFVIcXdQ';

let cachedToken = FALLBACK_VISITOR_DATA;
let cachedTokenTime = 0;

async function getVisitorToken() {
  if (cachedToken && Date.now() - cachedTokenTime < 12 * 3600 * 1000) {
    return cachedToken;
  }
  try {
    const controller = new AbortController();
    const t = setTimeout(() => controller.abort(), 4000);
    const res = await fetch('https://www.youtube.com/sw.js_data', { signal: controller.signal });
    clearTimeout(t);
    const text = await res.text();
    const clean = text.replace(/^\)\]\}'/, '').trim();
    const data = JSON.parse(clean);

    function find(obj) {
      if (typeof obj === 'string' && obj.startsWith('Cg') && obj.length > 50) return obj;
      if (Array.isArray(obj)) {
        for (const item of obj) {
          const f = find(item);
          if (f) return f;
        }
      } else if (typeof obj === 'object' && obj !== null) {
        for (const k of Object.keys(obj)) {
          const f = find(obj[k]);
          if (f) return f;
        }
      }
      return null;
    }
    const token = find(data);
    if (token) {
      cachedToken = token;
      cachedTokenTime = Date.now();
      return token;
    }
  } catch {
    // fallback
  }
  return cachedToken;
}

async function resolveAudio(videoId) {
  const token = await getVisitorToken();
  const payload = {
    context: {
      client: {
        clientName: 'VISIONOS',
        clientVersion: '1.02',
        deviceMake: 'Apple',
        deviceModel: 'RealityDevice17,1',
        userAgent:
          'Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15',
        osName: 'visionOS',
        osVersion: '26.5.23O471',
        hl: 'vi',
        visitorData: token,
      },
    },
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
      'User-Agent': payload.context.client.userAgent,
      'X-Youtube-Client-Name': '101',
      'X-Youtube-Client-Version': '1.02',
      Origin: 'https://www.youtube.com',
      'X-Goog-Visitor-Id': token,
    },
    body: JSON.stringify(payload),
  });

  const j = await res.json();
  if (j?.playabilityStatus?.status && j.playabilityStatus.status !== 'OK') {
    throw new Error(j.playabilityStatus.reason || j.playabilityStatus.status);
  }

  const formats = j?.streamingData?.adaptiveFormats || [];
  let bestUrl = null;
  let highestBitrate = 0;
  let contentLength;

  for (const f of formats) {
    const mime = f.mimeType || '';
    const bitrate = f.bitrate || 0;
    if ((mime.includes('audio/mp4') || mime.includes('m4a')) && f.url && bitrate > highestBitrate) {
      highestBitrate = bitrate;
      bestUrl = f.url;
      contentLength = f.contentLength ? parseInt(f.contentLength, 10) : undefined;
    }
  }

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

  if (!bestUrl) {
    bestUrl = j?.streamingData?.hlsManifestUrl;
  }

  return {
    streamUrl: bestUrl,
    title: j?.videoDetails?.title || 'Bài hát YouTube',
    author: j?.videoDetails?.author || 'YouTube',
    durationSeconds: parseInt(j?.videoDetails?.lengthSeconds || '0', 10),
    thumbnailUrl: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
    contentLength,
  };
}

async function searchYouTube(query) {
  const trimmed = (query || '').trim();
  if (!trimmed) return [];

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
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) return [];
  const json = await res.json();
  const sections =
    json?.contents?.twoColumnSearchResultsRenderer?.primaryContents?.sectionListRenderer?.contents || [];

  const results = [];
  for (const section of sections) {
    const items = section?.itemSectionRenderer?.contents || [];
    for (const item of items) {
      const video = item.videoRenderer;
      if (!video || !video.videoId) continue;

      const title = video.title?.runs?.[0]?.text || 'Video YouTube';
      const author = video.ownerText?.runs?.[0]?.text || '';
      const lengthText = video.lengthText?.simpleText || '';
      const durationSeconds = parseDuration(lengthText);

      results.push({
        id: video.videoId,
        title,
        author,
        durationSeconds,
        thumbnailUrl: `https://i.ytimg.com/vi/${video.videoId}/hqdefault.jpg`,
      });
    }
  }
  return results;
}

function parseDuration(text) {
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

module.exports = {
  resolveAudio,
  searchYouTube,
};

