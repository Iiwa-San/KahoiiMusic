import React, { useRef } from 'react';
import {
  StyleSheet,
  View,
  Image,
  Animated,
  Dimensions,
  PanResponder,
} from 'react-native';
import { usePlayer } from '../context/PlayerContext';

const { width } = Dimensions.get('window');
const ARTWORK_SIZE = Math.min(width * 0.74, 300);

export const ArtworkView: React.FC = () => {
  const { currentTrack, playNext, playPrevious } = usePlayer();

  // Gesture translation for swipe left/right (0 animation when idle)
  const panX = useRef(new Animated.Value(0)).current;

  // Swipe Left (Next) / Swipe Right (Prev) PanResponder
  const panResponder = useRef(
    PanResponder.create({
      onMoveShouldSetPanResponder: (_, gestureState) => {
        return Math.abs(gestureState.dx) > 15 && Math.abs(gestureState.dy) < 30;
      },
      onPanResponderMove: (_, gestureState) => {
        panX.setValue(gestureState.dx);
      },
      onPanResponderRelease: (_, gestureState) => {
        const threshold = 60;
        if (gestureState.dx < -threshold) {
          playNext();
        } else if (gestureState.dx > threshold) {
          playPrevious();
        }
        Animated.spring(panX, {
          toValue: 0,
          friction: 6,
          tension: 40,
          useNativeDriver: true,
        }).start();
      },
    })
  ).current;

  const rotateAnim = panX.interpolate({
    inputRange: [-200, 0, 200],
    outputRange: ['-8deg', '0deg', '8deg'],
    extrapolate: 'clamp',
  });

  const artworkUri =
    currentTrack?.thumbnailUrl ||
    (currentTrack?.id ? `https://i.ytimg.com/vi/${currentTrack.id}/hqdefault.jpg` : null);

  return (
    <View style={styles.container} {...panResponder.panHandlers}>
      {/* Lightweight subtle glow backdrop - minimal GPU rasterization footprint */}
      <View style={styles.glowBackdrop} />

      {/* Main Artwork with zero idle animation overhead */}
      <Animated.View
        style={[
          styles.artworkWrapper,
          {
            transform: [
              { translateX: panX },
              { rotate: rotateAnim },
            ],
          },
        ]}
      >
        {artworkUri ? (
          <Image
            source={{ uri: artworkUri }}
            style={styles.image}
            resizeMode="cover"
          />
        ) : (
          <View style={styles.placeholderContainer}>
            <Image
              source={require('../../assets/logo_icon.png')}
              style={styles.placeholderLogo}
              resizeMode="contain"
            />
          </View>
        )}
      </Animated.View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 10,
  },
  glowBackdrop: {
    position: 'absolute',
    width: ARTWORK_SIZE - 24,
    height: ARTWORK_SIZE - 24,
    borderRadius: 24,
    backgroundColor: 'rgba(99, 102, 241, 0.18)',
    shadowColor: '#818cf8',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.35,
    shadowRadius: 12,
    elevation: 6,
  },
  artworkWrapper: {
    width: ARTWORK_SIZE,
    height: ARTWORK_SIZE,
    borderRadius: 24,
    overflow: 'hidden',
    backgroundColor: '#171923',
    borderWidth: 1.5,
    borderColor: 'rgba(255, 255, 255, 0.15)',
    elevation: 8,
  },
  image: {
    width: '100%',
    height: '100%',
  },
  placeholderContainer: {
    width: '100%',
    height: '100%',
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#11131f',
  },
  placeholderLogo: {
    width: 120,
    height: 120,
    opacity: 0.85,
  },
});
