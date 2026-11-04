import React from 'react';
import { X, Trash2, Play } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onOpenSearch: () => void;
}

export const QueueModal: React.FC<Props> = ({ isOpen, onClose, onOpenSearch }) => {
  const { queue, currentIndex, playQueueIndex, removeFromQueue, clearQueue } = usePlayer();

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 className="modal-title">Danh sách chờ phát</h3>
            <span className="modal-subtitle">{queue.length} bài hát</span>
          </div>
          <div className="header-right">
            {queue.length > 0 && (
              <button className="text-btn danger" onClick={clearQueue}>
                Xóa tất cả
              </button>
            )}
            <button className="icon-btn" onClick={onClose}>
              <X size={20} />
            </button>
          </div>
        </div>

        <div className="modal-list">
          {queue.map((item, index) => {
            const isPlayingThis = index === currentIndex;
            return (
              <div key={`${item.id}-${index}`} className={`track-item ${isPlayingThis ? 'active' : ''}`}>
                <img src={item.thumbnailUrl} alt={item.title} className="track-thumb" />
                <div className="track-meta" onClick={() => playQueueIndex(index)}>
                  <div className="item-title">{item.title}</div>
                  <div className="item-author">{item.author}</div>
                </div>

                <div className="item-actions">
                  <button
                    className="action-icon-btn"
                    onClick={() => removeFromQueue(index)}
                    title="Xóa khỏi danh sách"
                  >
                    <Trash2 size={16} />
                  </button>
                </div>
              </div>
            );
          })}

          {queue.length === 0 && (
            <div className="empty-state">
              <p>Danh sách đang trống</p>
              <button
                className="primary-btn"
                onClick={() => {
                  onClose();
                  onOpenSearch();
                }}
              >
                Tìm bài hát thêm vào
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

