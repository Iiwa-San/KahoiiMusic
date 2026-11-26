import React from "react";
import { ListMusic, Play, Loader2 } from "lucide-react";
import type { SearchPlaylistItem } from "../types";

const DEFAULT_TRACK_ART = "./logo_icon.png";

export interface PlaylistCardProps {
  playlist: SearchPlaylistItem;
  onClick: () => void;
  isLoading?: boolean;
  index?: number;
  className?: string;
  style?: React.CSSProperties;
}

export const PlaylistCard: React.FC<PlaylistCardProps> = ({
  playlist,
  onClick,
  isLoading = false,
  index,
  className = "",
  style,
}) => {
  const staggerDelay = typeof index === "number" ? Math.min(index * 45, 400) : 0;
  const cardStyle: React.CSSProperties = {
    ...(staggerDelay > 0 ? { animationDelay: `${staggerDelay}ms` } : {}),
    ...style,
  };

  return (
    <div
      onClick={onClick}
      style={cardStyle}
      className={`w-full flex flex-col cursor-pointer group item-slide-up ${className}`}
    >
      <div className="relative w-full aspect-square rounded-xl sm:rounded-[18px] xl:rounded-[22px] overflow-hidden shadow-md group-hover:scale-[1.03] transition-transform duration-200 border border-white/5 bg-purple-900/30">
        <img
          src={playlist.thumbnail || DEFAULT_TRACK_ART}
          alt={playlist.title}
          loading="lazy"
          decoding="async"
          onError={(e) => {
            e.currentTarget.src = DEFAULT_TRACK_ART;
          }}
          className="w-full h-full object-cover"
        />
        <div
          className={`absolute inset-0 bg-black/40 flex items-center justify-center transition-opacity z-10 ${
            isLoading ? "opacity-100" : "opacity-0 group-hover:opacity-100"
          }`}
        >
          <div className="w-10 h-10 rounded-full theme-btn-primary flex items-center justify-center shadow-xl text-white hover:scale-110 active:scale-95 transition-all">
            {isLoading ? (
              <Loader2 className="w-5 h-5 animate-spin text-white" />
            ) : (
              <Play className="w-5 h-5 fill-current translate-x-0.5" />
            )}
          </div>
        </div>
        <span className="absolute bottom-1.5 right-1.5 px-2 py-0.5 rounded-md bg-black/75 text-[10px] font-mono text-white/90 flex items-center gap-1 backdrop-blur-sm shadow z-10">
          <ListMusic className="w-3 h-3 text-purple-300" />
          <span>Playlist</span>
        </span>
      </div>
      <p className="text-[12px] font-semibold truncate mt-2 text-white/90 group-hover:theme-accent-text transition-colors">
        {playlist.title}
      </p>
      <p className="text-[10px] text-purple-200/60 truncate mt-0.5">
        {playlist.trackCountText || playlist.author || "Kahoii Music"}
      </p>
    </div>
  );
};
