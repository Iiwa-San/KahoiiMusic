import React from "react";
import { Mic2 } from "lucide-react";

const DEFAULT_TRACK_ART = "./logo_icon.png";

export interface ArtistItemData {
  id?: string;
  name: string;
  avatar?: string;
  thumbnail?: string;
  subscribers?: string;
  genre?: string;
}

export interface ArtistCardProps {
  artist: ArtistItemData;
  onClick: () => void;
  index?: number;
  className?: string;
  style?: React.CSSProperties;
}

export const ArtistCard: React.FC<ArtistCardProps> = ({
  artist,
  onClick,
  index,
  className = "",
  style,
}) => {
  const artistAvatar = artist.avatar || artist.thumbnail || DEFAULT_TRACK_ART;
  const artistLabel = artist.genre || artist.subscribers || "Nghệ sĩ";

  const staggerDelay = typeof index === "number" ? Math.min(index * 40, 360) : 0;
  const cardStyle: React.CSSProperties = {
    ...(staggerDelay > 0 ? { animationDelay: `${staggerDelay}ms` } : {}),
    ...style,
  };

  return (
    <div
      onClick={onClick}
      style={cardStyle}
      className={`w-full flex flex-col items-center cursor-pointer group item-slide-up ${className}`}
    >
      <div className="w-full aspect-square rounded-full overflow-hidden bg-purple-900/30 shadow-md border-2 border-white/10 group-hover:theme-accent-border group-hover:scale-[1.04] transition-all duration-200 relative">
        <img
          src={artistAvatar}
          alt={artist.name}
          loading="lazy"
          decoding="async"
          onError={(e) => {
            e.currentTarget.src = DEFAULT_TRACK_ART;
          }}
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-black/35 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
          <div className="w-8 h-8 sm:w-9 sm:h-9 rounded-full theme-btn-primary flex items-center justify-center shadow-lg text-white">
            <Mic2 className="w-4 h-4" />
          </div>
        </div>
      </div>
      <p className="text-[12px] font-semibold text-white/90 group-hover:theme-accent-text truncate mt-2 text-center w-full transition-colors">
        {artist.name}
      </p>
      <p className="text-[10px] text-purple-200/60 truncate text-center w-full mt-0.5">
        {artistLabel}
      </p>
    </div>
  );
};
