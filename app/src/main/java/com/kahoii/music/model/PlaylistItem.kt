package com.kahoii.music.model

import com.google.gson.annotations.SerializedName
import java.io.File
import java.io.Serializable
import java.util.Locale

data class PlaylistItem(
    @SerializedName("id")
    val id: String,

    @SerializedName("title")
    val title: String = "",

    @SerializedName("artist")
    val artist: String = "",

    @SerializedName("durationSeconds")
    val durationSeconds: Int = 0,

    @SerializedName("thumbnailUrl")
    val customThumbnailUrl: String? = null,

    @SerializedName("localAudioPath")
    val localAudioPath: String? = null
) : Serializable {

    val isOffline: Boolean
        get() = !localAudioPath.isNullOrBlank() && File(localAudioPath).exists()

    val url: String
        get() = if (isOffline) localAudioPath!! else "https://www.youtube.com/watch?v=$id"

    val thumbnailUrl: String
        get() = customThumbnailUrl ?: "https://img.youtube.com/vi/$id/hqdefault.jpg"

    val formattedDuration: String
        get() {
            if (durationSeconds <= 0) return "--:--"
            val hours = durationSeconds / 3600
            val minutes = (durationSeconds % 3600) / 60
            val seconds = durationSeconds % 60
            return if (hours > 0) {
                String.format(Locale.getDefault(), "%d:%02d:%02d", hours, minutes, seconds)
            } else {
                String.format(Locale.getDefault(), "%02d:%02d", minutes, seconds)
            }
        }
}
