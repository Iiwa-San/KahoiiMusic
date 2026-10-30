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
import java.io.File

class HistoryRepository(private val context: Context) {

    private val gson = Gson()
    private val historyFile = File(context.filesDir, "history.json")
    private val favoritesFile = File(context.filesDir, "favorites.json")

    private val _history = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val history: StateFlow<List<PlaylistItem>> = _history.asStateFlow()

    private val _favorites = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val favorites: StateFlow<List<PlaylistItem>> = _favorites.asStateFlow()

    init {
        CoroutineScope(Dispatchers.IO).launch {
            loadHistory()
            loadFavorites()
        }
    }

    @Synchronized
    private fun loadHistory() {
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

    @Synchronized
    private fun saveHistory() {
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
        if (track.id.isBlank()) return@withContext
        val current = _history.value.toMutableList()
        current.removeAll { it.id.equals(track.id, ignoreCase = true) }
        current.add(0, track)
        if (current.size > 200) {
            current.removeAt(current.size - 1)
        }
        _history.value = current
        saveHistory()
    }

    suspend fun removeTrack(id: String) = withContext(Dispatchers.IO) {
        val current = _history.value.toMutableList()
        current.removeAll { it.id.equals(id, ignoreCase = true) }
        _history.value = current
        saveHistory()
    }

    suspend fun clearHistory() = withContext(Dispatchers.IO) {
        _history.value = emptyList()
        saveHistory()
    }

    fun isFavorite(id: String): Boolean {
        if (id.isBlank()) return false
        return _favorites.value.any { it.id.equals(id, ignoreCase = true) }
    }

    suspend fun toggleFavorite(track: PlaylistItem): Boolean = withContext(Dispatchers.IO) {
        if (track.id.isBlank()) return@withContext false
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
        if (id.isBlank()) return null
        val match = _favorites.value.find { it.id.equals(id, ignoreCase = true) }
            ?: _history.value.find { it.id.equals(id, ignoreCase = true) }
        return if (match != null && match.title.isNotBlank() && match.title != "None" && match.title != "NA") {
            match.title
        } else null
    }
}
