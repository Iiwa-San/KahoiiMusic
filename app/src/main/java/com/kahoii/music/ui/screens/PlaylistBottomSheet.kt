package com.kahoii.music.ui.screens

import android.widget.Toast
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DeleteSweep
import androidx.compose.material.icons.filled.GraphicEq
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.TabRowDefaults
import androidx.compose.material3.TabRowDefaults.tabIndicatorOffset
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.kahoii.music.R
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.ui.theme.SurfaceDark
import com.kahoii.music.ui.theme.SurfaceElevated
import com.kahoii.music.ui.theme.TextMuted
import com.kahoii.music.ui.theme.TextWhite

@OptIn(ExperimentalMaterial3Api::class, ExperimentalFoundationApi::class)
@Composable
fun PlaylistBottomSheet(
    playlist: List<PlaylistItem>,
    history: List<PlaylistItem>,
    currentTrackId: String?,
    isPlaylistLoading: Boolean,
    onPlaylistTrackSelected: (PlaylistItem, Int) -> Unit,
    onHistoryTrackSelected: (PlaylistItem) -> Unit,
    onAddToPlaylist: (PlaylistItem) -> Boolean,
    onRemoveFromPlaylist: (Int) -> Unit,
    onDeleteHistoryItem: (String) -> Unit,
    onClearHistory: () -> Unit,
    onDismissRequest: () -> Unit
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = false)
    var selectedTab by remember { mutableIntStateOf(0) } // 0: Playlist, 1: History
    val context = LocalContext.current
    val haptic = LocalHapticFeedback.current

    ModalBottomSheet(
        onDismissRequest = onDismissRequest,
        sheetState = sheetState,
        containerColor = SurfaceDark,
        dragHandle = null
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .fillMaxHeight(0.75f)
                .padding(top = 12.dp)
        ) {
            // Drag handle pill
            Box(
                modifier = Modifier
                    .width(40.dp)
                    .height(4.dp)
                    .clip(CircleShape)
                    .background(Color.White.copy(alpha = 0.3f))
                    .align(Alignment.CenterHorizontally)
            )

            Spacer(modifier = Modifier.height(12.dp))

            // Tabs: Danh sách phát & Lịch sử
            TabRow(
                selectedTabIndex = selectedTab,
                containerColor = SurfaceDark,
                contentColor = TextWhite,
                indicator = { tabPositions ->
                    TabRowDefaults.SecondaryIndicator(
                        Modifier.tabIndicatorOffset(tabPositions[selectedTab]),
                        color = NeonPink
                    )
                }
            ) {
                Tab(
                    selected = selectedTab == 0,
                    onClick = { selectedTab = 0 },
                    text = {
                        Text(
                            text = "Danh sách phát (${playlist.size})",
                            fontWeight = if (selectedTab == 0) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 0) NeonPink else TextMuted
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    text = {
                        Text(
                            text = "Lịch sử (${history.size})",
                            fontWeight = if (selectedTab == 1) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 1) NeonPink else TextMuted
                        )
                    }
                )
            }

            // Playlist / History Content
            if (selectedTab == 0) {
                // PLAYLIST TAB
                if (isPlaylistLoading) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            CircularProgressIndicator(color = NeonPink)
                            Spacer(modifier = Modifier.height(8.dp))
                            Text(text = "Đang tải danh sách...", color = TextMuted, fontSize = 13.sp)
                        }
                    }
                } else if (playlist.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Chưa có bài hát trong danh sách phát\nDán link playlist hoặc tìm bài hát",
                            color = TextMuted,
                            fontSize = 13.sp,
                            textAlign = androidx.compose.ui.text.style.TextAlign.Center
                        )
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                            .padding(vertical = 8.dp)
                    ) {
                        itemsIndexed(playlist, key = { index, item -> "${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                onClick = { onPlaylistTrackSelected(item, index) },
                                onDelete = { onRemoveFromPlaylist(index) },
                                deleteIcon = Icons.Default.Close,
                                deleteContentDescription = "Xóa khỏi danh sách phát"
                            )
                        }
                    }
                }
            } else {
                // HISTORY TAB
                if (history.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 6.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Nhấn giữ icon để thêm vào danh sách phát",
                            color = TextMuted.copy(alpha = 0.7f),
                            fontSize = 11.sp
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.clickable { onClearHistory() }
                        ) {
                            Icon(
                                imageVector = Icons.Default.DeleteSweep,
                                contentDescription = "Clear History",
                                tint = NeonPink,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "Xóa tất cả",
                                color = NeonPink,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }
                }

                if (history.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Chưa có lịch sử phát",
                            color = TextMuted,
                            fontSize = 13.sp
                        )
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                    ) {
                        itemsIndexed(history, key = { index, item -> "hist_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                onClick = { onHistoryTrackSelected(item) },
                                onIconLongClick = {
                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                    val added = onAddToPlaylist(item)
                                    val msg = if (added) {
                                        "Đã thêm vào danh sách phát: ${item.title}"
                                    } else {
                                        "Bài hát đã có trong danh sách phát"
                                    }
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                },
                                onAddToPlaylist = {
                                    val added = onAddToPlaylist(item)
                                    val msg = if (added) {
                                        "Đã thêm vào danh sách phát: ${item.title}"
                                    } else {
                                        "Bài hát đã có trong danh sách phát"
                                    }
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                },
                                onDelete = { onDeleteHistoryItem(item.id) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Xóa khỏi lịch sử"
                            )
                        }
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun TrackRow(
    item: PlaylistItem,
    index: Int,
    isPlaying: Boolean,
    onClick: () -> Unit,
    onDelete: (() -> Unit)? = null,
    onIconLongClick: (() -> Unit)? = null,
    onAddToPlaylist: (() -> Unit)? = null,
    deleteIcon: androidx.compose.ui.graphics.vector.ImageVector = Icons.Default.Delete,
    deleteContentDescription: String = "Xóa"
) {
    val bgColor = if (isPlaying) SurfaceElevated else Color.Transparent

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Thumbnail or Playing Equalizer (Click to play, Long-press to add to playlist)
        Box(
            modifier = Modifier
                .size(44.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(Color.Black)
                .combinedClickable(
                    onClick = onClick,
                    onLongClick = onIconLongClick
                ),
            contentAlignment = Alignment.Center
        ) {
            AsyncImage(
                model = ImageRequest.Builder(LocalContext.current)
                    .data(item.thumbnailUrl)
                    .crossfade(true)
                    .error(R.drawable.logo_icon)
                    .placeholder(R.drawable.logo_icon)
                    .build(),
                contentDescription = item.title,
                contentScale = ContentScale.Crop,
                modifier = Modifier.size(44.dp)
            )

            if (isPlaying) {
                Box(
                    modifier = Modifier
                        .matchParentSize()
                        .background(Color.Black.copy(alpha = 0.5f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.GraphicEq,
                        contentDescription = "Playing",
                        tint = NeonPink,
                        modifier = Modifier.size(24.dp)
                    )
                }
            }
        }

        Spacer(modifier = Modifier.width(12.dp))

        // Title and Duration
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title.ifBlank { "Bài hát" },
                color = if (isPlaying) NeonPink else TextWhite,
                fontSize = 14.sp,
                fontWeight = if (isPlaying) FontWeight.Bold else FontWeight.Normal,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )

            Row(verticalAlignment = Alignment.CenterVertically) {
                if (item.artist.isNotBlank()) {
                    Text(
                        text = item.artist,
                        color = TextMuted,
                        fontSize = 12.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false)
                    )
                    Text(text = " • ", color = TextMuted, fontSize = 12.sp)
                }
                Text(
                    text = item.formattedDuration,
                    color = TextMuted,
                    fontSize = 12.sp
                )
            }
        }

        // Add to playlist button (in history)
        if (onAddToPlaylist != null) {
            IconButton(
                onClick = onAddToPlaylist,
                modifier = Modifier.size(36.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.Add,
                    contentDescription = "Thêm vào danh sách phát",
                    tint = NeonPink,
                    modifier = Modifier.size(20.dp)
                )
            }
        }

        // Delete button
        if (onDelete != null) {
            IconButton(
                onClick = onDelete,
                modifier = Modifier.size(36.dp)
            ) {
                Icon(
                    imageVector = deleteIcon,
                    contentDescription = deleteContentDescription,
                    tint = TextMuted.copy(alpha = 0.7f),
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}
