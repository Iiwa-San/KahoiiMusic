import Foundation

public struct PlaylistItem: Identifiable, Codable, Hashable, Equatable, Sendable {
    public let id: String
    public var title: String
    public var author: String
    public var durationSeconds: Int
    public var thumbnailUrl: String
    public var streamUrl: String?
    public var addedAt: Date
    public var isFavorite: Bool

    public init(
        id: String,
        title: String,
        author: String = "",
        durationSeconds: Int = 0,
        thumbnailUrl: String = "",
        streamUrl: String? = nil,
        addedAt: Date = Date(),
        isFavorite: Bool = false
    ) {
        self.id = id
        self.title = title
        self.author = author
        self.durationSeconds = durationSeconds
        self.thumbnailUrl = thumbnailUrl.isEmpty ? "https://i.ytimg.com/vi/\(id)/hqdefault.jpg" : thumbnailUrl
        self.streamUrl = streamUrl
        self.addedAt = addedAt
        self.isFavorite = isFavorite
    }

    public var youtubeUrl: String {
        return "https://www.youtube.com/watch?v=\(id)"
    }

    public var artworkURL: URL? {
        if !thumbnailUrl.isEmpty, let url = URL(string: thumbnailUrl) {
            return url
        }
        return URL(string: "https://i.ytimg.com/vi/\(id)/hqdefault.jpg")
    }

    public var highResArtworkURL: URL? {
        return URL(string: "https://i.ytimg.com/vi/\(id)/maxresdefault.jpg") ?? artworkURL
    }

    public var formattedDuration: String {
        if durationSeconds <= 0 {
            return "--:--"
        }
        let hours = durationSeconds / 3600
        let minutes = (durationSeconds % 3600) / 60
        let seconds = durationSeconds % 60

        if hours > 0 {
            return String(format: "%d:%02d:%02d", hours, minutes, seconds)
        } else {
            return String(format: "%02d:%02d", minutes, seconds)
        }
    }
}

public enum PlaybackState: Equatable, Sendable {
    case idle
    case loading(String)
    case playing
    case paused
    case failed(String)

    public var isPlaying: Bool {
        if case .playing = self { return true }
        return false
    }

    public var isLoading: Bool {
        if case .loading = self { return true }
        return false
    }
}

public enum LoopMode: Int, Codable, CaseIterable, Sendable {
    case off = 0
    case all = 1
    case one = 2

    public var iconName: String {
        switch self {
        case .off: return "repeat"
        case .all: return "repeat"
        case .one: return "repeat.1"
        }
    }

    public var title: String {
        switch self {
        case .off: return "Tắt lặp lại"
        case .all: return "Lặp lại danh sách"
        case .one: return "Lặp lại 1 bài"
        }
    }
}

