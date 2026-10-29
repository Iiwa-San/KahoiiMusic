import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct MiniPlayerView: View {
    @ObservedObject var player: AudioPlayerService
    public let onTap: () -> Void

    public init(player: AudioPlayerService, onTap: @escaping () -> Void) {
        self.player = player
        self.onTap = onTap
    }

    public var body: some View {
        if let track = player.currentTrack {
            Button(action: onTap) {
                HStack(spacing: 12) {
                    // Artwork Thumbnail
                    if let url = track.artworkURL {
                        AsyncImage(url: url) { image in
                            image
                                .resizable()
                                .aspectRatio(contentMode: .fill)
                        } placeholder: {
                            Color.white.opacity(0.1)
                        }
                        .frame(width: 44, height: 44)
                        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                    }

                    // Title & Artist
                    VStack(alignment: .leading, spacing: 2) {
                        Text(track.title)
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundColor(.white)
                            .lineLimit(1)

                        Text(track.author.isEmpty ? "Kahoii Music" : track.author)
                            .font(.system(size: 12, weight: .regular))
                            .foregroundColor(.white.opacity(0.6))
                            .lineLimit(1)
                    }

                    Spacer()

                    // Play / Pause Button
                    Button(action: {
                        #if canImport(UIKit)
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        #endif
                        player.togglePlayPause()
                    }) {
                        Image(systemName: player.playbackState.isPlaying ? "pause.fill" : "play.fill")
                            .font(.system(size: 18))
                            .foregroundColor(.white)
                            .frame(width: 36, height: 36)
                    }

                    // Next Button
                    Button(action: {
                        #if canImport(UIKit)
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        #endif
                        player.nextTrack()
                    }) {
                        Image(systemName: "forward.fill")
                            .font(.system(size: 16))
                            .foregroundColor(.white.opacity(0.8))
                            .frame(width: 36, height: 36)
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 8)
                .background(
                    RoundedRectangle(cornerRadius: 16, style: .continuous)
                        .fill(Color(white: 0.12).opacity(0.95))
                        .overlay(
                            RoundedRectangle(cornerRadius: 16, style: .continuous)
                                .stroke(Color.white.opacity(0.12), lineWidth: 1)
                        )
                )
                .shadow(color: Color.black.opacity(0.4), radius: 12, x: 0, y: 6)
            }
            .buttonStyle(PlainButtonStyle())
            .padding(.horizontal, 16)
            .padding(.bottom, 8)
        }
    }
}

