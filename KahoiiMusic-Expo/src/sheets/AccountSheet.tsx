import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Alert,
  Modal,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { FirebaseAuthService } from '../services/FirebaseAuthService';
import { FirebaseSyncService } from '../services/FirebaseSyncService';
import { PlaylistCollectionService } from '../services/PlaylistCollectionService';
import { SavedTracksService } from '../services/SavedTracksService';
import { usePlayer } from '../context/PlayerContext';
import { FirebaseUserInfo } from '../types/music';

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const AccountSheet: React.FC<Props> = ({ visible, onClose }) => {
  const {
    currentTrack,
    history,
    favorites,
    applyCloudPayload,
    clearUserDataExceptPlayingTrack,
    getCloudPayload,
  } = usePlayer();

  const [authService] = useState(() => FirebaseAuthService.getInstance());
  const [syncService] = useState(() => FirebaseSyncService.getInstance());
  const [collectionService] = useState(() => PlaylistCollectionService.getInstance());
  const [savedService] = useState(() => SavedTracksService.getInstance());

  const [user, setUser] = useState(authService.currentUser);
  const [tab, setTab] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [isSyncing, setIsSyncing] = useState(false);
  const [syncStatus, setSyncStatus] = useState('');
  const [collectionsCount, setCollectionsCount] = useState(collectionService.collections.length);
  const [savedCount, setSavedCount] = useState(savedService.savedTracks.length);

  useEffect(() => {
    const unsubAuth = authService.subscribe(() => {
      setUser(authService.currentUser);
    });
    const unsubSync = syncService.subscribe(() => {
      setIsSyncing(syncService.isSyncing);
      setSyncStatus(syncService.syncStatusMessage);
    });
    const unsubCol = collectionService.subscribe(() => {
      setCollectionsCount(collectionService.collections.length);
    });
    const unsubSaved = savedService.subscribe(() => {
      setSavedCount(savedService.savedTracks.length);
    });
    return () => {
      unsubAuth();
      unsubSync();
      unsubCol();
      unsubSaved();
    };
  }, [authService, syncService, collectionService, savedService]);

  const handleSyncFromCloud = async (targetUser?: FirebaseUserInfo | null) => {
    const activeUser = targetUser || user || authService.currentUser;
    if (!activeUser) return;

    const payload = await syncService.syncFetch(activeUser.localId, activeUser.idToken);
    if (payload) {
      await applyCloudPayload(payload);
      Alert.alert(
        'Đồng bộ hoàn tất',
        `Đã nạp ${payload.favorites?.length || 0} bài yêu thích, ${
          payload.collections?.length || 0
        } bộ sưu tập, ${payload.history?.length || 0} lịch sử nghe từ đám mây.`
      );
    } else {
      // If cloud doc is empty or 404, upload current local state so it's backed up!
      await handleSyncToCloud(activeUser);
      Alert.alert(
        'Đã kết nối đám mây',
        `Tài khoản chưa có dữ liệu đám mây. Đã khởi tạo bản sao lưu dữ liệu cho ${activeUser.email}.`
      );
    }
  };

  const handleSyncToCloud = async (targetUser?: FirebaseUserInfo | null) => {
    const activeUser = targetUser || user || authService.currentUser;
    if (!activeUser) return;

    const payload = getCloudPayload();
    const success = await syncService.syncUpload(activeUser.localId, activeUser.idToken, payload);
    return success;
  };

  const handleAuth = async () => {
    const trimmedEmail = email.trim();
    if (!trimmedEmail || !password) {
      Alert.alert('Lỗi', 'Vui lòng nhập đầy đủ email và mật khẩu.');
      return;
    }

    if (tab === 'register') {
      if (password !== confirmPassword) {
        Alert.alert('Lỗi', 'Mật khẩu xác nhận không khớp.');
        return;
      }
      if (password.length < 6) {
        Alert.alert('Lỗi', 'Mật khẩu phải từ 6 ký tự trở lên.');
        return;
      }
    }

    setIsProcessing(true);
    if (tab === 'login') {
      const res = await authService.signIn(trimmedEmail, password);
      setIsProcessing(false);
      if (res.success && res.user) {
        setUser(res.user);
        // Automatically sync from cloud on login
        await handleSyncFromCloud(res.user);
      } else {
        Alert.alert('Đăng nhập thất bại', res.message || 'Email hoặc mật khẩu không đúng.');
      }
    } else {
      const res = await authService.signUp(trimmedEmail, password);
      setIsProcessing(false);
      if (res.success && res.user) {
        setUser(res.user);
        Alert.alert('Thành công', 'Đăng ký tài khoản thành công! Dữ liệu sẽ tự động đồng bộ.');
        // Initial upload for new user
        await handleSyncToCloud(res.user);
      } else {
        Alert.alert('Đăng ký thất bại', res.message || 'Không thể tạo tài khoản.');
      }
    }
  };

  const handleForgotPassword = async () => {
    const trimmedEmail = email.trim();
    if (!trimmedEmail || !trimmedEmail.includes('@')) {
      Alert.alert('Thông báo', 'Vui lòng nhập email hợp lệ ở ô trên để nhận liên kết khôi phục mật khẩu.');
      return;
    }
    setIsProcessing(true);
    const res = await authService.sendPasswordReset(trimmedEmail);
    setIsProcessing(false);
    Alert.alert(res.success ? 'Thành công' : 'Lỗi', res.message);
  };

  const handleSignOut = () => {
    Alert.alert(
      'Đăng xuất',
      'Bạn có muốn đăng xuất tài khoản không?\n\n(Danh sách bài hát của bạn đã được lưu an toàn trên đám mây và sẽ tự động tải về khi bạn đăng nhập lại).',
      [
        { text: 'Hủy', style: 'cancel' },
        {
          text: 'Đăng xuất',
          style: 'destructive',
          onPress: async () => {
            await clearUserDataExceptPlayingTrack(currentTrack?.id);
            await authService.signOut();
            setUser(null);
            Alert.alert('Đã đăng xuất', 'Đã đăng xuất khỏi tài khoản Kahoii.');
          },
        },
      ]
    );
  };

  return (
    <Modal visible={visible} animationType="slide" transparent>
      <View style={styles.overlay}>
        <View style={styles.sheetContainer}>
          {/* Header */}
          <View style={styles.header}>
            <Text style={styles.headerTitle}>Tài khoản Kahoii</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Ionicons name="close" size={24} color="#94a3b8" />
            </TouchableOpacity>
          </View>

          <ScrollView contentContainerStyle={styles.scrollContent}>
            {user ? (
              // Logged In View
              <View style={styles.profileBox}>
                <View style={styles.avatar}>
                  <Ionicons name="person-circle" size={72} color="#ec4899" />
                </View>
                <Text style={styles.emailText}>{user.email}</Text>
                <Text style={styles.uidText}>UID: {user.localId}</Text>

                {/* Stats Card (Matching PC & Native iOS) */}
                <View style={styles.statsCard}>
                  <Text style={styles.statsRow}>
                    Đã tải: {savedCount} bài  •  Bộ sưu tập: {collectionsCount}
                  </Text>
                  <Text style={styles.statsRow}>
                    Lịch sử nghe: {history.length} bài  •  Yêu thích: {favorites.length}
                  </Text>
                </View>

                {/* Sync status text */}
                <Text style={styles.syncStatusText}>
                  {syncStatus || 'Đồng bộ tự động đám mây đang bật'}
                </Text>

                {/* Single Unified Action Button */}
                <TouchableOpacity
                  style={[styles.syncBtn, isSyncing && styles.syncBtnDisabled]}
                  onPress={() => handleSyncFromCloud()}
                  disabled={isSyncing}
                >
                  {isSyncing ? (
                    <View style={styles.rowCenter}>
                      <ActivityIndicator size="small" color="#fff" style={{ marginRight: 8 }} />
                      <Text style={styles.syncBtnText}>⟳ Đang Đồng Bộ...</Text>
                    </View>
                  ) : (
                    <Text style={styles.syncBtnText}>⟳ Đồng Bộ Ngay (Tự Động Bật)</Text>
                  )}
                </TouchableOpacity>

                {/* Sign Out Button */}
                <TouchableOpacity style={styles.signOutBtn} onPress={handleSignOut}>
                  <Text style={styles.signOutText}>Đăng Xuất</Text>
                </TouchableOpacity>
              </View>
            ) : (
              // Auth Form
              <View style={styles.formBox}>
                {/* Segmented Tab */}
                <View style={styles.tabContainer}>
                  <TouchableOpacity
                    style={[styles.tabBtn, tab === 'login' && styles.tabBtnActive]}
                    onPress={() => setTab('login')}
                  >
                    <Text style={[styles.tabText, tab === 'login' && styles.tabTextActive]}>
                      Đăng nhập
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.tabBtn, tab === 'register' && styles.tabBtnActive]}
                    onPress={() => setTab('register')}
                  >
                    <Text style={[styles.tabText, tab === 'register' && styles.tabTextActive]}>
                      Đăng ký
                    </Text>
                  </TouchableOpacity>
                </View>

                {/* Email Field */}
                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Email</Text>
                  <View style={styles.inputWrapper}>
                    <Ionicons name="mail" size={18} color="#94a3b8" style={styles.inputIcon} />
                    <TextInput
                      style={styles.input}
                      placeholder="your.email@example.com"
                      placeholderTextColor="#64748b"
                      value={email}
                      onChangeText={setEmail}
                      autoCapitalize="none"
                      keyboardType="email-address"
                    />
                  </View>
                </View>

                {/* Password Field */}
                <View style={styles.inputGroup}>
                  <Text style={styles.inputLabel}>Mật khẩu</Text>
                  <View style={styles.inputWrapper}>
                    <Ionicons name="lock-closed" size={18} color="#94a3b8" style={styles.inputIcon} />
                    <TextInput
                      style={styles.input}
                      placeholder="Tối thiểu 6 ký tự"
                      placeholderTextColor="#64748b"
                      value={password}
                      onChangeText={setPassword}
                      secureTextEntry
                    />
                  </View>
                </View>

                {/* Quên mật khẩu (chỉ tab đăng nhập) */}
                {tab === 'login' && (
                  <TouchableOpacity onPress={handleForgotPassword} style={styles.forgotBtn}>
                    <Text style={styles.forgotText}>Quên mật khẩu?</Text>
                  </TouchableOpacity>
                )}

                {/* Confirm Password (chỉ tab đăng ký) */}
                {tab === 'register' && (
                  <View style={styles.inputGroup}>
                    <Text style={styles.inputLabel}>Nhập lại mật khẩu</Text>
                    <View style={styles.inputWrapper}>
                      <Ionicons name="shield-checkmark" size={18} color="#94a3b8" style={styles.inputIcon} />
                      <TextInput
                        style={styles.input}
                        placeholder="Nhập lại mật khẩu trên"
                        placeholderTextColor="#64748b"
                        value={confirmPassword}
                        onChangeText={setConfirmPassword}
                        secureTextEntry
                      />
                    </View>
                  </View>
                )}

                {/* Submit Button */}
                <TouchableOpacity
                  style={[styles.submitBtn, isProcessing && styles.submitBtnDisabled]}
                  onPress={handleAuth}
                  disabled={isProcessing}
                >
                  {isProcessing ? (
                    <ActivityIndicator color="#fff" />
                  ) : (
                    <Text style={styles.submitBtnText}>
                      {tab === 'login' ? 'Đăng nhập' : 'Đăng ký tài khoản'}
                    </Text>
                  )}
                </TouchableOpacity>

                <Text style={styles.cloudHint}>
                  Khi đăng nhập, toàn bộ bài hát, danh sách yêu thích, đã tải và bộ sưu tập sẽ tự động đồng bộ lên đám mây Firebase.
                </Text>
              </View>
            )}
          </ScrollView>
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
    backgroundColor: '#0d0818',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    height: '80%',
    paddingHorizontal: 20,
    paddingTop: 16,
    paddingBottom: 24,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingBottom: 16,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.08)',
  },
  headerTitle: {
    color: '#fff',
    fontSize: 18,
    fontWeight: '700',
  },
  closeBtn: {
    padding: 4,
  },
  scrollContent: {
    paddingVertical: 20,
  },
  profileBox: {
    alignItems: 'center',
    paddingBottom: 24,
  },
  avatar: {
    marginBottom: 8,
  },
  emailText: {
    color: '#fff',
    fontSize: 17,
    fontWeight: '700',
    marginBottom: 4,
  },
  uidText: {
    color: '#94a3b8',
    fontSize: 11,
    fontFamily: Platform.OS === 'ios' ? 'Menlo' : 'monospace',
    marginBottom: 16,
  },
  statsCard: {
    width: '100%',
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    borderRadius: 14,
    paddingVertical: 14,
    paddingHorizontal: 16,
    alignItems: 'center',
    marginBottom: 14,
  },
  statsRow: {
    color: '#cbd5e1',
    fontSize: 13,
    fontWeight: '500',
    marginVertical: 2,
  },
  syncStatusText: {
    color: '#ec4899',
    fontSize: 12,
    fontStyle: 'italic',
    textAlign: 'center',
    marginBottom: 18,
  },
  rowCenter: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  syncBtn: {
    backgroundColor: '#ec4899',
    paddingVertical: 13,
    borderRadius: 12,
    alignItems: 'center',
    width: '100%',
    marginBottom: 12,
  },
  syncBtnDisabled: {
    opacity: 0.6,
  },
  syncBtnText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '700',
  },
  signOutBtn: {
    backgroundColor: 'rgba(239, 68, 68, 0.12)',
    paddingVertical: 12,
    borderRadius: 12,
    alignItems: 'center',
    width: '100%',
    marginTop: 6,
  },
  signOutText: {
    color: '#f87171',
    fontSize: 14,
    fontWeight: '700',
  },
  formBox: {
    backgroundColor: 'rgba(255,255,255,0.04)',
    borderRadius: 18,
    padding: 18,
  },
  tabContainer: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255,255,255,0.06)',
    borderRadius: 10,
    padding: 4,
    marginBottom: 18,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 8,
  },
  tabBtnActive: {
    backgroundColor: '#ec4899',
  },
  tabText: {
    color: '#94a3b8',
    fontSize: 13,
    fontWeight: '600',
  },
  tabTextActive: {
    color: '#fff',
  },
  inputGroup: {
    marginBottom: 14,
  },
  inputLabel: {
    color: 'rgba(255,255,255,0.7)',
    fontSize: 13,
    fontWeight: '500',
    marginBottom: 6,
  },
  inputWrapper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderRadius: 10,
    paddingHorizontal: 12,
    height: 46,
  },
  inputIcon: {
    marginRight: 10,
  },
  input: {
    flex: 1,
    color: '#fff',
    fontSize: 14,
  },
  forgotBtn: {
    alignSelf: 'flex-end',
    marginBottom: 14,
    marginTop: -4,
  },
  forgotText: {
    color: '#ec4899',
    fontSize: 12.5,
    fontWeight: '500',
    textDecorationLine: 'underline',
  },
  submitBtn: {
    backgroundColor: '#ec4899',
    height: 48,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 4,
    marginBottom: 14,
  },
  submitBtnDisabled: {
    opacity: 0.6,
  },
  submitBtnText: {
    color: '#fff',
    fontSize: 15,
    fontWeight: '700',
  },
  cloudHint: {
    color: 'rgba(255,255,255,0.4)',
    fontSize: 11.5,
    textAlign: 'center',
    lineHeight: 16,
  },
});
