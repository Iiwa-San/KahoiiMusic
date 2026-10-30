package com.kahoii.music.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.QueueMusic
import androidx.compose.material.icons.filled.DownloadDone
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Share
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.ui.theme.NeonPink

@Composable
fun BottomActionsSection(
    playlistCount: Int,
    savedCount: Int,
    favoritesCount: Int,
    historyCount: Int = 0,
    onPlaylistClick: () -> Unit,
    onSavedClick: () -> Unit,
    onHistoryClick: () -> Unit,
    onFavoritesClick: () -> Unit,
    onShareClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // 1. Danh sách (Queue / Playlist)
        BottomActionButton(
            icon = Icons.AutoMirrored.Filled.QueueMusic,
            label = "Danh sách",
            badgeCount = playlistCount,
            badgeColor = Color(0xFFA855F7), // Purple
            onClick = onPlaylistClick,
            modifier = Modifier.weight(1f)
        )

        // 2. Đã tải (Downloads)
        BottomActionButton(
            icon = Icons.Default.DownloadDone,
            label = "Đã tải",
            badgeCount = savedCount,
            badgeColor = Color(0xFF22C55E), // Emerald Green
            onClick = onSavedClick,
            modifier = Modifier.weight(1f)
        )

        // 3. Lịch sử (History)
        BottomActionButton(
            icon = Icons.Default.History,
            label = "Lịch sử",
            badgeCount = historyCount,
            badgeColor = Color(0xFF3B82F6), // Blue
            onClick = onHistoryClick,
            modifier = Modifier.weight(1f)
        )

        // 4. Yêu thích (Favorites)
        BottomActionButton(
            icon = Icons.Default.Favorite,
            label = "Yêu thích",
            iconTint = if (favoritesCount > 0) Color(0xFFFF2D55) else Color.White.copy(alpha = 0.75f),
            badgeCount = favoritesCount,
            badgeColor = Color(0xFFFF2D55),
            onClick = onFavoritesClick,
            modifier = Modifier.weight(1f)
        )

        // 5. Chia sẻ (Share)
        BottomActionButton(
            icon = Icons.Default.Share,
            label = "Chia sẻ",
            badgeCount = 0,
            badgeColor = Color.Transparent,
            onClick = onShareClick,
            modifier = Modifier.weight(1f)
        )
    }
}

@Composable
private fun BottomActionButton(
    icon: ImageVector,
    label: String,
    badgeCount: Int,
    badgeColor: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    iconTint: Color = Color.White.copy(alpha = 0.75f)
) {
    val interactionSource = remember { MutableInteractionSource() }

    Column(
        modifier = modifier
            .clickable(
                interactionSource = interactionSource,
                indication = null,
                onClick = onClick
            ),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        BadgedBox(
            badge = {
                if (badgeCount > 0) {
                    Badge(
                        containerColor = badgeColor,
                        contentColor = Color.White,
                        modifier = Modifier.offset(x = 6.dp, y = (-2).dp)
                    ) {
                        Text(
                            text = if (badgeCount > 99) "99+" else "$badgeCount",
                            fontSize = 8.5.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }
        ) {
            Icon(
                imageVector = icon,
                contentDescription = label,
                tint = iconTint,
                modifier = Modifier.size(21.dp)
            )
        }

        Spacer(modifier = Modifier.height(4.dp))

        Text(
            text = label,
            color = Color.White.copy(alpha = 0.75f),
            fontSize = 11.sp,
            fontWeight = FontWeight.Medium
        )
    }
}

