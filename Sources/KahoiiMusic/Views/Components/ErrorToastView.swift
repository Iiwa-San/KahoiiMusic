import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct ErrorToastView: View {
    @ObservedObject var errorLog = ErrorLogService.shared
    var onShowDetails: (() -> Void)? = nil

    public init(onShowDetails: (() -> Void)? = nil) {
        self.onShowDetails = onShowDetails
    }

    public var body: some View {
        if let toast = errorLog.activeToast {
            VStack {
                HStack(alignment: .top, spacing: 12) {
                    Image(systemName: toast.isError ? "exclamationmark.octagon.fill" : "info.circle.fill")
                        .font(.system(size: 22))
                        .foregroundColor(toast.isError ? .red : .yellow)

                    VStack(alignment: .leading, spacing: 2) {
                        Text(toast.title)
                            .font(.system(size: 14, weight: .bold))
                            .foregroundColor(.white)

                        Text(toast.message)
                            .font(.system(size: 12))
                            .foregroundColor(.white.opacity(0.85))
                            .lineLimit(3)
                    }

                    Spacer()

                    VStack(spacing: 6) {
                        Button(action: {
                            #if canImport(UIKit)
                            UIImpactFeedbackGenerator(style: .light).impactOccurred()
                            #endif
                            errorLog.dismissToast()
                        }) {
                            Image(systemName: "xmark")
                                .font(.system(size: 12, weight: .bold))
                                .foregroundColor(.white.opacity(0.6))
                                .padding(6)
                                .background(Color.white.opacity(0.1))
                                .clipShape(Circle())
                        }

                        if onShowDetails != nil {
                            Button(action: {
                                onShowDetails?()
                            }) {
                                Text("Chi tiết")
                                    .font(.system(size: 10, weight: .bold))
                                    .foregroundColor(.purple)
                            }
                        }
                    }
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 12)
                .background(
                    RoundedRectangle(cornerRadius: 16, style: .continuous)
                        .fill(Color(white: 0.12).opacity(0.95))
                        .overlay(
                            RoundedRectangle(cornerRadius: 16, style: .continuous)
                                .stroke(toast.isError ? Color.red.opacity(0.4) : Color.yellow.opacity(0.4), lineWidth: 1)
                        )
                )
                .shadow(color: Color.black.opacity(0.4), radius: 12, x: 0, y: 6)
                .padding(.horizontal, 16)
                .padding(.top, 8)

                Spacer()
            }
            .transition(.move(edge: .top).combined(with: .opacity))
            .animation(.spring(response: 0.35, dampingFraction: 0.8), value: errorLog.activeToast)
            .zIndex(999)
        }
    }
}

