package com.kahoii.music.model

enum class PlaybackStatus {
    IDLE,
    BUFFERING,
    PLAYING,
    PAUSED,
    ENDED,
    ERROR
}

data class PlayerUiState(
    val currentTrack: PlaylistItem? = null,
    val status: PlaybackStatus = PlaybackStatus.IDLE,
    val currentPositionMs: Long = 0L,
    val durationMs: Long = 0L,
    val statusMessage: String = "Sẵn sàng",
    val loopMode: LoopMode = LoopMode.OFF,
    val volume: Float = 1.0f,
    val playlist: List<PlaylistItem> = emptyList(),
    val currentPlaylistIndex: Int = -1,
    val currentPlaylistId: String? = null,
    val history: List<PlaylistItem> = emptyList(),
    val searchResults: List<PlaylistItem> = emptyList(),
    val isSearching: Boolean = false,
    val isPlaylistLoading: Boolean = false
) {
    val isPlaying: Boolean
        get() = status == PlaybackStatus.PLAYING

    val progress: Float
        get() = if (durationMs > 0) (currentPositionMs.toFloat() / durationMs.toFloat()).coerceIn(0f, 1f) else 0f
}

