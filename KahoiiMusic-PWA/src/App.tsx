import React, { useState } from 'react';
import { usePlayer } from './context/PlayerContext';
import { Header } from './components/Header';
import { SearchPill } from './components/SearchPill';
import { Artwork } from './components/Artwork';
import { TrackInfo } from './components/TrackInfo';
import { ProgressBar } from './components/ProgressBar';
import { Controls } from './components/Controls';
import { VolumeSlider } from './components/VolumeSlider';
import { BottomNav } from './components/BottomNav';

// Modals
import { SearchModal } from './modals/SearchModal';
import { QueueModal } from './modals/QueueModal';
import { FavoritesModal } from './modals/FavoritesModal';
import { HistoryModal } from './modals/HistoryModal';
import { SleepTimerModal } from './modals/SleepTimerModal';
import { AccountModal } from './modals/AccountModal';
import { OfflineModal } from './modals/OfflineModal';

export const App: React.FC = () => {
  const { currentPalette, errorMessage } = usePlayer();

  const [searchOpen, setSearchOpen] = useState(false);
  const [queueOpen, setQueueOpen] = useState(false);
  const [favoritesOpen, setFavoritesOpen] = useState(false);
  const [offlineOpen, setOfflineOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [sleepTimerOpen, setSleepTimerOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);

  return (
    <div
      className="app-container"
      style={{
        background: `radial-gradient(circle at 50% 20%, ${currentPalette.secondary} 0%, ${currentPalette.background} 85%)`,
      }}
    >
      <div className="glass-ambient-tint" />

      <div className="app-shell">
        <Header onOpenAccount={() => setAccountOpen(true)} />

        <SearchPill
          onOpenSearch={() => setSearchOpen(true)}
          onOpenSleepTimer={() => setSleepTimerOpen(true)}
        />

        <main className="player-stage">
          <Artwork />
          <TrackInfo />
          <ProgressBar />
          <Controls />
          <VolumeSlider />
        </main>

        <BottomNav
          onOpenQueue={() => setQueueOpen(true)}
          onOpenFavorites={() => setFavoritesOpen(true)}
          onOpenOffline={() => setOfflineOpen(true)}
          onOpenHistory={() => setHistoryOpen(true)}
          onOpenSleepTimer={() => setSleepTimerOpen(true)}
        />

        {errorMessage && <div className="error-toast">{errorMessage}</div>}
      </div>

      {/* Lazy Modals */}
      <SearchModal isOpen={searchOpen} onClose={() => setSearchOpen(false)} />
      <QueueModal
        isOpen={queueOpen}
        onClose={() => setQueueOpen(false)}
        onOpenSearch={() => setSearchOpen(true)}
      />
      <FavoritesModal isOpen={favoritesOpen} onClose={() => setFavoritesOpen(false)} />
      <OfflineModal isOpen={offlineOpen} onClose={() => setOfflineOpen(false)} />
      <HistoryModal isOpen={historyOpen} onClose={() => setHistoryOpen(false)} />
      <SleepTimerModal isOpen={sleepTimerOpen} onClose={() => setSleepTimerOpen(false)} />
      <AccountModal isOpen={accountOpen} onClose={() => setAccountOpen(false)} />
    </div>
  );
};

