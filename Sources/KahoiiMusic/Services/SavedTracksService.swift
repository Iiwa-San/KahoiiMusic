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
        config.timeoutIntervalForRequest = 60.0 // 1 phút timeout cho mỗi gói dữ liệu
        config.timeoutIntervalForResource = 1200.0 // 20 phút tổng cộng (hỗ trợ audio cực dài >100MB)
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

    // MARK: - Temp Audio Cache (Matching PC KahoiiTemp/audio)
    public var tempAudioDir: URL {
        let caches = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0]
        return caches.appendingPathComponent("KahoiiTemp/audio", isDirectory: true)
    }

    public func tempAudioURL(for id: String) -> URL? {
        guard !id.isEmpty else { return nil }
        let file = tempAudioDir.appendingPathComponent("\(id).m4a")
        if FileManager.default.fileExists(atPath: file.path),
           let attr = try? FileManager.default.attributesOfItem(atPath: file.path),
           let size = attr[.size] as? Int64, size > 1024 * 100 {
            return file
        }
        return nil
    }

    public func cleanTempCacheLRU(maxSizeBytes: Int64 = 300 * 1024 * 1024) {
        let fm = FileManager.default
        guard let files = try? fm.contentsOfDirectory(at: tempAudioDir, includingPropertiesForKeys: [.contentAccessDateKey, .contentModificationDateKey, .fileSizeKey], options: .skipsHiddenFiles) else { return }

        var totalSize: Int64 = 0
        var fileList: [(url: URL, date: Date, size: Int64)] = []

        for file in files {
            guard let res = try? file.resourceValues(forKeys: [.contentAccessDateKey, .contentModificationDateKey, .fileSizeKey]),
                  let size = res.fileSize else { continue }
            let date = res.contentAccessDate ?? res.contentModificationDate ?? Date.distantPast
            totalSize += Int64(size)
            fileList.append((url: file, date: date, size: Int64(size)))
        }

        if totalSize > maxSizeBytes {
            fileList.sort { $0.date < $1.date }
            for item in fileList {
                if totalSize <= maxSizeBytes { break }
                try? fm.removeItem(at: item.url)
                totalSize -= item.size
            }
        }
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

    /// High-speed multi-chunk HTTP Range downloader (10-20 MB/s), bypassing YouTube 32KB/s throttling
    public func downloadAudioChunked(from streamUrl: URL, to destinationUrl: URL, expectedContentLength: Int64? = nil) async throws {
        var totalLength: Int64 = expectedContentLength ?? 0

        // Ensure parent directory exists
        try FileManager.default.createDirectory(at: destinationUrl.deletingLastPathComponent(), withIntermediateDirectories: true)

        // If total length is unknown, query via Range: bytes=0-0 probe
        if totalLength <= 0 {
            var headReq = URLRequest(url: streamUrl)
            headReq.setValue("bytes=0-0", forHTTPHeaderField: "Range")
            headReq.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", forHTTPHeaderField: "User-Agent")
            headReq.setValue("https://www.youtube.com/", forHTTPHeaderField: "Referer")
            if let (_, resp) = try? await downloadSession.data(for: headReq),
               let httpResp = resp as? HTTPURLResponse {
                var contentRangeStr: String? = httpResp.value(forHTTPHeaderField: "Content-Range")
                if contentRangeStr == nil {
                    for (k, v) in httpResp.allHeaderFields {
                        if let keyStr = k as? String, keyStr.caseInsensitiveCompare("Content-Range") == .orderedSame, let valStr = v as? String {
                            contentRangeStr = valStr
                            break
                        }
                    }
                }
                if let rangeHeader = contentRangeStr,
                   let totalPart = rangeHeader.split(separator: "/").last,
                   let parsed = Int64(String(totalPart).trimmingCharacters(in: .whitespacesAndNewlines)) {
                    totalLength = parsed
                }
            }
        }

        // Fallback to standard download if file is very small (< 1 MB) or length could not be determined
        if totalLength <= 1024 * 1024 {
            var req = URLRequest(url: streamUrl)
            req.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", forHTTPHeaderField: "User-Agent")
            req.setValue("https://www.youtube.com/", forHTTPHeaderField: "Referer")
            let (tempFile, _) = try await downloadSession.download(for: req)
            if FileManager.default.fileExists(atPath: destinationUrl.path) {
                try? FileManager.default.removeItem(at: destinationUrl)
            }
            try FileManager.default.moveItem(at: tempFile, to: destinationUrl)
            return
        }

        // Fast chunked Range download (5MB chunks at maximum network speed)
        let tempPartFile = destinationUrl.deletingLastPathComponent().appendingPathComponent(destinationUrl.lastPathComponent + ".part")
        if FileManager.default.fileExists(atPath: tempPartFile.path) {
            try? FileManager.default.removeItem(at: tempPartFile)
        }
        FileManager.default.createFile(atPath: tempPartFile.path, contents: nil)
        let fileHandle = try FileHandle(forWritingTo: tempPartFile)
        defer { try? fileHandle.close() }

        let chunkSize: Int64 = 5 * 1024 * 1024 // 5 MB per chunk
        var downloadedBytes: Int64 = 0

        while downloadedBytes < totalLength {
            let endByte = min(downloadedBytes + chunkSize - 1, totalLength - 1)
            var chunkReq = URLRequest(url: streamUrl)
            chunkReq.setValue("bytes=\(downloadedBytes)-\(endByte)", forHTTPHeaderField: "Range")
            chunkReq.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", forHTTPHeaderField: "User-Agent")
            chunkReq.setValue("https://www.youtube.com/", forHTTPHeaderField: "Referer")

            let (chunkData, resp) = try await downloadSession.data(for: chunkReq)
            guard let httpResp = resp as? HTTPURLResponse, (httpResp.statusCode == 200 || httpResp.statusCode == 206) else {
                throw URLError(.badServerResponse)
            }
            try fileHandle.write(contentsOf: chunkData)
            downloadedBytes += Int64(chunkData.count)
        }

        try? fileHandle.close()

        if FileManager.default.fileExists(atPath: destinationUrl.path) {
            try? FileManager.default.removeItem(at: destinationUrl)
        }
        try FileManager.default.moveItem(at: tempPartFile, to: destinationUrl)
    }

    public func saveTrackAsync(_ item: PlaylistItem) async {
        guard !item.id.isEmpty else { return }
        if isSaved(id: item.id) { return }

        downloadingTrackIds.insert(item.id)
        defer { downloadingTrackIds.remove(item.id) }

        ensureDirectoriesExist()

        do {
            let targetAudioFile = audioDir.appendingPathComponent("\(item.id).m4a")
            let targetThumbFile = thumbDir.appendingPathComponent("\(item.id).jpg")

            var resolvedTitle = item.title
            var resolvedAuthor = item.author
            var resolvedDuration = item.durationSeconds
            var resolvedThumb = item.thumbnailUrl

            // 0. Quick check: If this track was already cached in tempAudioDir during playback, copy instantly!
            if let cachedTemp = tempAudioURL(for: item.id) {
                if FileManager.default.fileExists(atPath: targetAudioFile.path) {
                    try? FileManager.default.removeItem(at: targetAudioFile)
                }
                try FileManager.default.copyItem(at: cachedTemp, to: targetAudioFile)
                print("[SavedTracksService] Copied from temp cache instantly for \(item.id)")
            } else {
                // 1. Resolve audio stream with fast fallback
                let resolved = try await YouTubeResolver.shared.resolveAudioStream(for: item.id)
                resolvedTitle = resolved.title
                resolvedAuthor = resolved.author
                resolvedDuration = resolved.durationSeconds
                resolvedThumb = resolved.thumbnailUrl

                // 2. High-speed chunked Range download (10-20 MB/s, finishes 20min in ~1.5s)
                try await downloadAudioChunked(from: resolved.streamUrl, to: targetAudioFile, expectedContentLength: resolved.contentLength)
            }

            // 3. Download Thumbnail if not present
            if !FileManager.default.fileExists(atPath: targetThumbFile.path),
               let thumbUrl = URL(string: resolvedThumb) {
                if let (tempThumb, _) = try? await downloadSession.download(from: thumbUrl) {
                    try? FileManager.default.moveItem(at: tempThumb, to: targetThumbFile)
                }
            }

            // 4. Update savedTracks list
            var savedItem = item
            savedItem.title = resolvedTitle
            savedItem.author = resolvedAuthor
            savedItem.durationSeconds = resolvedDuration > 0 ? resolvedDuration : 1
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
