import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  View,
  Text,
  TouchableOpacity,
  FlatList,
  TextInput,
  Modal,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { PlaylistCollectionService } from '../services/PlaylistCollectionService';
import { PlaylistItem, UserPlaylist } from '../types/music';

interface Props {
  visible: boolean;
  track: PlaylistItem | null;
  onClose: () => void;
}

export const AddToCollectionSheet: React.FC<Props> = ({ visible, track, onClose }) => {
  const [collectionService] = useState(() => PlaylistCollectionService.getInstance());
  const [collections, setCollections] = useState<UserPlaylist[]>(collectionService.collections);
  const [newPlaylistName, setNewPlaylistName] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  useEffect(() => {
    const unsub = collectionService.subscribe(() => {
      setCollections([...collectionService.collections]);
    });
    return () => {
      unsub();
    };
  }, [collectionService]);

  if (!track) return null;

  const handleToggleTrack = async (collection: UserPlaylist) => {
    const exists = collection.items.some((item) => item.id === track.id);
    if (exists) {
      await collectionService.removeTrack(collection.id, track.id);
    } else {
      await collectionService.addTrack(collection.id, track);
    }
  };

  const handleCreateNew = async () => {
    if (!newPlaylistName.trim()) {
      Alert.alert('Lỗi', 'Vui lòng nhập tên danh sách phát.');
      return;
    }
    const created = await collectionService.createPlaylist(newPlaylistName.trim());
    await collectionService.addTrack(created.id, track);
    setNewPlaylistName('');
    setIsCreating(false);
  };

  const renderItem = ({ item }: { item: UserPlaylist }) => {
    const isAdded = item.items.some((i) => i.id === track.id);

    return (
      <TouchableOpacity
        style={[styles.itemRow, isAdded && styles.itemAddedRow]}
        onPress={() => handleToggleTrack(item)}
        activeOpacity={0.7}
      >
        <View style={styles.itemIconWrapper}>
          <Ionicons
            name="folder"
            size={24}
            color={isAdded ? '#38bdf8' : '#94a3b8'}
          />
        </View>

        <View style={styles.itemInfo}>
          <Text style={[styles.itemName, isAdded && styles.itemAddedText]} numberOfLines={1}>
            {item.name}
          </Text>
          <Text style={styles.itemCount}>{item.items.length} bài hát</Text>
        </View>

        <Ionicons
          name={isAdded ? 'checkmark-circle' : 'add-circle-outline'}
          size={24}
          color={isAdded ? '#38bdf8' : '#64748b'}
        />
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
              <Text style={styles.headerTitle}>Lưu vào Bộ sưu tập</Text>
              <Text style={styles.headerSubtitle} numberOfLines={1}>
                {track.title}
              </Text>
            </View>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Ionicons name="close" size={24} color="#94a3b8" />
            </TouchableOpacity>
          </View>

          {/* New Playlist Creator */}
          {isCreating ? (
            <View style={styles.createBox}>
              <TextInput
                style={styles.createInput}
                placeholder="Tên danh sách mới..."
                placeholderTextColor="#64748b"
                value={newPlaylistName}
                onChangeText={setNewPlaylistName}
                autoFocus
              />
              <View style={styles.createActions}>
                <TouchableOpacity
                  style={styles.cancelBtn}
                  onPress={() => setIsCreating(false)}
                >
                  <Text style={styles.cancelText}>Hủy</Text>
                </TouchableOpacity>
                <TouchableOpacity style={styles.confirmBtn} onPress={handleCreateNew}>
                  <Text style={styles.confirmText}>Tạo</Text>
                </TouchableOpacity>
              </View>
            </View>
          ) : (
            <TouchableOpacity
              style={styles.newPlaylistBtn}
              onPress={() => setIsCreating(true)}
              activeOpacity={0.7}
            >
              <Ionicons name="add" size={20} color="#38bdf8" style={{ marginRight: 8 }} />
              <Text style={styles.newPlaylistText}>Tạo danh sách phát mới</Text>
            </TouchableOpacity>
          )}

          {/* Collections List */}
          <FlatList
            data={collections}
            keyExtractor={(item) => item.id}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="folder-open-outline" size={48} color="#475569" />
                <Text style={styles.emptyText}>Chưa có bộ sưu tập nào. Nhấn nút trên để tạo mới!</Text>
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
    height: '70%',
    backgroundColor: '#0f172a',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    paddingTop: 16,
    paddingHorizontal: 20,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
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
    maxWidth: 260,
  },
  closeBtn: {
    padding: 6,
  },
  newPlaylistBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(56, 189, 248, 0.12)',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 14,
    marginVertical: 14,
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.25)',
  },
  newPlaylistText: {
    color: '#38bdf8',
    fontSize: 14,
    fontWeight: '600',
  },
  createBox: {
    flexDirection: 'row',
    alignItems: 'center',
    marginVertical: 14,
    gap: 8,
  },
  createInput: {
    flex: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 9,
    color: '#ffffff',
    fontSize: 14,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.15)',
  },
  createActions: {
    flexDirection: 'row',
    gap: 6,
  },
  cancelBtn: {
    paddingVertical: 9,
    paddingHorizontal: 12,
    borderRadius: 10,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  cancelText: {
    color: '#94a3b8',
    fontSize: 13,
  },
  confirmBtn: {
    paddingVertical: 9,
    paddingHorizontal: 14,
    borderRadius: 10,
    backgroundColor: '#0284c7',
  },
  confirmText: {
    color: '#ffffff',
    fontSize: 13,
    fontWeight: '600',
  },
  listContent: {
    paddingBottom: 24,
  },
  itemRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderRadius: 14,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'transparent',
  },
  itemAddedRow: {
    borderColor: 'rgba(56, 189, 248, 0.3)',
    backgroundColor: 'rgba(56, 189, 248, 0.08)',
  },
  itemIconWrapper: {
    marginRight: 12,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    color: '#ffffff',
    fontSize: 15,
    fontWeight: '600',
  },
  itemAddedText: {
    color: '#38bdf8',
  },
  itemCount: {
    color: '#64748b',
    fontSize: 12,
    marginTop: 2,
  },
  emptyContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 40,
  },
  emptyText: {
    color: '#64748b',
    fontSize: 13,
    marginTop: 10,
    textAlign: 'center',
    paddingHorizontal: 20,
  },
});
