import React, { useState, useEffect } from 'react';
import { StyleSheet, View, Text, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';
import { EqualizerBars } from './EqualizerBars';
import { SavedTracksService } from '../services/SavedTracksService';

  interface Props {
    onOpenAddToCollection: () => void;
  }

  export const TrackInfoSection: React.FC<Props> = ({ onOpenAddToCollection }) => {
    const {
      currentTrack,
      playbackState,
      isFavorite,
      toggleFavorite,
      statusMessage,
      downloadSpeed,
    } = usePlayer();

    const [savedService] = useState(() => SavedTracksService.getInstance());
    const [isSaved, setIsSaved] = useState(false);
    const [isDownloading, setIsDownloading] = useState(false);

    useEffect(() => {
      if (currentTrack) {
        setIsSaved(savedService.isSaved(currentTrack.id));
        setIsDownloading(savedService.isDownloading(currentTrack.id));
      }
      const unsub = savedService.subscribe(() => {
        if (currentTrack) {
          setIsSaved(savedService.isSaved(currentTrack.id));
          setIsDownloading(savedService.isDownloading(currentTrack.id));
        }
      });
      return () => {
        unsub();
      };
    }, [currentTrack, savedService]);

    if (!currentTrack) {
      return (
        <View style={styles.emptyContainer}>
          <Text style={styles.emptyTitle}>Chưa chọn bài hát</Text>
          <Text style={styles.statusSubtitle}>Dán link hoặc tìm kiếm bài hát ở trên</Text>
        </View>
      );
    }

    const isFav = isFavorite(currentTrack.id);
    const isPlaying = playbackState === 'playing';

    const handleDownload = async () => {
      if (isSaved) {
        Alert.alert('Đã tải', 'Bài hát này đã được lưu trong máy để nghe offline.');
        return;
      }
      const success = await savedService.saveTrackAsync(currentTrack);
      if (success) {
        Alert.alert('Thành công', `Đã tải về "${currentTrack.title}" để nghe offline.`);
      }
    };

    return (
      <View style={styles.container}>
        <View style={styles.infoWrapper}>
          <View style={styles.titleRow}>
            <Text style={styles.title} numberOfLines={1} ellipsizeMode="tail">
              {currentTrack.title}
            </Text>
            <EqualizerBars isPlaying={isPlaying} color="#38bdf8" />
          </View>
          <Text style={styles.author} numberOfLines={1}>
            {currentTrack.author || 'YouTube'}
          </Text>
          <View style={styles.statusRow}>
            <Text style={styles.statusText}>{statusMessage}</Text>
            {downloadSpeed && (
              <View style={styles.speedBadge}>
                <Ionicons name="flash" size={11} color="#38bdf8" style={{ marginRight: 3 }} />
                <Text style={styles.speedText}>{downloadSpeed}</Text>
              </View>
            )}
          </View>
        </View>

        {/* Action Buttons: Add to collection, Download Offline, Favorite */}
        <View style={styles.actionsRow}>
          {/* Add to collection */}
          <TouchableOpacity
            style={styles.iconBtn}
            onPress={onOpenAddToCollection}
            activeOpacity={0.7}
          >
            <Ionicons name="folder-outline" size={21} color="#cbd5e1" />
          </TouchableOpacity>

          {/* Download Offline */}
          <TouchableOpacity
            style={styles.iconBtn}
            onPress={handleDownload}
            activeOpacity={0.7}
            disabled={isDownloading}
          >
            {isDownloading ? (
              <ActivityIndicator size="small" color="#38bdf8" />
            ) : (
              <Ionicons
                name={isSaved ? 'checkmark-circle' : 'arrow-down-circle-outline'}
                size={22}
                color={isSaved ? '#10b981' : '#cbd5e1'}
              />
            )}
          </TouchableOpacity>

          {/* Favorite */}
          <TouchableOpacity
            style={styles.iconBtn}
            onPress={() => toggleFavorite(currentTrack)}
            activeOpacity={0.7}
          >
            <Ionicons
              name={isFav ? 'heart' : 'heart-outline'}
              size={23}
              color={isFav ? '#f43f5e' : '#cbd5e1'}
            />
          </TouchableOpacity>
        </View>
      </View>
    );
  };

  const styles = StyleSheet.create({
    container: {
      flexDirection: 'row',
      alignItems: 'center',
      justifyContent: 'space-between',
      width: '100%',
      paddingHorizontal: 6,
      marginVertical: 4,
    },
    emptyContainer: {
      alignItems: 'center',
      justifyContent: 'center',
      paddingVertical: 10,
    },
    infoWrapper: {
      flex: 1,
      marginRight: 10,
    },
    titleRow: {
      flexDirection: 'row',
      alignItems: 'center',
    },
    title: {
      flexShrink: 1,
      color: '#ffffff',
      fontSize: 18,
      fontWeight: '700',
      letterSpacing: 0.2,
      marginBottom: 2,
    },
    author: {
      color: '#94a3b8',
      fontSize: 13,
      fontWeight: '500',
    },
    statusRow: {
      flexDirection: 'row',
      alignItems: 'center',
      marginTop: 3,
      gap: 8,
    },
    statusText: {
      color: '#94a3b8',
      fontSize: 12,
    },
    statusSubtitle: {
      color: '#94a3b8',
      fontSize: 13,
    },
    speedBadge: {
      flexDirection: 'row',
      alignItems: 'center',
      backgroundColor: 'rgba(56, 189, 248, 0.15)',
      paddingHorizontal: 6,
      paddingVertical: 2,
      borderRadius: 6,
      borderWidth: 1,
      borderColor: 'rgba(56, 189, 248, 0.3)',
    },
    speedText: {
      color: '#38bdf8',
      fontSize: 11,
      fontWeight: '700',
      fontVariant: ['tabular-nums'],
    },
    emptyTitle: {
      color: '#ffffff',
      fontSize: 18,
      fontWeight: '600',
      textAlign: 'center',
    },
      actionsRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
      },
      iconBtn: {
        width: 38,
        height: 38,
        borderRadius: 19,
        backgroundColor: 'rgba(255, 255, 255, 0.08)',
        justifyContent: 'center',
        alignItems: 'center',
      },
    });
