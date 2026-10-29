import SwiftUI

public struct DynamicGradientView: View {
    public let palette: GradientPalette
    @State private var animateGlow: Bool = false

    public init(palette: GradientPalette) {
        self.palette = palette
    }

    public var body: some View {
        ZStack {
            // Base background
            palette.bottomColor
                .ignoresSafeArea()

            // Dynamic linear blend
            LinearGradient(
                colors: [
                    palette.topColor.opacity(0.85),
                    palette.bottomColor
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            // Ambient glowing radial orbs
            GeometryReader { proxy in
                Circle()
                    .fill(palette.accentColor.opacity(0.18))
                    .frame(width: proxy.size.width * 0.9, height: proxy.size.width * 0.9)
                    .blur(radius: 70)
                    .offset(
                        x: animateGlow ? 40 : -40,
                        y: animateGlow ? proxy.size.height * 0.15 : proxy.size.height * 0.05
                    )

                Circle()
                    .fill(palette.topColor.opacity(0.25))
                    .frame(width: proxy.size.width * 0.7, height: proxy.size.width * 0.7)
                    .blur(radius: 60)
                    .offset(
                        x: animateGlow ? -30 : 30,
                        y: proxy.size.height * 0.4
                    )
            }
            .ignoresSafeArea()
            .animation(.easeInOut(duration: 6.0).repeatForever(autoreverses: true), value: animateGlow)
            .onAppear {
                animateGlow = true
            }
        }
        .animation(.easeInOut(duration: 0.8), value: palette.topColor)
    }
}

