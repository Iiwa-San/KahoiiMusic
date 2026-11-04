const { getDefaultConfig } = require('expo/metro-config');
const { resolveAudio, searchYouTube } = require('./api/youtubeHelper');

const config = getDefaultConfig(__dirname);

// API proxy middleware for Web / PWA development
config.server = {
  ...config.server,
  enhanceMiddleware: (metroMiddleware) => {
    return async (req, res, next) => {
      const url = new URL(req.url, 'http://localhost');

      // 1. /api/player?id=...
      if (url.pathname === '/api/player') {
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
        res.setHeader('Content-Type', 'application/json');

        if (req.method === 'OPTIONS') {
          res.statusCode = 200;
          return res.end();
        }

        const id = url.searchParams.get('id');
        if (!id) {
          res.statusCode = 400;
          return res.end(JSON.stringify({ error: 'Missing id parameter' }));
        }

        try {
          const data = await resolveAudio(id);
          res.statusCode = 200;
          return res.end(JSON.stringify(data));
        } catch (e) {
          res.statusCode = 500;
          return res.end(JSON.stringify({ error: e.message }));
        }
      }

      // 2. /api/search?q=...
      if (url.pathname === '/api/search') {
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
        res.setHeader('Content-Type', 'application/json');

        if (req.method === 'OPTIONS') {
          res.statusCode = 200;
          return res.end();
        }

        const q = url.searchParams.get('q') || '';
        try {
          const data = await searchYouTube(q);
          res.statusCode = 200;
          return res.end(JSON.stringify(data));
        } catch (e) {
          res.statusCode = 500;
          return res.end(JSON.stringify({ error: e.message }));
        }
      }

      return metroMiddleware(req, res, next);
    };
  },
};

module.exports = config;

