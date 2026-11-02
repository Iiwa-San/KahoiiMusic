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
}
