import React, { useState } from 'react';
import {
  StyleSheet,
  View,
  Text,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { Ionicons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';
import { DynamicGradient } from '../components/DynamicGradient';
import { HeaderBar } from '../components/HeaderBar';
import { SearchPillBar } from '../components/SearchPillBar';
import { ArtworkView } from '../components/ArtworkView';
import { TrackInfoSection } from '../components/TrackInfoSection';
import { MusicTrackBar } from '../components/MusicTrackBar';
import { PlaybackControls } from '../components/PlaybackControls';
import { VolumeSlider } from '../components/VolumeSlider';
import { BottomActions } from '../components/BottomActions';

// Sheets
import { SearchResultsSheet } from '../sheets/SearchResultsSheet';
import { PlaylistSheet } from '../sheets/PlaylistSheet';
import { FavoritesSheet } from '../sheets/FavoritesSheet';
import { HistorySheet } from '../sheets/HistorySheet';
import { SleepTimerModal } from '../sheets/SleepTimerModal';
import { AccountSheet } from '../sheets/AccountSheet';
import { AddToCollectionSheet } from '../sheets/AddToCollectionSheet';
import { SavedTracksSheet } from '../sheets/SavedTracksSheet';
import { ErrorLogsSheet } from '../sheets/ErrorLogsSheet';

export const MainPlayerScreen: React.FC = () => {
  const { currentPalette, currentTrack, errorMessage } = usePlayer();

  const [searchVisible, setSearchVisible] = useState(false);
  const [queueVisible, setQueueVisible] = useState(false);
  const [favoritesVisible, setFavoritesVisible] = useState(false);
  const [historyVisible, setHistoryVisible] = useState(false);
  const [sleepTimerVisible, setSleepTimerVisible] = useState(false);
  const [accountVisible, setAccountVisible] = useState(false);
  const [collectionVisible, setCollectionVisible] = useState(false);
  const [savedTracksVisible, setSavedTracksVisible] = useState(false);
  const [errorLogsVisible, setErrorLogsVisible] = useState(false);

  return (
    <View style={styles.root}>
      <StatusBar style="light" />

      {/* Dynamic Animated Gradient Background */}
      <DynamicGradient palette={currentPalette} />

      <SafeAreaView style={styles.safeArea}>
        <View style={styles.mainContainer}>
            {/* Top Brand & Status Header */}
            <HeaderBar
              onOpenAccount={() => setAccountVisible(true)}
              onOpenErrorLogs={() => setErrorLogsVisible(true)}
            />

            {/* Search Pill Bar & Paste/Timer triggers */}
            <View style={styles.searchBarWrapper}>
              <SearchPillBar
                onOpenSearch={() => setSearchVisible(true)}
                onOpenSleepTimer={() => setSleepTimerVisible(true)}
              />
            </View>

            {/* Center Content: Artwork, Track Info, Controls */}
            <View style={styles.centerSection}>
              {/* Album Artwork with Swipe Next/Prev Gesture */}
              <ArtworkView />

              {/* Track Info (Title, Author, Add to Collection, Offline Download, Favorite) */}
              <TrackInfoSection
                onOpenAddToCollection={() => setCollectionVisible(true)}
              />

              {/* Scrubber Progress Bar */}
              <MusicTrackBar />

              {/* Playback Controls (Shuffle, Prev, Play/Pause Hero, Next, Loop) */}
              <PlaybackControls />

              {/* Volume Slider */}
              <VolumeSlider />
            </View>

            {/* Bottom Navigation Actions (5 items: Queue, Saved/Offline, History, Favorites, Share) */}
            <View style={styles.bottomContainer}>
              <BottomActions
                onOpenQueue={() => setQueueVisible(true)}
                onOpenSaved={() => setSavedTracksVisible(true)}
                onOpenHistory={() => setHistoryVisible(true)}
                onOpenFavorites={() => setFavoritesVisible(true)}
              />
            </View>
          </View>

          {/* Floating Error Toast Notification */}
          {errorMessage && (
            <View style={styles.errorToast}>
              <Ionicons name="alert-circle" size={20} color="#f87171" style={{ marginRight: 8 }} />
              <Text style={styles.errorToastText} numberOfLines={2}>
                {errorMessage}
              </Text>
            </View>
          )}
      </SafeAreaView>

      {/* Modals & Sheets - Lazy Mounted to liberate RAM when closed */}
      {searchVisible && (
        <SearchResultsSheet
          visible={searchVisible}
          onClose={() => setSearchVisible(false)}
        />
      )}

      {queueVisible && (
        <PlaylistSheet
          visible={queueVisible}
          onClose={() => setQueueVisible(false)}
          onOpenSearch={() => setSearchVisible(true)}
        />
      )}

      {favoritesVisible && (
        <FavoritesSheet
          visible={favoritesVisible}
          onClose={() => setFavoritesVisible(false)}
        />
      )}

      {historyVisible && (
        <HistorySheet
          visible={historyVisible}
          onClose={() => setHistoryVisible(false)}
        />
      )}

      {sleepTimerVisible && (
        <SleepTimerModal
          visible={sleepTimerVisible}
          onClose={() => setSleepTimerVisible(false)}
        />
      )}

      {accountVisible && (
        <AccountSheet
          visible={accountVisible}
          onClose={() => setAccountVisible(false)}
        />
      )}

      {collectionVisible && (
        <AddToCollectionSheet
          visible={collectionVisible}
          track={currentTrack}
          onClose={() => setCollectionVisible(false)}
        />
      )}

      {savedTracksVisible && (
        <SavedTracksSheet
          visible={savedTracksVisible}
          onClose={() => setSavedTracksVisible(false)}
        />
      )}

      {errorLogsVisible && (
        <ErrorLogsSheet
          visible={errorLogsVisible}
          onClose={() => setErrorLogsVisible(false)}
        />
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: '#0a0a14',
  },
  safeArea: {
    flex: 1,
  },
  mainContainer: {
    flex: 1,
    paddingHorizontal: 16,
    justifyContent: 'space-between',
  },
    searchBarWrapper: {
      paddingVertical: 4,
    },
    centerSection: {
      flex: 1,
      justifyContent: 'space-evenly',
      paddingVertical: 2,
    },
    bottomContainer: {
      paddingBottom: 4,
    },
    errorToast: {
      position: 'absolute',
      bottom: 84,
      left: 20,
      right: 20,
      backgroundColor: 'rgba(127, 29, 29, 0.94)',
      borderRadius: 14,
      paddingHorizontal: 16,
      paddingVertical: 12,
      flexDirection: 'row',
      alignItems: 'center',
      borderWidth: 1,
      borderColor: 'rgba(239, 68, 68, 0.4)',
      shadowColor: '#000',
      shadowOffset: { width: 0, height: 4 },
      shadowOpacity: 0.3,
      shadowRadius: 8,
      elevation: 8,
    },
    errorToastText: {
      color: '#fef2f2',
      fontSize: 13,
      fontWeight: '500',
      flex: 1,
    },
  });
