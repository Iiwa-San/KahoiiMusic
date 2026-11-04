import React from 'react';
import {
  StyleSheet,
  View,
  Text,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as Clipboard from 'expo-clipboard';
import { YouTubeResolver } from '../services/YouTubeResolver';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  onOpenSearch: () => void;
  onOpenSleepTimer: () => void;
}

export const SearchPillBar: React.FC<Props> = ({ onOpenSearch, onOpenSleepTimer }) => {
  const { playTrack, addPlaylistToQueue, sleepTimerRemaining } = usePlayer();

  const handlePasteClipboard = async () => {
    try {
      const text = await Clipboard.getStringAsync();
      const trimmed = text.trim();
      if (!trimmed) {
        Alert.alert('Bộ nhớ tạm trống', 'Vui lòng sao chép liên kết YouTube trước khi dán.');
        return;
      }

      const resolver = YouTubeResolver.getInstance();
      const playlistId = resolver.extractPlaylistId(trimmed);
      const videoId = resolver.extractVideoId(trimmed);

      if (playlistId && !videoId) {
        // Load playlist
        Alert.alert('Đang tải danh sách phát...', 'Đang nạp bài hát từ YouTube Playlist.');
        const playlist = await resolver.loadPlaylist(playlistId);
        if (playlist.items.length > 0) {
          addPlaylistToQueue(playlist.items, true);
          Alert.alert('Thành công', `Đã nạp ${playlist.items.length} bài hát từ playlist.`);
        } else {
          Alert.alert('Lỗi', 'Không thể tải danh sách phát từ liên kết này.');
        }
        return;
      }

      if (videoId) {
        // Play single video
        playTrack({
          id: videoId,
          title: 'Đang tải bài hát...',
          author: 'YouTube',
          durationSeconds: 0,
          thumbnailUrl: `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`,
        });
        return;
      }

      // If text is a search term, open search with text
      Alert.alert(
        'Liên kết không hợp lệ',
        'Vui lòng dán link video hoặc playlist YouTube (ví dụ: https://youtu.be/...)'
      );
    } catch (e: any) {
      Alert.alert('Lỗi', e.message || 'Không thể đọc nội dung bộ nhớ tạm.');
    }
  };

  const formatTimerBadge = (seconds: number) => {
    const mins = Math.ceil(seconds / 60);
    return `${mins}m`;
  };

  return (
    <View style={styles.container}>
      {/* Search Input Trigger */}
      <TouchableOpacity
        style={styles.searchPill}
        onPress={onOpenSearch}
        activeOpacity={0.8}
      >
        <Ionicons name="search" size={18} color="#94a3b8" style={styles.icon} />
        <Text style={styles.placeholder} numberOfLines={1}>
          Tìm kiếm bài hát, ca sĩ, dán link...
        </Text>
      </TouchableOpacity>

      {/* Paste Button */}
      <TouchableOpacity
        style={styles.actionBtn}
        onPress={handlePasteClipboard}
        activeOpacity={0.7}
      >
        <Ionicons name="clipboard-outline" size={19} color="#38bdf8" />
      </TouchableOpacity>

      {/* Sleep Timer Button */}
      <TouchableOpacity
        style={[styles.actionBtn, sleepTimerRemaining !== null && styles.timerActiveBtn]}
        onPress={onOpenSleepTimer}
        activeOpacity={0.7}
      >
        <Ionicons
          name="moon-outline"
          size={18}
          color={sleepTimerRemaining !== null ? '#f59e0b' : '#94a3b8'}
        />
        {sleepTimerRemaining !== null && (
          <View style={styles.timerBadge}>
            <Text style={styles.timerBadgeText}>
              {formatTimerBadge(sleepTimerRemaining)}
            </Text>
          </View>
        )}
      </TouchableOpacity>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    width: '100%',
  },
  searchPill: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 24,
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
  },
  icon: {
    marginRight: 8,
  },
  placeholder: {
    color: '#94a3b8',
    fontSize: 14,
  },
  actionBtn: {
    width: 42,
    height: 42,
    borderRadius: 21,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    position: 'relative',
  },
  timerActiveBtn: {
    backgroundColor: 'rgba(245, 158, 11, 0.18)',
    borderColor: 'rgba(245, 158, 11, 0.4)',
  },
  timerBadge: {
    position: 'absolute',
    top: -4,
    right: -4,
    backgroundColor: '#f59e0b',
    borderRadius: 8,
    paddingHorizontal: 4,
    paddingVertical: 1,
  },
  timerBadgeText: {
    color: '#000',
    fontSize: 9,
    fontWeight: '700',
  },
});

