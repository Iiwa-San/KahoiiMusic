import React from 'react';
import { User, ShieldAlert } from 'lucide-react';

interface Props {
  onOpenAccount: () => void;
}

export const Header: React.FC<Props> = ({ onOpenAccount }) => {
  return (
    <header className="header">
      <div className="brand">
        <img src="/logo_icon.png" alt="Logo" className="brand-logo" />
        <span className="brand-title">KahoiiMusic</span>
        <span className="pwa-badge">PWA</span>
      </div>

      <div className="header-actions">
        <button
          className="icon-btn"
          onClick={onOpenAccount}
          title="Tài khoản & Đồng bộ"
          aria-label="Tài khoản"
        >
          <User size={20} />
        </button>
      </div>
    </header>
  );
};

