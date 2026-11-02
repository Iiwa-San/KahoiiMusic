package com.kahoii.music.service

import android.content.Context
import android.net.Uri
import android.os.PowerManager
import androidx.media3.common.MediaItem
import androidx.media3.common.MediaMetadata
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import com.kahoii.music.data.HistoryRepository
import com.kahoii.music.data.SavedTracksRepository
import com.kahoii.music.data.YouTubeRepository
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

    // State
    @Volatile
    var activeQueue: List<PlaylistItem> = emptyList()

    @Volatile
    var currentTrack: PlaylistItem? = null

    @Volatile
    var currentIndex: Int = -1

    @Volatile
    var loopMode: LoopMode = LoopMode.OFF

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
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun releaseWakeLock() {
        try {
            if (wakeLock?.isHeld == true) {
                wakeLock?.release()
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
            LoopMode.ALL -> {
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

                if (currIdx >= 0 && currIdx + 1 < queue.size) {
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
        if (queue.isEmpty()) return
        if (queue.size == 1) {
            player?.seekTo(0L)
            player?.play()
            return
        }

        val currentId = currentTrack?.id
        val currIdx = if (!currentId.isNullOrBlank()) {
            queue.indexOfFirst { it.id.equals(currentId, ignoreCase = true) }
        } else -1

        val nextIdx = if (isShuffled && queue.size > 1) {
            val otherIndices = queue.indices.filter { it != currIdx }
            if (otherIndices.isNotEmpty()) otherIndices.random() else 0
        } else {
            if (currIdx >= 0) (currIdx + 1) % queue.size else 0
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

        if (currIdx <= 0 || queue.size == 1) {
            player?.seekTo(0L)
            if (player?.isPlaying == false) player?.play()
            return
        }

        val prevIdx = currIdx - 1
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
                    return@launch
                }

                // 1c. In-memory resolvedStreamCache
                val cachedStream = YouTubeRepository.getCachedStream(track.id)
                if (cachedStream != null) {
                    val resolvedTrack = track.copy(
                        title = if (track.title.isBlank() || track.title.startsWith("Bài ")) cachedStream.title else track.title,
                        artist = if (track.artist.isBlank()) cachedStream.artist else track.artist,
                        durationSeconds = if (track.durationSeconds <= 0) cachedStream.durationSeconds else track.durationSeconds
                    )
                    currentTrack = resolvedTrack
                    playStreamUrl(cachedStream.audioUrl, resolvedTrack, cachedStream.artworkBytes)
                    preloadNextTrack(resolvedTrack.id)
                    onResolved?.invoke(resolvedTrack)
                    return@launch
                }

                // 2. Resolve fresh stream
                onStatusMessage?.invoke("Đang nạp âm thanh...")
                val result = youTubeRepository.resolveAudio(track.id)
                result.onSuccess { stream ->
                    val resolvedTrack = track.copy(
                        title = if (track.title.isBlank() || track.title.startsWith("Bài ")) stream.title else track.title,
                        artist = if (track.artist.isBlank()) stream.artist else track.artist,
                        durationSeconds = if (track.durationSeconds <= 0) stream.durationSeconds else track.durationSeconds
                    )
                    currentTrack = resolvedTrack
                    playStreamUrl(stream.audioUrl, resolvedTrack, stream.artworkBytes)

                    serviceScope.launch(Dispatchers.IO) {
                        savedRepo?.cacheTempAudio(resolvedTrack.id, stream.audioUrl, stream.contentLength)
                    }
                    preloadNextTrack(resolvedTrack.id)
                    onResolved?.invoke(resolvedTrack)
                }.onFailure { err ->
                    onStatusMessage?.invoke("Lỗi kết nối: ${err.message}")
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

        p.stop()
        p.setMediaItem(mediaItem)
        p.prepare()
        p.playWhenReady = true
        p.play()
    }

    private var preloadJob: Job? = null

    fun preloadNextTrack(currentTrackId: String) {
        preloadJob?.cancel()
        preloadJob = serviceScope.launch(Dispatchers.IO) {
            try {
                val queue = activeQueue
                if (queue.size <= 1) return@launch
                val currIdx = queue.indexOfFirst { it.id.equals(currentTrackId, ignoreCase = true) }
                if (currIdx < 0) return@launch
                val nextIdx = (currIdx + 1) % queue.size
                if (nextIdx == currIdx) return@launch

                val nextTrack = queue[nextIdx]
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
