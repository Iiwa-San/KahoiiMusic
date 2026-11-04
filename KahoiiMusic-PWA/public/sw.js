// KahoiiMusic PWA Service Worker
const CACHE_NAME = 'kahoiimusic-pwa-v1';
const PRECACHE_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/logo_icon.png',
  '/app_logo.png'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(PRECACHE_ASSETS).catch((err) => {
        console.warn('[SW] Precache non-fatal warning:', err);
      });
    }).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.map((key) => {
          if (key !== CACHE_NAME) {
            return caches.delete(key);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// Fetch handler: Network first for API/HTML, cache fallback
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Skip caching YouTube audio streams and API
  if (
    url.hostname.includes('googlevideo.com') ||
    url.hostname.includes('youtube.com') ||
    url.hostname.includes('ytimg.com') ||
    url.pathname.startsWith('/api/') ||
    url.pathname.endsWith('.m4a') ||
    url.pathname.endsWith('.webm') ||
    url.pathname.endsWith('.m3u8')
  ) {
    return;
  }

  if (event.request.method !== 'GET') {
    return;
  }

  event.respondWith(
    fetch(event.request)
      .then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
          const toCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(event.request, toCache);
          });
        }
        return networkResponse;
      })
      .catch(async () => {
        const cached = await caches.match(event.request);
        if (cached) return cached;
        if (event.request.mode === 'navigate') {
          return caches.match('/');
        }
        return new Response('Offline', { status: 503, statusText: 'Service Unavailable' });
      })
  );
});

