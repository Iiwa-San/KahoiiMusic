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
import java.io.File

class HistoryRepository(private val context: Context) {

    private val gson = GsonUtils.gson
    private val historyFile = File(context.filesDir, "history.json")
    private val favoritesFile = File(context.filesDir, "favorites.json")

    var isLoggedIn: () -> Boolean = { false }

    private val _history = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val history: StateFlow<List<PlaylistItem>> = _history.asStateFlow()

    private val _favorites = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val favorites: StateFlow<List<PlaylistItem>> = _favorites.asStateFlow()

    init {
        CoroutineScope(Dispatchers.IO).launch {
            if (isLoggedIn()) {
                loadHistory()
            }
            loadFavorites()
        }
    }

    @Synchronized
    fun loadHistory() {
        if (!isLoggedIn()) {
            _history.value = emptyList()
            return
        }
        try {
            if (historyFile.exists()) {
                val json = historyFile.readText()
                val listType = object : TypeToken<List<PlaylistItem>>() {}.type
                val loadedList: List<PlaylistItem>? = gson.fromJson(json, listType)
                if (loadedList != null) {
                    _history.value = loadedList
                    return
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        _history.value = emptyList()
    }

    fun onLoggedOut() {
        _history.value = emptyList()
    }

    @Synchronized
    private fun saveHistory() {
        if (!isLoggedIn()) return
        try {
            val json = gson.toJson(_history.value)
            historyFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    @Synchronized
    private fun loadFavorites() {
        try {
            if (favoritesFile.exists()) {
                val json = favoritesFile.readText()
                val listType = object : TypeToken<List<PlaylistItem>>() {}.type
                val loadedList: List<PlaylistItem>? = gson.fromJson(json, listType)
                if (loadedList != null) {
                    _favorites.value = loadedList
                    return
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        _favorites.value = emptyList()
    }

    @Synchronized
    private fun saveFavorites() {
        try {
            val json = gson.toJson(_favorites.value)
            favoritesFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    suspend fun addTrack(track: PlaylistItem) = withContext(Dispatchers.IO) {
        if (!isLoggedIn()) return@withContext
        if (track.safeId.isBlank()) return@withContext
        val current = _history.value.toMutableList()
        val existing = current.find { it.id.equals(track.id, ignoreCase = true) }
        val finalTrack = if (existing != null && track.durationSeconds <= 0 && existing.durationSeconds > 0) {
            track.copy(durationSeconds = existing.durationSeconds)
        } else track

        current.removeAll { it.id.equals(track.id, ignoreCase = true) }
        current.add(0, finalTrack)
        if (current.size > 200) {
            current.removeAt(current.size - 1)
        }
        _history.value = current
        saveHistory()
    }

    suspend fun updateTrackInfoOnly(track: PlaylistItem) = withContext(Dispatchers.IO) {
        if (!isLoggedIn()) return@withContext
        if (track.safeId.isBlank()) return@withContext
        val current = _history.value.toMutableList()
        val index = current.indexOfFirst { it.id.equals(track.id, ignoreCase = true) }
        if (index >= 0) {
            val existing = current[index]
            val tTitle = track.safeTitle
            val tArtist = track.safeArtist
            val updated = existing.copy(
                title = if (tTitle.isNotBlank() && tTitle != "None" && !tTitle.startsWith("Bài ")) tTitle else existing.safeTitle,
                artist = if (tArtist.isNotBlank()) tArtist else existing.safeArtist,
                durationSeconds = if (track.durationSeconds > 0) track.durationSeconds else existing.durationSeconds,
                customThumbnailUrl = if (!track.customThumbnailUrl.isNullOrBlank()) track.customThumbnailUrl else existing.customThumbnailUrl
            )
            current[index] = updated
            _history.value = current
            saveHistory()
        }
    }

    suspend fun removeTrack(id: String) = withContext(Dispatchers.IO) {
        val current = _history.value.toMutableList()
        current.removeAll { it.id.equals(id, ignoreCase = true) }
        _history.value = current
        saveHistory()
    }

    suspend fun mergeHistory(cloudHistory: List<PlaylistItem>) = withContext(Dispatchers.IO) {
        if (!isLoggedIn()) return@withContext
        if (cloudHistory.isEmpty()) return@withContext
        val current = _history.value.toMutableList()
        for (track in cloudHistory) {
            if (track.safeId.isBlank()) continue
            if (!current.any { it.id.equals(track.id, ignoreCase = true) }) {
                current.add(track)
            }
        }
        val finalHistory = if (current.size > 200) current.take(200) else current
        _history.value = finalHistory
        saveHistory()
    }

    suspend fun mergeFavorites(cloudFavorites: List<PlaylistItem>) = withContext(Dispatchers.IO) {
        if (cloudFavorites.isEmpty()) return@withContext
        val current = _favorites.value.toMutableList()
        for (fav in cloudFavorites) {
            if (fav.safeId.isBlank()) continue
            if (!current.any { it.id.equals(fav.id, ignoreCase = true) }) {
                current.add(fav)
            }
        }
        _favorites.value = current
        saveFavorites()
    }

    suspend fun clearAll() = withContext(Dispatchers.IO) {
        _history.value = emptyList()
        _favorites.value = emptyList()
        saveHistory()
        saveFavorites()
    }

    suspend fun clearHistory() = withContext(Dispatchers.IO) {
        _history.value = emptyList()
        saveHistory()
    }

    suspend fun clearHistoryExcept(exceptId: String?) = withContext(Dispatchers.IO) {
        val keep = _history.value.find { !exceptId.isNullOrBlank() && it.id.equals(exceptId, ignoreCase = true) }
        _history.value = if (keep != null) listOf(keep) else emptyList()
        _favorites.value = emptyList()
        saveHistory()
        saveFavorites()
    }

    fun isFavorite(id: String): Boolean {
        if (id.isNullOrBlank()) return false
        return _favorites.value.any { it.id.equals(id, ignoreCase = true) }
    }

    suspend fun toggleFavorite(track: PlaylistItem): Boolean = withContext(Dispatchers.IO) {
        if (track.safeId.isBlank()) return@withContext false
        val current = _favorites.value.toMutableList()
        val existingIndex = current.indexOfFirst { it.id.equals(track.id, ignoreCase = true) }
        val isNowFavorite = if (existingIndex >= 0) {
            current.removeAt(existingIndex)
            false
        } else {
            current.add(0, track)
            true
        }
        _favorites.value = current
        saveFavorites()
        isNowFavorite
    }

    suspend fun removeFavorite(id: String) = withContext(Dispatchers.IO) {
        val current = _favorites.value.toMutableList()
        current.removeAll { it.id.equals(id, ignoreCase = true) }
        _favorites.value = current
        saveFavorites()
    }

    fun getTrackTitle(id: String): String? {
        if (id.isNullOrBlank()) return null
        val match = _favorites.value.find { it.id.equals(id, ignoreCase = true) }
            ?: _history.value.find { it.id.equals(id, ignoreCase = true) }
        return if (match != null && match.title.isNotBlank() && match.title != "None" && match.title != "NA") {
            match.title
        } else null
    }
}
