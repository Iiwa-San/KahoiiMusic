import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct PlaylistQueueSheet: View {
    @ObservedObject var player: AudioPlayerService
    @Environment(\.dismiss) private var dismiss

    @State private var showingLoadPlaylistDialog: Bool = false
    @State private var playlistInputUrl: String = ""

    public init(player: AudioPlayerService) {
        self.player = player
    }

    public var body: some View {
        NavigationView {
            ZStack {
                Color(white: 0.08).ignoresSafeArea()

                VStack(spacing: 0) {
                    // Header Stats
                    HStack {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Danh sách chờ")
                                .font(.system(size: 20, weight: .bold))
                                .foregroundColor(.white)

                            Text("\(player.playlist.count) bài hát • \(formattedTotalDuration())")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.6))
                        }

                        Spacer()

                        // Load Playlist Button
                        Button(action: {
                            showingLoadPlaylistDialog = true
                        }) {
                            HStack(spacing: 6) {
                                Image(systemName: "plus.circle.fill")
                                Text("Nạp URL")
                            }
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 12)
                            .padding(.vertical, 7)
                            .background(Color.purple.opacity(0.8))
                            .clipShape(Capsule())
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 14)

                    Divider().background(Color.white.opacity(0.1))

                    if player.playlist.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Image(systemName: "music.note.list")
                                .font(.system(size: 56))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Danh sách chờ trống")
                                .font(.system(size: 17, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Tìm kiếm bài hát hoặc dán link YouTube để bắt đầu nghe nhạc.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 40)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(Array(player.playlist.enumerated()), id: \.element.id) { index, item in
                                playlistRow(index: index, item: item)
                                    .listRowBackground(
                                        index == player.currentIndex ? Color.white.opacity(0.08) : Color.clear
                                    )
                                    .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                            .onDelete(perform: deleteItems)
                            .onMove(perform: moveItems)
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    if !player.playlist.isEmpty {
                        Button("Xóa tất cả") {
                            player.clearQueue()
                        }
                        .foregroundColor(.red.opacity(0.8))
                    }
                }

                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Xong") {
                        dismiss()
                    }
                    .foregroundColor(.white)
                    .fontWeight(.semibold)
                }
            }
            .alert("Nạp danh sách phát YouTube", isPresented: $showingLoadPlaylistDialog) {
                TextField("Dán link YouTube playlist...", text: $playlistInputUrl)
                Button("Nạp", action: {
                    if !playlistInputUrl.isEmpty {
                        player.loadAndPlay(input: playlistInputUrl)
                        playlistInputUrl = ""
                    }
                })
                Button("Hủy", role: .cancel) {
                    playlistInputUrl = ""
                }
            } message: {
                Text("Nhập URL hoặc ID danh sách phát từ YouTube:")
            }
        }
    }

    @ViewBuilder
    private func playlistRow(index: Int, item: PlaylistItem) -> some View {
        let isCurrent = (index == player.currentIndex)

        Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            player.currentIndex = index
            player.playTrack(item)
        }) {
            HStack(spacing: 12) {
                // Index or Equalizer
                if isCurrent {
                    EqualizerAnimationView(isPlaying: player.playbackState.isPlaying, color: .purple)
                        .frame(width: 24)
                } else {
                    Text("\(index + 1)")
                        .font(.system(size: 13, weight: .medium, design: .monospaced))
                        .foregroundColor(.white.opacity(0.4))
                        .frame(width: 24)
                }

                // Thumbnail
                if let url = item.artworkURL {
                    AsyncImage(url: url) { img in
                        img.resizable().aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Color.white.opacity(0.1)
                    }
                    .frame(width: 48, height: 48)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                // Details
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 14, weight: isCurrent ? .bold : .medium))
                        .foregroundColor(isCurrent ? .purple : .white)
                        .lineLimit(1)

                    Text(item.author.isEmpty ? "YouTube" : item.author)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.55))
                        .lineLimit(1)
                }

                Spacer()

                Text(item.formattedDuration)
                    .font(.system(size: 12, weight: .regular, design: .monospaced))
                    .foregroundColor(.white.opacity(0.5))
            }
            .padding(.vertical, 4)
        }
        .buttonStyle(PlainButtonStyle())
        .contextMenu {
            Button(action: {
                player.currentIndex = index
                player.playTrack(item)
            }) {
                Label("Phát ngay", systemImage: "play.fill")
            }

            if SavedTracksService.shared.isSaved(id: item.id) {
                Label("✓ Đã lưu Offline", systemImage: "checkmark.circle.fill")
            } else {
                Button(action: {
                    Task { await SavedTracksService.shared.saveTrackAsync(item) }
                }) {
                    Label("Tải về nghe Offline", systemImage: "arrow.down.circle")
                }
            }

            Button(role: .destructive, action: {
                if index < player.playlist.count {
                    player.playlist.remove(at: index)
                }
            }) {
                Label("Xóa khỏi danh sách", systemImage: "trash")
            }
        }
    }

    private func deleteItems(at offsets: IndexSet) {
        for index in offsets {
            player.removeFromQueue(at: index)
        }
    }

    private func moveItems(from source: IndexSet, to destination: Int) {
        player.playlist.move(fromOffsets: source, toOffset: destination)
    }

    private func formattedTotalDuration() -> String {
        let total = player.playlist.reduce(0) { $0 + $1.durationSeconds }
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        if hours > 0 {
            return "\(hours) giờ \(minutes) phút"
        } else {
            return "\(minutes) phút"
        }
    }
}

