import Foundation
import Combine

public struct CloudMusicPayload: Codable {
    public var playlists: [PlaylistItem]
    public var savedTracks: [PlaylistItem]
    public var history: [PlaylistItem]
    public var favorites: [PlaylistItem]
    public var collections: [UserPlaylist]
    public var updatedAt: String

    public init(playlists: [PlaylistItem] = [],
                savedTracks: [PlaylistItem] = [],
                history: [PlaylistItem] = [],
                favorites: [PlaylistItem] = [],
                collections: [UserPlaylist] = [],
                updatedAt: String = ISO8601DateFormatter().string(from: Date())) {
        self.playlists = playlists
        self.savedTracks = savedTracks
        self.history = history
        self.favorites = favorites
        self.collections = collections
        self.updatedAt = updatedAt
    }
}

@MainActor
public class FirebaseSyncService: ObservableObject {
    public static let shared = FirebaseSyncService()

    @Published public var isSyncing: Bool = false
    @Published public var syncStatusMessage: String = ""
    public var isApplyingCloudPayload: Bool = false
    private var autoUploadTask: Task<Void, Never>? = nil

    private let projectId = "kahoiimusic"
    private let apiKey = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8"

    private init() {}

    public func syncFetch(userId: String, idToken: String) async -> CloudMusicPayload? {
        isSyncing = true
        syncStatusMessage = "Đang tải dữ liệu đám mây..."
        defer { isSyncing = false }

        let endpoint = "https://firestore.googleapis.com/v1/projects/\(projectId)/databases/(default)/documents/users/\(userId)?key=\(apiKey)"
        guard let url = URL(string: endpoint) else { return nil }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.setValue("Bearer \(idToken)", forHTTPHeaderField: "Authorization")

        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                syncStatusMessage = "Chưa có dữ liệu đồng bộ đám mây"
                return nil
            }

            guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let fields = json["fields"] as? [String: Any],
                  let musicDataJsonField = fields["musicDataJson"] as? [String: Any],
                  let stringValue = musicDataJsonField["stringValue"] as? String,
                  let stringData = stringValue.data(using: .utf8) else {
                syncStatusMessage = "Dữ liệu đám mây rỗng"
                return nil
            }

            let payload = try JSONDecoder().decode(CloudMusicPayload.self, from: stringData)
            syncStatusMessage = "Đồng bộ đám mây thành công"
            return payload
        } catch {
            syncStatusMessage = "Lỗi tải đồng bộ: \(error.localizedDescription)"
            return nil
        }
    }

    public func syncUpload(userId: String, idToken: String, payload: CloudMusicPayload) async -> Bool {
        isSyncing = true
        syncStatusMessage = "Đang lưu lên đám mây..."
        defer { isSyncing = false }

        let endpoint = "https://firestore.googleapis.com/v1/projects/\(projectId)/databases/(default)/documents/users/\(userId)?updateMask.fieldPaths=musicDataJson&updateMask.fieldPaths=updatedAt&key=\(apiKey)"
        guard let url = URL(string: endpoint) else { return false }

        guard let payloadData = try? JSONEncoder().encode(payload),
              let jsonString = String(data: payloadData, encoding: .utf8) else {
            syncStatusMessage = "Lỗi mã hóa dữ liệu"
            return false
        }

        var request = URLRequest(url: url)
        request.httpMethod = "PATCH"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(idToken)", forHTTPHeaderField: "Authorization")

        let firestoreBody: [String: Any] = [
            "fields": [
                "musicDataJson": [
                    "stringValue": jsonString
                ],
                "updatedAt": [
                    "stringValue": payload.updatedAt
                ]
            ]
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: firestoreBody)

        do {
            let (_, response) = try await URLSession.shared.data(for: request)
            guard let http = response as? HTTPURLResponse, (http.statusCode == 200 || http.statusCode == 201) else {
                syncStatusMessage = "Lưu đám mây không thành công"
                return false
            }
            syncStatusMessage = "Đã lưu lên đám mây thành công"
            return true
        } catch {
            syncStatusMessage = "Lỗi lưu đám mây: \(error.localizedDescription)"
            return false
        }
    }

    public func requestAutoUpload() {
        guard let user = FirebaseAuthService.shared.currentUser, !isApplyingCloudPayload else { return }
        autoUploadTask?.cancel()
        autoUploadTask = Task {
            try? await Task.sleep(nanoseconds: 2_500_000_000)
            guard !Task.isCancelled else { return }
            let payload = CloudMusicPayload(
                playlists: AudioPlayerService.shared.playlist,
                savedTracks: SavedTracksService.shared.savedTracks,
                history: HistoryService.shared.history,
                favorites: HistoryService.shared.favorites,
                collections: PlaylistCollectionService.shared.playlists
            )
            _ = await syncUpload(userId: user.localId, idToken: user.idToken, payload: payload)
        }
    }

    public func triggerAutoFetch() {
        guard let user = FirebaseAuthService.shared.currentUser else { return }
        Task {
            if let payload = await syncFetch(userId: user.localId, idToken: user.idToken) {
                isApplyingCloudPayload = true
                applyCloudPayload(payload)
                isApplyingCloudPayload = false
            } else {
                requestAutoUpload()
            }
        }
    }

    public func applyCloudPayload(_ payload: CloudMusicPayload) {
        // 1. Favorites
        for fav in payload.favorites {
            if !HistoryService.shared.favorites.contains(where: { $0.id == fav.id }) {
                HistoryService.shared.favorites.append(fav)
            }
        }

        // 2. Collections
        for cloudCol in payload.collections {
            if let localCol = PlaylistCollectionService.shared.playlists.first(where: { $0.id == cloudCol.id }) {
                for item in cloudCol.items {
                    if !localCol.items.contains(where: { $0.id == item.id }) {
                        PlaylistCollectionService.shared.addTrack(to: localCol.id, track: item)
                    }
                }
            } else {
                let created = PlaylistCollectionService.shared.createPlaylist(name: cloudCol.name)
                for item in cloudCol.items {
                    PlaylistCollectionService.shared.addTrack(to: created.id, track: item)
                }
            }
        }

        // 3. Auto-download missing savedTracks
        Task {
            for track in payload.savedTracks {
                if !SavedTracksService.shared.isSaved(id: track.id) {
                    await SavedTracksService.shared.saveTrackAsync(track)
                }
            }
        }
    }
}