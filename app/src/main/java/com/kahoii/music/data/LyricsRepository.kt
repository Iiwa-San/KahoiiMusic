package com.kahoii.music.data

import android.content.Context
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.kahoii.music.model.LyricLine
import com.kahoii.music.model.LyricsData
import com.kahoii.music.util.GsonUtils
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.URLEncoder
import java.util.concurrent.TimeUnit
import java.util.regex.Pattern
import kotlin.math.abs

class LyricsRepository(private val context: Context) {

    private val gson: Gson = GsonUtils.gson

    private val httpClient: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(10, TimeUnit.SECONDS)
        .followRedirects(true)
        .followSslRedirects(true)
        .build()

    private val lyricsCacheDir = File(context.cacheDir, "lyrics").apply {
        if (!exists()) mkdirs()
    }

    suspend fun fetchLyrics(
        videoId: String,
        title: String,
        artist: String,
        durationSeconds: Int
    ): LyricsData = withContext(Dispatchers.IO) {
        val cleanVid = videoId.trim()
        val cleanTitle = title.trim()
        val cleanArtist = artist.trim()

        if (cleanVid.isBlank() && cleanTitle.isBlank()) {
            return@withContext LyricsData.empty(cleanVid)
        }

        // 1. Check local disk cache
        if (cleanVid.isNotBlank()) {
            val cachedLrc = File(lyricsCacheDir, "$cleanVid.lrc")
            if (cachedLrc.exists()) {
                try {
                    val lrcContent = cachedLrc.readText(Charsets.UTF_8)
                    val data = parseLrc(lrcContent, cleanVid, cleanTitle, cleanArtist)
                    if (data != null && data.hasLyrics) {
                        attachCachedRomanization(cleanVid, data)
                        return@withContext data
                    }
                } catch (e: Exception) {
                    e.printStackTrace()
                }
            }

            // Check .txt cache
            val cachedTxt = File(lyricsCacheDir, "$cleanVid.txt")
            if (cachedTxt.exists()) {
                try {
                    val txtContent = cachedTxt.readText(Charsets.UTF_8)
                    val data = parsePlainText(txtContent, cleanVid, cleanTitle, cleanArtist)
                    if (data != null && data.hasLyrics) {
                        attachCachedRomanization(cleanVid, data)
                        return@withContext data
                    }
                } catch (e: Exception) {
                    e.printStackTrace()
                }
            }

            // Check .none marker (valid if written in last 30 minutes)
            val noneMarker = File(lyricsCacheDir, "$cleanVid.none")
            if (noneMarker.exists() && System.currentTimeMillis() - noneMarker.lastModified() < 30 * 60 * 1000L) {
                return@withContext LyricsData.empty(cleanVid)
            }
        }

        // 2. Fetch from LRCLIB API
        val lrclibResult = tryFetchFromLrclib(cleanVid, cleanTitle, cleanArtist, durationSeconds)
        if (lrclibResult != null && lrclibResult.hasLyrics && lrclibResult.isSynced) {
            cacheLyricsOnDisk(cleanVid, lrclibResult)
            fetchRomanizationIfNeeded(cleanVid, lrclibResult)
            return@withContext lrclibResult
        }

        // 3. Fallback to YouTube Music Innertube API
        if (cleanVid.length == 11) {
            try {
                val ytmResult = tryFetchFromYouTubeMusic(cleanVid, cleanTitle, cleanArtist)
                if (ytmResult != null && ytmResult.hasLyrics) {
                    cacheLyricsOnDisk(cleanVid, ytmResult)
                    fetchRomanizationIfNeeded(cleanVid, ytmResult)
                    return@withContext ytmResult
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }

        // 4. Fallback to unsynced LRCLIB result if available
        if (lrclibResult != null && lrclibResult.hasLyrics) {
            cacheLyricsOnDisk(cleanVid, lrclibResult)
            fetchRomanizationIfNeeded(cleanVid, lrclibResult)
            return@withContext lrclibResult
        }

        // 5. Mark as having no lyrics
        if (cleanVid.isNotBlank()) {
            try {
                File(lyricsCacheDir, "$cleanVid.none").writeText(System.currentTimeMillis().toString())
            } catch (_: Exception) {}
        }

        LyricsData.empty(cleanVid)
    }

    private fun tryFetchFromLrclib(
        videoId: String,
        title: String,
        artist: String,
        durationSeconds: Int
    ): LyricsData? {
        val candidates = buildSearchCandidates(title, artist)
        if (candidates.isEmpty()) return null

        var bestSyncedJson: JSONObject? = null
        var bestPlainJson: JSONObject? = null

        val limit = minOf(4, candidates.size)
        for (i in 0 until limit) {
            val cand = candidates[i]

            // Step A: /api/get with exact duration if artist available
            if (durationSeconds > 0 && cand.artist.isNotBlank()) {
                try {
                    val url = "https://lrclib.net/api/get?track_name=" +
                            URLEncoder.encode(cand.track, "UTF-8") +
                            "&artist_name=" + URLEncoder.encode(cand.artist, "UTF-8") +
                            "&duration=" + durationSeconds

                    val request = Request.Builder()
                        .url(url)
                        .header("User-Agent", "KahoiiMusic/2.0 (Android)")
                        .build()

                    httpClient.newCall(request).execute().use { response ->
                        if (response.isSuccessful) {
                            val bodyStr = response.body?.string()
                            if (!bodyStr.isNullOrBlank()) {
                                val json = JSONObject(bodyStr)
                                val isInst = json.optBoolean("instrumental", false)
                                if (!isInst) {
                                    val synced = json.optString("syncedLyrics", "")
                                    if (synced.isNotBlank()) {
                                        val data = parseLrc(synced, videoId, title, artist)
                                        if (data != null && data.hasLyrics) return data
                                    }
                                }
                            }
                        }
                    }
                } catch (_: Exception) {}
            }

            // Step B: /api/search?track_name=...
            try {
                var searchUrl = "https://lrclib.net/api/search?track_name=" +
                        URLEncoder.encode(cand.track, "UTF-8")
                if (cand.artist.isNotBlank()) {
                    searchUrl += "&artist_name=" + URLEncoder.encode(cand.artist, "UTF-8")
                }

                val request = Request.Builder()
                    .url(searchUrl)
                    .header("User-Agent", "KahoiiMusic/2.0 (Android)")
                    .build()

                httpClient.newCall(request).execute().use { response ->
                    if (response.isSuccessful) {
                        val bodyStr = response.body?.string()
                        if (!bodyStr.isNullOrBlank()) {
                            val arr = JSONArray(bodyStr)
                            for (j in 0 until arr.length()) {
                                val item = arr.getJSONObject(j)
                                val isInst = item.optBoolean("instrumental", false)
                                if (isInst) continue

                                val itemTrack = item.optString("trackName", "")
                                val itemArtist = item.optString("artistName", "")
                                val itemAlbum = item.optString("albumName", "")
                                val itemDur = item.optDouble("duration", 0.0).toInt()

                                if (!isTitleMatch(cand.track, itemTrack, cand.artist, itemArtist, itemAlbum)) {
                                    continue
                                }

                                if (durationSeconds > 0 && itemDur > 0 && abs(itemDur - durationSeconds) > 8) {
                                    continue
                                }

                                val synced = item.optString("syncedLyrics", "")
                                val plain = item.optString("plainLyrics", "")

                                if (synced.isNotBlank()) {
                                    if (durationSeconds <= 0 || itemDur <= 0 || abs(itemDur - durationSeconds) <= 6) {
                                        val data = parseLrc(synced, videoId, title, artist)
                                        if (data != null && data.hasLyrics) return data
                                    }
                                    if (bestSyncedJson == null) bestSyncedJson = item
                                } else if (plain.isNotBlank() && bestPlainJson == null) {
                                    bestPlainJson = item
                                }
                            }
                        }
                    }
                }
            } catch (_: Exception) {}
        }

        if (bestSyncedJson != null) {
            val synced = bestSyncedJson.optString("syncedLyrics", "")
            if (synced.isNotBlank()) {
                val data = parseLrc(synced, videoId, title, artist)
                if (data != null && data.hasLyrics) return data
            }
        }

        if (bestPlainJson != null) {
            val plain = bestPlainJson.optString("plainLyrics", "")
            if (plain.isNotBlank()) {
                val data = parsePlainText(plain, videoId, title, artist)
                if (data != null && data.hasLyrics) return data
            }
        }

        return null
    }

    private fun tryFetchFromYouTubeMusic(videoId: String, title: String, artist: String): LyricsData? {
        try {
            val nextUrl = "https://music.youtube.com/youtubei/v1/next"
            val payloadNext = "{\"context\":{\"client\":{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20230515.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}},\"videoId\":\"$videoId\"}"

            val reqNext = Request.Builder()
                .url(nextUrl)
                .post(payloadNext.toRequestBody("application/json; charset=utf-8".toMediaType()))
                .header("Origin", "https://music.youtube.com")
                .header("Referer", "https://music.youtube.com/watch?v=$videoId")
                .build()

            var browseId: String? = null
            httpClient.newCall(reqNext).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string() ?: ""
                    browseId = extractBrowseIdFromWatchNext(body)
                }
            }

            if (browseId.isNullOrBlank()) return null

            val browseUrl = "https://music.youtube.com/youtubei/v1/browse"
            val payloadBrowse = "{\"context\":{\"client\":{\"clientName\":\"WEB_REMIX\",\"clientVersion\":\"1.20230515.01.00\",\"hl\":\"vi\",\"gl\":\"VN\"}},\"browseId\":\"$browseId\"}"

            val reqBrowse = Request.Builder()
                .url(browseUrl)
                .post(payloadBrowse.toRequestBody("application/json; charset=utf-8".toMediaType()))
                .header("Origin", "https://music.youtube.com")
                .header("Referer", "https://music.youtube.com/watch?v=$videoId")
                .build()

            httpClient.newCall(reqBrowse).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string() ?: ""
                    val lyricsText = extractLyricsTextFromBrowse(body)
                    if (lyricsText.isNotBlank()) {
                        return parsePlainText(lyricsText, videoId, title, artist)
                    }
                }
            }
        } catch (_: Exception) {}
        return null
    }

    private fun extractBrowseIdFromWatchNext(json: String): String? {
        val m1 = Pattern.compile("\"browseId\"\\s*:\\s*\"(MPLY[a-zA-Z0-9_\\-]+)\"").matcher(json)
        if (m1.find()) return m1.group(1)

        val m2 = Pattern.compile("\"browseId\"\\s*:\\s*\"([^\"]+)\".*?\"MUSIC_PAGE_TYPE_TRACK_LYRICS\"", Pattern.DOTALL).matcher(json)
        if (m2.find()) return m2.group(1)
        return null
    }

    private fun extractLyricsTextFromBrowse(json: String): String {
        var m = Pattern.compile("\"musicDescriptionShelfRenderer\".*?\"description\"\\s*:\\s*\\{\\s*\"runs\"\\s*:\\s*\\[(.*?)\\]\\s*\\}", Pattern.DOTALL).matcher(json)
        if (!m.find()) {
            m = Pattern.compile("\"musicDescriptionShelfRenderer\".*?\"runs\"\\s*:\\s*\\[(.*?)\\]", Pattern.DOTALL).matcher(json)
            if (!m.find()) return ""
        }
        val runsJson = m.group(1) ?: return ""
        val tm = Pattern.compile("\"text\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"").matcher(runsJson)
        val sb = StringBuilder()
        while (tm.find()) {
            val raw = tm.group(1) ?: ""
            sb.append(unescapeJsonString(raw))
        }
        return sb.toString().trim()
    }

    private fun unescapeJsonString(s: String): String {
        return s.replace("\\n", "\n")
            .replace("\\r", "\r")
            .replace("\\t", "\t")
            .replace("\\\"", "\"")
            .replace("\\\\", "\\")
    }

    private fun cacheLyricsOnDisk(videoId: String, data: LyricsData) {
        if (videoId.isBlank() || !data.hasLyrics) return
        try {
            if (data.isSynced && data.plainText.isNotBlank()) {
                File(lyricsCacheDir, "$videoId.lrc").writeText(data.plainText, Charsets.UTF_8)
            } else if (data.plainText.isNotBlank()) {
                File(lyricsCacheDir, "$videoId.txt").writeText(data.plainText, Charsets.UTF_8)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    private fun attachCachedRomanization(videoId: String, data: LyricsData) {
        if (videoId.isBlank() || data.lines.isEmpty()) return
        try {
            val romFile = File(lyricsCacheDir, "${videoId}_romaji.json")
            if (romFile.exists()) {
                val json = romFile.readText(Charsets.UTF_8)
                val type = object : TypeToken<List<String>>() {}.type
                val list: List<String>? = gson.fromJson(json, type)
                if (list != null && list.size == data.lines.size) {
                    val updatedLines = data.lines.mapIndexed { idx, line ->
                        line.copy(romanizedText = list[idx].takeIf { it.isNotBlank() })
                    }
                    // mutate lines list in data
                    val field = data.javaClass.getDeclaredField("lines")
                    field.isAccessible = true
                    field.set(data, updatedLines)
                }
            }
        } catch (_: Exception) {}
    }

    private fun fetchRomanizationIfNeeded(videoId: String, data: LyricsData) {
        if (videoId.isBlank() || data.lines.isEmpty()) return

        val fullText = data.lines.joinToString(" ") { it.text }
        val kanaMatches = Pattern.compile("[\u3040-\u309F\u30A0-\u30FF]").matcher(fullText)
        var kanaCount = 0
        while (kanaMatches.find()) kanaCount++

        val hanziMatches = Pattern.compile("[\u4E00-\u9FFF]").matcher(fullText)
        var hanziCount = 0
        while (hanziMatches.find()) hanziCount++

        val lang = when {
            kanaCount >= 2 -> "ja"
            hanziCount >= 4 -> "zh-CN"
            else -> null
        } ?: return

        try {
            val batchSize = 25
            val romanizedLines = ArrayList<String>()

            for (start in data.lines.indices step batchSize) {
                val count = minOf(batchSize, data.lines.size - start)
                val batchTexts = (0 until count).map { i ->
                    val t = data.lines[start + i].text
                    if (t.isBlank()) "♪" else t.trim()
                }

                val batchQuery = batchTexts.joinToString(" ~ ")
                val url = "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=" +
                        lang + "&tl=en&dt=rm&q=" + URLEncoder.encode(batchQuery, "UTF-8")

                val request = Request.Builder().url(url).build()
                httpClient.newCall(request).execute().use { response ->
                    if (response.isSuccessful) {
                        val body = response.body?.string() ?: ""
                        val arr = JSONArray(body)
                        if (arr.length() > 0) {
                            val items = arr.optJSONArray(0)
                            if (items != null && items.length() > 0) {
                                val firstItem = items.optJSONArray(0)
                                val romStr = if (firstItem != null && firstItem.length() > 3) {
                                    firstItem.optString(3, "")
                                } else ""

                                val parts = romStr.split("~")
                                for (pIdx in 0 until count) {
                                    val r = if (pIdx < parts.size) parts[pIdx].trim() else ""
                                    if (r.isNotBlank() && r != "♪") {
                                        romanizedLines.add(r)
                                    } else {
                                        romanizedLines.add("")
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (romanizedLines.size == data.lines.size && romanizedLines.any { it.isNotBlank() }) {
                val updatedLines = data.lines.mapIndexed { idx, line ->
                    line.copy(romanizedText = romanizedLines[idx].takeIf { it.isNotBlank() })
                }
                val field = data.javaClass.getDeclaredField("lines")
                field.isAccessible = true
                field.set(data, updatedLines)

                File(lyricsCacheDir, "${videoId}_romaji.json").writeText(
                    gson.toJson(romanizedLines),
                    Charsets.UTF_8
                )
            }
        } catch (_: Exception) {}
    }

    companion object {
        fun parseLrc(lrcContent: String, videoId: String, title: String, artist: String): LyricsData? {
            if (lrcContent.isBlank()) return null
            val lines = mutableListOf<LyricLine>()
            val regex = Pattern.compile("\\[(\\d{1,2}):(\\d{1,2})(?:\\.(\\d{1,3}))?\\](.*)")

            lrcContent.lineSequence().forEach { line ->
                val trimmed = line.trim()
                if (trimmed.isNotBlank()) {
                    val m = regex.matcher(trimmed)
                    if (m.find()) {
                        val min = m.group(1)?.toIntOrNull() ?: 0
                        val sec = m.group(2)?.toIntOrNull() ?: 0
                        val msStr = m.group(3) ?: ""
                        val ms = when {
                            msStr.length == 2 -> (msStr.toIntOrNull() ?: 0) * 10
                            msStr.length == 1 -> (msStr.toIntOrNull() ?: 0) * 100
                            msStr.length >= 3 -> msStr.substring(0, 3).toIntOrNull() ?: 0
                            else -> 0
                        }
                        val tsMs = (min * 60 + sec) * 1000L + ms
                        val text = m.group(4)?.trim() ?: ""
                        lines.add(LyricLine(timestampMs = tsMs, text = text))
                    }
                }
            }

            if (lines.isEmpty()) return null
            lines.sortBy { it.timestampMs }

            return LyricsData(
                videoId = videoId,
                title = title,
                artist = artist,
                hasLyrics = true,
                isSynced = true,
                lines = lines,
                plainText = lrcContent
            )
        }

        fun parsePlainText(plainContent: String, videoId: String, title: String, artist: String): LyricsData? {
            if (plainContent.isBlank()) return null
            val lines = mutableListOf<LyricLine>()
            plainContent.lineSequence().forEach { line ->
                val trimmed = line.trim()
                if (trimmed.isNotBlank()) {
                    lines.add(LyricLine(timestampMs = 0L, text = trimmed))
                }
            }

            if (lines.isEmpty()) return null
            return LyricsData(
                videoId = videoId,
                title = title,
                artist = artist,
                hasLyrics = true,
                isSynced = false,
                lines = lines,
                plainText = plainContent
            )
        }

        private data class SearchCandidate(val track: String, val artist: String)

        private fun buildSearchCandidates(title: String, artist: String): List<SearchCandidate> {
            val list = mutableListOf<SearchCandidate>()
            if (title.isBlank()) return list

            val cleanT = cleanTitleForSearch(title)
            val cleanA = cleanArtistForSearch(artist)

            // 1. Japanese quotes
            val mSlash = Pattern.compile("^[「『【]([^/」』】]+)/([^」』】]+)[」』】]").matcher(title)
            if (mSlash.find()) {
                list.add(SearchCandidate(mSlash.group(1)!!.trim(), mSlash.group(2)!!.trim()))
                list.add(SearchCandidate(mSlash.group(2)!!.trim(), mSlash.group(1)!!.trim()))
            }

            val mJp = Pattern.compile("([^\\s「『]+)\\s*[「『]([^」』]+)[」』]").matcher(title)
            if (mJp.find()) {
                list.add(SearchCandidate(mJp.group(2)!!.trim(), mJp.group(1)!!.trim()))
                list.add(SearchCandidate(mJp.group(1)!!.trim(), mJp.group(2)!!.trim()))
            }

            val mJustTrack = Pattern.compile("[「『]([^」』]+)[」』]").matcher(title)
            if (mJustTrack.find()) {
                val inner = mJustTrack.group(1)!!.trim()
                if (inner.contains("/")) {
                    val p = inner.split("/", limit = 2)
                    list.add(SearchCandidate(p[0].trim(), p[1].trim()))
                    list.add(SearchCandidate(p[1].trim(), p[0].trim()))
                } else {
                    list.add(SearchCandidate(inner, cleanA))
                }
            }

            // 2. Delimiters: " | ", " - ", " / ", " – ", " — "
            val separators = arrayOf(" | ", " - ", " / ", " – ", " — ")
            for (sep in separators) {
                if (cleanT.contains(sep)) {
                    val parts = cleanT.split(sep).map { it.trim() }.filter { it.isNotBlank() }
                    if (parts.size >= 2) {
                        val p0 = parts[0]
                        val p1 = parts[1]
                        if (cleanA.isNotBlank() && areArtistsSimilar(p0, cleanA)) {
                            list.add(0, SearchCandidate(p1, cleanA))
                            list.add(1, SearchCandidate(p1, p0))
                        } else if (cleanA.isNotBlank() && areArtistsSimilar(p1, cleanA)) {
                            list.add(0, SearchCandidate(p0, cleanA))
                            list.add(1, SearchCandidate(p0, p1))
                        } else {
                            list.add(SearchCandidate(p0, p1))
                            list.add(SearchCandidate(p1, p0))
                            if (cleanA.isNotBlank()) {
                                list.add(SearchCandidate(p0, cleanA))
                                list.add(SearchCandidate(p1, cleanA))
                            }
                        }
                    }
                    break
                }
            }

            // 3. Clean track with clean artist
            if (cleanT.isNotBlank()) {
                if (cleanA.isNotBlank()) {
                    list.add(0, SearchCandidate(cleanT, cleanA))
                    if (cleanT.contains(cleanA, ignoreCase = true)) {
                        val withoutA = cleanT.replace(cleanA, "", ignoreCase = true)
                            .trim(' ', '-', '/', '|', '–', '—', '_')
                        if (withoutA.isNotBlank()) {
                            list.add(1, SearchCandidate(withoutA, cleanA))
                        }
                    }
                }
                list.add(SearchCandidate(cleanT, ""))
            }

            // Deduplicate
            val unique = mutableListOf<SearchCandidate>()
            val seen = mutableSetOf<String>()
            for (c in list) {
                val t = c.track.replace("\\s+".toRegex(), " ").trim()
                val a = c.artist.replace("\\s+".toRegex(), " ").trim()
                if (t.isBlank()) continue
                val key = "$t||$a".lowercase()
                if (seen.add(key)) {
                    unique.add(SearchCandidate(t, a))
                }
            }
            return unique
        }

        fun cleanTitleForSearch(rawTitle: String): String {
            if (rawTitle.isBlank()) return ""
            var s = rawTitle
            s = s.replace("#\\S+".toRegex(), " ")
            s = s.replace("(?:弾き語りで)?歌ってみた(?:のさ)?".toRegex(RegexOption.IGNORE_CASE), " ")
            s = s.replace("\\b(?:covered\\s+by|cover|acoustic\\s+ver|piano\\s+ver|full\\s+ver|short\\s+ver)\\b".toRegex(RegexOption.IGNORE_CASE), " ")
            s = s.replace("\\s*[\\[\\({【『「][^\\]\\)}】』」]*(?:MV|Official|Video|Audio|Lyric|Vietsub|Full|4K|1080p|HD|Sub|Prod|Visualizer|Teaser|Music Video|MusicVideo|OST|Soundtrack|Theme|Clip|wuwa|graduation)[^\\]\\)}】』」]*[\\]\\)}】』」]".toRegex(RegexOption.IGNORE_CASE), " ")
            s = s.replace("\\b(?:Official\\s+(?:Music\\s+)?Video|Official\\s+Audio|Lyric\\s+Video|Music\\s+Video|MV|Audio|Official\\s+Chinese\\s+Lyrics|Official\\s+English\\s+Lyrics)\\b".toRegex(RegexOption.IGNORE_CASE), " ")
            s = s.replace("\\s*(?:feat\\.|ft\\.|featuring)\\s+[^/| -]+".toRegex(RegexOption.IGNORE_CASE), " ")
            return s.replace("\\s+".toRegex(), " ").trim()
        }

        fun cleanArtistForSearch(rawArtist: String): String {
            if (rawArtist.isBlank()) return ""
            var s = rawArtist
            s = s.replace(" - Topic".toRegex(RegexOption.IGNORE_CASE), "")
            s = s.replace("Official Channel".toRegex(RegexOption.IGNORE_CASE), "")
            s = s.replace("Official".toRegex(RegexOption.IGNORE_CASE), "")
            s = s.replace("Channel".toRegex(RegexOption.IGNORE_CASE), "")
            return s.replace("\\s+".toRegex(), " ").trim()
        }

        fun normalizeForMatch(s: String): String {
            if (s.isBlank()) return ""
            return s.lowercase().replace("[\\s\\-_/|–—'\"~`\\.・,!?\\(\\)\\[\\]\\{\\}【】『』「」]+".toRegex(), "")
        }

        fun areArtistsSimilar(a1: String, a2: String): Boolean {
            if (a1.isBlank() || a2.isBlank()) return true
            val n1 = normalizeForMatch(a1)
            val n2 = normalizeForMatch(a2)
            if (n1.equals(n2, ignoreCase = true)) return true
            if (n1.contains(n2) || n2.contains(n1)) return true
            return false
        }

        fun isTitleMatch(
            targetTitle: String,
            resultTrack: String,
            targetArtist: String = "",
            resultArtist: String = "",
            resultAlbum: String = ""
        ): Boolean {
            if (targetTitle.isBlank() || resultTrack.isBlank()) return false
            val tTarget = normalizeForMatch(targetTitle)
            val tTrack = normalizeForMatch(resultTrack)
            val aTarget = normalizeForMatch(targetArtist)
            val aResult = normalizeForMatch(resultArtist)

            if (aTarget.isNotBlank() && aResult.isNotBlank() && !areArtistsSimilar(aTarget, aResult)) {
                return false
            }

            if (tTarget.equals(tTrack, ignoreCase = true)) return true
            if (tTarget.length >= 4 && tTrack.length >= 4) {
                if (tTarget.contains(tTrack) || tTrack.contains(tTarget)) {
                    val ratio = minOf(tTarget.length, tTrack.length).toDouble() / maxOf(tTarget.length, tTrack.length)
                    if (ratio >= 0.50) return true
                }
            }
            return false
        }
    }
}

