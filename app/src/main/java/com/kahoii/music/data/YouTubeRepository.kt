package com.kahoii.music.data

import com.kahoii.music.model.PlaylistItem
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.util.concurrent.TimeUnit
import java.util.regex.Pattern

data class AudioStreamResult(
    val videoId: String,
    val title: String,
    val artist: String,
    val durationSeconds: Int,
    val audioUrl: String,
    val thumbnailUrl: String,
    val artworkBytes: ByteArray? = null
)

class YouTubeRepository {

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    companion object {
        private val VIDEO_ID_REGEX = Pattern.compile(
            "^.*(?:youtu.be\\/|v\\/|e\\/|u\\/\\w+\\/|embed\\/|v=)([^#&?]*).*"
        )
        private val LIST_ID_REGEX = Pattern.compile("[?&]list=([a-zA-Z0-9_-]+)")
        private val JSON_MEDIA_TYPE = "application/json; charset=utf-8".toMediaType()

        fun extractVideoId(url: String?): String? {
            if (url.isNullOrBlank()) return null
            val trimmed = url.trim()

            // Direct 11-char ID
            if (trimmed.length == 11 && trimmed.matches(Regex("^[a-zA-Z0-9_-]{11}$"))) {
                return trimmed
            }

            // Shorts format
            val shortsMatch = Regex("youtube\\.com/shorts/([a-zA-Z0-9_-]{11})").find(trimmed)
            if (shortsMatch != null) {
                return shortsMatch.groupValues[1]
            }

            val matcher = VIDEO_ID_REGEX.matcher(trimmed)
            if (matcher.find()) {
                val id = matcher.group(1)
                if (!id.isNullOrBlank() && id.length == 11) {
                    return id
                }
            }
            return null
        }

        fun extractPlaylistId(url: String?): String? {
            if (url.isNullOrBlank()) return null
            val matcher = LIST_ID_REGEX.matcher(url.trim())
            if (matcher.find()) {
                return matcher.group(1)
            }
            return null
        }
    }

    fun fetchThumbnailBytes(url: String): ByteArray? {
        return try {
            val req = Request.Builder().url(url).build()
            client.newCall(req).execute().use { resp ->
                if (resp.isSuccessful) resp.body?.bytes() else null
            }
        } catch (e: Exception) {
            null
        }
    }

    suspend fun resolveAudio(input: String): Result<AudioStreamResult> = withContext(Dispatchers.IO) {
        val videoId = extractVideoId(input) ?: return@withContext Result.failure(
            IllegalArgumentException("Không tìm thấy Video ID hợp lệ")
        )

        // Try Method 1: YouTube VisionOS client with session visitor token (highest success rate, bypasses bot checks)
        try {
            val visionResult = fetchFromVisionOS(videoId)
            if (visionResult != null) return@withContext Result.success(visionResult)
        } catch (e: Exception) {
            e.printStackTrace()
        }

        // Try Method 2: YouTube InnerTube ANDROID_VR client
        try {
            val vrResult = fetchFromInnerTubeVR(videoId)
            if (vrResult != null) return@withContext Result.success(vrResult)
        } catch (e: Exception) {
            e.printStackTrace()
        }

        // Try Method 3: Piped API as fallback
        try {
            val pipedResult = fetchFromPiped(videoId)
            if (pipedResult != null) return@withContext Result.success(pipedResult)
        } catch (e: Exception) {
            e.printStackTrace()
        }

        // Try Method 4: Invidious API as secondary fallback
        try {
            val invidiousResult = fetchFromInvidious(videoId)
            if (invidiousResult != null) return@withContext Result.success(invidiousResult)
        } catch (e: Exception) {
            e.printStackTrace()
        }

        Result.failure(Exception("Không thể lấy luồng âm thanh từ YouTube"))
    }

    private fun fetchVisitorData(videoId: String): String? {
        return try {
            val req = Request.Builder()
                .url("https://www.youtube.com/watch?v=$videoId")
                .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
                .header("Accept-Language", "en-US,en;q=0.9")
                .build()

            client.newCall(req).execute().use { response ->
                if (!response.isSuccessful) return null
                val html = response.body?.string() ?: return null
                val match = Regex("\"VISITOR_DATA\":\"([^\"]+)\"").find(html)
                    ?: Regex("\"visitorData\":\"([^\"]+)\"").find(html)
                match?.groupValues?.get(1)
            }
        } catch (e: Exception) {
            null
        }
    }

    private fun fetchFromVisionOS(videoId: String): AudioStreamResult? {
        val visitorData = fetchVisitorData(videoId)

        val clientObj = JSONObject().apply {
            put("clientName", "VISIONOS")
            put("clientVersion", "1.02")
            put("deviceMake", "Apple")
            put("deviceModel", "RealityDevice17,1")
            put("userAgent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15")
            put("osName", "visionOS")
            put("osVersion", "26.5.23O471")
            put("hl", "en")
            put("timeZone", "UTC")
            put("utcOffsetMinutes", 0)
            if (!visitorData.isNullOrBlank()) {
                put("visitorData", visitorData)
            }
        }

        val jsonPayload = JSONObject().apply {
            put("context", JSONObject().apply {
                put("client", clientObj)
            })
            put("videoId", videoId)
            put("playbackContext", JSONObject().apply {
                put("contentPlaybackContext", JSONObject().apply {
                    put("html5Preference", "HTML5_PREF_WANTS")
                    put("signatureTimestamp", 20752)
                })
            })
            put("contentCheckOk", true)
            put("racyCheckOk", true)
        }

        val reqBuilder = Request.Builder()
            .url("https://www.youtube.com/youtubei/v1/player?prettyPrint=false")
            .post(jsonPayload.toString().toRequestBody(JSON_MEDIA_TYPE))
            .header("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15")
            .header("X-Youtube-Client-Name", "101")
            .header("X-Youtube-Client-Version", "1.02")
            .header("Origin", "https://www.youtube.com")
            .header("Cookie", "PREF=hl=en&tz=UTC")

        if (!visitorData.isNullOrBlank()) {
            reqBuilder.header("X-Goog-Visitor-Id", visitorData)
        }

        client.newCall(reqBuilder.build()).execute().use { response ->
            if (!response.isSuccessful) return null
            val body = response.body?.string() ?: return null
            val root = JSONObject(body)

            val playabilityStatus = root.optJSONObject("playabilityStatus")
            val status = playabilityStatus?.optString("status")
            if (status != "OK") return null

            val videoDetails = root.optJSONObject("videoDetails")
            val title = videoDetails?.optString("title", "Kahoii Music") ?: "Kahoii Music"
            val artist = videoDetails?.optString("author", "") ?: ""
            val durationSeconds = videoDetails?.optInt("lengthSeconds", 0) ?: 0

            val streamingData = root.optJSONObject("streamingData") ?: return null
            val adaptiveFormats = streamingData.optJSONArray("adaptiveFormats")

            var bestAudioUrl: String? = null
            var highestBitrate = 0

            if (adaptiveFormats != null) {
                for (i in 0 until adaptiveFormats.length()) {
                    val format = adaptiveFormats.getJSONObject(i)
                    val mimeType = format.optString("mimeType", "")
                    if (mimeType.startsWith("audio/")) {
                        val url = format.optString("url")
                        val bitrate = format.optInt("bitrate", 0)
                        if (url.isNotBlank() && bitrate > highestBitrate) {
                            highestBitrate = bitrate
                            bestAudioUrl = url
                        }
                    }
                }
            }

            // Fallback to HLS manifest if direct url not in adaptive formats
            if (bestAudioUrl.isNullOrBlank()) {
                val hlsUrl = streamingData.optString("hlsManifestUrl")
                if (hlsUrl.isNotBlank()) {
                    bestAudioUrl = hlsUrl
                }
            }

            if (!bestAudioUrl.isNullOrBlank()) {
                val thumbUrl = "https://img.youtube.com/vi/$videoId/hqdefault.jpg"
                val artBytes = fetchThumbnailBytes(thumbUrl)
                return AudioStreamResult(
                    videoId = videoId,
                    title = title,
                    artist = artist,
                    durationSeconds = durationSeconds,
                    audioUrl = bestAudioUrl,
                    thumbnailUrl = thumbUrl,
                    artworkBytes = artBytes
                )
            }
        }
        return null
    }

    private fun fetchFromInnerTubeVR(videoId: String): AudioStreamResult? {
        val jsonPayload = JSONObject().apply {
            put("videoId", videoId)
            put("context", JSONObject().apply {
                put("client", JSONObject().apply {
                    put("clientName", "ANDROID_VR")
                    put("clientVersion", "1.61.48")
                    put("deviceMake", "Oculus")
                    put("deviceModel", "Quest 3")
                    put("osName", "Android")
                    put("osVersion", "12")
                })
            })
        }

        val request = Request.Builder()
            .url("https://www.youtube.com/youtubei/v1/player")
            .post(jsonPayload.toString().toRequestBody(JSON_MEDIA_TYPE))
            .header("User-Agent", "com.google.android.apps.youtube.vr.oculus/1.61.48 (Linux; U; Android 12)")
            .build()

        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) return null
            val body = response.body?.string() ?: return null
            val root = JSONObject(body)

            val videoDetails = root.optJSONObject("videoDetails")
            val title = videoDetails?.optString("title", "Kahoii Music") ?: "Kahoii Music"
            val artist = videoDetails?.optString("author", "") ?: ""
            val durationSeconds = videoDetails?.optInt("lengthSeconds", 0) ?: 0

            val streamingData = root.optJSONObject("streamingData") ?: return null
            val adaptiveFormats = streamingData.optJSONArray("adaptiveFormats") ?: return null

            var bestAudioUrl: String? = null
            var highestBitrate = 0

            for (i in 0 until adaptiveFormats.length()) {
                val format = adaptiveFormats.getJSONObject(i)
                val mimeType = format.optString("mimeType", "")
                if (mimeType.startsWith("audio/")) {
                    val url = format.optString("url")
                    val bitrate = format.optInt("bitrate", 0)
                    if (url.isNotBlank() && bitrate > highestBitrate) {
                        highestBitrate = bitrate
                        bestAudioUrl = url
                    }
                }
            }

            if (!bestAudioUrl.isNullOrBlank()) {
                val thumbUrl = "https://img.youtube.com/vi/$videoId/hqdefault.jpg"
                val artBytes = fetchThumbnailBytes(thumbUrl)
                return AudioStreamResult(
                    videoId = videoId,
                    title = title,
                    artist = artist,
                    durationSeconds = durationSeconds,
                    audioUrl = bestAudioUrl,
                    thumbnailUrl = thumbUrl,
                    artworkBytes = artBytes
                )
            }
        }
        return null
    }

    private fun fetchFromPiped(videoId: String): AudioStreamResult? {
        val pipedEndpoints = listOf(
            "https://api.piped.privacydev.net/streams/$videoId",
            "https://pipedapi.tokhmi.xyz/streams/$videoId",
            "https://piped.video/api/v1/streams/$videoId"
        )

        for (endpoint in pipedEndpoints) {
            try {
                val request = Request.Builder()
                    .url(endpoint)
                    .header("User-Agent", "Mozilla/5.0")
                    .build()

                client.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use
                    val body = response.body?.string() ?: return@use
                    val root = JSONObject(body)

                    val title = root.optString("title", "Kahoii Music")
                    val artist = root.optString("uploader", "")
                    val duration = root.optInt("duration", 0)
                    val audioStreams = root.optJSONArray("audioStreams") ?: return@use

                    var bestUrl: String? = null
                    var highestBitrate = 0

                    for (i in 0 until audioStreams.length()) {
                        val stream = audioStreams.getJSONObject(i)
                        val url = stream.optString("url")
                        val bitrate = stream.optInt("bitrate", 0)
                        if (url.isNotBlank() && bitrate > highestBitrate) {
                            highestBitrate = bitrate
                            bestUrl = url
                        }
                    }

                    if (!bestUrl.isNullOrBlank()) {
                        return AudioStreamResult(
                            videoId = videoId,
                            title = title,
                            artist = artist,
                            durationSeconds = duration,
                            audioUrl = bestUrl,
                            thumbnailUrl = "https://img.youtube.com/vi/$videoId/hqdefault.jpg"
                        )
                    }
                }
            } catch (e: Exception) {
                // Try next piped endpoint
            }
        }
        return null
    }

    private fun fetchFromInvidious(videoId: String): AudioStreamResult? {
        val invidiousEndpoints = listOf(
            "https://inv.nadeko.net/api/v1/videos/$videoId",
            "https://invidious.nerdvpn.de/api/v1/videos/$videoId"
        )

        for (endpoint in invidiousEndpoints) {
            try {
                val request = Request.Builder()
                    .url(endpoint)
                    .header("User-Agent", "Mozilla/5.0")
                    .build()

                client.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use
                    val body = response.body?.string() ?: return@use
                    val root = JSONObject(body)

                    val title = root.optString("title", "Kahoii Music")
                    val artist = root.optString("author", "")
                    val duration = root.optInt("lengthSeconds", 0)
                    val adaptiveFormats = root.optJSONArray("adaptiveFormats") ?: return@use

                    var bestUrl: String? = null
                    var highestBitrate = 0

                    for (i in 0 until adaptiveFormats.length()) {
                        val format = adaptiveFormats.getJSONObject(i)
                        val type = format.optString("type", "")
                        if (type.startsWith("audio/")) {
                            val url = format.optString("url")
                            val bitrate = format.optInt("bitrate", 0)
                            if (url.isNotBlank() && bitrate > highestBitrate) {
                                highestBitrate = bitrate
                                bestUrl = url
                            }
                        }
                    }

                    if (!bestUrl.isNullOrBlank()) {
                        return AudioStreamResult(
                            videoId = videoId,
                            title = title,
                            artist = artist,
                            durationSeconds = duration,
                            audioUrl = bestUrl,
                            thumbnailUrl = "https://img.youtube.com/vi/$videoId/hqdefault.jpg"
                        )
                    }
                }
            } catch (e: Exception) {
                // Try next
            }
        }
        return null
    }

    suspend fun search(query: String): Result<List<PlaylistItem>> = withContext(Dispatchers.IO) {
        if (query.isBlank()) return@withContext Result.success(emptyList())

        try {
            val jsonPayload = JSONObject().apply {
                put("query", query)
                put("context", JSONObject().apply {
                    put("client", JSONObject().apply {
                        put("clientName", "WEB")
                        put("clientVersion", "2.20240101.00.00")
                        put("hl", "vi")
                        put("gl", "VN")
                    })
                })
            }

            val request = Request.Builder()
                .url("https://www.youtube.com/youtubei/v1/search")
                .post(jsonPayload.toString().toRequestBody(JSON_MEDIA_TYPE))
                .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext Result.failure(Exception("Lỗi tìm kiếm: HTTP ${response.code}"))
                val body = response.body?.string() ?: return@withContext Result.success(emptyList())
                val root = JSONObject(body)

                val items = mutableListOf<PlaylistItem>()

                val contents = root.optJSONObject("contents")
                    ?.optJSONObject("twoColumnSearchResultsRenderer")
                    ?.optJSONObject("primaryContents")
                    ?.optJSONObject("sectionListRenderer")
                    ?.optJSONArray("contents")

                if (contents != null && contents.length() > 0) {
                    val itemSection = contents.getJSONObject(0)
                        .optJSONObject("itemSectionRenderer")
                        ?.optJSONArray("contents")

                    if (itemSection != null) {
                        for (i in 0 until itemSection.length()) {
                            val itemObj = itemSection.getJSONObject(i)
                            val videoRenderer = itemObj.optJSONObject("videoRenderer") ?: continue

                            val videoId = videoRenderer.optString("videoId")
                            if (videoId.isBlank() || videoId.length != 11) continue

                            val title = videoRenderer.optJSONObject("title")
                                ?.optJSONArray("runs")
                                ?.optJSONObject(0)
                                ?.optString("text") ?: "Bài hát"

                            val artist = videoRenderer.optJSONObject("ownerText")
                                ?.optJSONArray("runs")
                                ?.optJSONObject(0)
                                ?.optString("text")
                                ?: videoRenderer.optJSONObject("shortBylineText")
                                    ?.optJSONArray("runs")
                                    ?.optJSONObject(0)
                                    ?.optString("text") ?: ""

                            val lengthText = videoRenderer.optJSONObject("lengthText")
                                ?.optString("simpleText", "") ?: ""

                            val durationSeconds = parseDurationToSeconds(lengthText)

                            items.add(
                                PlaylistItem(
                                    id = videoId,
                                    title = title,
                                    artist = artist,
                                    durationSeconds = durationSeconds
                                )
                            )
                        }
                    }
                }
                return@withContext Result.success(items)
            }
        } catch (e: Exception) {
            e.printStackTrace()
            return@withContext Result.failure(e)
        }
    }

    suspend fun resolvePlaylist(playlistUrlOrId: String, initialVideoId: String? = null): Result<List<PlaylistItem>> = withContext(Dispatchers.IO) {
        val listId = extractPlaylistId(playlistUrlOrId) ?: playlistUrlOrId.trim()
        val videoId = initialVideoId ?: extractVideoId(playlistUrlOrId)
        if (listId.isBlank()) return@withContext Result.failure(IllegalArgumentException("Mã Playlist không hợp lệ"))

        // Case A: YouTube Mix / Radio (listId starts with "RD")
        if (listId.startsWith("RD", ignoreCase = true)) {
            val mixItems = fetchMixFromInnerTube(listId, videoId)
            if (mixItems.isNotEmpty()) {
                return@withContext Result.success(mixItems)
            }
        }

        // Case B: Standard Playlist (PL..., OLAK..., etc.) via InnerTube Browse
        val browseItems = fetchPlaylistFromInnerTube(listId)
        if (browseItems.isNotEmpty()) {
            return@withContext Result.success(browseItems)
        }

        // Case C: Fallback to HTML scraping
        val htmlItems = fetchPlaylistFromHtml(listId)
        if (htmlItems.isNotEmpty()) {
            return@withContext Result.success(htmlItems)
        }

        Result.success(emptyList())
    }

    private fun fetchMixFromInnerTube(listId: String, videoId: String?): List<PlaylistItem> {
        return try {
            val payload = JSONObject().apply {
                put("context", JSONObject().apply {
                    put("client", JSONObject().apply {
                        put("clientName", "WEB")
                        put("clientVersion", "2.20240101.00.00")
                        put("hl", "vi")
                        put("gl", "VN")
                    })
                })
                put("playlistId", listId)
                if (!videoId.isNullOrBlank()) {
                    put("videoId", videoId)
                }
            }

            val request = Request.Builder()
                .url("https://www.youtube.com/youtubei/v1/next?prettyPrint=false")
                .post(payload.toString().toRequestBody(JSON_MEDIA_TYPE))
                .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return emptyList()
                val body = response.body?.string() ?: return emptyList()
                parsePlaylistFromRawJson(body)
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    private fun fetchPlaylistFromInnerTube(listId: String): List<PlaylistItem> {
        return try {
            val browseId = if (listId.startsWith("VL")) listId else "VL$listId"
            val payload = JSONObject().apply {
                put("context", JSONObject().apply {
                    put("client", JSONObject().apply {
                        put("clientName", "WEB")
                        put("clientVersion", "2.20240101.00.00")
                        put("hl", "vi")
                        put("gl", "VN")
                    })
                })
                put("browseId", browseId)
            }

            val request = Request.Builder()
                .url("https://www.youtube.com/youtubei/v1/browse?prettyPrint=false")
                .post(payload.toString().toRequestBody(JSON_MEDIA_TYPE))
                .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return emptyList()
                val body = response.body?.string() ?: return emptyList()
                parsePlaylistFromRawJson(body)
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    private fun fetchPlaylistFromHtml(listId: String): List<PlaylistItem> {
        return try {
            val url = "https://www.youtube.com/playlist?list=$listId"
            val request = Request.Builder()
                .url(url)
                .header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36")
                .header("Accept-Language", "vi,en;q=0.9")
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return emptyList()
                val html = response.body?.string() ?: return emptyList()
                parsePlaylistFromRawJson(html)
            }
        } catch (e: Exception) {
            emptyList()
        }
    }

    private fun parsePlaylistFromRawJson(rawJson: String): List<PlaylistItem> {
        val items = mutableListOf<PlaylistItem>()
        val seenIds = HashSet<String>()

        // 1. Try modern lockupViewModel (YouTube web playlist format)
        val lockupPattern = Pattern.compile(
            "\"lockupViewModel\":\\{.*?\"url\":\"https:\\/\\/i\\.ytimg\\.com\\/vi\\/([a-zA-Z0-9_-]{11})\\/.*?\"lockupMetadataViewModel\":\\{\"title\":\\{\"content\":\"([^\"]+)\".*?\"metadataParts\":\\[\\{\"text\":\\{\"content\":\"([^\"]+)\""
        )
        val lockupMatcher = lockupPattern.matcher(rawJson)
        while (lockupMatcher.find()) {
            val id = lockupMatcher.group(1) ?: continue
            if (seenIds.add(id)) {
                val title = unescapeJsonString(lockupMatcher.group(2) ?: "Bài hát")
                val artist = unescapeJsonString(lockupMatcher.group(3) ?: "")
                items.add(PlaylistItem(id = id, title = title, artist = artist, durationSeconds = 0))
            }
        }

        // 2. Try playlistPanelVideoRenderer (Mix / Radio format)
        if (items.isEmpty()) {
            val mixPattern = Pattern.compile(
                "\"playlistPanelVideoRenderer\":\\{\"title\":\\{.*?\"simpleText\":\"([^\"]+)\".*?\"runs\":\\[\\{\"text\":\"([^\"]+)\".*?\"videoId\":\"([a-zA-Z0-9_-]{11})\""
            )
            val mixMatcher = mixPattern.matcher(rawJson)
            while (mixMatcher.find()) {
                val id = mixMatcher.group(3) ?: continue
                if (seenIds.add(id)) {
                    val title = unescapeJsonString(mixMatcher.group(1) ?: "Bài hát")
                    val artist = unescapeJsonString(mixMatcher.group(2) ?: "")
                    items.add(PlaylistItem(id = id, title = title, artist = artist, durationSeconds = 0))
                }
            }
        }

        // 3. Try standard playlistVideoRenderer
        if (items.isEmpty()) {
            val pvrPattern = Pattern.compile(
                "\"playlistVideoRenderer\":\\{\"videoId\":\"([a-zA-Z0-9_-]{11})\".*?\"title\":\\{(?:\"runs\":\\[\\{\"text\":\"([^\"]+)\"\\}|\"simpleText\":\"([^\"]+)\")"
            )
            val pvrMatcher = pvrPattern.matcher(rawJson)
            while (pvrMatcher.find()) {
                val id = pvrMatcher.group(1) ?: continue
                if (seenIds.add(id)) {
                    val title = unescapeJsonString(pvrMatcher.group(2) ?: pvrMatcher.group(3) ?: "Bài hát")
                    items.add(PlaylistItem(id = id, title = title, artist = "", durationSeconds = 0))
                }
            }
        }

        // 4. Fallback: extract any 11-char videoId occurrences if no titles found
        if (items.isEmpty()) {
            val genericPattern = Pattern.compile("\"videoId\":\"([a-zA-Z0-9_-]{11})\"")
            val genericMatcher = genericPattern.matcher(rawJson)
            var count = 1
            while (genericMatcher.find()) {
                val id = genericMatcher.group(1) ?: continue
                if (seenIds.add(id)) {
                    items.add(PlaylistItem(id = id, title = "Bài $count", artist = "", durationSeconds = 0))
                    count++
                }
            }
        }

        return items
    }

    private fun unescapeJsonString(input: String): String {
        val sb = StringBuilder()
        var i = 0
        while (i < input.length) {
            if (input[i] == '\\' && i + 1 < input.length) {
                when (input[i + 1]) {
                    'u' -> {
                        if (i + 5 < input.length) {
                            val hex = input.substring(i + 2, i + 6)
                            try {
                                sb.append(hex.toInt(16).toChar())
                                i += 6
                                continue
                            } catch (_: Exception) {
                            }
                        }
                    }
                    'n' -> { sb.append('\n'); i += 2; continue }
                    'r' -> { sb.append('\r'); i += 2; continue }
                    't' -> { sb.append('\t'); i += 2; continue }
                    '"' -> { sb.append('"'); i += 2; continue }
                    '\\' -> { sb.append('\\'); i += 2; continue }
                    '/' -> { sb.append('/'); i += 2; continue }
                }
            }
            sb.append(input[i])
            i++
        }
        return sb.toString()
    }

    suspend fun fetchVideoTitle(videoId: String): String? = withContext(Dispatchers.IO) {
        try {
            val oembedUrl = "https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v=$videoId&format=json"
            val request = Request.Builder().url(oembedUrl).build()
            client.newCall(request).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string() ?: return@use
                    val json = JSONObject(body)
                    if (json.has("title")) {
                        return@withContext json.getString("title")
                    }
                }
            }
        } catch (e: Exception) {
            // Ignore
        }
        null
    }

    private fun parseDurationToSeconds(durationStr: String): Int {
        if (durationStr.isBlank()) return 0
        val parts = durationStr.split(":").mapNotNull { it.trim().toIntOrNull() }
        return when (parts.size) {
            1 -> parts[0]
            2 -> parts[0] * 60 + parts[1]
            3 -> parts[0] * 3600 + parts[1] * 60 + parts[2]
            else -> 0
        }
    }
}

