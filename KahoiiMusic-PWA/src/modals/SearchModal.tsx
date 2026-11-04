import React, { useState } from 'react';
import { X, Search, Play, Plus, Loader2 } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';
import { YouTubeResolver } from '../services/YouTubeResolver';
import { PlaylistItem } from '../types/music';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const SearchModal: React.FC<Props> = ({ isOpen, onClose }) => {
  const { playTrack, addToQueue } = usePlayer();
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<PlaylistItem[]>([]);

  if (!isOpen) return null;

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;
    setLoading(true);
    try {
      const items = await YouTubeResolver.getInstance().search(query);
      setResults(items);
    } finally {
      setLoading(false);
    }
  };

  const formatDuration = (secs: number) => {
    const m = Math.floor(secs / 60);
    const s = secs % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">Tìm kiếm trên YouTube</h3>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSearch} className="modal-search-form">
          <input
            type="text"
            className="modal-search-input"
            placeholder="Nhập tên bài hát hoặc ca sĩ..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            autoFocus
          />
          <button type="submit" className="submit-btn" disabled={loading}>
            {loading ? <Loader2 size={18} className="spin" /> : <Search size={18} />}
          </button>
        </form>

        <div className="modal-list">
          {results.map((item) => (
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
                <div className="item-author">
                  {item.author} • {formatDuration(item.durationSeconds)}
                </div>
              </div>

              <div className="item-actions">
                <button
                  className="action-icon-btn"
                  onClick={() => addToQueue(item)}
                  title="Thêm vào danh sách chờ"
                >
                  <Plus size={18} />
                </button>
                <button
                  className="action-icon-btn play"
                  onClick={() => {
                    playTrack(item);
                    onClose();
                  }}
                  title="Phát ngay"
                >
                  <Play size={18} fill="#38bdf8" color="#38bdf8" />
                </button>
              </div>
            </div>
          ))}

          {!loading && results.length === 0 && (
            <div className="empty-state">
              {query ? 'Không tìm thấy kết quả phù hợp' : 'Nhập từ khóa và bấm Tìm kiếm'}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

