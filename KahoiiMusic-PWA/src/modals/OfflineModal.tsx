import React, { useEffect, useState } from 'react';
import { X, Play, Trash2, HardDrive, WifiOff } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';
import { OfflineAudioService } from '../services/OfflineAudioService';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const OfflineModal: React.FC<Props> = ({ isOpen, onClose }) => {
  const { offlineTracks, playTrack, deleteOfflineTrack } = usePlayer();
  const [storageInfo, setStorageInfo] = useState<{ usedMB: number; quotaMB: number }>({
    usedMB: 0,
    quotaMB: 0,
  });

  useEffect(() => {
    if (isOpen) {
      OfflineAudioService.getInstance()
        .getStorageEstimate()
        .then(setStorageInfo);
    }
  }, [isOpen, offlineTracks]);

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <WifiOff size={20} color="#38bdf8" />
              <h3 className="modal-title">Nhạc Ngoại Tuyến (Offline)</h3>
            </div>
            <span className="modal-subtitle">
              {offlineTracks.length} bài hát đã lưu trong IndexedDB máy bạn
            </span>
          </div>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        {storageInfo.quotaMB > 0 && (
          <div
            style={{
              padding: '10px 16px',
              margin: '8px 16px 0',
              borderRadius: '12px',
              backgroundColor: 'rgba(255, 255, 255, 0.05)',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              fontSize: '12px',
              color: '#94a3b8',
            }}
          >
            <HardDrive size={16} color="#38bdf8" />
            <span>
              Bộ nhớ trình duyệt: <strong>{storageInfo.usedMB} MB</strong> đã dùng /{' '}
              {storageInfo.quotaMB > 1024
                ? `${(storageInfo.quotaMB / 1024).toFixed(1)} GB`
                : `${storageInfo.quotaMB} MB`}{' '}
              khả dụng
            </span>
          </div>
        )}

        <div className="modal-list">
          {offlineTracks.map((item) => (
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
                  onClick={() => {
                    playTrack(item);
                    onClose();
                  }}
                  title="Phát"
                >
                  <Play size={18} color="#38bdf8" />
                </button>
                <button
                  className="action-icon-btn"
                  onClick={() => deleteOfflineTrack(item.id)}
                  title="Xóa bản tải"
                >
                  <Trash2 size={18} color="#f43f5e" />
                </button>
              </div>
            </div>
          ))}

          {offlineTracks.length === 0 && (
            <div className="empty-state">
              <p>Chưa có bài hát ngoại tuyến nào</p>
              <small>
                Bấm vào nút tải xuống (mũi tên tải về) cạnh nút tim khi nghe nhạc để lưu bài hát
                vào bộ nhớ máy phát không cần mạng.
              </small>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

