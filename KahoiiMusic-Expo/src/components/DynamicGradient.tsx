import React from 'react';
import { StyleSheet, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { GradientPalette } from '../types/music';

interface Props {
  palette: GradientPalette;
}

export const DynamicGradient: React.FC<Props> = ({ palette }) => {
  return (
    <View style={StyleSheet.absoluteFill} pointerEvents="none">
      <LinearGradient
        colors={[palette.secondary, palette.primary, palette.background]}
        start={{ x: 0.1, y: 0 }}
        end={{ x: 0.9, y: 1 }}
        style={StyleSheet.absoluteFill}
      />
      {/* Subtle glass dark tint layer */}
      <View style={styles.overlay} />
    </View>
  );
};

const styles = StyleSheet.create({
  overlay: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(5, 5, 12, 0.55)',
  },
});
