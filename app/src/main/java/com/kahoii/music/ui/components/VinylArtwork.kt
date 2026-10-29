package com.kahoii.music.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.kahoii.music.R
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.ui.theme.VinylBlack
import com.kahoii.music.ui.theme.VinylCenter
import com.kahoii.music.ui.theme.VinylGroove

@Composable
fun VinylArtwork(
    thumbnailUrl: String?,
    isPlaying: Boolean,
    modifier: Modifier = Modifier,
    size: Dp = 240.dp
) {
    val rotationAnim = remember { Animatable(0f) }

    LaunchedEffect(isPlaying) {
        if (isPlaying) {
            while (true) {
                val target = rotationAnim.value + 360f
                rotationAnim.animateTo(
                    targetValue = target,
                    animationSpec = tween(durationMillis = 10000, easing = LinearEasing)
                )
            }
        } else {
            rotationAnim.stop()
        }
    }

    Box(
        modifier = modifier
            .size(size)
            .shadow(elevation = 20.dp, shape = CircleShape, spotColor = Color.Black, ambientColor = Color.Black)
            .graphicsLayer { rotationZ = rotationAnim.value % 360f },
        contentAlignment = Alignment.Center
    ) {
        // 1. Vinyl disc background with grooved rings
        Canvas(modifier = Modifier.size(size)) {
            val centerOffset = Offset(size.toPx() / 2f, size.toPx() / 2f)
            val radius = size.toPx() / 2f

            // Outer Vinyl Disc
            drawCircle(
                color = VinylBlack,
                radius = radius,
                center = centerOffset
            )

            // Outer edge subtle border
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(Color(0xFF2A2A2A), Color(0xFF101010)),
                    center = centerOffset,
                    radius = radius
                ),
                radius = radius,
                center = centerOffset,
                style = Stroke(width = 2f)
            )

            // Groove rings
            val grooveCount = 10
            val minR = radius * 0.45f
            val maxR = radius * 0.94f
            val step = (maxR - minR) / grooveCount

            for (i in 0 until grooveCount) {
                val r = minR + i * step
                drawCircle(
                    color = VinylGroove.copy(alpha = 0.55f),
                    radius = r,
                    center = centerOffset,
                    style = Stroke(width = 1.2f)
                )
            }
        }

        // 2. Center Album Sticker / Thumbnail
        val centerSize = size * 0.44f
        Box(
            modifier = Modifier
                .size(centerSize)
                .clip(CircleShape)
                .background(Color.Black)
                .border(width = 2.dp, color = NeonPink.copy(alpha = 0.7f), shape = CircleShape),
            contentAlignment = Alignment.Center
        ) {
            AsyncImage(
                model = ImageRequest.Builder(LocalContext.current)
                    .data(thumbnailUrl ?: R.drawable.logo_icon)
                    .crossfade(true)
                    .error(R.drawable.logo_icon)
                    .placeholder(R.drawable.logo_icon)
                    .build(),
                contentDescription = "Artwork Thumbnail",
                contentScale = ContentScale.Crop,
                modifier = Modifier
                    .size(centerSize)
                    .clip(CircleShape)
            )

            // 3. Center Spindle hole
            val spindleSize = centerSize * 0.22f
            Box(
                modifier = Modifier
                    .size(spindleSize)
                    .clip(CircleShape)
                    .background(VinylCenter)
                    .border(width = 2.dp, color = Color(0xFFC0C0C0), shape = CircleShape)
            )
        }
    }
}

