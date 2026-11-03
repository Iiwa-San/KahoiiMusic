package com.kahoii.music.data

import android.content.Context
import com.google.gson.Gson
import com.google.gson.JsonObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

class FirebaseAuthRepository(private val context: Context) {

    private val prefs = context.getSharedPreferences("kahoii_auth_prefs", Context.MODE_PRIVATE)
    private val gson = Gson()
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .build()

    private val _currentUser = MutableStateFlow<FirebaseUser?>(null)
    val currentUser: StateFlow<FirebaseUser?> = _currentUser.asStateFlow()

    init {
        loadSavedUser()
    }

    private fun loadSavedUser() {
        val json = prefs.getString("user_json", null)
        if (!json.isNullOrBlank()) {
            try {
                val user = gson.fromJson(json, FirebaseUser::class.java)
                _currentUser.value = user
            } catch (_: Exception) {}
        }
    }

    private fun saveUser(user: FirebaseUser?) {
        _currentUser.value = user
        if (user != null) {
            prefs.edit().putString("user_json", gson.toJson(user)).apply()
        } else {
            prefs.edit().remove("user_json").apply()
        }
    }

    suspend fun signUp(email: String, password: String): Result<FirebaseUser> = withContext(Dispatchers.IO) {
        val trimmedEmail = email.trim()
        val trimmedPassword = password.trim()

        if (trimmedEmail.isBlank() || !trimmedEmail.contains("@")) {
            return@withContext Result.failure(IllegalArgumentException("Vui lòng nhập địa chỉ email hợp lệ"))
        }
        if (trimmedPassword.length < 6) {
            return@withContext Result.failure(IllegalArgumentException("Mật khẩu phải có tối thiểu 6 ký tự"))
        }

        try {
            val payload = JsonObject().apply {
                addProperty("email", trimmedEmail)
                addProperty("password", trimmedPassword)
                addProperty("returnSecureToken", true)
            }

            val request = Request.Builder()
                .url(FirebaseConfig.AUTH_SIGN_UP_URL)
                .post(payload.toString().toRequestBody(jsonMediaType))
                .build()

            client.newCall(request).execute().use { response ->
                val body = response.body?.string().orEmpty()
                if (response.isSuccessful) {
                    val json = gson.fromJson(body, JsonObject::class.java)
                    val user = FirebaseUser(
                        localId = json.get("localId").asString,
                        email = json.get("email").asString,
                        idToken = json.get("idToken").asString,
                        refreshToken = json.get("refreshToken").asString,
                        expiresIn = json.get("expiresIn")?.asLong ?: 3600L
                    )
                    saveUser(user)
                    Result.success(user)
                } else {
                    Result.failure(Exception(parseAuthError(body)))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối máy chủ Firebase: ${e.localizedMessage ?: e.message}"))
        }
    }

    suspend fun signIn(email: String, password: String): Result<FirebaseUser> = withContext(Dispatchers.IO) {
        val trimmedEmail = email.trim()
        val trimmedPassword = password.trim()

        if (trimmedEmail.isBlank()) {
            return@withContext Result.failure(IllegalArgumentException("Vui lòng nhập email"))
        }
        if (trimmedPassword.isBlank()) {
            return@withContext Result.failure(IllegalArgumentException("Vui lòng nhập mật khẩu"))
        }

        try {
            val payload = JsonObject().apply {
                addProperty("email", trimmedEmail)
                addProperty("password", trimmedPassword)
                addProperty("returnSecureToken", true)
            }

            val request = Request.Builder()
                .url(FirebaseConfig.AUTH_SIGN_IN_URL)
                .post(payload.toString().toRequestBody(jsonMediaType))
                .build()

            client.newCall(request).execute().use { response ->
                val body = response.body?.string().orEmpty()
                if (response.isSuccessful) {
                    val json = gson.fromJson(body, JsonObject::class.java)
                    val user = FirebaseUser(
                        localId = json.get("localId").asString,
                        email = json.get("email").asString,
                        idToken = json.get("idToken").asString,
                        refreshToken = json.get("refreshToken").asString,
                        expiresIn = json.get("expiresIn")?.asLong ?: 3600L
                    )
                    saveUser(user)
                    Result.success(user)
                } else {
                    Result.failure(Exception(parseAuthError(body)))
                }
            }
        } catch (e: Exception) {
            Result.failure(Exception("Lỗi kết nối máy chủ Firebase: ${e.localizedMessage ?: e.message}"))
        }
    }

    fun signOut() {
        saveUser(null)
    }

    private fun parseAuthError(body: String): String {
        return try {
            val obj = gson.fromJson(body, JsonObject::class.java)
            val errorObj = obj.getAsJsonObject("error")
            val message = errorObj?.get("message")?.asString ?: "Lỗi xác thực không xác định"
            when {
                message.contains("CONFIGURATION_NOT_FOUND") ->
                    "Dịch vụ xác thực chưa được bật trên Firebase Console (Vui lòng vào Build -> Authentication -> Bật Email/Password)."
                message.contains("EMAIL_EXISTS") ->
                    "Email này đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác."
                message.contains("INVALID_LOGIN_CREDENTIALS") || message.contains("EMAIL_NOT_FOUND") || message.contains("INVALID_PASSWORD") ->
                    "Email hoặc mật khẩu không chính xác."
                message.contains("WEAK_PASSWORD") ->
                    "Mật khẩu quá yếu (cần tối thiểu 6 ký tự)."
                message.contains("INVALID_EMAIL") ->
                    "Địa chỉ email không đúng định dạng."
                message.contains("USER_DISABLED") ->
                    "Tài khoản này đã bị tạm khóa."
                message.contains("TOO_MANY_ATTEMPTS_TRY_LATER") ->
                    "Quá nhiều lần thử thất bại. Vui lòng thử lại sau ít phút."
                else -> "Lỗi xác thực: $message"
            }
        } catch (_: Exception) {
            "Lỗi kết nối Firebase hoặc dịch vụ tạm ngưng."
        }
    }
}
