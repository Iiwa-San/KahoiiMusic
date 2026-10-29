import Foundation

public struct ResolvedAudioStream: Sendable {
    public let streamUrl: URL
    public let title: String
    public let author: String
    public let durationSeconds: Int
    public let thumbnailUrl: String
}

public actor YouTubeResolver {
    public static let shared = YouTubeResolver()

    // Public Invidious instances with automatic fallback
    private let invidiousInstances: [String] = [
        "https://inv.nadeko.net",
        "https://invidious.nerdvpn.de",
        "https://yewtu.be",
        "https://invidious.jing.rocks",
        "https://invidious.drgns.space",
        "https://iv.melmac.space"
    ]

    // Piped fallback instances
    private let pipedInstances: [String] = [
        "https://pipedapi.kavin.rocks",
        "https://api.piped.privacy.com.de",
        "https://pipedapi.tokhmi.xyz"
    ]

    private let urlSession: URLSession

    public init() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 10.0
        config.timeoutIntervalForResource = 20.0
        self.urlSession = URLSession(configuration: config)
    }

    // MARK: - ID & URL Extraction

    public nonisolated func extractVideoId(from input: String) -> String? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return nil }

        // Raw 11-char ID
        let idRegex = try? NSRegularExpression(pattern: "^[a-zA-Z0-9_-]{11}$")
        if let match = idRegex?.firstMatch(in: trimmed, range: NSRange(location: 0, length: trimmed.utf16.count)) {
            return (trimmed as NSString).substring(with: match.range)
        }

        // Standard YouTube URL formats
        let patterns = [
            #"(?:v=|\/v\/|embed\/|youtu\.be\/|\/shorts\/)([a-zA-Z0-9_-]{11})"#,
            #"[?&]v=([a-zA-Z0-9_-]{11})"#
        ]

        for pattern in patterns {
            if let regex = try? NSRegularExpression(pattern: pattern, options: .caseInsensitive),
               let match = regex.firstMatch(in: trimmed, range: NSRange(location: 0, length: trimmed.utf16.count)),
               match.numberOfRanges > 1 {
                return (trimmed as NSString).substring(with: match.range(at: 1))
            }
        }

        return nil
    }

    public nonisolated func extractPlaylistId(from input: String) -> String? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        let patterns = [
            #"[?&]list=([a-zA-Z0-9_-]+)"#
        ]
        for pattern in patterns {
            if let regex = try? NSRegularExpression(pattern: pattern, options: .caseInsensitive),
               let match = regex.firstMatch(in: trimmed, range: NSRange(location: 0, length: trimmed.utf16.count)),
               match.numberOfRanges > 1 {
                return (trimmed as NSString).substring(with: match.range(at: 1))
            }
        }
        return nil
    }

    public nonisolated func isUrl(_ input: String) -> Bool {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return trimmed.hasPrefix("http://") || trimmed.hasPrefix("https://") || trimmed.contains("youtu.be") || trimmed.contains("youtube.com")
    }

    // MARK: - Resolve Audio Stream

    public func resolveAudioStream(for videoId: String) async throws -> ResolvedAudioStream {
        // 1. Try Invidious instances
        for host in invidiousInstances {
            if let resolved = try? await resolveFromInvidious(instance: host, videoId: videoId) {
                return resolved
            }
        }

        // 2. Try Piped instances
        for host in pipedInstances {
            if let resolved = try? await resolveFromPiped(instance: host, videoId: videoId) {
                return resolved
            }
        }

        // 3. Fallback: Direct audio fallback url
        let fallbackUrl = URL(string: "https://www.youtube.com/watch?v=\(videoId)")!
        return ResolvedAudioStream(
            streamUrl: fallbackUrl,
            title: "YouTube Video",
            author: "Unknown Artist",
            durationSeconds: 0,
            thumbnailUrl: "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
        )
    }

    private func resolveFromInvidious(instance: String, videoId: String) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "\(instance)/api/v1/videos/\(videoId)?fields=title,author,lengthSeconds,videoThumbnails,adaptiveFormats,formatStreams") else {
            throw URLError(.badURL)
        }

        var request = URLRequest(url: url)
        request.setValue("KahoiiMusic/1.0 (iOS)", forHTTPHeaderField: "User-Agent")

        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw URLError(.cannotParseResponse)
        }

        let title = json["title"] as? String ?? "Unknown Title"
        let author = json["author"] as? String ?? "Unknown Artist"
        let duration = json["lengthSeconds"] as? Int ?? 0

        // Find best audio URL
        var bestAudioUrlString: String? = nil

        if let adaptiveFormats = json["adaptiveFormats"] as? [[String: Any]] {
            // Prioritize audio/mp4 (AAC / m4a) because iOS AVPlayer natively hardware decodes it
            let audioFormats = adaptiveFormats.filter {
                let type = $0["type"] as? String ?? ""
                return type.contains("audio/mp4") || type.contains("audio/m4a")
            }

            if let topAudio = audioFormats.first, let urlStr = topAudio["url"] as? String {
                bestAudioUrlString = urlStr
            } else {
                // Any audio
                let anyAudio = adaptiveFormats.filter { ($0["type"] as? String ?? "").contains("audio") }
                if let topAny = anyAudio.first, let urlStr = topAny["url"] as? String {
                    bestAudioUrlString = urlStr
                }
            }
        }

        // Fallback to formatStreams (progressive MP4)
        if bestAudioUrlString == nil, let formatStreams = json["formatStreams"] as? [[String: Any]] {
            if let firstStream = formatStreams.first, let urlStr = firstStream["url"] as? String {
                bestAudioUrlString = urlStr
            }
        }

        guard let streamStr = bestAudioUrlString, let streamUrl = URL(string: streamStr) else {
            throw URLError(.resourceUnavailable)
        }

        // Find best thumbnail
        var thumb = "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
        if let thumbs = json["videoThumbnails"] as? [[String: Any]], let firstThumb = thumbs.first?["url"] as? String {
            thumb = firstThumb
        }

        return ResolvedAudioStream(
            streamUrl: streamUrl,
            title: title,
            author: author,
            durationSeconds: duration,
            thumbnailUrl: thumb
        )
    }

    private func resolveFromPiped(instance: String, videoId: String) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "\(instance)/streams/\(videoId)") else {
            throw URLError(.badURL)
        }

        var request = URLRequest(url: url)
        request.setValue("KahoiiMusic/1.0 (iOS)", forHTTPHeaderField: "User-Agent")

        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw URLError(.cannotParseResponse)
        }

        let title = json["title"] as? String ?? "Unknown Title"
        let author = json["uploader"] as? String ?? "Unknown Artist"
        let duration = json["duration"] as? Int ?? 0
        let thumbnail = json["thumbnailUrl"] as? String ?? "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"

        guard let audioStreams = json["audioStreams"] as? [[String: Any]] else {
            throw URLError(.resourceUnavailable)
        }

        // Pick m4a or highest bitrate audio stream
        let m4aStream = audioStreams.first { ($0["format"] as? String)?.lowercased() == "m4a" } ?? audioStreams.first
        guard let streamStr = m4aStream?["url"] as? String, let streamUrl = URL(string: streamStr) else {
            throw URLError(.resourceUnavailable)
        }

        return ResolvedAudioStream(
            streamUrl: streamUrl,
            title: title,
            author: author,
            durationSeconds: duration,
            thumbnailUrl: thumbnail
        )
    }

    // MARK: - Search Query

    public func search(query: String) async throws -> [PlaylistItem] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return [] }

        guard let encoded = trimmed.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) else {
            return []
        }

        for host in invidiousInstances {
            guard let url = URL(string: "\(host)/api/v1/search?q=\(encoded)&type=video") else { continue }
            var request = URLRequest(url: url)
            request.setValue("KahoiiMusic/1.0 (iOS)", forHTTPHeaderField: "User-Agent")

            do {
                let (data, response) = try await urlSession.data(for: request)
                guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
                    continue
                }

                guard let array = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] else {
                    continue
                }

                var items: [PlaylistItem] = []
                for item in array {
                    guard let videoId = item["videoId"] as? String else { continue }
                    let title = item["title"] as? String ?? "Unknown"
                    let author = item["author"] as? String ?? ""
                    let duration = item["lengthSeconds"] as? Int ?? 0

                    var thumb = "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
                    if let thumbs = item["videoThumbnails"] as? [[String: Any]],
                       let first = thumbs.first?["url"] as? String {
                        thumb = first
                    }

                    items.append(PlaylistItem(
                        id: videoId,
                        title: title,
                        author: author,
                        durationSeconds: duration,
                        thumbnailUrl: thumb
                    ))
                }

                if !items.isEmpty {
                    return items
                }
            } catch {
                continue
            }
        }

        return []
    }

    // MARK: - Playlist Loading

    public func loadPlaylist(playlistId: String) async throws -> (title: String, items: [PlaylistItem]) {
        for host in invidiousInstances {
            guard let url = URL(string: "\(host)/api/v1/playlists/\(playlistId)") else { continue }
            var request = URLRequest(url: url)
            request.setValue("KahoiiMusic/1.0 (iOS)", forHTTPHeaderField: "User-Agent")

            do {
                let (data, response) = try await urlSession.data(for: request)
                guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
                    continue
                }

                guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                    continue
                }

                let playlistTitle = json["title"] as? String ?? "Danh sách phát"
                guard let videos = json["videos"] as? [[String: Any]] else {
                    continue
                }

                var items: [PlaylistItem] = []
                for vid in videos {
                    guard let videoId = vid["videoId"] as? String else { continue }
                    let title = vid["title"] as? String ?? "Unknown"
                    let author = vid["author"] as? String ?? ""
                    let duration = vid["lengthSeconds"] as? Int ?? 0

                    var thumb = "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
                    if let thumbs = vid["videoThumbnails"] as? [[String: Any]],
                       let first = thumbs.first?["url"] as? String {
                        thumb = first
                    }

                    items.append(PlaylistItem(
                        id: videoId,
                        title: title,
                        author: author,
                        durationSeconds: duration,
                        thumbnailUrl: thumb
                    ))
                }

                if !items.isEmpty {
                    return (playlistTitle, items)
                }
            } catch {
                continue
            }
        }

        throw URLError(.badServerResponse)
    }
}

