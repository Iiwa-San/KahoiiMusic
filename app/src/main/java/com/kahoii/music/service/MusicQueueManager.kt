package com.kahoii.music.service

import android.content.Context
import android.net.Uri
import android.net.wifi.WifiManager
import android.os.PowerManager
import androidx.media3.common.MediaItem
import androidx.media3.common.MediaMetadata
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import com.kahoii.music.data.HistoryRepository
import com.kahoii.music.data.SavedTracksRepository
import com.kahoii.music.data.YouTubeRepository
import com.kahoii.music.data.AudioStreamResult
import com.kahoii.music.model.ActiveQueueSource
import com.kahoii.music.model.LoopMode
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.model.SleepTimerOption
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.io.File

object MusicQueueManager {
    val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val youTubeRepository = YouTubeRepository()
    private var historyRepository: HistoryRepository? = null
    private var savedTracksRepository: SavedTracksRepository? = null

    @Volatile
    var player: ExoPlayer? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private val shufflePlayedIndices = mutableListOf<Int>()

    // State
    @Volatile
    var activeQueue: List<PlaylistItem> = emptyList()
        set(value) {
            field = value
            synchronized(shufflePlayedIndices) {
                shufflePlayedIndices.clear()
                val cur = currentTrack?.id
                val idx = if (!cur.isNullOrBlank()) value.indexOfFirst { it.id.equals(cur, ignoreCase = true) } else currentIndex
                if (idx >= 0) shufflePlayedIndices.add(idx)
            }
        }

    @Volatile
    var currentTrack: PlaylistItem? = null

    @Volatile
    var currentIndex: Int = -1

    @Volatile
    var loopMode: LoopMode = LoopMode.OFF
        set(value) {
            field = value
            if (value == LoopMode.SHUFFLE) {
                synchronized(shufflePlayedIndices) {
                    shufflePlayedIndices.clear()
                    val cur = currentTrack?.id
                    val idx = if (!cur.isNullOrBlank()) activeQueue.indexOfFirst { it.id.equals(cur, ignoreCase = true) } else currentIndex
                    if (idx >= 0) shufflePlayedIndices.add(idx)
                }
            }
        }

    @Volatile
    var isShuffled: Boolean = false

    @Volatile
    var activeQueueSource: ActiveQueueSource = ActiveQueueSource.PLAYLIST

    @Volatile
    var sleepTimerOption: SleepTimerOption = SleepTimerOption.OFF

    @Volatile
    var sleepTimerRemainingSeconds: Long = 0L

    @Volatile
    private var isTransitioning = false

    // Observers for MainViewModel UI
    var onTrackChanged: ((PlaylistItem) -> Unit)? = null
    var onTrackResolved: ((PlaylistItem) -> Unit)? = null
    var onPlaylistIndexChanged: ((Int) -> Unit)? = null
    var onQueueUpdated: ((List<PlaylistItem>) -> Unit)? = null
    var onStatusMessage: ((String) -> Unit)? = null

    fun init(context: Context, exoPlayer: ExoPlayer) {
        player = exoPlayer
        if (historyRepository == null) {
            historyRepository = HistoryRepository(context)
        }
        if (savedTracksRepository == null) {
            savedTracksRepository = SavedTracksRepository(context)
        }
        if (wakeLock == null) {
            try {
                val pm = context.getSystemService(Context.POWER_SERVICE) as? PowerManager
                wakeLock = pm?.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "KahoiiMusic:PlaybackTransition")
                wakeLock?.setReferenceCounted(false)
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
        if (wifiLock == null) {
            try {
                val wm = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
                @Suppress("DEPRECATION")
                wifiLock = wm?.createWifiLock(WifiManager.WIFI_MODE_FULL_HIGH_PERF, "KahoiiMusic:WifiLock")
                wifiLock?.setReferenceCounted(false)
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }

    fun releaseService(exoPlayer: ExoPlayer) {
        if (player === exoPlayer) {
            player = null
        }
        releaseWakeLock()
    }

    fun acquireWakeLock(timeoutMs: Long = 60_000L) {
        try {
            if (wakeLock?.isHeld == false) {
                wakeLock?.acquire(timeoutMs)
            }
            if (wifiLock?.isHeld == false) {
                wifiLock?.acquire()
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun releaseWakeLock() {
        try {
            if (wakeLock?.isHeld == true) {
                wakeLock?.release()
            }
            if (wifiLock?.isHeld == true) {
                wifiLock?.release()
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun triggerNext() {
        playNext()
    }

    fun triggerPrevious() {
        playPrevious()
    }

    fun handleTrackEnded() {
        if (isTransitioning) return
        isTransitioning = true

        acquireWakeLock(60_000L)

        if (sleepTimerOption == SleepTimerOption.END_OF_TRACK) {
            player?.pause()
            sleepTimerOption = SleepTimerOption.OFF
            onStatusMessage?.invoke("Hết bài hát - Đã dừng hẹn giờ")
            isTransitioning = false
            return
        }

        when (loopMode) {
            LoopMode.ONE -> {
                player?.seekTo(0L)
                player?.play()
                isTransitioning = false
            }
            LoopMode.ALL, LoopMode.SHUFFLE -> {
                playNext()
                serviceScope.launch {
                    delay(800)
                    isTransitioning = false
                }
            }
            LoopMode.OFF -> {
                val queue = activeQueue
                val currentId = currentTrack?.id
                val currIdx = if (!currentId.isNullOrBlank()) {
                    queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
                } else -1

                val canPlayNext = if (currIdx >= 0) {
                    currIdx + 1 < queue.size
                } else {
                    queue.isNotEmpty()
                }

                if (canPlayNext) {
                    playNext()
                    serviceScope.launch {
                        delay(800)
                        isTransitioning = false
                    }
                } else {
                    player?.pause()
                    onStatusMessage?.invoke("Đã phát hết danh sách")
                    isTransitioning = false
                }
            }
        }
    }

    fun playNext() {
        acquireWakeLock(60_000L)
        val queue = activeQueue
        if (queue.isEmpty()) {
            val cur = currentTrack
            if (cur != null && (loopMode == LoopMode.ALL || loopMode == LoopMode.SHUFFLE)) {
                player?.seekTo(0L)
                player?.play()
            }
            return
        }
        if (queue.size == 1) {
            if (currentTrack?.id == queue[0].id) {
                player?.seekTo(0L)
                player?.play()
                return
            } else {
                currentIndex = 0
                val nextTrack = queue[0]
                onPlaylistIndexChanged?.invoke(0)
                playTrack(nextTrack)
                return
            }
        }

        val currentId = currentTrack?.id
        val currIdx = if (!currentId.isNullOrBlank()) {
            queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
        } else -1

        val nextIdx = if (loopMode == LoopMode.SHUFFLE || isShuffled) {
            synchronized(shufflePlayedIndices) {
                shufflePlayedIndices.removeAll { it !in queue.indices }

                if (currIdx >= 0 && !shufflePlayedIndices.contains(currIdx)) {
                    shufflePlayedIndices.add(currIdx)
                }
                var unplayed = queue.indices.filter { !shufflePlayedIndices.contains(it) }
                if (unplayed.isEmpty()) {
                    // Loop lại list sau khi hết tất cả các bài!
                    shufflePlayedIndices.clear()
                    unplayed = queue.indices.filter { it != currIdx }
                    if (unplayed.isEmpty()) {
                        unplayed = listOf(0)
                    }
                }
                unplayed.random()
            }
        } else {
            if (currIdx >= 0) (currIdx + 1) % queue.size else 0
        }

        synchronized(shufflePlayedIndices) {
            if (!shufflePlayedIndices.contains(nextIdx)) {
                shufflePlayedIndices.add(nextIdx)
            }
        }

        currentIndex = nextIdx
        val nextTrack = queue[nextIdx]
        onPlaylistIndexChanged?.invoke(nextIdx)
        playTrack(nextTrack)
    }

    fun playPrevious() {
        acquireWakeLock(60_000L)
        val queue = activeQueue
        if (queue.isEmpty()) return

        val currentId = currentTrack?.id
        val currIdx = if (!currentId.isNullOrBlank()) {
            queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
        } else 0

        val prevIdx = if (loopMode == LoopMode.SHUFFLE) {
            val shuffPrev = synchronized(shufflePlayedIndices) {
                if (shufflePlayedIndices.size > 1) {
                    shufflePlayedIndices.removeAt(shufflePlayedIndices.size - 1)
                    shufflePlayedIndices.lastOrNull()
                } else null
            }
            if (shuffPrev != null) {
                shuffPrev
            } else {
                if (currIdx <= 0 || queue.size == 1) {
                    player?.seekTo(0L)
                    if (player?.isPlaying == false) player?.play()
                    return
                }
                currIdx - 1
            }
        } else {
            if (currIdx <= 0 || queue.size == 1) {
                player?.seekTo(0L)
                if (player?.isPlaying == false) player?.play()
                return
            }
            currIdx - 1
        }

        currentIndex = prevIdx
        val prevTrack = queue[prevIdx]
        onPlaylistIndexChanged?.invoke(prevIdx)
        playTrack(prevTrack)
    }

    private var playJob: Job? = null

    fun playTrack(track: PlaylistItem, onResolved: ((PlaylistItem) -> Unit)? = null) {
        playJob?.cancel()
        playJob = serviceScope.launch {
            acquireWakeLock(60_000L)
            try {
                currentTrack = track
                onTrackChanged?.invoke(track)

                // 1. Saved track offline
                val savedRepo = savedTracksRepository
                val savedTrack = savedRepo?.getSavedTrack(track.id)
                if (savedTrack != null && savedTrack.isOffline) {
                    val fileUri = Uri.fromFile(File(savedTrack.localAudioPath!!)).toString()
                    playStreamUrl(fileUri, savedTrack)
                    preloadNextTrack(savedTrack.id)
                    onResolved?.invoke(savedTrack)
                    onTrackResolved?.invoke(savedTrack)
                    return@launch
                }

                // 1b. Temp cache on disk
                val cachedTemp = savedRepo?.getTempAudioFile(track.id)
                if (cachedTemp != null && cachedTemp.exists()) {
                    val cachedTrack = track.copy(
                        durationSeconds = if (track.durationSeconds > 0) track.durationSeconds else 180
                    )
                    currentTrack = cachedTrack
                    val fileUri = Uri.fromFile(cachedTemp).toString()
                    playStreamUrl(fileUri, cachedTrack)
                    preloadNextTrack(cachedTrack.id)
                    onResolved?.invoke(cachedTrack)
                    onTrackResolved?.invoke(cachedTrack)
                    return@launch
                }

                // 1c. In-memory resolvedStreamCache
                val cachedStream: AudioStreamResult? = youTubeRepository.getCachedStream(track.id)
                if (cachedStream != null) {
                    val curTitle = track.safeTitle
                    val curArtist = track.safeArtist
                    val resolvedTrack = track.copy(
                        title = if (curTitle.isBlank() || curTitle.startsWith("Bài ")) cachedStream.title else curTitle,
                        artist = if (curArtist.isBlank()) cachedStream.artist else curArtist,
                        durationSeconds = if (track.durationSeconds <= 0) cachedStream.durationSeconds else track.durationSeconds
                    )
                    currentTrack = resolvedTrack
                    playStreamUrl(cachedStream.audioUrl, resolvedTrack, cachedStream.artworkBytes)
                    preloadNextTrack(resolvedTrack.id)
                    onResolved?.invoke(resolvedTrack)
                    onTrackResolved?.invoke(resolvedTrack)
                    return@launch
                }

                // 2. Resolve fresh stream with background auto-retry
                onStatusMessage?.invoke("Đang nạp âm thanh...")
                var result = youTubeRepository.resolveAudio(track.id)
                if (result.isFailure) {
                    acquireWakeLock(60_000L)
                    delay(1200L)
                    result = youTubeRepository.resolveAudio(track.id)
                }

                result.onSuccess { stream ->
                    val curTitle = track.safeTitle
                    val curArtist = track.safeArtist
                    val resolvedTrack = track.copy(
                        title = if (curTitle.isBlank() || curTitle.startsWith("Bài ")) stream.title else curTitle,
                        artist = if (curArtist.isBlank()) stream.artist else curArtist,
                        durationSeconds = if (track.durationSeconds <= 0) stream.durationSeconds else track.durationSeconds
                    )
                    currentTrack = resolvedTrack
                    playStreamUrl(stream.audioUrl, resolvedTrack, stream.artworkBytes)

                    preloadNextTrack(resolvedTrack.id)
                    onResolved?.invoke(resolvedTrack)
                    onTrackResolved?.invoke(resolvedTrack)
                }.onFailure { err ->
                    onStatusMessage?.invoke("Lỗi kết nối: ${err.message}")
                    serviceScope.launch {
                        delay(2000L)
                        if (player?.isPlaying == false && activeQueue.size > 1) {
                            playNext()
                        }
                    }
                }
            } finally {
                // WakeLock released on STATE_READY
            }
        }
    }

    fun playStreamUrl(audioUrl: String, track: PlaylistItem, artworkBytes: ByteArray? = null) {
        val p = player ?: return
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

        p.setMediaItem(mediaItem, /* resetPosition = */ true)
        p.prepare()
        p.playWhenReady = true
        p.play()
    }

    private var preloadJob: Job? = null

    fun preloadNextTrack(currentTrackId: String) {
        preloadJob?.cancel()
        preloadJob = serviceScope.launch(Dispatchers.IO) {
            try {
                acquireWakeLock(25_000L)
                val queue = activeQueue
                if (queue.size <= 1) return@launch
                val currIdx = queue.indexOfFirst { it.id.equals(currentTrackId, ignoreCase = true) }
                if (currIdx < 0) return@launch
                val nextIdx = (currIdx + 1) % queue.size
                if (nextIdx == currIdx) return@launch

                val nextTrack = queue[nextIdx]
                if (savedTracksRepository?.isTrackSaved(nextTrack.id) == true) return@launch
                val cachedTemp = savedTracksRepository?.getTempAudioFile(nextTrack.id)
                if (cachedTemp != null && cachedTemp.exists() && cachedTemp.length() > 500_000L) return@launch

                val result = youTubeRepository.resolveAudio(nextTrack.id)
                result.onSuccess { stream ->
                    savedTracksRepository?.cacheTempAudio(nextTrack.id, stream.audioUrl, stream.contentLength)
                }
            } catch (_: Exception) {}
        }
    }

    fun handlePlaybackError(error: PlaybackException) {
        serviceScope.launch {
            acquireWakeLock(30_000L)
            delay(1500L)
            playNext()
        }
    }
}
