package com.kahoii.music.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Repeat
import androidx.compose.material.icons.filled.RepeatOne
import androidx.compose.material.icons.filled.Shuffle
import androidx.compose.material.icons.filled.SkipNext
import androidx.compose.material.icons.filled.SkipPrevious
import androidx.compose.material.icons.filled.Timer
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.model.LoopMode
import com.kahoii.music.ui.theme.GradientBottom
import com.kahoii.music.ui.theme.NeonPink

@Composable
fun PlaybackControlsView(
    isPlaying: Boolean,
    isLoading: Boolean,
    isShuffled: Boolean,
    loopMode: LoopMode,
    isSleepTimerActive: Boolean,
    sleepTimerText: String,
    onTogglePlayPause: () -> Unit,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
    onToggleShuffle: () -> Unit,
    onToggleLoop: () -> Unit,
    onTimerClick: () -> Unit,
    modifier: Modifier = Modifier,
    accentColor: Color = NeonPink,
    isCurrentTrackSaved: Boolean = false,
    isCurrentTrackDownloading: Boolean = false,
    onQuickDownload: () -> Unit = {}
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // 1. Quick Download Button
        Box(
            modifier = Modifier
                .weight(1f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onQuickDownload),
            contentAlignment = Alignment.Center
        ) {
            if (isCurrentTrackDownloading) {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    color = Color(0xFF22C55E),
                    strokeWidth = 2.dp
                )
            } else if (isCurrentTrackSaved) {
                Icon(
                    imageVector = Icons.Default.Check,
                    contentDescription = "Đã tải về máy",
                    tint = Color(0xFF22C55E),
                    modifier = Modifier.size(20.dp)
                )
            } else {
                Icon(
                    imageVector = Icons.Default.Download,
                    contentDescription = "Tải về nhanh",
                    tint = Color.White.copy(alpha = 0.45f),
                    modifier = Modifier.size(20.dp)
                )
            }
        }

        // 2. Shuffle Button
        Box(
            modifier = Modifier
                .weight(1f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onToggleShuffle),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Default.Shuffle,
                contentDescription = "Trộn bài",
                tint = if (isShuffled) accentColor else Color.White.copy(alpha = 0.45f),
                modifier = Modifier.size(20.dp)
            )
        }

        // 3. Previous Button
        Box(
            modifier = Modifier
                .weight(1.05f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onPrevious),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Default.SkipPrevious,
                contentDescription = "Bài trước",
                tint = Color.White,
                modifier = Modifier.size(30.dp)
            )
        }

        // 4. Hero Play / Pause Button (Luminous orb matching iOS)
        Box(
            modifier = Modifier
                .weight(1.35f)
                .height(64.dp),
            contentAlignment = Alignment.Center
        ) {
            Box(
                modifier = Modifier
                    .size(62.dp)
                    .shadow(
                        elevation = 14.dp,
                        shape = CircleShape,
                        spotColor = accentColor,
                        ambientColor = accentColor
                    )
                    .clip(CircleShape)
                    .background(
                        Brush.radialGradient(
                            listOf(accentColor, GradientBottom)
                        )
                    )
                    .clickable(onClick = onTogglePlayPause),
                contentAlignment = Alignment.Center
            ) {
                if (isLoading) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(26.dp),
                        color = Color.White,
                        strokeWidth = 2.5.dp
                    )
                } else {
                    Icon(
                        imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                        contentDescription = if (isPlaying) "Tạm dừng" else "Phát",
                        tint = Color.White,
                        modifier = Modifier
                            .size(34.dp)
                            .offset(x = if (isPlaying) 0.dp else 2.dp)
                    )
                }
            }
        }

        // 5. Next Button
        Box(
            modifier = Modifier
                .weight(1.05f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onNext),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Default.SkipNext,
                contentDescription = "Bài tiếp theo",
                tint = Color.White,
                modifier = Modifier.size(30.dp)
            )
        }

        // 6. Loop / Repeat Button
        Box(
            modifier = Modifier
                .weight(1f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onToggleLoop),
            contentAlignment = Alignment.Center
        ) {
            when (loopMode) {
                LoopMode.OFF -> Icon(
                    imageVector = Icons.Default.Repeat,
                    contentDescription = "Tắt lặp lại",
                    tint = Color.White.copy(alpha = 0.45f),
                    modifier = Modifier.size(20.dp)
                )
                LoopMode.ONE -> Icon(
                    imageVector = Icons.Default.RepeatOne,
                    contentDescription = "Lặp 1 bài",
                    tint = accentColor,
                    modifier = Modifier.size(22.dp)
                )
                LoopMode.ALL -> Icon(
                    imageVector = Icons.Default.Repeat,
                    contentDescription = "Lặp toàn bộ",
                    tint = accentColor,
                    modifier = Modifier.size(22.dp)
                )
            }
        }

        // 7. Sleep Timer Button with Countdown label
        Box(
            modifier = Modifier
                .weight(1f)
                .height(48.dp)
                .clip(CircleShape)
                .clickable(onClick = onTimerClick),
            contentAlignment = Alignment.Center
        ) {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                Icon(
                    imageVector = Icons.Default.Timer,
                    contentDescription = "Hẹn giờ",
                    tint = if (isSleepTimerActive) accentColor else Color.White.copy(alpha = 0.45f),
                    modifier = Modifier.size(19.dp)
                )
                if (isSleepTimerActive && sleepTimerText.isNotBlank()) {
                    Text(
                        text = sleepTimerText,
                        color = accentColor,
                        fontSize = 9.sp,
                        fontWeight = FontWeight.Bold,
                        maxLines = 1
                    )
                }
            }
        }
    }
}
