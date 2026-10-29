import Foundation
import Combine

public struct AppSettings: Codable, Sendable {
    public var autoPlayNext: Bool = true
    public var highQualityAudio: Bool = true
    public var keepHistoryCount: Int = 100

    public init(
        autoPlayNext: Bool = true,
        highQualityAudio: Bool = true,
        keepHistoryCount: Int = 100
    ) {
        self.autoPlayNext = autoPlayNext
        self.highQualityAudio = highQualityAudio
        self.keepHistoryCount = keepHistoryCount
    }
}

@MainActor
public class HistoryService: ObservableObject {
    public static let shared = HistoryService()

    @Published public var history: [PlaylistItem] = []
    @Published public var favorites: [PlaylistItem] = []
    @Published public var settings: AppSettings = AppSettings()

    private let fileManager = FileManager.default
    private let historyFileName = "kahoii_history.json"
    private let favoritesFileName = "kahoii_favorites.json"
    private let settingsFileName = "kahoii_settings.json"

    private init() {
        loadSettings()
        loadHistory()
        loadFavorites()
    }

    private var storageDirectory: URL {
        let paths = fileManager.urls(for: .documentDirectory, in: .userDomainMask)
        return paths.first ?? fileManager.temporaryDirectory
    }

    // MARK: - History Management

    public func recordPlay(_ track: PlaylistItem) {
        var mutableTrack = track
        mutableTrack.addedAt = Date()

        // Remove duplicate if already exists
        history.removeAll { $0.id == track.id }
        // Prepend to top
        history.insert(mutableTrack, at: 0)

        // Trim exceeding limit
        if history.count > settings.keepHistoryCount {
            history = Array(history.prefix(settings.keepHistoryCount))
        }

        saveHistory()
    }

    public func removeHistoryItem(id: String) {
        history.removeAll { $0.id == id }
        saveHistory()
    }

    public func clearHistory() {
        history.removeAll()
        saveHistory()
    }

    // MARK: - Favorites Management

    public func isFavorite(id: String) -> Bool {
        return favorites.contains { $0.id == id }
    }

    public func toggleFavorite(_ track: PlaylistItem) {
        if let idx = favorites.firstIndex(where: { $0.id == track.id }) {
            favorites.remove(at: idx)
        } else {
            var fav = track
            fav.isFavorite = true
            fav.addedAt = Date()
            favorites.insert(fav, at: 0)
        }
        saveFavorites()
    }

    // MARK: - Persistence

    private func saveHistory() {
        let url = storageDirectory.appendingPathComponent(historyFileName)
        do {
            let data = try JSONEncoder().encode(history)
            try data.write(to: url, options: .atomic)
        } catch {
            print("[HistoryService] Failed to save history: \(error)")
        }
    }

    private func loadHistory() {
        let url = storageDirectory.appendingPathComponent(historyFileName)
        guard fileManager.fileExists(atPath: url.path) else { return }
        do {
            let data = try Data(contentsOf: url)
            history = try JSONDecoder().decode([PlaylistItem].self, from: data)
        } catch {
            print("[HistoryService] Failed to load history: \(error)")
        }
    }

    private func saveFavorites() {
        let url = storageDirectory.appendingPathComponent(favoritesFileName)
        do {
            let data = try JSONEncoder().encode(favorites)
            try data.write(to: url, options: .atomic)
        } catch {
            print("[HistoryService] Failed to save favorites: \(error)")
        }
    }

    private func loadFavorites() {
        let url = storageDirectory.appendingPathComponent(favoritesFileName)
        guard fileManager.fileExists(atPath: url.path) else { return }
        do {
            let data = try Data(contentsOf: url)
            favorites = try JSONDecoder().decode([PlaylistItem].self, from: data)
        } catch {
            print("[HistoryService] Failed to load favorites: \(error)")
        }
    }

    public func updateSettings(_ newSettings: AppSettings) {
        settings = newSettings
        saveSettings()
    }

    private func saveSettings() {
        let url = storageDirectory.appendingPathComponent(settingsFileName)
        do {
            let data = try JSONEncoder().encode(settings)
            try data.write(to: url, options: .atomic)
        } catch {
            print("[HistoryService] Failed to save settings: \(error)")
        }
    }

    private func loadSettings() {
        let url = storageDirectory.appendingPathComponent(settingsFileName)
        guard fileManager.fileExists(atPath: url.path) else { return }
        do {
            let data = try Data(contentsOf: url)
            settings = try JSONDecoder().decode(AppSettings.self, from: data)
        } catch {
            print("[HistoryService] Failed to load settings: \(error)")
        }
    }
}

