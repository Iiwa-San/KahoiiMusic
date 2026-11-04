// Post-export script to enhance dist/index.html with PWA meta tags & Service Worker
const fs = require('fs');
const path = require('path');

const indexPath = path.join(__dirname, '..', 'dist', 'index.html');

if (!fs.existsSync(indexPath)) {
  console.log('[post-export] dist/index.html not found, skipping.');
  process.exit(0);
}

let html = fs.readFileSync(indexPath, 'utf8');

const pwaTags = `
    <!-- PWA Primary Meta Tags -->
    <meta name="theme-color" content="#0d0f18" />
    <meta name="apple-mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
    <meta name="apple-mobile-web-app-title" content="KahoiiMusic" />
    <meta name="application-name" content="KahoiiMusic" />
    <meta name="mobile-web-app-capable" content="yes" />

    <!-- PWA Icons & Manifest -->
    <link rel="manifest" href="/manifest.json" />
    <link rel="icon" type="image/png" href="/icons/icon-192.png" />
    <link rel="apple-touch-icon" href="/icons/icon-192.png" />
    <link rel="apple-touch-icon" sizes="512x512" href="/icons/icon-512.png" />

    <!-- Service Worker Registration -->
    <script>
      if ('serviceWorker' in navigator) {
        window.addEventListener('load', function() {
          navigator.serviceWorker.register('/sw.js')
            .then(function(reg) { console.log('[PWA] Service Worker registered: ', reg.scope); })
            .catch(function(err) { console.warn('[PWA] Service Worker registration failed: ', err); });
        });
      }
    </script>
`;

if (!html.includes('rel="manifest"')) {
  html = html.replace('</head>', `${pwaTags}\n  </head>`);
  fs.writeFileSync(indexPath, html, 'utf8');
  console.log('✅ [post-export] Successfully enhanced dist/index.html with PWA tags!');
} else {
  console.log('[post-export] PWA tags already present in dist/index.html.');
}

