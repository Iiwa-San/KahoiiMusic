import React, { useState } from 'react';
import {
  StyleSheet,
  View,
  Text,
  TextInput,
  TouchableOpacity,
  FlatList,
  Image,
  ActivityIndicator,
  Modal,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { PlaylistItem } from '../types/music';
import { YouTubeResolver } from '../services/YouTubeResolver';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const SearchResultsSheet: React.FC<Props> = ({ visible, onClose }) => {
  const { playTrack, addToQueue, currentTrack } = usePlayer();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<PlaylistItem[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSearch = async () => {
    if (!query.trim()) return;
    setLoading(true);
    try {
      const res = await YouTubeResolver.getInstance().search(query);
      setResults(res);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
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
    const isCurrent = currentTrack?.id === item.id;

    return (
      <View style={[styles.itemContainer, isCurrent && styles.activeItem]}>
        <TouchableOpacity
          style={styles.itemContent}
          onPress={() => handleSelectTrack(item)}
          activeOpacity={0.7}
        >
          <Image source={{ uri: item.thumbnailUrl }} style={styles.thumbnail} />
          <View style={styles.itemTextContainer}>
            <Text style={[styles.itemTitle, isCurrent && styles.activeText]} numberOfLines={2}>
              {item.title}
            </Text>
            <View style={styles.itemSubRow}>
              <Text style={styles.itemAuthor} numberOfLines={1}>
                {item.author}
              </Text>
              <Text style={styles.itemDuration}>{formatDuration(item.durationSeconds)}</Text>
            </View>
          </View>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.addQueueBtn}
          onPress={() => addToQueue(item)}
          activeOpacity={0.6}
        >
          <Ionicons name="add-circle-outline" size={24} color="#38bdf8" />
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
            <Text style={styles.headerTitle}>Tìm kiếm trên YouTube</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Ionicons name="close" size={24} color="#94a3b8" />
            </TouchableOpacity>
          </View>

          {/* Search Box */}
          <View style={styles.searchBarWrapper}>
            <Ionicons name="search" size={20} color="#94a3b8" style={styles.searchIcon} />
            <TextInput
              style={styles.searchInput}
              placeholder="Nhập tên bài hát hoặc ca sĩ..."
              placeholderTextColor="#64748b"
              value={query}
              onChangeText={setQuery}
              onSubmitEditing={handleSearch}
              returnKeyType="search"
              autoFocus
            />
            {query.length > 0 && (
              <TouchableOpacity onPress={() => setQuery('')} style={styles.clearBtn}>
                <Ionicons name="close-circle" size={18} color="#64748b" />
              </TouchableOpacity>
            )}
            <TouchableOpacity style={styles.submitBtn} onPress={handleSearch}>
              <Text style={styles.submitText}>Tìm</Text>
            </TouchableOpacity>
          </View>

          {/* List or Loading */}
          {loading ? (
            <View style={styles.centerContainer}>
              <ActivityIndicator size="large" color="#38bdf8" />
              <Text style={styles.loadingText}>Đang tìm kiếm...</Text>
            </View>
          ) : (
            <FlatList
              data={results}
              keyExtractor={(item, index) => `${item.id}-${index}`}
              renderItem={renderItem}
              contentContainerStyle={styles.listContent}
              initialNumToRender={8}
              maxToRenderPerBatch={8}
              windowSize={5}
              removeClippedSubviews={Platform.OS === 'android'}
              ListEmptyComponent={
                <View style={styles.centerContainer}>
                  <Ionicons name="musical-notes-outline" size={48} color="#475569" />
                  <Text style={styles.emptyText}>
                    {query ? 'Không tìm thấy kết quả nào' : 'Nhập từ khóa và bấm Tìm'}
                  </Text>
                </View>
              }
            />
          )}
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
    height: '88%',
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
    marginBottom: 12,
  },
  headerTitle: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '700',
  },
  closeBtn: {
    padding: 6,
  },
  searchBarWrapper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 14,
    paddingHorizontal: 12,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    color: '#ffffff',
    height: 44,
    fontSize: 15,
  },
  clearBtn: {
    padding: 6,
  },
  submitBtn: {
    backgroundColor: '#0284c7',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 6,
    marginLeft: 6,
  },
  submitText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 13,
  },
  listContent: {
    paddingBottom: 32,
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
    borderColor: 'rgba(56, 189, 248, 0.5)',
    backgroundColor: 'rgba(56, 189, 248, 0.08)',
  },
  itemContent: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  thumbnail: {
    width: 64,
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
    lineHeight: 18,
  },
  activeText: {
    color: '#38bdf8',
  },
  itemSubRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 4,
  },
  itemAuthor: {
    color: '#94a3b8',
    fontSize: 12,
    flex: 1,
  },
  itemDuration: {
    color: '#64748b',
    fontSize: 11,
    marginLeft: 8,
  },
  addQueueBtn: {
    padding: 8,
  },
  centerContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingTop: 60,
  },
  loadingText: {
    color: '#94a3b8',
    marginTop: 12,
    fontSize: 14,
  },
  emptyText: {
    color: '#64748b',
    marginTop: 12,
    fontSize: 14,
  },
});

