package com.kahoii.music.data

import com.google.gson.JsonObject
import com.kahoii.music.util.GsonUtils
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

class FirebaseSyncRepository {

    private val gson = GsonUtils.gson
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .build()

    suspend fun uploadMusicData(user: FirebaseUser, payload: CloudMusicPayload): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val musicDataJson = gson.toJson(payload)
            val docUrl = "${FirebaseConfig.getFirestoreDocumentUrl(user.localId)}?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt"

            val fields = JsonObject().apply {
                add("musicDataJson", JsonObject().apply {
                    addProperty("stringValue", musicDataJson)
                })
                add("updatedAt", JsonObject().apply {
                    addProperty("stringValue", payload.updatedAt)
                })
            }
            val requestBody = JsonObject().apply {
                add("fields", fields)
            }

            val request = Request.Builder()
                .url(docUrl)
                .patch(requestBody.toString().toRequestBody(jsonMediaType))
                .header("Authorization", "Bearer ${user.idToken}")
                .build()

            client.newCall(request).execute().use { response ->
                val body = response.body?.string().orEmpty()
                if (response.isSuccessful) {
                    Result.success(Unit)
                } else if (response.code == 404) {
                    Result.failure(Exception("Cơ sở dữ liệu Firestore chưa được tạo trên Firebase Console (Vui lòng vào Build -> Firestore Database -> Create Database)."))
                } else {
                    Result.failure(Exception("Lỗi đồng bộ đám mây (${response.code}): $body"))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối đồng bộ: ${e.localizedMessage ?: e.message}"))
        }
    }

    suspend fun downloadMusicData(user: FirebaseUser): Result<CloudMusicPayload?> = withContext(Dispatchers.IO) {
        try {
            val docUrl = FirebaseConfig.getFirestoreDocumentUrl(user.localId)
            val request = Request.Builder()
                .url(docUrl)
                .get()
                .header("Authorization", "Bearer ${user.idToken}")
                .build()

            client.newCall(request).execute().use { response ->
                if (response.code == 404) {
                    // Document does not exist yet (first-time user)
                    return@withContext Result.success(null)
                }

                val body = response.body?.string().orEmpty()
                if (response.isSuccessful) {
                    val root = gson.fromJson(body, JsonObject::class.java)
                    val fields = root.getAsJsonObject("fields")
                    val rawJson = fields?.getAsJsonObject("musicDataJson")?.get("stringValue")?.asString
                    if (!rawJson.isNullOrBlank()) {
                        val payload = gson.fromJson(rawJson, CloudMusicPayload::class.java)
                        Result.success(payload)
                    } else {
                        Result.success(null)
                    }
                } else {
                    Result.failure(Exception("Lỗi tải dữ liệu (${response.code}): $body"))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối tải dữ liệu: ${e.localizedMessage ?: e.message}"))
        }
    }
}
