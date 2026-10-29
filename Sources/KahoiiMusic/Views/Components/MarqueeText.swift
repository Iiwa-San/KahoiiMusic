import SwiftUI

public struct MarqueeText: View {
    public let text: String
    public let font: Font
    public let color: Color

    @State private var textWidth: CGFloat = 0
    @State private var containerWidth: CGFloat = 0
    @State private var offset: CGFloat = 0
    @State private var isAnimating: Bool = false

    public init(text: String, font: Font = .system(size: 20, weight: .bold, design: .rounded), color: Color = .white) {
        self.text = text
        self.font = font
        self.color = color
    }

    public var body: some View {
        GeometryReader { proxy in
            let availableWidth = proxy.size.width
            let shouldScroll = textWidth > availableWidth

            ZStack(alignment: .leading) {
                if shouldScroll {
                    HStack(spacing: 40) {
                        textView
                        textView
                    }
                    .offset(x: offset)
                    .onAppear {
                        startAnimation(scrollDistance: textWidth + 40)
                    }
                    .onChange(of: text) { _ in
                        offset = 0
                        startAnimation(scrollDistance: textWidth + 40)
                    }
                } else {
                    textView
                        .frame(maxWidth: .infinity, alignment: .center)
                }
            }
            .frame(width: availableWidth, alignment: shouldScroll ? .leading : .center)
            .clipped()
            .onAppear {
                containerWidth = availableWidth
            }
        }
        .frame(height: 28)
    }

    private var textView: some View {
        Text(text)
            .font(font)
            .foregroundColor(color)
            .lineLimit(1)
            .fixedSize(horizontal: true, vertical: false)
            .background(
                GeometryReader { textGeo in
                    Color.clear
                        .preference(key: TextWidthPreferenceKey.self, value: textGeo.size.width)
                }
            )
            .onPreferenceChange(TextWidthPreferenceKey.self) { width in
                self.textWidth = width
            }
    }

    private func startAnimation(scrollDistance: CGFloat) {
        guard scrollDistance > 0 else { return }
        withAnimation(.linear(duration: Double(textWidth / 30.0)).delay(1.5).repeatForever(autoreverses: false)) {
            offset = -scrollDistance
        }
    }
}

private struct TextWidthPreferenceKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

