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
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.ClearAll
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DeleteSweep
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.GraphicEq
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.ScrollableTabRow
import androidx.compose.material3.Tab
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
import androidx.compose.ui.text.style.TextAlign
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
    savedTracks: List<PlaylistItem>,
    favorites: List<PlaylistItem>,
    downloadingTrackIds: Set<String>,
    currentTrackId: String?,
    isPlaylistLoading: Boolean,
    initialTab: Int = 0,
    onPlaylistTrackSelected: (PlaylistItem, Int) -> Unit,
    onHistoryTrackSelected: (PlaylistItem, Int) -> Unit,
    onSavedTrackSelected: (PlaylistItem, Int) -> Unit,
    onFavoriteTrackSelected: (PlaylistItem, Int) -> Unit,
    onAddToPlaylist: (PlaylistItem) -> Boolean,
    onRemoveFromPlaylist: (Int) -> Unit,
    onClearPlaylist: () -> Unit,
    onDeleteHistoryItem: (String) -> Unit,
    onClearHistory: () -> Unit,
    onToggleFavorite: (PlaylistItem) -> Unit,
    onDownloadTrack: (PlaylistItem) -> Unit,
    onDeleteSavedTrack: (String) -> Unit,
    onPlayAllSavedTracks: () -> Unit,
    onPlayAllFavorites: () -> Unit,
    onDismissRequest: () -> Unit
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = false)
    var selectedTab by remember { mutableIntStateOf(initialTab.coerceIn(0, 3)) }
    val context = LocalContext.current
    val haptic = LocalHapticFeedback.current

    ModalBottomSheet(
        onDismissRequest = onDismissRequest,
        sheetState = sheetState,
        containerColor = Color(0xFF140F24),
        dragHandle = null
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .fillMaxHeight(0.82f)
                .padding(top = 12.dp)
        ) {
            // Drag handle pill
            Box(
                modifier = Modifier
                    .width(42.dp)
                    .height(4.dp)
                    .clip(CircleShape)
                    .background(Color.White.copy(alpha = 0.35f))
                    .align(Alignment.CenterHorizontally)
            )

            Spacer(modifier = Modifier.height(12.dp))

            // 4 Tabs matching iOS navigation: Danh sách | Đã tải | Lịch sử | Yêu thích
            ScrollableTabRow(
                selectedTabIndex = selectedTab,
                containerColor = Color(0xFF140F24),
                contentColor = Color.White,
                edgePadding = 16.dp,
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
                            text = "Danh sách (${playlist.size})",
                            fontWeight = if (selectedTab == 0) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 0) NeonPink else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    text = {
                        Text(
                            text = "Đã tải (${savedTracks.size})",
                            fontWeight = if (selectedTab == 1) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 1) Color(0xFF22C55E) else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 2,
                    onClick = { selectedTab = 2 },
                    text = {
                        Text(
                            text = "Lịch sử (${history.size})",
                            fontWeight = if (selectedTab == 2) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 2) NeonPink else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 3,
                    onClick = { selectedTab = 3 },
                    text = {
                        Text(
                            text = "Yêu thích (${favorites.size})",
                            fontWeight = if (selectedTab == 3) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 3) Color(0xFFFF2D55) else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
            }

            // ================= TAB 0: PLAYLIST QUEUE =================
            if (selectedTab == 0) {
                if (playlist.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Danh sách chờ (${playlist.size} bài)",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .clip(RoundedCornerShape(8.dp))
                                .clickable { onClearPlaylist() }
                                .padding(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.ClearAll,
                                contentDescription = "Clear Playlist",
                                tint = NeonPink,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "Làm rỗng",
                                color = NeonPink,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }
                }

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
                            Text(text = "Đang nạp danh sách...", color = TextMuted, fontSize = 13.sp)
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
                            text = "Danh sách chờ trống\nTìm kiếm bài hát hoặc dán link YouTube để bắt đầu",
                            color = Color.White.copy(alpha = 0.5f),
                            fontSize = 13.sp,
                            textAlign = TextAlign.Center
                        )
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                    ) {
                        itemsIndexed(playlist, key = { index, item -> "pl_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)

                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = isFav,
                                isDownloading = isDownloading,
                                onClick = { onPlaylistTrackSelected(item, index) },
                                onDownload = { onDownloadTrack(item) },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onDelete = { onRemoveFromPlaylist(index) },
                                deleteIcon = Icons.Default.Close,
                                deleteContentDescription = "Xóa khỏi danh sách"
                            )
                        }
                    }
                }
            }
            // ================= TAB 1: SAVED (ĐÃ TẢI OFFLINE) =================
            else if (selectedTab == 1) {
                if (savedTracks.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "${savedTracks.size} bài hát đã tải về máy",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .clip(CapsuleShape)
                                .background(Color(0xFF22C55E).copy(alpha = 0.85f))
                                .clickable { onPlayAllSavedTracks() }
                                .padding(horizontal = 12.dp, vertical = 6.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.PlayArrow,
                                contentDescription = "Play All Saved",
                                tint = Color.White,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "Phát tất cả",
                                color = Color.White,
                                fontSize = 12.5.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                if (savedTracks.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            modifier = Modifier.padding(28.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Download,
                                contentDescription = "Chưa có bài tải về",
                                tint = Color.White.copy(alpha = 0.35f),
                                modifier = Modifier.size(52.dp)
                            )
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = "Chưa có bài hát nào được tải về",
                                color = Color.White,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = "Bấm biểu tượng 📥 tải về ở cạnh bài hát để nghe offline không cần Internet.",
                                color = Color.White.copy(alpha = 0.5f),
                                fontSize = 12.5.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                    ) {
                        itemsIndexed(savedTracks, key = { index, item -> "saved_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }

                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                isSaved = true,
                                isFavorite = isFav,
                                isDownloading = false,
                                onClick = { onSavedTrackSelected(item, index) },
                                onAddToPlaylist = {
                                    val added = onAddToPlaylist(item)
                                    val msg = if (added) "Đã thêm vào danh sách phát" else "Bài hát đã có trong danh sách phát"
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onDelete = { onDeleteSavedTrack(item.id) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Xóa bài tải về"
                            )
                        }
                    }
                }
            }
            // ================= TAB 2: HISTORY =================
            else if (selectedTab == 2) {
                if (history.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Lịch sử đã nghe gần đây",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .clip(RoundedCornerShape(8.dp))
                                .clickable { onClearHistory() }
                                .padding(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.DeleteSweep,
                                contentDescription = "Clear History",
                                tint = NeonPink,
                                modifier = Modifier.size(16.dp)
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
                            text = "Chưa có lịch sử phát nhạc",
                            color = Color.White.copy(alpha = 0.5f),
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
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)

                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = isFav,
                                isDownloading = isDownloading,
                                onClick = { onHistoryTrackSelected(item, index) },
                                onAddToPlaylist = {
                                    val added = onAddToPlaylist(item)
                                    val msg = if (added) "Đã thêm vào danh sách phát" else "Bài hát đã có trong danh sách phát"
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onDownload = { onDownloadTrack(item) },
                                onDelete = { onDeleteHistoryItem(item.id) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Xóa khỏi lịch sử"
                            )
                        }
                    }
                }
            }
            // ================= TAB 3: FAVORITES (YÊU THÍCH) =================
            else {
                if (favorites.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "${favorites.size} bài hát đã thích",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .clip(CapsuleShape)
                                .background(Color(0xFFFF2D55).copy(alpha = 0.85f))
                                .clickable { onPlayAllFavorites() }
                                .padding(horizontal = 12.dp, vertical = 6.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.PlayArrow,
                                contentDescription = "Play All Favorites",
                                tint = Color.White,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "Phát tất cả",
                                color = Color.White,
                                fontSize = 12.5.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                if (favorites.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            modifier = Modifier.padding(28.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.FavoriteBorder,
                                contentDescription = "Chưa có bài thích",
                                tint = Color.White.copy(alpha = 0.35f),
                                modifier = Modifier.size(52.dp)
                            )
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = "Chưa có bài hát yêu thích",
                                color = Color.White,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = "Nhấn biểu tượng trái tim ❤️ khi nghe nhạc để lưu bài hát vào đây.",
                                color = Color.White.copy(alpha = 0.5f),
                                fontSize = 12.5.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                    ) {
                        itemsIndexed(favorites, key = { index, item -> "fav_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)

                            TrackRow(
                                item = item,
                                index = index + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = true,
                                isDownloading = isDownloading,
                                onClick = { onFavoriteTrackSelected(item, index) },
                                onAddToPlaylist = {
                                    val added = onAddToPlaylist(item)
                                    val msg = if (added) "Đã thêm vào danh sách phát" else "Bài hát đã có trong danh sách phát"
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onDownload = { onDownloadTrack(item) },
                                onDelete = { onToggleFavorite(item) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Bỏ yêu thích"
                            )
                        }
                    }
                }
            }
        }
    }
}

private val CapsuleShape = RoundedCornerShape(percent = 50)

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun TrackRow(
    item: PlaylistItem,
    index: Int,
    isPlaying: Boolean,
    isSaved: Boolean = false,
    isFavorite: Boolean = false,
    isDownloading: Boolean = false,
    onClick: () -> Unit,
    onDownload: (() -> Unit)? = null,
    onToggleFavorite: (() -> Unit)? = null,
    onAddToPlaylist: (() -> Unit)? = null,
    onDelete: (() -> Unit)? = null,
    deleteIcon: androidx.compose.ui.graphics.vector.ImageVector = Icons.Default.Delete,
    deleteContentDescription: String = "Xóa"
) {
    val bgColor = if (isPlaying) Color.White.copy(alpha = 0.08f) else Color.Transparent

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Thumbnail with rounded corners matching iOS
        Box(
            modifier = Modifier
                .size(46.dp)
                .clip(RoundedCornerShape(10.dp))
                .background(Color(0xFF201B2E)),
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
                modifier = Modifier.size(46.dp)
            )

            if (isPlaying) {
                Box(
                    modifier = Modifier
                        .matchParentSize()
                        .background(Color.Black.copy(alpha = 0.55f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.GraphicEq,
                        contentDescription = "Playing",
                        tint = NeonPink,
                        modifier = Modifier.size(22.dp)
                    )
                }
            }
        }

        Spacer(modifier = Modifier.width(12.dp))

        // Title and Subtitle
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title.ifBlank { "Bài hát" },
                color = if (isPlaying) NeonPink else Color.White,
                fontSize = 14.sp,
                fontWeight = if (isPlaying) FontWeight.Bold else FontWeight.Medium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )

            Spacer(modifier = Modifier.height(2.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                if (isSaved) {
                    Text(
                        text = "Offline",
                        color = Color(0xFF22C55E),
                        fontSize = 10.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(Color(0xFF22C55E).copy(alpha = 0.18f))
                            .padding(horizontal = 4.dp, vertical = 1.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                }

                if (item.artist.isNotBlank()) {
                    Text(
                        text = item.artist,
                        color = Color.White.copy(alpha = 0.55f),
                        fontSize = 11.5.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false)
                    )
                    Text(text = " • ", color = Color.White.copy(alpha = 0.55f), fontSize = 11.5.sp)
                }
                Text(
                    text = item.formattedDuration,
                    color = Color.White.copy(alpha = 0.55f),
                    fontSize = 11.5.sp
                )
            }
        }

        // Action Buttons Row: Favorite, Download, Add, Delete
        Row(verticalAlignment = Alignment.CenterVertically) {
            // Favorite Button
            if (onToggleFavorite != null) {
                IconButton(
                    onClick = onToggleFavorite,
                    modifier = Modifier.size(34.dp)
                ) {
                    Icon(
                        imageVector = if (isFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                        contentDescription = "Yêu thích",
                        tint = if (isFavorite) Color(0xFFFF2D55) else Color.White.copy(alpha = 0.45f),
                        modifier = Modifier.size(19.dp)
                    )
                }
            }

            // Download Offline Button
            if (onDownload != null) {
                if (isDownloading) {
                    Box(
                        modifier = Modifier.size(34.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            color = Color(0xFF22C55E),
                            strokeWidth = 2.dp
                        )
                    }
                } else if (isSaved) {
                    Box(
                        modifier = Modifier.size(34.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Default.Check,
                            contentDescription = "Đã tải",
                            tint = Color(0xFF22C55E),
                            modifier = Modifier.size(18.dp)
                        )
                    }
                } else {
                    IconButton(
                        onClick = onDownload,
                        modifier = Modifier.size(34.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Download,
                            contentDescription = "Tải về nghe offline",
                            tint = Color.White.copy(alpha = 0.5f),
                            modifier = Modifier.size(19.dp)
                        )
                    }
                }
            }

            // Add to Playlist Button
            if (onAddToPlaylist != null) {
                IconButton(
                    onClick = onAddToPlaylist,
                    modifier = Modifier.size(34.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Add,
                        contentDescription = "Thêm vào danh sách",
                        tint = NeonPink,
                        modifier = Modifier.size(19.dp)
                    )
                }
            }

            // Delete Button
            if (onDelete != null) {
                IconButton(
                    onClick = onDelete,
                    modifier = Modifier.size(34.dp)
                ) {
                    Icon(
                        imageVector = deleteIcon,
                        contentDescription = deleteContentDescription,
                        tint = Color.White.copy(alpha = 0.45f),
                        modifier = Modifier.size(17.dp)
                    )
                }
            }
        }
    }
}
