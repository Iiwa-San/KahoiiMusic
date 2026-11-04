import React from 'react';
import { X, Play, Heart } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const FavoritesModal: React.FC<Props> = ({ isOpen, onClose }) => {
  const { favorites, playTrack, toggleFavorite } = usePlayer();

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <h3 className="modal-title">Bài hát Yêu thích</h3>
            <span className="modal-subtitle">{favorites.length} bài hát</span>
          </div>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div className="modal-list">
          {favorites.map((item) => (
            <div key={item.id} className="track-item">
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
                  className="action-icon-btn"
                  onClick={() => toggleFavorite(item)}
                  title="Bỏ thích"
                >
                  <Heart size={18} fill="#f43f5e" color="#f43f5e" />
                </button>
              </div>
            </div>
          ))}

          {favorites.length === 0 && (
            <div className="empty-state">
              <p>Chưa có bài hát yêu thích nào</p>
              <small>Nhấn biểu tượng trái tim khi đang nghe bài hát để lưu lại.</small>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

