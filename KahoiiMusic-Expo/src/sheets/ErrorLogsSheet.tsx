import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  View,
  Text,
  TouchableOpacity,
  FlatList,
  Modal,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { ErrorLogService } from '../services/ErrorLogService';
import { ErrorLogItem } from '../types/music';

interface Props {
  visible: boolean;
  onClose: () => void;
}

export const ErrorLogsSheet: React.FC<Props> = ({ visible, onClose }) => {
  const [errorService] = useState(() => ErrorLogService.getInstance());
  const [logs, setLogs] = useState<ErrorLogItem[]>(errorService.logs);

  useEffect(() => {
    const unsub = errorService.subscribe(() => {
      setLogs([...errorService.logs]);
    });
    return () => {
      unsub();
    };
  }, [errorService]);

  const handleClear = () => {
    errorService.clear();
  };

  const formatTime = (ts: number) => {
    const d = new Date(ts);
    return `${d.getHours().toString().padStart(2, '0')}:${d
      .getMinutes()
      .toString()
      .padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}`;
  };

  const renderItem = ({ item }: { item: ErrorLogItem }) => {
    return (
      <View style={styles.logCard}>
        <View style={styles.logHeader}>
          <View style={styles.categoryBadge}>
            <Text style={styles.categoryText}>{item.category}</Text>
          </View>
          <Text style={styles.timestampText}>{formatTime(item.timestamp)}</Text>
        </View>

        <Text style={styles.logTitle}>{item.title}</Text>
        <Text style={styles.logMessage}>{item.message}</Text>
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
              <Text style={styles.headerTitle}>Nhật ký lỗi hệ thống</Text>
              <Text style={styles.headerSubtitle}>{logs.length} bản ghi</Text>
            </View>

            <View style={styles.headerActions}>
              {logs.length > 0 && (
                <TouchableOpacity onPress={handleClear} style={styles.clearBtn} activeOpacity={0.7}>
                  <Text style={styles.clearText}>Xóa nhật ký</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
                <Ionicons name="close" size={24} color="#94a3b8" />
              </TouchableOpacity>
            </View>
          </View>

          {/* List */}
          <FlatList
            data={logs}
            keyExtractor={(item) => item.id}
            renderItem={renderItem}
            contentContainerStyle={styles.listContent}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="checkmark-circle-outline" size={54} color="#10b981" />
                <Text style={styles.emptyTitle}>Hệ thống hoạt động ổn định</Text>
                <Text style={styles.emptySubtitle}>Không có lỗi phát sinh nào được ghi nhận.</Text>
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
    height: '75%',
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
  logCard: {
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderRadius: 14,
    padding: 12,
    marginBottom: 10,
    borderLeftWidth: 3,
    borderLeftColor: '#f43f5e',
  },
  logHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  categoryBadge: {
    backgroundColor: 'rgba(244, 63, 94, 0.15)',
    paddingHorizontal: 7,
    paddingVertical: 2,
    borderRadius: 6,
  },
  categoryText: {
    color: '#f43f5e',
    fontSize: 10,
    fontWeight: '700',
  },
  timestampText: {
    color: '#64748b',
    fontSize: 11,
    fontFamily: 'monospace',
  },
  logTitle: {
    color: '#ffffff',
    fontSize: 14,
    fontWeight: '700',
    marginBottom: 2,
  },
  logMessage: {
    color: '#94a3b8',
    fontSize: 12,
    lineHeight: 16,
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
