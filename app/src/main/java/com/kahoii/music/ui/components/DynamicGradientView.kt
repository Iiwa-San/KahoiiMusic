package com.kahoii.music.ui.components

import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import com.kahoii.music.ui.theme.GradientBottom
import com.kahoii.music.ui.theme.GradientTop
import com.kahoii.music.ui.theme.NeonPink

@Composable
fun DynamicGradientView(
    modifier: Modifier = Modifier,
    topColor: Color = Color(0xFF1E1038),
    bottomColor: Color = Color(0xFF0B0714),
    accentColor: Color = NeonPink,
    content: @Composable () -> Unit
) {
    val infiniteTransition = rememberInfiniteTransition(label = "ambientGlow")

    // Slow organic floating animation
    val glowAnimX by infiniteTransition.animateFloat(
        initialValue = -60f,
        targetValue = 60f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 8000, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "glowX"
    )

    val glowAnimY by infiniteTransition.animateFloat(
        initialValue = 0.15f,
        targetValue = 0.35f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 11000, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "glowY"
    )

    val scalePulse by infiniteTransition.animateFloat(
        initialValue = 0.85f,
        targetValue = 1.15f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 6000, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "glowScale"
    )

    Box(modifier = modifier.fillMaxSize()) {
        // Base Dynamic Background with Radial Glow Orbs
        Canvas(modifier = Modifier.fillMaxSize()) {
            val w = size.width
            val h = size.height

            // 1. Base Vertical Linear Gradient (Obsidian to Midnight Purple)
            drawRect(
                brush = Brush.verticalGradient(
                    colors = listOf(topColor, bottomColor)
                )
            )

            // 2. Primary Ambient Radial Orb (Floating Accent Neon)
            val orbCenter1 = Offset(w * 0.45f + glowAnimX, h * glowAnimY)
            val orbRadius1 = (w * 0.65f) * scalePulse
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(
                        accentColor.copy(alpha = 0.22f),
                        accentColor.copy(alpha = 0.08f),
                        Color.Transparent
                    ),
                    center = orbCenter1,
                    radius = orbRadius1
                ),
                center = orbCenter1,
                radius = orbRadius1
            )

            // 3. Secondary Ambient Orb (Deep Violet / Magenta glow near center)
            val orbCenter2 = Offset(w * 0.7f - glowAnimX, h * 0.55f)
            val orbRadius2 = (w * 0.7f) * (2f - scalePulse)
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(
                        GradientTop.copy(alpha = 0.26f),
                        GradientBottom.copy(alpha = 0.10f),
                        Color.Transparent
                    ),
                    center = orbCenter2,
                    radius = orbRadius2
                ),
                center = orbCenter2,
                radius = orbRadius2
            )
        }

        // Foreground Content
        content()
    }
}

