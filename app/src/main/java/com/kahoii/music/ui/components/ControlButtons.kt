package com.kahoii.music.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Repeat
import androidx.compose.material.icons.filled.RepeatOne
import androidx.compose.material.icons.filled.SkipNext
import androidx.compose.material.icons.filled.SkipPrevious
import androidx.compose.material.icons.filled.VolumeDown
import androidx.compose.material.icons.filled.VolumeMute
import androidx.compose.material.icons.filled.VolumeUp
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.kahoii.music.model.LoopMode
import com.kahoii.music.ui.theme.GradientBottom
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.ui.theme.PlayedRed
import com.kahoii.music.ui.theme.TextMuted
import com.kahoii.music.ui.theme.TextWhite

@Composable
fun ControlButtons(
    isPlaying: Boolean,
    loopMode: LoopMode,
    volume: Float,
    onTogglePlayPause: () -> Unit,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
    onToggleLoop: () -> Unit,
    onVolumeClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceEvenly,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // 1. Loop Button
        IconButton(onClick = onToggleLoop) {
            when (loopMode) {
                LoopMode.OFF -> Icon(
                    imageVector = Icons.Default.Repeat,
                    contentDescription = "Loop Off",
                    tint = TextMuted.copy(alpha = 0.6f),
                    modifier = Modifier.size(26.dp)
                )
                LoopMode.ONE -> Icon(
                    imageVector = Icons.Default.RepeatOne,
                    contentDescription = "Repeat One",
                    tint = NeonPink,
                    modifier = Modifier.size(28.dp)
                )
                LoopMode.ALL -> Icon(
                    imageVector = Icons.Default.Repeat,
                    contentDescription = "Repeat All",
                    tint = GradientBottom,
                    modifier = Modifier.size(28.dp)
                )
            }
        }

        // 2. Previous Button
        IconButton(
            onClick = onPrevious,
            modifier = Modifier.size(48.dp)
        ) {
            Icon(
                imageVector = Icons.Default.SkipPrevious,
                contentDescription = "Previous Track",
                tint = TextWhite,
                modifier = Modifier.size(34.dp)
            )
        }

        // 3. Center Play/Pause Button
        Box(
            modifier = Modifier
                .size(68.dp)
                .shadow(elevation = 12.dp, shape = CircleShape, spotColor = NeonPink)
                .clip(CircleShape)
                .background(
                    Brush.radialGradient(
                        listOf(NeonPink, GradientBottom, PlayedRed)
                    )
                )
                .clickable(onClick = onTogglePlayPause),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                contentDescription = if (isPlaying) "Pause" else "Play",
                tint = TextWhite,
                modifier = Modifier.size(38.dp)
            )
        }

        // 4. Next Button
        IconButton(
            onClick = onNext,
            modifier = Modifier.size(48.dp)
        ) {
            Icon(
                imageVector = Icons.Default.SkipNext,
                contentDescription = "Next Track",
                tint = TextWhite,
                modifier = Modifier.size(34.dp)
            )
        }

        // 5. Volume Button
        IconButton(onClick = onVolumeClick) {
            val volumeIcon = when {
                volume <= 0.01f -> Icons.Default.VolumeMute
                volume < 0.5f -> Icons.Default.VolumeDown
                else -> Icons.Default.VolumeUp
            }
            Icon(
                imageVector = volumeIcon,
                contentDescription = "Volume",
                tint = TextWhite,
                modifier = Modifier.size(26.dp)
            )
        }
    }
}

