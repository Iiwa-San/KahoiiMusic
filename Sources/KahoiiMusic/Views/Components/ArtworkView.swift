import SwiftUI

public struct ArtworkView: View {
    public let artworkUrl: URL?
    public let isPlaying: Bool
    public let accentColor: Color

    public init(artworkUrl: URL?, isPlaying: Bool, accentColor: Color = Color.purple) {
        self.artworkUrl = artworkUrl
        self.isPlaying = isPlaying
        self.accentColor = accentColor
    }

    public var body: some View {
        ZStack {
            // Ambient colored glow
            if let _ = artworkUrl {
                AsyncImage(url: artworkUrl) { phase in
                    switch phase {
                    case .success(let image):
                        image
                            .resizable()
                            .aspectRatio(contentMode: .fill)
                            .blur(radius: 35)
                            .opacity(isPlaying ? 0.45 : 0.2)
                            .scaleEffect(0.92)
                    default:
                        Color.clear
                    }
                }
            }

            // Main Artwork Image
            Group {
                if let url = artworkUrl {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .empty:
                            placeholderView(isLoading: true)
                        case .success(let image):
                            image
                                .resizable()
                                .aspectRatio(contentMode: .fill)
                        case .failure:
                            placeholderView(isLoading: false)
                        @unknown default:
                            placeholderView(isLoading: false)
                        }
                    }
                } else {
                    placeholderView(isLoading: false)
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 22, style: .continuous)
                    .stroke(LinearGradient(
                        colors: [Color.white.opacity(0.2), Color.white.opacity(0.04)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ), lineWidth: 1)
            )
            .shadow(color: Color.black.opacity(0.5), radius: 24, x: 0, y: 14)
            .scaleEffect(isPlaying ? 1.0 : 0.92)
            .animation(.spring(response: 0.5, dampingFraction: 0.75), value: isPlaying)
        }
        .aspectRatio(1, contentMode: .fit)
    }

    @ViewBuilder
    private func placeholderView(isLoading: Bool) -> some View {
        ZStack {
            LinearGradient(
                colors: [Color(white: 0.16), Color(white: 0.09)],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            if isLoading {
                ProgressView()
                    .tint(.white)
                    .scaleEffect(1.2)
            } else {
                VStack(spacing: 12) {
                    Image(systemName: "music.note")
                        .font(.system(size: 64, weight: .light))
                        .foregroundStyle(
                            LinearGradient(
                                colors: [accentColor, accentColor.opacity(0.6)],
                                startPoint: .top,
                                endPoint: .bottom
                            )
                        )

                    Text("Kahoii Music")
                        .font(.system(size: 15, weight: .semibold, design: .rounded))
                        .foregroundColor(.white.opacity(0.4))
                }
            }
        }
    }
}

