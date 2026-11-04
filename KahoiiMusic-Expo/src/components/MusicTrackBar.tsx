import React, { useState, useEffect } from 'react';
import { StyleSheet, View, Text } from 'react-native';
import Slider from '@react-native-community/slider';
import { usePlayer } from '../context/PlayerContext';

export const MusicTrackBar: React.FC = () => {
  const { currentTime, duration, seek } = usePlayer();
  const [isSeeking, setIsSeeking] = useState(false);
  const [seekValue, setSeekValue] = useState(0);

  useEffect(() => {
    if (!isSeeking) {
      setSeekValue(currentTime);
    }
  }, [currentTime, isSeeking]);

  const formatTime = (seconds: number) => {
    if (!seconds || isNaN(seconds) || seconds < 0) return '0:00';
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);

    const pad = (n: number) => (n < 10 ? `0${n}` : `${n}`);

    if (hrs > 0) {
      return `${hrs}:${pad(mins)}:${pad(secs)}`;
    }
    return `${mins}:${pad(secs)}`;
  };

  const handleSlidingStart = () => {
    setIsSeeking(true);
  };

  const handleValueChange = (val: number) => {
    setSeekValue(val);
  };

  const handleSlidingComplete = async (val: number) => {
    setIsSeeking(false);
    await seek(val);
  };

  const effectiveDuration = duration > 0 ? duration : 1;

  return (
    <View style={styles.container}>
      <Slider
        style={styles.slider}
        minimumValue={0}
        maximumValue={effectiveDuration}
        value={seekValue}
        minimumTrackTintColor="#38bdf8"
        maximumTrackTintColor="rgba(255, 255, 255, 0.2)"
        thumbTintColor="#ffffff"
        onSlidingStart={handleSlidingStart}
        onValueChange={handleValueChange}
        onSlidingComplete={handleSlidingComplete}
      />
      <View style={styles.timeRow}>
        <Text style={styles.timeText}>{formatTime(isSeeking ? seekValue : currentTime)}</Text>
        <Text style={styles.timeText}>{formatTime(duration)}</Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    width: '100%',
    paddingVertical: 4,
  },
  slider: {
    width: '100%',
    height: 36,
  },
  timeRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 12,
    marginTop: -8,
  },
  timeText: {
    color: '#94a3b8',
    fontSize: 12,
    fontVariant: ['tabular-nums'],
  },
});

