package com.kahoii.music.model

enum class PlaybackStatus {
    IDLE,
    BUFFERING,
    PLAYING,
    PAUSED,
    ENDED,
    ERROR
}

enum class ActiveQueueSource(val label: String) {
    PLAYLIST("Danh sách"),
    SAVED("Đã tải"),
    FAVORITES("Yêu thích"),
    HISTORY("Lịch sử")
}

data class PlayerUiState(
    val currentTrack: PlaylistItem? = null,
    val status: PlaybackStatus = PlaybackStatus.IDLE,
    val currentPositionMs: Long = 0L,
    val durationMs: Long = 0L,
    val statusMessage: String = "Sẵn sàng",
    val loopMode: LoopMode = LoopMode.OFF,
    val isShuffled: Boolean = false,
    val activeQueueSource: ActiveQueueSource = ActiveQueueSource.PLAYLIST,
    val volume: Float = 1.0f,
    val isMuted: Boolean = false,
    val preMuteVolume: Float = 1.0f,
    val playlist: List<PlaylistItem> = emptyList(),
    val currentPlaylistIndex: Int = -1,
    val currentPlaylistId: String? = null,
    val history: List<PlaylistItem> = emptyList(),
    val savedTracks: List<PlaylistItem> = emptyList(),
    val favorites: List<PlaylistItem> = emptyList(),
    val downloadingTrackIds: Set<String> = emptySet(),
    val sleepTimerOption: SleepTimerOption = SleepTimerOption.OFF,
    val sleepTimerRemainingSeconds: Long = 0L,
    val searchResults: List<PlaylistItem> = emptyList(),
    val isSearching: Boolean = false,
    val isPlaylistLoading: Boolean = false
) {
    val isPlaying: Boolean
        get() = status == PlaybackStatus.PLAYING

    val progress: Float
        get() = if (durationMs > 0) (currentPositionMs.toFloat() / durationMs.toFloat()).coerceIn(0f, 1f) else 0f

    val isSleepTimerActive: Boolean
        get() = sleepTimerOption.isActive

    val isCurrentTrackFavorite: Boolean
        get() = currentTrack != null && favorites.any { it.id.equals(currentTrack.id, ignoreCase = true) }

    val isCurrentTrackSaved: Boolean
        get() = currentTrack != null && savedTracks.any { it.id.equals(currentTrack.id, ignoreCase = true) }

    val isCurrentTrackDownloading: Boolean
        get() = currentTrack != null && downloadingTrackIds.contains(currentTrack.id)

    val activeQueue: List<PlaylistItem>
        get() = when (activeQueueSource) {
            ActiveQueueSource.PLAYLIST -> playlist
            ActiveQueueSource.SAVED -> savedTracks
            ActiveQueueSource.FAVORITES -> favorites
            ActiveQueueSource.HISTORY -> history
        }

    val currentQueueIndex: Int
        get() {
            val curr = currentTrack ?: return -1
            return activeQueue.indexOfFirst { it.id.equals(curr.id, ignoreCase = true) }
        }

    val activeQueueTitle: String
        get() {
            val idx = currentQueueIndex
            val total = activeQueue.size
            return if (idx >= 0 && total > 0) {
                "${activeQueueSource.label} (${idx + 1}/$total)"
            } else {
                activeQueueSource.label
            }
        }

    val sleepTimerFormatted: String
        get() {
            if (sleepTimerOption == SleepTimerOption.END_OF_TRACK) return "Hết bài"
            if (!sleepTimerOption.isActive || sleepTimerRemainingSeconds <= 0) return ""
            val m = sleepTimerRemainingSeconds / 60
            val s = sleepTimerRemainingSeconds % 60
            return String.format(java.util.Locale.getDefault(), "%02d:%02d", m, s)
        }
}
