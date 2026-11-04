import React from 'react';
import {
  StyleSheet,
  View,
  TouchableOpacity,
  ActivityIndicator,
  Text,
} from 'react-native';
import { Ionicons, MaterialCommunityIcons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';

export const PlaybackControls: React.FC = () => {
  const {
    playbackState,
    togglePlayPause,
    playNext,
    playPrevious,
    loopMode,
    toggleLoopMode,
    isShuffled,
    toggleShuffle,
  } = usePlayer();

  const isPlaying = playbackState === 'playing';
  const isLoading = playbackState === 'loading';

  return (
    <View style={styles.container}>
      {/* Shuffle Button */}
      <TouchableOpacity
        style={[styles.smallBtn, isShuffled && styles.activeBtn]}
        onPress={toggleShuffle}
        activeOpacity={0.7}
      >
        <Ionicons
          name="shuffle"
          size={22}
          color={isShuffled ? '#38bdf8' : '#94a3b8'}
        />
      </TouchableOpacity>

      {/* Previous Button */}
      <TouchableOpacity
        style={styles.mediumBtn}
        onPress={playPrevious}
        activeOpacity={0.7}
      >
        <Ionicons name="play-skip-back" size={26} color="#f8fafc" />
      </TouchableOpacity>

      {/* Main Play / Pause Button */}
      <TouchableOpacity
        style={styles.playBtn}
        onPress={togglePlayPause}
        activeOpacity={0.8}
        disabled={isLoading}
      >
        {isLoading ? (
          <ActivityIndicator size="small" color="#0f172a" />
        ) : (
          <Ionicons
            name={isPlaying ? 'pause' : 'play'}
            size={32}
            color="#0f172a"
            style={isPlaying ? undefined : { marginLeft: 3 }}
          />
        )}
      </TouchableOpacity>

      {/* Next Button */}
      <TouchableOpacity
        style={styles.mediumBtn}
        onPress={playNext}
        activeOpacity={0.7}
      >
        <Ionicons name="play-skip-forward" size={26} color="#f8fafc" />
      </TouchableOpacity>

      {/* Loop Button */}
      <TouchableOpacity
        style={[styles.smallBtn, loopMode !== 'off' && styles.activeBtn]}
        onPress={toggleLoopMode}
        activeOpacity={0.7}
      >
        {loopMode === 'one' ? (
          <View style={styles.loopOneWrapper}>
            <MaterialCommunityIcons name="repeat-once" size={24} color="#38bdf8" />
          </View>
        ) : (
          <Ionicons
            name="repeat"
            size={22}
            color={loopMode === 'all' ? '#38bdf8' : '#94a3b8'}
          />
        )}
      </TouchableOpacity>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    width: '100%',
    paddingHorizontal: 8,
    marginVertical: 4,
  },
  smallBtn: {
    width: 44,
    height: 44,
    borderRadius: 22,
    justifyContent: 'center',
    alignItems: 'center',
  },
  activeBtn: {
    backgroundColor: 'rgba(56, 189, 248, 0.15)',
  },
  mediumBtn: {
    width: 50,
    height: 50,
    borderRadius: 25,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  playBtn: {
    width: 70,
    height: 70,
    borderRadius: 35,
    backgroundColor: '#ffffff',
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#ffffff',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.35,
    shadowRadius: 10,
    elevation: 8,
  },
  loopOneWrapper: {
    justifyContent: 'center',
    alignItems: 'center',
  },
});

