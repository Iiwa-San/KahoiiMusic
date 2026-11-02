package com.kahoii.music.data

import android.content.Context
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.model.UserPlaylist
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.util.UUID

class PlaylistCollectionRepository(private val context: Context) {

    private val gson = Gson()
    private val playlistsFile = File(context.filesDir, "playlists.json")

    private val _playlists = MutableStateFlow<List<UserPlaylist>>(emptyList())
    val playlists: StateFlow<List<UserPlaylist>> = _playlists.asStateFlow()

    init {
        CoroutineScope(Dispatchers.IO).launch {
            loadPlaylists()
        }
    }

    @Synchronized
    private fun loadPlaylists() {
        try {
            if (playlistsFile.exists()) {
                val json = playlistsFile.readText()
                val listType = object : TypeToken<List<UserPlaylist>>() {}.type
                val loadedList: List<UserPlaylist>? = gson.fromJson(json, listType)
                if (loadedList != null) {
                    _playlists.value = loadedList
                    return
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        _playlists.value = emptyList()
    }

    @Synchronized
    private fun savePlaylists() {
        try {
            val json = gson.toJson(_playlists.value)
            playlistsFile.writeText(json)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    suspend fun savePlaylist(name: String, items: List<PlaylistItem>): UserPlaylist = withContext(Dispatchers.IO) {
        val playlist = UserPlaylist(
            id = UUID.randomUUID().toString(),
            name = if (name.isBlank()) "Playlist" else name.trim(),
            createdAt = System.currentTimeMillis(),
            items = items.toList()
        )
        val current = _playlists.value.toMutableList()
        current.add(0, playlist)
        _playlists.value = current
        savePlaylists()
        playlist
    }

    suspend fun createPlaylist(name: String): UserPlaylist = withContext(Dispatchers.IO) {
        savePlaylist(name, emptyList())
    }

    suspend fun deletePlaylist(id: String) = withContext(Dispatchers.IO) {
        val current = _playlists.value.toMutableList()
        current.removeAll { it.id == id }
        _playlists.value = current
        savePlaylists()
    }

    suspend fun renamePlaylist(id: String, newName: String) = withContext(Dispatchers.IO) {
        val current = _playlists.value.toMutableList()
        val index = current.indexOfFirst { it.id == id }
        if (index != -1) {
            current[index] = current[index].copy(name = newName.trim())
            _playlists.value = current
            savePlaylists()
        }
    }

    suspend fun addTrackToPlaylist(playlistId: String, track: PlaylistItem) = withContext(Dispatchers.IO) {
        val current = _playlists.value.toMutableList()
        val index = current.indexOfFirst { it.id == playlistId }
        if (index != -1) {
            val p = current[index]
            val items = p.items.toMutableList()
            if (!items.any { it.id.equals(track.id, ignoreCase = true) }) {
                items.add(track)
                current[index] = p.copy(items = items)
                _playlists.value = current
                savePlaylists()
            }
        }
    }

    suspend fun removeTrackFromPlaylist(playlistId: String, trackId: String) = withContext(Dispatchers.IO) {
        val current = _playlists.value.toMutableList()
        val index = current.indexOfFirst { it.id == playlistId }
        if (index != -1) {
            val p = current[index]
            val items = p.items.toMutableList()
            items.removeAll { it.id.equals(trackId, ignoreCase = true) }
            current[index] = p.copy(items = items)
            _playlists.value = current
            savePlaylists()
        }
    }
}
