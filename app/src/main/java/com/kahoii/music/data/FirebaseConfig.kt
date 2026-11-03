package com.kahoii.music.data

object FirebaseConfig {
    const val API_KEY = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8"
    const val PROJECT_ID = "kahoiimusic"
    const val AUTH_DOMAIN = "kahoiimusic.firebaseapp.com"
    const val STORAGE_BUCKET = "kahoiimusic.firebasestorage.app"

    const val AUTH_SIGN_IN_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=$API_KEY"
    const val AUTH_SIGN_UP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
    const val AUTH_LOOKUP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=$API_KEY"

    fun getFirestoreDocumentUrl(userId: String): String {
        return "https://firestore.googleapis.com/v1/projects/$PROJECT_ID/databases/(default)/documents/users/$userId"
    }
}

data class FirebaseUser(
    val localId: String,
    val email: String,
    val idToken: String,
    val refreshToken: String,
    val expiresIn: Long = 3600
)

data class CloudMusicPayload(
    val playlist: List<com.kahoii.music.model.PlaylistItem> = emptyList(),
    val favorites: List<com.kahoii.music.model.PlaylistItem> = emptyList(),
    val history: List<com.kahoii.music.model.PlaylistItem> = emptyList(),
    val savedTracks: List<com.kahoii.music.model.PlaylistItem> = emptyList(),
    val collections: List<com.kahoii.music.model.UserPlaylist> = emptyList(),
    val updatedAt: Long = System.currentTimeMillis()
)
