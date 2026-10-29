import SwiftUI

public struct EqualizerAnimationView: View {
    public let isPlaying: Bool
    public let color: Color

    @State private var bar1Height: CGFloat = 0.4
    @State private var bar2Height: CGFloat = 0.8
    @State private var bar3Height: CGFloat = 0.5
    @State private var bar4Height: CGFloat = 0.9

    public init(isPlaying: Bool, color: Color = .purple) {
        self.isPlaying = isPlaying
        self.color = color
    }

    public var body: some View {
        HStack(alignment: .bottom, spacing: 3) {
            bar(height: bar1Height)
            bar(height: bar2Height)
            bar(height: bar3Height)
            bar(height: bar4Height)
        }
        .frame(width: 18, height: 16)
        .onAppear {
            if isPlaying {
                startAnimation()
            }
        }
        .onChange(of: isPlaying) { playing in
            if playing {
                startAnimation()
            } else {
                stopAnimation()
            }
        }
    }

    private func bar(height: CGFloat) -> some View {
        RoundedRectangle(cornerRadius: 1.5)
            .fill(color)
            .frame(width: 2.5, height: max(3, 16 * height))
    }

    private func startAnimation() {
        withAnimation(.easeInOut(duration: 0.45).repeatForever(autoreverses: true)) {
            bar1Height = 0.9
        }
        withAnimation(.easeInOut(duration: 0.35).repeatForever(autoreverses: true)) {
            bar2Height = 0.3
        }
        withAnimation(.easeInOut(duration: 0.5).repeatForever(autoreverses: true)) {
            bar3Height = 0.95
        }
        withAnimation(.easeInOut(duration: 0.4).repeatForever(autoreverses: true)) {
            bar4Height = 0.25
        }
    }

    private func stopAnimation() {
        withAnimation(.easeOut(duration: 0.2)) {
            bar1Height = 0.3
            bar2Height = 0.3
            bar3Height = 0.3
            bar4Height = 0.3
        }
    }
}

