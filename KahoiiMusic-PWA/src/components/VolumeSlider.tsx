import React from 'react';
import { Volume2, VolumeX } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

export const VolumeSlider: React.FC = () => {
  const { volume, setVolume } = usePlayer();

  const handleToggleMute = () => {
    if (volume > 0) {
      setVolume(0);
    } else {
      setVolume(0.8);
    }
  };

  const percent = volume * 100;

  return (
    <div className="volume-slider-row">
      <button className="volume-btn" onClick={handleToggleMute} title="Tắt / Bật tiếng">
        {volume === 0 ? <VolumeX size={18} /> : <Volume2 size={18} />}
      </button>

      <input
        type="range"
        min={0}
        max={1}
        step={0.01}
        value={volume}
        onChange={(e) => setVolume(parseFloat(e.target.value))}
        className="volume-slider"
        style={{
          background: `linear-gradient(to right, #94a3b8 0%, #94a3b8 ${percent}%, rgba(255, 255, 255, 0.15) ${percent}%, rgba(255, 255, 255, 0.15) 100%)`,
        }}
      />
    </div>
  );
};

