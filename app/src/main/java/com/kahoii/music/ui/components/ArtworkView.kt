package com.kahoii.music.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.MusicNote
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.kahoii.music.R
import com.kahoii.music.ui.theme.NeonPink
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

@Composable
fun ArtworkView(
    thumbnailUrl: String?,
    isPlaying: Boolean,
    modifier: Modifier = Modifier,
    accentColor: Color = NeonPink,
    onSwipeLeft: () -> Unit = {},
    onSwipeRight: () -> Unit = {}
) {
    val scaleAnim by animateFloatAsState(
        targetValue = if (isPlaying) 1.0f else 0.92f,
        animationSpec = spring(dampingRatio = 0.75f, stiffness = 300f),
        label = "artworkScale"
    )

    val offsetX = remember { Animatable(0f) }
    val coroutineScope = rememberCoroutineScope()
    val density = LocalDensity.current
    val haptic = LocalHapticFeedback.current

    // Reset offset when track changes
    LaunchedEffect(thumbnailUrl) {
        if (offsetX.value != 0f) {
            offsetX.snapTo(0f)
        }
    }

    val swipeThresholdPx = with(density) { 65.dp.toPx() }
    val exitDistancePx = with(density) { 320.dp.toPx() }

    Box(
        modifier = modifier
            .aspectRatio(1f)
            .scale(scaleAnim)
            .offset { IntOffset(offsetX.value.roundToInt(), 0) }
            .graphicsLayer {
                // Subtle organic tilt during horizontal swipe
                rotationZ = (offsetX.value / 32f).coerceIn(-12f, 12f)
            }
            .pointerInput(thumbnailUrl) {
                detectHorizontalDragGestures(
                    onDragStart = { },
                    onDragEnd = {
                        coroutineScope.launch {
                            val currentOffset = offsetX.value
                            if (currentOffset < -swipeThresholdPx) {
                                // Swipe Left -> Chuyển sang bài tiếp theo (Next Track)
                                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                onSwipeLeft()
                                offsetX.animateTo(-exitDistancePx, tween(durationMillis = 140))
                                offsetX.snapTo(exitDistancePx)
                                offsetX.animateTo(0f, spring(dampingRatio = 0.78f, stiffness = 420f))
                            } else if (currentOffset > swipeThresholdPx) {
                                // Swipe Right -> Quay về bài trước đó (Previous Track)
                                haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                onSwipeRight()
                                offsetX.animateTo(exitDistancePx, tween(durationMillis = 140))
                                offsetX.snapTo(-exitDistancePx)
                                offsetX.animateTo(0f, spring(dampingRatio = 0.78f, stiffness = 420f))
                            } else {
                                // Drag did not reach threshold -> Snap back smoothly to center
                                offsetX.animateTo(0f, spring(dampingRatio = 0.75f, stiffness = 500f))
                            }
                        }
                    },
                    onDragCancel = {
                        coroutineScope.launch {
                            offsetX.animateTo(0f, spring(dampingRatio = 0.75f, stiffness = 500f))
                        }
                    },
                    onHorizontalDrag = { change, dragAmount ->
                        change.consume()
                        coroutineScope.launch {
                            offsetX.snapTo(offsetX.value + dragAmount)
                        }
                    }
                )
            },
        contentAlignment = Alignment.Center
    ) {
        val cornerRadius = 24.dp

        // 1. Ambient colored glow behind the artwork (matches iOS ArtworkView blur)
        if (!thumbnailUrl.isNullOrBlank()) {
            AsyncImage(
                model = ImageRequest.Builder(LocalContext.current)
                    .data(thumbnailUrl)
                    .crossfade(true)
                    .build(),
                contentDescription = null,
                contentScale = ContentScale.Crop,
                modifier = Modifier
                    .fillMaxSize(0.92f)
                    .blur(radius = 36.dp)
                    .clip(RoundedCornerShape(cornerRadius))
                    .scale(1.05f)
            )
        }

        // 2. Main Album Artwork Card
        Box(
            modifier = Modifier
                .fillMaxSize(0.96f)
                .shadow(
                    elevation = 24.dp,
                    shape = RoundedCornerShape(cornerRadius),
                    spotColor = Color.Black,
                    ambientColor = Color.Black
                )
                .clip(RoundedCornerShape(cornerRadius))
                .background(
                    Brush.verticalGradient(
                        listOf(Color(0xFF221A36), Color(0xFF140F22))
                    )
                )
                .border(
                    width = 1.dp,
                    brush = Brush.linearGradient(
                        listOf(Color.White.copy(alpha = 0.22f), Color.White.copy(alpha = 0.05f))
                    ),
                    shape = RoundedCornerShape(cornerRadius)
                ),
            contentAlignment = Alignment.Center
        ) {
            if (!thumbnailUrl.isNullOrBlank()) {
                AsyncImage(
                    model = ImageRequest.Builder(LocalContext.current)
                        .data(thumbnailUrl)
                        .crossfade(true)
                        .error(R.drawable.logo_icon)
                        .placeholder(R.drawable.logo_icon)
                        .build(),
                    contentDescription = "Album Artwork",
                    contentScale = ContentScale.Crop,
                    modifier = Modifier.fillMaxSize()
                )
            } else {
                // Placeholder matching iOS placeholderView
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    modifier = Modifier.padding(24.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.MusicNote,
                        contentDescription = "Kahoii Music",
                        tint = accentColor.copy(alpha = 0.8f),
                        modifier = Modifier.size(68.dp)
                    )
                    Spacer(modifier = Modifier.height(10.dp))
                    Text(
                        text = "Kahoii Music",
                        color = Color.White.copy(alpha = 0.6f),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }
        }
    }
}
