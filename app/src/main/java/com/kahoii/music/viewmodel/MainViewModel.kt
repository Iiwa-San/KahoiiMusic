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
import com.kahoii.music.BuildConfig
import com.kahoii.music.data.CloudMusicPayload
import com.kahoii.music.data.FirebaseAuthRepository
import com.kahoii.music.data.FirebaseSyncRepository
import com.kahoii.music.data.FirebaseUser
import com.kahoii.music.data.HistoryRepository
import com.kahoii.music.data.LyricsRepository
import com.kahoii.music.data.PlaylistCollectionRepository
import com.kahoii.music.data.SavedTracksRepository
import com.kahoii.music.data.YouTubeRepository
import com.kahoii.music.model.LyricsData
import com.kahoii.music.util.AppUpdateManager
import com.kahoii.music.util.NetworkUtils
import com.kahoii.music.model.ActiveQueueSource
import com.kahoii.music.model.AppUpdateInfo
import com.kahoii.music.model.LoopMode
import com.kahoii.music.model.PlaybackStatus
import com.kahoii.music.model.PlayerUiState
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.model.RomanizationDisplayMode
import com.kahoii.music.model.SleepTimerOption
import com.kahoii.music.model.UpdateDownloadState
import com.kahoii.music.model.UserPlaylist
import com.kahoii.music.service.MusicPlaybackService
import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
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
    private val playlistCollectionRepository = PlaylistCollectionRepository(application)
    private val authRepository = FirebaseAuthRepository(application)
    private val syncRepository = FirebaseSyncRepository(authRepository)
    private val lyricsRepository = LyricsRepository(application)
    private val appPrefs = application.getSharedPreferences("kahoii_prefs", Context.MODE_PRIVATE)
    private var syncJob: Job? = null
    private var lyricsJob: Job? = null
    private var isApplyingCloudPayload = true

    private val _uiState = MutableStateFlow(PlayerUiState())
    val uiState: StateFlow<PlayerUiState> = _uiState.asStateFlow()

    private var controllerFuture: ListenableFuture<MediaController>? = null
    private var controller: MediaController? = null
    private var progressJob: Job? = null
    private var sleepTimerJob: Job? = null
    private var pendingMediaItem: Pair<String, PlaylistItem>? = null

    init {
        val savedRomMode = appPrefs.getInt("lyrics_romanization_mode", RomanizationDisplayMode.DUAL.value)
        _uiState.update { it.copy(romanizationMode = RomanizationDisplayMode.fromValue(savedRomMode)) }

        historyRepository.isLoggedIn = { authRepository.currentUser.value != null }
        initMediaController()
        observeHistory()
        observeSavedTracks()
        observeFavorites()
        observePlaylists()
        observeAuth()
        checkAppUpdate(isManual = false)
        com.kahoii.music.service.MusicQueueManager.onTrackChanged = { track ->
            _uiState.update { it.copy(currentTrack = track) }
            if (track != null) {
                if (!LyricsRepository.isPlaceholderTitle(track.title)) {
                    loadLyricsForTrack(track.id, track.safeTitle, track.safeArtist, track.durationSeconds)
                } else {
                    _uiState.update { it.copy(lyricsData = null, isLyricsLoading = true, currentLyricIndex = -1) }
                }
            } else {
                _uiState.update { it.copy(lyricsData = null, isLyricsLoading = false, currentLyricIndex = -1) }
            }
        }
        com.kahoii.music.service.MusicQueueManager.onTrackResolved = { resolvedTrack ->
            _uiState.update { state ->
                val updatedPlaylist = state.playlist.map { pItem ->
                    if (pItem.id.equals(resolvedTrack.id, ignoreCase = true)) {
                        pItem.copy(
                            title = resolvedTrack.title,
                            artist = resolvedTrack.artist,
                            durationSeconds = resolvedTrack.durationSeconds
                        )
                    } else pItem
                }
                state.copy(
                    currentTrack = resolvedTrack,
                    playlist = updatedPlaylist,
                    durationMs = resolvedTrack.durationSeconds * 1000L
                )
            }
            loadLyricsForTrack(resolvedTrack.id, resolvedTrack.safeTitle, resolvedTrack.safeArtist, resolvedTrack.durationSeconds)
        }
        com.kahoii.music.service.MusicQueueManager.onPlaylistIndexChanged = { idx ->
            _uiState.update { it.copy(currentPlaylistIndex = idx) }
        }
        com.kahoii.music.service.MusicQueueManager.onStatusMessage = { msg ->
            _uiState.update { it.copy(statusMessage = msg) }
        }
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
            com.kahoii.music.service.MusicQueueManager.currentTrack?.let { cur ->
                _uiState.update { it.copy(currentTrack = cur, currentPlaylistIndex = com.kahoii.music.service.MusicQueueManager.currentIndex) }
                loadLyricsForTrack(cur.id, cur.safeTitle, cur.safeArtist, cur.durationSeconds)
            }
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
                        val durationSec = (duration / 1000L).toInt()

                        _uiState.update { state ->
                            val cur = state.currentTrack
                            val updatedTrack = if (cur != null && cur.durationSeconds <= 0 && durationSec > 0) {
                                cur.copy(durationSeconds = durationSec)
                            } else cur
                            val updatedPlaylist = state.playlist.map { pItem ->
                                if (cur != null && pItem.id.equals(cur.id, ignoreCase = true) && pItem.durationSeconds <= 0 && durationSec > 0) {
                                    pItem.copy(durationSeconds = durationSec)
                                } else pItem
                            }
                            state.copy(
                                status = newStatus,
                                durationMs = duration,
                                currentTrack = updatedTrack,
                                playlist = updatedPlaylist,
                                statusMessage = if (isPlaying) "Đang phát" else "Tạm dừng"
                            )
                        }
                        if (durationSec > 0) {
                            _uiState.value.currentTrack?.let { trackToUpdate ->
                                viewModelScope.launch(Dispatchers.IO) {
                                    historyRepository.updateTrackInfoOnly(trackToUpdate)
                                }
                            }
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
                if (!isApplyingCloudPayload && _uiState.value.currentUser != null) {
                    triggerCloudUpload()
                }
            }
        }
    }

    private fun observeSavedTracks() {
        viewModelScope.launch {
            savedTracksRepository.savedTracks.collect { savedList ->
                _uiState.update { it.copy(savedTracks = savedList) }
                if (!isApplyingCloudPayload && _uiState.value.currentUser != null) {
                    triggerCloudUpload()
                }
            }
        }
    }

    private fun observeFavorites() {
        viewModelScope.launch {
            historyRepository.favorites.collect { favList ->
                _uiState.update { it.copy(favorites = favList) }
                if (!isApplyingCloudPayload && _uiState.value.currentUser != null) {
                    triggerCloudUpload()
                }
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
                    val lyrics = _uiState.value.lyricsData
                    val newIndex = if (lyrics != null && lyrics.isSynced && lyrics.lines.isNotEmpty()) {
                        val targetMs = currentPos + 120L
                        var found = -1
                        for (i in lyrics.lines.indices) {
                            if (lyrics.lines[i].timestampMs <= targetMs) {
                                found = i
                            } else {
                                break
                            }
                        }
                        found
                    } else -1

                    _uiState.update {
                        it.copy(
                            currentPositionMs = currentPos,
                            durationMs = if (dur > 0) dur else it.durationMs,
                            currentLyricIndex = newIndex
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

            // If a playlist is specified in URL (pure playlist or video+playlist)
            if (!listId.isNullOrBlank()) {
                val isCurrentlyPlaying = _uiState.value.status == PlaybackStatus.PLAYING || _uiState.value.currentTrack != null
                if (isCurrentlyPlaying) {
                    // Do NOT interrupt the current song!
                    // Clear old playlist from queue and load new playlist in background.
                    // The current song continues playing and is kept in history.
                    loadPlaylistWithoutInterrupting(trimmed, listId)
                    return@launch
                }

                // If not playing, load and play
                if (!videoId.isNullOrBlank()) {
                    playVideoById(videoId)
                    loadPlaylistBackground(trimmed, videoId)
                } else {
                    loadPlaylist(trimmed, null)
                }
                return@launch
            }

            // 1. If a single video ID is found
            if (!videoId.isNullOrBlank()) {
                val isCurrentlyPlaying = _uiState.value.status == PlaybackStatus.PLAYING || _uiState.value.currentTrack != null
                if (isCurrentlyPlaying) {
                    loadSingleTrackWithoutInterrupting(videoId)
                    return@launch
                }
                playVideoById(videoId)
                return@launch
            }

            // 3. Otherwise search query
            searchAndPlayTop(trimmed)
        }
    }

    private fun loadSingleTrackWithoutInterrupting(videoId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isPlaylistLoading = true, statusMessage = "Đang nạp bài hát vào danh sách phát...") }
            val current = _uiState.value.currentTrack
            current?.let { historyRepository.addTrack(it) }

            val streamRes = youTubeRepository.resolveAudio(videoId)
            val title = streamRes.getOrNull()?.title ?: "Bài hát"
            val artist = streamRes.getOrNull()?.artist ?: ""
            val duration = streamRes.getOrNull()?.durationSeconds ?: 0

            val singleItem = PlaylistItem(
                id = videoId,
                title = title,
                artist = artist,
                durationSeconds = duration
            )
            val items = listOf(singleItem)

            _uiState.update {
                it.copy(
                    playlist = items,
                    isPlaylistLoading = false,
                    currentPlaylistId = null,
                    currentPlaylistIndex = -1,
                    activeQueueSource = ActiveQueueSource.PLAYLIST,
                    statusMessage = "Đã nạp 1 bài vào danh sách phát"
                )
            }

            com.kahoii.music.service.MusicQueueManager.activeQueue = items
            com.kahoii.music.service.MusicQueueManager.activeQueueSource = ActiveQueueSource.PLAYLIST
            com.kahoii.music.service.MusicQueueManager.currentIndex = -1

            streamRes.onSuccess { stream ->
                savedTracksRepository.cacheTempAudio(videoId, stream.audioUrl, stream.contentLength)
            }
            cleanUnusedTempAudio()
        }
    }

    private fun loadPlaylistWithoutInterrupting(playlistUrl: String, listId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isPlaylistLoading = true, statusMessage = "Đang nạp danh sách mới...") }
            val current = _uiState.value.currentTrack
            val currentId = current?.id

            // Ensure current track is kept in history
            current?.let { historyRepository.addTrack(it) }

            val targetVideoId = YouTubeRepository.extractVideoId(playlistUrl)
            val result = youTubeRepository.resolvePlaylist(playlistUrl, targetVideoId)
            result.onSuccess { items ->
                if (items.isNotEmpty()) {
                    val targetIndex = if (!currentId.isNullOrBlank()) {
                        items.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
                    } else -1

                    _uiState.update {
                        it.copy(
                            playlist = items,
                            isPlaylistLoading = false,
                            currentPlaylistId = listId,
                            currentPlaylistIndex = if (targetIndex >= 0) targetIndex else -1,
                            activeQueueSource = ActiveQueueSource.PLAYLIST,
                            statusMessage = "Đã nạp ${items.size} bài vào danh sách phát"
                        )
                    }

                    // Update activeQueue in MusicQueueManager to new playlist
                    com.kahoii.music.service.MusicQueueManager.activeQueue = items
                    com.kahoii.music.service.MusicQueueManager.activeQueueSource = ActiveQueueSource.PLAYLIST
                    com.kahoii.music.service.MusicQueueManager.currentIndex = if (targetIndex >= 0) targetIndex else -1

                    // Preload ONLY the 1 next track ahead
                    val nextTrackToPreload = if (targetIndex >= 0 && targetIndex + 1 < items.size) {
                        items[targetIndex + 1]
                    } else {
                        items[0]
                    }
                    viewModelScope.launch(Dispatchers.IO) {
                        val streamRes = youTubeRepository.resolveAudio(nextTrackToPreload.id)
                        streamRes.onSuccess { stream ->
                            savedTracksRepository.cacheTempAudio(nextTrackToPreload.id, stream.audioUrl, stream.contentLength)
                        }
                    }

                    cleanUnusedTempAudio()
                } else {
                    _uiState.update { it.copy(isPlaylistLoading = false, statusMessage = "Danh sách phát trống") }
                }
            }.onFailure { err ->
                _uiState.update { it.copy(isPlaylistLoading = false, statusMessage = "Lỗi nạp danh sách: ${err.message}") }
            }
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
            ActiveQueueSource.COLLECTION -> _uiState.value.collectionTracks
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
            ActiveQueueSource.COLLECTION -> _uiState.value.collectionTracks
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

    private var playTrackJob: Job? = null

    fun playTrack(item: PlaylistItem) {
        com.kahoii.music.service.MusicQueueManager.activeQueue = _uiState.value.activeQueue
        com.kahoii.music.service.MusicQueueManager.currentIndex = _uiState.value.currentPlaylistIndex
        com.kahoii.music.service.MusicQueueManager.loopMode = _uiState.value.loopMode
        com.kahoii.music.service.MusicQueueManager.isShuffled = _uiState.value.isShuffled
        com.kahoii.music.service.MusicQueueManager.activeQueueSource = _uiState.value.activeQueueSource
        com.kahoii.music.service.MusicQueueManager.currentTrack = item

        playTrackJob?.cancel()
        playTrackJob = viewModelScope.launch {
            // 1. Check if track is saved offline first -> Instant playback with 0 delay!
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
                if (_uiState.value.activeQueueSource == ActiveQueueSource.HISTORY) {
                    historyRepository.updateTrackInfoOnly(savedTrack)
                } else {
                    historyRepository.addTrack(savedTrack)
                }
                val fileUri = Uri.fromFile(File(savedTrack.localAudioPath!!)).toString()
                playStreamUrl(fileUri, savedTrack)
                preloadNextTrack(savedTrack.id)
                cleanUnusedTempAudio()
                return@launch
            }

            // 1b. Check if track is cached in temp audio -> Instant playback!
            val cachedTemp = savedTracksRepository.getTempAudioFile(item.id)
            if (cachedTemp != null && cachedTemp.exists()) {
                val cachedTrack = item.copy(
                    durationSeconds = if (item.durationSeconds > 0) item.durationSeconds else 180
                )
                _uiState.update {
                    it.copy(
                        currentTrack = cachedTrack,
                        durationMs = cachedTrack.durationSeconds * 1000L,
                        statusMessage = "Đang phát (Đã lưu tạm)",
                        status = PlaybackStatus.BUFFERING
                    )
                }
                if (_uiState.value.activeQueueSource == ActiveQueueSource.HISTORY) {
                    historyRepository.updateTrackInfoOnly(cachedTrack)
                } else {
                    historyRepository.addTrack(cachedTrack)
                }
                val fileUri = Uri.fromFile(cachedTemp).toString()
                playStreamUrl(fileUri, cachedTrack)
                preloadNextTrack(cachedTrack.id)
                cleanUnusedTempAudio()
                return@launch
            }

            _uiState.update {
                it.copy(
                    currentTrack = item,
                    statusMessage = "Đang nạp âm thanh...",
                    status = PlaybackStatus.BUFFERING
                )
            }
            if (!LyricsRepository.isPlaceholderTitle(item.title)) {
                loadLyricsForTrack(item.id, item.safeTitle, item.safeArtist, item.durationSeconds)
            }

            val result = youTubeRepository.resolveAudio(item.id)
            result.onSuccess { stream ->
                val curTitle = item.safeTitle
                val curArtist = item.safeArtist
                val resolvedTrack = item.copy(
                    title = if (curTitle.isBlank() || curTitle == "Đang tải..." || curTitle.startsWith("Bài ")) {
                        stream.title.ifBlank { curTitle }
                    } else {
                        curTitle
                    },
                    artist = if (curArtist.isBlank() || curArtist == "Đang tải...") stream.artist else curArtist,
                    durationSeconds = if (item.durationSeconds <= 0) stream.durationSeconds else item.durationSeconds
                )

                _uiState.update { state ->
                    val updatedPlaylist = state.playlist.map { pItem ->
                        if (pItem.id.equals(resolvedTrack.id, ignoreCase = true)) {
                            pItem.copy(
                                title = resolvedTrack.title,
                                artist = resolvedTrack.artist,
                                durationSeconds = resolvedTrack.durationSeconds
                            )
                        } else pItem
                    }
                    state.copy(
                        currentTrack = resolvedTrack,
                        playlist = updatedPlaylist,
                        durationMs = resolvedTrack.durationSeconds * 1000L
                    )
                }

                // If listening from History, keep order fixed!
                if (_uiState.value.activeQueueSource == ActiveQueueSource.HISTORY) {
                    historyRepository.updateTrackInfoOnly(resolvedTrack)
                } else {
                    historyRepository.addTrack(resolvedTrack)
                }

                // Play in ExoPlayer
                playStreamUrl(stream.audioUrl, resolvedTrack, stream.artworkBytes)

                // Preload only 1 next track ahead & clean old session audio
                preloadNextTrack(resolvedTrack.id)
                cleanUnusedTempAudio()
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
        // If user already had a session playlist loaded in Tab 0, DO NOT wipe it out!
        if (_uiState.value.playlist.isEmpty()) {
            _uiState.update {
                it.copy(
                    playlist = listOf(tempItem),
                    currentPlaylistIndex = 0,
                    activeQueueSource = ActiveQueueSource.PLAYLIST
                )
            }
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
            .setArtist(track.displayArtist.ifBlank { track.artist })
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

        p.stop()
        p.setMediaItem(mediaItem)
        p.prepare()
        p.playWhenReady = true
        p.play()

        // Auto-load lyrics for the track currently starting playback
        if (!LyricsRepository.isPlaceholderTitle(track.title)) {
            loadLyricsForTrack(track.id, track.safeTitle, track.safeArtist, track.durationSeconds)
        }
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
        com.kahoii.music.service.MusicQueueManager.isShuffled = newShuffle
    }

    fun toggleFavorite(track: PlaylistItem? = _uiState.value.currentTrack) {
        val target = track ?: _uiState.value.currentTrack ?: return
        if (target.id.isBlank()) return
        viewModelScope.launch {
            historyRepository.toggleFavorite(target)
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
        com.kahoii.music.service.MusicQueueManager.activeQueue = _uiState.value.activeQueue
        com.kahoii.music.service.MusicQueueManager.loopMode = _uiState.value.loopMode
        com.kahoii.music.service.MusicQueueManager.isShuffled = _uiState.value.isShuffled
        com.kahoii.music.service.MusicQueueManager.activeQueueSource = _uiState.value.activeQueueSource
        com.kahoii.music.service.MusicQueueManager.playNext()
    }

    fun playPrevious() {
        com.kahoii.music.service.MusicQueueManager.activeQueue = _uiState.value.activeQueue
        com.kahoii.music.service.MusicQueueManager.loopMode = _uiState.value.loopMode
        com.kahoii.music.service.MusicQueueManager.isShuffled = _uiState.value.isShuffled
        com.kahoii.music.service.MusicQueueManager.activeQueueSource = _uiState.value.activeQueueSource
        com.kahoii.music.service.MusicQueueManager.playPrevious()
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

    fun seekTo(positionMs: Long) {
        val p = controller ?: return
        p.seekTo(positionMs)
        _uiState.update { it.copy(currentPositionMs = positionMs) }
    }

    fun toggleLyricsVisibility() {
        val willBeVisible = !_uiState.value.isLyricsVisible
        _uiState.update { it.copy(isLyricsVisible = willBeVisible) }
        if (willBeVisible) {
            val cur = _uiState.value.currentTrack
            val lyrics = _uiState.value.lyricsData
            if (cur != null && cur.id.isNotBlank() && (lyrics == null || !lyrics.hasLyrics)) {
                loadLyricsForTrack(cur.id, cur.safeTitle, cur.safeArtist, cur.durationSeconds, forceRefresh = true)
            }
        }
    }

    fun cycleRomanizationMode() {
        val current = _uiState.value.romanizationMode
        val next = when (current) {
            RomanizationDisplayMode.ORIGINAL -> RomanizationDisplayMode.DUAL
            RomanizationDisplayMode.DUAL -> RomanizationDisplayMode.ROMANIZED_ONLY
            RomanizationDisplayMode.ROMANIZED_ONLY -> RomanizationDisplayMode.ORIGINAL
        }
        appPrefs.edit().putInt("lyrics_romanization_mode", next.value).apply()
        _uiState.update { it.copy(romanizationMode = next) }
    }

    fun loadLyricsForTrack(
        videoId: String,
        title: String,
        artist: String,
        durationSeconds: Int,
        forceRefresh: Boolean = false
    ) {
        if (videoId.isBlank()) {
            _uiState.update { it.copy(lyricsData = null, isLyricsLoading = false, currentLyricIndex = -1) }
            return
        }

        if (LyricsRepository.isPlaceholderTitle(title)) {
            // Track is still resolving, hold loading state until stream resolves
            _uiState.update { it.copy(lyricsData = null, isLyricsLoading = true, currentLyricIndex = -1) }
            return
        }

        if (!forceRefresh && _uiState.value.lyricsData?.videoId == videoId && _uiState.value.lyricsData?.hasLyrics == true) {
            return
        }

        lyricsJob?.cancel()
        _uiState.update {
            it.copy(
                lyricsData = null,
                isLyricsLoading = true,
                currentLyricIndex = -1
            )
        }
        lyricsJob = viewModelScope.launch {
            val result = lyricsRepository.fetchLyrics(videoId, title, artist, durationSeconds, forceRefresh)
            if (_uiState.value.currentTrack?.id == videoId) {
                _uiState.update {
                    it.copy(
                        lyricsData = result,
                        isLyricsLoading = false
                    )
                }
            }
        }
    }

    fun toggleLoopMode() {
        val nextMode = when (_uiState.value.loopMode) {
            LoopMode.OFF -> LoopMode.ALL
            LoopMode.ALL -> LoopMode.ONE
            LoopMode.ONE -> LoopMode.SHUFFLE
            LoopMode.SHUFFLE -> LoopMode.OFF
        }
        _uiState.update { it.copy(loopMode = nextMode) }
        com.kahoii.music.service.MusicQueueManager.loopMode = nextMode

        val msg = when (nextMode) {
            LoopMode.OFF -> "Chế độ: Phát tuần tự"
            LoopMode.ALL -> "Chế độ: Lặp lại danh sách"
            LoopMode.ONE -> "Chế độ: Lặp lại 1 bài"
            LoopMode.SHUFFLE -> "Chế độ: Phát ngẫu nhiên (Lặp khi hết)"
        }
        Toast.makeText(getApplication(), msg, Toast.LENGTH_SHORT).show()

        val p = controller ?: return
        p.repeatMode = Player.REPEAT_MODE_OFF
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
            val originalVolume = controller?.volume ?: _uiState.value.volume
            var isFading = false
            try {
                var remaining = totalSeconds
                while (remaining > 3 && isActive) {
                    delay(1000)
                    remaining--
                    _uiState.update { it.copy(sleepTimerRemainingSeconds = remaining) }
                }

                // Giảm dần âm lượng trong 3 giây trước khi tắt
                if (isActive && remaining > 0) {
                    isFading = true
                    val startVol = controller?.volume ?: originalVolume
                    val totalFadeMs = (remaining * 1000L).coerceAtMost(3000L)
                    val steps = 30
                    val stepDelay = (totalFadeMs / steps).coerceAtLeast(50L)
                    for (step in 1..steps) {
                        if (!isActive) break
                        delay(stepDelay)
                        val fraction = (steps - step).toFloat() / steps
                        controller?.volume = (startVol * fraction).coerceIn(0f, 1f)
                        val remSec = ((steps - step) * totalFadeMs / (steps * 1000L))
                        _uiState.update { it.copy(sleepTimerRemainingSeconds = remSec) }
                    }
                }

                if (isActive) {
                    controller?.pause()
                    controller?.volume = originalVolume
                    _uiState.update {
                        it.copy(
                            sleepTimerOption = SleepTimerOption.OFF,
                            sleepTimerRemainingSeconds = 0L,
                            statusMessage = "Hẹn giờ đã kết thúc - Đã tạm dừng nhạc"
                        )
                    }
                }
            } finally {
                if (isFading) {
                    controller?.volume = originalVolume
                }
            }
        }
    }

    // ================= OFFLINE SAVED TRACKS & PRELOAD =================
    fun quickDownloadCurrentTrack() {
        val track = _uiState.value.currentTrack ?: return
        if (savedTracksRepository.isTrackSaved(track.id)) {
            Toast.makeText(getApplication(), "Bài hát đã có trong mục Đã tải", Toast.LENGTH_SHORT).show()
            return
        }
        if (isTrackDownloading(track.id)) {
            Toast.makeText(getApplication(), "Bài hát đang được tải...", Toast.LENGTH_SHORT).show()
            return
        }
        saveTrackOffline(track)
    }

    private var preloadingJob: Job? = null

    private fun preloadNextTrack(currentTrackId: String) {
        preloadingJob?.cancel()
        preloadingJob = viewModelScope.launch(Dispatchers.IO) {
            try {
                val state = _uiState.value
                val queue = state.activeQueue
                if (queue.size <= 1) return@launch

                val currentIndex = queue.indexOfFirst { it.id.equals(currentTrackId, ignoreCase = true) }
                if (currentIndex < 0) return@launch

                val nextIndex = (currentIndex + 1) % queue.size
                if (nextIndex == currentIndex) return@launch

                val nextTrack = queue[nextIndex]
                if (savedTracksRepository.isTrackSaved(nextTrack.id) || savedTracksRepository.getTempAudioFile(nextTrack.id) != null) {
                    return@launch
                }

                // Preload exactly 1 track ahead to optimize storage
                val result = youTubeRepository.resolveAudio(nextTrack.id)
                result.onSuccess { stream ->
                    savedTracksRepository.cacheTempAudio(nextTrack.id, stream.audioUrl, stream.contentLength)
                    if (stream.durationSeconds > 0) {
                        _uiState.update { state ->
                            val updatedPlaylist = state.playlist.map { pItem ->
                                if (pItem.id.equals(nextTrack.id, ignoreCase = true)) {
                                    val curTitle = pItem.safeTitle
                                    val curArtist = pItem.safeArtist
                                    pItem.copy(
                                        title = if (curTitle.isBlank() || curTitle.startsWith("Bài ")) stream.title else curTitle,
                                        artist = if (curArtist.isBlank()) stream.artist else curArtist,
                                        durationSeconds = stream.durationSeconds
                                    )
                                } else pItem
                            }
                            state.copy(playlist = updatedPlaylist)
                        }
                    }
                }
            } catch (_: Exception) { }
        }
    }

    fun cleanUnusedTempAudio() {
        viewModelScope.launch(Dispatchers.IO) {
            val protectedSet = mutableSetOf<String>()
            _uiState.value.currentTrack?.let { protectedSet.add(it.id) }

            // Only protect the 1 next preloaded track in queue
            val queue = _uiState.value.activeQueue
            val curId = _uiState.value.currentTrack?.id
            val curIdx = if (!curId.isNullOrBlank()) queue.indexOfFirst { it.id.equals(curId, ignoreCase = true) } else -1
            val nextIdx = if (curIdx >= 0) curIdx + 1 else 0
            if (nextIdx in queue.indices) {
                protectedSet.add(queue[nextIdx].id)
            }

            savedTracksRepository.cleanUnusedTempAudio(protectedSet)
        }
    }

    fun saveTrackOffline(item: PlaylistItem, bypassCellularWarning: Boolean = false) {
        if (item.safeId.isBlank()) return
        if (savedTracksRepository.isTrackSaved(item.id)) return

        if (!bypassCellularWarning && NetworkUtils.isCellular(getApplication())) {
            _uiState.update { it.copy(pendingCellularDownloadTrack = item) }
            return
        }

        executeDownloadTrack(item)
    }

    private fun executeDownloadTrack(item: PlaylistItem) {
        viewModelScope.launch(Dispatchers.IO) {
            _uiState.update { it.copy(downloadingTrackIds = it.downloadingTrackIds + item.id) }
            try {
                val streamResult = youTubeRepository.resolveAudio(item.id)
                streamResult.onSuccess { stream ->
                    val curTitle = item.safeTitle
                    val curArtist = item.safeArtist
                    val fullItem = item.copy(
                        title = if (curTitle.isBlank() || curTitle == "Đang tải...") stream.title else curTitle,
                        artist = if (curArtist.isBlank()) stream.artist else curArtist,
                        durationSeconds = if (item.durationSeconds <= 0) stream.durationSeconds else item.durationSeconds
                    )
                    savedTracksRepository.saveTrack(fullItem, stream.audioUrl, stream.thumbnailUrl, stream.contentLength)
                    cleanUnusedTempAudio()
                }
            } catch (_: Exception) {
            } finally {
                _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds - item.id) }
            }
        }
    }
    private var lastObservedUserId: String? = null

    private fun observeAuth() {
        viewModelScope.launch {
            authRepository.currentUser.collect { user ->
                val prevId = lastObservedUserId
                val newId = user?.localId
                lastObservedUserId = newId
                _uiState.update { it.copy(currentUser = user) }
                if (user != null) {
                    historyRepository.loadHistory()
                    if (prevId != newId) {
                        syncCloudData(user)
                    }
                } else {
                    isApplyingCloudPayload = false
                    historyRepository.onLoggedOut()
                }
            }
        }
    }

    fun signUp(email: String, pass: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusMessage = "Đang tạo tài khoản...") }
            val res = authRepository.signUp(email, pass)
            res.onSuccess { user ->
                clearUserDataExceptCurrentPlaying()
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Đăng ký thành công!") }
                Toast.makeText(getApplication(), "Đăng ký thành công!", Toast.LENGTH_SHORT).show()
                uploadCurrentDataToCloud(user)
            }.onFailure { err ->
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = err.message) }
                Toast.makeText(getApplication(), err.message, Toast.LENGTH_LONG).show()
            }
        }
    }

    fun signIn(email: String, pass: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusMessage = "Đang đăng nhập...") }
            val res = authRepository.signIn(email, pass)
            res.onSuccess { user ->
                clearUserDataExceptCurrentPlaying()
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Đăng nhập thành công!") }
                Toast.makeText(getApplication(), "Đăng nhập thành công!", Toast.LENGTH_SHORT).show()
                syncCloudData(user)
            }.onFailure { err ->
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = err.message) }
                Toast.makeText(getApplication(), err.message, Toast.LENGTH_LONG).show()
            }
        }
    }

    fun forgotPassword(email: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusMessage = "Đang gửi email khôi phục...") }
            val res = authRepository.sendPasswordResetEmail(email)
            res.onSuccess { msg ->
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = msg) }
                Toast.makeText(getApplication(), msg, Toast.LENGTH_LONG).show()
            }.onFailure { err ->
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = err.message) }
                Toast.makeText(getApplication(), err.message, Toast.LENGTH_LONG).show()
            }
        }
    }

    private fun clearUserDataExceptCurrentPlaying() {
        viewModelScope.launch {
            val currentTrack = _uiState.value.currentTrack
            if (currentTrack != null) {
                _uiState.update { it.copy(playlist = listOf(currentTrack), currentPlaylistIndex = 0) }
            } else {
                _uiState.update { it.copy(playlist = emptyList(), currentPlaylistIndex = -1) }
            }
            historyRepository.clearAll()
            savedTracksRepository.clearAll()
            playlistCollectionRepository.clearAll()
            _uiState.update {
                it.copy(
                    savedTracks = emptyList(),
                    history = emptyList(),
                    favorites = emptyList(),
                    playlists = emptyList()
                )
            }
        }
    }

    fun signOut() {
        clearUserDataExceptCurrentPlaying()
        historyRepository.onLoggedOut()
        authRepository.signOut()
        _uiState.update { it.copy(currentUser = null, syncStatusMessage = null) }
        Toast.makeText(getApplication(), "Đã đăng xuất", Toast.LENGTH_SHORT).show()
    }

    fun updateAvatar(bitmap: android.graphics.Bitmap) {
        viewModelScope.launch {
            try {
                val b64 = com.kahoii.music.util.ImageUtils.bitmapToBase64(bitmap, maxSize = 200)
                authRepository.updateAvatar(b64)
                _uiState.update { it.copy(currentUser = authRepository.currentUser.value) }

                val res = authRepository.uploadAvatar(b64)
                res.onSuccess {
                    Toast.makeText(getApplication(), "Đã cập nhật ảnh đại diện!", Toast.LENGTH_SHORT).show()
                }.onFailure { err ->
                    Toast.makeText(getApplication(), "Lỗi lưu đám mây: ${err.message}", Toast.LENGTH_SHORT).show()
                }
            } catch (e: Exception) {
                Toast.makeText(getApplication(), "Không thể xử lý ảnh: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    fun syncNow() {
        val user = _uiState.value.currentUser ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusMessage = "Đang đồng bộ...") }
            isApplyingCloudPayload = true
            syncJob?.cancel()
            val res = syncRepository.downloadMusicData(user)
            res.onSuccess { cloudPayload ->
                if (cloudPayload != null) {
                    try {
                        applyCloudPayload(cloudPayload)
                    } finally {
                        delay(500)
                    }
                }
                // Upload merged state
                val mergedPayload = CloudMusicPayload(
                    playlist = _uiState.value.playlist,
                    favorites = _uiState.value.favorites,
                    history = _uiState.value.history,
                    savedTracks = _uiState.value.savedTracks,
                    collections = _uiState.value.playlists
                )
                syncRepository.uploadMusicData(user, mergedPayload)
                isApplyingCloudPayload = false
                syncJob?.cancel()
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Đã đồng bộ đám mây lúc ${java.text.SimpleDateFormat("HH:mm", java.util.Locale.getDefault()).format(java.util.Date())}") }
            }.onFailure { err ->
                isApplyingCloudPayload = false
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Lỗi đồng bộ: ${err.message}") }
            }
        }
    }

    private var backgroundDownloadJob: kotlinx.coroutines.Job? = null

    private fun startSequentialBackgroundDownload(tracks: List<PlaylistItem>) {
        backgroundDownloadJob?.cancel()
        backgroundDownloadJob = viewModelScope.launch(Dispatchers.IO) {
            for (track in tracks) {
                if (!isActive) break
                if (savedTracksRepository.isTrackSaved(track.id)) continue
                _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds + track.id) }
                try {
                    val streamResult = youTubeRepository.resolveAudio(track.id)
                    streamResult.onSuccess { stream ->
                        val curTitle = track.safeTitle
                        val curArtist = track.safeArtist
                        val fullItem = track.copy(
                            title = if (curTitle.isBlank() || curTitle == "Đang tải...") stream.title else curTitle,
                            artist = if (curArtist.isBlank()) stream.artist else curArtist,
                            durationSeconds = if (track.durationSeconds <= 0) stream.durationSeconds else track.durationSeconds
                        )
                        savedTracksRepository.saveTrack(fullItem, stream.audioUrl, stream.thumbnailUrl, stream.contentLength)
                    }
                } catch (_: Exception) {
                } finally {
                    _uiState.update { s -> s.copy(downloadingTrackIds = s.downloadingTrackIds - track.id) }
                }
                delay(800)
            }
        }
    }

    private fun syncCloudData(user: FirebaseUser) {
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusMessage = "Đang đồng bộ...") }
            isApplyingCloudPayload = true
            syncJob?.cancel()
            val res = syncRepository.downloadMusicData(user)
            res.onSuccess { cloudPayload ->
                if (cloudPayload != null) {
                    try {
                        applyCloudPayload(cloudPayload)
                    } finally {
                        delay(800)
                        isApplyingCloudPayload = false
                        syncJob?.cancel()
                    }
                    _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Đồng bộ đám mây đã hoàn tất") }

                    // Tự động tải lại bài đã lưu tuần tự ngầm, không spam thông báo
                    val missingTracks = cloudPayload.savedTracks.filter { !savedTracksRepository.isTrackSaved(it.id) }
                    if (missingTracks.isNotEmpty()) {
                        if (NetworkUtils.isCellular(getApplication())) {
                            _uiState.update { it.copy(pendingCellularDownloadTracks = missingTracks) }
                        } else {
                            startSequentialBackgroundDownload(missingTracks)
                        }
                    }
                } else {
                    isApplyingCloudPayload = false
                    _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Dữ liệu đám mây rỗng") }
                }
            }.onFailure { err ->
                isApplyingCloudPayload = false
                _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Lỗi đồng bộ: ${err.message}") }
            }
        }
    }

    private suspend fun applyCloudPayload(payload: CloudMusicPayload) = withContext(Dispatchers.IO) {
        if (payload.playlist.isNotEmpty()) {
            _uiState.update { it.copy(playlist = payload.playlist) }
        }
        historyRepository.mergeFavorites(payload.favorites)
        historyRepository.mergeHistory(payload.history)
        savedTracksRepository.mergeSavedTracks(payload.savedTracks)
        playlistCollectionRepository.mergeCollections(payload.collections)
    }

    fun triggerCloudUpload() {
        val user = _uiState.value.currentUser ?: return
        if (isApplyingCloudPayload) return
        syncJob?.cancel()
        syncJob = viewModelScope.launch {
            delay(2500)
            uploadCurrentDataToCloud(user)
        }
    }

    private suspend fun uploadCurrentDataToCloud(user: FirebaseUser) {
        _uiState.update { it.copy(isSyncing = true) }
        val payload = CloudMusicPayload(
            playlist = _uiState.value.playlist,
            favorites = _uiState.value.favorites,
            history = _uiState.value.history,
            savedTracks = _uiState.value.savedTracks,
            collections = _uiState.value.playlists
        )
        val res = syncRepository.uploadMusicData(user, payload)
        res.onSuccess {
            _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Đã lưu đám mây lúc ${java.text.SimpleDateFormat("HH:mm", java.util.Locale.getDefault()).format(java.util.Date())}") }
        }.onFailure { err ->
            _uiState.update { it.copy(isSyncing = false, syncStatusMessage = "Lỗi đồng bộ: ${err.message}") }
        }
    }

    fun confirmCellularDownload() {
        val single = _uiState.value.pendingCellularDownloadTrack
        val list = _uiState.value.pendingCellularDownloadTracks
        _uiState.update { it.copy(pendingCellularDownloadTrack = null, pendingCellularDownloadTracks = emptyList()) }

        if (single != null) {
            executeDownloadTrack(single)
        }
        if (list.isNotEmpty()) {
            startSequentialBackgroundDownload(list)
        }
    }

    fun proceedCellularDownload() = confirmCellularDownload()

    fun dismissCellularDownload() {
        _uiState.update { it.copy(pendingCellularDownloadTrack = null, pendingCellularDownloadTracks = emptyList()) }
    }

    fun cancelCellularDownload() = dismissCellularDownload()


    fun deleteSavedTrack(id: String) {
        viewModelScope.launch {
            savedTracksRepository.deleteTrack(id)
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
        com.kahoii.music.service.MusicQueueManager.activeQueue = _uiState.value.activeQueue
        com.kahoii.music.service.MusicQueueManager.loopMode = _uiState.value.loopMode
        com.kahoii.music.service.MusicQueueManager.isShuffled = _uiState.value.isShuffled
        com.kahoii.music.service.MusicQueueManager.activeQueueSource = _uiState.value.activeQueueSource
        com.kahoii.music.service.MusicQueueManager.handleTrackEnded()
    }

    private fun observePlaylists() {
        viewModelScope.launch {
            playlistCollectionRepository.playlists.collect { list ->
                _uiState.update { it.copy(playlists = list) }
                if (!isApplyingCloudPayload && _uiState.value.currentUser != null) {
                    triggerCloudUpload()
                }
            }
        }
    }

    fun playCollection(collection: UserPlaylist, startIndex: Int = 0) {
        if (collection.items.isNotEmpty()) {
            val safeIndex = startIndex.coerceIn(0, collection.items.size - 1)
            _uiState.update {
                it.copy(
                    collectionTracks = collection.items,
                    currentPlaylistIndex = safeIndex,
                    currentPlaylistTitle = collection.name,
                    activeQueueSource = ActiveQueueSource.COLLECTION
                )
            }
            playFromQueueIndex(safeIndex, ActiveQueueSource.COLLECTION)
            Toast.makeText(getApplication(), "Đang phát bộ sưu tập: ${collection.name}", Toast.LENGTH_SHORT).show()
        } else {
            Toast.makeText(getApplication(), "Bộ sưu tập này chưa có bài hát nào", Toast.LENGTH_SHORT).show()
        }
    }

    fun saveCurrentPlaylistToCollection(name: String) {
        viewModelScope.launch {
            val currentList = _uiState.value.playlist
            if (currentList.isNotEmpty()) {
                playlistCollectionRepository.savePlaylist(name, currentList)
            }
        }
    }

    fun createCollection(name: String) {
        viewModelScope.launch {
            playlistCollectionRepository.createPlaylist(name)
        }
    }

    fun deleteCollection(id: String) {
        viewModelScope.launch {
            playlistCollectionRepository.deletePlaylist(id)
        }
    }

    fun renameCollection(id: String, newName: String) {
        viewModelScope.launch {
            playlistCollectionRepository.renamePlaylist(id, newName)
        }
    }

    fun addTrackToCollection(playlistId: String, track: PlaylistItem) {
        viewModelScope.launch {
            playlistCollectionRepository.addTrackToPlaylist(playlistId, track)
        }
    }

    fun removeTrackFromCollection(playlistId: String, trackId: String) {
        viewModelScope.launch {
            playlistCollectionRepository.removeTrackFromPlaylist(playlistId, trackId)
            if (_uiState.value.activeQueueSource == ActiveQueueSource.PLAYLIST) {
                val updated = _uiState.value.playlist.filter { it.id != trackId }
                _uiState.update { it.copy(playlist = updated) }
                com.kahoii.music.service.MusicQueueManager.activeQueue = updated
            }
        }
    }

    fun downloadCollection(collection: UserPlaylist) {
        viewModelScope.launch {
            val missing = collection.items.filter { !savedTracksRepository.isTrackSaved(it.id) }
            if (missing.isNotEmpty()) {
                if (NetworkUtils.isCellular(getApplication())) {
                    _uiState.update { it.copy(pendingCellularDownloadTracks = missing) }
                } else {
                    startSequentialBackgroundDownload(missing)
                }
            }
        }
    }

    private var updateDownloadJob: Job? = null

    fun checkAppUpdate(isManual: Boolean = false) {
        viewModelScope.launch(Dispatchers.IO) {
            _uiState.update { it.copy(isCheckingUpdate = true) }
            val remoteInfo = AppUpdateManager.fetchRemoteVersion()
            val currentCode = BuildConfig.VERSION_CODE
            val isNewer = remoteInfo != null && remoteInfo.versionCode > currentCode

            withContext(Dispatchers.Main) {
                if (isNewer) {
                    _uiState.update {
                        it.copy(
                            updateInfo = remoteInfo,
                            updateDownloadState = UpdateDownloadState.Idle,
                            isCheckingUpdate = false
                        )
                    }
                } else {
                    _uiState.update { it.copy(isCheckingUpdate = false) }
                    if (isManual) {
                        val message = if (remoteInfo != null) {
                            "Bạn đang sử dụng phiên bản mới nhất (${BuildConfig.VERSION_NAME})"
                        } else {
                            "Không thể kiểm tra bản cập nhật lúc này. Vui lòng thử lại sau."
                        }
                        Toast.makeText(getApplication(), message, Toast.LENGTH_SHORT).show()
                    }
                }
            }
        }
    }

    fun startDownloadUpdate(context: android.content.Context) {
        val info = _uiState.value.updateInfo ?: return
        updateDownloadJob?.cancel()
        _uiState.update {
            it.copy(updateDownloadState = UpdateDownloadState.Downloading(0f, 0L, 0L))
        }

        updateDownloadJob = viewModelScope.launch(Dispatchers.IO) {
            val result = AppUpdateManager.downloadApk(context, info.apkUrl) { progress, downloaded, total ->
                _uiState.update {
                    it.copy(updateDownloadState = UpdateDownloadState.Downloading(progress, downloaded, total))
                }
            }

            withContext(Dispatchers.Main) {
                result.fold(
                    onSuccess = { apkFile ->
                        _uiState.update {
                            it.copy(updateDownloadState = UpdateDownloadState.Completed(apkFile))
                        }
                        // Automatically trigger installer
                        AppUpdateManager.installApk(context, apkFile)
                    },
                    onFailure = { error ->
                        _uiState.update {
                            it.copy(updateDownloadState = UpdateDownloadState.Error(error.message ?: "Tải bản cập nhật thất bại"))
                        }
                    }
                )
            }
        }
    }

    fun installDownloadedUpdate(context: android.content.Context, apkFile: File) {
        AppUpdateManager.installApk(context, apkFile)
    }

    fun dismissUpdateDialog() {
        val info = _uiState.value.updateInfo
        if (info?.forceUpdate != true) {
            updateDownloadJob?.cancel()
            _uiState.update {
                it.copy(updateInfo = null, updateDownloadState = UpdateDownloadState.Idle)
            }
        }
    }

    override fun onCleared() {
        com.kahoii.music.service.MusicQueueManager.onTrackChanged = null
        com.kahoii.music.service.MusicQueueManager.onPlaylistIndexChanged = null
        com.kahoii.music.service.MusicQueueManager.onStatusMessage = null
        progressJob?.cancel()
        sleepTimerJob?.cancel()
        preloadingJob?.cancel()
        playTrackJob?.cancel()
        updateDownloadJob?.cancel()
        if (controller?.isPlaying != true) {
            controller?.release()
            controllerFuture?.let { MediaController.releaseFuture(it) }
            controller = null
        }
        super.onCleared()
    }
}
