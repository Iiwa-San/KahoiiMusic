import React from 'react';
import { StyleSheet, View } from 'react-native';
import Slider from '@react-native-community/slider';
import { Ionicons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';

export const VolumeSlider: React.FC = () => {
  const { volume, setVolume } = usePlayer();

  return (
    <View style={styles.container}>
      <Ionicons name="volume-low" size={18} color="#94a3b8" />
      <Slider
        style={styles.slider}
        minimumValue={0}
        maximumValue={1}
        value={volume}
        minimumTrackTintColor="rgba(255, 255, 255, 0.6)"
        maximumTrackTintColor="rgba(255, 255, 255, 0.15)"
        thumbTintColor="#ffffff"
        onValueChange={setVolume}
      />
      <Ionicons name="volume-high" size={20} color="#94a3b8" />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    width: '100%',
    paddingHorizontal: 8,
    gap: 8,
    marginVertical: 4,
  },
  slider: {
    flex: 1,
    height: 30,
  },
});

