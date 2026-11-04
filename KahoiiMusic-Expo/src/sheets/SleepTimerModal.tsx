import React from 'react';
import { StyleSheet, View, Text, TouchableOpacity, Modal } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { usePlayer } from '../context/PlayerContext';

interface Props {
  visible: boolean;
  onClose: () => void;
}

const TIMER_OPTIONS = [
  { label: '15 phút', minutes: 15 },
  { label: '30 phút', minutes: 30 },
  { label: '45 phút', minutes: 45 },
  { label: '60 phút (1 giờ)', minutes: 60 },
  { label: '90 phút (1.5 giờ)', minutes: 90 },
];

export const SleepTimerModal: React.FC<Props> = ({ visible, onClose }) => {
  const { sleepTimerMinutes, sleepTimerRemaining, setSleepTimer } = usePlayer();

  const handleSelect = (mins: number | null) => {
    setSleepTimer(mins);
    onClose();
  };

  const formatCountdown = (secs: number) => {
    const m = Math.floor(secs / 60);
    const s = secs % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  return (
    <Modal visible={visible} animationType="fade" transparent>
      <View style={styles.overlay}>
        <View style={styles.modalContent}>
          {/* Header */}
          <View style={styles.header}>
            <View style={styles.headerTitleRow}>
              <Ionicons name="moon" size={20} color="#f59e0b" style={{ marginRight: 8 }} />
              <Text style={styles.title}>Hẹn giờ tắt nhạc</Text>
            </View>
            <TouchableOpacity onPress={onClose} style={styles.closeBtn}>
              <Ionicons name="close" size={22} color="#94a3b8" />
            </TouchableOpacity>
          </View>

          {/* Active status */}
          {sleepTimerRemaining !== null && (
            <View style={styles.activeStatusBox}>
              <Text style={styles.activeStatusLabel}>Thời gian còn lại:</Text>
              <Text style={styles.countdownText}>{formatCountdown(sleepTimerRemaining)}</Text>
            </View>
          )}

          {/* Options */}
          <View style={styles.optionsList}>
            {TIMER_OPTIONS.map((opt) => {
              const isSelected = sleepTimerMinutes === opt.minutes;
              return (
                <TouchableOpacity
                  key={opt.minutes}
                  style={[styles.optionItem, isSelected && styles.selectedOption]}
                  onPress={() => handleSelect(opt.minutes)}
                  activeOpacity={0.7}
                >
                  <Text style={[styles.optionText, isSelected && styles.selectedOptionText]}>
                    {opt.label}
                  </Text>
                  {isSelected && <Ionicons name="checkmark-circle" size={18} color="#38bdf8" />}
                </TouchableOpacity>
              );
            })}

            {/* Turn off option */}
            {sleepTimerMinutes !== null && (
              <TouchableOpacity
                style={[styles.optionItem, styles.cancelOption]}
                onPress={() => handleSelect(null)}
                activeOpacity={0.7}
              >
                <Text style={styles.cancelOptionText}>Tắt hẹn giờ</Text>
                <Ionicons name="close-circle-outline" size={18} color="#ef4444" />
              </TouchableOpacity>
            )}
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 24,
  },
  modalContent: {
    width: '100%',
    backgroundColor: '#0f172a',
    borderRadius: 24,
    padding: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.1)',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  headerTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  title: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '700',
  },
  closeBtn: {
    padding: 4,
  },
  activeStatusBox: {
    backgroundColor: 'rgba(245, 158, 11, 0.12)',
    borderRadius: 12,
    padding: 12,
    alignItems: 'center',
    marginBottom: 16,
    borderWidth: 1,
    borderColor: 'rgba(245, 158, 11, 0.3)',
  },
  activeStatusLabel: {
    color: '#f59e0b',
    fontSize: 12,
    fontWeight: '500',
  },
  countdownText: {
    color: '#ffffff',
    fontSize: 24,
    fontWeight: '700',
    marginTop: 4,
    fontVariant: ['tabular-nums'],
  },
  optionsList: {
    gap: 8,
  },
  optionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderRadius: 14,
  },
  selectedOption: {
    backgroundColor: 'rgba(56, 189, 248, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.4)',
  },
  optionText: {
    color: '#e2e8f0',
    fontSize: 15,
    fontWeight: '500',
  },
  selectedOptionText: {
    color: '#38bdf8',
    fontWeight: '700',
  },
  cancelOption: {
    backgroundColor: 'rgba(239, 68, 68, 0.12)',
    marginTop: 6,
  },
  cancelOptionText: {
    color: '#ef4444',
    fontSize: 15,
    fontWeight: '600',
  },
});

