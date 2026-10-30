package com.kahoii.music.viewmodel

import android.app.Application
import android.content.ComponentName
import android.net.Uri
import android.widget.Toast
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
import com.kahoii.music.data.SavedTracksRepository
import com.kahoii.music.data.YouTubeRepository
import com.kahoii.music.model.ActiveQueueSource
import com.kahoii.music.model.LoopMode
import com.kahoii.music.model.PlaybackStatus
import com.kahoii.music.model.PlayerUiState
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.model.SleepTimerOption
import com.kahoii.music.service.MusicPlaybackService
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.File

class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val youTubeRepository = YouTubeRepository()
    private val historyRepository = HistoryRepository(application)
    private val savedTracksRepository = SavedTracksRepository(application)

    private val _uiState = MutableStateFlow(PlayerUiState())
    val uiState: StateFlow<PlayerUiState> = _uiState.asStateFlow()

    private var controllerFuture: ListenableFuture<MediaController>? = null
    private var controller: MediaController? = null
    private var progressJob: Job? = null
    private var sleepTimerJob: Job? = null
    private var pendingMediaItem: Pair<String, PlaylistItem>? = null

    init {
        initMediaController()
        observeHistory()
        observeSavedTracks()
        observeFavorites()
        com.kahoii.music.service.MusicQueueManager.onNextAction = { playNext() }
        com.kahoii.music.service.MusicQueueManager.onPreviousAction = { playPrevious() }
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

    private fun observeSavedTracks() {
        viewModelScope.launch {
            savedTracksRepository.savedTracks.collect { savedList ->
                _uiState.update { it.copy(savedTracks = savedList) }
            }
        }
    }

    private fun observeFavorites() {
        viewModelScope.launch {
            historyRepository.favorites.collect { favList ->
                _uiState.update { it.copy(favorites = favList) }
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

    fun isTrackSaved(id: String): Boolean {
        return savedTracksRepository.isTrackSaved(id)
    }

    fun isTrackDownloading(id: String): Boolean {
        return _uiState.value.downloadingTrackIds.contains(id)
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

            // 1. If a single video ID is found
            if (!videoId.isNullOrBlank()) {
                playVideoById(videoId)

                if (!listId.isNullOrBlank()) {
                    loadPlaylistBackground(trimmed, videoId)
                }
                return@launch
            }

            // 2. If a pure playlist URL was provided
            if (!listId.isNullOrBlank()) {
                loadPlaylist(trimmed, null)
                return@launch
            }

            // 3. Otherwise search query
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
                            currentPlaylistIndex = targetIndex,
                            activeQueueSource = ActiveQueueSource.PLAYLIST
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
                        currentPlaylistId = YouTubeRepository.extractPlaylistId(playlistUrl),
                        activeQueueSource = ActiveQueueSource.PLAYLIST
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
            _uiState.update {
                it.copy(
                    currentPlaylistIndex = index,
                    activeQueueSource = ActiveQueueSource.PLAYLIST
                )
            }
            playTrack(item)
        }
    }

    fun playFromQueueIndex(index: Int, source: ActiveQueueSource) {
        val queue = when (source) {
            ActiveQueueSource.PLAYLIST -> _uiState.value.playlist
            ActiveQueueSource.SAVED -> _uiState.value.savedTracks
            ActiveQueueSource.FAVORITES -> _uiState.value.favorites
            ActiveQueueSource.HISTORY -> _uiState.value.history
        }
        if (index in queue.indices) {
            val item = queue[index]
            _uiState.update {
                it.copy(
                    activeQueueSource = source,
                    currentPlaylistIndex = index
                )
            }
            playTrack(item)
        }
    }

    fun playFromQueue(item: PlaylistItem, source: ActiveQueueSource) {
        val queue = when (source) {
            ActiveQueueSource.PLAYLIST -> _uiState.value.playlist
            ActiveQueueSource.SAVED -> _uiState.value.savedTracks
            ActiveQueueSource.FAVORITES -> _uiState.value.favorites
            ActiveQueueSource.HISTORY -> _uiState.value.history
        }
        val targetIndex = queue.indexOfFirst { it.id.equals(item.id, ignoreCase = true) }
        _uiState.update {
            it.copy(
                activeQueueSource = source,
                currentPlaylistIndex = if (targetIndex >= 0) targetIndex else 0
            )
        }
        playTrack(item)
    }

    fun playTrack(item: PlaylistItem) {
        viewModelScope.launch {
            // Check if track is saved offline first -> Instant playback with 0 delay!
            val savedTrack = savedTracksRepository.getSavedTrack(item.id)
            if (savedTrack != null && savedTrack.isOffline) {
                _uiState.update {
                    it.copy(
                        currentTrack = savedTrack,
                        durationMs = savedTrack.durationSeconds * 1000L,
                        statusMessage = "Đang phát (Offline)",
                        status = PlaybackStatus.BUFFERING
                    )
                }
                historyRepository.addTrack(savedTrack)
                val fileUri = Uri.fromFile(File(savedTrack.localAudioPath!!)).toString()
                playStreamUrl(fileUri, savedTrack)
                return@launch
            }

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
        val existingTitle = savedTracksRepository.getSavedTrack(videoId)?.title
            ?: historyRepository.getTrackTitle(videoId)
        val tempItem = PlaylistItem(
            id = videoId,
            title = existingTitle ?: "Đang tải...",
            durationSeconds = 0
        )
        _uiState.update {
            it.copy(
                playlist = listOf(tempItem),
                currentPlaylistIndex = 0,
                activeQueueSource = ActiveQueueSource.PLAYLIST
            )
        }
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
                    _uiState.update {
                        it.copy(
                            playlist = items,
                            currentPlaylistIndex = 0,
                            activeQueueSource = ActiveQueueSource.PLAYLIST
                        )
                    }
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

    fun toggleShuffle() {
        val newShuffle = !_uiState.value.isShuffled
        _uiState.update { it.copy(isShuffled = newShuffle) }
        val msg = if (newShuffle) "Đã bật trộn bài (Shuffle On)" else "Đã tắt trộn bài (Shuffle Off)"
        Toast.makeText(getApplication(), msg, Toast.LENGTH_SHORT).show()
    }

    fun toggleFavorite(track: PlaylistItem? = _uiState.value.currentTrack) {
        val target = track ?: _uiState.value.currentTrack ?: return
        if (target.id.isBlank()) return
        viewModelScope.launch {
            val isFav = historyRepository.toggleFavorite(target)
            val msg = if (isFav) "Đã thêm vào Yêu thích ❤️" else "Đã xóa khỏi Yêu thích"
            Toast.makeText(getApplication(), msg, Toast.LENGTH_SHORT).show()
        }
    }

    fun playAllFavorites() {
        val favs = _uiState.value.favorites
        if (favs.isNotEmpty()) {
            _uiState.update { it.copy(activeQueueSource = ActiveQueueSource.FAVORITES) }
            playFromQueueIndex(0, ActiveQueueSource.FAVORITES)
            Toast.makeText(getApplication(), "Đang phát toàn bộ mục Yêu thích (${favs.size} bài)", Toast.LENGTH_SHORT).show()
        } else {
            Toast.makeText(getApplication(), "Chưa có bài hát nào trong mục Yêu thích", Toast.LENGTH_SHORT).show()
        }
    }

    fun playNext() {
        val state = _uiState.value
        val queue = state.activeQueue
        val queueName = state.activeQueueSource.label
        if (queue.isEmpty()) {
            controller?.seekTo(0L)
            Toast.makeText(getApplication(), "Mục $queueName chưa có bài hát", Toast.LENGTH_SHORT).show()
            return
        }

        if (queue.size == 1) {
            controller?.seekTo(0L)
            controller?.play()
            Toast.makeText(getApplication(), "Mục $queueName chỉ có 1 bài hát", Toast.LENGTH_SHORT).show()
            return
        }

        val currentId = state.currentTrack?.id
        val currentIndex = if (!currentId.isNullOrBlank()) {
            queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
        } else -1

        val nextIndex = if (state.isShuffled && queue.size > 1) {
            val otherIndices = queue.indices.filter { it != currentIndex }
            if (otherIndices.isNotEmpty()) otherIndices.random() else 0
        } else {
            if (currentIndex >= 0) {
                (currentIndex + 1) % queue.size
            } else {
                0
            }
        }

        val nextTrack = queue[nextIndex]
        _uiState.update { it.copy(currentPlaylistIndex = nextIndex) }
        playTrack(nextTrack)

        val msg = "[$queueName ${nextIndex + 1}/${queue.size}] ${nextTrack.title}"
        Toast.makeText(getApplication(), msg, Toast.LENGTH_SHORT).show()
    }

    fun playPrevious() {
        val p = controller
        if (p != null && p.currentPosition > 3000L) {
            p.seekTo(0L)
            return
        }

        val state = _uiState.value
        val queue = state.activeQueue
        val queueName = state.activeQueueSource.label
        if (queue.isEmpty()) {
            p?.seekTo(0L)
            Toast.makeText(getApplication(), "Mục $queueName chưa có bài hát", Toast.LENGTH_SHORT).show()
            return
        }

        if (queue.size == 1) {
            p?.seekTo(0L)
            p?.play()
            Toast.makeText(getApplication(), "Mục $queueName chỉ có 1 bài hát", Toast.LENGTH_SHORT).show()
            return
        }

        val currentId = state.currentTrack?.id
        val currentIndex = if (!currentId.isNullOrBlank()) {
            queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
        } else 0

        val prevIndex = if (currentIndex > 0) {
            currentIndex - 1
        } else {
            queue.size - 1
        }

        val prevTrack = queue[prevIndex]
        _uiState.update { it.copy(currentPlaylistIndex = prevIndex) }
        playTrack(prevTrack)

        val msg = "[$queueName ${prevIndex + 1}/${queue.size}] ${prevTrack.title}"
        Toast.makeText(getApplication(), msg, Toast.LENGTH_SHORT).show()
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
        _uiState.update { it.copy(volume = clamped, isMuted = clamped <= 0.001f) }
        controller?.volume = clamped
    }

    fun toggleMute() {
        val currentVol = _uiState.value.volume
        if (currentVol > 0.001f) {
            _uiState.update { it.copy(preMuteVolume = currentVol) }
            setVolume(0f)
        } else {
            val target = if (_uiState.value.preMuteVolume > 0.05f) _uiState.value.preMuteVolume else 0.5f
            setVolume(target)
        }
    }

    // ================= SLEEP TIMER =================
    fun setSleepTimer(option: SleepTimerOption) {
        sleepTimerJob?.cancel()
        _uiState.update { it.copy(sleepTimerOption = option) }

        if (option == SleepTimerOption.OFF || option == SleepTimerOption.END_OF_TRACK) {
            _uiState.update { it.copy(sleepTimerRemainingSeconds = 0L) }
            return
        }

        val totalSeconds = option.minutes * 60L
        _uiState.update { it.copy(sleepTimerRemainingSeconds = totalSeconds) }

        sleepTimerJob = viewModelScope.launch {
            var remaining = totalSeconds
            while (remaining > 0 && isActive) {
                delay(1000)
                remaining--
                _uiState.update { it.copy(sleepTimerRemainingSeconds = remaining) }
            }

            if (remaining <= 0) {
                controller?.pause()
                _uiState.update {
                    it.copy(
                        sleepTimerOption = SleepTimerOption.OFF,
                        sleepTimerRemainingSeconds = 0L,
                        statusMessage = "Hẹn giờ đã kết thúc - Đã tạm dừng nhạc"
                    )
                }
            }
        }
    }

    // ================= OFFLINE SAVED TRACKS =================
    fun saveTrackOffline(item: PlaylistItem) {
        if (item.id.isBlank()) return
        if (savedTracksRepository.isTrackSaved(item.id)) {
            Toast.makeText(getApplication(), "Bài hát đã được lưu trong mục Đã tải", Toast.LENGTH_SHORT).show()
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(downloadingTrackIds = it.downloadingTrackIds + item.id) }
            Toast.makeText(getApplication(), "Đang tải bài hát về máy: ${item.title}...", Toast.LENGTH_SHORT).show()

            // Resolve audio stream if needed
            val streamResult = youTubeRepository.resolveAudio(item.id)
            streamResult.onSuccess { stream ->
                val fullItem = item.copy(
                    title = if (item.title.isBlank() || item.title == "Đang tải...") stream.title else item.title,
                    artist = if (item.artist.isBlank()) stream.artist else item.artist,
                    durationSeconds = if (item.durationSeconds <= 0) stream.durationSeconds else item.durationSeconds
                )

                val saveResult = savedTracksRepository.saveTrack(fullItem, stream.audioUrl, stream.thumbnailUrl)
                saveResult.onSuccess {
                    _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds - item.id) }
                    Toast.makeText(getApplication(), "✓ Đã lưu bài hát vào mục Đã tải!", Toast.LENGTH_SHORT).show()
                }.onFailure { err ->
                    _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds - item.id) }
                    Toast.makeText(getApplication(), "Lỗi tải tệp: ${err.message}", Toast.LENGTH_LONG).show()
                }
            }.onFailure { err ->
                _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds - item.id) }
                Toast.makeText(getApplication(), "Lỗi phân giải luồng tải: ${err.message}", Toast.LENGTH_LONG).show()
            }
        }
    }

    fun deleteSavedTrack(id: String) {
        viewModelScope.launch {
            val success = savedTracksRepository.deleteTrack(id)
            if (success) {
                Toast.makeText(getApplication(), "Đã xóa bài hát khỏi mục Đã tải", Toast.LENGTH_SHORT).show()
            }
        }
    }

    fun playAllSavedTracks() {
        val saved = _uiState.value.savedTracks
        if (saved.isNotEmpty()) {
            _uiState.update { it.copy(activeQueueSource = ActiveQueueSource.SAVED) }
            playFromQueueIndex(0, ActiveQueueSource.SAVED)
            Toast.makeText(getApplication(), "Đang phát toàn bộ mục Đã tải (${saved.size} bài)", Toast.LENGTH_SHORT).show()
        } else {
            Toast.makeText(getApplication(), "Chưa có bài hát nào trong mục Đã tải", Toast.LENGTH_SHORT).show()
        }
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

        // Check Sleep Timer End of Track
        if (state.sleepTimerOption == SleepTimerOption.END_OF_TRACK) {
            controller?.pause()
            _uiState.update {
                it.copy(
                    sleepTimerOption = SleepTimerOption.OFF,
                    statusMessage = "Hết bài hát - Đã dừng hẹn giờ"
                )
            }
            return
        }

        when (state.loopMode) {
            LoopMode.ONE -> {
                controller?.seekTo(0L)
                controller?.play()
            }
            LoopMode.ALL -> {
                playNext()
            }
            LoopMode.OFF -> {
                val queue = state.activeQueue
                val currentId = state.currentTrack?.id
                val currentIndex = if (!currentId.isNullOrBlank()) {
                    queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
                } else -1

                if (currentIndex >= 0 && currentIndex + 1 < queue.size) {
                    playNext()
                } else {
                    controller?.pause()
                    _uiState.update {
                        it.copy(status = PlaybackStatus.PAUSED, statusMessage = "Đã phát hết danh sách")
                    }
                }
            }
        }
    }

    override fun onCleared() {
        com.kahoii.music.service.MusicQueueManager.onNextAction = null
        com.kahoii.music.service.MusicQueueManager.onPreviousAction = null
        progressJob?.cancel()
        sleepTimerJob?.cancel()
        controller?.release()
        controllerFuture?.let { MediaController.releaseFuture(it) }
        controller = null
        super.onCleared()
    }
}
