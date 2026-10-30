import Foundation
import Combine
import SwiftUI

@MainActor
public class SavedTracksService: ObservableObject {
    public static let shared = SavedTracksService()

    @Published public private(set) var savedTracks: [PlaylistItem] = []
    @Published public private(set) var downloadingTrackIds: Set<String> = []
    @Published public var downloadErrorMessage: String?

    private lazy var downloadSession: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 25.0
        config.timeoutIntervalForResource = 90.0
        return URLSession(configuration: config)
    }()

    public var baseDir: URL {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        return docs.appendingPathComponent("Saved", isDirectory: true)
    }

    public var audioDir: URL {
        baseDir.appendingPathComponent("audio", isDirectory: true)
    }

    public var thumbDir: URL {
        baseDir.appendingPathComponent("thumb", isDirectory: true)
    }

    private var jsonFile: URL {
        baseDir.appendingPathComponent("saved_tracks.json")
    }

    public var storagePathDescription: String {
        "Tệp > Trên iPhone > Kahoii Music > Saved"
    }

    private init() {
        ensureDirectoriesExist()
        loadFromFile()
    }

    public func ensureDirectoriesExist() {
        let fm = FileManager.default
        do {
            try fm.createDirectory(at: audioDir, withIntermediateDirectories: true, attributes: nil)
            try fm.createDirectory(at: thumbDir, withIntermediateDirectories: true, attributes: nil)
        } catch {
            print("[SavedTracksService] Error creating directories: \(error)")
            ErrorLogService.shared.log("Lỗi tạo thư mục", error.localizedDescription, category: "Hệ thống")
        }
    }

    public func isSaved(id: String) -> Bool {
        guard !id.isEmpty else { return false }
        guard savedTracks.contains(where: { $0.id == id }) else { return false }
        return savedAudioURL(for: id) != nil
    }

    public func isDownloading(id: String) -> Bool {
        downloadingTrackIds.contains(id)
    }

    public func savedAudioURL(for id: String) -> URL? {
        guard !id.isEmpty else { return nil }
        let fm = FileManager.default
        let exts = ["m4a", "mp3", "webm", "aac", "opus"]
        for ext in exts {
            let fileUrl = audioDir.appendingPathComponent("\(id).\(ext)")
            if fm.fileExists(atPath: fileUrl.path) {
                if let attrs = try? fm.attributesOfItem(atPath: fileUrl.path),
                   let size = attrs[.size] as? Int64, size > 10000 {
                    return fileUrl
                }
            }
        }
        return nil
    }

    public func savedThumbURL(for id: String) -> URL? {
        guard !id.isEmpty else { return nil }
        let fileUrl = thumbDir.appendingPathComponent("\(id).jpg")
        if FileManager.default.fileExists(atPath: fileUrl.path) {
            return fileUrl
        }
        return nil
    }

    public func saveTrackAsync(_ item: PlaylistItem) async {
        guard !item.id.isEmpty else { return }
        if isSaved(id: item.id) { return }

        downloadingTrackIds.insert(item.id)
        defer { downloadingTrackIds.remove(item.id) }

        ensureDirectoriesExist()

        do {
            // 1. Resolve audio stream with fast fallback
            let resolvedStreamUrl: URL
            var resolvedTitle = item.title
            var resolvedAuthor = item.author
            var resolvedDuration = item.durationSeconds
            var resolvedThumb = item.thumbnailUrl

            if let existingStream = item.streamUrl, let u = URL(string: existingStream) {
                resolvedStreamUrl = u
            } else {
                let resolved = try await YouTubeResolver.shared.resolveAudioStream(for: item.id)
                resolvedStreamUrl = resolved.streamUrl
                resolvedTitle = resolved.title
                resolvedAuthor = resolved.author
                resolvedDuration = resolved.durationSeconds
                resolvedThumb = resolved.thumbnailUrl
            }

            // 2. Prepare optimized requests with matching headers to bypass CDN speed throttling
            let targetAudioFile = audioDir.appendingPathComponent("\(item.id).m4a")
            var req = URLRequest(url: resolvedStreamUrl)
            req.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", forHTTPHeaderField: "User-Agent")
            req.setValue("https://www.youtube.com/", forHTTPHeaderField: "Referer")
            let finalAudioRequest = req
            let finalThumbUrl = resolvedThumb

            let targetThumbFile = thumbDir.appendingPathComponent("\(item.id).jpg")

            // 3. Parallel download: Download Audio and Thumbnail at the same time for max speed
            async let audioTask: (URL, URLResponse) = downloadSession.download(for: finalAudioRequest)
            async let thumbTask: (URL, URLResponse)? = {
                if let thumbUrl = URL(string: finalThumbUrl) {
                    return try? await downloadSession.download(from: thumbUrl)
                }
                return nil
            }()

            let ((tempAudioUrl, _), thumbResult) = try await (audioTask, thumbTask)

            // Save audio file
            if FileManager.default.fileExists(atPath: targetAudioFile.path) {
                try? FileManager.default.removeItem(at: targetAudioFile)
            }
            try FileManager.default.moveItem(at: tempAudioUrl, to: targetAudioFile)

            // Save thumbnail file
            if let (tempThumbUrl, _) = thumbResult {
                if FileManager.default.fileExists(atPath: targetThumbFile.path) {
                    try? FileManager.default.removeItem(at: targetThumbFile)
                }
                try? FileManager.default.moveItem(at: tempThumbUrl, to: targetThumbFile)
            }

            // 4. Update savedTracks list
            var savedItem = item
            savedItem.title = resolvedTitle
            savedItem.author = resolvedAuthor
            savedItem.durationSeconds = resolvedDuration
            savedItem.thumbnailUrl = resolvedThumb
            savedItem.streamUrl = targetAudioFile.absoluteString

            savedTracks.removeAll(where: { $0.id == item.id })
            savedTracks.insert(savedItem, at: 0)
            saveToFile()

            ErrorLogService.shared.log("Tải thành công", "Đã lưu \"\(savedItem.title)\" vào mục Đã tải (Offline).", category: "Tải nhạc", isError: false)

        } catch {
            print("[SavedTracksService] Error saving track \(item.id): \(error)")
            downloadErrorMessage = "Lỗi khi lưu bài: \(error.localizedDescription)"
            ErrorLogService.shared.log("Lỗi tải bài", "\(item.title): \(error.localizedDescription)", category: "Tải nhạc")
        }
    }

    public func deleteSavedTrack(id: String) {
        guard !id.isEmpty else { return }

        // Remove files
        if let audioUrl = savedAudioURL(for: id) {
            try? FileManager.default.removeItem(at: audioUrl)
        }
        if let thumbUrl = savedThumbURL(for: id) {
            try? FileManager.default.removeItem(at: thumbUrl)
        }

        savedTracks.removeAll(where: { $0.id == id })
        saveToFile()
    }

    private func loadFromFile() {
        guard FileManager.default.fileExists(atPath: jsonFile.path) else { return }
        do {
            let data = try Data(contentsOf: jsonFile)
            let decoded = try JSONDecoder().decode([PlaylistItem].self, from: data)
            // Filter to only items whose audio files actually exist
            self.savedTracks = decoded.filter { isSaved(id: $0.id) }
        } catch {
            print("[SavedTracksService] Load error: \(error)")
            ErrorLogService.shared.log("Lỗi đọc danh sách tải", error.localizedDescription, category: "Hệ thống")
        }
    }

    private func saveToFile() {
        do {
            let data = try JSONEncoder().encode(savedTracks)
            try data.write(to: jsonFile, options: .atomic)
        } catch {
            print("[SavedTracksService] Save error: \(error)")
            ErrorLogService.shared.log("Lỗi ghi danh sách tải", error.localizedDescription, category: "Hệ thống")
        }
    }
}
