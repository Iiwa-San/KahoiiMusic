import React from 'react';
import { usePlayer } from '../context/PlayerContext';

export const ProgressBar: React.FC = () => {
  const { currentTime, duration, seekTo } = usePlayer();

  const formatTime = (secs: number) => {
    if (isNaN(secs) || secs < 0) return '0:00';
    const m = Math.floor(secs / 60);
    const s = Math.floor(secs % 60);
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    seekTo(parseFloat(e.target.value));
  };

  const progressPercent = duration > 0 ? (currentTime / duration) * 100 : 0;

  return (
    <div className="progress-section">
      <div className="slider-wrapper">
        <input
          type="range"
          min={0}
          max={duration || 100}
          step={0.5}
          value={currentTime}
          onChange={handleChange}
          className="progress-slider"
          style={{
            background: `linear-gradient(to right, #38bdf8 0%, #38bdf8 ${progressPercent}%, rgba(255, 255, 255, 0.15) ${progressPercent}%, rgba(255, 255, 255, 0.15) 100%)`,
          }}
        />
      </div>

      <div className="time-row">
        <span className="time-text">{formatTime(currentTime)}</span>
        <span className="time-text">{formatTime(duration)}</span>
      </div>
    </div>
  );
};

