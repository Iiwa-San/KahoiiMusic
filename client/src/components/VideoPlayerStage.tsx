import React, { useState, useRef, useEffect } from "react";
import {
  Play,
  Pause,
  SkipBack,
  SkipForward,
  Repeat,
  Repeat1,
  Volume2,
  VolumeX,
  Maximize,
  Minimize,
  Gauge,
  Settings2,
  Heart,
  Clock,
  Loader2,
  Mic2,
  Film,
  Music,
  X,
  ListPlus,
  PictureInPicture2,
} from "lucide-react";
import type { Track, VideoQualityOption } from "../types";
import { formatTime } from "../utils/lyricsParser";

const SPEED_OPTIONS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];

interface VideoPlayerStageProps {
  currentTrack: Track | null;
  isPlaying: boolean;
  isLoading: boolean;
  currentTime: number;
  duration: number;
  volume: number;
  isMuted: boolean;
  repeatMode: "off" | "all" | "one";
  activePlayerType: "video" | "audio" | "youtube";
  availableQualities: VideoQualityOption[];
  selectedQuality: string;
  playbackRate: number;
  isFavorite: boolean;
  lyricsText: string | null;
  sleepTimerMinutes: number | null;
  videoRef: React.RefObject<HTMLVideoElement | null>;
  onTogglePlayPause: () => void;
  onNext: () => void;
  onPrev: () => void;
  onSeek: (newTime: number) => void;
  onVolumeChange: (val: number) => void;
  onToggleMute: () => void;
  onCycleRepeat: () => void;
  onSelectQuality: (q: VideoQualityOption) => void;
  onSelectSpeed: (speed: number) => void;
  onToggleFavorite: () => void;
  onAddToPlaylist?: () => void;
  onSelectSleepTimer: (mins: number | null) => void;
  onVideoTimeUpdate: () => void;
  onVideoLoadedMetadata: () => void;
  onVideoEnded: () => void;
  onVideoPlay: () => void;
  onVideoPause: () => void;
  onVideoError?: () => void;
}

export const VideoPlayerStage: React.FC<VideoPlayerStageProps> = ({
  currentTrack,
  isPlaying,
  isLoading,
  currentTime,
  duration,
  volume,
  isMuted,
  repeatMode,
  activePlayerType,
  availableQualities,
  selectedQuality,
  playbackRate,
  isFavorite,
  lyricsText,
  sleepTimerMinutes,
  videoRef,
  onTogglePlayPause,
  onNext,
  onPrev,
  onSeek,
  onVolumeChange,
  onToggleMute,
  onCycleRepeat,
  onSelectQuality,
  onSelectSpeed,
  onToggleFavorite,
  onAddToPlaylist,
  onSelectSleepTimer,
  onVideoTimeUpdate,
  onVideoLoadedMetadata,
  onVideoEnded,
  onVideoPlay,
  onVideoPause,
  onVideoError,
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showQualityMenu, setShowQualityMenu] = useState(false);
  const [showSpeedMenu, setShowSpeedMenu] = useState(false);
  const [showSleepMenu, setShowSleepMenu] = useState(false);
  const [showLyrics, setShowLyrics] = useState(false);
  const [isPiP, setIsPiP] = useState(false);
  const [dragTime, setDragTime] = useState<number | null>(null);
  const isDraggingRef = useRef(false);

  useEffect(() => {
    const onFsChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener("fullscreenchange", onFsChange);
    return () => document.removeEventListener("fullscreenchange", onFsChange);
  }, []);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const onEnter = () => setIsPiP(true);
    const onLeave = () => setIsPiP(false);
    video.addEventListener("enterpictureinpicture", onEnter);
    video.addEventListener("leavepictureinpicture", onLeave);
    return () => {
      video.removeEventListener("enterpictureinpicture", onEnter);
      video.removeEventListener("leavepictureinpicture", onLeave);
    };
  }, [videoRef]);

  const toggleFullscreen = async () => {
    try {
      if (!document.fullscreenElement && containerRef.current) {
        await containerRef.current.requestFullscreen();
      } else if (document.fullscreenElement) {
        await document.exitFullscreen();
      }
    } catch (err) {
      console.warn("Fullscreen error:", err);
    }
  };

  const togglePiP = async () => {
    try {
      const video = videoRef.current;
      if (!video || !document.pictureInPictureEnabled) return;
      if (document.pictureInPictureElement) {
        await document.exitPictureInPicture();
      } else {
        if (video.readyState === 0) return;
        await video.requestPictureInPicture();
      }
    } catch (err) {
      console.warn("Picture-in-Picture error:", err);
    }
  };

  const displayTime = dragTime !== null ? dragTime : currentTime;
  const progressPercent = duration > 0 ? Math.min(100, (displayTime / duration) * 100) : 0;

  return (
    <div
      id="thuongtube-video-container"
      ref={containerRef}
      className={`figma-panel flex flex-col overflow-hidden relative transition-all ${
        isFullscreen ? "w-screen h-screen rounded-none bg-black" : "w-full flex-1 min-h-[260px] md:min-h-[340px]"
      }`}
    >
      {/* Top Track Title Strip */}
      <div className="px-4 py-2.5 flex items-center justify-between gap-3 border-b border-white/10 bg-black/25 flex-shrink-0 z-20">
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-pink-500 to-purple-600 flex items-center justify-center flex-shrink-0 shadow">
            {activePlayerType === "audio" ? (
              <Music className="w-4 h-4 text-white" />
            ) : (
              <Film className="w-4 h-4 text-white" />
            )}
          </div>
          <div className="min-w-0 flex-1">
            <h2 className="text-xs md:text-sm font-bold text-white truncate">
              {currentTrack ? currentTrack.title : "Chưa chọn video / bài hát"}
            </h2>
            <p className="text-[11px] text-white/60 truncate">
              {currentTrack
                ? `${currentTrack.author || "YouTube"} • ${
                    activePlayerType === "video"
                      ? `Video (${selectedQuality})`
                      : activePlayerType === "youtube"
                      ? "YouTube Player"
                      : "Chế độ chỉ âm thanh"
                  }`
                : "Tìm kiếm hoặc dán link YouTube ở thanh trên để phát video"}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-1.5 flex-shrink-0">
          {currentTrack && (
            <>
              <button
                type="button"
                onClick={onAddToPlaylist}
                title="Thêm vào danh sách phát"
                className="p-1.5 rounded-lg text-white/60 hover:text-pink-400 hover:bg-white/10 transition-colors cursor-pointer"
              >
                <ListPlus className="w-4 h-4" />
              </button>
              <button
                type="button"
                onClick={onToggleFavorite}
                title={isFavorite ? "Bỏ yêu thích" : "Thêm vào yêu thích"}
                className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                  isFavorite ? "text-pink-500 bg-pink-500/15" : "text-white/60 hover:text-white hover:bg-white/10"
                }`}
              >
                <Heart className={`w-4 h-4 ${isFavorite ? "fill-current" : ""}`} />
              </button>
            </>
          )}
          <button
            type="button"
            onClick={() => setShowLyrics((v) => !v)}
            title="Xem lời bài hát"
            className={`p-1.5 rounded-lg transition-colors cursor-pointer flex items-center gap-1 text-xs ${
              showLyrics ? "text-pink-400 bg-pink-500/20" : "text-white/60 hover:text-white hover:bg-white/10"
            }`}
          >
            <Mic2 className="w-4 h-4" />
            <span className="hidden sm:inline text-[11px] font-medium">Lời</span>
          </button>
        </div>
      </div>

      {/* Main Video / Audio Viewport */}
      <div className="relative flex-1 bg-black/70 flex items-center justify-center overflow-hidden min-h-[180px]">
        {/* HTML5 Video Element (visible when activePlayerType === 'video') */}
        <video
          ref={videoRef}
          playsInline
          onClick={onTogglePlayPause}
          onTimeUpdate={onVideoTimeUpdate}
          onLoadedMetadata={onVideoLoadedMetadata}
          onEnded={onVideoEnded}
          onPlay={onVideoPlay}
          onPause={onVideoPause}
          onError={onVideoError}
          className={`w-full h-full object-contain cursor-pointer ${
            currentTrack && activePlayerType === "video" ? "block" : "hidden"
          }`}
        />

        {/* Visible YouTube Iframe Container (wrapped so YT.Player iframe swap keeps React layout intact) */}
        <div
          className={
            currentTrack && activePlayerType === "youtube"
              ? "w-full h-full"
              : "fixed -bottom-[9999px] -right-[9999px] w-px h-px opacity-0 pointer-events-none"
          }
        >
          <div id="kahoii-yt-iframe" className="w-full h-full" />
        </div>

        {/* Audio-Only or Idle Poster View */}
        {(!currentTrack || activePlayerType === "audio") && (
          <div className="flex flex-col items-center justify-center p-4 text-center gap-3 w-full h-full bg-gradient-to-b from-[#240a50]/60 to-[#120428]/90">
            {currentTrack ? (
              <>
                <div className="relative w-28 h-28 md:w-36 md:h-36 rounded-2xl overflow-hidden shadow-2xl border border-pink-500/40">
                  <img
                    src={currentTrack.thumbnail || `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg`}
                    alt={currentTrack.title}
                    className="w-full h-full object-cover"
                  />
                </div>
                <span className="px-3 py-1 rounded-full bg-pink-500/20 border border-pink-500/40 text-[11px] font-semibold text-pink-300">
                  Đang phát chế độ Chỉ âm thanh
                </span>
              </>
            ) : (
              <div className="flex flex-col items-center gap-2 text-white/40">
                <Film className="w-12 h-12 text-pink-500/50" />
                <p className="text-sm font-semibold text-white/70">ThuongTube Video & Music</p>
                <p className="text-xs max-w-xs">
                  Chọn video từ danh sách hoặc tìm kiếm ở thanh trên để xem ngay
                </p>
              </div>
            )}
          </div>
        )}

        {/* Loading Spinner Overlay */}
        {isLoading && (
          <div className="absolute inset-0 bg-black/55 backdrop-blur-xs flex items-center justify-center z-20 pointer-events-none">
            <div className="flex items-center gap-2 px-4 py-2 rounded-full bg-black/80 border border-pink-500/40 text-xs font-semibold">
              <Loader2 className="w-4 h-4 animate-spin text-pink-400" />
              <span>Đang tải luồng video...</span>
            </div>
          </div>
        )}

        {/* Lyrics Floating Overlay */}
        {showLyrics && (
          <div className="absolute inset-0 bg-black/85 backdrop-blur-md z-30 flex flex-col p-4 overflow-hidden">
            <div className="flex items-center justify-between pb-2 border-b border-white/10 mb-2">
              <span className="text-xs font-bold text-pink-400 flex items-center gap-1.5">
                <Mic2 className="w-4 h-4" /> Lời bài hát
              </span>
              <button
                type="button"
                onClick={() => setShowLyrics(false)}
                className="p-1 rounded-lg hover:bg-white/10 text-white/70 hover:text-white cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto text-center text-xs md:text-sm text-white/85 whitespace-pre-line leading-relaxed px-2">
              {lyricsText || "Chưa có lời cho video/bài hát này."}
            </div>
          </div>
        )}
      </div>

      {/* Bottom Video Controls Bar (Seek + Play/Pause + Speed + Quality + Fullscreen) */}
      <div className="px-3 md:px-4 py-2.5 bg-[#120526]/95 border-t border-white/10 flex flex-col gap-2 flex-shrink-0 z-20">
        {/* Progress Bar */}
        <div className="flex items-center gap-2.5">
          <span className="text-[11px] font-mono text-white/70 w-10 text-right">
            {formatTime(displayTime)}
          </span>
          <div className="relative flex-1 flex items-center">
            <input
              type="range"
              min={0}
              max={duration || 100}
              step={0.5}
              value={displayTime}
              onPointerDown={() => {
                isDraggingRef.current = true;
              }}
              onChange={(e) => {
                const val = parseFloat(e.target.value);
                if (isDraggingRef.current) {
                  setDragTime(val);
                } else {
                  onSeek(val);
                }
              }}
              onPointerUp={(e) => {
                if (isDraggingRef.current) {
                  isDraggingRef.current = false;
                  const val = parseFloat((e.target as HTMLInputElement).value);
                  setDragTime(null);
                  onSeek(val);
                }
              }}
              className="w-full h-1.5 rounded-full appearance-none cursor-pointer accent-pink-500 bg-white/15"
              style={{
                background: `linear-gradient(to right, #ec4899 ${progressPercent}%, rgba(255,255,255,0.18) ${progressPercent}%)`,
              }}
            />
          </div>
          <span className="text-[11px] font-mono text-white/70 w-10">
            {formatTime(duration)}
          </span>
        </div>

        {/* Control Buttons Row */}
        <div className="flex items-center justify-between gap-2 flex-wrap">
          {/* Left: Transport + Volume */}
          <div className="flex items-center gap-1.5 md:gap-2.5">
            <button
              type="button"
              onClick={onPrev}
              title="Bài trước"
              className="p-1.5 text-white/80 hover:text-pink-400 transition-colors cursor-pointer"
            >
              <SkipBack className="w-4 h-4 fill-current" />
            </button>

            <button
              type="button"
              onClick={onTogglePlayPause}
              disabled={isLoading}
              title={isPlaying ? "Tạm dừng" : "Phát"}
              className="w-8 h-8 md:w-9 md:h-9 rounded-full flex items-center justify-center text-white shadow-lg hover:scale-105 active:scale-95 transition-all cursor-pointer"
              style={{ background: "var(--theme-btn-gradient)" }}
            >
              {isLoading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : isPlaying ? (
                <Pause className="w-4 h-4 fill-current" />
              ) : (
                <Play className="w-4 h-4 fill-current translate-x-0.5" />
              )}
            </button>

            <button
              type="button"
              onClick={onNext}
              title="Tiếp theo"
              className="p-1.5 text-white/80 hover:text-pink-400 transition-colors cursor-pointer"
            >
              <SkipForward className="w-4 h-4 fill-current" />
            </button>

            <button
              type="button"
              onClick={onCycleRepeat}
              title={`Lặp: ${repeatMode === "off" ? "Tắt" : repeatMode === "one" ? "1 bài" : "Tất cả"}`}
              className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                repeatMode !== "off" ? "text-pink-400 bg-pink-500/15" : "text-white/60 hover:text-white"
              }`}
            >
              {repeatMode === "one" ? <Repeat1 className="w-4 h-4" /> : <Repeat className="w-4 h-4" />}
            </button>

            {/* Volume */}
            <div className="hidden sm:flex items-center gap-1.5 ml-1">
              <button
                type="button"
                onClick={onToggleMute}
                title={isMuted ? "Bật âm" : "Tắt âm"}
                className="p-1 text-white/70 hover:text-white cursor-pointer"
              >
                {isMuted || volume === 0 ? (
                  <VolumeX className="w-4 h-4 text-red-400" />
                ) : (
                  <Volume2 className="w-4 h-4" />
                )}
              </button>
              <input
                type="range"
                min={0}
                max={1}
                step={0.05}
                value={isMuted ? 0 : volume}
                onChange={(e) => onVolumeChange(parseFloat(e.target.value))}
                className="w-16 md:w-20 h-1 accent-pink-500 cursor-pointer"
              />
            </div>
          </div>

          {/* Right: Speed, Quality, Sleep Timer, Fullscreen */}
          <div className="flex items-center gap-1.5 relative">
            {/* Sleep Timer */}
            <div className="relative">
              <button
                type="button"
                onClick={() => {
                  setShowSleepMenu((v) => !v);
                  setShowSpeedMenu(false);
                  setShowQualityMenu(false);
                }}
                title="Hẹn giờ tắt"
                className={`px-2 py-1 rounded-lg text-[11px] font-semibold flex items-center gap-1 border transition-colors cursor-pointer ${
                  sleepTimerMinutes
                    ? "bg-pink-500/20 border-pink-500/50 text-pink-300"
                    : "bg-white/5 border-white/10 text-white/75 hover:bg-white/10"
                }`}
              >
                <Clock className="w-3.5 h-3.5" />
                {sleepTimerMinutes ? <span>{sleepTimerMinutes}m</span> : null}
              </button>
              {showSleepMenu && (
                <div className="absolute bottom-9 right-0 p-1.5 rounded-xl bg-[#130628]/95 border border-pink-500/40 shadow-2xl flex flex-col gap-0.5 min-w-[115px] z-40">
                  <span className="text-[10px] uppercase font-bold text-pink-400 px-2 py-1">Hẹn giờ</span>
                  {[15, 30, 45, 60].map((m) => (
                    <button
                      key={m}
                      type="button"
                      onClick={() => {
                        onSelectSleepTimer(m);
                        setShowSleepMenu(false);
                      }}
                      className={`text-left text-xs px-2.5 py-1.5 rounded-lg cursor-pointer ${
                        sleepTimerMinutes === m ? "bg-pink-500 text-white font-bold" : "hover:bg-white/10 text-white/80"
                      }`}
                    >
                      {m} phút
                    </button>
                  ))}
                  {sleepTimerMinutes && (
                    <button
                      type="button"
                      onClick={() => {
                        onSelectSleepTimer(null);
                        setShowSleepMenu(false);
                      }}
                      className="text-left text-xs px-2.5 py-1.5 rounded-lg text-red-400 hover:bg-red-500/20 cursor-pointer border-t border-white/10 mt-0.5"
                    >
                      Tắt hẹn giờ
                    </button>
                  )}
                </div>
              )}
            </div>

            {/* Playback Speed */}
            <div className="relative">
              <button
                type="button"
                onClick={() => {
                  setShowSpeedMenu((v) => !v);
                  setShowQualityMenu(false);
                  setShowSleepMenu(false);
                }}
                title="Tốc độ phát"
                className="px-2 py-1 rounded-lg bg-white/5 hover:bg-white/15 border border-white/10 text-[11px] font-semibold text-white/90 flex items-center gap-1 cursor-pointer"
              >
                <Gauge className="w-3.5 h-3.5 text-pink-400" />
                <span>{playbackRate}x</span>
              </button>
              {showSpeedMenu && (
                <div className="absolute bottom-9 right-0 p-1.5 rounded-xl bg-[#130628]/95 border border-pink-500/40 shadow-2xl flex flex-col gap-0.5 min-w-[95px] z-40">
                  <span className="text-[10px] uppercase font-bold text-pink-400 px-2 py-1">Tốc độ</span>
                  {SPEED_OPTIONS.map((sp) => (
                    <button
                      key={sp}
                      type="button"
                      onClick={() => {
                        onSelectSpeed(sp);
                        setShowSpeedMenu(false);
                      }}
                      className={`text-left text-xs px-2.5 py-1 rounded-lg cursor-pointer ${
                        playbackRate === sp ? "bg-pink-500 text-white font-bold" : "hover:bg-white/10 text-white/85"
                      }`}
                    >
                      {sp === 1 ? "1x (Chuẩn)" : `${sp}x`}
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Quality Selector */}
            <div className="relative">
              <button
                type="button"
                onClick={() => {
                  setShowQualityMenu((v) => !v);
                  setShowSpeedMenu(false);
                  setShowSleepMenu(false);
                }}
                title="Chất lượng video"
                className="px-2 py-1 rounded-lg bg-white/5 hover:bg-white/15 border border-white/10 text-[11px] font-semibold text-white/90 flex items-center gap-1 cursor-pointer"
              >
                <Settings2 className="w-3.5 h-3.5 text-purple-300" />
                <span>{selectedQuality || "Auto"}</span>
              </button>
              {showQualityMenu && (
                <div className="absolute bottom-9 right-0 p-1.5 rounded-xl bg-[#130628]/95 border border-pink-500/40 shadow-2xl flex flex-col gap-0.5 min-w-[130px] z-40">
                  <span className="text-[10px] uppercase font-bold text-pink-400 px-2 py-1">Chất lượng</span>
                  {availableQualities.length > 0 ? (
                    availableQualities.map((q) => (
                      <button
                        key={q.quality}
                        type="button"
                        onClick={() => {
                          onSelectQuality(q);
                          setShowQualityMenu(false);
                        }}
                        className={`text-left text-xs px-2.5 py-1.5 rounded-lg flex items-center justify-between cursor-pointer ${
                          selectedQuality === q.quality
                            ? "bg-pink-500 text-white font-bold"
                            : "hover:bg-white/10 text-white/85"
                        }`}
                      >
                        <span>{q.quality}</span>
                        {q.height >= 720 && (
                          <span className="text-[9px] px-1 rounded bg-white/20">HD</span>
                        )}
                      </button>
                    ))
                  ) : (
                    <div className="text-[11px] text-white/50 px-2 py-1.5">
                      {activePlayerType === "youtube" ? "Tự động (YouTube)" : "Chưa có luồng"}
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Picture-in-Picture (PiP) Button */}
            {activePlayerType === "video" && currentTrack && (
              <button
                type="button"
                onClick={togglePiP}
                title={isPiP ? "Tắt cửa sổ nổi (PiP)" : "Thu nhỏ nổi trên màn hình (PiP)"}
                className={`p-1.5 rounded-lg border transition-colors cursor-pointer ${
                  isPiP
                    ? "bg-pink-500/30 border-pink-500/60 text-pink-300"
                    : "bg-white/5 hover:bg-pink-500/30 border-white/10 text-white/90 hover:text-white"
                }`}
              >
                <PictureInPicture2 className="w-4 h-4" />
              </button>
            )}

            {/* Fullscreen Button */}
            <button
              type="button"
              onClick={toggleFullscreen}
              title={isFullscreen ? "Thu nhỏ" : "Phóng to toàn màn hình"}
              className="p-1.5 rounded-lg bg-white/5 hover:bg-pink-500/30 border border-white/10 text-white/90 hover:text-white transition-colors cursor-pointer"
            >
              {isFullscreen ? <Minimize className="w-4 h-4" /> : <Maximize className="w-4 h-4" />}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
