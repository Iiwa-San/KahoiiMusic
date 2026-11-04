import React from 'react';
import { Search, Clipboard, Clock } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  onOpenSearch: () => void;
  onOpenSleepTimer: () => void;
}

export const SearchPill: React.FC<Props> = ({ onOpenSearch, onOpenSleepTimer }) => {
  const { playUrl, sleepTimerRemaining } = usePlayer();

  const handlePaste = async () => {
    try {
      const text = await navigator.clipboard.readText();
      if (text && text.trim()) {
        playUrl(text.trim());
      } else {
        alert('Bộ nhớ tạm trống hoặc không chứa liên kết YouTube.');
      }
    } catch {
      const manual = prompt('Dán link video YouTube vào đây:');
      if (manual && manual.trim()) {
        playUrl(manual.trim());
      }
    }
  };

  const formatTimer = (sec: number) => {
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  return (
    <div className="search-pill-bar">
      <button className="search-trigger-btn" onClick={onOpenSearch}>
        <Search size={18} className="search-icon" />
        <span className="search-placeholder">Tìm kiếm bài hát, ca sĩ...</span>
      </button>

      <button className="quick-btn" onClick={handlePaste} title="Dán link YouTube">
        <Clipboard size={18} />
      </button>

      <button
        className={`quick-btn ${sleepTimerRemaining !== null ? 'active-timer' : ''}`}
        onClick={onOpenSleepTimer}
        title="Hẹn giờ tắt nhạc"
      >
        <Clock size={18} />
        {sleepTimerRemaining !== null && (
          <span className="timer-badge">{formatTimer(sleepTimerRemaining)}</span>
        )}
      </button>
    </div>
  );
};

