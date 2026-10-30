import Foundation
import Combine
import SwiftUI

public struct ErrorLogItem: Identifiable, Sendable {
    public let id: UUID
    public let timestamp: Date
    public let title: String
    public let message: String
    public let category: String

    public init(id: UUID = UUID(), timestamp: Date = Date(), title: String, message: String, category: String = "Chung") {
        self.id = id
        self.timestamp = timestamp
        self.title = title
        self.message = message
        self.category = category
    }

    public var formattedTime: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        return formatter.string(from: timestamp)
    }
}

public struct ErrorToastItem: Identifiable, Equatable {
    public let id = UUID()
    public let title: String
    public let message: String
    public let isError: Bool

    public static func == (lhs: ErrorToastItem, rhs: ErrorToastItem) -> Bool {
        lhs.id == rhs.id
    }
}

@MainActor
public class ErrorLogService: ObservableObject {
    public static let shared = ErrorLogService()

    @Published public private(set) var logs: [ErrorLogItem] = []
    @Published public var activeToast: ErrorToastItem? = nil

    private var dismissTask: Task<Void, Never>? = nil

    private init() {}

    public func log(_ title: String, _ message: String, category: String = "Chung", isError: Bool = true, showToast: Bool = true) {
        let item = ErrorLogItem(title: title, message: message, category: category)
        logs.insert(item, at: 0)
        if logs.count > 100 {
            logs.removeLast()
        }

        print("[ErrorLogService] [\(category)] \(title): \(message)")

        if showToast {
            dismissTask?.cancel()
            activeToast = ErrorToastItem(title: title, message: message, isError: isError)
            dismissTask = Task {
                try? await Task.sleep(nanoseconds: 6_000_000_000) // 6 seconds
                if !Task.isCancelled {
                    self.activeToast = nil
                }
            }
        }
    }

    public func dismissToast() {
        dismissTask?.cancel()
        activeToast = nil
    }

    public func clearLogs() {
        logs.removeAll()
        dismissToast()
    }
}

