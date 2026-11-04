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
import { EqualizerBars } from '../components/EqualizerBars';

interface Props {
  visible: boolean;
  onClose: () => void;
  onOpenSearch: () => void;
}

export const PlaylistSheet: React.FC<Props> = ({ visible, onClose, onOpenSearch }) => {
  const {
    queue,
    currentIndex,
    playQueueIndex,
    removeFromQueue,
    clearQueue,
    playbackState,
  } = usePlayer();

  const isPlaying = playbackState === 'playing';

  const formatDuration = (seconds: number) => {
    if (!seconds) return '--:--';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const renderItem = ({ item, index }: { item: PlaylistItem; index: number }) => {
    const isCurrent = index === currentIndex;

    return (
      <View style={[styles.itemContainer, isCurrent && styles.activeItem]}>
        <TouchableOpacity
          style={styles.itemContent}
          onPress={() => playQueueIndex(index)}
          activeOpacity={0.7}
        >
          <View style={styles.thumbWrapper}>
            <Image source={{ uri: item.thumbnailUrl }} style={styles.thumbnail} />
            {isCurrent && (
              <View style={styles.playingIndicator}>
                <EqualizerBars isPlaying={isPlaying} color="#ffffff" />
              </View>
            )}
          </View>

          <View style={styles.itemTextContainer}>
            <Text style={[styles.itemTitle, isCurrent && styles.activeText]} numberOfLines={1}>
              {item.title}
            </Text>
            <Text style={styles.itemAuthor} numberOfLines={1}>
              {item.author || 'YouTube'}
            </Text>
          </View>

          <Text style={styles.itemDuration}>{formatDuration(item.durationSeconds)}</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.removeBtn}
          onPress={() => removeFromQueue(index)}
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
              <Text style={styles.headerTitle}>Danh sách phát chờ</Text>
              <Text style={styles.headerSubtitle}>{queue.length} bài hát</Text>
            </View>

            <View style={styles.headerActions}>
              {queue.length > 0 && (
                <TouchableOpacity
                  onPress={clearQueue}
                  style={styles.clearBtn}
                  activeOpacity={0.7}
                >
                  <Text style={styles.clearText}>Xóa tất cả</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
                <Ionicons name="close" size={24} color="#94a3b8" />
              </TouchableOpacity>
            </View>
          </View>

          {/* List */}
          <FlatList
            data={queue}
            keyExtractor={(item, index) => `${item.id}-${index}`}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            initialNumToRender={8}
            maxToRenderPerBatch={8}
            windowSize={5}
            removeClippedSubviews={Platform.OS === 'android'}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="musical-notes-outline" size={54} color="#475569" />
                <Text style={styles.emptyTitle}>Danh sách đang trống</Text>
                <Text style={styles.emptySubtitle}>
                  Tìm kiếm bài hát hoặc dán link YouTube để bắt đầu nghe nhạc.
                </Text>
                <TouchableOpacity
                  style={styles.findSongBtn}
                  onPress={() => {
                    onClose();
                    onOpenSearch();
                  }}
                  activeOpacity={0.8}
                >
                  <Ionicons name="search" size={18} color="#ffffff" style={{ marginRight: 6 }} />
                  <Text style={styles.findSongText}>Tìm bài hát</Text>
                </TouchableOpacity>
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
    borderWidth: 1,
    borderColor: 'transparent',
  },
  activeItem: {
    borderColor: 'rgba(56, 189, 248, 0.6)',
    backgroundColor: 'rgba(56, 189, 248, 0.1)',
  },
  itemContent: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  thumbWrapper: {
    position: 'relative',
    width: 48,
    height: 48,
    borderRadius: 8,
    overflow: 'hidden',
  },
  thumbnail: {
    width: '100%',
    height: '100%',
    backgroundColor: '#1e293b',
  },
  playingIndicator: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(0, 0, 0, 0.45)',
    justifyContent: 'center',
    alignItems: 'center',
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
    color: '#38bdf8',
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
  removeBtn: {
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
  findSongBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0284c7',
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 20,
    marginTop: 20,
  },
  findSongText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 14,
  },
});
