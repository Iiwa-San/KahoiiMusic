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
    private val settingsFile = File(context.filesDir, "settings.json")

    private val _history = MutableStateFlow<List<PlaylistItem>>(emptyList())
    val history: StateFlow<List<PlaylistItem>> = _history.asStateFlow()

    init {
        CoroutineScope(Dispatchers.IO).launch {
            loadHistory()
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

    suspend fun addTrack(track: PlaylistItem) = withContext(Dispatchers.IO) {
        if (track.id.isBlank()) return@withContext
        val current = _history.value.toMutableList()
        // Remove duplicate if already exists, then place at top
        current.removeAll { it.id.equals(track.id, ignoreCase = true) }
        current.add(0, track)
        // Keep up to 200 history items
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

    fun getTrackTitle(id: String): String? {
        if (id.isBlank()) return null
        val match = _history.value.find { it.id.equals(id, ignoreCase = true) }
        return if (match != null && match.title.isNotBlank() && match.title != "None" && match.title != "NA") {
            match.title
        } else null
    }
}

