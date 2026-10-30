package com.kahoii.music.ui.components

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.ui.theme.GradientBottom
import com.kahoii.music.ui.theme.NeonPink
import java.util.Locale
import kotlin.math.roundToInt

@Composable
fun MusicTrackBarView(
    currentPositionMs: Long,
    durationMs: Long,
    onSeekCommitted: (Float) -> Unit,
    modifier: Modifier = Modifier,
    accentColor: Color = NeonPink
) {
    var isDragging by remember { mutableStateOf(false) }
    var dragProgress by remember { mutableFloatStateOf(0f) }
    var barWidthPx by remember { mutableFloatStateOf(0f) }
    val density = LocalDensity.current

    val actualProgress = if (durationMs > 0) {
        (currentPositionMs.toFloat() / durationMs.toFloat()).coerceIn(0f, 1f)
    } else 0f

    val displayProgress = if (isDragging) dragProgress else actualProgress
    val displayedPositionMs = if (isDragging) (dragProgress * durationMs).toLong() else currentPositionMs

    val barHeight by animateDpAsState(
        targetValue = if (isDragging) 6.dp else 4.dp,
        animationSpec = tween(durationMillis = 150),
        label = "barHeight"
    )

    val thumbSize by animateDpAsState(
        targetValue = if (isDragging) 16.dp else 10.dp,
        animationSpec = tween(durationMillis = 150),
        label = "thumbSize"
    )

    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        // Scrubber Area (Tap & Drag gesture detection)
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(28.dp)
                .onSizeChanged { barWidthPx = it.width.toFloat() }
                .pointerInput(durationMs) {
                    detectTapGestures { offset ->
                        if (barWidthPx > 0 && durationMs > 0) {
                            val target = (offset.x / barWidthPx).coerceIn(0f, 1f)
                            onSeekCommitted(target)
                        }
                    }
                }
                .pointerInput(durationMs) {
                    detectDragGestures(
                        onDragStart = { offset ->
                            isDragging = true
                            if (barWidthPx > 0) {
                                dragProgress = (offset.x / barWidthPx).coerceIn(0f, 1f)
                            }
                        },
                        onDragEnd = {
                            isDragging = false
                            onSeekCommitted(dragProgress)
                        },
                        onDragCancel = {
                            isDragging = false
                        },
                        onDrag = { change, _ ->
                            change.consume()
                            if (barWidthPx > 0) {
                                dragProgress = (change.position.x / barWidthPx).coerceIn(0f, 1f)
                            }
                        }
                    )
                },
            contentAlignment = Alignment.CenterStart
        ) {
            // Background inactive track
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(barHeight)
                    .clip(RoundedCornerShape(3.dp))
                    .background(Color.White.copy(alpha = 0.18f))
            )

            // Active progress track with Gradient
            Box(
                modifier = Modifier
                    .fillMaxWidth(displayProgress)
                    .height(barHeight)
                    .clip(RoundedCornerShape(3.dp))
                    .background(
                        Brush.horizontalGradient(
                            listOf(accentColor, GradientBottom)
                        )
                    )
            )

            // Scrubber Thumb
            val thumbPx = with(density) { thumbSize.toPx() }
            val thumbOffsetPx = if (barWidthPx > 0) {
                ((barWidthPx * displayProgress) - (thumbPx / 2f)).coerceIn(0f, barWidthPx - thumbPx)
            } else 0f

            Box(
                modifier = Modifier
                    .offset { IntOffset(thumbOffsetPx.roundToInt(), 0) }
                    .size(thumbSize)
                    .shadow(elevation = 6.dp, shape = CircleShape, spotColor = Color.Black)
                    .clip(CircleShape)
                    .background(Color.White)
            )
        }

        // Monospaced Time Labels below track
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = formatDuration(displayedPositionMs),
                color = Color.White.copy(alpha = 0.6f),
                fontSize = 11.5.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Medium
            )

            Text(
                text = formatDuration(durationMs),
                color = Color.White.copy(alpha = 0.6f),
                fontSize = 11.5.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

private fun formatDuration(ms: Long): String {
    if (ms <= 0L) return "00:00"
    val totalSeconds = ms / 1000
    val hours = totalSeconds / 3600
    val minutes = (totalSeconds % 3600) / 60
    val seconds = totalSeconds % 60
    return if (hours > 0) {
        String.format(Locale.getDefault(), "%d:%02d:%02d", hours, minutes, seconds)
    } else {
        String.format(Locale.getDefault(), "%02d:%02d", minutes, seconds)
    }
}

