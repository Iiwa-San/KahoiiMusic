package com.kahoii.music.ui.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.QueueMusic
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.R
import com.kahoii.music.ui.components.ControlButtons
import com.kahoii.music.ui.components.MarqueeText
import com.kahoii.music.ui.components.MusicTrackBar
import com.kahoii.music.ui.components.SearchPillBox
import com.kahoii.music.ui.components.VinylArtwork
import com.kahoii.music.ui.components.VolumeDialog
import com.kahoii.music.ui.theme.CardBackground
import com.kahoii.music.ui.theme.CardBorder
import com.kahoii.music.ui.theme.GradientBottom
import com.kahoii.music.ui.theme.GradientTop
import com.kahoii.music.ui.theme.NeonPink
import com.kahoii.music.ui.theme.TextMuted
import com.kahoii.music.ui.theme.TextWhite
import com.kahoii.music.ui.theme.TopBarBg
import com.kahoii.music.viewmodel.MainViewModel

@Composable
fun MusicPlayerScreen(
    viewModel: MainViewModel,
    modifier: Modifier = Modifier
) {
    val uiState by viewModel.uiState.collectAsState()
    var showPlaylistSheet by remember { mutableStateOf(false) }
    var showVolumeDialog by remember { mutableStateOf(false) }

    // Outer continuous gradient background (matching C# Form1 GradientPanel)
    Box(
        modifier = modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(GradientTop, GradientBottom)
                )
            )
            .statusBarsPadding()
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.SpaceBetween,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // ================= 1. TOP BAR =================
            Column(modifier = Modifier.fillMaxWidth()) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Logo and Brand Name
                    Row(
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Image(
                            painter = painterResource(id = R.drawable.logo_icon),
                            contentDescription = "Logo",
                            modifier = Modifier
                                .size(28.dp)
                                .clip(CircleShape)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = "Kahoii Music",
                            color = NeonPink,
                            fontSize = 18.sp,
                            fontWeight = FontWeight.Bold,
                            style = MaterialTheme.typography.titleMedium
                        )
                    }

                    // Playlist / Menu Button with Badge
                    IconButton(
                        onClick = { showPlaylistSheet = true },
                        modifier = Modifier.size(40.dp)
                    ) {
                        BadgedBox(
                            badge = {
                                if (uiState.playlist.isNotEmpty()) {
                                    Badge(
                                        containerColor = NeonPink,
                                        contentColor = Color.White
                                    ) {
                                        Text("${uiState.playlist.size}")
                                    }
                                }
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Default.QueueMusic,
                                contentDescription = "Playlist Menu",
                                tint = if (uiState.playlist.isNotEmpty()) NeonPink else TextWhite,
                                modifier = Modifier.size(26.dp)
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(6.dp))

                // Search Pill Box
                SearchPillBox(
                    onSearchSubmitted = { query ->
                        viewModel.loadAndPlay(query)
                    }
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ================= 2. PLAYER CARD =================
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f)
                    .shadow(elevation = 16.dp, shape = RoundedCornerShape(24.dp), spotColor = Color.Black),
                shape = RoundedCornerShape(24.dp),
                colors = CardDefaults.cardColors(containerColor = CardBackground),
                border = androidx.compose.foundation.BorderStroke(1.dp, CardBorder)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(horizontal = 16.dp, vertical = 12.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.SpaceEvenly
                ) {
                    // Vinyl Record Artwork with live smooth rotation
                    VinylArtwork(
                        thumbnailUrl = uiState.currentTrack?.thumbnailUrl,
                        isPlaying = uiState.isPlaying,
                        size = 220.dp
                    )

                    // Track Title & Info
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 8.dp)
                    ) {
                        val currentTitle = uiState.currentTrack?.title?.ifBlank { "Kahoii Music" } ?: "Kahoii Music"
                        MarqueeText(
                            text = currentTitle,
                            style = MaterialTheme.typography.titleLarge,
                            color = TextWhite,
                            modifier = Modifier.fillMaxWidth()
                        )

                        Spacer(modifier = Modifier.height(4.dp))

                        // Artist or Status Message
                        val subtitle = when {
                            uiState.currentTrack?.artist?.isNotBlank() == true -> uiState.currentTrack!!.artist
                            uiState.statusMessage.isNotBlank() -> uiState.statusMessage
                            else -> "Sẵn sàng phát nhạc"
                        }

                        Text(
                            text = subtitle,
                            color = NeonPink.copy(alpha = 0.9f),
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium,
                            maxLines = 1
                        )
                    }

                    // Music Track Bar (Progress & Duration)
                    MusicTrackBar(
                        currentPositionMs = uiState.currentPositionMs,
                        durationMs = uiState.durationMs,
                        onSeekTo = { progress ->
                            viewModel.seekTo(progress)
                        }
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ================= 3. BOTTOM CONTROLS =================
            ControlButtons(
                isPlaying = uiState.isPlaying,
                loopMode = uiState.loopMode,
                volume = uiState.volume,
                onTogglePlayPause = { viewModel.togglePlayPause() },
                onPrevious = { viewModel.playPrevious() },
                onNext = { viewModel.playNext() },
                onToggleLoop = { viewModel.toggleLoopMode() },
                onVolumeClick = { showVolumeDialog = true }
            )

            Spacer(modifier = Modifier.height(8.dp))
        }

        // Playlist / History Bottom Sheet
        if (showPlaylistSheet) {
            PlaylistBottomSheet(
                playlist = uiState.playlist,
                history = uiState.history,
                currentTrackId = uiState.currentTrack?.id,
                isPlaylistLoading = uiState.isPlaylistLoading,
                onPlaylistTrackSelected = { track, index ->
                    viewModel.playTrackAtIndex(index)
                },
                onHistoryTrackSelected = { track ->
                    viewModel.playTrack(track)
                },
                onAddToPlaylist = { track ->
                    viewModel.addToPlaylist(track)
                },
                onRemoveFromPlaylist = { index ->
                    viewModel.removeFromPlaylist(index)
                },
                onDeleteHistoryItem = { id ->
                    viewModel.removeHistoryItem(id)
                },
                onClearHistory = {
                    viewModel.clearHistory()
                },
                onDismissRequest = { showPlaylistSheet = false }
            )
        }

        // Volume Dialog
        if (showVolumeDialog) {
            VolumeDialog(
                volume = uiState.volume,
                onVolumeChanged = { viewModel.setVolume(it) },
                onDismissRequest = { showVolumeDialog = false }
            )
        }
    }
}

