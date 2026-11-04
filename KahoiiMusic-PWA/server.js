import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { resolveAudio, searchYouTube } from './server/youtubeHelper.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const PORT = process.env.PORT || 3000;
const DIST_DIR = path.join(__dirname, 'dist');

const MIME_TYPES = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.ttf': 'font/ttf',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
};

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    res.statusCode = 200;
    return res.end();
  }

  // 1. API: /api/player?id=...
  if (url.pathname === '/api/player') {
    res.setHeader('Content-Type', 'application/json');
    const id = url.searchParams.get('id');
    if (!id) {
      res.statusCode = 400;
      return res.end(JSON.stringify({ error: 'Missing id' }));
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

  // 2. API: /api/search?q=...
  if (url.pathname === '/api/search') {
    res.setHeader('Content-Type', 'application/json');
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

  // 3. Static files from dist/
  let filePath = path.join(DIST_DIR, url.pathname);
  if (fs.existsSync(filePath) && fs.statSync(filePath).isDirectory()) {
    filePath = path.join(filePath, 'index.html');
  }

  if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';
    res.setHeader('Content-Type', contentType);
    if (ext === '.html') {
      res.setHeader('Cache-Control', 'no-cache');
    } else {
      res.setHeader('Cache-Control', 'public, max-age=31536000, immutable');
    }
    return fs.createReadStream(filePath).pipe(res);
  }

  // 4. SPA Fallback
  const indexPath = path.join(DIST_DIR, 'index.html');
  if (fs.existsSync(indexPath)) {
    res.setHeader('Content-Type', 'text/html');
    return fs.createReadStream(indexPath).pipe(res);
  }

  res.statusCode = 404;
  res.end('Not Found');
});

server.listen(PORT, () => {
  console.log(`\n======================================================`);
  console.log(`  🎵 KahoiiMusic PWA Server is running!`);
  console.log(`  🌐 Local:   http://localhost:${PORT}`);
  console.log(`  📱 Network: http://0.0.0.0:${PORT}`);
  console.log(`======================================================\n`);
});

