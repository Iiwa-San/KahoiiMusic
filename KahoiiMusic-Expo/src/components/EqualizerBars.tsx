import React from 'react';
import { StyleSheet, View } from 'react-native';

interface Props {
  isPlaying: boolean;
  color?: string;
}

// Ultra-lightweight zero-RAM equalizer indicator without JS thread loop overhead
export const EqualizerBars: React.FC<Props> = ({ isPlaying, color = '#38bdf8' }) => {
  return (
    <View style={styles.container}>
      <View style={[styles.bar, { height: isPlaying ? 9 : 4, backgroundColor: color }]} />
      <View style={[styles.bar, { height: isPlaying ? 18 : 4, backgroundColor: color }]} />
      <View style={[styles.bar, { height: isPlaying ? 12 : 4, backgroundColor: color }]} />
      <View style={[styles.bar, { height: isPlaying ? 16 : 4, backgroundColor: color }]} />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    justifyContent: 'center',
    gap: 3,
    height: 24,
    paddingHorizontal: 6,
  },
  bar: {
    width: 3.2,
    borderRadius: 2,
    opacity: 0.9,
  },
});

