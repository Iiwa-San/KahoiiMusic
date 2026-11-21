import React from "react";
import { Music } from "lucide-react";
import type { Track } from "../types";
import { formatTime } from "../utils/lyricsParser";
import { UnifiedPlayOverlay } from "./UnifiedPlayOverlay";

const DEFAULT_TRACK_ART = "./logo_icon.png";

export interface TrackCardProps {
  track: Track;
  isCurrent: boolean;
  isPlaying: boolean;
  isLoading: boolean;
  onPlay: () => void;
  index?: number;
  className?: string;
  style?: React.CSSProperties;
}

export const TrackCard: React.FC<TrackCardProps> = ({
  track,
  isCurrent,
  isPlaying,
  isLoading,
  onPlay,
  index,
  className = "",
  style,
}) => {
  const isThisPlaying = isCurrent && isPlaying;
  const isThisLoading = isCurrent && isLoading;

  const staggerDelay = typeof index === "number" ? Math.min(index * 40, 360) : 0;
  const cardStyle: React.CSSProperties = {
    ...(staggerDelay > 0 ? { animationDelay: `${staggerDelay}ms` } : {}),
    ...style,
  };

  return (
    <div
      onClick={onPlay}
      style={cardStyle}
      className={`w-full flex flex-col cursor-pointer group item-slide-up ${className}`}
    >
      <div
        className={`relative w-full aspect-square rounded-xl sm:rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border ${
          isCurrent
            ? "theme-accent-border shadow-lg"
            : "border-white/5 bg-purple-900/30"
        }`}
      >
        <img
          src={track.thumbnail || `https://i.ytimg.com/vi/${track.id}/mqdefault.jpg`}
          alt={track.title}
          onError={(e) => {
            e.currentTarget.src = DEFAULT_TRACK_ART;
          }}
          className="w-full h-full object-cover"
        />
        <UnifiedPlayOverlay
          isCurrent={isCurrent}
          isPlaying={isThisPlaying}
          isLoading={isThisLoading}
          onToggle={onPlay}
        />
        {(track.duration > 0 || !!track.durationText) && (
          <span className="absolute bottom-1.5 right-1.5 px-1.5 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
            {track.isMusic !== false && (
              <Music className="w-2.5 h-2.5 text-purple-300 opacity-90" />
            )}
            <span>{track.durationText || formatTime(track.duration)}</span>
          </span>
        )}
      </div>
      <p
        className={`text-[12px] font-semibold truncate mt-2 transition-colors ${
          isCurrent
            ? "theme-accent-text font-bold"
            : "text-white/90 group-hover:theme-accent-text"
        }`}
      >
        {track.title}
      </p>
      <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
        {track.author || "Kahoii Music"}
      </p>
    </div>
  );
};
