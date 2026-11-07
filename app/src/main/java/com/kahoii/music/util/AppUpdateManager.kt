package com.kahoii.music.util

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import android.widget.Toast
import androidx.core.content.FileProvider
import com.kahoii.music.BuildConfig
import com.kahoii.music.model.AppUpdateInfo
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.util.concurrent.TimeUnit

object AppUpdateManager {

    private const val PRIMARY_VERSION_URL =
        "https://raw.githubusercontent.com/Iiwa-San/KahoiiMusic/AndroidApp/version.json"
    private const val FALLBACK_VERSION_URL =
        "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/version.json"

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .followRedirects(true)
        .followSslRedirects(true)
        .build()

    private val downloadClient = httpClient.newBuilder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(180, TimeUnit.SECONDS)
        .build()

    /**
     * Fetch remote version info from GitHub.
     */
    suspend fun fetchRemoteVersion(): AppUpdateInfo? = withContext(Dispatchers.IO) {
        val urls = listOf(PRIMARY_VERSION_URL, FALLBACK_VERSION_URL)
        for (baseUrl in urls) {
            try {
                val urlWithBuster = "$baseUrl?t=${System.currentTimeMillis()}"
                val request = Request.Builder()
                    .url(urlWithBuster)
                    .header("Cache-Control", "no-cache, no-store, must-revalidate")
                    .header("Pragma", "no-cache")
                    .header("User-Agent", "KahoiiMusic-Android/${BuildConfig.VERSION_NAME}")
                    .build()

                httpClient.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use
                    val bodyStr = response.body?.string() ?: return@use
                    val json = JSONObject(bodyStr)
                    val versionCode = json.optInt("versionCode", 0)
                    val versionName = json.optString("versionName", "")
                    val changelog = json.optString("changelog", "")
                    val apkUrl = json.optString(
                        "apkUrl",
                        "https://github.com/Iiwa-San/KahoiiMusic/releases/download/latest-android/KahoiiMusic.apk"
                    )
                    val forceUpdate = json.optBoolean("forceUpdate", false)

                    if (versionCode > 0 && apkUrl.isNotBlank()) {
                        return@withContext AppUpdateInfo(
                            versionCode = versionCode,
                            versionName = versionName,
                            changelog = changelog,
                            apkUrl = apkUrl,
                            forceUpdate = forceUpdate
                        )
                    }
                }
            } catch (e: Exception) {
                // Try fallback URL
            }
        }
        null
    }

    /**
     * Check if a newer version exists compared to the current running app.
     */
    suspend fun checkForUpdate(): AppUpdateInfo? {
        val remoteInfo = fetchRemoteVersion() ?: return null
        val currentCode = BuildConfig.VERSION_CODE
        return if (remoteInfo.versionCode > currentCode) {
            remoteInfo
        } else {
            null
        }
    }

    /**
     * Download release APK to app's cache directory with streaming progress.
     */
    suspend fun downloadApk(
        context: Context,
        apkUrl: String,
        onProgress: (progress: Float, downloaded: Long, total: Long) -> Unit
    ): Result<File> = withContext(Dispatchers.IO) {
        try {
            val request = Request.Builder()
                .url(apkUrl)
                .header("User-Agent", "KahoiiMusic-Android/${BuildConfig.VERSION_NAME}")
                .build()

            val response = downloadClient.newCall(request).execute()
            if (!response.isSuccessful) {
                return@withContext Result.failure(Exception("Tải bản cập nhật thất bại (HTTP ${response.code})"))
            }

            val body = response.body ?: return@withContext Result.failure(Exception("Dữ liệu cập nhật trống"))
            val totalBytes = body.contentLength()

            val cacheDir = context.externalCacheDir ?: context.cacheDir
            val updateDir = File(cacheDir, "updates")
            if (!updateDir.exists()) updateDir.mkdirs()

            val apkFile = File(updateDir, "KahoiiMusic-update.apk")
            if (apkFile.exists()) {
                apkFile.delete()
            }

            body.byteStream().use { inputStream ->
                FileOutputStream(apkFile).use { outputStream ->
                    val buffer = ByteArray(16384)
                    var bytesDownloaded: Long = 0
                    var read: Int
                    var lastNotifyTime = 0L

                    while (inputStream.read(buffer).also { read = it } != -1) {
                        outputStream.write(buffer, 0, read)
                        bytesDownloaded += read

                        val now = System.currentTimeMillis()
                        if (now - lastNotifyTime > 80 || bytesDownloaded == totalBytes) {
                            lastNotifyTime = now
                            val progress = if (totalBytes > 0) {
                                (bytesDownloaded.toFloat() / totalBytes.toFloat()).coerceIn(0f, 1f)
                            } else 0f
                            withContext(Dispatchers.Main) {
                                onProgress(progress, bytesDownloaded, totalBytes)
                            }
                        }
                    }
                    outputStream.flush()
                }
            }

            Result.success(apkFile)
        } catch (e: Exception) {
            e.printStackTrace()
            Result.failure(e)
        }
    }

    /**
     * Check if app has permission to install unknown apps (Android 8.0+).
     */
    fun canRequestPackageInstalls(context: Context): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.packageManager.canRequestPackageInstalls()
        } else {
            true
        }
    }

    /**
     * Launch settings to request unknown app source install permission.
     */
    fun requestInstallPermission(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            try {
                val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES).apply {
                    data = Uri.parse("package:${context.packageName}")
                    addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                }
                context.startActivity(intent)
                Toast.makeText(
                    context,
                    "Vui lòng bật 'Cho phép từ nguồn này' để KahoiiMusic có thể tự cài đặt cập nhật",
                    Toast.LENGTH_LONG
                ).show()
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }

    /**
     * Trigger Android system package installer to install the downloaded APK.
     */
    fun installApk(context: Context, apkFile: File): Boolean {
        return try {
            if (!apkFile.exists()) {
                Toast.makeText(context, "Không tìm thấy file cài đặt cập nhật", Toast.LENGTH_SHORT).show()
                return false
            }

            if (!canRequestPackageInstalls(context)) {
                requestInstallPermission(context)
                return false
            }

            val apkUri: Uri = FileProvider.getUriForFile(
                context,
                "${context.packageName}.provider",
                apkFile
            )

            val intent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(apkUri, "application/vnd.android.package-archive")
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(intent)
            true
        } catch (e: Exception) {
            e.printStackTrace()
            Toast.makeText(context, "Không thể mở bộ cài đặt: ${e.message}", Toast.LENGTH_LONG).show()
            false
        }
    }
}

