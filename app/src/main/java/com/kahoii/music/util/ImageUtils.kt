package com.kahoii.music.util

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.PorterDuff
import android.graphics.PorterDuffXfermode
import android.graphics.Rect
import android.util.Base64
import java.io.ByteArrayOutputStream

object ImageUtils {

    fun base64ToBitmap(base64Str: String?): Bitmap? {
        if (base64Str.isNullOrBlank()) return null
        return try {
            val bytes = Base64.decode(base64Str, Base64.DEFAULT)
            BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
        } catch (_: Exception) {
            null
        }
    }

    fun bitmapToBase64(bitmap: Bitmap, maxSize: Int = 180): String {
        val scaled = if (bitmap.width > maxSize || bitmap.height > maxSize) {
            val ratio = bitmap.width.toFloat() / bitmap.height.toFloat()
            val (targetW, targetH) = if (ratio > 1f) {
                maxSize to (maxSize / ratio).toInt().coerceAtLeast(1)
            } else {
                (maxSize * ratio).toInt().coerceAtLeast(1) to maxSize
            }
            Bitmap.createScaledBitmap(bitmap, targetW, targetH, true)
        } else {
            bitmap
        }

        val outputStream = ByteArrayOutputStream()
        scaled.compress(Bitmap.CompressFormat.PNG, 95, outputStream)
        val bytes = outputStream.toByteArray()
        return Base64.encodeToString(bytes, Base64.NO_WRAP)
    }

    fun cropToCircle(src: Bitmap): Bitmap {
        val size = minOf(src.width, src.height)
        val left = (src.width - size) / 2
        val top = (src.height - size) / 2

        val output = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(output)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG)
        val rect = Rect(left, top, left + size, top + size)
        val destRect = Rect(0, 0, size, size)

        canvas.drawARGB(0, 0, 0, 0)
        canvas.drawCircle(size / 2f, size / 2f, size / 2f, paint)
        paint.xfermode = PorterDuffXfermode(PorterDuff.Mode.SRC_IN)
        canvas.drawBitmap(src, rect, destRect, paint)
        return output
    }
}
