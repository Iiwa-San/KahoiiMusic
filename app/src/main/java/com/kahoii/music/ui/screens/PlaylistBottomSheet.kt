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
import androidx.compose.foundation.layout.widthIn
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
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.filled.PlaylistAdd
import androidx.compose.material.icons.filled.Search
import androidx.compose.ui.graphics.SolidColor
import com.kahoii.music.ui.components.AddToCollectionDialog
import com.kahoii.music.util.ArtistUtils.removeDiacritics
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.ScrollableTabRow
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRowDefaults
import androidx.compose.material3.TabRowDefaults.tabIndicatorOffset
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
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
import com.kahoii.music.model.UserPlaylist
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.ui.theme.SurfaceDark
import com.kahoii.music.ui.theme.SurfaceElevated
import com.kahoii.music.ui.theme.TextMuted
import com.kahoii.music.ui.theme.TextWhite
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class, ExperimentalFoundationApi::class)
@Composable
fun PlaylistBottomSheet(
    playlist: List<PlaylistItem>,
    playlists: List<UserPlaylist> = emptyList(),
    history: List<PlaylistItem>,
    savedTracks: List<PlaylistItem>,
    favorites: List<PlaylistItem>,
    downloadingTrackIds: Set<String>,
    currentTrackId: String?,
    currentDurationMs: Long = 0L,
    isPlaylistLoading: Boolean,
    currentPlaylistTitle: String? = null,
    initialTab: Int = 0,
    onPlaylistTrackSelected: (PlaylistItem, Int) -> Unit,
    onCollectionTrackSelected: (UserPlaylist, Int) -> Unit = { _, _ -> },
    onSaveCurrentPlaylist: (String) -> Unit = {},
    onCreateCollection: (String) -> Unit = {},
    onDeleteCollection: (String) -> Unit = {},
    onRenameCollection: (String, String) -> Unit = { _, _ -> },
    onPlayCollection: (UserPlaylist) -> Unit = {},
    onDownloadCollection: (UserPlaylist) -> Unit = {},
    onAddTrackToCollection: (String, PlaylistItem) -> Unit = { _, _ -> },
    onRemoveTrackFromCollection: (String, String) -> Unit = { _, _ -> },
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
    isLoggedIn: Boolean = false,
    onOpenAccount: () -> Unit = {},
    onDismissRequest: () -> Unit
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = false)
    var selectedTab by remember { mutableIntStateOf(initialTab.coerceIn(0, 4)) }
    val context = LocalContext.current
    val haptic = LocalHapticFeedback.current

    var showSavePlaylistDialog by remember { mutableStateOf(false) }
    var savePlaylistName by remember { mutableStateOf("") }

    var showCreateCollectionDialog by remember { mutableStateOf(false) }
    var newCollectionName by remember { mutableStateOf("") }

    var showRenameDialog by remember { mutableStateOf(false) }
    var renameCollectionId by remember { mutableStateOf("") }
    var renameCollectionName by remember { mutableStateOf("") }

    var inspectingCollectionId by remember { mutableStateOf<String?>(null) }
    var trackForCollectionPicker by remember { mutableStateOf<PlaylistItem?>(null) }

    var searchPlaylistQuery by remember { mutableStateOf("") }
    var searchCollectionsQuery by remember { mutableStateOf("") }
    var searchCollectionTracksQuery by remember { mutableStateOf("") }
    var searchSavedQuery by remember { mutableStateOf("") }
    var searchHistoryQuery by remember { mutableStateOf("") }
    var searchFavoritesQuery by remember { mutableStateOf("") }

    ModalBottomSheet(
        onDismissRequest = onDismissRequest,
        sheetState = sheetState,
        containerColor = Color(0xFF140F24),
        dragHandle = null
    ) {
        Box(
            modifier = Modifier.fillMaxWidth(),
            contentAlignment = Alignment.Center
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .widthIn(max = 680.dp)
                    .fillMaxHeight(0.85f)
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

            // 5 Tabs: Danh sách | Bộ sưu tập | Đã tải | Lịch sử | Yêu thích
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
                            text = "Bộ sưu tập (${playlists.size})",
                            fontWeight = if (selectedTab == 1) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 1) Color(0xFFC084FC) else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 2,
                    onClick = { selectedTab = 2 },
                    text = {
                        Text(
                            text = "Đã tải (${savedTracks.size})",
                            fontWeight = if (selectedTab == 2) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 2) Color(0xFF22C55E) else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 3,
                    onClick = { selectedTab = 3 },
                    text = {
                        Text(
                            text = "Lịch sử (${if (isLoggedIn) history.size else 0})",
                            fontWeight = if (selectedTab == 3) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 3) NeonPink else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 4,
                    onClick = { selectedTab = 4 },
                    text = {
                        Text(
                            text = "Yêu thích (${favorites.size})",
                            fontWeight = if (selectedTab == 4) FontWeight.Bold else FontWeight.Normal,
                            color = if (selectedTab == 4) Color(0xFFFF2D55) else Color.White.copy(alpha = 0.6f),
                            fontSize = 13.5.sp
                        )
                    }
                )
            }

            // ================= TAB 0: PLAYLIST QUEUE =================
            if (selectedTab == 0) {
                val filteredPlaylist = remember(playlist, searchPlaylistQuery) {
                    if (searchPlaylistQuery.isBlank()) playlist
                    else {
                        val q = removeDiacritics(searchPlaylistQuery)
                        playlist.filter {
                            removeDiacritics(it.title).contains(q) ||
                            removeDiacritics(it.displayArtist).contains(q)
                        }
                    }
                }

                if (playlist.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (searchPlaylistQuery.isBlank()) "Danh sách chờ (${playlist.size} bài)"
                                   else "Kết quả (${filteredPlaylist.size}/${playlist.size} bài)",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )

                        Row(verticalAlignment = Alignment.CenterVertically) {
                            // Lưu vào bộ sưu tập button
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier
                                    .clip(RoundedCornerShape(8.dp))
                                    .background(Color(0xFF9333EA).copy(alpha = 0.35f))
                                    .clickable {
                                        savePlaylistName = currentPlaylistTitle ?: "Danh sách phát ${SimpleDateFormat("dd/MM/yyyy HH:mm", Locale.getDefault()).format(Date())}"
                                        showSavePlaylistDialog = true
                                    }
                                    .padding(horizontal = 10.dp, vertical = 5.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Download,
                                    contentDescription = "Save Playlist",
                                    tint = Color(0xFFC084FC),
                                    modifier = Modifier.size(15.dp)
                                )
                                Spacer(modifier = Modifier.width(4.dp))
                                Text(
                                    text = "Lưu BST",
                                    color = Color(0xFFE9D5FF),
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.SemiBold
                                )
                            }

                            Spacer(modifier = Modifier.width(8.dp))

                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                modifier = Modifier
                                    .clip(RoundedCornerShape(8.dp))
                                    .clickable { onClearPlaylist() }
                                    .padding(horizontal = 8.dp, vertical = 5.dp)
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

                    PlaylistSearchBar(
                        query = searchPlaylistQuery,
                        onQueryChange = { searchPlaylistQuery = it },
                        placeholder = "Tìm trong danh sách chờ..."
                    )
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
                } else if (filteredPlaylist.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Không tìm thấy bài hát nào phù hợp với \"$searchPlaylistQuery\"",
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
                        itemsIndexed(filteredPlaylist, key = { index, item -> "pl_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)
                            val originalIndex = playlist.indexOfFirst { it.id == item.id }.takeIf { it >= 0 } ?: index

                            TrackRow(
                                item = item,
                                index = originalIndex + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = isFav,
                                isDownloading = isDownloading,
                                currentDurationMs = currentDurationMs,
                                onClick = { onPlaylistTrackSelected(item, originalIndex) },
                                onDownload = { onDownloadTrack(item) },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onAddToCollection = { trackForCollectionPicker = item },
                                onDelete = { onRemoveFromPlaylist(originalIndex) },
                                deleteIcon = Icons.Default.Close,
                                deleteContentDescription = "Xóa khỏi danh sách"
                            )
                        }
                    }
                }
            }
            // ================= TAB 1: BỘ SƯU TẬP (COLLECTIONS) =================
            else if (selectedTab == 1) {
                val currentInspecting = playlists.find { it.id == inspectingCollectionId }
                if (currentInspecting != null) {
                    // Chi tiết 1 bộ sưu tập (Có thanh tìm kiếm riêng cho bài hát trong BST)
                    val filteredCollectionTracks = remember(currentInspecting.items, searchCollectionTracksQuery) {
                        if (searchCollectionTracksQuery.isBlank()) currentInspecting.items
                        else {
                            val q = removeDiacritics(searchCollectionTracksQuery)
                            currentInspecting.items.filter {
                                removeDiacritics(it.title).contains(q) ||
                                removeDiacritics(it.displayArtist).contains(q)
                            }
                        }
                    }

                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .weight(1f)
                                .clickable {
                                    inspectingCollectionId = null
                                    searchCollectionTracksQuery = ""
                                }
                        ) {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = "Quay lại",
                                tint = NeonPink,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                text = currentInspecting.name,
                                color = Color.White,
                                fontSize = 14.sp,
                                fontWeight = FontWeight.Bold,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                        }

                        Row(verticalAlignment = Alignment.CenterVertically) {
                            if (currentInspecting.items.isNotEmpty()) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    modifier = Modifier
                                        .clip(RoundedCornerShape(8.dp))
                                        .background(Color(0xFF9333EA).copy(alpha = 0.85f))
                                        .clickable { onPlayCollection(currentInspecting) }
                                        .padding(horizontal = 10.dp, vertical = 6.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.PlayArrow,
                                        contentDescription = "Phát tất cả",
                                        tint = Color.White,
                                        modifier = Modifier.size(15.dp)
                                    )
                                    Spacer(modifier = Modifier.width(4.dp))
                                    Text(
                                        text = "Phát",
                                        color = Color.White,
                                        fontSize = 12.sp,
                                        fontWeight = FontWeight.Bold
                                    )
                                }

                                Spacer(modifier = Modifier.width(6.dp))

                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    modifier = Modifier
                                        .clip(RoundedCornerShape(8.dp))
                                        .background(Color.White.copy(alpha = 0.15f))
                                        .clickable { onDownloadCollection(currentInspecting) }
                                        .padding(horizontal = 10.dp, vertical = 6.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Download,
                                        contentDescription = "Tải",
                                        tint = Color.White,
                                        modifier = Modifier.size(15.dp)
                                    )
                                    Spacer(modifier = Modifier.width(4.dp))
                                    Text(
                                        text = "Tải",
                                        color = Color.White,
                                        fontSize = 12.sp,
                                        fontWeight = FontWeight.Medium
                                    )
                                }
                            }
                        }
                    }

                    if (currentInspecting.items.isNotEmpty()) {
                        PlaylistSearchBar(
                            query = searchCollectionTracksQuery,
                            onQueryChange = { searchCollectionTracksQuery = it },
                            placeholder = "Tìm bài hát trong ${currentInspecting.name}..."
                        )
                    }

                    if (currentInspecting.items.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Bộ sưu tập này chưa có bài hát nào\nLưu playlist hoặc thêm bài hát để nghe tại đây",
                                color = Color.White.copy(alpha = 0.5f),
                                fontSize = 13.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    } else if (filteredCollectionTracks.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Không tìm thấy bài hát nào phù hợp với \"$searchCollectionTracksQuery\"",
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
                            itemsIndexed(filteredCollectionTracks, key = { index, item -> "coll_${item.id}_$index" }) { index, item ->
                                val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                                val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                                val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                                val isDownloading = downloadingTrackIds.contains(item.id)
                                val originalIndex = currentInspecting.items.indexOfFirst { it.id == item.id }.takeIf { it >= 0 } ?: index

                                TrackRow(
                                    item = item,
                                    index = originalIndex + 1,
                                    isPlaying = isPlayingThis,
                                    isSaved = isSaved,
                                    isFavorite = isFav,
                                    isDownloading = isDownloading,
                                    currentDurationMs = currentDurationMs,
                                    onClick = { onCollectionTrackSelected(currentInspecting, originalIndex) },
                                    onDownload = { onDownloadTrack(item) },
                                    onToggleFavorite = { onToggleFavorite(item) },
                                    onDelete = { onRemoveTrackFromCollection(currentInspecting.id, item.id) },
                                    deleteIcon = Icons.Default.Close,
                                    deleteContentDescription = "Xóa khỏi bộ sưu tập"
                                )
                            }
                        }
                    }
                } else {
                    // Danh sách tất cả các bộ sưu tập đã lưu (Có thanh tìm kiếm riêng cho tên bộ sưu tập)
                    val filteredPlaylists = remember(playlists, searchCollectionsQuery) {
                        if (searchCollectionsQuery.isBlank()) playlists
                        else {
                            val q = removeDiacritics(searchCollectionsQuery)
                            playlists.filter {
                                removeDiacritics(it.name).contains(q)
                            }
                        }
                    }

                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (searchCollectionsQuery.isBlank()) "${playlists.size} bộ sưu tập đã lưu"
                                   else "Kết quả (${filteredPlaylists.size}/${playlists.size} bộ sưu tập)",
                            color = Color.White.copy(alpha = 0.7f),
                            fontSize = 12.sp
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .clip(RoundedCornerShape(8.dp))
                                .background(Color(0xFF9333EA).copy(alpha = 0.35f))
                                .clickable {
                                    newCollectionName = ""
                                    showCreateCollectionDialog = true
                                }
                                .padding(horizontal = 10.dp, vertical = 5.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Add,
                                contentDescription = "Tạo mới",
                                tint = Color(0xFFC084FC),
                                modifier = Modifier.size(15.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "+ Tạo mới",
                                color = Color(0xFFE9D5FF),
                                fontSize = 12.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                        }
                    }

                    if (playlists.isNotEmpty()) {
                        PlaylistSearchBar(
                            query = searchCollectionsQuery,
                            onQueryChange = { searchCollectionsQuery = it },
                            placeholder = "Tìm bộ sưu tập..."
                        )
                    }

                    if (playlists.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Chưa có bộ sưu tập nào\nBấm nút 'Lưu BST' ở tab Danh sách để lưu",
                                color = Color.White.copy(alpha = 0.5f),
                                fontSize = 13.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    } else if (filteredPlaylists.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Không tìm thấy bộ sưu tập nào phù hợp với \"$searchCollectionsQuery\"",
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
                            itemsIndexed(filteredPlaylists, key = { index, item -> "user_pl_${item.id}_$index" }) { index, p ->
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(horizontal = 12.dp, vertical = 4.dp)
                                        .clip(RoundedCornerShape(12.dp))
                                        .background(Color.White.copy(alpha = 0.05f))
                                        .clickable {
                                            searchCollectionTracksQuery = ""
                                            inspectingCollectionId = p.id
                                        }
                                        .padding(horizontal = 14.dp, vertical = 12.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Box(
                                        modifier = Modifier
                                            .size(42.dp)
                                            .clip(RoundedCornerShape(8.dp))
                                            .background(Color(0xFF9333EA).copy(alpha = 0.25f)),
                                        contentAlignment = Alignment.Center
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.PlayArrow,
                                            contentDescription = null,
                                            tint = Color(0xFFC084FC),
                                            modifier = Modifier.size(22.dp)
                                        )
                                    }

                                    Spacer(modifier = Modifier.width(12.dp))

                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            text = p.name,
                                            color = Color.White,
                                            fontSize = 14.5.sp,
                                            fontWeight = FontWeight.Bold,
                                            maxLines = 1,
                                            overflow = TextOverflow.Ellipsis
                                        )
                                        Spacer(modifier = Modifier.height(2.dp))
                                        Text(
                                            text = "♫ ${p.items.size} bài hát",
                                            color = Color(0xFFC084FC),
                                            fontSize = 12.sp
                                        )
                                    }

                                    if (p.items.isNotEmpty()) {
                                        IconButton(onClick = { onPlayCollection(p) }) {
                                            Icon(
                                                imageVector = Icons.Default.PlayArrow,
                                                contentDescription = "Phát tất cả",
                                                tint = Color.White,
                                                modifier = Modifier.size(20.dp)
                                            )
                                        }
                                    }

                                    IconButton(onClick = {
                                        renameCollectionId = p.id
                                        renameCollectionName = p.name
                                        showRenameDialog = true
                                    }) {
                                        Icon(
                                            imageVector = Icons.Default.Add,
                                            contentDescription = "Đổi tên",
                                            tint = Color.White.copy(alpha = 0.6f),
                                            modifier = Modifier.size(18.dp)
                                        )
                                    }

                                    IconButton(onClick = { onDeleteCollection(p.id) }) {
                                        Icon(
                                            imageVector = Icons.Default.Delete,
                                            contentDescription = "Xóa",
                                            tint = Color(0xFFFF5252),
                                            modifier = Modifier.size(18.dp)
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // ================= TAB 2: SAVED (ĐÃ TẢI OFFLINE) =================
            else if (selectedTab == 2) {
                val filteredSaved = remember(savedTracks, searchSavedQuery) {
                    if (searchSavedQuery.isBlank()) savedTracks
                    else {
                        val q = removeDiacritics(searchSavedQuery)
                        savedTracks.filter {
                            removeDiacritics(it.title).contains(q) ||
                            removeDiacritics(it.displayArtist).contains(q)
                        }
                    }
                }

                if (savedTracks.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (searchSavedQuery.isBlank()) "${savedTracks.size} bài hát đã tải về máy"
                                   else "Kết quả (${filteredSaved.size}/${savedTracks.size} bài)",
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

                    PlaylistSearchBar(
                        query = searchSavedQuery,
                        onQueryChange = { searchSavedQuery = it },
                        placeholder = "Tìm bài hát đã tải..."
                    )
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
                            modifier = Modifier.padding(24.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Download,
                                contentDescription = null,
                                tint = Color.White.copy(alpha = 0.25f),
                                modifier = Modifier.size(48.dp)
                            )
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = "Chưa có bài hát đã tải",
                                color = Color.White,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Bấm biểu tượng tải xuống ở bất kỳ bài hát nào để nghe ngoại tuyến không cần mạng.",
                                color = TextMuted,
                                fontSize = 12.5.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                } else if (filteredSaved.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Không tìm thấy bài hát nào phù hợp với \"$searchSavedQuery\"",
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
                        itemsIndexed(filteredSaved, key = { index, item -> "saved_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                            val originalIndex = savedTracks.indexOfFirst { it.id == item.id }.takeIf { it >= 0 } ?: index

                            TrackRow(
                                item = item,
                                index = originalIndex + 1,
                                isPlaying = isPlayingThis,
                                isSaved = true,
                                isFavorite = isFav,
                                isDownloading = false,
                                currentDurationMs = currentDurationMs,
                                onClick = { onSavedTrackSelected(item, originalIndex) },
                                onAddToPlaylist = {
                                    onAddToPlaylist(item)
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onAddToCollection = { trackForCollectionPicker = item },
                                onDelete = { onDeleteSavedTrack(item.id) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Xóa khỏi bộ nhớ"
                            )
                        }
                    }
                }
            }
            // ================= TAB 3: HISTORY =================
            else if (selectedTab == 3) {
                val filteredHistory = remember(history, searchHistoryQuery) {
                    if (searchHistoryQuery.isBlank()) history
                    else {
                        val q = removeDiacritics(searchHistoryQuery)
                        history.filter {
                            removeDiacritics(it.title).contains(q) ||
                            removeDiacritics(it.displayArtist).contains(q)
                        }
                    }
                }

                if (!isLoggedIn) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f)
                            .padding(24.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center
                    ) {
                        Box(
                            modifier = Modifier
                                .size(56.dp)
                                .clip(CircleShape)
                                .background(NeonPink.copy(alpha = 0.15f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                imageVector = Icons.Default.Lock,
                                contentDescription = null,
                                tint = NeonPink,
                                modifier = Modifier.size(28.dp)
                            )
                        }
                        Spacer(modifier = Modifier.height(14.dp))
                        Text(
                            text = "Yêu cầu đăng nhập tài khoản",
                            color = Color.White,
                            fontWeight = FontWeight.Bold,
                            fontSize = 16.sp
                        )
                        Spacer(modifier = Modifier.height(6.dp))
                        Text(
                            text = "Đăng nhập tài khoản Kahoii để xem và lưu lịch sử nghe nhạc",
                            color = Color.White.copy(alpha = 0.65f),
                            fontSize = 13.sp,
                            textAlign = androidx.compose.ui.text.style.TextAlign.Center
                        )
                        Spacer(modifier = Modifier.height(18.dp))
                        Button(
                            onClick = { onOpenAccount() },
                            colors = ButtonDefaults.buttonColors(containerColor = NeonPink),
                            shape = RoundedCornerShape(20.dp),
                            contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 24.dp, vertical = 10.dp)
                        ) {
                            Text(
                                text = "Đăng Nhập Ngay",
                                color = Color.White,
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.5.sp
                            )
                        }
                    }
                } else {
                    if (history.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (searchHistoryQuery.isBlank()) "Lịch sử đã nghe gần đây"
                                   else "Kết quả (${filteredHistory.size}/${history.size} bài)",
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

                    PlaylistSearchBar(
                        query = searchHistoryQuery,
                        onQueryChange = { searchHistoryQuery = it },
                        placeholder = "Tìm trong lịch sử nghe..."
                    )
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
                } else if (filteredHistory.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Không tìm thấy bài hát nào phù hợp với \"$searchHistoryQuery\"",
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
                        itemsIndexed(filteredHistory, key = { index, item -> "hist_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isFav = favorites.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)
                            val originalIndex = history.indexOfFirst { it.id == item.id }.takeIf { it >= 0 } ?: index

                            TrackRow(
                                item = item,
                                index = originalIndex + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = isFav,
                                isDownloading = isDownloading,
                                currentDurationMs = currentDurationMs,
                                onClick = { onHistoryTrackSelected(item, originalIndex) },
                                onAddToPlaylist = {
                                    onAddToPlaylist(item)
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onAddToCollection = { trackForCollectionPicker = item },
                                onDownload = { onDownloadTrack(item) },
                                onDelete = { onDeleteHistoryItem(item.id) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Xóa khỏi lịch sử"
                            )
                        }
                    }
                }
            }
            }
            // ================= TAB 4: FAVORITES (YÊU THÍCH) =================
            else {
                val filteredFavorites = remember(favorites, searchFavoritesQuery) {
                    if (searchFavoritesQuery.isBlank()) favorites
                    else {
                        val q = removeDiacritics(searchFavoritesQuery)
                        favorites.filter {
                            removeDiacritics(it.title).contains(q) ||
                            removeDiacritics(it.displayArtist).contains(q)
                        }
                    }
                }

                if (favorites.isNotEmpty()) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (searchFavoritesQuery.isBlank()) "${favorites.size} bài hát đã thích"
                                   else "Kết quả (${filteredFavorites.size}/${favorites.size} bài)",
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

                    PlaylistSearchBar(
                        query = searchFavoritesQuery,
                        onQueryChange = { searchFavoritesQuery = it },
                        placeholder = "Tìm trong bài hát yêu thích..."
                    )
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
                            modifier = Modifier.padding(24.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.FavoriteBorder,
                                contentDescription = null,
                                tint = Color.White.copy(alpha = 0.25f),
                                modifier = Modifier.size(48.dp)
                            )
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = "Chưa có bài hát yêu thích",
                                color = Color.White,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Bấm biểu tượng trái tim ở góc màn hình nghe nhạc để thêm bài hát vào đây.",
                                color = TextMuted,
                                fontSize = 12.5.sp,
                                textAlign = TextAlign.Center
                            )
                        }
                    }
                } else if (filteredFavorites.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = "Không tìm thấy bài hát nào phù hợp với \"$searchFavoritesQuery\"",
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
                        itemsIndexed(filteredFavorites, key = { index, item -> "fav_${item.id}_$index" }) { index, item ->
                            val isPlayingThis = item.id.equals(currentTrackId, ignoreCase = true)
                            val isSaved = savedTracks.any { it.id.equals(item.id, ignoreCase = true) }
                            val isDownloading = downloadingTrackIds.contains(item.id)
                            val originalIndex = favorites.indexOfFirst { it.id == item.id }.takeIf { it >= 0 } ?: index

                            TrackRow(
                                item = item,
                                index = originalIndex + 1,
                                isPlaying = isPlayingThis,
                                isSaved = isSaved,
                                isFavorite = true,
                                isDownloading = isDownloading,
                                currentDurationMs = currentDurationMs,
                                onClick = { onFavoriteTrackSelected(item, originalIndex) },
                                onAddToPlaylist = {
                                    onAddToPlaylist(item)
                                },
                                onToggleFavorite = { onToggleFavorite(item) },
                                onAddToCollection = { trackForCollectionPicker = item },
                                onDownload = { onDownloadTrack(item) },
                                onDelete = { onToggleFavorite(item) },
                                deleteIcon = Icons.Default.Delete,
                                deleteContentDescription = "Bỏ yêu thích"
                            )
                        }
                    }
                }
            }

            // Dialog Lưu vào bộ sưu tập
            if (showSavePlaylistDialog) {
                AlertDialog(
                    onDismissRequest = { showSavePlaylistDialog = false },
                    title = { Text("Lưu vào bộ sưu tập", color = Color.White, fontWeight = FontWeight.Bold) },
                    text = {
                        Column {
                            Text("Nhập tên cho danh sách phát này:", color = Color.White.copy(alpha = 0.7f), fontSize = 13.sp)
                            Spacer(modifier = Modifier.height(8.dp))
                            OutlinedTextField(
                                value = savePlaylistName,
                                onValueChange = { savePlaylistName = it },
                                singleLine = true,
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedTextColor = Color.White,
                                    unfocusedTextColor = Color.White,
                                    focusedBorderColor = NeonPink,
                                    unfocusedBorderColor = Color.White.copy(alpha = 0.3f)
                                )
                            )
                        }
                    },
                    confirmButton = {
                        TextButton(onClick = {
                            if (savePlaylistName.isNotBlank()) {
                                onSaveCurrentPlaylist(savePlaylistName.trim())
                                showSavePlaylistDialog = false
                                selectedTab = 1
                            }
                        }) {
                            Text("Lưu", color = NeonPink, fontWeight = FontWeight.Bold)
                        }
                    },
                    dismissButton = {
                        TextButton(onClick = { showSavePlaylistDialog = false }) {
                            Text("Hủy", color = Color.White.copy(alpha = 0.6f))
                        }
                    },
                    containerColor = Color(0xFF1E1635)
                )
            }

            // Dialog Tạo bộ sưu tập mới
            if (showCreateCollectionDialog) {
                AlertDialog(
                    onDismissRequest = { showCreateCollectionDialog = false },
                    title = { Text("Tạo bộ sưu tập mới", color = Color.White, fontWeight = FontWeight.Bold) },
                    text = {
                        Column {
                            Text("Nhập tên bộ sưu tập:", color = Color.White.copy(alpha = 0.7f), fontSize = 13.sp)
                            Spacer(modifier = Modifier.height(8.dp))
                            OutlinedTextField(
                                value = newCollectionName,
                                onValueChange = { newCollectionName = it },
                                singleLine = true,
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedTextColor = Color.White,
                                    unfocusedTextColor = Color.White,
                                    focusedBorderColor = NeonPink,
                                    unfocusedBorderColor = Color.White.copy(alpha = 0.3f)
                                )
                            )
                        }
                    },
                    confirmButton = {
                        TextButton(onClick = {
                            if (newCollectionName.isNotBlank()) {
                                onCreateCollection(newCollectionName.trim())
                                showCreateCollectionDialog = false
                            }
                        }) {
                            Text("Tạo", color = NeonPink, fontWeight = FontWeight.Bold)
                        }
                    },
                    dismissButton = {
                        TextButton(onClick = { showCreateCollectionDialog = false }) {
                            Text("Hủy", color = Color.White.copy(alpha = 0.6f))
                        }
                    },
                    containerColor = Color(0xFF1E1635)
                )
            }

            // Dialog Đổi tên bộ sưu tập
            if (showRenameDialog) {
                AlertDialog(
                    onDismissRequest = { showRenameDialog = false },
                    title = { Text("Đổi tên bộ sưu tập", color = Color.White, fontWeight = FontWeight.Bold) },
                    text = {
                        Column {
                            Text("Nhập tên mới:", color = Color.White.copy(alpha = 0.7f), fontSize = 13.sp)
                            Spacer(modifier = Modifier.height(8.dp))
                            OutlinedTextField(
                                value = renameCollectionName,
                                onValueChange = { renameCollectionName = it },
                                singleLine = true,
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedTextColor = Color.White,
                                    unfocusedTextColor = Color.White,
                                    focusedBorderColor = NeonPink,
                                    unfocusedBorderColor = Color.White.copy(alpha = 0.3f)
                                )
                            )
                        }
                    },
                    confirmButton = {
                        TextButton(onClick = {
                            if (renameCollectionName.isNotBlank()) {
                                onRenameCollection(renameCollectionId, renameCollectionName.trim())
                                showRenameDialog = false
                            }
                        }) {
                            Text("Lưu", color = NeonPink, fontWeight = FontWeight.Bold)
                        }
                    },
                    dismissButton = {
                        TextButton(onClick = { showRenameDialog = false }) {
                            Text("Hủy", color = Color.White.copy(alpha = 0.6f))
                        }
                    },
                    containerColor = Color(0xFF1E1635)
                )
            }
        }
    }

        // Dialog thêm từng bài vào bộ sưu tập
        if (trackForCollectionPicker != null) {
            val targetTrack = trackForCollectionPicker!!
            AddToCollectionDialog(
                track = targetTrack,
                collections = playlists,
                onSelectCollection = { collectionId ->
                    onAddTrackToCollection(collectionId, targetTrack)
                    trackForCollectionPicker = null
                },
                onCreateCollectionAndAdd = { newName ->
                    onCreateCollection(newName)
                    trackForCollectionPicker = null
                },
                onDismiss = { trackForCollectionPicker = null }
            )
        }
    }
}

@Composable
private fun PlaylistSearchBar(
    query: String,
    onQueryChange: (String) -> Unit,
    placeholder: String,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 6.dp)
            .clip(RoundedCornerShape(10.dp))
            .background(Color.White.copy(alpha = 0.08f))
            .padding(horizontal = 12.dp, vertical = 8.dp)
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.fillMaxWidth()
        ) {
            Icon(
                imageVector = Icons.Default.Search,
                contentDescription = "Tìm kiếm",
                tint = Color.White.copy(alpha = 0.5f),
                modifier = Modifier.size(16.dp)
            )
            Spacer(modifier = Modifier.width(8.dp))
            Box(modifier = Modifier.weight(1f)) {
                if (query.isEmpty()) {
                    Text(
                        text = placeholder,
                        color = Color.White.copy(alpha = 0.4f),
                        fontSize = 13.sp
                    )
                }
                BasicTextField(
                    value = query,
                    onValueChange = onQueryChange,
                    singleLine = true,
                    textStyle = androidx.compose.ui.text.TextStyle(
                        color = Color.White,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Normal
                    ),
                    cursorBrush = SolidColor(NeonPink),
                    modifier = Modifier.fillMaxWidth()
                )
            }
            if (query.isNotEmpty()) {
                Spacer(modifier = Modifier.width(6.dp))
                Icon(
                    imageVector = Icons.Default.Close,
                    contentDescription = "Xóa tìm kiếm",
                    tint = Color.White.copy(alpha = 0.6f),
                    modifier = Modifier
                        .size(16.dp)
                        .clickable { onQueryChange("") }
                )
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
    currentDurationMs: Long = 0L,
    onClick: () -> Unit,
    onDownload: (() -> Unit)? = null,
    onToggleFavorite: (() -> Unit)? = null,
    onAddToPlaylist: (() -> Unit)? = null,
    onAddToCollection: (() -> Unit)? = null,
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
                        .size(46.dp)
                        .background(Color.Black.copy(alpha = 0.5f)),
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

        // Title and duration
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title.ifBlank { "Bài $index" },
                color = if (isPlaying) NeonPink else Color.White,
                fontSize = 14.5.sp,
                fontWeight = if (isPlaying) FontWeight.Bold else FontWeight.Normal,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )

            Spacer(modifier = Modifier.height(2.dp))

            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.fillMaxWidth()
            ) {
                val artistText = item.displayArtist
                if (artistText.isNotBlank()) {
                    Text(
                        text = artistText,
                        color = Color.White.copy(alpha = 0.65f),
                        fontSize = 12.sp,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = "•",
                        color = TextMuted,
                        fontSize = 11.sp
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                }

                val durText = if (isPlaying && currentDurationMs > 0) {
                    val sec = (currentDurationMs / 1000).toInt()
                    val m = sec / 60
                    val s = sec % 60
                    String.format(Locale.getDefault(), "%02d:%02d", m, s)
                } else {
                    item.formattedDuration
                }

                Text(
                    text = durText,
                    color = TextMuted,
                    fontSize = 12.sp
                )

                if (isSaved) {
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = "• Đã tải",
                        color = Color(0xFF22C55E),
                        fontSize = 11.5.sp
                    )
                }
            }
        }

        Spacer(modifier = Modifier.width(8.dp))

        // Action Buttons: Favorite, Download, Add, Delete
        Row(verticalAlignment = Alignment.CenterVertically) {
            if (onToggleFavorite != null) {
                IconButton(
                    onClick = onToggleFavorite,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = if (isFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                        contentDescription = if (isFavorite) "Bỏ thích" else "Yêu thích",
                        tint = if (isFavorite) Color(0xFFFF2D55) else Color.White.copy(alpha = 0.6f),
                        modifier = Modifier.size(19.dp)
                    )
                }
            }

            if (onDownload != null) {
                IconButton(
                    onClick = onDownload,
                    enabled = !isSaved && !isDownloading,
                    modifier = Modifier.size(36.dp)
                ) {
                    when {
                        isDownloading -> {
                            CircularProgressIndicator(
                                color = NeonPink,
                                strokeWidth = 2.dp,
                                modifier = Modifier.size(18.dp)
                            )
                        }
                        isSaved -> {
                            Icon(
                                imageVector = Icons.Default.Check,
                                contentDescription = "Đã lưu",
                                tint = Color(0xFF22C55E),
                                modifier = Modifier.size(19.dp)
                            )
                        }
                        else -> {
                            Icon(
                                imageVector = Icons.Default.Download,
                                contentDescription = "Tải về máy",
                                tint = Color.White.copy(alpha = 0.6f),
                                modifier = Modifier.size(19.dp)
                            )
                        }
                    }
                }
            }

            if (onAddToPlaylist != null) {
                IconButton(
                    onClick = onAddToPlaylist,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Add,
                        contentDescription = "Thêm vào danh sách phát",
                        tint = Color.White.copy(alpha = 0.6f),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }

            if (onAddToCollection != null) {
                IconButton(
                    onClick = onAddToCollection,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.PlaylistAdd,
                        contentDescription = "Thêm vào bộ sưu tập",
                        tint = Color.White.copy(alpha = 0.6f),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }

            if (onDelete != null) {
                IconButton(
                    onClick = onDelete,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = deleteIcon,
                        contentDescription = deleteContentDescription,
                        tint = Color.White.copy(alpha = 0.6f),
                        modifier = Modifier.size(19.dp)
                    )
                }
            }
        }
    }
}
