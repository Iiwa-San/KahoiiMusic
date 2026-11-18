// Service Worker for Kahoii Music PWA & Offline Support
const CACHE_NAME = 'kahoii-music-v1';

const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/logo_icon.png',
  '/app_logo.png',
  '/favicon.ico',
  '/favicon.svg',
  '/figma_player.png',
  '/figma_item_thumb.png'
];

// Install: Cache essential app shell assets
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(STATIC_ASSETS).catch((err) => {
        console.warn('[SW] Some static assets failed to precache:', err);
      });
    }).then(() => self.skipWaiting())
  );
});

// Activate: Clean up older caches and claim clients immediately
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
      );
    }).then(() => self.clients.claim())
  );
});

// Fetch: Strategy depending on request type
self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Skip non-GET requests and browser extensions
  if (request.method !== 'GET' || !url.protocol.startsWith('http')) {
    return;
  }

  // 1. Navigation requests (Opening the app when offline):
  // Network first -> fallback to cached /index.html
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () => {
        const cache = await caches.open(CACHE_NAME);
        const cachedIndex = await cache.match('/index.html') || await cache.match('/');
        return cachedIndex || new Response('Kahoii Music đang hoạt động ngoại tuyến. Vui lòng mở lại trang.', {
          status: 200,
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      })
    );
    return;
  }

  // 2. Static Vite build assets (/assets/..., .js, .css, images, fonts):
  // Cache First, then update cache in background (Stale-While-Revalidate)
  const isStaticAsset = (
    url.origin === self.location.origin &&
    (url.pathname.startsWith('/assets/') ||
     url.pathname.endsWith('.js') ||
     url.pathname.endsWith('.css') ||
     url.pathname.endsWith('.png') ||
     url.pathname.endsWith('.jpg') ||
     url.pathname.endsWith('.svg') ||
     url.pathname.endsWith('.woff2') ||
     STATIC_ASSETS.includes(url.pathname))
  );

  if (isStaticAsset) {
    event.respondWith(
      caches.match(request).then((cachedResponse) => {
        const fetchPromise = fetch(request).then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200) {
            const responseClone = networkResponse.clone();
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(request, responseClone);
            });
          }
          return networkResponse;
        }).catch(() => null);

        return cachedResponse || fetchPromise;
      })
    );
    return;
  }

  // 3. API or external streaming requests:
  // Let network handle directly. If offline and fails, it fails gracefully.
  event.respondWith(
    fetch(request).catch(() => {
      // Return empty JSON or 503 if offline and cannot reach API
      if (url.pathname.startsWith('/api/')) {
        return new Response(JSON.stringify({ offline: true, error: "Bạn đang ngoại tuyến" }), {
          status: 503,
          headers: { 'Content-Type': 'application/json; charset=utf-8' }
        });
      }
      return new Response('Ngoại tuyến', { status: 503 });
    })
  );
});

