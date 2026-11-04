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

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const FavoritesSheet: React.FC<Props> = ({ visible, onClose }) => {
  const { favorites, playTrack, toggleFavorite, addPlaylistToQueue } = usePlayer();

  const handlePlayAll = () => {
    if (favorites.length > 0) {
      addPlaylistToQueue(favorites, true);
      onClose();
    }
  };

  const handleSelectTrack = (track: PlaylistItem) => {
    playTrack(track);
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
      <View style={styles.itemContainer}>
        <TouchableOpacity
          style={styles.itemContent}
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

        <TouchableOpacity
          style={styles.favBtn}
          onPress={() => toggleFavorite(item)}
          activeOpacity={0.6}
        >
          <Ionicons name="heart" size={20} color="#f43f5e" />
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
              <Text style={styles.headerTitle}>Bài hát yêu thích</Text>
              <Text style={styles.headerSubtitle}>{favorites.length} bài hát đã lưu</Text>
            </View>

            <View style={styles.headerActions}>
              {favorites.length > 0 && (
                <TouchableOpacity
                  onPress={handlePlayAll}
                  style={styles.playAllBtn}
                  activeOpacity={0.7}
                >
                  <Ionicons name="play" size={14} color="#ffffff" style={{ marginRight: 4 }} />
                  <Text style={styles.playAllText}>Phát tất cả</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
                <Ionicons name="close" size={24} color="#94a3b8" />
              </TouchableOpacity>
            </View>
          </View>

          {/* List */}
          <FlatList
            data={favorites}
            keyExtractor={(item) => item.id}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            initialNumToRender={8}
            maxToRenderPerBatch={8}
            windowSize={5}
            removeClippedSubviews={Platform.OS === 'android'}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="heart-dislike-outline" size={54} color="#475569" />
                <Text style={styles.emptyTitle}>Chưa có bài hát yêu thích</Text>
                <Text style={styles.emptySubtitle}>
                  Nhấn biểu tượng trái tim khi đang nghe bài hát để lưu vào danh sách yêu thích.
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
  playAllBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#e11d48',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
  },
  playAllText: {
    color: '#ffffff',
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
  favBtn: {
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

