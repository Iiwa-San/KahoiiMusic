import React, { useState, useEffect } from 'react';
import { StyleSheet, View, Text, TouchableOpacity, Share } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';
import { SavedTracksService } from '../services/SavedTracksService';

interface Props {
  onOpenQueue: () => void;
  onOpenSaved: () => void;
  onOpenHistory: () => void;
  onOpenFavorites: () => void;
}

export const BottomActions: React.FC<Props> = ({
  onOpenQueue,
  onOpenSaved,
  onOpenHistory,
  onOpenFavorites,
}) => {
  const { queue, favorites, history, currentTrack } = usePlayer();
  const [savedService] = useState(() => SavedTracksService.getInstance());
  const [savedCount, setSavedCount] = useState(savedService.savedTracks.length);

  useEffect(() => {
    const unsub = savedService.subscribe(() => {
      setSavedCount(savedService.savedTracks.length);
    });
    return () => {
      unsub();
    };
  }, [savedService]);

  const handleShare = async () => {
    if (!currentTrack) return;
    try {
      const youtubeUrl = `https://www.youtube.com/watch?v=${currentTrack.id}`;
      await Share.share({
        title: currentTrack.title,
        message: `Nghe "${currentTrack.title}" trên Kahoii Music: ${youtubeUrl}`,
        url: youtubeUrl,
      });
    } catch {
      // non-fatal
    }
  };

  return (
    <View style={styles.container}>
        {/* 1. Queue Button */}
        <TouchableOpacity style={styles.actionItem} onPress={onOpenQueue} activeOpacity={0.7}>
          <View style={styles.iconWrapper}>
            <Ionicons name="list" size={21} color="#f8fafc" />
            {queue.length > 0 && (
              <View style={styles.badge}>
                <Text style={styles.badgeText}>{queue.length}</Text>
              </View>
            )}
          </View>
          <Text style={styles.label}>Danh sách</Text>
        </TouchableOpacity>

          {/* 2. Saved / Downloads Button */}
          <TouchableOpacity style={styles.actionItem} onPress={onOpenSaved} activeOpacity={0.7}>
            <View style={styles.iconWrapper}>
              <Ionicons name="arrow-down-circle" size={21} color="#10b981" />
              {savedCount > 0 && (
                <View style={[styles.badge, styles.greenBadge]}>
                  <Text style={styles.badgeText}>{savedCount}</Text>
                </View>
              )}
            </View>
            <Text style={styles.label}>Đã tải</Text>
          </TouchableOpacity>

          {/* 3. History Button */}
          <TouchableOpacity style={styles.actionItem} onPress={onOpenHistory} activeOpacity={0.7}>
            <View style={styles.iconWrapper}>
              <Ionicons name="time-outline" size={21} color="#f8fafc" />
              {history.length > 0 && (
                <View style={[styles.badge, styles.blueBadge]}>
                  <Text style={styles.badgeText}>{history.length}</Text>
                </View>
              )}
            </View>
            <Text style={styles.label}>Lịch sử</Text>
          </TouchableOpacity>

          {/* 4. Favorites Button */}
          <TouchableOpacity style={styles.actionItem} onPress={onOpenFavorites} activeOpacity={0.7}>
            <View style={styles.iconWrapper}>
              <Ionicons name="heart" size={21} color="#f43f5e" />
              {favorites.length > 0 && (
                <View style={[styles.badge, styles.favBadge]}>
                  <Text style={styles.badgeText}>{favorites.length}</Text>
                </View>
              )}
            </View>
            <Text style={styles.label}>Yêu thích</Text>
          </TouchableOpacity>

          {/* 5. Share Button */}
          <TouchableOpacity
            style={[styles.actionItem, !currentTrack && styles.disabledItem]}
            onPress={handleShare}
            activeOpacity={0.7}
            disabled={!currentTrack}
          >
            <View style={styles.iconWrapper}>
              <Ionicons name="share-social-outline" size={21} color="#94a3b8" />
            </View>
            <Text style={styles.label}>Chia sẻ</Text>
          </TouchableOpacity>
        </View>
        );
};

        const styles = StyleSheet.create({
          container: {
          flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
        paddingVertical: 8,
        paddingHorizontal: 8,
        backgroundColor: 'rgba(255, 255, 255, 0.05)',
        borderRadius: 18,
        borderWidth: 1,
        borderColor: 'rgba(255, 255, 255, 0.08)',
  },
        actionItem: {
          alignItems: 'center',
        justifyContent: 'center',
        flex: 1,
        paddingVertical: 2,
  },
        disabledItem: {
          opacity: 0.4,
  },
        iconWrapper: {
          position: 'relative',
        marginBottom: 4,
  },
        label: {
          color: '#94a3b8',
        fontSize: 11,
        fontWeight: '500',
  },
        badge: {
          position: 'absolute',
        top: -6,
        right: -10,
        backgroundColor: '#8b5cf6',
        borderRadius: 9,
        paddingHorizontal: 4,
        paddingVertical: 1,
        minWidth: 16,
        alignItems: 'center',
  },
        greenBadge: {
          backgroundColor: '#10b981',
  },
        blueBadge: {
          backgroundColor: '#0284c7',
  },
        favBadge: {
          backgroundColor: '#f43f5e',
  },
        badgeText: {
        color: '#ffffff',
        fontSize: 9,
        fontWeight: '700',
  },
});
