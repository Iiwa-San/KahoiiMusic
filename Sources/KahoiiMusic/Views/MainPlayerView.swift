import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct MainPlayerView: View {
    @StateObject private var player = AudioPlayerService.shared
    @StateObject private var historyService = HistoryService.shared
    @StateObject private var savedService = SavedTracksService.shared
    @StateObject private var sleepTimer = SleepTimerService.shared

    @State private var searchInput: String = ""
    @State private var isSearching: Bool = false
    @State private var searchResults: [PlaylistItem] = []

    @State private var showingPlaylistSheet: Bool = false
    @State private var showingHistorySheet: Bool = false
    @State private var showingFavoritesSheet: Bool = false
    @State private var showingSavedTracksSheet: Bool = false
    @State private var showingSearchResultsSheet: Bool = false

    public init() {}

    public var body: some View {
        ZStack {
            // Dynamic animated gradient backdrop
            DynamicGradientView(palette: player.currentPalette)

            // Main Content Layout
            VStack(spacing: 0) {
                // Top Header & Search Bar
                headerSection
                    .padding(.top, 8)
                    .padding(.horizontal, 20)

                Spacer(minLength: 10)

                // Album Artwork
                artworkSection
                    .padding(.horizontal, 32)

                Spacer(minLength: 16)

                // Track Info & Favorite
                trackInfoSection
                    .padding(.horizontal, 24)

                Spacer(minLength: 12)

                // Progress Bar (Scrubber)
                MusicTrackBarView(
                    currentTime: player.currentTime,
                    duration: player.duration,
                    accentColor: player.currentPalette.accentColor
                ) { targetTime in
                    player.seek(to: targetTime)
                }
                .padding(.horizontal, 24)

                Spacer(minLength: 12)

                // Playback Control Buttons (Shuffle, Prev, Play/Pause, Next, Loop)
                playbackControlsSection
                    .padding(.horizontal, 24)

                Spacer(minLength: 14)

                // Volume Slider
                volumeSliderSection
                    .padding(.horizontal, 28)

                Spacer(minLength: 16)

                // Bottom Navigation Bar (Queue, History, Favorites, Share)
                bottomActionsSection
                    .padding(.horizontal, 24)
                    .padding(.bottom, 12)
            }

            // Error Overlay Banner
            if let error = player.errorMessage {
                VStack {
                    Spacer()
                    HStack(spacing: 10) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.yellow)
                        Text(error)
                            .font(.system(size: 13, weight: .medium))
                            .foregroundColor(.white)
                            .lineLimit(2)
                        Spacer()
                        Button("Đóng") {
                            player.errorMessage = nil
                        }
                        .font(.system(size: 13, weight: .bold))
                        .foregroundColor(.white.opacity(0.8))
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 12)
                    .background(Color.black.opacity(0.85))
                    .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .shadow(radius: 10)
                    .padding(.horizontal, 20)
                    .padding(.bottom, 80)
                }
                .transition(.move(edge: .bottom).combined(with: .opacity))
                .animation(.easeInOut, value: player.errorMessage)
            }
        }
        // Sheets
        .sheet(isPresented: $showingPlaylistSheet) {
            PlaylistQueueSheet(player: player)
        }
        .sheet(isPresented: $showingHistorySheet) {
            HistorySheet(player: player)
        }
        .sheet(isPresented: $showingFavoritesSheet) {
            FavoritesSheet(player: player)
        }
        .sheet(isPresented: $showingSavedTracksSheet) {
            SavedTracksSheet(player: player)
        }
        .sheet(isPresented: $showingSearchResultsSheet) {
            SearchResultsSheet(
                results: searchResults,
                query: searchInput,
                isLoading: isSearching,
                player: player
            )
        }
    }

    // MARK: - Header & Search

    private var headerSection: some View {
        VStack(spacing: 12) {
            // App Title & Status
            HStack {
                HStack(spacing: 8) {
                    Image(systemName: "waveform.circle.fill")
                        .font(.system(size: 22))
                        .foregroundColor(player.currentPalette.accentColor)

                    Text("Kahoii Music")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundColor(.white)
                }

                Spacer()

                // Status Indicator
                Text(player.statusMessage)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundColor(.white.opacity(0.6))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 4)
                    .background(Color.white.opacity(0.08))
                    .clipShape(Capsule())
            }

            // Search Bar
            SearchPillBar(
                text: $searchInput,
                placeholder: "Tìm kiếm bài hát, dán link YouTube...",
                onSubmit: performSearch,
                onPaste: {
                    if !searchInput.isEmpty {
                        performSearch()
                    }
                }
            )
        }
    }

    // MARK: - Artwork

    private var artworkSection: some View {
        ArtworkView(
            artworkUrl: player.currentTrack?.artworkURL,
            isPlaying: player.playbackState.isPlaying,
            accentColor: player.currentPalette.accentColor
        )
        .frame(maxWidth: 340, maxHeight: 340)
    }

    // MARK: - Track Info & Favorite

    private var trackInfoSection: some View {
        HStack(alignment: .center, spacing: 14) {
            VStack(alignment: .leading, spacing: 4) {
                if let track = player.currentTrack {
                    MarqueeText(
                        text: track.title,
                        font: .system(size: 19, weight: .bold, design: .rounded),
                        color: .white
                    )

                    Text(track.author.isEmpty ? "YouTube" : track.author)
                        .font(.system(size: 14, weight: .medium))
                        .foregroundColor(.white.opacity(0.6))
                        .lineLimit(1)
                } else {
                    Text("Chưa chọn bài hát")
                        .font(.system(size: 18, weight: .semibold, design: .rounded))
                        .foregroundColor(.white.opacity(0.8))

                    Text("Dán link hoặc tìm bài hát ở trên")
                        .font(.system(size: 13))
                        .foregroundColor(.white.opacity(0.4))
                }
            }

            Spacer()

            if let track = player.currentTrack {
                HStack(spacing: 2) {
                    // Download Button (Offline)
                    Button(action: {
                        #if canImport(UIKit)
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        #endif
                        Task {
                            if savedService.isSaved(id: track.id) {
                                savedService.deleteSavedTrack(id: track.id)
                            } else {
                                await savedService.saveTrackAsync(track)
                            }
                        }
                    }) {
                        if savedService.isDownloading(id: track.id) {
                            ProgressView()
                                .tint(.green)
                                .scaleEffect(0.8)
                                .frame(width: 40, height: 40)
                        } else {
                            Image(systemName: savedService.isSaved(id: track.id) ? "arrow.down.circle.fill" : "arrow.down.circle")
                                .font(.system(size: 21))
                                .foregroundColor(savedService.isSaved(id: track.id) ? .green : .white.opacity(0.5))
                                .frame(width: 40, height: 40)
                        }
                    }

                    // Favorite Button
                    Button(action: {
                        #if canImport(UIKit)
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        #endif
                        historyService.toggleFavorite(track)
                    }) {
                        Image(systemName: historyService.isFavorite(id: track.id) ? "heart.fill" : "heart")
                            .font(.system(size: 22))
                            .foregroundColor(historyService.isFavorite(id: track.id) ? .pink : .white.opacity(0.5))
                            .frame(width: 40, height: 40)
                    }
                }
            }
        }
    }

    // MARK: - Playback Controls

    private var playbackControlsSection: some View {
        HStack(spacing: 0) {
            // Shuffle Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.toggleShuffle()
            }) {
                Image(systemName: "shuffle")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(player.isShuffled ? player.currentPalette.accentColor : .white.opacity(0.5))
                    .frame(maxWidth: .infinity)
            }

            // Previous Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.previousTrack()
            }) {
                Image(systemName: "backward.fill")
                    .font(.system(size: 26))
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
            }

            // Play / Pause Button (Large Hero Button)
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                #endif
                player.togglePlayPause()
            }) {
                ZStack {
                    Circle()
                        .fill(
                            LinearGradient(
                                colors: [player.currentPalette.accentColor, player.currentPalette.accentColor.opacity(0.8)],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 68, height: 68)
                        .shadow(color: player.currentPalette.accentColor.opacity(0.4), radius: 14, x: 0, y: 6)

                    if player.playbackState.isLoading {
                        ProgressView()
                            .tint(.white)
                            .scaleEffect(1.2)
                    } else {
                        Image(systemName: player.playbackState.isPlaying ? "pause.fill" : "play.fill")
                            .font(.system(size: 28))
                            .foregroundColor(.white)
                            .offset(x: player.playbackState.isPlaying ? 0 : 2)
                    }
                }
                .frame(maxWidth: .infinity)
            }

            // Next Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.nextTrack()
            }) {
                Image(systemName: "forward.fill")
                    .font(.system(size: 26))
                    .foregroundColor(.white)
                    .frame(maxWidth: .infinity)
            }

            // Loop / Repeat Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.cycleLoopMode()
            }) {
                Image(systemName: player.loopMode.iconName)
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(player.loopMode != .off ? player.currentPalette.accentColor : Color(red: 220/255, green: 220/255, blue: 220/255))
                    .frame(maxWidth: .infinity)
            }

            // Sleep Timer Button
            Menu {
                Button("15 phút") { sleepTimer.startTimer(minutes: 15) }
                Button("30 phút") { sleepTimer.startTimer(minutes: 30) }
                Button("45 phút") { sleepTimer.startTimer(minutes: 45) }
                Button("60 phút") { sleepTimer.startTimer(minutes: 60) }
                Button("Hết bài hát hiện tại") { sleepTimer.startEndOfTrack() }
                if sleepTimer.isActive {
                    Button(role: .destructive, action: { sleepTimer.cancelTimer() }) {
                        Label("Tắt hẹn giờ", systemImage: "xmark.circle")
                    }
                }
            } label: {
                VStack(spacing: 2) {
                    Image(systemName: sleepTimer.isActive ? "timer.circle.fill" : "timer")
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundColor(sleepTimer.isActive ? player.currentPalette.accentColor : Color(red: 220/255, green: 220/255, blue: 220/255))

                    if sleepTimer.isActive {
                        Text(sleepTimer.formattedRemainingTime)
                            .font(.system(size: 9, weight: .bold))
                            .foregroundColor(player.currentPalette.accentColor)
                    }
                }
                .frame(maxWidth: .infinity)
            }
        }
    }

    // MARK: - Volume Slider

    private var volumeSliderSection: some View {
        HStack(spacing: 12) {
            Image(systemName: "speaker.fill")
                .font(.system(size: 11))
                .foregroundColor(.white.opacity(0.4))

            Slider(
                value: Binding(
                    get: { Double(player.volume) },
                    set: { player.setVolume(Float($0)) }
                ),
                in: 0.0...1.0
            )
            .tint(.white.opacity(0.8))

            Image(systemName: "speaker.wave.3.fill")
                .font(.system(size: 11))
                .foregroundColor(.white.opacity(0.4))
        }
    }

    // MARK: - Bottom Actions

    private var bottomActionsSection: some View {
        HStack {
            // Queue / Playlist Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                showingPlaylistSheet = true
            }) {
                VStack(spacing: 4) {
                    ZStack(alignment: .topTrailing) {
                        Image(systemName: "music.note.list")
                            .font(.system(size: 20))

                        if !player.playlist.isEmpty {
                            Text("\(player.playlist.count)")
                                .font(.system(size: 9, weight: .bold))
                                .foregroundColor(.white)
                                .padding(3)
                                .background(Color.purple)
                                .clipShape(Circle())
                                .offset(x: 10, y: -6)
                        }
                    }
                    Text("Danh sách")
                        .font(.system(size: 11, weight: .medium))
                }
                .foregroundColor(.white.opacity(0.75))
                .frame(maxWidth: .infinity)
            }

            // Saved / Downloads Button (Đã tải)
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                showingSavedTracksSheet = true
            }) {
                VStack(spacing: 4) {
                    ZStack(alignment: .topTrailing) {
                        Image(systemName: "arrow.down.circle.fill")
                            .font(.system(size: 20))

                        if !savedService.savedTracks.isEmpty {
                            Text("\(savedService.savedTracks.count)")
                                .font(.system(size: 9, weight: .bold))
                                .foregroundColor(.white)
                                .padding(3)
                                .background(Color.green)
                                .clipShape(Circle())
                                .offset(x: 10, y: -6)
                        }
                    }
                    Text("Đã tải")
                        .font(.system(size: 11, weight: .medium))
                }
                .foregroundColor(.white.opacity(0.75))
                .frame(maxWidth: .infinity)
            }

            // History Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                showingHistorySheet = true
            }) {
                VStack(spacing: 4) {
                    Image(systemName: "clock.arrow.circlepath")
                        .font(.system(size: 20))
                    Text("Lịch sử")
                        .font(.system(size: 11, weight: .medium))
                }
                .foregroundColor(.white.opacity(0.75))
                .frame(maxWidth: .infinity)
            }

            // Favorites Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                showingFavoritesSheet = true
            }) {
                VStack(spacing: 4) {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 20))
                        .foregroundColor(historyService.favorites.isEmpty ? .white.opacity(0.75) : .pink.opacity(0.85))
                    Text("Yêu thích")
                        .font(.system(size: 11, weight: .medium))
                }
                .foregroundColor(.white.opacity(0.75))
                .frame(maxWidth: .infinity)
            }

            // Share Button
            if let track = player.currentTrack {
                Button(action: {
                    shareTrack(track)
                }) {
                    VStack(spacing: 4) {
                        Image(systemName: "square.and.arrow.up")
                            .font(.system(size: 20))
                        Text("Chia sẻ")
                            .font(.system(size: 11, weight: .medium))
                    }
                    .foregroundColor(.white.opacity(0.75))
                    .frame(maxWidth: .infinity)
                }
            }
        }
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(Color.white.opacity(0.06))
        )
    }

    // MARK: - Actions

    private func performSearch() {
        let trimmed = searchInput.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        // If direct URL or ID -> play immediately
        if YouTubeResolver.shared.isUrl(trimmed) || YouTubeResolver.shared.extractVideoId(from: trimmed) != nil || YouTubeResolver.shared.extractPlaylistId(from: trimmed) != nil {
            player.loadAndPlay(input: trimmed)
            searchInput = ""
            return
        }

        // Otherwise open search results sheet and search
        isSearching = true
        showingSearchResultsSheet = true
        Task {
            do {
                let results = try await YouTubeResolver.shared.search(query: trimmed)
                self.searchResults = results
                self.isSearching = false
            } catch {
                self.searchResults = []
                self.isSearching = false
            }
        }
    }

    private func shareTrack(_ track: PlaylistItem) {
        #if canImport(UIKit)
        guard let url = URL(string: track.youtubeUrl) else { return }
        let activityVC = UIActivityViewController(activityItems: [track.title, url], applicationActivities: nil)
        if let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
           let rootVC = windowScene.windows.first?.rootViewController {
            rootVC.present(activityVC, animated: true)
        }
        #endif
    }
}

