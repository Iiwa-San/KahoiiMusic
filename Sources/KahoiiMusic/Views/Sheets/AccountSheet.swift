import SwiftUI

public struct AccountSheet: View {
    @Environment(\.dismiss) private var dismiss
    @ObservedObject private var authService = FirebaseAuthService.shared
    @ObservedObject private var syncService = FirebaseSyncService.shared
    @ObservedObject private var historyService = HistoryService.shared
    @ObservedObject private var savedService = SavedTracksService.shared
    @ObservedObject private var collectionService = PlaylistCollectionService.shared
    @ObservedObject private var player = AudioPlayerService.shared

    @State private var selectedTab: Int = 0 // 0: Đăng nhập, 1: Đăng ký
    @State private var emailInput: String = ""
    @State private var passwordInput: String = ""
    @State private var confirmPasswordInput: String = ""
    @State private var errorMessage: String? = nil
    @State private var isProcessing: Bool = false
    @State private var showCellularAlert: Bool = false
    @State private var pendingSyncTracksToDownload: [PlaylistItem] = []
    @State private var alertMessage: String = ""
    @State private var showAlert: Bool = false
    @State private var showSignOutAlert: Bool = false

    public init() {}

    public var body: some View {
        NavigationView {
            ZStack {
                Color(red: 0.08, green: 0.06, blue: 0.12).ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 20) {
                        if authService.isLoggedIn, let user = authService.currentUser {
                            loggedInView(user: user)
                        } else {
                            authFormView
                        }
                    }
                    .padding(20)
                }
            }
            .navigationTitle("Tài khoản Kahoii")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Đóng") {
                        dismiss()
                    }
                    .foregroundColor(.white)
                }
            }
            .alert("Thông báo", isPresented: $showAlert) {
                Button("OK", role: .cancel) {}
            } message: {
                Text(alertMessage)
            }
            .alert("Cảnh báo dữ liệu di động (4G/5G)", isPresented: $showCellularAlert) {
                Button("Hủy", role: .cancel) {
                    pendingSyncTracksToDownload = []
                }
                Button("Tiếp tục tải") {
                    startDownloadingMissingTracks(pendingSyncTracksToDownload)
                    pendingSyncTracksToDownload = []
                }
            } message: {
                Text("Bạn đang kết nối dữ liệu di động. Đồng bộ sẽ tải về \(pendingSyncTracksToDownload.count) bài hát đã lưu. Quá trình này có thể tốn dung lượng gói cước. Bạn có muốn tiếp tục không?")
            }
        }
    }

    // MARK: - Logged In View
    @ViewBuilder
    private func loggedInView(user: FirebaseUserInfo) -> some View {
        VStack(spacing: 16) {
            // Profile Header
            VStack(spacing: 8) {
                Image(systemName: "person.crop.circle.fill")
                    .font(.system(size: 64))
                    .foregroundColor(Color(red: 1.0, green: 0.2, blue: 0.6))

                Text(user.email)
                    .font(.system(size: 17, weight: .bold))
                    .foregroundColor(.white)

                Text("UID: \(user.localId)")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundColor(Color(red: 0.53, green: 0.51, blue: 0.61))
                    .lineLimit(1)
            }
            .padding(.vertical, 6)

            // Stats Card (Streamlined matching PC)
            VStack(spacing: 4) {
                Text("Đã tải: \(savedService.savedTracks.count) bài  •  Bộ sưu tập: \(collectionService.playlists.count)")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(Color(red: 0.78, green: 0.76, blue: 0.86))
                Text("Lịch sử nghe: \(historyService.history.count) bài")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(Color(red: 0.78, green: 0.76, blue: 0.86))
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 12)
            .padding(.horizontal, 16)
            .background(Color.white.opacity(0.06))
            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))

            // Sync Status
            Text(syncService.syncStatusMessage.isEmpty ? "Đồng bộ tự động đám mây đang bật" : syncService.syncStatusMessage)
                .font(.system(size: 12, weight: .medium))
                .italic()
                .foregroundColor(Color(red: 1.0, green: 0.2, blue: 0.6))
                .multilineTextAlignment(.center)
                .padding(.horizontal, 10)

            // Action: Single Unified Button (Matching PC)
            Button(action: {
                performManualSync()
            }) {
                HStack(spacing: 8) {
                    if syncService.isSyncing {
                        ProgressView().tint(.white)
                        Text("⟳  Đang Đồng Bộ...")
                    } else {
                        Text("⟳  Đồng Bộ Ngay (Tự Động Bật)")
                    }
                }
                .font(.system(size: 13.5, weight: .bold))
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
                .background(Color(red: 1.0, green: 0.2, blue: 0.6))
                .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
            .disabled(syncService.isSyncing)

            // Sign out button
            Button(action: {
                showSignOutAlert = true
            }) {
                Text("Đăng Xuất")
                    .font(.system(size: 13.5, weight: .bold))
                    .foregroundColor(Color(red: 1.0, green: 0.45, blue: 0.5))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 11)
                    .background(Color(red: 0.15, green: 0.08, blue: 0.12))
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
            .alert("Đăng xuất", isPresented: $showSignOutAlert) {
                Button("Hủy", role: .cancel) {}
                Button("Đăng xuất", role: .destructive) {
                    let currentId = player.currentTrack?.id
                    authService.signOut(currentPlayingId: currentId)
                }
            } message: {
                Text("Bạn có muốn đăng xuất tài khoản không?\n\n(Danh sách bài hát của bạn đã được lưu an toàn trên đám mây và sẽ tự động tải về khi bạn đăng nhập lại).")
            }
        }
    }

    private func statPill(title: String, count: Int) -> some View {
        VStack(spacing: 2) {
            Text("\(count)")
                .font(.system(size: 14, weight: .bold))
                .foregroundColor(.white)
            Text(title)
                .font(.system(size: 10))
                .foregroundColor(.white.opacity(0.5))
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 8)
        .background(Color.white.opacity(0.05))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    // MARK: - Auth Form View
    @ViewBuilder
    private var authFormView: some View {
        VStack(spacing: 18) {
            // Segmented Picker
            Picker("Xác thực", selection: $selectedTab) {
                Text("Đăng nhập").tag(0)
                Text("Đăng ký").tag(1)
            }
            .pickerStyle(.segmented)
            .padding(.bottom, 6)

            // Email Field
            VStack(alignment: .leading, spacing: 6) {
                Text("Email")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(.white.opacity(0.7))

                HStack {
                    Image(systemName: "envelope.fill")
                        .foregroundColor(.white.opacity(0.4))
                    TextField("your.email@example.com", text: $emailInput)
                        .foregroundColor(.white)
                        .autocapitalization(.none)
                        .keyboardType(.emailAddress)
                }
                .padding(12)
                .background(Color.white.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            }

            // Password Field
            VStack(alignment: .leading, spacing: 6) {
                Text("Mật khẩu")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(.white.opacity(0.7))

                HStack {
                    Image(systemName: "lock.fill")
                        .foregroundColor(.white.opacity(0.4))
                    SecureField("Tối thiểu 6 ký tự", text: $passwordInput)
                        .foregroundColor(.white)
                }
                .padding(12)
                .background(Color.white.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            }

            // Quên mật khẩu chỉ xuất hiện ở tab Đăng nhập
            if selectedTab == 0 {
                HStack {
                    Spacer()
                    Button(action: {
                        let email = emailInput.trimmingCharacters(in: .whitespacesAndNewlines)
                        if email.isEmpty || !email.contains("@") {
                            errorMessage = "Vui lòng nhập email hợp lệ ở trên để nhận liên kết khôi phục."
                            return
                        }
                        isProcessing = true
                        Task {
                            let res = await authService.sendPasswordReset(email: email)
                            isProcessing = false
                            switch res {
                            case .success(let msg):
                                errorMessage = nil
                                alertMessage = msg
                                showAlert = true
                            case .failure(let err):
                                errorMessage = err.localizedDescription
                            }
                        }
                    }) {
                        Text("Quên mật khẩu?")
                            .font(.system(size: 12.5, weight: .medium))
                            .foregroundColor(Color(red: 1.0, green: 0.2, blue: 0.6))
                            .underline()
                    }
                }
                .padding(.top, 2)
            }

            // Confirm Password Field (only for Sign up)
            if selectedTab == 1 {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Nhập lại mật khẩu")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(.white.opacity(0.7))

                    HStack {
                        Image(systemName: "lock.shield.fill")
                            .foregroundColor(.white.opacity(0.4))
                        SecureField("Nhập lại mật khẩu trên", text: $confirmPasswordInput)
                            .foregroundColor(.white)
                    }
                    .padding(12)
                    .background(Color.white.opacity(0.08))
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
            }

            if let err = errorMessage {
                Text(err)
                    .font(.system(size: 12))
                    .foregroundColor(.red)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            // Submit Button
            Button(action: {
                submitAuth()
            }) {
                HStack(spacing: 8) {
                    if isProcessing {
                        ProgressView().tint(.white)
                    } else {
                        Text(selectedTab == 0 ? "Đăng nhập" : "Đăng ký tài khoản")
                            .font(.system(size: 15, weight: .bold))
                    }
                }
                .foregroundColor(.white)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .background(Color(red: 1.0, green: 0.2, blue: 0.6))
                .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
            .disabled(isProcessing)

            Text("Khi đăng nhập, toàn bộ bài hát, danh sách yêu thích, đã tải và bộ sưu tập sẽ tự động đồng bộ lên đám mây Firebase.")
                .font(.system(size: 11.5))
                .foregroundColor(.white.opacity(0.4))
                .multilineTextAlignment(.center)
                .padding(.horizontal, 10)
        }
        .padding(16)
        .background(Color.white.opacity(0.05))
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
    }

    // MARK: - Actions

    private func submitAuth() {
        errorMessage = nil
        let email = emailInput.trimmingCharacters(in: .whitespacesAndNewlines)
        if email.isEmpty || passwordInput.isEmpty {
            errorMessage = "Vui lòng nhập đầy đủ thông tin."
            return
        }

        if selectedTab == 1 && passwordInput != confirmPasswordInput {
            errorMessage = "Mật khẩu xác nhận không khớp."
            return
        }

        isProcessing = true
        Task {
            let result: Result<FirebaseUserInfo, AuthError>
            if selectedTab == 0 {
                result = await authService.signIn(email: email, password: passwordInput)
            } else {
                result = await authService.signUp(email: email, password: passwordInput)
            }

            isProcessing = false
            switch result {
            case .success(let user):
                let currentId = player.currentTrack?.id
                authService.clearUserDataExceptPlayingTrack(currentPlayingId: currentId)
                #if canImport(UIKit)
                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                #endif
                // Auto fetch and sync cloud
                performFetch()
            case .failure(let err):
                errorMessage = err.localizedDescription
            }
        }
    }

    private func performUpload() {
        guard let user = authService.currentUser else { return }
        let payload = CloudMusicPayload(
            playlists: player.playlist,
            savedTracks: savedService.savedTracks,
            history: historyService.history,
            favorites: historyService.favorites,
            collections: collectionService.playlists
        )

        Task {
            _ = await syncService.syncUpload(userId: user.localId, idToken: user.idToken, payload: payload)
        }
    }

    private func performManualSync() {
        performFetch()
    }

    private func performFetch() {
        guard let user = authService.currentUser else { return }
        Task {
            if let payload = await syncService.syncFetch(userId: user.localId, idToken: user.idToken) {
                // Apply cloud payload
                applyCloudPayload(payload)
            } else {
                // If cloud document doesn't exist yet, push local data to cloud as initial state!
                performUpload()
            }
        }
    }

    private func applyCloudPayload(_ payload: CloudMusicPayload) {
        // 1. Merge Favorites
        for fav in payload.favorites {
            if !historyService.favorites.contains(where: { $0.id == fav.id }) {
                historyService.favorites.append(fav)
            }
        }

        // 2. Merge Collections
        for cloudCol in payload.collections {
            if let localCol = collectionService.playlists.first(where: { $0.id == cloudCol.id }) {
                for item in cloudCol.items {
                    if !localCol.items.contains(where: { $0.id == item.id }) {
                        collectionService.addTrack(to: localCol.id, track: item)
                    }
                }
            } else {
                let created = collectionService.createPlaylist(name: cloudCol.name)
                for item in cloudCol.items {
                    collectionService.addTrack(to: created.id, track: item)
                }
            }
        }

        // 3. Auto-download missing savedTracks
        var missingTracks: [PlaylistItem] = []
        for track in payload.savedTracks {
            if !savedService.isSaved(id: track.id) {
                missingTracks.append(track)
            }
        }

        if !missingTracks.isEmpty {
            if NetworkMonitor.shared.isCellular {
                pendingSyncTracksToDownload = missingTracks
                showCellularAlert = true
            } else {
                startDownloadingMissingTracks(missingTracks)
            }
        }
    }

    private func startDownloadingMissingTracks(_ tracks: [PlaylistItem]) {
        Task {
            for track in tracks {
                await savedService.saveTrackAsync(track)
            }
        }
    }
}
