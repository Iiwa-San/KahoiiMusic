import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct SearchResultsSheet: View {
    public let results: [PlaylistItem]
    public let query: String
    public let isLoading: Bool
    @ObservedObject var player: AudioPlayerService
    @Environment(\.dismiss) private var dismiss

    public init(
        results: [PlaylistItem],
        query: String,
        isLoading: Bool,
        player: AudioPlayerService
    ) {
        self.results = results
        self.query = query
        self.isLoading = isLoading
        self.player = player
    }

    public var body: some View {
        NavigationView {
            ZStack {
                Color(white: 0.08).ignoresSafeArea()

                VStack(spacing: 0) {
                    if isLoading {
                        VStack(spacing: 16) {
                            Spacer()
                            ProgressView()
                                .tint(.white)
                                .scaleEffect(1.3)
                            Text("Đang tìm kiếm '\(query)'...")
                                .font(.system(size: 15))
                                .foregroundColor(.white.opacity(0.6))
                            Spacer()
                        }
                    } else if results.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Image(systemName: "magnifyingglass")
                                .font(.system(size: 56))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Không tìm thấy kết quả")
                                .font(.system(size: 17, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Thử tìm kiếm với từ khóa khác hoặc dán trực tiếp liên kết YouTube.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 40)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(results) { item in
                                resultRow(item: item)
                                    .listRowBackground(Color.clear)
                                    .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }

                    if player.currentTrack != nil {
                        MiniPlayerView(player: player) {
                            dismiss()
                        }
                    }
                }
            }
            .navigationTitle("Kết quả tìm kiếm")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Đóng") {
                        dismiss()
                    }
                    .foregroundColor(.white)
                    .fontWeight(.semibold)
                }
            }
        }
    }

    @ViewBuilder
    private func resultRow(item: PlaylistItem) -> some View {
        HStack(spacing: 12) {
            // Track Info tap target
            HStack(spacing: 12) {
                // Thumbnail
                if let url = item.artworkURL {
                    AsyncImage(url: url) { img in
                        img.resizable().aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Color.white.opacity(0.1)
                    }
                    .frame(width: 52, height: 52)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                // Info
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundColor(.white)
                        .lineLimit(2)

                    HStack(spacing: 6) {
                        Text(item.author.isEmpty ? "YouTube" : item.author)
                            .font(.system(size: 12))
                            .foregroundColor(.white.opacity(0.6))
                            .lineLimit(1)

                        Text("•")
                            .font(.system(size: 10))
                            .foregroundColor(.white.opacity(0.3))

                        Text(item.formattedDuration)
                            .font(.system(size: 11, design: .monospaced))
                            .foregroundColor(.white.opacity(0.4))
                    }
                }

                Spacer()
            }
            .contentShape(Rectangle())
            .onTapGesture {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                #endif
                player.playTrack(item)
                player.playlist = results
                if let idx = results.firstIndex(where: { $0.id == item.id }) {
                    player.currentIndex = idx
                }
                dismiss()
            }

            // Add to Queue button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.addToQueue(item)
            }) {
                Image(systemName: "text.badge.plus")
                    .font(.system(size: 18))
                    .foregroundColor(.purple)
                    .frame(width: 36, height: 36)
                    .background(Color.white.opacity(0.08))
                    .clipShape(Circle())
            }
            .buttonStyle(.plain)
        }
        .padding(.vertical, 4)
        .contextMenu {
            Button(action: {
                player.playTrack(item)
                dismiss()
            }) {
                Label("Phát ngay", systemImage: "play.fill")
            }

            Button(action: {
                player.addToQueue(item)
            }) {
                Label("Thêm vào danh sách chờ", systemImage: "text.badge.plus")
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
        }
    }
}

