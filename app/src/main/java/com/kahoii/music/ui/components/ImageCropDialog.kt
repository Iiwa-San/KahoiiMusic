package com.kahoii.music.ui.components

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.PorterDuff
import android.graphics.PorterDuffXfermode
import android.graphics.Rect
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.kahoii.music.ui.theme.NeonPink
import kotlin.math.max
import kotlin.math.roundToInt

@Composable
fun ImageCropDialog(
    sourceBitmap: Bitmap,
    onDismissRequest: () -> Unit,
    onCropConfirmed: (Bitmap) -> Unit
) {
    val density = LocalDensity.current
    val viewportSizeDp = 260.dp
    val circleDiameterDp = 220.dp

    val viewportSizePx = with(density) { viewportSizeDp.toPx() }
    val circleDiameterPx = with(density) { circleDiameterDp.toPx() }
    val circleRadiusPx = circleDiameterPx / 2f

    // Calculate base scale so image fills the circle
    val baseScale = max(
        circleDiameterPx / sourceBitmap.width.toFloat(),
        circleDiameterPx / sourceBitmap.height.toFloat()
    )

    var userZoom by remember { mutableFloatStateOf(1.0f) }
    var offsetX by remember { mutableFloatStateOf(0f) }
    var offsetY by remember { mutableFloatStateOf(0f) }

    val currentScale = baseScale * userZoom

    // Constrain offset so user cannot pan completely outside the circle
    val maxOffsetX = max(0f, (sourceBitmap.width * currentScale - circleDiameterPx) / 2f + 50f)
    val maxOffsetY = max(0f, (sourceBitmap.height * currentScale - circleDiameterPx) / 2f + 50f)

    Dialog(
        onDismissRequest = onDismissRequest,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.75f)),
            contentAlignment = Alignment.Center
        ) {
            Column(
                modifier = Modifier
                    .width(320.dp)
                    .clip(RoundedCornerShape(20.dp))
                    .background(Color(0xFF181328))
                    .padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Header
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Cắt Ảnh Đại Diện",
                        color = Color.White,
                        fontSize = 17.sp,
                        fontWeight = FontWeight.Bold
                    )
                    IconButton(
                        onClick = onDismissRequest,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Đóng",
                            tint = Color.White.copy(alpha = 0.7f),
                            modifier = Modifier.size(20.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Interactive Crop Viewport
                Box(
                    modifier = Modifier
                        .size(viewportSizeDp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(Color(0xFF0F0B18))
                        .pointerInput(currentScale) {
                            detectDragGestures { change, dragAmount ->
                                change.consume()
                                offsetX = (offsetX + dragAmount.x).coerceIn(-maxOffsetX, maxOffsetX)
                                offsetY = (offsetY + dragAmount.y).coerceIn(-maxOffsetY, maxOffsetY)
                            }
                        },
                    contentAlignment = Alignment.Center
                ) {
                    val composeBitmap = remember(sourceBitmap) { sourceBitmap.asImageBitmap() }

                    Canvas(modifier = Modifier.fillMaxSize()) {
                        val canvasW = size.width
                        val canvasH = size.height
                        val centerX = canvasW / 2f
                        val centerY = canvasH / 2f

                        // Draw source image scaled and translated
                        val imgW = sourceBitmap.width * currentScale
                        val imgH = sourceBitmap.height * currentScale
                        val imgLeft = (centerX + offsetX) - imgW / 2f
                        val imgTop = (centerY + offsetY) - imgH / 2f

                        drawImage(
                            image = composeBitmap,
                            dstOffset = IntOffset(imgLeft.roundToInt(), imgTop.roundToInt()),
                            dstSize = IntSize(imgW.roundToInt(), imgH.roundToInt())
                        )

                        // Draw dark overlay around circle
                        // Top rectangle
                        drawRect(
                            color = Color.Black.copy(alpha = 0.60f),
                            topLeft = Offset.Zero,
                            size = Size(canvasW, centerY - circleRadiusPx)
                        )
                        // Bottom rectangle
                        drawRect(
                            color = Color.Black.copy(alpha = 0.60f),
                            topLeft = Offset(0f, centerY + circleRadiusPx),
                            size = Size(canvasW, canvasH - (centerY + circleRadiusPx))
                        )
                        // Left rectangle
                        drawRect(
                            color = Color.Black.copy(alpha = 0.60f),
                            topLeft = Offset(0f, centerY - circleRadiusPx),
                            size = Size(centerX - circleRadiusPx, circleRadiusPx * 2f)
                        )
                        // Right rectangle
                        drawRect(
                            color = Color.Black.copy(alpha = 0.60f),
                            topLeft = Offset(centerX + circleRadiusPx, centerY - circleRadiusPx),
                            size = Size(canvasW - (centerX + circleRadiusPx), circleRadiusPx * 2f)
                        )

                        // Glowing gradient circle border
                        drawCircle(
                            brush = Brush.linearGradient(
                                colors = listOf(Color(0xFFFF2A85), Color(0xFF8A2BE2), Color(0xFF00E5FF))
                            ),
                            radius = circleRadiusPx,
                            center = Offset(centerX, centerY),
                            style = Stroke(width = 2.5.dp.toPx())
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Zoom Slider
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Text(
                        text = "🔍",
                        fontSize = 14.sp
                    )
                    Slider(
                        value = userZoom,
                        onValueChange = {
                            userZoom = it
                            val newScale = baseScale * it
                            val newMaxX = max(0f, (sourceBitmap.width * newScale - circleDiameterPx) / 2f + 50f)
                            val newMaxY = max(0f, (sourceBitmap.height * newScale - circleDiameterPx) / 2f + 50f)
                            offsetX = offsetX.coerceIn(-newMaxX, newMaxX)
                            offsetY = offsetY.coerceIn(-newMaxY, newMaxY)
                        },
                        valueRange = 1.0f..3.0f,
                        modifier = Modifier.weight(1f),
                        colors = SliderDefaults.colors(
                            thumbColor = NeonPink,
                            activeTrackColor = NeonPink,
                            inactiveTrackColor = Color.White.copy(alpha = 0.15f)
                        )
                    )
                    Text(
                        text = "${(userZoom * 100).toInt()}%",
                        color = Color.White.copy(alpha = 0.7f),
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Medium
                    )
                }

                Spacer(modifier = Modifier.height(16.dp))

                // Action Buttons
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedButton(
                        onClick = onDismissRequest,
                        modifier = Modifier.weight(1f).height(42.dp),
                        shape = RoundedCornerShape(10.dp),
                        border = androidx.compose.foundation.BorderStroke(1.dp, Color.White.copy(alpha = 0.2f)),
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = Color.White)
                    ) {
                        Text("Hủy", fontSize = 13.5.sp)
                    }

                    Button(
                        onClick = {
                            try {
                                val outSize = 240
                                val outBitmap = Bitmap.createBitmap(outSize, outSize, Bitmap.Config.ARGB_8888)
                                val canvas = Canvas(outBitmap)
                                val paint = Paint(Paint.ANTI_ALIAS_FLAG or Paint.FILTER_BITMAP_FLAG)

                                val srcCenterX = -offsetX / currentScale + sourceBitmap.width / 2f
                                val srcCenterY = -offsetY / currentScale + sourceBitmap.height / 2f
                                val srcRadius = circleRadiusPx / currentScale

                                val srcLeft = (srcCenterX - srcRadius).roundToInt()
                                val srcTop = (srcCenterY - srcRadius).roundToInt()
                                val srcSide = (srcRadius * 2f).roundToInt()

                                val srcRect = Rect(
                                    srcLeft.coerceIn(0, sourceBitmap.width - 1),
                                    srcTop.coerceIn(0, sourceBitmap.height - 1),
                                    (srcLeft + srcSide).coerceIn(1, sourceBitmap.width),
                                    (srcTop + srcSide).coerceIn(1, sourceBitmap.height)
                                )
                                val dstRect = Rect(0, 0, outSize, outSize)

                                canvas.drawARGB(0, 0, 0, 0)
                                canvas.drawCircle(outSize / 2f, outSize / 2f, outSize / 2f, paint)
                                paint.xfermode = PorterDuffXfermode(PorterDuff.Mode.SRC_IN)
                                canvas.drawBitmap(sourceBitmap, srcRect, dstRect, paint)

                                onCropConfirmed(outBitmap)
                            } catch (_: Exception) {
                                onCropConfirmed(sourceBitmap)
                            }
                        },
                        modifier = Modifier
                            .weight(1.3f)
                            .height(42.dp)
                            .background(
                                brush = Brush.linearGradient(
                                    colors = listOf(Color(0xFFFF2A85), Color(0xFF8A2BE2))
                                ),
                                shape = RoundedCornerShape(10.dp)
                            ),
                        colors = ButtonDefaults.buttonColors(containerColor = Color.Transparent),
                        contentPadding = PaddingValues(0.dp)
                    ) {
                        Text(
                            text = "Cắt & Lưu",
                            color = Color.White,
                            fontSize = 13.5.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        }
    }
}
