package com.kahoii.music.ui.screens

import android.content.Intent
import androidx.compose.foundation.background
import androidx.compose.foundation.Image
import androidx.compose.foundation.border
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.GraphicEq
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.outlined.AccountCircle
import com.kahoii.music.ui.components.AccountBottomSheet
import com.kahoii.music.ui.components.CellularDownloadDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.model.ActiveQueueSource
import com.kahoii.music.model.PlaybackStatus
import com.kahoii.music.model.UserPlaylist
import com.kahoii.music.ui.components.ArtworkView
import com.kahoii.music.ui.components.BottomActionsSection
import com.kahoii.music.ui.components.DynamicGradientView
import com.kahoii.music.ui.components.MarqueeText
import com.kahoii.music.ui.components.MusicTrackBarView
import com.kahoii.music.ui.components.PlaybackControlsView
import com.kahoii.music.ui.components.SearchPillBox
import com.kahoii.music.ui.components.SleepTimerDialog
import com.kahoii.music.ui.components.VolumeSliderSection
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.viewmodel.MainViewModel

@Composable
fun MusicPlayerScreen(
    viewModel: MainViewModel,
    modifier: Modifier = Modifier
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    val focusManager = LocalFocusManager.current
    val keyboardController = LocalSoftwareKeyboardController.current

    var showPlaylistSheet by remember { mutableStateOf(false) }
    var selectedSheetTab by remember { mutableIntStateOf(0) }
    var showSleepTimerDialog by remember { mutableStateOf(false) }
    var showAccountSheet by remember { mutableStateOf(false) }

    // Dynamic animated backdrop matching iOS DynamicGradientView
    DynamicGradientView(
        modifier = modifier
            .statusBarsPadding()
            .navigationBarsPadding(),
        accentColor = NeonPink
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .clickable(
                    interactionSource = remember { MutableInteractionSource() },
                    indication = null
                ) {
                    focusManager.clearFocus()
                    keyboardController?.hide()
                }
                .padding(horizontal = 14.dp, vertical = 6.dp),
            verticalArrangement = Arrangement.SpaceBetween,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // ================= 1. HEADER & SEARCH BAR =================
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                // Top Brand & Status Pill Row
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 2.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Logo & App Title matching iOS header
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Box(
                            modifier = Modifier
                                .size(28.dp)
                                .clip(CircleShape)
                                .background(NeonPink.copy(alpha = 0.2f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                imageVector = Icons.Default.GraphicEq,
                                contentDescription = "Logo",
                                tint = NeonPink,
                                modifier = Modifier.size(18.dp)
                            )
                        }

                        Text(
                            text = "Kahoii Music",
                            color = Color.White,
                            fontSize = 18.sp,
                            fontWeight = FontWeight.Bold,
                            style = MaterialTheme.typography.titleMedium
                        )
                    }

                    // Status Indicator Capsule & User Icon (Status to the left of User Icon)
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(
                            text = uiState.statusMessage,
                            color = Color.White.copy(alpha = 0.65f),
                            fontSize = 11.5.sp,
                            fontWeight = FontWeight.Medium,
                            modifier = Modifier
                                .clip(RoundedCornerShape(percent = 50))
                                .background(Color.White.copy(alpha = 0.08f))
                                .padding(horizontal = 10.dp, vertical = 4.dp)
                        )

                        val userAvatarBitmap = remember(uiState.currentUser?.avatarBase64) {
                            com.kahoii.music.util.ImageUtils.base64ToBitmap(uiState.currentUser?.avatarBase64)?.asImageBitmap()
                        }

                        if (uiState.isLoggedIn) {
                            Box(
                                modifier = Modifier
                                    .size(32.dp)
                                    .clickable { showAccountSheet = true },
                                contentAlignment = Alignment.Center
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(30.dp)
                                        .background(
                                            brush = Brush.linearGradient(
                                                colors = listOf(Color(0xFFFF2A85), Color(0xFF8A2BE2), Color(0xFF00E5FF))
                                            ),
                                            shape = CircleShape
                                        )
                                        .padding(1.5.dp)
                                        .clip(CircleShape)
                                        .background(Color(0xFF140F24)),
                                    contentAlignment = Alignment.Center
                                ) {
                                    if (userAvatarBitmap != null) {
                                        Image(
                                            bitmap = userAvatarBitmap,
                                            contentDescription = "Tài khoản",
                                            contentScale = ContentScale.Crop,
                                            modifier = Modifier.fillMaxSize().clip(CircleShape)
                                        )
                                    } else {
                                        Icon(
                                            imageVector = Icons.Default.AccountCircle,
                                            contentDescription = "Tài khoản",
                                            tint = NeonPink,
                                            modifier = Modifier.size(22.dp)
                                        )
                                    }
                                }

                                Box(
                                    modifier = Modifier
                                        .size(7.dp)
                                        .align(Alignment.TopEnd)
                                        .background(Color(0xFF00E676), CircleShape)
                                        .border(1.dp, Color(0xFF140F24), CircleShape)
                                )
                            }
                        } else {
                            IconButton(
                                onClick = { showAccountSheet = true },
                                modifier = Modifier.size(34.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Outlined.AccountCircle,
                                    contentDescription = "Tài khoản",
                                    tint = Color.White.copy(alpha = 0.85f),
                                    modifier = Modifier.size(26.dp)
                                )
                            }
                        }
                    }
                }

                // Search Pill Bar (with instant Clipboard Paste button)
                SearchPillBox(
                    onSearchSubmitted = { query ->
                        viewModel.loadAndPlay(query)
                    }
                )
            }

            Spacer(modifier = Modifier.height(6.dp))

            // ================= 2. ALBUM ARTWORK =================
            ArtworkView(
                thumbnailUrl = uiState.currentTrack?.thumbnailUrl,
                isPlaying = uiState.isPlaying,
                accentColor = NeonPink,
                onSwipeLeft = { viewModel.playNext() },
                onSwipeRight = { viewModel.playPrevious() },
                modifier = Modifier
                    .fillMaxWidth(0.85f)
                    .weight(1f, fill = false)
            )

            Spacer(modifier = Modifier.height(8.dp))

            // ================= 3. TRACK INFO & FAVORITE / DOWNLOAD =================
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 4.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Title and Artist
                Column(modifier = Modifier.weight(1f)) {
                    val currentTitle = uiState.currentTrack?.title?.ifBlank { "Chưa chọn bài hát" } ?: "Kahoii Music"
                    MarqueeText(
                        text = currentTitle,
                        style = MaterialTheme.typography.titleLarge,
                        color = Color.White,
                        modifier = Modifier.fillMaxWidth()
                    )

                    Spacer(modifier = Modifier.height(2.dp))

                    val subtitle = when {
                        uiState.currentTrack != null -> {
                            val queueInfo = uiState.activeQueueTitle
                            val artist = uiState.currentTrack?.displayArtist?.ifBlank { "YouTube" } ?: "YouTube"
                            val offlineTag = if (uiState.currentTrack?.isOffline == true) " • Offline" else ""
                            "$queueInfo • $artist$offlineTag"
                        }
                        else -> "Dán link hoặc tìm bài hát ở trên"
                    }

                    Text(
                        text = subtitle,
                        color = Color.White.copy(alpha = 0.6f),
                        fontSize = 13.5.sp,
                        fontWeight = FontWeight.Medium,
                        maxLines = 1
                    )
                }

                Spacer(modifier = Modifier.width(8.dp))

                // Favorite Heart Button
                IconButton(
                    onClick = { viewModel.toggleFavorite() },
                    enabled = uiState.currentTrack != null,
                    modifier = Modifier.size(38.dp)
                ) {
                    Icon(
                        imageVector = if (uiState.isCurrentTrackFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                        contentDescription = "Yêu thích",
                        tint = when {
                            uiState.currentTrack == null -> Color.White.copy(alpha = 0.2f)
                            uiState.isCurrentTrackFavorite -> Color(0xFFFF2D55)
                            else -> Color.White.copy(alpha = 0.5f)
                        },
                        modifier = Modifier.size(23.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            // ================= 4. MUSIC TRACK BAR (PROGRESS & SCRUBBER) =================
            MusicTrackBarView(
                currentPositionMs = uiState.currentPositionMs,
                durationMs = uiState.durationMs,
                accentColor = NeonPink,
                onSeekCommitted = { progress ->
                    viewModel.seekTo(progress)
                }
            )

            Spacer(modifier = Modifier.height(10.dp))

            // ================= 5. PLAYBACK CONTROLS (QUICK DOWNLOAD, SHUFFLE, PREV, PLAY, NEXT, LOOP, TIMER) =================
            PlaybackControlsView(
                isPlaying = uiState.isPlaying,
                isLoading = uiState.status == PlaybackStatus.BUFFERING,
                isShuffled = uiState.isShuffled,
                loopMode = uiState.loopMode,
                isSleepTimerActive = uiState.isSleepTimerActive,
                sleepTimerText = uiState.sleepTimerFormatted,
                accentColor = NeonPink,
                isCurrentTrackSaved = uiState.isCurrentTrackSaved,
                isCurrentTrackDownloading = uiState.isCurrentTrackDownloading,
                onQuickDownload = {
                    viewModel.quickDownloadCurrentTrack()
                },
                onTogglePlayPause = { viewModel.togglePlayPause() },
                onPrevious = { viewModel.playPrevious() },
                onNext = { viewModel.playNext() },
                onToggleShuffle = { viewModel.toggleShuffle() },
                onToggleLoop = { viewModel.toggleLoopMode() },
                onTimerClick = { showSleepTimerDialog = true }
            )

            Spacer(modifier = Modifier.height(12.dp))

            // ================= 6. VOLUME SLIDER =================
            VolumeSliderSection(
                volume = uiState.volume,
                onVolumeChanged = { viewModel.setVolume(it) },
                onToggleMute = { viewModel.toggleMute() }
            )

            Spacer(modifier = Modifier.height(12.dp))

            // ================= 7. BOTTOM NAVIGATION BAR (DANH SÁCH, ĐÃ TẢI, LỊCH SỬ, YÊU THÍCH, CHIA SẺ) =================
            BottomActionsSection(
                playlistCount = uiState.playlist.size,
                savedCount = uiState.savedTracks.size,
                favoritesCount = uiState.favorites.size,
                historyCount = uiState.history.size,
                onPlaylistClick = {
                    selectedSheetTab = 0
                    showPlaylistSheet = true
                },
                onSavedClick = {
                    selectedSheetTab = 2
                    showPlaylistSheet = true
                },
                onHistoryClick = {
                    selectedSheetTab = 3
                    showPlaylistSheet = true
                },
                onFavoritesClick = {
                    selectedSheetTab = 4
                    showPlaylistSheet = true
                },
                onShareClick = {
                    val track = uiState.currentTrack
                    if (track != null) {
                        val shareIntent = Intent(Intent.ACTION_SEND).apply {
                            type = "text/plain"
                            putExtra(Intent.EXTRA_SUBJECT, track.title)
                            putExtra(Intent.EXTRA_TEXT, "${track.title}\n${track.url}")
                        }
                        context.startActivity(Intent.createChooser(shareIntent, "Chia sẻ bài hát"))
                    }
                }
            )

            Spacer(modifier = Modifier.height(4.dp))
        }

        // ================= BOTTOM SHEETS & DIALOGS =================

        // 1. Unified 4-Tab Bottom Sheet (Danh sách, Đã tải, Lịch sử, Yêu thích)
        if (showPlaylistSheet) {
            PlaylistBottomSheet(
                playlist = uiState.playlist,
                playlists = uiState.playlists,
                history = uiState.history,
                savedTracks = uiState.savedTracks,
                favorites = uiState.favorites,
                downloadingTrackIds = uiState.downloadingTrackIds,
                currentTrackId = uiState.currentTrack?.id,
                currentDurationMs = uiState.durationMs,
                isPlaylistLoading = uiState.isPlaylistLoading,
                currentPlaylistTitle = uiState.currentPlaylistTitle,
                initialTab = selectedSheetTab,
                onPlaylistTrackSelected = { track, index ->
                    viewModel.playFromQueueIndex(index, ActiveQueueSource.PLAYLIST)
                },
                onCollectionTrackSelected = { collection, index ->
                    viewModel.playCollection(collection, index)
                },
                onSaveCurrentPlaylist = { name ->
                    viewModel.saveCurrentPlaylistToCollection(name)
                },
                onCreateCollection = { name ->
                    viewModel.createCollection(name)
                },
                onDeleteCollection = { id ->
                    viewModel.deleteCollection(id)
                },
                onRenameCollection = { id, newName ->
                    viewModel.renameCollection(id, newName)
                },
                onPlayCollection = { collection ->
                    viewModel.playCollection(collection, 0)
                },
                onDownloadCollection = { collection ->
                    viewModel.downloadCollection(collection)
                },
                onAddTrackToCollection = { playlistId, track ->
                    viewModel.addTrackToCollection(playlistId, track)
                },
                onRemoveTrackFromCollection = { playlistId, trackId ->
                    viewModel.removeTrackFromCollection(playlistId, trackId)
                },
                onHistoryTrackSelected = { track, index ->
                    viewModel.playFromQueueIndex(index, ActiveQueueSource.HISTORY)
                },
                onSavedTrackSelected = { track, index ->
                    viewModel.playFromQueueIndex(index, ActiveQueueSource.SAVED)
                },
                onFavoriteTrackSelected = { track, index ->
                    viewModel.playFromQueueIndex(index, ActiveQueueSource.FAVORITES)
                },
                onAddToPlaylist = { track ->
                    viewModel.addToPlaylist(track)
                },
                onRemoveFromPlaylist = { index ->
                    viewModel.removeFromPlaylist(index)
                },
                onClearPlaylist = {
                    viewModel.clearPlaylist()
                },
                onDeleteHistoryItem = { id ->
                    viewModel.removeHistoryItem(id)
                },
                onClearHistory = {
                    viewModel.clearHistory()
                },
                onToggleFavorite = { track ->
                    viewModel.toggleFavorite(track)
                },
                onDownloadTrack = { track ->
                    viewModel.saveTrackOffline(track)
                },
                onDeleteSavedTrack = { id ->
                    viewModel.deleteSavedTrack(id)
                },
                onPlayAllSavedTracks = {
                    viewModel.playAllSavedTracks()
                },
                onPlayAllFavorites = {
                    viewModel.playAllFavorites()
                },
                onDismissRequest = { showPlaylistSheet = false }
            )
        }

        // 2. Sleep Timer Dialog
        if (showSleepTimerDialog) {
            SleepTimerDialog(
                currentOption = uiState.sleepTimerOption,
                remainingFormatted = uiState.sleepTimerFormatted,
                onSelectOption = { option ->
                    viewModel.setSleepTimer(option)
                },
                onDismissRequest = { showSleepTimerDialog = false }
            )
        }

        // 3. Account Bottom Sheet
        if (showAccountSheet) {
            AccountBottomSheet(
                currentUser = uiState.currentUser,
                isSyncing = uiState.isSyncing,
                syncStatusMessage = uiState.syncStatusMessage,
                savedTracksCount = uiState.savedTracks.size,
                collectionsCount = uiState.playlists.size,
                historyCount = uiState.history.size,
                isCheckingUpdate = uiState.isCheckingUpdate,
                onCheckUpdate = {
                    viewModel.checkAppUpdate(isManual = true)
                },
                onSignIn = { email, pass ->
                    viewModel.signIn(email, pass)
                },
                onSignUp = { email, pass ->
                    viewModel.signUp(email, pass)
                },
                onSignOut = {
                    viewModel.signOut()
                },
                onSyncNow = {
                    viewModel.syncNow()
                },
                onForgotPassword = { email ->
                    viewModel.forgotPassword(email)
                },
                onUpdateAvatar = { bitmap ->
                    viewModel.updateAvatar(bitmap)
                },
                onDismissRequest = { showAccountSheet = false }
            )
        }

        // 4. Cellular Download Warning Dialog
        val hasCellularWarning = uiState.pendingCellularDownloadTrack != null || uiState.pendingCellularDownloadTracks.isNotEmpty()
        if (hasCellularWarning) {
            val title = uiState.pendingCellularDownloadTrack?.title
                ?: "${uiState.pendingCellularDownloadTracks.size} bài hát"
            CellularDownloadDialog(
                trackTitle = title,
                onConfirm = {
                    viewModel.proceedCellularDownload()
                },
                onDismiss = {
                    viewModel.cancelCellularDownload()
                }
            )
        }

        // 5. In-App Auto Update Dialog
        uiState.updateInfo?.let { updateInfo ->
            com.kahoii.music.ui.components.UpdateDialog(
                updateInfo = updateInfo,
                downloadState = uiState.updateDownloadState,
                onStartDownload = {
                    viewModel.startDownloadUpdate(context)
                },
                onInstall = { apkFile ->
                    viewModel.installDownloadedUpdate(context, apkFile)
                },
                onDismiss = {
                    viewModel.dismissUpdateDialog()
                }
            )
        }
    }
}
