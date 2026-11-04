import React from 'react';
import { X, Clock, StopCircle } from 'lucide-react';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export const SleepTimerModal: React.FC<Props> = ({ isOpen, onClose }) => {
  const { sleepTimerRemaining, startSleepTimer, cancelSleepTimer } = usePlayer();

  if (!isOpen) return null;

  const options = [15, 30, 45, 60, 90];

  const formatTimer = (sec: number) => {
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-content small" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">Hẹn giờ tắt nhạc</h3>
          <button className="icon-btn" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        {sleepTimerRemaining !== null && (
          <div className="active-timer-card">
            <Clock size={28} className="timer-icon spin-slow" />
            <div className="timer-info">
              <span className="timer-label">Thời gian còn lại</span>
              <span className="timer-countdown">{formatTimer(sleepTimerRemaining)}</span>
            </div>
            <button className="cancel-timer-btn" onClick={cancelSleepTimer} title="Hủy hẹn giờ">
              <StopCircle size={22} />
            </button>
          </div>
        )}

        <div className="timer-options-grid">
          {options.map((mins) => (
            <button
              key={mins}
              className="timer-option-btn"
              onClick={() => {
                startSleepTimer(mins);
                onClose();
              }}
            >
              {mins} phút
            </button>
          ))}
        </div>
      </div>
    </div>
  );
};

