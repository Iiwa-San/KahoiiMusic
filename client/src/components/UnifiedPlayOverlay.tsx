import React from "react";
import { Play, Pause, Loader2 } from "lucide-react";

export interface UnifiedPlayOverlayProps {
  isCurrent: boolean;
  isPlaying: boolean;
  isLoading?: boolean;
  onToggle: (e?: React.MouseEvent) => void;
  size?: "sm" | "md" | "lg";
  className?: string;
  buttonClassName?: string;
}

export const UnifiedPlayOverlay: React.FC<UnifiedPlayOverlayProps> = ({
  isCurrent,
  isPlaying,
  isLoading = false,
  onToggle,
  size = "md",
  className = "",
  buttonClassName = "",
}) => {
  const sizeClasses = {
    sm: "w-8 h-8",
    md: "w-10 h-10",
    lg: "w-12 h-12",
  }[size];

  const iconSizes = {
    sm: "w-4 h-4",
    md: "w-5 h-5",
    lg: "w-6 h-6",
  }[size];

  return (
    <div
      className={`absolute inset-0 bg-black/45 flex items-center justify-center transition-all duration-200 z-10 ${
        isCurrent ? "opacity-100 backdrop-blur-[1px]" : "opacity-0 group-hover:opacity-100"
      } ${className}`}
    >
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onToggle(e);
        }}
        className={`${sizeClasses} rounded-full theme-btn-primary flex items-center justify-center shadow-xl text-white hover:scale-110 active:scale-95 transition-all duration-150 cursor-pointer ${buttonClassName}`}
        title={isLoading ? "Đang tải..." : isCurrent && isPlaying ? "Tạm dừng" : "Phát"}
      >
        {isLoading ? (
          <Loader2 className={`${iconSizes} animate-spin text-white`} />
        ) : isCurrent && isPlaying ? (
          <Pause className={`${iconSizes} fill-current text-white`} />
        ) : (
          <Play className={`${iconSizes} fill-current text-white translate-x-0.5`} />
        )}
      </button>
    </div>
  );
};
