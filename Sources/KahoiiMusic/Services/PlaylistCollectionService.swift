import Foundation
import Combine

@MainActor
public class PlaylistCollectionService: ObservableObject {
    public static let shared = PlaylistCollectionService()

    @Published public var playlists: [UserPlaylist] = []

    private let fileManager = FileManager.default
    private let fileName = "kahoii_playlists.json"

    private init() {
        loadPlaylists()
    }

    private var storageDirectory: URL {
        let paths = fileManager.urls(for: .documentDirectory, in: .userDomainMask)
        return paths.first ?? fileManager.temporaryDirectory
    }

    private var fileURL: URL {
        storageDirectory.appendingPathComponent(fileName)
    }

    public func loadPlaylists() {
        guard fileManager.fileExists(atPath: fileURL.path) else {
            playlists = []
            return
        }
        do {
            let data = try Data(contentsOf: fileURL)
            playlists = try JSONDecoder().decode([UserPlaylist].self, from: data)
        } catch {
            print("Failed to load playlists: \(error)")
            playlists = []
        }
    }

    private func savePlaylists() {
        do {
            let data = try JSONEncoder().encode(playlists)
            try data.write(to: fileURL, options: .atomic)
        } catch {
            print("Failed to save playlists: \(error)")
        }
    }

    @discardableResult
    public func savePlaylist(name: String, items: [PlaylistItem]) -> UserPlaylist {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        let pl = UserPlaylist(name: trimmed.isEmpty ? "Playlist" : trimmed, items: items)
        playlists.insert(pl, at: 0)
        savePlaylists()
        return pl
    }

    @discardableResult
    public func createPlaylist(name: String) -> UserPlaylist {
        return savePlaylist(name: name, items: [])
    }

    public func deletePlaylist(id: String) {
        playlists.removeAll { $0.id == id }
        savePlaylists()
    }

    public func renamePlaylist(id: String, newName: String) {
        if let idx = playlists.firstIndex(where: { $0.id == id }) {
            playlists[idx].name = newName.trimmingCharacters(in: .whitespacesAndNewlines)
            savePlaylists()
        }
    }

    public func addTrack(to playlistId: String, track: PlaylistItem) {
        if let idx = playlists.firstIndex(where: { $0.id == playlistId }) {
            if !playlists[idx].items.contains(where: { $0.id == track.id }) {
                playlists[idx].items.append(track)
                savePlaylists()
            }
        }
    }

    public func removeTrack(from playlistId: String, trackId: String) {
        if let idx = playlists.firstIndex(where: { $0.id == playlistId }) {
            playlists[idx].items.removeAll { $0.id == trackId }
            savePlaylists()
        }
    }
}
