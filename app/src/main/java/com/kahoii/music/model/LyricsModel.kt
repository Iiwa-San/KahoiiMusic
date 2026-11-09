package com.kahoii.music.model

data class LyricLine(
    val timestampMs: Long,
    val text: String,
    val romanizedText: String? = null
)

data class LyricsData(
    val videoId: String = "",
    val title: String = "",
    val artist: String = "",
    val hasLyrics: Boolean = false,
    val isSynced: Boolean = false,
    val detectedLanguage: String? = null,
    val lines: List<LyricLine> = emptyList(),
    val plainText: String = ""
) {
    val hasRomanization: Boolean
        get() = lines.any { !it.romanizedText.isNullOrBlank() }

    companion object {
        fun empty(videoId: String = "") = LyricsData(videoId = videoId)
    }
}

