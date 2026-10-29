import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct FavoritesSheet: View {
    @ObservedObject var player: AudioPlayerService
    @ObservedObject var historyService = HistoryService.shared
    @Environment(\.dismiss) private var dismiss

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
                            Text("Bài hát yêu thích")
                                .font(.system(size: 20, weight: .bold))
                                .foregroundColor(.white)

                            Text("\(historyService.favorites.count) bài đã lưu")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.6))
                        }

                        Spacer()

                        if !historyService.favorites.isEmpty {
                            Button(action: {
                                #if canImport(UIKit)
                                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                                #endif
                                player.playlist = historyService.favorites
                                player.currentIndex = 0
                                if let first = historyService.favorites.first {
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
                                .background(Color.pink.opacity(0.8))
                                .clipShape(Capsule())
                            }
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 14)

                    Divider().background(Color.white.opacity(0.1))

                    if historyService.favorites.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Image(systemName: "heart.slash")
                                .font(.system(size: 56))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Chưa có bài hát yêu thích")
                                .font(.system(size: 17, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Nhấn biểu tượng trái tim khi nghe nhạc để lưu bài hát vào đây.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 40)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(historyService.favorites) { item in
                                favoriteRow(item: item)
                                    .listRowBackground(Color.clear)
                                    .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                            .onDelete { offsets in
                                for index in offsets {
                                    let item = historyService.favorites[index]
                                    historyService.toggleFavorite(item)
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
                    .foregroundColor(.white)
                    .fontWeight(.semibold)
                }
            }
        }
    }

    @ViewBuilder
    private func favoriteRow(item: PlaylistItem) -> some View {
        Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            player.playTrack(item)
        }) {
            HStack(spacing: 12) {
                if let url = item.artworkURL {
                    AsyncImage(url: url) { img in
                        img.resizable().aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Color.white.opacity(0.1)
                    }
                    .frame(width: 48, height: 48)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 14, weight: .medium))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    HStack(spacing: 6) {
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

                Button(action: {
                    #if canImport(UIKit)
                    UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    #endif
                    historyService.toggleFavorite(item)
                }) {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 16))
                        .foregroundColor(.pink)
                        .frame(width: 32, height: 32)
                }
                .buttonStyle(PlainButtonStyle())
            }
            .padding(.vertical, 4)
        }
        .buttonStyle(PlainButtonStyle())
    }
}

