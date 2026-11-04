import React from 'react';
import { X, Smartphone, CheckCircle, Info } from 'lucide-react';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const AccountModal: React.FC<Props> = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content small" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">Thông tin ứng dụng</h3>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div className="account-body">
          <div className="pwa-info-box">
            <Smartphone size={32} color="#38bdf8" />
            <div className="pwa-info-text">
              <h4>KahoiiMusic Progressive Web App</h4>
              <p>Phiên bản 1.0.0 (Web & PWA)</p>
            </div>
          </div>

          <div className="feature-list">
            <div className="feature-item">
              <CheckCircle size={18} color="#10b981" />
              <span>Phát âm thanh trong nền khi tắt màn hình</span>
            </div>
            <div className="feature-item">
              <CheckCircle size={18} color="#10b981" />
              <span>Điều khiển media trên Màn hình khóa (MediaSession)</span>
            </div>
            <div className="feature-item">
              <CheckCircle size={18} color="#10b981" />
              <span>Cài đặt trực tiếp lên Màn hình chính không cần App Store</span>
            </div>
            <div className="feature-item">
              <CheckCircle size={18} color="#10b981" />
              <span>Hỗ trợ ngoại tuyến với Service Worker</span>
            </div>
          </div>

          <div className="tip-box">
            <Info size={16} />
            <small>
              Mẹo cho iPhone: Bấm nút Chia sẻ trong Safari và chọn "Thêm vào Màn hình chính" để nghe nhạc nền khi khóa máy.
            </small>
          </div>
        </div>
      </div>
    </div>
  );
};

