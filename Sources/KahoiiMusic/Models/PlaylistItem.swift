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

    enum CodingKeys: String, CodingKey {
        case id, Id
        case title, Title
        case author, Artist, artist
        case durationSeconds, DurationSeconds
        case thumbnailUrl, ThumbnailUrl
        case streamUrl, localAudioPath, LocalAudioPath
        case addedAt, AddedAt
        case isFavorite, IsFavorite
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.id = (try? container.decodeIfPresent(String.self, forKey: .id))
            ?? (try? container.decodeIfPresent(String.self, forKey: .Id))
            ?? ""

        self.title = (try? container.decodeIfPresent(String.self, forKey: .title))
            ?? (try? container.decodeIfPresent(String.self, forKey: .Title))
            ?? ""

        self.author = (try? container.decodeIfPresent(String.self, forKey: .author))
            ?? (try? container.decodeIfPresent(String.self, forKey: .artist))
            ?? (try? container.decodeIfPresent(String.self, forKey: .Artist))
            ?? ""

        self.durationSeconds = (try? container.decodeIfPresent(Int.self, forKey: .durationSeconds))
            ?? (try? container.decodeIfPresent(Int.self, forKey: .DurationSeconds))
            ?? 0

        let thumb = (try? container.decodeIfPresent(String.self, forKey: .thumbnailUrl))
            ?? (try? container.decodeIfPresent(String.self, forKey: .ThumbnailUrl))
            ?? ""
        self.thumbnailUrl = thumb.isEmpty ? "https://i.ytimg.com/vi/\(self.id)/hqdefault.jpg" : thumb

        self.streamUrl = (try? container.decodeIfPresent(String.self, forKey: .streamUrl))
            ?? (try? container.decodeIfPresent(String.self, forKey: .localAudioPath))
            ?? (try? container.decodeIfPresent(String.self, forKey: .LocalAudioPath))

        if let date = try? container.decodeIfPresent(Date.self, forKey: .addedAt) {
            self.addedAt = date
        } else if let ms = try? container.decodeIfPresent(Double.self, forKey: .addedAt) {
            self.addedAt = ms > 100_000_000_000 ? Date(timeIntervalSince1970: ms / 1000.0) : Date(timeIntervalSince1970: ms)
        } else if let intMs = try? container.decodeIfPresent(Int64.self, forKey: .addedAt) {
            self.addedAt = intMs > 100_000_000_000 ? Date(timeIntervalSince1970: Double(intMs) / 1000.0) : Date(timeIntervalSince1970: Double(intMs))
        } else {
            self.addedAt = Date()
        }

        self.isFavorite = (try? container.decodeIfPresent(Bool.self, forKey: .isFavorite))
            ?? (try? container.decodeIfPresent(Bool.self, forKey: .IsFavorite))
            ?? false
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(author, forKey: .author)
        try container.encode(author, forKey: .artist)
        try container.encode(durationSeconds, forKey: .durationSeconds)
        try container.encode(thumbnailUrl, forKey: .thumbnailUrl)
        try container.encodeIfPresent(streamUrl, forKey: .streamUrl)
        try container.encode(Int64(addedAt.timeIntervalSince1970 * 1000), forKey: .addedAt)
        try container.encode(isFavorite, forKey: .isFavorite)
    }

    public var youtubeUrl: String {
        return "https://www.youtube.com/watch?v=\(id)"
    }

    public var artworkURL: URL? {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first
        if let localThumb = docs?.appendingPathComponent("Saved/thumb/\(id).jpg"),
           FileManager.default.fileExists(atPath: localThumb.path) {
            return localThumb
        }
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

    public var isPaused: Bool {
        if case .paused = self { return true }
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

