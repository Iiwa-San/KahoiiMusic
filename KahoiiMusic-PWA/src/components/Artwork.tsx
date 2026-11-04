import React, { useRef, useState } from 'react';
import { usePlayer } from '../context/PlayerContext';

export const Artwork: React.FC = () => {
  const { currentTrack, playNext, playPrevious } = usePlayer();
  const [touchStart, setTouchStart] = useState<number | null>(null);

  const handleTouchStart = (e: React.TouchEvent) => {
    setTouchStart(e.targetTouches[0].clientX);
  };

  const handleTouchEnd = (e: React.TouchEvent) => {
    if (touchStart === null) return;
    const touchEnd = e.changedTouches[0].clientX;
    const diff = touchStart - touchEnd;
    if (diff > 50) {
      playNext();
    } else if (diff < -50) {
      playPrevious();
    }
    setTouchStart(null);
  };

  const thumbUrl =
    currentTrack?.thumbnailUrl ||
    (currentTrack?.id ? `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg` : null);

  return (
    <div
      className="artwork-container"
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
    >
      <div className="artwork-glow" />
      <div className="artwork-card">
        {thumbUrl ? (
          <img src={thumbUrl} alt={currentTrack?.title || 'Artwork'} className="artwork-image" />
        ) : (
          <div className="artwork-placeholder">
            <img src="/logo_icon.png" alt="Logo Placeholder" className="placeholder-logo" />
          </div>
        )}
      </div>
    </div>
  );
};

