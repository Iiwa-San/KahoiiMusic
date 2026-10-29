package com.kahoii.music.viewmodel

import android.app.Application
import android.content.ComponentName
import android.net.Uri
import androidx.core.content.ContextCompat
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.MediaItem
import androidx.media3.common.MediaMetadata
import androidx.media3.common.Player
import androidx.media3.session.MediaController
import androidx.media3.session.SessionToken
import com.google.common.util.concurrent.ListenableFuture
import com.kahoii.music.data.HistoryRepository
import com.kahoii.music.data.YouTubeRepository
import com.kahoii.music.model.LoopMode
import com.kahoii.music.model.PlaybackStatus
import com.kahoii.music.model.PlayerUiState
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.service.MusicPlaybackService
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val youTubeRepository = YouTubeRepository()
    private val historyRepository = HistoryRepository(application)

    private val _uiState = MutableStateFlow(PlayerUiState())
    val uiState: StateFlow<PlayerUiState> = _uiState.asStateFlow()

    private var controllerFuture: ListenableFuture<MediaController>? = null
    private var controller: MediaController? = null
    private var progressJob: Job? = null
    private var pendingMediaItem: Pair<String, PlaylistItem>? = null

    init {
        initMediaController()
        observeHistory()
    }

    private fun initMediaController() {
        val sessionToken = SessionToken(
            getApplication(),
            ComponentName(getApplication(), MusicPlaybackService::class.java)
        )
        controllerFuture = MediaController.Builder(getApplication(), sessionToken).buildAsync()
        controllerFuture?.addListener({
            controller = controllerFuture?.get()
            setupPlayerListener()
            startProgressUpdates()
            pendingMediaItem?.let { (url, track) ->
                playStreamUrl(url, track)
                pendingMediaItem = null
            }
        }, ContextCompat.getMainExecutor(getApplication()))
    }

    private fun setupPlayerListener() {
        controller?.addListener(object : Player.Listener {
            override fun onPlaybackStateChanged(playbackState: Int) {
                when (playbackState) {
                    Player.STATE_BUFFERING -> {
                        _uiState.update { it.copy(status = PlaybackStatus.BUFFERING, statusMessage = "Đang nạp âm thanh...") }
                    }
                    Player.STATE_READY -> {
                        val isPlaying = controller?.isPlaying == true
                        val newStatus = if (isPlaying) PlaybackStatus.PLAYING else PlaybackStatus.PAUSED
                        val duration = controller?.duration?.coerceAtLeast(0L) ?: 0L
                        _uiState.update {
                            it.copy(
                                status = newStatus,
                                durationMs = duration,
                                statusMessage = if (isPlaying) "Đang phát" else "Tạm dừng"
                            )
                        }
                    }
                    Player.STATE_ENDED -> {
                        _uiState.update { it.copy(status = PlaybackStatus.ENDED, statusMessage = "Đã phát xong") }
                        handleTrackEnded()
                    }
                    Player.STATE_IDLE -> {
                        _uiState.update { it.copy(status = PlaybackStatus.IDLE) }
                    }
                }
            }

            override fun onIsPlayingChanged(isPlaying: Boolean) {
                val newStatus = if (isPlaying) PlaybackStatus.PLAYING else {
                    if (controller?.playbackState == Player.STATE_ENDED) PlaybackStatus.ENDED else PlaybackStatus.PAUSED
                }
                _uiState.update {
                    it.copy(
                        status = newStatus,
                        statusMessage = if (isPlaying) "Đang phát" else "Tạm dừng"
                    )
                }
            }
        })
    }

    private fun observeHistory() {
        viewModelScope.launch {
            historyRepository.history.collect { histList ->
                _uiState.update { it.copy(history = histList) }
            }
        }
    }

    private fun startProgressUpdates() {
        progressJob?.cancel()
        progressJob = viewModelScope.launch {
            while (isActive) {
                val p = controller
                if (p != null && p.isPlaying) {
                    val currentPos = p.currentPosition.coerceAtLeast(0L)
                    val dur = p.duration.coerceAtLeast(0L)
                    _uiState.update {
                        it.copy(
                            currentPositionMs = currentPos,
                            durationMs = if (dur > 0) dur else it.durationMs
                        )
                    }
                }
                delay(250)
            }
        }
    }

    fun loadAndPlay(input: String) {
        if (input.isBlank() || input == "link youtube..." || input == "linkYT") {
            stopAndClear()
            return
        }

        viewModelScope.launch {
            val trimmed = input.trim()
            val listId = YouTubeRepository.extractPlaylistId(trimmed)
            val videoId = YouTubeRepository.extractVideoId(trimmed)

            // 1. If a single video ID is found in the URL/input (even if it has &list=RD... or &list=PL...)
            if (!videoId.isNullOrBlank()) {
                // Immediately start playing the video so the user never has to wait!
                playVideoById(videoId)

                // If it also has a playlist or mix ID, load the playlist in the background to fill the drawer
                if (!listId.isNullOrBlank()) {
                    loadPlaylistBackground(trimmed, videoId)
                }
                return@launch
            }

            // 2. If a pure playlist URL was provided (without a specific video ID)
            if (!listId.isNullOrBlank()) {
                loadPlaylist(trimmed, null)
                return@launch
            }

            // 3. Otherwise treat as search query
            searchAndPlayTop(trimmed)
        }
    }

    private fun loadPlaylistBackground(playlistUrl: String, playingVideoId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isPlaylistLoading = true) }
            val result = youTubeRepository.resolvePlaylist(playlistUrl, playingVideoId)
            result.onSuccess { items ->
                if (items.isNotEmpty()) {
                    val targetIndex = items.indexOfFirst { it.id.equals(playingVideoId, ignoreCase = true) }.coerceAtLeast(0)
                    _uiState.update {
                        it.copy(
                            playlist = items,
                            isPlaylistLoading = false,
                            currentPlaylistId = YouTubeRepository.extractPlaylistId(playlistUrl),
                            currentPlaylistIndex = targetIndex
                        )
                    }
                } else {
                    _uiState.update { it.copy(isPlaylistLoading = false) }
                }
            }.onFailure {
                _uiState.update { it.copy(isPlaylistLoading = false) }
            }
        }
    }

    private fun loadPlaylist(playlistUrl: String, initialVideoId: String? = null) {
        viewModelScope.launch {
            _uiState.update { it.copy(isPlaylistLoading = true, statusMessage = "Đang tải danh sách phát...") }
            val result = youTubeRepository.resolvePlaylist(playlistUrl, initialVideoId)
            result.onSuccess { items ->
                _uiState.update {
                    it.copy(
                        playlist = items,
                        isPlaylistLoading = false,
                        currentPlaylistId = YouTubeRepository.extractPlaylistId(playlistUrl)
                    )
                }
                if (items.isNotEmpty()) {
                    val targetIndex = if (!initialVideoId.isNullOrBlank()) {
                        items.indexOfFirst { it.id.equals(initialVideoId, ignoreCase = true) }.coerceAtLeast(0)
                    } else 0
                    playTrackAtIndex(targetIndex)
                } else {
                    _uiState.update {
                        it.copy(
                            isPlaylistLoading = false,
                            statusMessage = "Không tìm thấy bài hát trong danh sách phát"
                        )
                    }
                    if (!initialVideoId.isNullOrBlank()) {
                        playVideoById(initialVideoId)
                    }
                }
            }.onFailure { err ->
                _uiState.update {
                    it.copy(
                        isPlaylistLoading = false,
                        statusMessage = "Lỗi tải playlist: ${err.message}"
                    )
                }
                if (!initialVideoId.isNullOrBlank()) {
                    playVideoById(initialVideoId)
                }
            }
        }
    }

    fun playTrackAtIndex(index: Int) {
        val playlist = _uiState.value.playlist
        if (index in playlist.indices) {
            val item = playlist[index]
            _uiState.update { it.copy(currentPlaylistIndex = index) }
            playTrack(item)
        }
    }

    fun playTrack(item: PlaylistItem) {
        viewModelScope.launch {
            _uiState.update {
                it.copy(
                    currentTrack = item,
                    statusMessage = "Đang nạp âm thanh...",
                    status = PlaybackStatus.BUFFERING
                )
            }

            val result = youTubeRepository.resolveAudio(item.id)
            result.onSuccess { stream ->
                val resolvedTrack = item.copy(
                    title = if (item.title.isBlank() || item.title == "Đang tải..." || item.title.startsWith("Bài ")) {
                        stream.title.ifBlank { item.title }
                    } else {
                        item.title
                    },
                    artist = if (item.artist.isBlank() || item.artist == "Đang tải...") stream.artist else item.artist,
                    durationSeconds = if (item.durationSeconds <= 0) stream.durationSeconds else item.durationSeconds
                )

                _uiState.update {
                    it.copy(
                        currentTrack = resolvedTrack,
                        durationMs = resolvedTrack.durationSeconds * 1000L
                    )
                }

                // Add to persistent history
                historyRepository.addTrack(resolvedTrack)

                // Play in ExoPlayer
                playStreamUrl(stream.audioUrl, resolvedTrack, stream.artworkBytes)
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        status = PlaybackStatus.ERROR,
                        statusMessage = "Lỗi tải: ${error.message ?: "Không lấy được audio"}"
                    )
                }
            }
        }
    }

    private fun playVideoById(videoId: String) {
        val existingTitle = historyRepository.getTrackTitle(videoId)
        val tempItem = PlaylistItem(
            id = videoId,
            title = existingTitle ?: "Đang tải...",
            durationSeconds = 0
        )
        playTrack(tempItem)
    }

    private fun searchAndPlayTop(query: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSearching = true, statusMessage = "Đang tìm bài hát...") }
            val result = youTubeRepository.search(query)
            result.onSuccess { items ->
                _uiState.update {
                    it.copy(
                        searchResults = items,
                        isSearching = false
                    )
                }
                if (items.isNotEmpty()) {
                    // Update playlist queue with search results and play first
                    _uiState.update { it.copy(playlist = items, currentPlaylistIndex = 0) }
                    playTrack(items[0])
                } else {
                    _uiState.update { it.copy(statusMessage = "Không tìm thấy kết quả") }
                }
            }.onFailure { err ->
                _uiState.update {
                    it.copy(
                        isSearching = false,
                        statusMessage = "Lỗi tìm kiếm: ${err.message}"
                    )
                }
            }
        }
    }

    fun searchOnly(query: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSearching = true) }
            val result = youTubeRepository.search(query)
            result.onSuccess { items ->
                _uiState.update { it.copy(searchResults = items, isSearching = false) }
            }.onFailure {
                _uiState.update { it.copy(isSearching = false) }
            }
        }
    }

    private fun playStreamUrl(audioUrl: String, track: PlaylistItem, artworkBytes: ByteArray? = null) {
        val p = controller
        if (p == null) {
            pendingMediaItem = Pair(audioUrl, track)
            return
        }
        val mediaMetadata = MediaMetadata.Builder()
            .setTitle(track.title)
            .setArtist(track.artist)
            .setArtworkUri(Uri.parse(track.thumbnailUrl))
            .apply {
                if (artworkBytes != null && artworkBytes.isNotEmpty()) {
                    setArtworkData(artworkBytes, MediaMetadata.PICTURE_TYPE_FRONT_COVER)
                }
            }
            .build()

        val mediaItem = MediaItem.Builder()
            .setUri(audioUrl)
            .setMediaId(track.id)
            .setMediaMetadata(mediaMetadata)
            .build()

        p.setMediaItem(mediaItem)
        p.prepare()
        p.play()
    }

    fun togglePlayPause() {
        val p = controller ?: return
        if (p.isPlaying) {
            p.pause()
        } else {
            p.play()
        }
    }

    fun playNext() {
        val state = _uiState.value
        val playlist = state.playlist
        if (playlist.isEmpty()) return

        val nextIndex = state.currentPlaylistIndex + 1
        if (nextIndex < playlist.size) {
            playTrackAtIndex(nextIndex)
        } else if (state.loopMode == LoopMode.ALL) {
            playTrackAtIndex(0)
        }
    }

    fun playPrevious() {
        val p = controller
        if (p != null && p.currentPosition > 3000L) {
            p.seekTo(0L)
            return
        }

        val state = _uiState.value
        val playlist = state.playlist
        if (playlist.isEmpty()) {
            p?.seekTo(0L)
            return
        }

        val prevIndex = state.currentPlaylistIndex - 1
        if (prevIndex >= 0) {
            playTrackAtIndex(prevIndex)
        } else if (state.loopMode == LoopMode.ALL) {
            playTrackAtIndex(playlist.size - 1)
        } else {
            p?.seekTo(0L)
        }
    }

    fun seekTo(progress: Float) {
        val p = controller ?: return
        val dur = p.duration
        if (dur > 0) {
            val targetMs = (dur * progress).toLong().coerceIn(0L, dur)
            p.seekTo(targetMs)
            _uiState.update { it.copy(currentPositionMs = targetMs) }
        }
    }

    fun toggleLoopMode() {
        val nextMode = when (_uiState.value.loopMode) {
            LoopMode.OFF -> LoopMode.ONE
            LoopMode.ONE -> LoopMode.ALL
            LoopMode.ALL -> LoopMode.OFF
        }
        _uiState.update { it.copy(loopMode = nextMode) }

        val p = controller ?: return
        when (nextMode) {
            LoopMode.OFF -> p.repeatMode = Player.REPEAT_MODE_OFF
            LoopMode.ONE -> p.repeatMode = Player.REPEAT_MODE_ONE
            LoopMode.ALL -> p.repeatMode = Player.REPEAT_MODE_ALL
        }
    }

    fun setVolume(volume: Float) {
        val clamped = volume.coerceIn(0f, 1f)
        _uiState.update { it.copy(volume = clamped) }
        controller?.volume = clamped
    }

    fun stopAndClear() {
        controller?.stop()
        controller?.clearMediaItems()
        _uiState.update {
            it.copy(
                currentTrack = null,
                status = PlaybackStatus.IDLE,
                currentPositionMs = 0L,
                durationMs = 0L,
                statusMessage = "Sẵn sàng",
                playlist = emptyList(),
                currentPlaylistIndex = -1
            )
        }
    }

    fun clearPlaylist() {
        _uiState.update {
            it.copy(
                playlist = emptyList(),
                currentPlaylistIndex = -1,
                currentPlaylistId = null
            )
        }
    }

    fun addToPlaylist(item: PlaylistItem): Boolean {
        val currentList = _uiState.value.playlist.toMutableList()
        val exists = currentList.any { it.id.equals(item.id, ignoreCase = true) }
        if (!exists) {
            currentList.add(item)
            val newIndex = if (_uiState.value.currentPlaylistIndex < 0 && _uiState.value.currentTrack != null) {
                if (_uiState.value.currentTrack?.id.equals(item.id, ignoreCase = true)) currentList.size - 1 else -1
            } else {
                _uiState.value.currentPlaylistIndex
            }
            _uiState.update { it.copy(playlist = currentList, currentPlaylistIndex = newIndex) }
            return true
        }
        return false
    }

    fun removeFromPlaylist(index: Int) {
        val currentList = _uiState.value.playlist.toMutableList()
        if (index in currentList.indices) {
            currentList.removeAt(index)
            val currentIndex = _uiState.value.currentPlaylistIndex
            val newIndex = when {
                currentList.isEmpty() -> -1
                currentIndex >= currentList.size -> currentList.size - 1
                currentIndex > index -> currentIndex - 1
                else -> currentIndex
            }
            _uiState.update { it.copy(playlist = currentList, currentPlaylistIndex = newIndex) }
        }
    }

    fun clearHistory() {
        viewModelScope.launch {
            historyRepository.clearHistory()
        }
    }

    fun removeHistoryItem(id: String) {
        viewModelScope.launch {
            historyRepository.removeTrack(id)
        }
    }

    private fun handleTrackEnded() {
        val state = _uiState.value
        when (state.loopMode) {
            LoopMode.ONE -> {
                controller?.seekTo(0L)
                controller?.play()
            }
            LoopMode.ALL -> {
                playNext()
            }
            LoopMode.OFF -> {
                if (state.currentPlaylistIndex + 1 < state.playlist.size) {
                    playNext()
                }
            }
        }
    }

    override fun onCleared() {
        progressJob?.cancel()
        controller?.release()
        controllerFuture?.let { MediaController.releaseFuture(it) }
        controller = null
        super.onCleared()
    }
}

