import Foundation

public struct UserPlaylist: Identifiable, Codable, Hashable, Equatable, Sendable {
    public let id: String
    public var name: String
    public let createdAt: Date
    public var items: [PlaylistItem]

    public init(id: String = UUID().uuidString, name: String, createdAt: Date = Date(), items: [PlaylistItem] = []) {
        self.id = id
        self.name = name
        self.createdAt = createdAt
        self.items = items
    }

    enum CodingKeys: String, CodingKey {
        case id, Id
        case name, Name
        case createdAt, CreatedAt, created, Created
        case items, Items
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.id = (try? container.decodeIfPresent(String.self, forKey: .id))
            ?? (try? container.decodeIfPresent(String.self, forKey: .Id))
            ?? UUID().uuidString

        self.name = (try? container.decodeIfPresent(String.self, forKey: .name))
            ?? (try? container.decodeIfPresent(String.self, forKey: .Name))
            ?? ""

        if let date = try? container.decodeIfPresent(Date.self, forKey: .createdAt) {
            self.createdAt = date
        } else if let ms = (try? container.decodeIfPresent(Double.self, forKey: .createdAt))
            ?? (try? container.decodeIfPresent(Double.self, forKey: .CreatedAt)) {
            self.createdAt = ms > 100_000_000_000 ? Date(timeIntervalSince1970: ms / 1000.0) : Date(timeIntervalSince1970: ms)
        } else if let intMs = (try? container.decodeIfPresent(Int64.self, forKey: .createdAt))
            ?? (try? container.decodeIfPresent(Int64.self, forKey: .CreatedAt)) {
            self.createdAt = intMs > 100_000_000_000 ? Date(timeIntervalSince1970: Double(intMs) / 1000.0) : Date(timeIntervalSince1970: Double(intMs))
        } else {
            self.createdAt = Date()
        }

        self.items = (try? container.decodeIfPresent([PlaylistItem].self, forKey: .items))
            ?? (try? container.decodeIfPresent([PlaylistItem].self, forKey: .Items))
            ?? []
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(name, forKey: .name)
        try container.encode(Int64(createdAt.timeIntervalSince1970 * 1000), forKey: .createdAt)
        try container.encode(items, forKey: .items)
    }
}