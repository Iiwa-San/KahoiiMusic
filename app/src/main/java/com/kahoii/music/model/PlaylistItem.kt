package com.kahoii.music.model

import com.google.gson.annotations.SerializedName
import java.io.File
import java.io.Serializable
import java.util.Locale

data class PlaylistItem(
    @SerializedName("id", alternate = ["Id"])
    val id: String = "",

    @SerializedName("title", alternate = ["Title"])
    val title: String = "",

    @SerializedName("artist", alternate = ["Artist"])
    val artist: String = "",

    @SerializedName("durationSeconds", alternate = ["DurationSeconds"])
    val durationSeconds: Int = 0,

    @SerializedName("thumbnailUrl", alternate = ["ThumbnailUrl"])
    val customThumbnailUrl: String? = null,

    @SerializedName("localAudioPath", alternate = ["LocalAudioPath"])
    val localAudioPath: String? = null
) : Serializable {

    val safeTitle: String
        get() = (title as? String?).orEmpty()

    val safeArtist: String
        get() = (artist as? String?).orEmpty()

    val safeId: String
        get() = (id as? String?).orEmpty()

    val isOffline: Boolean
        get() = !localAudioPath.isNullOrBlank() && File(localAudioPath).exists()

    val url: String
        get() = if (isOffline) localAudioPath!! else "https://www.youtube.com/watch?v=$safeId"

    val thumbnailUrl: String
        get() = customThumbnailUrl ?: "https://img.youtube.com/vi/$safeId/hqdefault.jpg"

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
