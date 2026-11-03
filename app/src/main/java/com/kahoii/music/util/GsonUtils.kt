package com.kahoii.music.util

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.google.gson.JsonDeserializationContext
import com.google.gson.JsonDeserializer
import com.google.gson.JsonElement
import java.lang.reflect.Type
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone

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

object GsonUtils {
    val gson: Gson = GsonBuilder()
        .registerTypeAdapter(Long::class.java, SafeLongDeserializer())
        .registerTypeAdapter(java.lang.Long::class.java, SafeLongDeserializer())
        .registerTypeAdapter(Long::class.javaObjectType, SafeLongDeserializer())
        .create()
}
