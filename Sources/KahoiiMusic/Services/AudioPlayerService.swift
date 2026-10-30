import Foundation
import AVFoundation
import MediaPlayer
import Combine
import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

@MainActor
public class AudioPlayerService: ObservableObject {
    public static let shared = AudioPlayerService()

    // MARK: - Published Properties

    @Published public var currentTrack: PlaylistItem?
    @Published public var playbackState: PlaybackState = .idle
    @Published public var currentTime: Double = 0
    @Published public var duration: Double = 0
    @Published public var volume: Float = 1.0
    @Published public var loopMode: LoopMode = .off
    @Published public var showArtworkInBackground: Bool = true {
        didSet {
            UserDefaults.standard.set(showArtworkInBackground, forKey: "showArtworkInBackground")
            updateNowPlayingInfo()
        }
    }

    private var cachedArtwork: MPMediaItemArtwork? = nil
    private var cachedArtworkTrackId: String? = nil
    private var loadingTimeoutTask: Task<Void, Never>? = nil
    @Published public var isShuffled: Bool = false
    @Published public var playlist: [PlaylistItem] = []
    @Published public var currentIndex: Int = -1
    @Published public var statusMessage: String = "Sẵn sàng"
    @Published public var errorMessage: String?
    @Published public var currentPalette: GradientPalette = .default

    // MARK: - Internal Audio State

    private var avPlayer: AVPlayer?
    private var timeObserverToken: Any?
    private var playerItemCancellables = Set<AnyCancellable>()
    private var originalPlaylist: [PlaylistItem] = []
    private var cancellables = Set<AnyCancellable>()

    private init() {
        setupAudioSession()
        setupRemoteCommands()
        setupInterruptionNotifications()
        SleepTimerService.shared.onTimerExpired = { [weak self] in
            Task { @MainActor in
                self?.pause()
            }
        }
    }

    deinit {
        if let token = timeObserverToken {
            avPlayer?.removeTimeObserver(token)
        }
    }

    // MARK: - Audio Session Setup

    private func setupAudioSession() {
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.playback, mode: .default, options: [.allowAirPlay, .allowBluetoothA2DP])
            try session.setActive(true)
        } catch {
            print("[AudioPlayerService] AudioSession setup error: \(error)")
        }
    }

    // MARK: - Remote Command Center (Lock Screen / Control Center)

    private func setupRemoteCommands() {
        let commandCenter = MPRemoteCommandCenter.shared()

        commandCenter.playCommand.isEnabled = true
        commandCenter.playCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.play() }
            return .success
        }

        commandCenter.pauseCommand.isEnabled = true
        commandCenter.pauseCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.pause() }
            return .success
        }

        commandCenter.togglePlayPauseCommand.isEnabled = true
        commandCenter.togglePlayPauseCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.togglePlayPause() }
            return .success
        }

        commandCenter.nextTrackCommand.isEnabled = true
        commandCenter.nextTrackCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.nextTrack() }
            return .success
        }

        commandCenter.previousTrackCommand.isEnabled = true
        commandCenter.previousTrackCommand.addTarget { [weak self] _ in
            Task { @MainActor in self?.previousTrack() }
            return .success
        }

        commandCenter.changePlaybackPositionCommand.isEnabled = true
        commandCenter.changePlaybackPositionCommand.addTarget { [weak self] event in
            guard let event = event as? MPChangePlaybackPositionCommandEvent else { return .commandFailed }
            Task { @MainActor in self?.seek(to: event.positionTime) }
            return .success
        }

        commandCenter.skipForwardCommand.isEnabled = true
        commandCenter.skipForwardCommand.preferredIntervals = [15]
        commandCenter.skipForwardCommand.addTarget { [weak self] _ in
            Task { @MainActor in
                guard let self = self else { return }
                self.seek(to: min(self.currentTime + 15, self.duration))
            }
            return .success
        }

        commandCenter.skipBackwardCommand.isEnabled = true
        commandCenter.skipBackwardCommand.preferredIntervals = [15]
        commandCenter.skipBackwardCommand.addTarget { [weak self] _ in
            Task { @MainActor in
                guard let self = self else { return }
                self.seek(to: max(self.currentTime - 15, 0))
            }
            return .success
        }
    }

    private func updateNowPlayingInfo() {
        guard let track = currentTrack else {
            MPNowPlayingInfoCenter.default().nowPlayingInfo = nil
            cachedArtwork = nil
            cachedArtworkTrackId = nil
            return
        }

        var info: [String: Any] = [
            MPMediaItemPropertyTitle: track.title,
            MPMediaItemPropertyArtist: track.author.isEmpty ? "Kahoii Music" : track.author,
            MPNowPlayingInfoPropertyElapsedPlaybackTime: currentTime,
            MPMediaItemPropertyPlaybackDuration: duration > 0 ? duration : Double(track.durationSeconds),
            MPNowPlayingInfoPropertyPlaybackRate: playbackState.isPlaying ? 1.0 : 0.0
        ]

        // Fix background thumbnail flickering: Only load/set artwork ONCE per track, never repeatedly
        if showArtworkInBackground {
            if let cached = cachedArtwork, cachedArtworkTrackId == track.id {
                info[MPMediaItemPropertyArtwork] = cached
            } else if let artworkUrl = track.artworkURL {
                if cachedArtworkTrackId != track.id {
                    cachedArtworkTrackId = track.id
                    Task { [weak self] in
                        guard let self = self else { return }
                        var loadedImg: UIImage? = nil
                        // 1. Check local saved thumbnail first (0ms latency)
                        if let localThumb = SavedTracksService.shared.savedThumbURL(for: track.id),
                           let data = try? Data(contentsOf: localThumb),
                           let img = UIImage(data: data) {
                            loadedImg = img
                        } else if let (data, _) = try? await URLSession.shared.data(from: artworkUrl),
                                  let img = UIImage(data: data) {
                            loadedImg = img
                        }

                        if let uiImage = loadedImg {
                            let artwork = MPMediaItemArtwork(boundsSize: uiImage.size) { _ in uiImage }
                            self.cachedArtwork = artwork
                            var currentInfo = MPNowPlayingInfoCenter.default().nowPlayingInfo ?? [:]
                            currentInfo[MPMediaItemPropertyArtwork] = artwork
                            MPNowPlayingInfoCenter.default().nowPlayingInfo = currentInfo
                        }
                    }
                }
            }
        }

        MPNowPlayingInfoCenter.default().nowPlayingInfo = info
    }

    // MARK: - Audio Interruptions & Route Changes

    private func setupInterruptionNotifications() {
        NotificationCenter.default.addObserver(
            forName: AVAudioSession.interruptionNotification,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            guard let self = self,
                  let userInfo = notification.userInfo,
                  let typeValue = userInfo[AVAudioSessionInterruptionTypeKey] as? UInt,
                  let type = AVAudioSession.InterruptionType(rawValue: typeValue) else { return }

            if type == .began {
                self.pause()
            } else if type == .ended {
                if let optionsValue = userInfo[AVAudioSessionInterruptionOptionKey] as? UInt {
                    let options = AVAudioSession.InterruptionOptions(rawValue: optionsValue)
                    if options.contains(.shouldResume) {
                        self.play()
                    }
                }
            }
        }

        NotificationCenter.default.addObserver(
            forName: AVAudioSession.routeChangeNotification,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            guard let self = self,
                  let userInfo = notification.userInfo,
                  let reasonValue = userInfo[AVAudioSessionRouteChangeReasonKey] as? UInt,
                  let reason = AVAudioSession.RouteChangeReason(rawValue: reasonValue) else { return }

            if reason == .oldDeviceUnavailable {
                self.pause()
            }
        }
    }

    // MARK: - Playback Control API

    public func loadAndPlay(input: String) {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }

        // Check if playlist
        if let playlistId = YouTubeResolver.shared.extractPlaylistId(from: trimmed) {
            loadPlaylist(playlistId: playlistId)
            return
        }

        // Check if direct video URL or ID
        if let videoId = YouTubeResolver.shared.extractVideoId(from: trimmed) {
            let item = PlaylistItem(id: videoId, title: "Đang tải bài hát...")
            playTrack(item)
            return
        }

        // Otherwise perform search
        Task {
            playbackState = .loading("Đang tìm kiếm...")
            statusMessage = "Đang tìm: \(trimmed)"
            do {
                let results = try await YouTubeResolver.shared.search(query: trimmed)
                if let first = results.first {
                    playTrack(first)
                    playlist = results
                    currentIndex = 0
                } else {
                    errorMessage = "Không tìm thấy kết quả phù hợp"
                    playbackState = .idle
                }
            } catch {
                errorMessage = "Lỗi tìm kiếm: \(error.localizedDescription)"
                playbackState = .idle
            }
        }
    }

    private func startLoadingTimeout(for track: PlaylistItem) {
        loadingTimeoutTask?.cancel()
        loadingTimeoutTask = Task { @MainActor [weak self] in
            try? await Task.sleep(nanoseconds: 60_000_000_000) // 60s (1 phút) timeout cho audio dài
            guard let self = self, !Task.isCancelled else { return }
            if self.playbackState.isLoading {
                let msg = "Quá thời gian kết nối luồng phát (Timeout 1 phút). Vui lòng kiểm tra mạng hoặc chọn bài khác."
                self.errorMessage = msg
                self.playbackState = .failed("Hết thời gian tải")
                self.statusMessage = "Hết thời gian chờ"
                self.avPlayer?.pause()
                self.avPlayer?.replaceCurrentItem(with: nil)
                self.playerItemCancellables.removeAll()
                ErrorLogService.shared.log("Timeout tải nhạc", msg, category: "Phát nhạc")
            }
        }
    }

    private func cancelLoadingTimeout() {
        loadingTimeoutTask?.cancel()
        loadingTimeoutTask = nil
    }

    public func playTrack(_ track: PlaylistItem, keepQueue: Bool = false) {
        currentTrack = track
        errorMessage = nil

        // Manage playlist queue
        if keepQueue {
            if let idx = playlist.firstIndex(where: { $0.id == track.id }) {
                currentIndex = idx
            } else {
                currentIndex = -1
            }
        } else {
            if let idx = playlist.firstIndex(where: { $0.id == track.id }) {
                currentIndex = idx
            } else {
                playlist.insert(track, at: 0)
                currentIndex = 0
            }
        }

        // 1. Check Offline file first (Highest priority: Instant offline playback)
        if let localAudio = SavedTracksService.shared.savedAudioURL(for: track.id) {
            playbackState = .playing
            statusMessage = "Đang phát Offline"
            var updatedTrack = track
            updatedTrack.streamUrl = localAudio.absoluteString
            self.currentTrack = updatedTrack

            if self.currentIndex >= 0 && self.currentIndex < self.playlist.count {
                self.playlist[self.currentIndex] = updatedTrack
            }

            HistoryService.shared.recordPlay(updatedTrack)
            self.startPlayer(with: localAudio, duration: Double(track.durationSeconds))

            if let artworkUrl = track.artworkURL {
                Task {
                    let palette = await ColorExtractor.shared.extractColors(from: artworkUrl)
                    self.currentPalette = palette
                }
            }
            return
        }

        // If the exact same track is already loaded in player, simply resume instead of reloading from byte 0
        if let current = currentTrack, current.id == track.id, let player = avPlayer, player.currentItem != nil {
            if playbackState.isPaused {
                play()
                return
            } else if playbackState.isPlaying {
                return
            }
        }

        // 2. Play Online stream
        playbackState = .loading("Đang kết nối luồng phát...")
        statusMessage = "Đang kết nối: \(track.title)"
        startLoadingTimeout(for: track)

        // Update theme dynamic gradient
        if let artworkUrl = track.artworkURL {
            Task {
                let palette = await ColorExtractor.shared.extractColors(from: artworkUrl)
                self.currentPalette = palette
            }
        }

        Task {
            do {
                let resolved = try await YouTubeResolver.shared.resolveAudioStream(for: track.id)
                var updatedTrack = track
                updatedTrack.title = resolved.title
                updatedTrack.author = resolved.author
                updatedTrack.durationSeconds = resolved.durationSeconds
                updatedTrack.thumbnailUrl = resolved.thumbnailUrl
                updatedTrack.streamUrl = resolved.streamUrl.absoluteString

                self.currentTrack = updatedTrack
                if self.currentIndex >= 0 && self.currentIndex < self.playlist.count {
                    self.playlist[self.currentIndex] = updatedTrack
                }

                // Record history
                HistoryService.shared.recordPlay(updatedTrack)

                self.startPlayer(with: resolved.streamUrl, duration: Double(resolved.durationSeconds))
            } catch {
                self.cancelLoadingTimeout()
                let errorDesc = error.localizedDescription
                self.errorMessage = "Không thể phát nhạc: \(errorDesc)"
                self.playbackState = .failed(errorDesc)
                self.statusMessage = "Lỗi kết nối"
                ErrorLogService.shared.log("Lỗi kết nối nhạc", "\(track.title): \(errorDesc)", category: "YouTube")
            }
        }
    }

    private func startPlayer(with streamUrl: URL, duration: Double) {
        if let token = timeObserverToken {
            avPlayer?.removeTimeObserver(token)
            timeObserverToken = nil
        }

        playerItemCancellables.removeAll()

        // Ensure audio session is active
        do {
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            print("[AudioPlayerService] AudioSession setActive error: \(error)")
        }

        // Set stream headers to prevent 403 on CDN
        let headers: [String: String] = [
            "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
            "Referer": "https://www.youtube.com/"
        ]
        let asset = AVURLAsset(url: streamUrl, options: ["AVURLAssetHTTPHeaderFieldsKey": headers])
        let playerItem = AVPlayerItem(asset: asset)
        playerItem.preferredForwardBufferDuration = 2.0

        if avPlayer == nil {
            avPlayer = AVPlayer(playerItem: playerItem)
        } else {
            avPlayer?.replaceCurrentItem(with: playerItem)
        }

        // Critical: Start playing immediately without waiting for a massive buffer on long tracks!
        avPlayer?.automaticallyWaitsToMinimizeStalling = false

        avPlayer?.volume = volume
        self.duration = duration
        self.currentTime = 0
        self.playbackState = .loading("Đang đệm âm thanh...")
        self.statusMessage = "Đang đệm..."

        // Observe player item status
        playerItem.publisher(for: \.status)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] status in
                guard let self = self else { return }
                switch status {
                case .readyToPlay:
                    self.cancelLoadingTimeout()
                    if self.playbackState != .paused {
                        self.avPlayer?.play()
                        self.playbackState = .playing
                        self.statusMessage = "Đang phát"
                        self.updateNowPlayingInfo()
                    }
                case .failed:
                    self.cancelLoadingTimeout()
                    let err = playerItem.error?.localizedDescription ?? "Lỗi phát luồng âm thanh"
                    self.errorMessage = "Không thể phát: \(err)"
                    self.playbackState = .failed(err)
                    self.statusMessage = "Lỗi phát nhạc"
                    ErrorLogService.shared.log("Lỗi giải mã âm thanh", err, category: "AVPlayer")
                case .unknown:
                    break
                @unknown default:
                    break
                }
            }
            .store(in: &playerItemCancellables)

        // Observe isPlaybackLikelyToKeepUp (instant play as soon as initial packets arrive)
        playerItem.publisher(for: \.isPlaybackLikelyToKeepUp)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] likely in
                guard let self = self else { return }
                if likely && self.playbackState.isLoading && self.playbackState != .paused {
                    self.cancelLoadingTimeout()
                    self.avPlayer?.play()
                    self.playbackState = .playing
                    self.statusMessage = "Đang phát"
                    self.updateNowPlayingInfo()
                }
            }
            .store(in: &playerItemCancellables)

        // Observe track finished
        NotificationCenter.default.removeObserver(self, name: .AVPlayerItemDidPlayToEndTime, object: nil)
        NotificationCenter.default.addObserver(
            forName: .AVPlayerItemDidPlayToEndTime,
            object: playerItem,
            queue: .main
        ) { [weak self] _ in
            self?.handleTrackEnded()
        }

        // Periodic time observer (every 0.25 seconds for smooth scrubber)
        let interval = CMTime(seconds: 0.25, preferredTimescale: CMTimeScale(NSEC_PER_SEC))
        timeObserverToken = avPlayer?.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
            guard let self = self else { return }
            self.currentTime = time.seconds
            if self.duration <= 0, let itemDuration = self.avPlayer?.currentItem?.duration.seconds, !itemDuration.isNaN {
                self.duration = itemDuration
            }
            // Note: iOS lock screen animates elapsed time automatically via MPNowPlayingInfoCenter.
            // Do NOT call updateNowPlayingInfo() here 4x/sec as that triggers thumbnail flashing!
        }

        avPlayer?.play()
    }

    public func play() {
        guard let player = avPlayer else {
            if let track = currentTrack {
                playTrack(track)
            }
            return
        }
        do {
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {}
        player.play()
        playbackState = .playing
        statusMessage = "Đang phát"
        updateNowPlayingInfo()
    }

    public func pause() {
        avPlayer?.pause()
        playbackState = .paused
        statusMessage = "Đã tạm dừng"
        updateNowPlayingInfo()
    }

    public func togglePlayPause() {
        if playbackState.isPlaying {
            pause()
        } else {
            play()
        }
    }

    public func seek(to seconds: Double) {
        let target = max(0, min(seconds, duration))
        let cmTime = CMTime(seconds: target, preferredTimescale: 600)
        avPlayer?.seek(to: cmTime, toleranceBefore: .zero, toleranceAfter: .zero)
        currentTime = target
        updateNowPlayingInfo()
    }

    public func nextTrack() {
        guard !playlist.isEmpty, currentIndex >= 0 else {
            seek(to: 0)
            return
        }

        var nextIndex = currentIndex + 1
        if nextIndex >= playlist.count {
            if loopMode == .all {
                nextIndex = 0
            } else {
                statusMessage = "Đã hết danh sách phát"
                return
            }
        }

        currentIndex = nextIndex
        playTrack(playlist[nextIndex])
    }

    public func previousTrack() {
        if currentTime > 3.0 {
            seek(to: 0)
            return
        }

        guard !playlist.isEmpty, currentIndex >= 0 else {
            seek(to: 0)
            return
        }

        var prevIndex = currentIndex - 1
        if prevIndex < 0 {
            prevIndex = playlist.count - 1
        }

        currentIndex = prevIndex
        playTrack(playlist[prevIndex])
    }

    private func handleTrackEnded() {
        SleepTimerService.shared.handleTrackFinished()

        if currentIndex < 0 {
            switch loopMode {
            case .one:
                seek(to: 0)
                play()
            default:
                playbackState = .paused
                seek(to: 0)
                statusMessage = "Đã phát xong"
            }
            return
        }

        switch loopMode {
        case .one:
            seek(to: 0)
            play()
        case .all:
            nextTrack()
        case .off:
            if currentIndex + 1 < playlist.count {
                nextTrack()
            } else {
                playbackState = .paused
                seek(to: 0)
                statusMessage = "Đã phát xong"
            }
        }
    }

    public func cycleLoopMode() {
        switch loopMode {
        case .off: loopMode = .all
        case .all: loopMode = .one
        case .one: loopMode = .off
        }
    }

    public func toggleShuffle() {
        isShuffled.toggle()
        if isShuffled {
            originalPlaylist = playlist
            guard let current = currentTrack else {
                playlist.shuffle()
                return
            }
            var remaining = playlist.filter { $0.id != current.id }
            remaining.shuffle()
            playlist = [current] + remaining
            currentIndex = 0
        } else {
            if !originalPlaylist.isEmpty {
                playlist = originalPlaylist
                if let current = currentTrack, let idx = playlist.firstIndex(where: { $0.id == current.id }) {
                    currentIndex = idx
                }
            }
        }
    }

    public func setVolume(_ val: Float) {
        let clamped = max(0.0, min(val, 1.0))
        volume = clamped
        avPlayer?.volume = clamped
    }

    public func addToQueue(_ track: PlaylistItem) {
        playlist.append(track)
        statusMessage = "Đã thêm vào danh sách chờ"
    }

    public func removeFromQueue(at index: Int) {
        guard index >= 0 && index < playlist.count else { return }
        playlist.remove(at: index)
        if index == currentIndex {
            if currentIndex < playlist.count {
                playTrack(playlist[currentIndex])
            } else {
                nextTrack()
            }
        } else if index < currentIndex {
            currentIndex -= 1
        }
    }

    public func loadPlaylist(playlistId: String) {
        Task {
            playbackState = .loading("Đang tải danh sách phát...")
            statusMessage = "Đang nạp playlist..."
            do {
                let (title, items) = try await YouTubeResolver.shared.loadPlaylist(playlistId: playlistId)
                guard !items.isEmpty else {
                    errorMessage = "Danh sách phát trống hoặc riêng tư"
                    playbackState = .idle
                    return
                }

                self.playlist = items
                self.currentIndex = 0
                self.statusMessage = "Đã tải: \(title)"
                self.playTrack(items[0])
            } catch {
                errorMessage = "Không thể tải danh sách phát: \(error.localizedDescription)"
                playbackState = .idle
            }
        }
    }

    public func clearQueue() {
        playlist.removeAll()
        currentIndex = -1
        avPlayer?.pause()
        avPlayer = nil
        currentTrack = nil
        playbackState = .idle
        statusMessage = "Danh sách phát đã xóa"
    }
}
