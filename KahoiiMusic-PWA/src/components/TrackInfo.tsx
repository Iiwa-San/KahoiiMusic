import React, { useState } from 'react';
import { Heart, Download, CheckCircle2, Loader2 } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

export const TrackInfo: React.FC = () => {
  const {
    currentTrack,
    isFavorite,
    toggleFavorite,
    statusMessage,
    isOffline,
    downloadTrackOffline,
    deleteOfflineTrack,
  } = usePlayer();

  const [downloading, setDownloading] = useState(false);

  if (!currentTrack) {
    return (
      <div className="track-info empty">
        <h2 className="track-title">Chưa chọn bài hát</h2>
        <p className="track-author">Dán liên kết YouTube hoặc tìm kiếm bài hát ở trên</p>
      </div>
    );
  }

  const favorite = isFavorite(currentTrack.id);
  const offline = isOffline(currentTrack.id);

  const handleDownloadToggle = async () => {
    if (downloading) return;
    if (offline) {
      if (window.confirm('Xóa bài hát này khỏi bộ nhớ ngoại tuyến?')) {
        await deleteOfflineTrack(currentTrack.id);
      }
    } else {
      setDownloading(true);
      try {
        await downloadTrackOffline(currentTrack);
      } finally {
        setDownloading(false);
      }
    }
  };

  return (
    <div className="track-info">
      <div className="track-details">
        <h2 className="track-title" title={currentTrack.title}>
          {currentTrack.title}
        </h2>
        <p className="track-author">{currentTrack.author || 'YouTube'}</p>
        <span className="track-status">{statusMessage}</span>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
        <button
          className={`favorite-btn ${offline ? 'active' : ''}`}
          onClick={handleDownloadToggle}
          title={offline ? 'Đã lưu ngoại tuyến (Bấm để xóa)' : 'Tải về nghe offline'}
          disabled={downloading}
          style={{
            cursor: downloading ? 'wait' : 'pointer',
          }}
        >
          {downloading ? (
            <Loader2 size={22} className="spin-icon" color="#38bdf8" />
          ) : offline ? (
            <CheckCircle2 size={22} color="#10b981" />
          ) : (
            <Download size={22} color="#94a3b8" />
          )}
        </button>

        <button
          className={`favorite-btn ${favorite ? 'active' : ''}`}
          onClick={() => toggleFavorite(currentTrack)}
          title={favorite ? 'Bỏ thích' : 'Yêu thích'}
        >
          <Heart size={24} fill={favorite ? '#f43f5e' : 'none'} color={favorite ? '#f43f5e' : '#94a3b8'} />
        </button>
      </div>
    </div>
  );
};

