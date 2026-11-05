package com.kahoii.music.util

import java.text.Normalizer
import java.util.regex.Pattern

object ArtistUtils {

    /**
     * Cleans common YouTube channel suffixes such as " - Topic" and "VEVO".
     */
    fun cleanArtist(artist: String?): String {
        if (artist.isNullOrBlank()) return ""
        var s = artist.trim()
        s = s.replace(Regex("\\s*-\\s*Topic$", RegexOption.IGNORE_CASE), "")
        s = s.replace(Regex("\\s*VEVO$", RegexOption.IGNORE_CASE), "")
        return s.trim()
    }

    /**
     * Per user instruction, author directly takes the YouTube channel name.
     * Title heuristics are disabled to prevent misattributions.
     */
    fun extractArtistFromTitle(title: String?): String {
        return ""
    }

    /**
     * Cleans song titles using Unicode NFKC to normalize stylized mathematical fonts
     * (e.g. 𝐏𝐥𝐚𝐲𝐥𝐢𝐬𝐭 -> Playlist, ① -> 1, fullwidth characters -> standard ASCII).
     */
    fun cleanDisplayTitle(title: String?): String {
        if (title.isNullOrBlank()) return ""
        val normalized = Normalizer.normalize(title, Normalizer.Form.NFKC)
        return normalized.trim()
    }

    /**
     * Removes Vietnamese diacritics and converts to lowercase for accent-free search.
     * e.g. "Sơn Tùng M-TP" -> "son tung m-tp", "Đã Tải" -> "da tai"
     */
    fun removeDiacritics(input: String?): String {
        if (input.isNullOrBlank()) return ""
        val str = input.replace('đ', 'd').replace('Đ', 'D')
        val normalized = Normalizer.normalize(str, Normalizer.Form.NFD)
        return Pattern.compile("\\p{InCombiningDiacriticalMarks}+").matcher(normalized).replaceAll("").lowercase().trim()
    }
}
