import Foundation
import Combine
import SwiftUI

@MainActor
public class SleepTimerService: ObservableObject {
    public static let shared = SleepTimerService()

    @Published public private(set) var remainingSeconds: Int = 0
    @Published public private(set) var isActive: Bool = false
    @Published public private(set) var isEndOfTrack: Bool = false

    private var timer: Timer?

    public var onTimerExpired: (() -> Void)?

    private init() {}

    public var formattedRemainingTime: String {
        if isEndOfTrack {
            return "Hết bài"
        }
        guard isActive, remainingSeconds > 0 else {
            return ""
        }
        let m = remainingSeconds / 60
        let s = remainingSeconds % 60
        return String(format: "%02d:%02d", m, s)
    }

    public func startTimer(minutes: Int) {
        cancelTimer()
        guard minutes > 0 else { return }

        remainingSeconds = minutes * 60
        isActive = true
        isEndOfTrack = false

        timer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self = self else { return }
                if self.remainingSeconds > 1 {
                    self.remainingSeconds -= 1
                } else {
                    self.timerExpired()
                }
            }
        }
    }

    public func startEndOfTrack() {
        cancelTimer()
        isActive = true
        isEndOfTrack = true
        remainingSeconds = 0
    }

    public func cancelTimer() {
        timer?.invalidate()
        timer = nil
        remainingSeconds = 0
        isActive = false
        isEndOfTrack = false
    }

    private func timerExpired() {
        cancelTimer()
        onTimerExpired?()
    }

    public func handleTrackFinished() {
        if isEndOfTrack {
            cancelTimer()
            onTimerExpired?()
        }
    }
}

