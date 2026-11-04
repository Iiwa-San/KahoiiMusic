import React from 'react';
import {
  StyleSheet,
  View,
  Text,
  TouchableOpacity,
  FlatList,
  Image,
  Modal,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { PlaylistItem } from '../types/music';
import { usePlayer } from '../context/PlayerContext';
import { StorageService } from '../services/StorageService';

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const HistorySheet: React.FC<Props> = ({ visible, onClose }) => {
  const { history, playTrack } = usePlayer();

  const handleSelectTrack = (track: PlaylistItem) => {
    playTrack(track);
    onClose();
  };

  const handleClearHistory = async () => {
    await StorageService.clearHistory();
    onClose();
  };

  const formatDuration = (seconds: number) => {
    if (!seconds) return '--:--';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const renderItem = ({ item }: { item: PlaylistItem }) => {
    return (
      <TouchableOpacity
        style={styles.itemContainer}
        onPress={() => handleSelectTrack(item)}
        activeOpacity={0.7}
      >
        <Image source={{ uri: item.thumbnailUrl }} style={styles.thumbnail} />
        <View style={styles.itemTextContainer}>
          <Text style={styles.itemTitle} numberOfLines={1}>
            {item.title}
          </Text>
          <Text style={styles.itemAuthor} numberOfLines={1}>
            {item.author || 'YouTube'}
          </Text>
        </View>
        <Text style={styles.itemDuration}>{formatDuration(item.durationSeconds)}</Text>
      </TouchableOpacity>
    );
  };

  return (
    <Modal visible={visible} animationType="slide" transparent>
      <View style={styles.overlay}>
        <View style={styles.sheetContainer}>
          {/* Header */}
          <View style={styles.header}>
            <View>
              <Text style={styles.headerTitle}>Lịch sử nghe nhạc</Text>
              <Text style={styles.headerSubtitle}>{history.length} bài hát gần đây</Text>
            </View>

            <View style={styles.headerActions}>
              {history.length > 0 && (
                <TouchableOpacity
                  onPress={handleClearHistory}
                  style={styles.clearBtn}
                  activeOpacity={0.7}
                >
                  <Text style={styles.clearText}>Xóa lịch sử</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
                <Ionicons name="close" size={24} color="#94a3b8" />
              </TouchableOpacity>
            </View>
          </View>

          {/* List */}
          <FlatList
            data={history}
            keyExtractor={(item, index) => `${item.id}-${index}`}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            initialNumToRender={8}
            maxToRenderPerBatch={8}
            windowSize={5}
            removeClippedSubviews={Platform.OS === 'android'}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="time-outline" size={54} color="#475569" />
                <Text style={styles.emptyTitle}>Lịch sử đang trống</Text>
                <Text style={styles.emptySubtitle}>
                  Các bài hát bạn đã nghe sẽ được lưu tại đây để nghe lại thuận tiện.
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
    color: '#94a3b8',
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
  itemAuthor: {
    color: '#94a3b8',
    fontSize: 12,
    marginTop: 2,
  },
  itemDuration: {
    color: '#64748b',
    fontSize: 11,
    marginRight: 8,
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

