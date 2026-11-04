package com.kahoii.music.util

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.google.gson.JsonDeserializationContext
import com.google.gson.JsonDeserializer
import com.google.gson.JsonElement
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.model.UserPlaylist
import java.lang.reflect.Type
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone
import java.util.UUID

class SafeLongDeserializer : JsonDeserializer<Long> {
    override fun deserialize(json: JsonElement?, typeOfT: Type?, context: JsonDeserializationContext?): Long {
        if (json == null || json.isJsonNull) return System.currentTimeMillis()
        try {
            if (json.isJsonPrimitive) {
                val prim = json.asJsonPrimitive
                if (prim.isNumber) {
                    return prim.asLong
                }
                val str = prim.asString.trim()
                if (str.isEmpty()) return System.currentTimeMillis()

                // 1. Numeric string ("1730623041000")
                str.toLongOrNull()?.let { return it }

                // 2. .NET JavaScriptSerializer format ("/Date(1730623041000)/")
                if (str.startsWith("/Date(") && str.endsWith(")/")) {
                    val sub = str.substring(6, str.length - 2).trim()
                    sub.toLongOrNull()?.let { return it }
                }

                // 3. ISO-8601 string ("2026-11-03T08:37:21.0248731Z" or variations)
                val patterns = arrayOf(
                    "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS'Z'",
                    "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS",
                    "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
                    "yyyy-MM-dd'T'HH:mm:ss.SSS",
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    "yyyy-MM-dd'T'HH:mm:ss"
                )
                for (p in patterns) {
                    try {
                        val sdf = SimpleDateFormat(p, Locale.US)
                        sdf.timeZone = TimeZone.getTimeZone("UTC")
                        val d = sdf.parse(str)
                        if (d != null) return d.time
                    } catch (_: Exception) {}
                }
            }
        } catch (_: Exception) {}
        return System.currentTimeMillis()
    }
}

class SafePlaylistItemDeserializer : JsonDeserializer<PlaylistItem> {
    override fun deserialize(json: JsonElement?, typeOfT: Type?, context: JsonDeserializationContext?): PlaylistItem {
        if (json == null || !json.isJsonObject) return PlaylistItem(id = "")
        val obj = json.asJsonObject
        val id = (obj.get("id") ?: obj.get("Id"))?.let { if (it.isJsonPrimitive) it.asString else "" } ?: ""
        val title = (obj.get("title") ?: obj.get("Title"))?.let { if (it.isJsonPrimitive) it.asString else "" } ?: ""
        val artist = (obj.get("artist") ?: obj.get("Artist"))?.let { if (it.isJsonPrimitive) it.asString else "" } ?: ""
        val durationSeconds = (obj.get("durationSeconds") ?: obj.get("DurationSeconds"))?.let {
            try {
                if (it.isJsonPrimitive) {
                    if (it.asJsonPrimitive.isNumber) it.asInt else it.asString.toIntOrNull() ?: 0
                } else 0
            } catch (_: Exception) { 0 }
        } ?: 0
        val thumbnailUrl = (obj.get("thumbnailUrl") ?: obj.get("ThumbnailUrl"))?.let { if (it.isJsonPrimitive) it.asString else null }
        val localAudioPath = (obj.get("localAudioPath") ?: obj.get("LocalAudioPath"))?.let { if (it.isJsonPrimitive) it.asString else null }

        return PlaylistItem(
            id = id,
            title = title,
            artist = artist,
            durationSeconds = durationSeconds,
            customThumbnailUrl = thumbnailUrl,
            localAudioPath = localAudioPath
        )
    }
}

class SafeUserPlaylistDeserializer : JsonDeserializer<UserPlaylist> {
    override fun deserialize(json: JsonElement?, typeOfT: Type?, context: JsonDeserializationContext?): UserPlaylist {
        if (json == null || !json.isJsonObject) return UserPlaylist()
        val obj = json.asJsonObject
        val id = (obj.get("id") ?: obj.get("Id"))?.let { if (it.isJsonPrimitive) it.asString else UUID.randomUUID().toString() } ?: UUID.randomUUID().toString()
        val name = (obj.get("name") ?: obj.get("Name"))?.let { if (it.isJsonPrimitive) it.asString else "" } ?: ""
        val createdAt = (obj.get("createdAt") ?: obj.get("CreatedAt"))?.let {
            if (it.isJsonPrimitive && it.asJsonPrimitive.isNumber) it.asLong else System.currentTimeMillis()
        } ?: System.currentTimeMillis()
        val itemsElement = obj.get("items") ?: obj.get("Items")
        val items = if (itemsElement != null && itemsElement.isJsonArray) {
            val list = mutableListOf<PlaylistItem>()
            for (elem in itemsElement.asJsonArray) {
                try {
                    val p: PlaylistItem? = context?.deserialize(elem, PlaylistItem::class.java)
                    if (p != null) list.add(p)
                } catch (_: Exception) {}
            }
            list
        } else emptyList()

        return UserPlaylist(
            id = id,
            name = name,
            createdAt = createdAt,
            items = items
        )
    }
}

object GsonUtils {
    val gson: Gson = GsonBuilder()
        .registerTypeAdapter(Long::class.java, SafeLongDeserializer())
        .registerTypeAdapter(java.lang.Long::class.java, SafeLongDeserializer())
        .registerTypeAdapter(Long::class.javaObjectType, SafeLongDeserializer())
        .registerTypeAdapter(PlaylistItem::class.java, SafePlaylistItemDeserializer())
        .registerTypeAdapter(UserPlaylist::class.java, SafeUserPlaylistDeserializer())
        .create()
}

