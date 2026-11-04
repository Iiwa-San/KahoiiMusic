import React from 'react';
import { X, Play, Trash2 } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const HistoryModal: React.FC<Props> = ({ isOpen, onClose }) => {
  const { history, playTrack, clearHistory } = usePlayer();

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 className="modal-title">Lịch sử nghe nhạc</h3>
            <span className="modal-subtitle">{history.length} bài hát</span>
          </div>
          <div className="header-right">
            {history.length > 0 && (
              <button className="text-btn danger" onClick={clearHistory}>
                Xóa lịch sử
              </button>
            )}
            <button className="icon-btn" onClick={onClose}>
              <X size={20} />
            </button>
          </div>
        </div>

        <div className="modal-list">
          {history.map((item, index) => (
            <div key={`${item.id}-${index}`} className="track-item">
              <img src={item.thumbnailUrl} alt={item.title} className="track-thumb" />
              <div
                className="track-meta"
                onClick={() => {
                  playTrack(item);
                  onClose();
                }}
              >
                <div className="item-title">{item.title}</div>
                <div className="item-author">{item.author}</div>
              </div>

              <div className="item-actions">
                <button
                  className="action-icon-btn play"
                  onClick={() => {
                    playTrack(item);
                    onClose();
                  }}
                  title="Phát lại"
                >
                  <Play size={18} fill="#38bdf8" color="#38bdf8" />
                </button>
              </div>
            </div>
          ))}

          {history.length === 0 && (
            <div className="empty-state">
              <p>Lịch sử đang trống</p>
              <small>Các bài hát bạn nghe sẽ được tự động lưu tại đây.</small>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

