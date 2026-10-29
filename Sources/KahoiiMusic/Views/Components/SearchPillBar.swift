import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct SearchPillBar: View {
    @Binding public var text: String
    public let placeholder: String
    public let onSubmit: () -> Void
    public let onPaste: () -> Void

    @FocusState private var isFocused: Bool

    public init(
        text: Binding<String>,
        placeholder: String = "Tìm kiếm bài hát hoặc dán link YouTube...",
        onSubmit: @escaping () -> Void,
        onPaste: @escaping () -> Void
    ) {
        self._text = text
        self.placeholder = placeholder
        self.onSubmit = onSubmit
        self.onPaste = onPaste
    }

    public var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "magnifyingglass")
                .foregroundColor(.white.opacity(0.5))
                .font(.system(size: 16, weight: .semibold))

            TextField("", text: $text, prompt: Text(placeholder).foregroundColor(.white.opacity(0.35)))
                .foregroundColor(.white)
                .font(.system(size: 14, weight: .regular))
                .submitLabel(.search)
                .focused($isFocused)
                .onSubmit {
                    onSubmit()
                }

            if !text.isEmpty {
                Button(action: {
                    text = ""
                }) {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundColor(.white.opacity(0.5))
                        .font(.system(size: 16))
                }
            } else {
                Button(action: {
                    #if canImport(UIKit)
                    if let clip = UIPasteboard.general.string, !clip.isEmpty {
                        text = clip
                        onPaste()
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    }
                    #endif
                }) {
                    HStack(spacing: 4) {
                        Image(systemName: "doc.on.clipboard")
                            .font(.system(size: 12))
                        Text("Dán")
                            .font(.system(size: 11, weight: .semibold))
                    }
                    .foregroundColor(.white.opacity(0.75))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.white.opacity(0.12))
                    .clipShape(Capsule())
                }
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(
            Capsule()
                .fill(Color.white.opacity(0.08))
                .overlay(
                    Capsule()
                        .stroke(Color.white.opacity(isFocused ? 0.25 : 0.1), lineWidth: 1)
                )
        )
    }
}

