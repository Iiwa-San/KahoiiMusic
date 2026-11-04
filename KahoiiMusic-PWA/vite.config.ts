import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolveAudio, searchYouTube } from './server/youtubeHelper.js';

export default defineConfig({
  plugins: [
    react(),
    {
      name: 'youtube-api-middleware',
      configureServer(server) {
        server.middlewares.use(async (req, res, next) => {
          const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);

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
            } catch (e: any) {
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
            } catch (e: any) {
              res.statusCode = 500;
              return res.end(JSON.stringify({ error: e.message }));
            }
          }

          next();
        });
      },
    },
  ],
  server: {
    host: '0.0.0.0',
    port: 3000,
  },
});

