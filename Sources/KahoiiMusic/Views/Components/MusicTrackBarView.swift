import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct MusicTrackBarView: View {
    public let currentTime: Double
    public let duration: Double
    public let accentColor: Color
    public let onSeekCommitted: (Double) -> Void

    @State private var isDragging: Bool = false
    @State private var dragProgress: Double = 0

    public init(
        currentTime: Double,
        duration: Double,
        accentColor: Color = Color.purple,
        onSeekCommitted: @escaping (Double) -> Void
    ) {
        self.currentTime = currentTime
        self.duration = duration
        self.accentColor = accentColor
        self.onSeekCommitted = onSeekCommitted
    }

    private var activeProgress: Double {
        if isDragging {
            return dragProgress
        }
        guard duration > 0 else { return 0 }
        return min(max(currentTime / duration, 0), 1)
    }

    public var body: some View {
        VStack(spacing: 8) {
            GeometryReader { geometry in
                let barWidth = geometry.size.width

                ZStack(alignment: .leading) {
                    // Background track
                    Capsule()
                        .fill(Color.white.opacity(0.18))
                        .frame(height: isDragging ? 7 : 4)

                    // Active progress fill
                    Capsule()
                        .fill(
                            LinearGradient(
                                colors: [accentColor, accentColor.opacity(0.8)],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                        )
                        .frame(width: max(0, barWidth * activeProgress), height: isDragging ? 7 : 4)

                    // Scrubbing Thumb
                    Circle()
                        .fill(Color.white)
                        .frame(width: isDragging ? 18 : 10, height: isDragging ? 18 : 10)
                        .shadow(color: Color.black.opacity(0.4), radius: 4, x: 0, y: 2)
                        .offset(x: max(0, min(barWidth * activeProgress - (isDragging ? 9 : 5), barWidth - (isDragging ? 18 : 10))))
                }
                .frame(height: 24)
                .contentShape(Rectangle())
                .gesture(
                    DragGesture(minimumDistance: 0)
                        .onChanged { value in
                            if !isDragging {
                                isDragging = true
                                #if canImport(UIKit)
                                UIImpactFeedbackGenerator(style: .light).impactOccurred()
                                #endif
                            }
                            let percent = Double(value.location.x / barWidth)
                            dragProgress = min(max(percent, 0.0), 1.0)
                        }
                        .onEnded { value in
                            let percent = Double(value.location.x / barWidth)
                            let finalPercent = min(max(percent, 0.0), 1.0)
                            isDragging = false
                            let targetSeconds = finalPercent * duration
                            onSeekCommitted(targetSeconds)
                            #if canImport(UIKit)
                            UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                            #endif
                        }
                )
            }
            .frame(height: 24)

            // Time labels
            HStack {
                Text(formatTime(isDragging ? dragProgress * duration : currentTime))
                    .font(.system(size: 12, weight: .medium, design: .monospaced))
                    .foregroundColor(.white.opacity(0.6))

                Spacer()

                Text(formatTime(duration))
                    .font(.system(size: 12, weight: .medium, design: .monospaced))
                    .foregroundColor(.white.opacity(0.6))
            }
        }
    }

    private func formatTime(_ seconds: Double) -> String {
        guard !seconds.isNaN && !seconds.isInfinite && seconds >= 0 else {
            return "00:00"
        }
        let total = Int(seconds)
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let secs = total % 60

        if hours > 0 {
            return String(format: "%d:%02d:%02d", hours, minutes, secs)
        } else {
            return String(format: "%02d:%02d", minutes, secs)
        }
    }
}

