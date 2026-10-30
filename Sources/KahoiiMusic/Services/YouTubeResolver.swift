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
    private var cachedVisitorData: String?
    private var visitorDataFetchTime: Date?
    private var resolvedStreamCache: [String: (stream: ResolvedAudioStream, cachedAt: Date)] = [:]

    public init() {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 25.0
        config.timeoutIntervalForResource = 60.0 // 1 phút timeout
        self.urlSession = URLSession(configuration: config)

        // Pre-warm visitor data in background on init so the first user tap is instant
        Task { [weak self] in
            _ = await self?.fetchVisitorData(for: "dQw4w9WgXcQ")
        }
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

    // MARK: - Visitor Data Token Fetcher

    private func fetchVisitorData(for videoId: String) async -> String? {
        if let cached = cachedVisitorData,
           let fetchTime = visitorDataFetchTime,
           Date().timeIntervalSince(fetchTime) < 86400 { // Cache 24 hours
            return cached
        }

        guard let url = URL(string: "https://www.youtube.com/watch?v=\(videoId)") else {
            return cachedVisitorData
        }

        var request = URLRequest(url: url)
        request.timeoutInterval = 8.0 // Quick 8s timeout to avoid stalling playback
        request.setValue("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36", forHTTPHeaderField: "User-Agent")
        request.setValue("en-US,en;q=0.9", forHTTPHeaderField: "Accept-Language")

        do {
            let (data, response) = try await urlSession.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200,
                  let html = String(data: data, encoding: .utf8) else {
                return cachedVisitorData
            }

            let patterns = [
                #""(?:VISITOR_DATA|visitorData)":"([^"]+)""#,
                #"ytcfg\.set\(\{"VISITOR_DATA":"([^"]+)""#
            ]

            for pattern in patterns {
                if let regex = try? NSRegularExpression(pattern: pattern),
                   let match = regex.firstMatch(in: html, range: NSRange(location: 0, length: html.utf16.count)),
                   match.numberOfRanges > 1 {
                    let token = (html as NSString).substring(with: match.range(at: 1))
                    if !token.isEmpty {
                        self.cachedVisitorData = token
                        self.visitorDataFetchTime = Date()
                        return token
                    }
                }
            }
        } catch {
            print("[YouTubeResolver] fetchVisitorData failed: \(error)")
        }

        return cachedVisitorData
    }

    // MARK: - Resolve Audio Stream

    public func resolveAudioStream(for videoId: String) async throws -> ResolvedAudioStream {
        // 0. Check in-memory cache first (valid for ~6 hours; cached for 2 hours)
        if let cached = resolvedStreamCache[videoId],
           Date().timeIntervalSince(cached.cachedAt) < 7200 {
            return cached.stream
        }

        // Fetch or get cached visitor token
        let visitorData = await fetchVisitorData(for: videoId)

        // 1. Primary: YouTube InnerTube API with VISIONOS client
        do {
            let res = try await resolveFromYouTubeVisionOS(videoId: videoId, visitorData: visitorData)
            resolvedStreamCache[videoId] = (stream: res, cachedAt: Date())
            return res
        } catch {
            print("[YouTubeResolver] VisionOS resolution failed: \(error)")
        }

        // 2. Secondary: YouTube InnerTube API with ANDROID_VR client
        do {
            let res = try await resolveFromYouTubeAndroidVR(videoId: videoId, visitorData: visitorData)
            resolvedStreamCache[videoId] = (stream: res, cachedAt: Date())
            return res
        } catch {
            print("[YouTubeResolver] AndroidVR resolution failed: \(error)")
        }

        // 3. Re-fetch fresh visitorData if stale and retry VisionOS once
        self.cachedVisitorData = nil
        let freshVisitorData = await fetchVisitorData(for: videoId)
        if freshVisitorData != visitorData {
            do {
                let res = try await resolveFromYouTubeVisionOS(videoId: videoId, visitorData: freshVisitorData)
                resolvedStreamCache[videoId] = (stream: res, cachedAt: Date())
                return res
            } catch {
                print("[YouTubeResolver] Retry with fresh visitorData failed: \(error)")
            }
        }

        throw NSError(
            domain: "YouTubeResolver",
            code: 404,
            userInfo: [NSLocalizedDescriptionKey: "Không thể trích xuất luồng âm thanh từ video này."]
        )
    }

    public func invalidateCache(for videoId: String) {
        resolvedStreamCache.removeValue(forKey: videoId)
    }

    // MARK: - YouTube Official Innertube (VisionOS)

    private func resolveFromYouTubeVisionOS(videoId: String, visitorData: String?) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "https://www.youtube.com/youtubei/v1/player?prettyPrint=false") else {
            throw URLError(.badURL)
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", forHTTPHeaderField: "User-Agent")
        request.setValue("101", forHTTPHeaderField: "X-Youtube-Client-Name")
        request.setValue("1.02", forHTTPHeaderField: "X-Youtube-Client-Version")
        request.setValue("https://www.youtube.com", forHTTPHeaderField: "Origin")
        request.setValue("PREF=hl=vi&tz=UTC", forHTTPHeaderField: "Cookie")

        if let token = visitorData, !token.isEmpty {
            request.setValue(token, forHTTPHeaderField: "X-Goog-Visitor-Id")
        }

        var clientObj: [String: Any] = [
            "clientName": "VISIONOS",
            "clientVersion": "1.02",
            "deviceMake": "Apple",
            "deviceModel": "RealityDevice17,1",
            "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
            "osName": "visionOS",
            "osVersion": "26.5.23O471",
            "hl": "vi",
            "timeZone": "UTC",
            "utcOffsetMinutes": 0
        ]
        if let token = visitorData, !token.isEmpty {
            clientObj["visitorData"] = token
        }

        let payload: [String: Any] = [
            "context": [
                "client": clientObj
            ],
            "videoId": videoId,
            "playbackContext": [
                "contentPlaybackContext": [
                    "html5Preference": "HTML5_PREF_WANTS",
                    "signatureTimestamp": 20752
                ]
            ],
            "contentCheckOk": true,
            "racyCheckOk": true
        ]

        request.httpBody = try JSONSerialization.data(withJSONObject: payload)

        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw URLError(.cannotParseResponse)
        }

        // Check playability
        if let playability = json["playabilityStatus"] as? [String: Any] {
            let status = playability["status"] as? String ?? ""
            if status != "OK" {
                let reason = playability["reason"] as? String ?? "Video không khả dụng (\(status))"
                throw NSError(domain: "YouTubeResolver", code: 403, userInfo: [NSLocalizedDescriptionKey: reason])
            }
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

        guard let streamingData = json["streamingData"] as? [String: Any] else {
            throw URLError(.resourceUnavailable)
        }

        var bestAudioUrl: URL? = nil
        var highestBitrate: Int = 0

        if let adaptive = streamingData["adaptiveFormats"] as? [[String: Any]] {
            // First pass: Prioritize hardware-friendly audio/mp4 (AAC m4a / itag 140) with highest bitrate
            for format in adaptive {
                let mime = format["mimeType"] as? String ?? ""
                let bitrate = format["bitrate"] as? Int ?? 0
                if (mime.contains("audio/mp4") || mime.contains("m4a")),
                   let urlStr = format["url"] as? String,
                   let u = URL(string: urlStr),
                   bitrate > highestBitrate {
                    highestBitrate = bitrate
                    bestAudioUrl = u
                }
            }

            // Second pass: Any other audio stream if no mp4 found
            if bestAudioUrl == nil {
                for format in adaptive {
                    let mime = format["mimeType"] as? String ?? ""
                    let bitrate = format["bitrate"] as? Int ?? 0
                    if mime.contains("audio"),
                       let urlStr = format["url"] as? String,
                       let u = URL(string: urlStr),
                       bitrate > highestBitrate {
                        highestBitrate = bitrate
                        bestAudioUrl = u
                    }
                }
            }
        }

        // Fallback to HLS Manifest
        if bestAudioUrl == nil,
           let hlsStr = streamingData["hlsManifestUrl"] as? String,
           let hlsUrl = URL(string: hlsStr) {
            bestAudioUrl = hlsUrl
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

    // MARK: - YouTube Official Innertube (Android VR Fallback)

    private func resolveFromYouTubeAndroidVR(videoId: String, visitorData: String?) async throws -> ResolvedAudioStream {
        guard let url = URL(string: "https://www.youtube.com/youtubei/v1/player?prettyPrint=false") else {
            throw URLError(.badURL)
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Mozilla/5.0 (Linux; Android 12; Quest 3) AppleWebKit/537.36 (KHTML, like Gecko) OculusBrowser/32.0.0.12.39", forHTTPHeaderField: "User-Agent")

        var clientObj: [String: Any] = [
            "clientName": "ANDROID_VR",
            "clientVersion": "1.61.48",
            "deviceMake": "Oculus",
            "deviceModel": "Quest 3",
            "osName": "Android",
            "osVersion": "12",
            "hl": "vi",
            "gl": "VN"
        ]
        if let token = visitorData, !token.isEmpty {
            clientObj["visitorData"] = token
            request.setValue(token, forHTTPHeaderField: "X-Goog-Visitor-Id")
        }

        let payload: [String: Any] = [
            "videoId": videoId,
            "context": [
                "client": clientObj
            ],
            "contentCheckOk": true,
            "racyCheckOk": true
        ]

        request.httpBody = try JSONSerialization.data(withJSONObject: payload)

        let (data, response) = try await urlSession.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
            throw URLError(.badServerResponse)
        }

        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw URLError(.cannotParseResponse)
        }

        if let playability = json["playabilityStatus"] as? [String: Any] {
            let status = playability["status"] as? String ?? ""
            if status != "OK" {
                let reason = playability["reason"] as? String ?? "Video không khả dụng (\(status))"
                throw NSError(domain: "YouTubeResolver", code: 403, userInfo: [NSLocalizedDescriptionKey: reason])
            }
        }

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

        guard let streamingData = json["streamingData"] as? [String: Any] else {
            throw URLError(.resourceUnavailable)
        }

        var bestAudioUrl: URL? = nil
        var highestBitrate: Int = 0

        if let adaptive = streamingData["adaptiveFormats"] as? [[String: Any]] {
            for format in adaptive {
                let mime = format["mimeType"] as? String ?? ""
                let bitrate = format["bitrate"] as? Int ?? 0
                if (mime.contains("audio/mp4") || mime.contains("m4a")),
                   let urlStr = format["url"] as? String,
                   let u = URL(string: urlStr),
                   bitrate > highestBitrate {
                    highestBitrate = bitrate
                    bestAudioUrl = u
                }
            }
            if bestAudioUrl == nil {
                for format in adaptive {
                    let mime = format["mimeType"] as? String ?? ""
                    let bitrate = format["bitrate"] as? Int ?? 0
                    if mime.contains("audio"),
                       let urlStr = format["url"] as? String,
                       let u = URL(string: urlStr),
                       bitrate > highestBitrate {
                        highestBitrate = bitrate
                        bestAudioUrl = u
                    }
                }
            }
        }

        if bestAudioUrl == nil,
           let hlsStr = streamingData["hlsManifestUrl"] as? String,
           let hlsUrl = URL(string: hlsStr) {
            bestAudioUrl = hlsUrl
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

    // MARK: - Search Query (Direct YouTube Official Search)

    public func search(query: String) async throws -> [PlaylistItem] {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return [] }

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

                    var title = "YouTube Video"
                    if let titleObj = videoRenderer["title"] as? [String: Any],
                       let runs = titleObj["runs"] as? [[String: Any]],
                       let firstRun = runs.first,
                       let t = firstRun["text"] as? String {
                        title = t
                    }

                    var author = ""
                    if let owner = videoRenderer["ownerText"] as? [String: Any],
                       let runs = owner["runs"] as? [[String: Any]],
                       let firstRun = runs.first,
                       let a = firstRun["text"] as? String {
                        author = a
                    }

                    var durationSec = 0
                    if let lengthObj = videoRenderer["lengthText"] as? [String: Any],
                       let simpleText = lengthObj["simpleText"] as? String {
                        durationSec = parseDurationToSeconds(simpleText)
                    }

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
        return ("Danh sách phát", [])
    }
}
