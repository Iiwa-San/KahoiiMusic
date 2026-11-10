package com.kahoii.music.ui.screens

import android.content.Intent
import android.content.res.Configuration
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.GraphicEq
import androidx.compose.material.icons.outlined.AccountCircle
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
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.kahoii.music.BuildConfig
import com.kahoii.music.R
import com.kahoii.music.model.ActiveQueueSource
import com.kahoii.music.model.PlaybackStatus
import com.kahoii.music.model.PlaylistItem
import com.kahoii.music.ui.components.AccountBottomSheet
import com.kahoii.music.ui.components.ArtworkView
import com.kahoii.music.ui.components.BottomActionsSection
import com.kahoii.music.ui.components.CellularDownloadDialog
import com.kahoii.music.ui.components.DynamicGradientView
import com.kahoii.music.ui.components.LyricsView
import com.kahoii.music.ui.components.MarqueeText
import com.kahoii.music.ui.components.MusicTrackBarView
import com.kahoii.music.ui.components.PlaybackControlsView
import com.kahoii.music.ui.components.SearchPillBox
import com.kahoii.music.ui.components.SearchDropdownOverlay
import com.kahoii.music.ui.components.FullSearchResultsView
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

    val configuration = LocalConfiguration.current
    val isLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
    val screenWidthDp = configuration.screenWidthDp
    val isTablet = screenWidthDp >= 600

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
        if (!isLandscape) {
            // =========================================================================
            // PORTRAIT MODE (Phones & Tablets)
            // =========================================================================
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .then(
                            if (isTablet) Modifier.widthIn(max = 520.dp)
                            else Modifier
                        )
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
                        HeaderRow(
                            statusMessage = uiState.statusMessage,
                            isLoggedIn = uiState.isLoggedIn,
                            avatarBase64 = uiState.currentUser?.avatarBase64,
                            onOpenAccount = { showAccountSheet = true }
                        )

                        Box(modifier = Modifier.fillMaxWidth()) {
                            SearchPillBox(
                                onSearchSubmitted = { query ->
                                    viewModel.openFullSearchResults(query)
                                },
                                onQueryChanged = { query ->
                                    viewModel.onSearchQueryChanged(query)
                                },
                                onDismissSuggestions = {
                                    viewModel.clearSearchSuggestions()
                                }
                            )

                            if (uiState.searchSuggestions.isNotEmpty() && !uiState.isFullSearchActive) {
                                SearchDropdownOverlay(
                                    query = uiState.currentSearchQuery.ifBlank { "kết quả" },
                                    suggestions = uiState.searchSuggestions,
                                    onTrackPlay = { track ->
                                        viewModel.clearSearchSuggestions()
                                        viewModel.playTrack(track)
                                    },
                                    onViewAll = { q ->
                                        viewModel.openFullSearchResults(q)
                                    },
                                    modifier = Modifier.padding(top = 48.dp)
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(6.dp))

                    if (uiState.isFullSearchActive) {
                        FullSearchResultsView(
                            query = uiState.currentSearchQuery,
                            results = uiState.searchResults,
                            isSearching = uiState.isSearching,
                            onTrackPlay = { track -> viewModel.playTrack(track) },
                            onTrackAdd = { track -> viewModel.addTrackToCurrentQueue(track) },
                            onClose = { viewModel.closeFullSearchResults() },
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f)
                        )
                    } else {
                        // ================= 2. ALBUM ARTWORK OR LYRICS =================
                        Box(
                            modifier = Modifier
                                .fillMaxWidth(if (isTablet) 0.70f else 0.88f)
                                .widthIn(max = 400.dp)
                                .weight(1f, fill = false),
                            contentAlignment = Alignment.Center
                        ) {
                        if (uiState.isLyricsVisible) {
                            LyricsView(
                                lyricsData = uiState.lyricsData,
                                isLoading = uiState.isLyricsLoading,
                                currentPositionMs = uiState.currentPositionMs,
                                currentLyricIndex = uiState.currentLyricIndex,
                                romanizationMode = uiState.romanizationMode,
                                onCycleRomanizationMode = { viewModel.cycleRomanizationMode() },
                                accentColor = NeonPink,
                                onSeek = { positionMs -> viewModel.seekTo(positionMs) },
                                onClose = { viewModel.toggleLyricsVisibility() },
                                onRetry = {
                                    uiState.currentTrack?.let { cur ->
                                        viewModel.loadLyricsForTrack(cur.id, cur.safeTitle, cur.safeArtist, cur.durationSeconds, forceRefresh = true)
                                    }
                                },
                                isSquare = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                        } else {
                            ArtworkView(
                                thumbnailUrl = uiState.currentTrack?.thumbnailUrl,
                                isPlaying = uiState.isPlaying,
                                accentColor = NeonPink,
                                onSwipeLeft = { viewModel.playNext() },
                                onSwipeRight = { viewModel.playPrevious() },
                                onArtworkClick = { viewModel.toggleLyricsVisibility() },
                                modifier = Modifier.fillMaxWidth()
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                    // ================= 3. TRACK INFO & FAVORITE / DOWNLOAD =================
                    TrackInfoRow(
                        currentTrack = uiState.currentTrack,
                        activeQueueTitle = uiState.activeQueueTitle,
                        isLyricsVisible = uiState.isLyricsVisible,
                        isLyricsLoading = uiState.isLyricsLoading,
                        hasLyrics = uiState.hasLyrics,
                        isFavorite = uiState.isCurrentTrackFavorite,
                        onToggleLyrics = { viewModel.toggleLyricsVisibility() },
                        onToggleFavorite = { viewModel.toggleFavorite() }
                    )

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

                    // ================= 5. PLAYBACK CONTROLS =================
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

                    // ================= 7. BOTTOM NAVIGATION BAR =================
                    BottomActionsSection(
                        playlistCount = uiState.playlist.size,
                        savedCount = uiState.savedTracks.size,
                        favoritesCount = uiState.favorites.size,
                        historyCount = if (uiState.isLoggedIn) uiState.history.size else 0,
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

                    Spacer(modifier = Modifier.height(2.dp))

                    // ================= 8. FOOTER: COPYRIGHT & CLICKABLE VERSION =================
                    FooterRow(
                        onCheckUpdate = { viewModel.checkAppUpdate(isManual = true) }
                    )

                    Spacer(modifier = Modifier.height(2.dp))
                }
            }
        } else {
            // =========================================================================
            // LANDSCAPE MODE (Phones & Tablets Adaptive 2-Column Layout)
            // =========================================================================
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .clickable(
                        interactionSource = remember { MutableInteractionSource() },
                        indication = null
                    ) {
                        focusManager.clearFocus()
                        keyboardController?.hide()
                    }
                    .padding(horizontal = 16.dp, vertical = 6.dp),
                horizontalArrangement = Arrangement.spacedBy(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // ================= LEFT PANE (PLAYER DECK) =================
                Column(
                    modifier = Modifier
                        .weight(0.48f)
                        .fillMaxHeight(),
                    verticalArrangement = Arrangement.SpaceBetween,
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    // Header & Search
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        HeaderRow(
                            statusMessage = uiState.statusMessage,
                            isLoggedIn = uiState.isLoggedIn,
                            avatarBase64 = uiState.currentUser?.avatarBase64,
                            onOpenAccount = { showAccountSheet = true }
                        )

                        Box(modifier = Modifier.fillMaxWidth()) {
                            SearchPillBox(
                                onSearchSubmitted = { query ->
                                    viewModel.openFullSearchResults(query)
                                },
                                onQueryChanged = { query ->
                                    viewModel.onSearchQueryChanged(query)
                                },
                                onDismissSuggestions = {
                                    viewModel.clearSearchSuggestions()
                                }
                            )

                            if (uiState.searchSuggestions.isNotEmpty() && !uiState.isFullSearchActive) {
                                SearchDropdownOverlay(
                                    query = uiState.currentSearchQuery.ifBlank { "kết quả" },
                                    suggestions = uiState.searchSuggestions,
                                    onTrackPlay = { track ->
                                        viewModel.clearSearchSuggestions()
                                        viewModel.playTrack(track)
                                    },
                                    onViewAll = { q ->
                                        viewModel.openFullSearchResults(q)
                                    },
                                    modifier = Modifier.padding(top = 48.dp)
                                )
                            }
                        }
                    }

                    if (uiState.isFullSearchActive) {
                        FullSearchResultsView(
                            query = uiState.currentSearchQuery,
                            results = uiState.searchResults,
                            isSearching = uiState.isSearching,
                            onTrackPlay = { track -> viewModel.playTrack(track) },
                            onTrackAdd = { track -> viewModel.addTrackToCurrentQueue(track) },
                            onClose = { viewModel.closeFullSearchResults() },
                            modifier = Modifier
                                .weight(1f)
                                .fillMaxWidth()
                        )
                    } else {
                        // Artwork View (scales vertically without overflow)
                        Box(
                            modifier = Modifier
                                .weight(1f, fill = false)
                                .padding(vertical = 4.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            ArtworkView(
                                thumbnailUrl = uiState.currentTrack?.thumbnailUrl,
                                isPlaying = uiState.isPlaying,
                                accentColor = NeonPink,
                                matchHeightConstraintsFirst = true,
                                onSwipeLeft = { viewModel.playNext() },
                                onSwipeRight = { viewModel.playPrevious() },
                                onArtworkClick = { viewModel.toggleLyricsVisibility() }
                            )
                        }
                    }

                    // Track Info (Title marquee, Subtitle, Favorite, Lyrics button)
                    TrackInfoRow(
                        currentTrack = uiState.currentTrack,
                        activeQueueTitle = uiState.activeQueueTitle,
                        isLyricsVisible = uiState.isLyricsVisible,
                        isLyricsLoading = uiState.isLyricsLoading,
                        hasLyrics = uiState.hasLyrics,
                        isFavorite = uiState.isCurrentTrackFavorite,
                        onToggleLyrics = { viewModel.toggleLyricsVisibility() },
                        onToggleFavorite = { viewModel.toggleFavorite() }
                    )

                    // Track Bar
                    MusicTrackBarView(
                        currentPositionMs = uiState.currentPositionMs,
                        durationMs = uiState.durationMs,
                        accentColor = NeonPink,
                        onSeekCommitted = { progress ->
                            viewModel.seekTo(progress)
                        }
                    )

                    // Playback Controls
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
                }

                // ================= RIGHT PANE (LYRICS OR QUEUE / LIBRARY DECK) =================
                Column(
                    modifier = Modifier
                        .weight(0.52f)
                        .fillMaxHeight(),
                    verticalArrangement = Arrangement.SpaceBetween,
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    if (uiState.isLyricsVisible) {
                        // Full height real-time Lyrics View
                        LyricsView(
                            lyricsData = uiState.lyricsData,
                            isLoading = uiState.isLyricsLoading,
                            currentPositionMs = uiState.currentPositionMs,
                            currentLyricIndex = uiState.currentLyricIndex,
                            romanizationMode = uiState.romanizationMode,
                            onCycleRomanizationMode = { viewModel.cycleRomanizationMode() },
                            accentColor = NeonPink,
                            onSeek = { positionMs -> viewModel.seekTo(positionMs) },
                            onClose = { viewModel.toggleLyricsVisibility() },
                            onRetry = {
                                uiState.currentTrack?.let { cur ->
                                    viewModel.loadLyricsForTrack(cur.id, cur.safeTitle, cur.safeArtist, cur.durationSeconds, forceRefresh = true)
                                }
                            },
                            isSquare = false,
                            modifier = Modifier.fillMaxSize()
                        )
                    } else {
                        // When lyrics is closed: Volume Slider + Live Queue Preview + Bottom Actions + Footer
                        VolumeSliderSection(
                            volume = uiState.volume,
                            onVolumeChanged = { viewModel.setVolume(it) },
                            onToggleMute = { viewModel.toggleMute() }
                        )

                        Spacer(modifier = Modifier.height(4.dp))

                        // Interactive Up Next Queue Card
                        LandscapeQueueCard(
                            playlist = uiState.playlist,
                            currentTrackId = uiState.currentTrack?.id,
                            onTrackClick = { idx ->
                                viewModel.playFromQueueIndex(idx, ActiveQueueSource.PLAYLIST)
                            },
                            onRemoveTrack = { idx ->
                                viewModel.removeFromPlaylist(idx)
                            },
                            onViewAllClick = {
                                selectedSheetTab = 0
                                showPlaylistSheet = true
                            },
                            modifier = Modifier.weight(1f)
                        )

                        Spacer(modifier = Modifier.height(6.dp))

                        BottomActionsSection(
                            playlistCount = uiState.playlist.size,
                            savedCount = uiState.savedTracks.size,
                            favoritesCount = uiState.favorites.size,
                            historyCount = if (uiState.isLoggedIn) uiState.history.size else 0,
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

                        Spacer(modifier = Modifier.height(2.dp))

                        FooterRow(
                            onCheckUpdate = { viewModel.checkAppUpdate(isManual = true) }
                        )
                    }
                }
            }
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
                isLoggedIn = uiState.isLoggedIn,
                onOpenAccount = {
                    showPlaylistSheet = false
                    showAccountSheet = true
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

// =========================================================================
// REUSABLE SUB-COMPONENTS
// =========================================================================

@Composable
private fun HeaderRow(
    statusMessage: String,
    isLoggedIn: Boolean,
    avatarBase64: String?,
    onOpenAccount: () -> Unit
) {
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

        // Status Indicator Capsule & User Icon
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = statusMessage,
                color = Color.White.copy(alpha = 0.65f),
                fontSize = 11.5.sp,
                fontWeight = FontWeight.Medium,
                modifier = Modifier
                    .clip(RoundedCornerShape(percent = 50))
                    .background(Color.White.copy(alpha = 0.08f))
                    .padding(horizontal = 10.dp, vertical = 4.dp)
            )

            val userAvatarBitmap = remember(avatarBase64) {
                com.kahoii.music.util.ImageUtils.base64ToBitmap(avatarBase64)?.asImageBitmap()
            }

            if (isLoggedIn) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .clickable { onOpenAccount() },
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
                    onClick = onOpenAccount,
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
}

@Composable
private fun TrackInfoRow(
    currentTrack: PlaylistItem?,
    activeQueueTitle: String,
    isLyricsVisible: Boolean,
    isLyricsLoading: Boolean,
    hasLyrics: Boolean,
    isFavorite: Boolean,
    onToggleLyrics: () -> Unit,
    onToggleFavorite: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Title and Artist
        Column(modifier = Modifier.weight(1f)) {
            val currentTitle = currentTrack?.title?.ifBlank { "Chưa chọn bài hát" } ?: "Kahoii Music"
            MarqueeText(
                text = currentTitle,
                style = MaterialTheme.typography.titleLarge,
                color = Color.White,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(2.dp))

            val subtitle = when {
                currentTrack != null -> {
                    val queueInfo = activeQueueTitle
                    val artist = currentTrack.displayArtist.ifBlank { "YouTube" }
                    val offlineTag = if (currentTrack.isOffline) " • Offline" else ""
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

        Spacer(modifier = Modifier.width(4.dp))

        // Lyrics Button
        IconButton(
            onClick = onToggleLyrics,
            modifier = Modifier
                .size(38.dp)
                .then(
                    if (isLyricsVisible) Modifier.background(NeonPink.copy(alpha = 0.22f), CircleShape)
                    else Modifier
                )
        ) {
            if (isLyricsLoading) {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    color = NeonPink,
                    strokeWidth = 2.dp
                )
            } else {
                Image(
                    painter = painterResource(id = R.drawable.ic_lyrics),
                    contentDescription = "Lời bài hát",
                    modifier = Modifier
                        .size(23.dp)
                        .alpha(
                            when {
                                isLyricsVisible -> 1.0f
                                hasLyrics -> 0.85f
                                else -> 0.40f
                            }
                        )
                )
            }
        }

        // Favorite Heart Button
        IconButton(
            onClick = onToggleFavorite,
            enabled = currentTrack != null,
            modifier = Modifier.size(38.dp)
        ) {
            Icon(
                imageVector = if (isFavorite) Icons.Default.Favorite else Icons.Default.FavoriteBorder,
                contentDescription = "Yêu thích",
                tint = when {
                    currentTrack == null -> Color.White.copy(alpha = 0.2f)
                    isFavorite -> Color(0xFFFF2D55)
                    else -> Color.White.copy(alpha = 0.5f)
                },
                modifier = Modifier.size(23.dp)
            )
        }
    }
}

@Composable
private fun FooterRow(
    onCheckUpdate: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 6.dp, vertical = 2.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Spacer(modifier = Modifier.width(44.dp))
        Text(
            text = "copyright by Kahoii-San",
            color = Color.White.copy(alpha = 0.35f),
            fontSize = 11.sp,
            fontWeight = FontWeight.Normal,
            textAlign = TextAlign.Center,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = "v${BuildConfig.VERSION_NAME}",
            color = NeonPink.copy(alpha = 0.8f),
            fontSize = 11.sp,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier
                .clip(RoundedCornerShape(4.dp))
                .clickable { onCheckUpdate() }
                .padding(horizontal = 4.dp, vertical = 2.dp)
        )
    }
}

@Composable
private fun LandscapeQueueCard(
    playlist: List<PlaylistItem>,
    currentTrackId: String?,
    onTrackClick: (Int) -> Unit,
    onRemoveTrack: (Int) -> Unit,
    onViewAllClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(Color(0xFF130E22).copy(alpha = 0.85f))
            .border(
                width = 1.dp,
                brush = Brush.verticalGradient(
                    listOf(
                        NeonPink.copy(alpha = 0.35f),
                        Color(0xFF8A2BE2).copy(alpha = 0.2f),
                        Color.White.copy(alpha = 0.05f)
                    )
                ),
                shape = RoundedCornerShape(16.dp)
            )
            .padding(10.dp)
    ) {
        if (playlist.isEmpty()) {
            Column(
                modifier = Modifier.fillMaxSize(),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                Icon(
                    imageVector = Icons.Default.GraphicEq,
                    contentDescription = null,
                    tint = Color.White.copy(alpha = 0.25f),
                    modifier = Modifier.size(36.dp)
                )
                Spacer(modifier = Modifier.height(6.dp))
                Text(
                    text = "Danh sách phát đang trống",
                    color = Color.White.copy(alpha = 0.55f),
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Medium
                )
                Text(
                    text = "Dán link YouTube ở trên để phát nhạc",
                    color = Color.White.copy(alpha = 0.35f),
                    fontSize = 11.5.sp
                )
            }
        } else {
            Column(modifier = Modifier.fillMaxSize()) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 6.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Danh sách chờ (${playlist.size})",
                        color = Color.White.copy(alpha = 0.9f),
                        fontSize = 13.5.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = "Xem tất cả",
                        color = NeonPink,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .clickable { onViewAllClick() }
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    )
                }

                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    itemsIndexed(playlist) { idx, track ->
                        val isCurrent = track.id == currentTrackId
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .background(if (isCurrent) NeonPink.copy(alpha = 0.18f) else Color.Transparent)
                                .clickable { onTrackClick(idx) }
                                .padding(horizontal = 6.dp, vertical = 4.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            AsyncImage(
                                model = ImageRequest.Builder(LocalContext.current)
                                    .data(track.thumbnailUrl)
                                    .crossfade(true)
                                    .build(),
                                contentDescription = track.safeTitle,
                                contentScale = ContentScale.Crop,
                                modifier = Modifier
                                    .size(32.dp)
                                    .clip(RoundedCornerShape(6.dp))
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = track.safeTitle,
                                    color = if (isCurrent) NeonPink else Color.White,
                                    fontSize = 12.sp,
                                    fontWeight = if (isCurrent) FontWeight.Bold else FontWeight.Medium,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                                Text(
                                    text = track.displayArtist.ifBlank { "YouTube" },
                                    color = Color.White.copy(alpha = 0.5f),
                                    fontSize = 10.5.sp,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                text = track.formattedDuration,
                                color = Color.White.copy(alpha = 0.4f),
                                fontSize = 11.sp
                            )
                            IconButton(
                                onClick = { onRemoveTrack(idx) },
                                modifier = Modifier.size(24.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Close,
                                    contentDescription = "Xoá",
                                    tint = Color.White.copy(alpha = 0.35f),
                                    modifier = Modifier.size(14.dp)
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
