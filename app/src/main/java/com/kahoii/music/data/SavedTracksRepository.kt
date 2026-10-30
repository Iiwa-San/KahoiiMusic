package com.kahoii.music.data

import android.content.Context
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.kahoii.music.model.PlaylistItem
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.FileOutputStream
import java.util.concurrent.TimeUnit

class SavedTracksRepository(private val context: Context) {

    private val gson = Gson()
    private val savedDir = File(context.filesDir, "saved_audio").apply { if (!exists()) mkdirs() }
    private val thumbDir = File(context.filesDir, "saved_thumb").apply { if (!exists()) mkdirs() }
    private val metadataFile = File(context.filesDir, "saved_tracks.json")

    private val client = OkHttpClient.Builder()
        .connectTimeout(20, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    private val _savedTracks = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val savedTracks: StateFlow<List<PlaylistItem>> = _savedTracks.asStateFlow()

    init {
        CoroutineScope(Dispatchers.IO).launch {
            loadSavedTracks()
        }
    }

    @Synchronized
    private fun loadSavedTracks() {
        try {
            if (metadataFile.exists()) {
                val json = metadataFile.readText()
                val listType = object : TypeToken<List<PlaylistItem>>() {}.type
                val loadedList: List<PlaylistItem>? = gson.fromJson(json, listType)
                if (loadedList != null) {
                    // Filter out tracks whose audio files no longer exist on disk
                    val validList = loadedList.filter { item ->
                        val localPath = item.localAudioPath
                        !localPath.isNullOrBlank() && File(localPath).exists() && File(localPath).length() > 50000
                    }
                    _savedTracks.value = validList
                    if (validList.size != loadedList.size) {
                        saveMetadata(validList)
                    }
                    return
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        _savedTracks.value = emptyList()
    }

    @Synchronized
    private fun saveMetadata(list: List<PlaylistItem>) {
        try {
            val json = gson.toJson(list)
            metadataFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    fun isTrackSaved(id: String): Boolean {
        if (id.isBlank()) return false
        val item = _savedTracks.value.find { it.id.equals(id, ignoreCase = true) }
        if (item?.localAudioPath == null) return false
        val file = File(item.localAudioPath)
        return file.exists() && file.length() > 50000
    }

    fun getSavedTrack(id: String): PlaylistItem? {
        if (id.isBlank()) return null
        return _savedTracks.value.find { it.id.equals(id, ignoreCase = true) }
    }

    suspend fun saveTrack(
        track: PlaylistItem,
        streamUrl: String,
        thumbnailUrl: String?
    ): Result<PlaylistItem> = withContext(Dispatchers.IO) {
        try {
            if (track.id.isBlank()) {
                return@withContext Result.failure(IllegalArgumentException("ID bài hát không hợp lệ"))
            }

            val audioFile = File(savedDir, "${track.id}.mp3")
            val thumbFile = File(thumbDir, "${track.id}.jpg")

            // 1. Download audio file if not present or too small
            if (!audioFile.exists() || audioFile.length() < 50000) {
                val req = Request.Builder()
                    .url(streamUrl)
                    .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
                    .build()

                client.newCall(req).execute().use { response ->
                    if (!response.isSuccessful) {
                        return@withContext Result.failure(Exception("Tải âm thanh thất bại: HTTP ${response.code}"))
                    }
                    val body = response.body ?: return@withContext Result.failure(Exception("Dữ liệu âm thanh trống"))
                    FileOutputStream(audioFile).use { out ->
                        body.byteStream().copyTo(out)
                    }
                }
            }

            // 2. Download thumbnail
            val thumbTargetUrl = thumbnailUrl ?: track.thumbnailUrl
            if (!thumbFile.exists() || thumbFile.length() < 500) {
                try {
                    val req = Request.Builder().url(thumbTargetUrl).build()
                    client.newCall(req).execute().use { response ->
                        if (response.isSuccessful) {
                            response.body?.byteStream()?.let { inputStream ->
                                FileOutputStream(thumbFile).use { out ->
                                    inputStream.copyTo(out)
                                }
                            }
                        }
                    }
                } catch (_: Exception) { }
            }

            val savedTrackItem = track.copy(
                localAudioPath = audioFile.absolutePath,
                customThumbnailUrl = if (thumbFile.exists() && thumbFile.length() > 500) {
                    thumbFile.toURI().toString()
                } else track.thumbnailUrl
            )

            val currentList = _savedTracks.value.toMutableList()
            currentList.removeAll { it.id.equals(track.id, ignoreCase = true) }
            currentList.add(0, savedTrackItem)
            _savedTracks.value = currentList
            saveMetadata(currentList)

            Result.success(savedTrackItem)
        } catch (e: Exception) {
            e.printStackTrace()
            Result.failure(e)
        }
    }

    suspend fun deleteTrack(id: String): Boolean = withContext(Dispatchers.IO) {
        try {
            val item = _savedTracks.value.find { it.id.equals(id, ignoreCase = true) }
            if (item != null) {
                item.localAudioPath?.let { path ->
                    val f = File(path)
                    if (f.exists()) f.delete()
                }
                val thumbFile = File(thumbDir, "$id.jpg")
                if (thumbFile.exists()) thumbFile.delete()

                val currentList = _savedTracks.value.toMutableList()
                currentList.removeAll { it.id.equals(id, ignoreCase = true) }
                _savedTracks.value = currentList
                saveMetadata(currentList)
                return@withContext true
            }
            false
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }
}

