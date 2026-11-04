import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
  useCallback,
  ReactNode,
} from 'react';
import {
  PlaylistItem,
  PlaybackStatusType,
  LoopMode,
  GradientPalette,
  AppSettings,
} from '../types/music';
import { StorageService } from '../services/StorageService';
import { YouTubeResolver } from '../services/YouTubeResolver';
import { OfflineAudioService } from '../services/OfflineAudioService';

const PALETTES: GradientPalette[] = [
  { primary: '#3b82f6', secondary: '#1e1b4b', background: '#0a0a14' },
  { primary: '#8b5cf6', secondary: '#2e1065', background: '#090812' },
  { primary: '#ec4899', secondary: '#4a044e', background: '#0d0814' },
  { primary: '#10b981', secondary: '#022c22', background: '#060f0d' },
  { primary: '#f59e0b', secondary: '#451a03', background: '#0e0b06' },
];

interface PlayerContextType {
  currentTrack: PlaylistItem | null;
  playbackState: PlaybackStatusType;
  currentTime: number;
  duration: number;
  volume: number;
  loopMode: LoopMode;
  isShuffled: boolean;
  statusMessage: string;
  errorMessage: string | null;
  currentPalette: GradientPalette;
  queue: PlaylistItem[];
  currentIndex: number;
  favorites: PlaylistItem[];
  history: PlaylistItem[];
  offlineTracks: PlaylistItem[];
  sleepTimerRemaining: number | null;
  settings: AppSettings;

  playTrack: (track: PlaylistItem) => Promise<void>;
  playUrl: (url: string) => Promise<void>;
  togglePlayPause: () => void;
  playNext: () => void;
  playPrevious: () => void;
  seekTo: (seconds: number) => void;
  setVolume: (vol: number) => void;
  toggleLoopMode: () => void;
  toggleShuffle: () => void;
  addToQueue: (track: PlaylistItem) => void;
  removeFromQueue: (index: number) => void;
  clearQueue: () => void;
  playQueueIndex: (index: number) => void;
  isFavorite: (id: string) => boolean;
  toggleFavorite: (track: PlaylistItem) => void;
  startSleepTimer: (minutes: number) => void;
  cancelSleepTimer: () => void;
  clearHistory: () => void;
  updateSettings: (newSettings: Partial<AppSettings>) => void;
  downloadTrackOffline: (track: PlaylistItem) => Promise<boolean>;
  isOffline: (id: string) => boolean;
  deleteOfflineTrack: (id: string) => Promise<void>;
}

const PlayerContext = createContext<PlayerContextType | null>(null);

export const PlayerProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const [currentTrack, setCurrentTrack] = useState<PlaylistItem | null>(null);
  const [playbackState, setPlaybackState] = useState<PlaybackStatusType>('idle');
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [duration, setDuration] = useState<number>(0);
  const [volume, setVolumeState] = useState<number>(1.0);
  const [loopMode, setLoopMode] = useState<LoopMode>('off');
  const [isShuffled, setIsShuffled] = useState<boolean>(false);
  const [statusMessage, setStatusMessage] = useState<string>('Sẵn sàng');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentPalette, setCurrentPalette] = useState<GradientPalette>(PALETTES[0]);

  const [queue, setQueue] = useState<PlaylistItem[]>(() => StorageService.loadQueue());
  const [unshuffledQueue, setUnshuffledQueue] = useState<PlaylistItem[]>(() => StorageService.loadQueue());
  const [currentIndex, setCurrentIndex] = useState<number>(-1);

  const [favorites, setFavorites] = useState<PlaylistItem[]>(() => StorageService.loadFavorites());
  const [history, setHistory] = useState<PlaylistItem[]>(() => StorageService.loadHistory());
  const [offlineTracks, setOfflineTracks] = useState<PlaylistItem[]>([]);
  const [settings, setSettings] = useState<AppSettings>(() => StorageService.loadSettings());

  const [sleepTimerRemaining, setSleepTimerRemaining] = useState<number | null>(null);
  const sleepTimerRef = useRef<number | null>(null);
  const playRequestIdRef = useRef<number>(0);

  // Subscribe to offline IndexedDB tracks
  useEffect(() => {
    const offlineService = OfflineAudioService.getInstance();
    offlineService.getAllOfflineTracks().then(setOfflineTracks);
    const unsub = offlineService.subscribe(() => {
      offlineService.getAllOfflineTracks().then(setOfflineTracks);
    });
    return unsub;
  }, []);

  // Initialize HTML5 Audio element
  useEffect(() => {
    const audio = new Audio();
    audio.preload = 'auto';
    audioRef.current = audio;

    const onTimeUpdate = () => {
      setCurrentTime(audio.currentTime);
      // Update position state for Lock Screen scrubber
      if ('mediaSession' in navigator && navigator.mediaSession.setPositionState && audio.duration) {
        try {
          navigator.mediaSession.setPositionState({
            duration: audio.duration,
            playbackRate: audio.playbackRate,
            position: audio.currentTime,
          });
        } catch {
          // ignore
        }
      }
    };

    const onLoadedMetadata = () => {
      if (audio.duration && !isNaN(audio.duration)) {
        setDuration(audio.duration);
      }
    };

    const onPlaying = () => {
      setPlaybackState('playing');
      setStatusMessage('Đang phát');
      if ('mediaSession' in navigator) {
        navigator.mediaSession.playbackState = 'playing';
      }
    };

    const onPause = () => {
      if (playbackState !== 'loading') {
        setPlaybackState('paused');
        setStatusMessage('Tạm dừng');
        if ('mediaSession' in navigator) {
          navigator.mediaSession.playbackState = 'paused';
        }
      }
    };

    const onWaiting = () => {
      setStatusMessage('Đang tải đệm...');
    };

    const onEnded = () => {
      handleTrackEnded();
    };

    const onError = () => {
      setPlaybackState('error');
      setErrorMessage('Lỗi phát âm thanh. Vui lòng thử lại.');
      setStatusMessage('Lỗi phát');
    };

    audio.addEventListener('timeupdate', onTimeUpdate);
    audio.addEventListener('loadedmetadata', onLoadedMetadata);
    audio.addEventListener('playing', onPlaying);
    audio.addEventListener('pause', onPause);
    audio.addEventListener('waiting', onWaiting);
    audio.addEventListener('ended', onEnded);
    audio.addEventListener('error', onError);

    return () => {
      audio.pause();
      audio.removeEventListener('timeupdate', onTimeUpdate);
      audio.removeEventListener('loadedmetadata', onLoadedMetadata);
      audio.removeEventListener('playing', onPlaying);
      audio.removeEventListener('pause', onPause);
      audio.removeEventListener('waiting', onWaiting);
      audio.removeEventListener('ended', onEnded);
      audio.removeEventListener('error', onError);
    };
  }, []);

  // Update MediaSession lock screen metadata when current track changes
  useEffect(() => {
    if (!currentTrack || !('mediaSession' in navigator)) return;

    try {
      navigator.mediaSession.metadata = new MediaMetadata({
        title: currentTrack.title,
        artist: currentTrack.author || 'YouTube',
        album: 'KahoiiMusic',
        artwork: [
          { src: currentTrack.thumbnailUrl, sizes: '96x96', type: 'image/jpeg' },
          { src: currentTrack.thumbnailUrl, sizes: '128x128', type: 'image/jpeg' },
          { src: currentTrack.thumbnailUrl, sizes: '256x256', type: 'image/jpeg' },
          { src: currentTrack.thumbnailUrl, sizes: '512x512', type: 'image/jpeg' },
        ],
      });
    } catch (e) {
      console.warn('[MediaSession] Metadata error:', e);
    }
  }, [currentTrack]);

  // Handle track finished
  const handleTrackEnded = useCallback(() => {
    if (loopMode === 'one' && audioRef.current) {
      audioRef.current.currentTime = 0;
      audioRef.current.play();
      return;
    }

    if (currentIndex + 1 < queue.length) {
      const nextIdx = currentIndex + 1;
      setCurrentIndex(nextIdx);
      playTrack(queue[nextIdx]);
    } else if (loopMode === 'all' && queue.length > 0) {
      setCurrentIndex(0);
      playTrack(queue[0]);
    } else {
      setPlaybackState('paused');
      setStatusMessage('Đã hết danh sách');
    }
  }, [loopMode, currentIndex, queue]);

  // Main track player
  const playTrack = useCallback(
    async (track: PlaylistItem) => {
      const requestId = ++playRequestIdRef.current;
      setErrorMessage(null);
      setPlaybackState('loading');
      setStatusMessage('Đang lấy luồng âm thanh...');

      // Dynamic color palette rotation based on title hash
      const hash = track.title.split('').reduce((acc, c) => acc + c.charCodeAt(0), 0);
      setCurrentPalette(PALETTES[hash % PALETTES.length]);

      try {
        // 1. Check IndexedDB offline cache first for 0ms offline playback
        const offlineUrl = await OfflineAudioService.getInstance().getOfflineAudioUrl(track.id);
        if (offlineUrl && audioRef.current) {
          if (requestId !== playRequestIdRef.current) return;
          setCurrentTrack(track);
          setDuration(track.durationSeconds);
          const updatedHistory = StorageService.recordPlay(track, settings.keepHistoryCount);
          setHistory(updatedHistory);

          audioRef.current.src = offlineUrl;
          audioRef.current.volume = volume;
          audioRef.current.loop = loopMode === 'one';
          await audioRef.current.play();
          setPlaybackState('playing');
          setStatusMessage('Đang phát Ngoại tuyến');
          return;
        }

        // 2. Online stream resolution
        const resolved = await YouTubeResolver.getInstance().resolveAudioStream(track.id);
        if (requestId !== playRequestIdRef.current) return;

        const updatedTrack: PlaylistItem = {
          ...track,
          title: resolved.title || track.title,
          author: resolved.author || track.author,
          durationSeconds: resolved.durationSeconds || track.durationSeconds,
          streamUrl: resolved.streamUrl,
          thumbnailUrl: resolved.thumbnailUrl || track.thumbnailUrl,
        };

        setCurrentTrack(updatedTrack);
        setDuration(updatedTrack.durationSeconds);

        // Record history
        const updatedHistory = StorageService.recordPlay(updatedTrack, settings.keepHistoryCount);
        setHistory(updatedHistory);

        // Update queue
        setQueue((prevQueue) => {
          const idx = prevQueue.findIndex((t) => t.id === track.id);
          if (idx === -1) {
            const nextQueue = [...prevQueue, updatedTrack];
            StorageService.saveQueue(nextQueue);
            setCurrentIndex(nextQueue.length - 1);
            return nextQueue;
          } else {
            setCurrentIndex(idx);
            return prevQueue;
          }
        });

        if (audioRef.current) {
          audioRef.current.src = resolved.streamUrl;
          audioRef.current.volume = volume;
          audioRef.current.loop = loopMode === 'one';
          await audioRef.current.play();
          setPlaybackState('playing');
          setStatusMessage('Đang phát');
        }
      } catch (err: any) {
        if (requestId !== playRequestIdRef.current) return;
        console.error('[PWA Audio] Playback error:', err);
        setErrorMessage(err.message || 'Không thể phát bài hát này.');
        setPlaybackState('error');
        setStatusMessage('Lỗi phát');
      }
    },
    [volume, loopMode, settings.keepHistoryCount]
  );

  const playUrl = useCallback(
    async (url: string) => {
      const resolver = YouTubeResolver.getInstance();
      const videoId = resolver.extractVideoId(url);
      if (!videoId) {
        setErrorMessage('Không nhận diện được liên kết YouTube hợp lệ.');
        return;
      }
      await playTrack({
        id: videoId,
        title: 'Đang tải...',
        author: 'YouTube',
        durationSeconds: 0,
        thumbnailUrl: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
      });
    },
    [playTrack]
  );

  const togglePlayPause = useCallback(() => {
    if (!audioRef.current || !currentTrack) return;
    if (audioRef.current.paused) {
      audioRef.current.play();
    } else {
      audioRef.current.pause();
    }
  }, [currentTrack]);

  const playNext = useCallback(() => {
    if (queue.length === 0) return;
    let nextIdx = currentIndex + 1;
    if (nextIdx >= queue.length) {
      if (loopMode === 'all') nextIdx = 0;
      else return;
    }
    setCurrentIndex(nextIdx);
    playTrack(queue[nextIdx]);
  }, [queue, currentIndex, loopMode, playTrack]);

  const playPrevious = useCallback(() => {
    if (queue.length === 0) return;
    if (audioRef.current && audioRef.current.currentTime > 3) {
      audioRef.current.currentTime = 0;
      return;
    }
    let prevIdx = currentIndex - 1;
    if (prevIdx < 0) {
      if (loopMode === 'all') prevIdx = queue.length - 1;
      else prevIdx = 0;
    }
    setCurrentIndex(prevIdx);
    playTrack(queue[prevIdx]);
  }, [queue, currentIndex, loopMode, playTrack]);

  const seekTo = useCallback((seconds: number) => {
    if (audioRef.current) {
      audioRef.current.currentTime = seconds;
      setCurrentTime(seconds);
    }
  }, []);

  const setVolume = useCallback((vol: number) => {
    const clamped = Math.max(0, Math.min(1, vol));
    setVolumeState(clamped);
    if (audioRef.current) {
      audioRef.current.volume = clamped;
    }
  }, []);

  const toggleLoopMode = useCallback(() => {
    setLoopMode((prev) => {
      let next: LoopMode = 'off';
      if (prev === 'off') next = 'all';
      else if (prev === 'all') next = 'one';
      if (audioRef.current) {
        audioRef.current.loop = next === 'one';
      }
      return next;
    });
  }, []);

  const toggleShuffle = useCallback(() => {
    setIsShuffled((prev) => {
      const next = !prev;
      if (next) {
        const cur = queue[currentIndex];
        const rest = queue.filter((_, i) => i !== currentIndex);
        for (let i = rest.length - 1; i > 0; i--) {
          const j = Math.floor(Math.random() * (i + 1));
          [rest[i], rest[j]] = [rest[j], rest[i]];
        }
        const shuffled = cur ? [cur, ...rest] : rest;
        setQueue(shuffled);
        setCurrentIndex(cur ? 0 : -1);
      } else {
        setQueue(unshuffledQueue);
        if (currentTrack) {
          const idx = unshuffledQueue.findIndex((t) => t.id === currentTrack.id);
          setCurrentIndex(idx >= 0 ? idx : 0);
        }
      }
      return next;
    });
  }, [queue, currentIndex, unshuffledQueue, currentTrack]);

  const addToQueue = useCallback((track: PlaylistItem) => {
    setQueue((prev) => {
      const next = [...prev, track];
      StorageService.saveQueue(next);
      return next;
    });
    setUnshuffledQueue((prev) => [...prev, track]);
  }, []);

  const removeFromQueue = useCallback((index: number) => {
    setQueue((prev) => {
      const next = prev.filter((_, i) => i !== index);
      StorageService.saveQueue(next);
      return next;
    });
  }, []);

  const clearQueue = useCallback(() => {
    setQueue([]);
    setUnshuffledQueue([]);
    setCurrentIndex(-1);
    StorageService.saveQueue([]);
  }, []);

  const playQueueIndex = useCallback(
    (index: number) => {
      if (index >= 0 && index < queue.length) {
        setCurrentIndex(index);
        playTrack(queue[index]);
      }
    },
    [queue, playTrack]
  );

  const isFavorite = useCallback(
    (id: string) => favorites.some((f) => f.id === id),
    [favorites]
  );

  const toggleFavorite = useCallback((track: PlaylistItem) => {
    setFavorites((prev) => {
      const exists = prev.some((f) => f.id === track.id);
      const next = exists ? prev.filter((f) => f.id !== track.id) : [track, ...prev];
      StorageService.saveFavorites(next);
      return next;
    });
  }, []);

  // Sleep Timer
  const startSleepTimer = useCallback((minutes: number) => {
    if (sleepTimerRef.current) clearInterval(sleepTimerRef.current);
    let remainingSec = minutes * 60;
    setSleepTimerRemaining(remainingSec);

    sleepTimerRef.current = window.setInterval(() => {
      remainingSec -= 1;
      if (remainingSec <= 0) {
        if (sleepTimerRef.current) clearInterval(sleepTimerRef.current);
        sleepTimerRef.current = null;
        setSleepTimerRemaining(null);
        if (audioRef.current) {
          audioRef.current.pause();
        }
        setPlaybackState('paused');
        setStatusMessage('Đã tắt theo hẹn giờ');
      } else {
        setSleepTimerRemaining(remainingSec);
      }
    }, 1000);
  }, []);

  const cancelSleepTimer = useCallback(() => {
    if (sleepTimerRef.current) {
      clearInterval(sleepTimerRef.current);
      sleepTimerRef.current = null;
    }
    setSleepTimerRemaining(null);
  }, []);

  const clearHistory = useCallback(() => {
    StorageService.clearHistory();
    setHistory([]);
  }, []);

  const updateSettings = useCallback((newSettings: Partial<AppSettings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...newSettings };
      StorageService.saveSettings(next);
      return next;
    });
  }, []);

  const isOffline = useCallback(
    (id: string) => offlineTracks.some((t) => t.id === id),
    [offlineTracks]
  );

  const downloadTrackOffline = useCallback(
    async (track: PlaylistItem): Promise<boolean> => {
      try {
        let streamUrl = track.streamUrl;
        if (!streamUrl) {
          const resolved = await YouTubeResolver.getInstance().resolveAudioStream(track.id);
          streamUrl = resolved.streamUrl;
        }
        if (!streamUrl) return false;
        const success = await OfflineAudioService.getInstance().saveTrackOffline(track, streamUrl);
        if (success) {
          const updated = await OfflineAudioService.getInstance().getAllOfflineTracks();
          setOfflineTracks(updated);
        }
        return success;
      } catch (err) {
        console.error('[Download offline error]:', err);
        return false;
      }
    },
    []
  );

  const deleteOfflineTrack = useCallback(
    async (id: string) => {
      await OfflineAudioService.getInstance().deleteTrack(id);
      const updated = await OfflineAudioService.getInstance().getAllOfflineTracks();
      setOfflineTracks(updated);
    },
    []
  );

  // Configure MediaSession action handlers (Lock Screen / Control Center)
  useEffect(() => {
    if (!('mediaSession' in navigator)) return;

    try {
      navigator.mediaSession.setActionHandler('play', () => {
        if (audioRef.current) audioRef.current.play();
      });
      navigator.mediaSession.setActionHandler('pause', () => {
        if (audioRef.current) audioRef.current.pause();
      });
      navigator.mediaSession.setActionHandler('previoustrack', () => {
        playPrevious();
      });
      navigator.mediaSession.setActionHandler('nexttrack', () => {
        playNext();
      });
      navigator.mediaSession.setActionHandler('seekto', (details) => {
        if (details.seekTime !== undefined && audioRef.current) {
          audioRef.current.currentTime = details.seekTime;
          setCurrentTime(details.seekTime);
        }
      });
      navigator.mediaSession.setActionHandler('seekbackward', (details) => {
        const offset = details.seekOffset || 10;
        if (audioRef.current) {
          audioRef.current.currentTime = Math.max(0, audioRef.current.currentTime - offset);
        }
      });
      navigator.mediaSession.setActionHandler('seekforward', (details) => {
        const offset = details.seekOffset || 10;
        if (audioRef.current) {
          audioRef.current.currentTime = Math.min(
            audioRef.current.duration || 0,
            audioRef.current.currentTime + offset
          );
        }
      });
    } catch (e) {
      console.warn('[MediaSession] Handler registration:', e);
    }
  }, [playNext, playPrevious]);

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
        errorMessage,
        currentPalette,
        queue,
        currentIndex,
        favorites,
        history,
        offlineTracks,
        sleepTimerRemaining,
        settings,

        playTrack,
        playUrl,
        togglePlayPause,
        playNext,
        playPrevious,
        seekTo,
        setVolume,
        toggleLoopMode,
        toggleShuffle,
        addToQueue,
        removeFromQueue,
        clearQueue,
        playQueueIndex,
        isFavorite,
        toggleFavorite,
        startSleepTimer,
        cancelSleepTimer,
        clearHistory,
        updateSettings,
        downloadTrackOffline,
        isOffline,
        deleteOfflineTrack,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
};

export const usePlayer = (): PlayerContextType => {
  const ctx = useContext(PlayerContext);
  if (!ctx) throw new Error('usePlayer must be used within PlayerProvider');
  return ctx;
};

