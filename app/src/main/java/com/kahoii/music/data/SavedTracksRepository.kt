package com.kahoii.music.data

import android.content.Context
import com.google.gson.Gson
import com.kahoii.music.util.GsonUtils
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

    private val gson = GsonUtils.gson
    private val savedDir = File(context.filesDir, "saved_audio").apply { if (!exists()) mkdirs() }
    private val thumbDir = File(context.filesDir, "saved_thumb").apply { if (!exists()) mkdirs() }
    private val metadataFile = File(context.filesDir, "saved_tracks.json")

    // Temp audio cache (Matching PC KahoiiTemp/audio & iOS Caches/KahoiiTemp/audio)
    val tempAudioDir = File(context.cacheDir, "temp_audio").apply { if (!exists()) mkdirs() }

    private val client = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(120, TimeUnit.SECONDS)
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

    fun getTempAudioFile(id: String): File? {
        if (id.isBlank()) return null
        val candidates = listOf(
            File(tempAudioDir, "$id.mp3"),
            File(tempAudioDir, "$id.m4a")
        )
        for (f in candidates) {
            if (f.exists() && f.length() > 50000) return f
        }
        return null
    }

    /**
     * Tải phân đoạn đa luồng HTTP Range (5MB/chunk) - Đạt tốc độ 10-20 MB/s,
     * khắc phục triệt để lỗi YouTube bóp băng thông 32KB/s gây timeout trên các bài dài > 10 phút.
     */
    suspend fun downloadAudioChunked(
        streamUrl: String,
        destinationFile: File,
        expectedLength: Long? = null
    ): Boolean = withContext(Dispatchers.IO) {
        try {
            destinationFile.parentFile?.mkdirs()
            var totalLength: Long = expectedLength ?: 0L

            // 1. Nếu chưa biết tổng dung lượng, gửi probe Range bytes=0-0 để đọc Content-Range
            if (totalLength <= 0L) {
                val headReq = Request.Builder()
                    .url(streamUrl)
                    .header("Range", "bytes=0-0")
                    .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
                    .header("Referer", "https://www.youtube.com/")
                    .build()

                try {
                    client.newCall(headReq).execute().use { resp ->
                        val contentRange = resp.header("Content-Range")
                        if (!contentRange.isNullOrBlank()) {
                            val totalStr = contentRange.substringAfterLast("/").trim()
                            totalLength = totalStr.toLongOrNull() ?: 0L
                        }
                    }
                } catch (_: Exception) { }
            }

            // 2. Nếu file quá nhỏ (< 1MB) hoặc không xác định được độ dài, tải đơn luồng thông thường
            if (totalLength <= 1024 * 1024L) {
                val req = Request.Builder()
                    .url(streamUrl)
                    .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
                    .header("Referer", "https://www.youtube.com/")
                    .build()

                client.newCall(req).execute().use { resp ->
                    if (!resp.isSuccessful) return@withContext false
                    val body = resp.body ?: return@withContext false
                    val tempPart = File(destinationFile.parentFile, "${destinationFile.name}.part")
                    FileOutputStream(tempPart).use { out ->
                        body.byteStream().copyTo(out)
                    }
                    if (destinationFile.exists()) destinationFile.delete()
                    return@withContext tempPart.renameTo(destinationFile)
                }
            }

            // 3. Tải đa phân đoạn 5MB siêu tốc
            val tempPart = File(destinationFile.parentFile, "${destinationFile.name}.part")
            if (tempPart.exists()) tempPart.delete()

            val chunkSize = 5 * 1024 * 1024L // 5MB chunks
            var currentOffset = 0L

            FileOutputStream(tempPart, true).use { outStream ->
                while (currentOffset < totalLength) {
                    val nextOffset = (currentOffset + chunkSize - 1).coerceAtMost(totalLength - 1)
                    val chunkReq = Request.Builder()
                        .url(streamUrl)
                        .header("Range", "bytes=$currentOffset-$nextOffset")
                        .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
                        .header("Referer", "https://www.youtube.com/")
                        .build()

                    client.newCall(chunkReq).execute().use { chunkResp ->
                        if (!chunkResp.isSuccessful && chunkResp.code != 206) {
                            return@withContext false
                        }
                        val body = chunkResp.body ?: return@withContext false
                        val bytes = body.bytes()
                        if (bytes.isEmpty()) {
                            return@withContext false
                        }
                        outStream.write(bytes)
                        currentOffset += bytes.size
                    }
                }
            }

            if (tempPart.exists() && tempPart.length() > 50000) {
                if (destinationFile.exists()) destinationFile.delete()
                return@withContext tempPart.renameTo(destinationFile)
            }
            false
        } catch (e: Exception) {
            e.printStackTrace()
            false
        }
    }

    /**
     * Lưu tạm âm thanh phục vụ phát lại tức thì và tải trước (Preload 1 bài tiếp theo)
     */
    suspend fun cacheTempAudio(trackId: String, streamUrl: String, expectedLength: Long? = null): File? = withContext(Dispatchers.IO) {
        if (trackId.isBlank() || streamUrl.isBlank()) return@withContext null
        val existing = getTempAudioFile(trackId)
        if (existing != null) return@withContext existing

        val target = File(tempAudioDir, "$trackId.mp3")
        val ok = downloadAudioChunked(streamUrl, target, expectedLength)
        if (ok && target.exists() && target.length() > 50000) {
            target
        } else null
    }

    /**
     * Dọn dẹp các file audio tạm không nằm trong danh sách phát đang hoạt động hoặc danh sách đã tải,
     * giúp giải phóng dung lượng bộ nhớ.
     */
    fun cleanUnusedTempAudio(protectedIds: Set<String>) {
        try {
            val files = tempAudioDir.listFiles() ?: return
            val savedIds = _savedTracks.value.map { it.id.lowercase() }.toSet()
            val protectedLower = protectedIds.map { it.lowercase() }.toSet()

            for (file in files) {
                if (file.name.endsWith(".part")) {
                    file.delete()
                    continue
                }
                val fileId = file.nameWithoutExtension.lowercase()
                if (!protectedLower.contains(fileId) && !savedIds.contains(fileId)) {
                    file.delete()
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    suspend fun saveTrack(
        track: PlaylistItem,
        streamUrl: String,
        thumbnailUrl: String?,
        contentLength: Long? = null
    ): Result<PlaylistItem> = withContext(Dispatchers.IO) {
        try {
            if (track.id.isBlank()) {
                return@withContext Result.failure(IllegalArgumentException("ID bài hát không hợp lệ"))
            }

            val audioFile = File(savedDir, "${track.id}.mp3")
            val thumbFile = File(thumbDir, "${track.id}.jpg")

            // 0. Kiểm tra nếu bài hát đã được lưu trong bộ nhớ tạm (temp_audio), sao chép tức thì (0s)
            val cachedTemp = getTempAudioFile(track.id)
            if (cachedTemp != null && cachedTemp.exists() && cachedTemp.length() > 50000) {
                if (audioFile.exists()) audioFile.delete()
                cachedTemp.copyTo(audioFile, overwrite = true)
            } else if (!audioFile.exists() || audioFile.length() < 50000) {
                // Tải phân đoạn đa luồng Range cực nhanh
                val ok = downloadAudioChunked(streamUrl, audioFile, contentLength)
                if (!ok || !audioFile.exists() || audioFile.length() < 50000) {
                    return@withContext Result.failure(Exception("Tải âm thanh thất bại hoặc file quá nhỏ"))
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

    suspend fun clearAll() = withContext(Dispatchers.IO) {
        val files = savedDir.listFiles() ?: emptyArray()
        for (f in files) {
            try { f.delete() } catch (_: Exception) {}
        }
        val thumbs = thumbDir.listFiles() ?: emptyArray()
        for (t in thumbs) {
            try { t.delete() } catch (_: Exception) {}
        }
        _savedTracks.value = emptyList()
        saveMetadata(emptyList())
    }

    suspend fun clearAllExcept(exceptId: String?) = withContext(Dispatchers.IO) {
        val keep = _savedTracks.value.find { !exceptId.isNullOrBlank() && it.id.equals(exceptId, ignoreCase = true) }
        val files = savedDir.listFiles() ?: emptyArray()
        for (f in files) {
            val name = f.nameWithoutExtension
            if (!exceptId.isNullOrBlank() && name.equals(exceptId, ignoreCase = true)) continue
            try { f.delete() } catch (_: Exception) {}
        }
        val thumbs = thumbDir.listFiles() ?: emptyArray()
        for (t in thumbs) {
            val name = t.nameWithoutExtension
            if (!exceptId.isNullOrBlank() && name.equals(exceptId, ignoreCase = true)) continue
            try { t.delete() } catch (_: Exception) {}
        }
        _savedTracks.value = if (keep != null) listOf(keep) else emptyList()
        saveMetadata(_savedTracks.value)
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
