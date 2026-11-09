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
    var hasLyrics: Boolean = false,
    var isSynced: Boolean = false,
    var detectedLanguage: String? = null,
    var lines: List<LyricLine> = emptyList(),
    var plainText: String = ""
) {
    val hasRomanization: Boolean
        get() = lines.any { !it.romanizedText.isNullOrBlank() }

    companion object {
        fun empty(videoId: String = "") = LyricsData(videoId = videoId)
    }
}


