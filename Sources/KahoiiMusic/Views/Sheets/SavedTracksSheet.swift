import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct SavedTracksSheet: View {
    @ObservedObject var player: AudioPlayerService
    @ObservedObject var savedService = SavedTracksService.shared
    @Environment(\.dismiss) private var dismiss

    public init(player: AudioPlayerService) {
        self.player = player
    }

    public var body: some View {
        NavigationView {
            ZStack {
                Color(white: 0.08).ignoresSafeArea()

                VStack(spacing: 0) {
                    // Header Actions
                    HStack {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Nhạc đã tải (Offline)")
                                .font(.system(size: 20, weight: .bold))
                                .foregroundColor(.white)

                            Text("\(savedService.savedTracks.count) bài hát • \(formattedTotalDuration())")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.6))
                        }

                        Spacer()

                        if !savedService.savedTracks.isEmpty {
                            Button(action: {
                                #if canImport(UIKit)
                                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                                #endif
                                if let first = savedService.savedTracks.first {
                                    player.playTrack(first, keepQueue: true)
                                }
                            }) {
                                HStack(spacing: 6) {
                                    Image(systemName: "play.circle.fill")
                                    Text("Phát tất cả")
                                }
                                .font(.system(size: 13, weight: .semibold))
                                .foregroundColor(.white)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 7)
                                .background(Color.purple.opacity(0.8))
                                .clipShape(Capsule())
                            }
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 14)

                    Divider().background(Color.white.opacity(0.1))

                    if savedService.savedTracks.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Image(systemName: "arrow.down.circle")
                                .font(.system(size: 56))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Chưa có bài hát nào được tải")
                                .font(.system(size: 17, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Bạn có thể tải bài hát từ Danh sách phát, Lịch sử hoặc Kết quả tìm kiếm để nghe offline không cần mạng.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 40)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(savedService.savedTracks) { item in
                                savedTrackRow(item: item)
                                    .listRowBackground(
                                        player.currentTrack?.id == item.id ? Color.white.opacity(0.08) : Color.clear
                                    )
                                    .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                            .onDelete { offsets in
                                for index in offsets {
                                    let item = savedService.savedTracks[index]
                                    savedService.deleteSavedTrack(id: item.id)
                                }
                            }
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Xong") {
                        dismiss()
                    }
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundColor(.white)
                }
            }
        }
    }

    @ViewBuilder
    private func savedTrackRow(item: PlaylistItem) -> some View {
        let isCurrent = (player.currentTrack?.id == item.id)

        HStack(spacing: 12) {
            // Track Info tap target
            HStack(spacing: 12) {
                // Thumbnail with play/offline indicator
                ZStack {
                    AsyncImage(url: item.artworkURL) { phase in
                        if let image = phase.image {
                            image.resizable().aspectRatio(contentMode: .fill)
                        } else {
                            Color.purple.opacity(0.3)
                        }
                    }
                    .frame(width: 48, height: 48)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))

                    if isCurrent && player.playbackState.isPlaying {
                        Color.black.opacity(0.4)
                            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                        EqualizerAnimationView(isPlaying: player.playbackState.isPlaying, color: .white)
                            .frame(width: 20, height: 20)
                    }
                }

                // Title and Author
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 15, weight: isCurrent ? .bold : .medium))
                        .foregroundColor(isCurrent ? player.currentPalette.accentColor : .white)
                        .lineLimit(1)

                    HStack(spacing: 6) {
                        Image(systemName: "arrow.down.circle.fill")
                            .font(.system(size: 10))
                            .foregroundColor(.green)

                        Text("Offline")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.green)

                        Text("•")
                            .font(.system(size: 11))
                            .foregroundColor(.white.opacity(0.3))

                        Text(item.author.isEmpty ? "YouTube" : item.author)
                            .font(.system(size: 12))
                            .foregroundColor(.white.opacity(0.5))
                            .lineLimit(1)
                    }
                }

                Spacer()

                // Duration
                Text(item.formattedDuration)
                    .font(.system(size: 12, weight: .medium, design: .monospaced))
                    .foregroundColor(.white.opacity(0.5))

            }
            .contentShape(Rectangle())
            .onTapGesture {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.playTrack(item, keepQueue: true)
            }

            // Context Menu
            Menu {
                Button(action: {
                    player.playTrack(item, keepQueue: true)
                }) {
                    Label("Phát ngay", systemImage: "play.fill")
                }

                Button(action: {
                    player.addToQueue(item)
                }) {
                    Label("Thêm vào danh sách chờ", systemImage: "text.badge.plus")
                }

                Button(role: .destructive, action: {
                    savedService.deleteSavedTrack(id: item.id)
                }) {
                    Label("Xóa khỏi mục Đã tải", systemImage: "trash")
                }
            } label: {
                Image(systemName: "ellipsis")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundColor(.white.opacity(0.5))
                    .frame(width: 32, height: 32)
            }
        }
        .padding(.vertical, 4)
    }

    private func formattedTotalDuration() -> String {
        let total = savedService.savedTracks.reduce(0) { $0 + $1.durationSeconds }
        let h = total / 3600
        let m = (total % 3600) / 60
        if h > 0 {
            return "\(h) giờ \(m) phút"
        } else {
            return "\(m) phút"
        }
    }
}
