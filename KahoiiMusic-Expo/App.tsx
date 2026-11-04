import React, { useEffect } from 'react';
import { LogBox, Platform } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { PlayerProvider } from './src/context/PlayerContext';
import { MainPlayerScreen } from './src/views/MainPlayerScreen';

// Ignore non-fatal Expo Go notifications
LogBox.ignoreLogs([
  'Failed to activate lock screen controls',
  'Failed to start the expo-audio playback service',
  "Response.blob() is using React Native's Blob",
]);

export default function App() {
  useEffect(() => {
    // Progressive Web App (PWA) client configuration & Service Worker registration
    if (Platform.OS === 'web' && typeof document !== 'undefined') {
      // 1. Web App Manifest
      if (!document.querySelector('link[rel="manifest"]')) {
        const link = document.createElement('link');
        link.rel = 'manifest';
        link.href = '/manifest.json';
        document.head.appendChild(link);
      }

      // 2. Apple Mobile Web App Meta & Icon
      if (!document.querySelector('link[rel="apple-touch-icon"]')) {
        const appleIcon = document.createElement('link');
        appleIcon.rel = 'apple-touch-icon';
        appleIcon.href = '/icons/icon-192.png';
        document.head.appendChild(appleIcon);
      }

      const metaTags = [
        { name: 'theme-color', content: '#0d0f18' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' },
        { name: 'apple-mobile-web-app-title', content: 'KahoiiMusic' },
        { name: 'mobile-web-app-capable', content: 'yes' },
      ];

      for (const tag of metaTags) {
        if (!document.querySelector(`meta[name="${tag.name}"]`)) {
          const meta = document.createElement('meta');
          meta.name = tag.name;
          meta.content = tag.content;
          document.head.appendChild(meta);
        }
      }

      // 3. Register Service Worker for offline PWA installation
      if ('serviceWorker' in navigator) {
        navigator.serviceWorker
          .register('/sw.js')
          .then((reg) => console.log('[PWA] Service Worker registered:', reg.scope))
          .catch((err) => console.warn('[PWA] Service Worker registration failed:', err));
      }
    }
  }, []);

  return (
    <SafeAreaProvider>
      <PlayerProvider>
        <MainPlayerScreen />
      </PlayerProvider>
    </SafeAreaProvider>
  );
}
