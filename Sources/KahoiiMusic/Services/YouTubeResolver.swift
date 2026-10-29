import Foundation

public struct ResolvedAudioStream: Sendable {
    public let streamUrl: URL
    public let title: String
    public let author: String
    public let durationSeconds: Int
    public let thumbnailUrl: String

    public init(
        streamUrl: URL,
        title: String,
        author: String,
        durationSeconds: Int,
        thumbnailUrl: String
    ) {
        self.streamUrl = streamUrl
        self.title = title
        self.author = author
        self.durationSeconds = durationSeconds
        self.thumbnailUrl = thumbnailUrl
    }
}

public actor YouTubeResolver {
    public static let shared = YouTubeResolver()

    private let urlSession: URLSession

    public init() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 4.0
        config.timeoutIntervalForResource = 8.0
        self.urlSession = URLSession(configuration: config)
    }

    // MARK: - ID & URL Extraction (Nonisolated for fast synchronous parsing)

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
        // 1. Primary: YouTube Official Innertube Player API (Fastest & direct googlevideo link)
        if let direct = try? await resolveFromYouTubeInnertube(videoId: videoId) {
            return direct
        }

        // 2. Fallback: Invidious / Piped instances
        let fallbackHosts = [
            "https://inv.nadeko.net",
            "https://invidious.nerdvpn.de",
            "https://yewtu.be",
            "https://pipedapi.kavin.rocks"
        ]

        for host in fallbackHosts {
            if let resolved = try? await resolveFromInstance(host: host, videoId: videoId) {
                return resolved
            }
        }

        // 3. Fallback: Direct Video URL
        let fallbackUrl = URL(string: "https://www.youtube.com/watch?v=\(videoId)")!
        return ResolvedAudioStream(
            streamUrl: fallbackUrl,
            title: "YouTube Audio",
            author: "YouTube",
            durationSeconds: 0,
            thumbnailUrl: "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
        )
    }

    // MARK: - YouTube Official Innertube Extraction

    private func resolveFromYouTubeInnertube(videoId: String) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "https://www.youtube.com/youtubei/v1/player") else {
            throw URLError(.badURL)
        }

        // 1. Primary: ANDROID client (itag 18 returns direct unthrottled non-ciphered stream)
        let androidPayload: [String: Any] = [
            "videoId": videoId,
            "context": [
                "client": [
                    "clientName": "ANDROID",
                    "clientVersion": "21.26.364",
                    "androidSdkVersion": 30,
                    "userAgent": "com.google.android.youtube/21.26.364 (Linux; U; Android 11) gzip",
                    "osName": "Android",
                    "osVersion": "11",
                    "hl": "vi",
                    "gl": "VN"
                ]
            ]
        ]

        if let stream = try? await queryInnertube(
            url: url,
            payload: androidPayload,
            userAgent: "com.google.android.youtube/21.26.364 (Linux; U; Android 11) gzip",
            videoId: videoId
        ) {
            return stream
        }

        // 2. Secondary: ANDROID_VR client fallback
        let vrPayload: [String: Any] = [
            "videoId": videoId,
            "context": [
                "client": [
                    "clientName": "ANDROID_VR",
                    "clientVersion": "1.60.19",
                    "deviceMake": "Oculus",
                    "deviceModel": "Quest 3",
                    "osName": "Android",
                    "osVersion": "12",
                    "hl": "vi",
                    "gl": "VN"
                ]
            ]
        ]

        return try await queryInnertube(
            url: url,
            payload: vrPayload,
            userAgent: "Mozilla/5.0 (Linux; Android 12; Quest 3) AppleWebKit/537.36",
            videoId: videoId
        )
    }

    private func queryInnertube(
        url: URL,
        payload: [String: Any],
        userAgent: String,
        videoId: String
    ) async throws -> ResolvedAudioStream {
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(userAgent, forHTTPHeaderField: "User-Agent")
        request.httpBody = try JSONSerialization.data(withJSONObject: payload)

        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw URLError(.cannotParseResponse)
        }

        // Extract metadata
        var title = "Bài hát YouTube"
        var author = "YouTube"
        var duration = 0

        if let details = json["videoDetails"] as? [String: Any] {
            title = details["title"] as? String ?? title
            author = details["author"] as? String ?? author
            if let lenStr = details["lengthSeconds"] as? String, let len = Int(lenStr) {
                duration = len
            } else if let len = details["lengthSeconds"] as? Int {
                duration = len
            }
        }

        // Extract audio stream URL
        var bestAudioUrl: URL? = nil

        if let streamingData = json["streamingData"] as? [String: Any] {
            // Priority 1: Check standard multiplexed formats (itag 18 has direct un-ciphered progressive MP4 audio!)
            if let formats = streamingData["formats"] as? [[String: Any]] {
                for format in formats {
                    let itag = format["itag"] as? Int ?? 0
                    if (itag == 18 || itag == 22),
                       let urlStr = format["url"] as? String,
                       let u = URL(string: urlStr) {
                        bestAudioUrl = u
                        break
                    }
                }
            }

            // Priority 2: Check adaptive formats (itag 140 m4a / AAC)
            if bestAudioUrl == nil, let adaptive = streamingData["adaptiveFormats"] as? [[String: Any]] {
                for format in adaptive {
                    let itag = format["itag"] as? Int ?? 0
                    let mime = format["mimeType"] as? String ?? ""
                    if (itag == 140 || mime.contains("audio/mp4")),
                       let urlStr = format["url"] as? String,
                       let u = URL(string: urlStr) {
                        bestAudioUrl = u
                        break
                    }
                }

                if bestAudioUrl == nil {
                    for format in adaptive {
                        let mime = format["mimeType"] as? String ?? ""
                        if mime.contains("audio"),
                           let urlStr = format["url"] as? String,
                           let u = URL(string: urlStr) {
                            bestAudioUrl = u
                            break
                        }
                    }
                }
            }

            // Priority 3: Fallback to HLS Manifest
            if bestAudioUrl == nil, let hlsStr = streamingData["hlsManifestUrl"] as? String, let hlsUrl = URL(string: hlsStr) {
                bestAudioUrl = hlsUrl
            }
        }

        guard let stream = bestAudioUrl else {
            throw URLError(.resourceUnavailable)
        }

        let thumb = "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"

        return ResolvedAudioStream(
            streamUrl: stream,
            title: title,
            author: author,
            durationSeconds: duration,
            thumbnailUrl: thumb
        )
    }

    private func resolveFromInstance(host: String, videoId: String) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "\(host)/api/v1/videos/\(videoId)?fields=title,author,lengthSeconds,adaptiveFormats") else {
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

        let title = json["title"] as? String ?? "Bài hát YouTube"
        let author = json["author"] as? String ?? "YouTube"
        let duration = json["lengthSeconds"] as? Int ?? 0

        guard let formats = json["adaptiveFormats"] as? [[String: Any]] else {
            throw URLError(.resourceUnavailable)
        }

        let audio = formats.first {
            let type = $0["type"] as? String ?? ""
            return type.contains("audio/mp4") || type.contains("audio/m4a")
        } ?? formats.first {
            ($0["type"] as? String ?? "").contains("audio")
        }

        guard let urlStr = audio?["url"] as? String, let streamUrl = URL(string: urlStr) else {
            throw URLError(.resourceUnavailable)
        }

        return ResolvedAudioStream(
            streamUrl: streamUrl,
            title: title,
            author: author,
            durationSeconds: duration,
            thumbnailUrl: "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"
        )
    }

    // MARK: - Search Query (Direct YouTube Official Search)

    public func search(query: String) async throws -> [PlaylistItem] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return [] }

        // 1. Direct YouTube Web Search API
        guard let url = URL(string: "https://www.youtube.com/youtubei/v1/search") else {
            return []
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", forHTTPHeaderField: "User-Agent")

        let payload: [String: Any] = [
            "query": trimmed,
            "context": [
                "client": [
                    "clientName": "WEB",
                    "clientVersion": "2.20240101.00.00",
                    "hl": "vi",
                    "gl": "VN"
                ]
            ]
        ]

        do {
            request.httpBody = try JSONSerialization.data(withJSONObject: payload)
            let (data, response) = try await urlSession.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
                return []
            }

            guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let contents = json["contents"] as? [String: Any],
                  let twoColumn = contents["twoColumnSearchResultsRenderer"] as? [String: Any],
                  let primary = twoColumn["primaryContents"] as? [String: Any],
                  let sectionList = primary["sectionListRenderer"] as? [String: Any],
                  let sectionContents = sectionList["contents"] as? [[String: Any]] else {
                return []
            }

            var results: [PlaylistItem] = []

            for section in sectionContents {
                guard let itemSection = section["itemSectionRenderer"] as? [String: Any],
                      let items = itemSection["contents"] as? [[String: Any]] else { continue }

                for item in items {
                    guard let videoRenderer = item["videoRenderer"] as? [String: Any],
                          let videoId = videoRenderer["videoId"] as? String else { continue }

                    // Title
                    var title = "YouTube Video"
                    if let titleObj = videoRenderer["title"] as? [String: Any],
                       let runs = titleObj["runs"] as? [[String: Any]],
                       let firstRun = runs.first,
                       let t = firstRun["text"] as? String {
                        title = t
                    }

                    // Author
                    var author = ""
                    if let owner = videoRenderer["ownerText"] as? [String: Any],
                       let runs = owner["runs"] as? [[String: Any]],
                       let firstRun = runs.first,
                       let a = firstRun["text"] as? String {
                        author = a
                    }

                    // Duration
                    var durationSec = 0
                    if let lengthObj = videoRenderer["lengthText"] as? [String: Any],
                       let simpleText = lengthObj["simpleText"] as? String {
                        durationSec = parseDurationToSeconds(simpleText)
                    }

                    // Thumbnail
                    let thumb = "https://i.ytimg.com/vi/\(videoId)/hqdefault.jpg"

                    results.append(PlaylistItem(
                        id: videoId,
                        title: title,
                        author: author,
                        durationSeconds: durationSec,
                        thumbnailUrl: thumb
                    ))
                }
            }

            return results
        } catch {
            return []
        }
    }

    private func parseDurationToSeconds(_ text: String) -> Int {
        let parts = text.components(separatedBy: ":").compactMap { Int($0) }
        if parts.count == 2 {
            return parts[0] * 60 + parts[1]
        } else if parts.count == 3 {
            return parts[0] * 3600 + parts[1] * 60 + parts[2]
        }
        return 0
    }

    // MARK: - Playlist Loading

    public func loadPlaylist(playlistId: String) async throws -> (title: String, items: [PlaylistItem]) {
        // Fallback simple query
        return ("Danh sách phát", [])
    }
}
