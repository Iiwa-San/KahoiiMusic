import React, { useState, useEffect } from 'react';
import { StyleSheet, View, Text, TouchableOpacity, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { FirebaseAuthService } from '../services/FirebaseAuthService';
import { ErrorLogService } from '../services/ErrorLogService';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  onOpenAccount: () => void;
  onOpenErrorLogs: () => void;
}

export const HeaderBar: React.FC<Props> = ({ onOpenAccount, onOpenErrorLogs }) => {
  const { statusMessage, currentPalette } = usePlayer();
  const [authService] = useState(() => FirebaseAuthService.getInstance());
  const [errorService] = useState(() => ErrorLogService.getInstance());

  const [isLoggedIn, setIsLoggedIn] = useState(authService.isLoggedIn);
  const [logCount, setLogCount] = useState(errorService.logs.length);
  const [showArtworkBg, setShowArtworkBg] = useState(true);

  useEffect(() => {
    const unsubAuth = authService.subscribe(() => {
      setIsLoggedIn(authService.isLoggedIn);
    });
    const unsubLog = errorService.subscribe(() => {
      setLogCount(errorService.logs.length);
    });
    return () => {
      unsubAuth();
      unsubLog();
    };
  }, [authService, errorService]);

  const handleOpenMenu = () => {
    Alert.alert('Cài đặt & Tùy chọn', undefined, [
      {
        text: showArtworkBg ? '✓ Ảnh bìa khi phát nền: Bật' : 'Ảnh bìa khi phát nền: Tắt',
        onPress: () => setShowArtworkBg(!showArtworkBg),
      },
      {
        text: `Nhật ký lỗi hệ thống (${logCount})`,
        onPress: onOpenErrorLogs,
      },
      { text: 'Đóng', style: 'cancel' },
    ]);
  };

  return (
    <View style={styles.container}>
      {/* Brand Logo & Name */}
      <View style={styles.brandRow}>
        <Ionicons name="musical-notes" size={20} color="#38bdf8" style={{ marginRight: 6 }} />
        <Text style={styles.brandTitle}>Kahoii Music</Text>
      </View>

      {/* Right Actions */}
      <View style={styles.actionsRow}>
        {/* Status indicator capsule */}
        <View style={styles.statusCapsule}>
          <Text style={styles.statusText} numberOfLines={1}>
            {statusMessage}
          </Text>
        </View>

        {/* Options Menu Button */}
        <TouchableOpacity style={styles.iconBtn} onPress={handleOpenMenu} activeOpacity={0.7}>
          <Ionicons name="ellipsis-horizontal" size={18} color="#cbd5e1" />
          {logCount > 0 && <View style={styles.errorDot} />}
        </TouchableOpacity>

        {/* Account / User Avatar Button */}
        <TouchableOpacity style={styles.accountBtn} onPress={onOpenAccount} activeOpacity={0.7}>
          <Ionicons
            name={isLoggedIn ? 'person-circle' : 'person-circle-outline'}
            size={26}
            color={isLoggedIn ? '#ec4899' : '#e2e8f0'}
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
    paddingVertical: 6,
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  brandTitle: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  actionsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  statusCapsule: {
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 14,
    maxWidth: 130,
  },
  statusText: {
    color: '#94a3b8',
    fontSize: 11,
    fontWeight: '500',
  },
  iconBtn: {
    width: 34,
    height: 34,
    borderRadius: 17,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    justifyContent: 'center',
    alignItems: 'center',
    position: 'relative',
  },
  errorDot: {
    position: 'absolute',
    top: 5,
    right: 5,
    width: 7,
    height: 7,
    borderRadius: 3.5,
    backgroundColor: '#ef4444',
  },
  accountBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
});

