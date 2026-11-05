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

class FirebaseSyncRepository(private val authRepository: FirebaseAuthRepository? = null) {

    private val gson = GsonUtils.gson
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .build()

    suspend fun uploadMusicData(user: FirebaseUser, payload: CloudMusicPayload): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val token = authRepository?.getValidToken() ?: user.idToken
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
                .header("Authorization", "Bearer $token")
                .build()

            var response = client.newCall(request).execute()
            var body = response.body?.string().orEmpty()

            if (response.code == 401 && authRepository != null) {
                response.close()
                val refreshed = authRepository.refreshToken()
                if (refreshed) {
                    val newToken = authRepository.getValidToken() ?: authRepository.currentUser.value?.idToken
                    if (!newToken.isNullOrBlank()) {
                        val retryRequest = Request.Builder()
                            .url(docUrl)
                            .patch(requestBody.toString().toRequestBody(jsonMediaType))
                            .header("Authorization", "Bearer $newToken")
                            .build()
                        response = client.newCall(retryRequest).execute()
                        body = response.body?.string().orEmpty()
                    }
                }
            }

            response.use {
                if (it.isSuccessful) {
                    Result.success(Unit)
                } else if (it.code == 404) {
                    Result.failure(Exception("Cơ sở dữ liệu Firestore chưa được tạo trên Firebase Console (Vui lòng vào Build -> Firestore Database -> Create Database)."))
                } else {
                    Result.failure(Exception("Lỗi đồng bộ đám mây (${it.code}): $body"))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối đồng bộ: ${e.localizedMessage ?: e.message}"))
        }
    }

    suspend fun downloadMusicData(user: FirebaseUser): Result<CloudMusicPayload?> = withContext(Dispatchers.IO) {
        try {
            val token = authRepository?.getValidToken() ?: user.idToken
            val docUrl = FirebaseConfig.getFirestoreDocumentUrl(user.localId)
            val request = Request.Builder()
                .url(docUrl)
                .get()
                .header("Authorization", "Bearer $token")
                .build()

            var response = client.newCall(request).execute()
            if (response.code == 401 && authRepository != null) {
                response.close()
                val refreshed = authRepository.refreshToken()
                if (refreshed) {
                    val newToken = authRepository.getValidToken() ?: authRepository.currentUser.value?.idToken
                    if (!newToken.isNullOrBlank()) {
                        val retryRequest = Request.Builder()
                            .url(docUrl)
                            .get()
                            .header("Authorization", "Bearer $newToken")
                            .build()
                        response = client.newCall(retryRequest).execute()
                    }
                }
            }

            response.use {
                if (it.code == 404) {
                    return@withContext Result.success(null)
                }

                val body = it.body?.string().orEmpty()
                if (it.isSuccessful) {
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
                    Result.failure(Exception("Lỗi tải dữ liệu (${it.code}): $body"))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối tải dữ liệu: ${e.localizedMessage ?: e.message}"))
        }
    }
}
