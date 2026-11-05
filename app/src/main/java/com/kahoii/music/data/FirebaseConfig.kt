package com.kahoii.music.data

import com.google.gson.annotations.SerializedName
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

fun currentIsoUtcTimestamp(): String {
    val sdf = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US)
    sdf.timeZone = TimeZone.getTimeZone("UTC")
    return sdf.format(Date())
}

object FirebaseConfig {
    const val API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8"
    const val PROJECT_ID = "kahoiimusic"
    const val AUTH_DOMAIN = "kahoiimusic.firebaseapp.com"
    const val STORAGE_BUCKET = "kahoiimusic.firebasestorage.app"

    const val AUTH_SIGN_IN_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=$API_KEY"
    const val AUTH_SIGN_UP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
    const val AUTH_LOOKUP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=$API_KEY"
    const val AUTH_PASSWORD_RESET_URL = "https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=$API_KEY"
    const val TOKEN_REFRESH_URL = "https://securetoken.googleapis.com/v1/token?key=$API_KEY"

    fun getFirestoreDocumentUrl(userId: String): String {
        return "https://firestore.googleapis.com/v1/projects/$PROJECT_ID/databases/(default)/documents/users/$userId"
    }
}

data class FirebaseUser(
    val localId: String,
    val email: String,
    val idToken: String,
    val refreshToken: String,
    val expiresIn: Long = 3600,
    val expiryTimestamp: Long = (System.currentTimeMillis() / 1000) + 3600 - 120
)

data class CloudMusicPayload(
    @SerializedName("playlist", alternate = ["playlists", "Playlists"])
    val playlist: List<com.kahoii.music.model.PlaylistItem> = emptyList(),

    @SerializedName("favorites", alternate = ["Favorites"])
    val favorites: List<com.kahoii.music.model.PlaylistItem> = emptyList(),

    @SerializedName("history", alternate = ["History"])
    val history: List<com.kahoii.music.model.PlaylistItem> = emptyList(),

    @SerializedName("savedTracks", alternate = ["SavedTracks"])
    val savedTracks: List<com.kahoii.music.model.PlaylistItem> = emptyList(),

    @SerializedName("collections", alternate = ["Collections"])
    val collections: List<com.kahoii.music.model.UserPlaylist> = emptyList(),

    @SerializedName("updatedAt", alternate = ["UpdatedAt"])
    val updatedAt: String = currentIsoUtcTimestamp()
)
