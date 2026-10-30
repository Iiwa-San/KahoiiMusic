import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct HistorySheet: View {
    @ObservedObject var player: AudioPlayerService
    @ObservedObject var historyService = HistoryService.shared
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
                            Text("Lịch sử phát")
                                .font(.system(size: 20, weight: .bold))
                                .foregroundColor(.white)

                            Text("\(historyService.history.count) bài đã nghe gần đây")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.6))
                        }

                        Spacer()

                        if !historyService.history.isEmpty {
                            Button(action: {
                                #if canImport(UIKit)
                                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                                #endif
                                // Play all history
                                player.playlist = historyService.history
                                player.currentIndex = 0
                                if let first = historyService.history.first {
                                    player.playTrack(first)
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

                    if historyService.history.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Image(systemName: "clock.arrow.circlepath")
                                .font(.system(size: 56))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Chưa có lịch sử phát")
                                .font(.system(size: 17, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Các bài hát bạn đã nghe sẽ tự động lưu lại ở đây.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 40)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(historyService.history) { item in
                                historyRow(item: item)
                                    .listRowBackground(Color.clear)
                                    .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                            .onDelete { offsets in
                                for index in offsets {
                                    let item = historyService.history[index]
                                    historyService.removeHistoryItem(id: item.id)
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
                ToolbarItem(placement: .navigationBarLeading) {
                    if !historyService.history.isEmpty {
                        Button("Xóa tất cả") {
                            historyService.clearHistory()
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
        }
    }

    @ViewBuilder
    private func historyRow(item: PlaylistItem) -> some View {
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
                    .frame(width: 48, height: 48)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                // Info
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 14, weight: .medium))
                        .foregroundColor(player.currentTrack?.id == item.id ? player.currentPalette.accentColor : .white)
                        .lineLimit(1)

                    HStack(spacing: 6) {
                        if savedService.isSaved(id: item.id) {
                            Text("Offline")
                                .font(.system(size: 10, weight: .bold))
                                .foregroundColor(.green)
                            Text("•")
                                .font(.system(size: 10))
                                .foregroundColor(.white.opacity(0.3))
                        }

                        Text(item.author.isEmpty ? "YouTube" : item.author)
                            .font(.system(size: 12))
                            .foregroundColor(.white.opacity(0.55))
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
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                player.playTrack(item, keepQueue: true)
            }

            // Download Button
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                Task {
                    if savedService.isSaved(id: item.id) {
                        savedService.deleteSavedTrack(id: item.id)
                    } else {
                        await savedService.saveTrackAsync(item)
                    }
                }
            }) {
                if savedService.isDownloading(id: item.id) {
                    ProgressView()
                        .tint(.green)
                        .scaleEffect(0.7)
                        .frame(width: 32, height: 32)
                } else {
                    Image(systemName: savedService.isSaved(id: item.id) ? "arrow.down.circle.fill" : "arrow.down.circle")
                        .font(.system(size: 17))
                        .foregroundColor(savedService.isSaved(id: item.id) ? .green : .white.opacity(0.35))
                        .frame(width: 32, height: 32)
                }
            }
            .buttonStyle(.plain)

            // Favorite Toggle
            Button(action: {
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                #endif
                historyService.toggleFavorite(item)
            }) {
                Image(systemName: historyService.isFavorite(id: item.id) ? "heart.fill" : "heart")
                    .font(.system(size: 16))
                    .foregroundColor(historyService.isFavorite(id: item.id) ? .pink : .white.opacity(0.3))
                    .frame(width: 32, height: 32)
            }
            .buttonStyle(.plain)
        }
        .padding(.vertical, 4)
        .contextMenu {
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

            if savedService.isSaved(id: item.id) {
                Button(role: .destructive, action: {
                    savedService.deleteSavedTrack(id: item.id)
                }) {
                    Label("Xóa khỏi mục Đã tải", systemImage: "trash")
                }
            } else {
                Button(action: {
                    Task { await savedService.saveTrackAsync(item) }
                }) {
                    Label("Tải về nghe Offline", systemImage: "arrow.down.circle")
                }
            }

            Button(role: .destructive, action: {
                historyService.removeHistoryItem(id: item.id)
            }) {
                Label("Xóa khỏi lịch sử", systemImage: "xmark")
            }
        }
    }
}

