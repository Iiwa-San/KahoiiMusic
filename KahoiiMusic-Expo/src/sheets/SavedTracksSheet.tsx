import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  View,
  Text,
  TouchableOpacity,
  FlatList,
  Image,
  Modal,
  Alert,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { SavedTracksService } from '../services/SavedTracksService';
import { PlaylistItem } from '../types/music';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const SavedTracksSheet: React.FC<Props> = ({ visible, onClose }) => {
  const { playTrack, currentTrack } = usePlayer();
  const [savedService] = useState(() => SavedTracksService.getInstance());
  const [savedTracks, setSavedTracks] = useState<PlaylistItem[]>(savedService.savedTracks);

  useEffect(() => {
    const unsub = savedService.subscribe(() => {
      setSavedTracks([...savedService.savedTracks]);
    });
    return () => {
      unsub();
    };
  }, [savedService]);

  const handlePlayTrack = (track: PlaylistItem) => {
    playTrack(track);
    onClose();
  };

  const handleDeleteTrack = (id: string, title: string) => {
    Alert.alert('Xóa bài hát đã tải', `Bạn có chắc muốn xóa bài "${title}" khỏi bộ nhớ máy?`, [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa',
        style: 'destructive',
        onPress: () => savedService.deleteTrack(id),
      },
    ]);
  };

  const handleClearAll = () => {
    Alert.alert('Xóa tất cả bài đã tải', 'Bạn có chắc chắn muốn xóa toàn bộ bài hát offline?', [
      { text: 'Hủy', style: 'cancel' },
      {
        text: 'Xóa tất cả',
        style: 'destructive',
        onPress: () => savedService.clearAll(),
      },
    ]);
  };

  const formatDuration = (seconds: number) => {
    if (!seconds) return '--:--';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const renderItem = ({ item }: { item: PlaylistItem }) => {
    const isCurrent = currentTrack?.id === item.id;

    return (
      <View style={[styles.itemContainer, isCurrent && styles.activeItem]}>
        <TouchableOpacity
          style={styles.itemContent}
          onPress={() => handlePlayTrack(item)}
          activeOpacity={0.7}
        >
          <Image source={{ uri: item.thumbnailUrl }} style={styles.thumbnail} />
          <View style={styles.itemTextContainer}>
            <Text style={[styles.itemTitle, isCurrent && styles.activeText]} numberOfLines={1}>
              {item.title}
            </Text>
            <View style={styles.subRow}>
              <View style={styles.offlineBadge}>
                <Ionicons name="cloud-offline" size={10} color="#10b981" style={{ marginRight: 3 }} />
                <Text style={styles.offlineBadgeText}>Offline</Text>
              </View>
              <Text style={styles.itemAuthor} numberOfLines={1}>
                {item.author || 'YouTube'}
              </Text>
            </View>
          </View>
          <Text style={styles.itemDuration}>{formatDuration(item.durationSeconds)}</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.deleteBtn}
          onPress={() => handleDeleteTrack(item.id, item.title)}
          activeOpacity={0.6}
        >
          <Ionicons name="trash-outline" size={18} color="#ef4444" />
        </TouchableOpacity>
      </View>
    );
  };

  return (
    <Modal visible={visible} animationType="slide" transparent>
      <View style={styles.overlay}>
        <View style={styles.sheetContainer}>
          {/* Header */}
          <View style={styles.header}>
            <View>
              <Text style={styles.headerTitle}>Bài hát đã tải về máy</Text>
              <Text style={styles.headerSubtitle}>{savedTracks.length} bài hát (Nghe không cần mạng)</Text>
            </View>

            <View style={styles.headerActions}>
              {savedTracks.length > 0 && (
                <TouchableOpacity onPress={handleClearAll} style={styles.clearBtn} activeOpacity={0.7}>
                  <Text style={styles.clearText}>Xóa hết</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
                <Ionicons name="close" size={24} color="#94a3b8" />
              </TouchableOpacity>
            </View>
          </View>

          {/* List */}
          <FlatList
            data={savedTracks}
            keyExtractor={(item) => item.id}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            initialNumToRender={8}
            maxToRenderPerBatch={8}
            windowSize={5}
            removeClippedSubviews={Platform.OS === 'android'}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="arrow-down-circle-outline" size={54} color="#475569" />
                <Text style={styles.emptyTitle}>Chưa có bài hát nào được tải về</Text>
                <Text style={styles.emptySubtitle}>
                  Bạn có thể bấm biểu tượng tải về trên bài hát để lưu lại nghe khi không có kết nối Internet.
                </Text>
              </View>
            }
          />
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.75)',
    justifyContent: 'flex-end',
  },
  sheetContainer: {
    height: '82%',
    backgroundColor: '#0f172a',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    paddingTop: 16,
    paddingHorizontal: 16,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    marginBottom: 8,
  },
  headerTitle: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '700',
  },
  headerSubtitle: {
    color: '#10b981',
    fontSize: 12,
    marginTop: 2,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  clearBtn: {
    backgroundColor: 'rgba(239, 68, 68, 0.15)',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 8,
  },
  clearText: {
    color: '#ef4444',
    fontSize: 12,
    fontWeight: '600',
  },
  closeBtn: {
    padding: 6,
  },
  listContent: {
    paddingBottom: 32,
    paddingTop: 8,
  },
  itemContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderRadius: 12,
    padding: 8,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'transparent',
  },
  activeItem: {
    borderColor: 'rgba(16, 185, 129, 0.5)',
    backgroundColor: 'rgba(16, 185, 129, 0.08)',
  },
  itemContent: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  thumbnail: {
    width: 48,
    height: 48,
    borderRadius: 8,
    backgroundColor: '#1e293b',
  },
  itemTextContainer: {
    flex: 1,
    marginLeft: 12,
    marginRight: 8,
  },
  itemTitle: {
    color: '#ffffff',
    fontSize: 14,
    fontWeight: '600',
  },
  activeText: {
    color: '#10b981',
  },
  subRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 3,
    gap: 6,
  },
  offlineBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(16, 185, 129, 0.15)',
    paddingHorizontal: 5,
    paddingVertical: 1,
    borderRadius: 4,
  },
  offlineBadgeText: {
    color: '#10b981',
    fontSize: 10,
    fontWeight: '600',
  },
  itemAuthor: {
    color: '#94a3b8',
    fontSize: 12,
    flex: 1,
  },
  itemDuration: {
    color: '#64748b',
    fontSize: 11,
    marginRight: 8,
  },
  deleteBtn: {
    padding: 8,
  },
  emptyContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 50,
  },
  emptyTitle: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '600',
    marginTop: 12,
  },
  emptySubtitle: {
    color: '#64748b',
    fontSize: 13,
    textAlign: 'center',
    marginTop: 6,
    paddingHorizontal: 24,
  },
});
