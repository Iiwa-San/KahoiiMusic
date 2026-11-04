import React from 'react';
import { ListMusic, Heart, History, Clock, ArrowDownToLine } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  onOpenQueue: () => void;
  onOpenFavorites: () => void;
  onOpenOffline: () => void;
  onOpenHistory: () => void;
  onOpenSleepTimer: () => void;
}

export const BottomNav: React.FC<Props> = ({
  onOpenQueue,
  onOpenFavorites,
  onOpenOffline,
  onOpenHistory,
  onOpenSleepTimer,
}) => {
  const { queue, favorites, offlineTracks } = usePlayer();

  return (
    <nav className="bottom-nav">
      <button className="nav-item" onClick={onOpenQueue} title="Hàng đợi">
        <ListMusic size={20} />
        <span className="nav-label">DS ({queue.length})</span>
      </button>

      <button className="nav-item" onClick={onOpenFavorites} title="Yêu thích">
        <Heart size={20} />
        <span className="nav-label">Thích ({favorites.length})</span>
      </button>

      <button className="nav-item" onClick={onOpenOffline} title="Đã tải ngoại tuyến">
        <ArrowDownToLine size={20} />
        <span className="nav-label">Tải về ({offlineTracks.length})</span>
      </button>

      <button className="nav-item" onClick={onOpenHistory} title="Lịch sử">
        <History size={20} />
        <span className="nav-label">Lịch sử</span>
      </button>

      <button className="nav-item" onClick={onOpenSleepTimer} title="Hẹn giờ">
        <Clock size={20} />
        <span className="nav-label">Hẹn giờ</span>
      </button>
    </nav>
  );
};

