import React, { createContext, useContext, useEffect, useRef, useState, useCallback } from 'react';
import { Platform } from 'react-native';
import {
  createAudioPlayer,
  setAudioModeAsync,
  setIsAudioActiveAsync,
  requestNotificationPermissionsAsync,
  AudioPlayer,
  AudioStatus,
} from 'expo-audio';
import {
  PlaylistItem,
  PlaybackStatusType,
  LoopMode,
  GradientPalette,
  AppSettings,
  CloudMusicPayload,
} from '../types/music';
import { YouTubeResolver } from '../services/YouTubeResolver';
import { StorageService } from '../services/StorageService';
import { SavedTracksService } from '../services/SavedTracksService';
import { PlaylistCollectionService } from '../services/PlaylistCollectionService';
import { FirebaseSyncService } from '../services/FirebaseSyncService';
import { File } from 'expo-file-system';

// Dynamic Palettes for background gradient
const PALETTES: GradientPalette[] = [
  { primary: '#1a103c', secondary: '#4c1d95', background: '#0a0518' }, // Neon Violet
  { primary: '#0f2027', secondary: '#203a43', background: '#091014' }, // Deep Teal
  { primary: '#2d0c24', secondary: '#831843', background: '#12040f' }, // Velvet Ruby
  { primary: '#0c2340', secondary: '#1e3a8a', background: '#050d1a' }, // Sapphire Midnight
  { primary: '#1a2e05', secondary: '#14532d', background: '#0a1403' }, // Forest Emerald
  { primary: '#3f1a04', secondary: '#9a3412', background: '#180a02' }, // Ember Glow
];

function getPaletteForTrack(trackId: string): GradientPalette {
  let hash = 0;
  for (let i = 0; i < trackId.length; i++) {
    hash = (hash << 5) - hash + trackId.charCodeAt(i);
    hash |= 0;
  }
  const index = Math.abs(hash) % PALETTES.length;
  return PALETTES[index];
}

interface PlayerContextType {
  // Track & State
  currentTrack: PlaylistItem | null;
  playbackState: PlaybackStatusType;
  currentTime: number;
  duration: number;
  volume: number;
  loopMode: LoopMode;
  isShuffled: boolean;
  statusMessage: string;
  downloadSpeed: string | null;
  errorMessage: string | null;
  currentPalette: GradientPalette;

  // Queue & Lists
  queue: PlaylistItem[];
  currentIndex: number;
  favorites: PlaylistItem[];
  history: PlaylistItem[];
  settings: AppSettings;

  // Controls
  playTrack: (track: PlaylistItem, replaceQueue?: boolean) => Promise<void>;
  play: () => Promise<void>;
  pause: () => Promise<void>;
  togglePlayPause: () => Promise<void>;
  seek: (seconds: number) => Promise<void>;
  playNext: () => Promise<void>;
  playPrevious: () => Promise<void>;
  toggleShuffle: () => void;
  toggleLoopMode: () => void;
  setVolume: (val: number) => Promise<void>;
  addToQueue: (track: PlaylistItem) => void;
  addPlaylistToQueue: (tracks: PlaylistItem[], playFirst?: boolean) => void;
  removeFromQueue: (index: number) => void;
  clearQueue: () => void;
  playQueueIndex: (index: number) => Promise<void>;
  toggleFavorite: (track: PlaylistItem) => Promise<void>;
  isFavorite: (id: string) => boolean;

  // Sleep Timer
  sleepTimerMinutes: number | null;
  sleepTimerRemaining: number | null;
  setSleepTimer: (minutes: number | null) => void;

  // Cloud Sync Helpers
  applyCloudPayload: (payload: CloudMusicPayload) => Promise<void>;
  clearUserDataExceptPlayingTrack: (currentPlayingId?: string) => Promise<void>;
  getCloudPayload: () => CloudMusicPayload;
}

const PlayerContext = createContext<PlayerContextType | null>(null);

export const PlayerProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const playerRef = useRef<AudioPlayer | null>(null);
  const statusSubRef = useRef<{ remove: () => void } | null>(null);
  const playRequestIdRef = useRef<number>(0);

  const [currentTrack, setCurrentTrack] = useState<PlaylistItem | null>(null);
  const [playbackState, setPlaybackState] = useState<PlaybackStatusType>('idle');
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [duration, setDuration] = useState<number>(0);
  const [volume, setVolumeState] = useState<number>(1.0);
  const [loopMode, setLoopMode] = useState<LoopMode>('off');
  const [isShuffled, setIsShuffled] = useState<boolean>(false);
  const [statusMessage, setStatusMessage] = useState<string>('Sẵn sàng');
  const [downloadSpeed, setDownloadSpeed] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentPalette, setCurrentPalette] = useState<GradientPalette>(PALETTES[0]);

  // Speed measurement probe disabled to optimize RAM heap and bandwidth
  const measureSpeed = useCallback((_url: string) => {
    // Zero-overhead no-op: eliminates 384KB arrayBuffer allocations from JS RAM
  }, []);

  const [queue, setQueue] = useState<PlaylistItem[]>([]);
  const [unshuffledQueue, setUnshuffledQueue] = useState<PlaylistItem[]>([]);
  const [currentIndex, setCurrentIndex] = useState<number>(-1);

  const [favorites, setFavorites] = useState<PlaylistItem[]>([]);
  const [history, setHistory] = useState<PlaylistItem[]>([]);
  const [settings, setSettings] = useState<AppSettings>({
    autoPlayNext: true,
    highQualityAudio: true,
    keepHistoryCount: 100,
    showArtworkInBackground: true,
  });

  // Cloud payload snapshot getter
  const getCloudPayload = useCallback((): CloudMusicPayload => {
    return {
      playlists: queue,
      savedTracks: SavedTracksService.getInstance().savedTracks,
      history,
      favorites,
      collections: PlaylistCollectionService.getInstance().collections,
      updatedAt: new Date().toISOString(),
    };
  }, [queue, history, favorites]);

  // Sleep Timer State
  const [sleepTimerMinutes, setSleepTimerMinutes] = useState<number | null>(null);
  const [sleepTimerRemaining, setSleepTimerRemaining] = useState<number | null>(null);
  const sleepTimerIntervalRef = useRef<any>(null);

  // Setup Audio Mode for persistent background audio
  const ensureAudioMode = useCallback(async () => {
    try {
      await setAudioModeAsync({
        playsInSilentMode: true,
        shouldPlayInBackground: true,
        interruptionMode: 'doNotMix',
        allowsRecording: false,
        shouldRouteThroughEarpiece: false,
      });
      await setIsAudioActiveAsync(true);
      if (Platform.OS === 'android') {
        await requestNotificationPermissionsAsync();
      }
    } catch (e) {
      console.warn('[Audio] Failed to configure audio mode:', e);
    }
  }, []);

  useEffect(() => {
    ensureAudioMode();

    // Load initial storage data
    StorageService.getFavorites().then(setFavorites);
    StorageService.getHistory().then(setHistory);
    StorageService.getSettings().then(setSettings);
    StorageService.getSavedQueue().then((saved) => {
      if (saved && saved.length > 0) {
        setQueue(saved);
        setUnshuffledQueue(saved);
      }
    });

    return () => {
      if (statusSubRef.current) {
        statusSubRef.current.remove();
      }
      if (playerRef.current) {
        try {
          playerRef.current.pause();
          playerRef.current.remove();
        } catch (e) {}
      }
    };
  }, []);

  // Sleep timer interval logic
  useEffect(() => {
    if (sleepTimerRemaining !== null && sleepTimerRemaining > 0) {
      sleepTimerIntervalRef.current = setInterval(() => {
        setSleepTimerRemaining((prev) => {
          if (prev === null || prev <= 1) {
            clearInterval(sleepTimerIntervalRef.current!);
            pause();
            setStatusMessage('Hẹn giờ tắt nhạc: Đã dừng');
            return null;
          }
          return prev - 1;
        });
      }, 1000);
    } else {
      if (sleepTimerIntervalRef.current) {
        clearInterval(sleepTimerIntervalRef.current);
      }
    }
    return () => {
      if (sleepTimerIntervalRef.current) {
        clearInterval(sleepTimerIntervalRef.current);
      }
    };
  }, [sleepTimerRemaining]);

  const setSleepTimer = useCallback((minutes: number | null) => {
    setSleepTimerMinutes(minutes);
    if (minutes === null || minutes <= 0) {
      setSleepTimerRemaining(null);
    } else {
      setSleepTimerRemaining(minutes * 60);
    }
  }, []);

  // Controls Implementation
  const play = useCallback(async () => {
    if (playerRef.current) {
      try {
        await ensureAudioMode();
        playerRef.current.play();
        setPlaybackState('playing');
        setStatusMessage('Đang phát');
      } catch (e) {
        console.error('Play error:', e);
      }
    }
  }, [ensureAudioMode]);

  const pause = useCallback(async () => {
    if (playerRef.current) {
      try {
        playerRef.current.pause();
        setPlaybackState('paused');
        setStatusMessage('Tạm dừng');
      } catch (e) {
        console.error('Pause error:', e);
      }
    }
  }, []);

  const togglePlayPause = useCallback(async () => {
    if (playbackState === 'playing') {
      await pause();
    } else {
      await play();
    }
  }, [playbackState, play, pause]);

  const seek = useCallback(
    async (seconds: number) => {
      if (playerRef.current) {
        try {
          const clamped = Math.max(0, Math.min(seconds, duration || seconds));
          await playerRef.current.seekTo(clamped);
          setCurrentTime(clamped);
        } catch (e) {
          console.error('Seek error:', e);
        }
      }
    },
    [duration]
  );

  const setVolume = useCallback(async (val: number) => {
    const clamped = Math.max(0, Math.min(1.0, val));
    setVolumeState(clamped);
    if (playerRef.current) {
      playerRef.current.volume = clamped;
    }
  }, []);

  const playQueueIndex = useCallback(
    async (index: number) => {
      if (index >= 0 && index < queue.length) {
        setCurrentIndex(index);
        await playTrack(queue[index], false);
      }
    },
    [queue]
  );

  const playNext = useCallback(async () => {
    if (queue.length === 0) return;
    let nextIndex = currentIndex + 1;
    if (nextIndex >= queue.length) {
      if (loopMode === 'all') {
        nextIndex = 0;
      } else {
        await pause();
        return;
      }
    }
    await playQueueIndex(nextIndex);
  }, [queue, currentIndex, loopMode, pause, playQueueIndex]);

  const playPrevious = useCallback(async () => {
    if (queue.length === 0) return;
    if (currentTime > 3) {
      await seek(0);
      return;
    }
    let prevIndex = currentIndex - 1;
    if (prevIndex < 0) {
      prevIndex = loopMode === 'all' ? queue.length - 1 : 0;
    }
    await playQueueIndex(prevIndex);
  }, [queue, currentIndex, currentTime, loopMode, seek, playQueueIndex]);

  const handleTrackFinished = useCallback(() => {
    if (loopMode === 'one') {
      seek(0);
      play();
    } else if (settings.autoPlayNext) {
      playNext();
    } else {
      setPlaybackState('paused');
    }
  }, [loopMode, settings.autoPlayNext, playNext, seek, play]);

  // Main playTrack implementation
  const playTrack = useCallback(
    async (track: PlaylistItem, replaceQueue: boolean = false) => {
      const currentRequestId = ++playRequestIdRef.current;

      // 0ms immediate stop of old audio
      if (playerRef.current) {
        try {
          playerRef.current.pause();
        } catch (e) {}
        if (statusSubRef.current) {
          statusSubRef.current.remove();
          statusSubRef.current = null;
        }
        try {
          playerRef.current.remove();
        } catch (e) {}
        playerRef.current = null;
      }

      setPlaybackState('buffering');
      setStatusMessage('Đang chuẩn bị bài hát...');
      setErrorMessage(null);
      setCurrentTime(0);
      setDownloadSpeed(null);
      setCurrentPalette(getPaletteForTrack(track.id));

      // Manage Queue
      if (replaceQueue) {
        setQueue([track]);
        setUnshuffledQueue([track]);
        setCurrentIndex(0);
        StorageService.saveQueue([track]);
      } else {
        setQueue((prevQueue) => {
          const idx = prevQueue.findIndex((item) => item.id === track.id);
          let newQueue = [...prevQueue];
          if (idx >= 0) {
            setCurrentIndex(idx);
          } else {
            newQueue.unshift(track);
            setCurrentIndex(0);
          }
          StorageService.saveQueue(newQueue);
          return newQueue;
        });
      }

      // Ensure audio session is properly configured for background audio
      await ensureAudioMode();

      // 1. Check Offline file first (Highest priority: Instant offline playback)
      const savedService = SavedTracksService.getInstance();
      const localAudio = track.localAudioUri || savedService.getLocalAudioUri(track.id);
      if (localAudio) {
        try {
          const targetFile = new File(localAudio);
          if (targetFile.exists && targetFile.size > 1024 * 50) {
            if (currentRequestId !== playRequestIdRef.current) return;

            const updatedTrack: PlaylistItem = {
              ...track,
              localAudioUri: localAudio,
            };
            setCurrentTrack(updatedTrack);
            setDuration(track.durationSeconds);
            StorageService.recordPlay(updatedTrack, settings.keepHistoryCount).then((h) => {
              setHistory(h);
              FirebaseSyncService.getInstance().requestAutoUpload(getCloudPayload);
            });

            // Create AudioPlayer with keepAudioSessionActive = true for background audio
            const player = createAudioPlayer(localAudio, {
              updateInterval: 250,
              keepAudioSessionActive: true,
              preferredForwardBufferDuration: 30,
            });
            player.volume = volume;
            player.loop = loopMode === 'one';

            try {
              player.setActiveForLockScreen(
                true,
                {
                  title: track.title,
                  artist: track.author || 'YouTube',
                  artworkUrl: track.thumbnailUrl,
                },
                {
                  showNextTrack: true,
                  showPreviousTrack: true,
                  showSeekForward: true,
                  showSeekBackward: true,
                  seekForwardIntervalSeconds: 10,
                  seekBackwardIntervalSeconds: 10,
                }
              );
            } catch (e) {
              console.warn('[LockScreen] Error setting offline metadata:', e);
            }

            const sub = player.addListener('playbackStatusUpdate', (status: AudioStatus) => {
              if (currentRequestId !== playRequestIdRef.current) return;
              if (status.error) {
                setPlaybackState('error');
                setErrorMessage('Lỗi phát âm thanh: ' + status.error);
                return;
              }
              setCurrentTime(status.currentTime || 0);
              if (status.duration && status.duration > 0) setDuration(status.duration);
              if (status.playing) {
                setPlaybackState('playing');
                setStatusMessage('Đang phát Offline');
              } else {
                setPlaybackState('paused');
                setStatusMessage('Tạm dừng');
              }
              if (status.didJustFinish && !status.loop) {
                handleTrackFinished();
              }
            });

            statusSubRef.current = sub;
            playerRef.current = player;
            player.play();
            setPlaybackState('playing');
            setStatusMessage('Đang phát Offline');
            return;
          }
        } catch {
          // fallback to online stream
        }
      }

      try {
        // Resolve stream URL
        const resolver = YouTubeResolver.getInstance();
        const resolved = await resolver.resolveAudioStream(track.id);

        // If user changed to another song while resolving, abort immediately!
        if (currentRequestId !== playRequestIdRef.current) {
          return;
        }

        const updatedTrack: PlaylistItem = {
          ...track,
          title: resolved.title || track.title,
          author: resolved.author || track.author,
          durationSeconds: resolved.durationSeconds || track.durationSeconds,
          streamUrl: resolved.streamUrl,
          thumbnailUrl: resolved.thumbnailUrl || track.thumbnailUrl,
        };
        setCurrentTrack(updatedTrack);
        setDuration(resolved.durationSeconds || track.durationSeconds);

        // Record history
        StorageService.recordPlay(updatedTrack, settings.keepHistoryCount).then((h) => {
          setHistory(h);
          FirebaseSyncService.getInstance().requestAutoUpload(getCloudPayload);
        });

        // Pre-warm and measure network download speed in background
        measureSpeed(resolved.streamUrl);

        // Re-check cancellation before creating player
        if (currentRequestId !== playRequestIdRef.current) {
          return;
        }

        // Create new AudioPlayer with expo-audio with keepAudioSessionActive = true
        const player = createAudioPlayer(
          {
            uri: resolved.streamUrl,
            headers: {
              'User-Agent':
                'Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15',
              Referer: 'https://www.youtube.com/',
            },
          },
          {
            updateInterval: 250,
            keepAudioSessionActive: true,
            preferredForwardBufferDuration: 30,
          }
        );
        player.volume = volume;
        player.loop = loopMode === 'one';

        // Configure lock screen media controls (essential for OS background audio keep-alive)
        try {
          player.setActiveForLockScreen(
            true,
            {
              title: updatedTrack.title,
              artist: updatedTrack.author || 'YouTube',
              artworkUrl: updatedTrack.thumbnailUrl,
            },
            {
              showNextTrack: true,
              showPreviousTrack: true,
              showSeekForward: true,
              showSeekBackward: true,
              seekForwardIntervalSeconds: 10,
              seekBackwardIntervalSeconds: 10,
            }
          );
        } catch (e) {
          console.warn('[LockScreen] Error setting online metadata:', e);
        }

        // Listen for playback status changes
        const sub = player.addListener('playbackStatusUpdate', (status: AudioStatus) => {
          if (currentRequestId !== playRequestIdRef.current) {
            return;
          }

          if (status.error) {
            console.error(`[Kahoii Audio Error]: ${status.error}`);
            setPlaybackState('error');
            setErrorMessage('Lỗi phát âm thanh: ' + status.error);
            return;
          }

          setCurrentTime(status.currentTime || 0);
          if (status.duration && status.duration > 0) {
            setDuration(status.duration);
          }

          if (status.isBuffering) {
            setStatusMessage('Đang tải đệm...');
            measureSpeed(resolved.streamUrl);
          } else if (status.playing) {
            setPlaybackState('playing');
            setStatusMessage('Đang phát');
          } else {
            setPlaybackState('paused');
            setStatusMessage('Tạm dừng');
          }

          if (status.didJustFinish && !status.loop) {
            handleTrackFinished();
          }
        });

        statusSubRef.current = sub;
        playerRef.current = player;
        player.play();
        setPlaybackState('playing');
        setStatusMessage('Đang phát');
      } catch (err: any) {
        if (currentRequestId !== playRequestIdRef.current) {
          return;
        }
        console.error('[AudioService] Playback failed:', err);
        let msg = err.message || 'Không thể phát bài hát này';
        if (
          msg.includes('UnknownHostException') ||
          msg.includes('Unable to resolve host') ||
          msg.includes('Network request failed') ||
          msg.includes('SocketException')
        ) {
          msg = 'Mất kết nối Internet. Vui lòng kiểm tra lại mạng Wi-Fi/4G.';
        }
        setErrorMessage(msg);
        setPlaybackState('error');
        setStatusMessage('Lỗi mạng');
      }
    },
    [volume, loopMode, settings.keepHistoryCount, measureSpeed, handleTrackFinished, getCloudPayload, ensureAudioMode]
  );

  const toggleShuffle = useCallback(() => {
    setIsShuffled((prev) => {
      const next = !prev;
      if (next) {
        // Shuffle queue keeping current track at index 0
        const current = queue[currentIndex];
        const rest = queue.filter((_, i) => i !== currentIndex);
        for (let i = rest.length - 1; i > 0; i--) {
          const j = Math.floor(Math.random() * (i + 1));
          [rest[i], rest[j]] = [rest[j], rest[i]];
        }
        const shuffled = current ? [current, ...rest] : rest;
        setQueue(shuffled);
        setCurrentIndex(current ? 0 : -1);
      } else {
        // Restore unshuffled
        setQueue(unshuffledQueue);
        if (currentTrack) {
          const idx = unshuffledQueue.findIndex((t) => t.id === currentTrack.id);
          setCurrentIndex(idx >= 0 ? idx : 0);
        }
      }
      return next;
    });
  }, [queue, currentIndex, unshuffledQueue, currentTrack]);

  const toggleLoopMode = useCallback(() => {
    setLoopMode((prev) => {
      let next: LoopMode = 'off';
      if (prev === 'off') next = 'all';
      else if (prev === 'all') next = 'one';
      else next = 'off';

      if (playerRef.current) {
        playerRef.current.loop = next === 'one';
      }
      return next;
    });
  }, []);

  const addToQueue = useCallback((track: PlaylistItem) => {
    setQueue((prev) => {
      const updated = [...prev, track];
      StorageService.saveQueue(updated);
      return updated;
    });
    setUnshuffledQueue((prev) => [...prev, track]);
  }, []);

  const addPlaylistToQueue = useCallback(
    (tracks: PlaylistItem[], playFirst: boolean = false) => {
      if (tracks.length === 0) return;
      if (playFirst) {
        playTrack(tracks[0], true);
        if (tracks.length > 1) {
          const remaining = tracks.slice(1);
          setQueue((prev) => {
            const updated = [...prev, ...remaining];
            StorageService.saveQueue(updated);
            return updated;
          });
          setUnshuffledQueue((prev) => [...prev, ...remaining]);
        }
      } else {
        setQueue((prev) => {
          const updated = [...prev, ...tracks];
          StorageService.saveQueue(updated);
          return updated;
        });
        setUnshuffledQueue((prev) => [...prev, ...tracks]);
      }
    },
    [playTrack]
  );

  const removeFromQueue = useCallback(
    (index: number) => {
      setQueue((prev) => {
        const updated = prev.filter((_, i) => i !== index);
        StorageService.saveQueue(updated);
        return updated;
      });
      setUnshuffledQueue((prev) => prev.filter((_, i) => i !== index));
      if (index === currentIndex) {
        if (queue.length > 1) {
          const nextIdx = index < queue.length - 1 ? index : 0;
          playQueueIndex(nextIdx);
        } else {
          if (playerRef.current) {
            try {
              playerRef.current.pause();
              playerRef.current.remove();
            } catch (e) {}
            playerRef.current = null;
          }
          setCurrentTrack(null);
          setPlaybackState('idle');
        }
      } else if (index < currentIndex) {
        setCurrentIndex((prev) => prev - 1);
      }
    },
    [currentIndex, queue, playQueueIndex]
  );

  const clearQueue = useCallback(() => {
    setQueue([]);
    setUnshuffledQueue([]);
    setCurrentIndex(-1);
    StorageService.saveQueue([]);
    if (playerRef.current) {
      try {
        playerRef.current.pause();
        playerRef.current.remove();
      } catch (e) {}
      playerRef.current = null;
    }
    setCurrentTrack(null);
    setPlaybackState('idle');
  }, []);

  const toggleFavorite = useCallback(
    async (track: PlaylistItem) => {
      const { favorites: updatedFavs } = await StorageService.toggleFavorite(track);
      setFavorites(updatedFavs);
      FirebaseSyncService.getInstance().requestAutoUpload(() => ({
        playlists: queue,
        savedTracks: SavedTracksService.getInstance().savedTracks,
        history,
        favorites: updatedFavs,
        collections: PlaylistCollectionService.getInstance().collections,
        updatedAt: new Date().toISOString(),
      }));
    },
    [queue, history]
  );

  const isFavorite = useCallback(
    (id: string) => {
      return favorites.some((item) => item.id === id);
    },
    [favorites]
  );

  // Cloud Sync Helpers
  const applyCloudPayload = useCallback(
    async (payload: CloudMusicPayload) => {
      // 1. Favorites merge
      setFavorites((prevFavs) => {
        const map = new Map(prevFavs.map((f) => [f.id, f]));
        payload.favorites.forEach((f) => {
          if (!map.has(f.id)) map.set(f.id, f);
        });
        const merged = Array.from(map.values());
        StorageService.saveFavorites(merged);
        return merged;
      });

      // 2. History merge
      setHistory((prevHist) => {
        const map = new Map(prevHist.map((h) => [h.id, h]));
        payload.history.forEach((h) => {
          if (!map.has(h.id)) map.set(h.id, h);
        });
        const merged = Array.from(map.values());
        StorageService.saveHistory(merged);
        return merged;
      });

      // 3. Queue merge
      if (payload.playlists && payload.playlists.length > 0) {
        setQueue((prev) => {
          if (prev.length <= 1) {
            StorageService.saveQueue(payload.playlists);
            setUnshuffledQueue(payload.playlists);
            return payload.playlists;
          }
          return prev;
        });
      }

      // 4. Collections merge
      const colService = PlaylistCollectionService.getInstance();
      const localCols = colService.collections;
      const mergedCols = [...localCols];
      payload.collections.forEach((cloudCol) => {
        const localIdx = mergedCols.findIndex((c) => c.id === cloudCol.id);
        if (localIdx >= 0) {
          const itemMap = new Map(mergedCols[localIdx].items.map((i) => [i.id, i]));
          cloudCol.items.forEach((item) => {
            if (!itemMap.has(item.id)) itemMap.set(item.id, item);
          });
          mergedCols[localIdx] = {
            ...mergedCols[localIdx],
            items: Array.from(itemMap.values()),
          };
        } else {
          mergedCols.push(cloudCol);
        }
      });
      await colService.setCollections(mergedCols);

      // 5. Auto download missing savedTracks
      const savedService = SavedTracksService.getInstance();
      for (const track of payload.savedTracks) {
        if (!savedService.isSaved(track.id)) {
          savedService.saveTrackAsync(track).catch((e) => {
            console.warn('[Sync] Auto download failed for track:', track.id, e);
          });
        }
      }
    },
    []
  );

  const clearUserDataExceptPlayingTrack = useCallback(
    async (currentPlayingId?: string) => {
      // 1. Keep only playing track in queue
      setQueue((prev) => {
        const remaining = prev.filter((t) => t.id === currentPlayingId);
        StorageService.saveQueue(remaining);
        setUnshuffledQueue(remaining);
        setCurrentIndex(remaining.length > 0 ? 0 : -1);
        return remaining;
      });

      // 2. Clear favorites and history
      setFavorites([]);
      await StorageService.saveFavorites([]);
      setHistory([]);
      await StorageService.saveHistory([]);

      // 3. Clear collections
      await PlaylistCollectionService.getInstance().clearAll();

      // 4. Clear saved tracks except current playing
      await SavedTracksService.getInstance().clearAllSaved(currentPlayingId);
    },
    []
  );

  return (
    <PlayerContext.Provider
      value={{
        currentTrack,
        playbackState,
        currentTime,
        duration,
        volume,
        loopMode,
        isShuffled,
        statusMessage,
        downloadSpeed,
        errorMessage,
        currentPalette,
        queue,
        currentIndex,
        favorites,
        history,
        settings,
        playTrack,
        play,
        pause,
        togglePlayPause,
        seek,
        playNext,
        playPrevious,
        toggleShuffle,
        toggleLoopMode,
        setVolume,
        addToQueue,
        addPlaylistToQueue,
        removeFromQueue,
        clearQueue,
        playQueueIndex,
        toggleFavorite,
        isFavorite,
        sleepTimerMinutes,
        sleepTimerRemaining,
        setSleepTimer,
        applyCloudPayload,
        clearUserDataExceptPlayingTrack,
        getCloudPayload,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = () => {
  const context = useContext(PlayerContext);
  if (!context) {
    throw new Error('usePlayer must be used within a PlayerProvider');
  }
  return context;
};
