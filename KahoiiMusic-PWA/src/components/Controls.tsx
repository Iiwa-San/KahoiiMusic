import React from 'react';
import {
  Shuffle,
  SkipBack,
  Play,
  Pause,
  SkipForward,
  Repeat,
  Repeat1,
} from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

export const Controls: React.FC = () => {
  const {
    playbackState,
    togglePlayPause,
    playNext,
    playPrevious,
    loopMode,
    toggleLoopMode,
    isShuffled,
    toggleShuffle,
  } = usePlayer();

  const isPlaying = playbackState === 'playing';

  return (
    <div className="controls-row">
      {/* Shuffle button */}
      <button
        className={`control-btn ${isShuffled ? 'active' : ''}`}
        onClick={toggleShuffle}
        title="Trộn bài (Shuffle)"
      >
        <Shuffle size={20} />
      </button>

      {/* Previous button */}
      <button className="control-btn" onClick={playPrevious} title="Bài trước">
        <SkipBack size={26} />
      </button>

      {/* Hero Play / Pause button */}
      <button
        className="play-hero-btn"
        onClick={togglePlayPause}
        title={isPlaying ? 'Tạm dừng' : 'Phát'}
      >
        {isPlaying ? (
          <Pause size={32} fill="#ffffff" color="#ffffff" />
        ) : (
          <Play size={32} fill="#ffffff" color="#ffffff" style={{ marginLeft: 3 }} />
        )}
      </button>

      {/* Next button */}
      <button className="control-btn" onClick={playNext} title="Bài kế tiếp">
        <SkipForward size={26} />
      </button>

      {/* Loop button */}
      <button
        className={`control-btn ${loopMode !== 'off' ? 'active' : ''}`}
        onClick={toggleLoopMode}
        title={`Chế độ lặp: ${loopMode}`}
      >
        {loopMode === 'one' ? <Repeat1 size={20} /> : <Repeat size={20} />}
      </button>
    </div>
  );
};

