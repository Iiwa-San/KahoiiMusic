import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct PlaylistQueueSheet: View {
    @ObservedObject var player: AudioPlayerService
    @ObservedObject private var collectionService = PlaylistCollectionService.shared
    @Environment(\.dismiss) private var dismiss

    @State private var selectedTab: Int = 0 // 0: Danh sách chờ, 1: Bộ sưu tập
    @State private var inspectingCollectionId: String? = nil

    // Dialog states
    @State private var showingLoadPlaylistDialog: Bool = false
    @State private var playlistInputUrl: String = ""

    @State private var showingSavePlaylistDialog: Bool = false
    @State private var savePlaylistName: String = ""

    @State private var showingCreateCollectionDialog: Bool = false
    @State private var newCollectionName: String = ""

    @State private var showingRenameDialog: Bool = false
    @State private var renameCollectionId: String = ""
    @State private var renameCollectionName: String = ""

    @State private var trackToAddToCollection: PlaylistItem? = nil
    @State private var showingAddToCollectionSheet: Bool = false

    public init(player: AudioPlayerService) {
        self.player = player
    }

    public var body: some View {
        NavigationView {
            ZStack {
                Color(white: 0.08).ignoresSafeArea()

                VStack(spacing: 0) {
                    // Segmented Tab Control: Danh sách chờ vs Bộ sưu tập
                    tabSelectorSection
                        .padding(.horizontal, 20)
                        .padding(.top, 12)
                        .padding(.bottom, 8)

                    Divider().background(Color.white.opacity(0.1))

                    if selectedTab == 0 {
                        queueTabView
                    } else {
                        collectionTabView
                    }
                }
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    if selectedTab == 0 && !player.playlist.isEmpty {
                        Button("Xóa tất cả") {
                            player.clearQueue()
                        }
                        .foregroundColor(.red.opacity(0.85))
                    } else if selectedTab == 1 && inspectingCollectionId != nil {
                        Button(action: {
                            inspectingCollectionId = nil
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "chevron.left")
                                Text("Bộ sưu tập")
                            }
                            .foregroundColor(.purple)
                            .font(.system(size: 15, weight: .semibold))
                        }
                    }
                }

                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Xong") {
                        dismiss()
                    }
                    .foregroundColor(.white)
                    .fontWeight(.semibold)
                }
            }
            // Alert: Nạp URL YouTube
            .alert("Nạp danh sách phát YouTube", isPresented: $showingLoadPlaylistDialog) {
                TextField("Dán link YouTube playlist...", text: $playlistInputUrl)
                Button("Nạp", action: {
                    if !playlistInputUrl.isEmpty {
                        player.loadAndPlay(input: playlistInputUrl)
                        playlistInputUrl = ""
                    }
                })
                Button("Hủy", role: .cancel) {
                    playlistInputUrl = ""
                }
            } message: {
                Text("Nhập URL hoặc ID danh sách phát từ YouTube:")
            }
            // Alert: Lưu danh sách phát hiện tại
            .alert("Lưu vào Bộ sưu tập", isPresented: $showingSavePlaylistDialog) {
                TextField("Tên bộ sưu tập...", text: $savePlaylistName)
                Button("Lưu", action: {
                    let title = savePlaylistName.trimmingCharacters(in: .whitespacesAndNewlines)
                    let finalName = title.isEmpty ? defaultCollectionName() : title
                    collectionService.savePlaylist(name: finalName, items: player.playlist)
                    savePlaylistName = ""
                    selectedTab = 1
                })
                Button("Hủy", role: .cancel) {
                    savePlaylistName = ""
                }
            } message: {
                Text("Lưu \(player.playlist.count) bài hát trong danh sách chờ vào Bộ sưu tập mới:")
            }
            // Alert: Tạo bộ sưu tập mới (rỗng)
            .alert("Tạo bộ sưu tập mới", isPresented: $showingCreateCollectionDialog) {
                TextField("Tên bộ sưu tập...", text: $newCollectionName)
                Button("Tạo", action: {
                    let title = newCollectionName.trimmingCharacters(in: .whitespacesAndNewlines)
                    let finalName = title.isEmpty ? defaultCollectionName() : title
                    collectionService.createPlaylist(name: finalName)
                    newCollectionName = ""
                })
                Button("Hủy", role: .cancel) {
                    newCollectionName = ""
                }
            } message: {
                Text("Nhập tên cho bộ sưu tập mới:")
            }
            // Alert: Đổi tên bộ sưu tập
            .alert("Đổi tên bộ sưu tập", isPresented: $showingRenameDialog) {
                TextField("Tên mới...", text: $renameCollectionName)
                Button("Lưu", action: {
                    if !renameCollectionName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                        collectionService.renamePlaylist(id: renameCollectionId, newName: renameCollectionName)
                    }
                    renameCollectionName = ""
                })
                Button("Hủy", role: .cancel) {
                    renameCollectionName = ""
                }
            }
            // ActionSheet/Sheet: Thêm bài hát vào bộ sưu tập
            .sheet(isPresented: $showingAddToCollectionSheet) {
                addToCollectionSheetView
            }
        }
    }

    // MARK: - Tab Selector
    private var tabSelectorSection: some View {
        HStack(spacing: 8) {
            tabButton(title: "Danh sách chờ (\(player.playlist.count))", index: 0, icon: "music.note.list")
            tabButton(title: "Bộ sưu tập (\(collectionService.playlists.count))", index: 1, icon: "folder.fill")
        }
    }

    private func tabButton(title: String, index: Int, icon: String) -> some View {
        let isSelected = selectedTab == index
        return Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            selectedTab = index
        }) {
            HStack(spacing: 6) {
                Image(systemName: icon)
                    .font(.system(size: 13, weight: .bold))
                Text(title)
                    .font(.system(size: 13, weight: isSelected ? .bold : .medium))
            }
            .frame(maxWidth: .infinity)
            .padding(.vertical, 8)
            .background(isSelected ? Color.purple.opacity(0.85) : Color.white.opacity(0.08))
            .foregroundColor(isSelected ? .white : .white.opacity(0.65))
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
    }

    // MARK: - TAB 0: Danh Sách Chờ
    private var queueTabView: some View {
        VStack(spacing: 0) {
            // Queue Header Bar
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("\(player.playlist.count) bài hát • \(formattedDuration(player.playlist))")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(.white.opacity(0.65))
                }

                Spacer()

                HStack(spacing: 8) {
                    // Button Save to Collection
                    if !player.playlist.isEmpty {
                        Button(action: {
                            savePlaylistName = defaultCollectionName()
                            showingSavePlaylistDialog = true
                        }) {
                            HStack(spacing: 5) {
                                Image(systemName: "square.and.arrow.down.fill")
                                Text("Lưu BST")
                            }
                            .font(.system(size: 12.5, weight: .semibold))
                            .foregroundColor(.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 6)
                            .background(Color(red: 0.75, green: 0.35, blue: 0.95))
                            .clipShape(Capsule())
                        }
                    }

                    // Button Load URL
                    Button(action: {
                        showingLoadPlaylistDialog = true
                    }) {
                        HStack(spacing: 5) {
                            Image(systemName: "plus.circle.fill")
                            Text("Nạp URL")
                        }
                        .font(.system(size: 12.5, weight: .semibold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 6)
                        .background(Color.white.opacity(0.14))
                        .clipShape(Capsule())
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 10)

            Divider().background(Color.white.opacity(0.08))

            if player.playlist.isEmpty {
                VStack(spacing: 16) {
                    Spacer()
                    Image(systemName: "music.note.list")
                        .font(.system(size: 56))
                        .foregroundColor(.white.opacity(0.25))

                    Text("Danh sách chờ trống")
                        .font(.system(size: 17, weight: .medium))
                        .foregroundColor(.white.opacity(0.7))

                    Text("Tìm kiếm bài hát hoặc dán link YouTube để bắt đầu nghe nhạc.")
                        .font(.system(size: 13))
                        .foregroundColor(.white.opacity(0.4))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 40)
                    Spacer()
                }
            } else {
                List {
                    ForEach(Array(player.playlist.enumerated()), id: \.element.id) { index, item in
                        playlistRow(index: index, item: item)
                            .listRowBackground(
                                index == player.currentIndex ? Color.white.opacity(0.08) : Color.clear
                            )
                            .listRowSeparatorTint(Color.white.opacity(0.08))
                    }
                    .onDelete(perform: deleteQueueItems)
                    .onMove(perform: moveQueueItems)
                }
                .listStyle(.plain)
                .scrollContentBackground(.hidden)
            }
        }
    }

    @ViewBuilder
    private func playlistRow(index: Int, item: PlaylistItem) -> some View {
        let isCurrent = (index == player.currentIndex)

        Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            player.currentIndex = index
            player.playTrack(item)
        }) {
            HStack(spacing: 12) {
                // Index or Equalizer
                if isCurrent {
                    EqualizerAnimationView(isPlaying: player.playbackState.isPlaying, color: .purple)
                        .frame(width: 24)
                } else {
                    Text("\(index + 1)")
                        .font(.system(size: 13, weight: .medium, design: .monospaced))
                        .foregroundColor(.white.opacity(0.4))
                        .frame(width: 24)
                }

                // Thumbnail
                if let url = item.artworkURL {
                    AsyncImage(url: url) { img in
                        img.resizable().aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Color.white.opacity(0.1)
                    }
                    .frame(width: 46, height: 46)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                // Details
                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 14, weight: isCurrent ? .bold : .medium))
                        .foregroundColor(isCurrent ? .purple : .white)
                        .lineLimit(1)

                    Text(item.author.isEmpty ? "YouTube" : item.author)
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.55))
                        .lineLimit(1)
                }

                Spacer()

                Text(item.formattedDuration)
                    .font(.system(size: 12, weight: .regular, design: .monospaced))
                    .foregroundColor(.white.opacity(0.5))
            }
            .padding(.vertical, 4)
        }
        .buttonStyle(PlainButtonStyle())
        .contextMenu {
            Button(action: {
                player.currentIndex = index
                player.playTrack(item)
            }) {
                Label("Phát ngay", systemImage: "play.fill")
            }

            Button(action: {
                trackToAddToCollection = item
                showingAddToCollectionSheet = true
            }) {
                Label("Thêm vào Bộ sưu tập...", systemImage: "folder.badge.plus")
            }

            if SavedTracksService.shared.isSaved(id: item.id) {
                Label("✓ Đã lưu Offline", systemImage: "checkmark.circle.fill")
            } else {
                Button(action: {
                    Task { await SavedTracksService.shared.saveTrackAsync(item) }
                }) {
                    Label("Tải về nghe Offline", systemImage: "arrow.down.circle")
                }
            }

            Button(role: .destructive, action: {
                if index < player.playlist.count {
                    player.removeFromQueue(at: index)
                }
            }) {
                Label("Xóa khỏi danh sách", systemImage: "trash")
            }
        }
    }

    private func deleteQueueItems(at offsets: IndexSet) {
        for index in offsets {
            player.removeFromQueue(at: index)
        }
    }

    private func moveQueueItems(from source: IndexSet, to destination: Int) {
        player.playlist.move(fromOffsets: source, toOffset: destination)
    }

    // MARK: - TAB 1: Bộ Sưu Tập
    private var collectionTabView: some View {
        Group {
            if let colId = inspectingCollectionId,
               let collection = collectionService.playlists.first(where: { $0.id == colId }) {
                collectionDetailView(collection: collection)
            } else {
                collectionListView
            }
        }
    }

    // List of Collections
    private var collectionListView: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Text("\(collectionService.playlists.count) bộ sưu tập đã lưu")
                    .font(.system(size: 13, weight: .medium))
                    .foregroundColor(.white.opacity(0.65))

                Spacer()

                Button(action: {
                    newCollectionName = ""
                    showingCreateCollectionDialog = true
                }) {
                    HStack(spacing: 5) {
                        Image(systemName: "plus")
                        Text("Tạo mới")
                    }
                    .font(.system(size: 12.5, weight: .semibold))
                    .foregroundColor(.white)
                    .padding(.horizontal, 11)
                    .padding(.vertical, 6)
                    .background(Color.purple.opacity(0.85))
                    .clipShape(Capsule())
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 10)

            Divider().background(Color.white.opacity(0.08))

            if collectionService.playlists.isEmpty {
                VStack(spacing: 16) {
                    Spacer()
                    Image(systemName: "folder.badge.plus")
                        .font(.system(size: 56))
                        .foregroundColor(.white.opacity(0.25))

                    Text("Chưa có bộ sưu tập nào")
                        .font(.system(size: 17, weight: .medium))
                        .foregroundColor(.white.opacity(0.7))

                    Text("Nhấn 'Tạo mới' hoặc chuyển sang tab 'Danh sách chờ' rồi chọn 'Lưu BST' để lưu danh sách hiện tại.")
                        .font(.system(size: 13))
                        .foregroundColor(.white.opacity(0.4))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 40)
                    Spacer()
                }
            } else {
                List {
                    ForEach(collectionService.playlists) { col in
                        collectionCardRow(collection: col)
                            .listRowBackground(Color.clear)
                            .listRowSeparatorTint(Color.white.opacity(0.08))
                    }
                    .onDelete(perform: deleteCollections)
                }
                .listStyle(.plain)
                .scrollContentBackground(.hidden)
            }
        }
    }

    private func collectionCardRow(collection: UserPlaylist) -> some View {
        Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            inspectingCollectionId = collection.id
        }) {
            HStack(spacing: 14) {
                // Folder / Artwork preview
                ZStack {
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(LinearGradient(
                            colors: [Color.purple.opacity(0.4), Color.blue.opacity(0.25)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ))
                        .frame(width: 52, height: 52)

                    if let firstTrack = collection.items.first, let url = firstTrack.artworkURL {
                        AsyncImage(url: url) { img in
                            img.resizable().aspectRatio(contentMode: .fill)
                        } placeholder: {
                            Image(systemName: "music.note.list")
                                .foregroundColor(.white.opacity(0.6))
                        }
                        .frame(width: 52, height: 52)
                        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                    } else {
                        Image(systemName: "music.note.list")
                            .font(.system(size: 20))
                            .foregroundColor(.white.opacity(0.7))
                    }
                }

                // Info
                VStack(alignment: .leading, spacing: 4) {
                    Text(collection.name)
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text("\(collection.items.count) bài hát • \(formattedDuration(collection.items))")
                        .font(.system(size: 12))
                        .foregroundColor(.white.opacity(0.55))

                    Text("Tạo: \(formattedDate(collection.createdAt))")
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.35))
                }

                Spacer()

                // Play Button
                Button(action: {
                    #if canImport(UIKit)
                    UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                    #endif
                    playEntireCollection(collection)
                }) {
                    Image(systemName: "play.circle.fill")
                        .font(.system(size: 32))
                        .foregroundColor(.purple)
                }
                .buttonStyle(PlainButtonStyle())
            }
            .padding(.vertical, 4)
        }
        .buttonStyle(PlainButtonStyle())
        .contextMenu {
            Button(action: {
                playEntireCollection(collection)
            }) {
                Label("Phát tất cả", systemImage: "play.fill")
            }

            Button(action: {
                downloadAllCollection(collection)
            }) {
                Label("Tải tất cả Offline", systemImage: "arrow.down.circle")
            }

            Button(action: {
                renameCollectionId = collection.id
                renameCollectionName = collection.name
                showingRenameDialog = true
            }) {
                Label("Đổi tên", systemImage: "pencil")
            }

            Button(role: .destructive, action: {
                collectionService.deletePlaylist(id: collection.id)
            }) {
                Label("Xóa bộ sưu tập", systemImage: "trash")
            }
        }
    }

    private func deleteCollections(at offsets: IndexSet) {
        for index in offsets {
            if index < collectionService.playlists.count {
                let id = collectionService.playlists[index].id
                collectionService.deletePlaylist(id: id)
            }
        }
    }

    // Detail View for a Single Collection
    private func collectionDetailView(collection: UserPlaylist) -> some View {
        VStack(spacing: 0) {
            // Collection Info Banner
            VStack(spacing: 12) {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text(collection.name)
                            .font(.system(size: 18, weight: .bold))
                            .foregroundColor(.white)
                            .lineLimit(1)

                        Text("\(collection.items.count) bài hát • \(formattedDuration(collection.items))")
                            .font(.system(size: 12.5))
                            .foregroundColor(.white.opacity(0.6))
                    }

                    Spacer()

                    // Rename button
                    Button(action: {
                        renameCollectionId = collection.id
                        renameCollectionName = collection.name
                        showingRenameDialog = true
                    }) {
                        Image(systemName: "pencil.circle")
                            .font(.system(size: 22))
                            .foregroundColor(.white.opacity(0.6))
                    }
                }

                // Action Bar (Play All, Download All)
                HStack(spacing: 10) {
                    Button(action: {
                        playEntireCollection(collection)
                    }) {
                        HStack(spacing: 6) {
                            Image(systemName: "play.fill")
                            Text("Phát tất cả")
                        }
                        .font(.system(size: 13, weight: .bold))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 9)
                        .background(Color.purple)
                        .clipShape(Capsule())
                    }

                    Button(action: {
                        downloadAllCollection(collection)
                    }) {
                        HStack(spacing: 6) {
                            Image(systemName: "arrow.down.circle.fill")
                            Text("Tải tất cả")
                        }
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 9)
                        .background(Color.white.opacity(0.12))
                        .clipShape(Capsule())
                    }
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 12)

            Divider().background(Color.white.opacity(0.08))

            if collection.items.isEmpty {
                VStack(spacing: 14) {
                    Spacer()
                    Image(systemName: "music.note")
                        .font(.system(size: 48))
                        .foregroundColor(.white.opacity(0.2))

                    Text("Bộ sưu tập chưa có bài hát")
                        .font(.system(size: 15, weight: .medium))
                        .foregroundColor(.white.opacity(0.6))

                    Text("Hãy thêm bài hát từ tab Danh sách chờ hoặc Tìm kiếm.")
                        .font(.system(size: 12.5))
                        .foregroundColor(.white.opacity(0.35))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 30)
                    Spacer()
                }
            } else {
                List {
                    ForEach(Array(collection.items.enumerated()), id: \.element.id) { index, item in
                        collectionTrackRow(collectionId: collection.id, index: index, item: item)
                            .listRowBackground(Color.clear)
                            .listRowSeparatorTint(Color.white.opacity(0.08))
                    }
                    .onDelete { offsets in
                        for index in offsets {
                            if index < collection.items.count {
                                let trackId = collection.items[index].id
                                collectionService.removeTrack(from: collection.id, trackId: trackId)
                            }
                        }
                    }
                }
                .listStyle(.plain)
                .scrollContentBackground(.hidden)
            }
        }
    }

    private func collectionTrackRow(collectionId: String, index: Int, item: PlaylistItem) -> some View {
        Button(action: {
            #if canImport(UIKit)
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            #endif
            if let col = collectionService.playlists.first(where: { $0.id == collectionId }) {
                player.setPlaylist(col.items, startIndex: index)
                player.playTrack(item)
            }
        }) {
            HStack(spacing: 12) {
                Text("\(index + 1)")
                    .font(.system(size: 13, weight: .medium, design: .monospaced))
                    .foregroundColor(.white.opacity(0.4))
                    .frame(width: 24)

                if let url = item.artworkURL {
                    AsyncImage(url: url) { img in
                        img.resizable().aspectRatio(contentMode: .fill)
                    } placeholder: {
                        Color.white.opacity(0.1)
                    }
                    .frame(width: 44, height: 44)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text(item.title)
                        .font(.system(size: 13.5, weight: .medium))
                        .foregroundColor(.white)
                        .lineLimit(1)

                    Text(item.author.isEmpty ? "YouTube" : item.author)
                        .font(.system(size: 11.5))
                        .foregroundColor(.white.opacity(0.55))
                        .lineLimit(1)
                }

                Spacer()

                Text(item.formattedDuration)
                    .font(.system(size: 12, weight: .regular, design: .monospaced))
                    .foregroundColor(.white.opacity(0.5))
            }
            .padding(.vertical, 3)
        }
        .buttonStyle(PlainButtonStyle())
        .contextMenu {
            Button(action: {
                if let col = collectionService.playlists.first(where: { $0.id == collectionId }) {
                    player.setPlaylist(col.items, startIndex: index)
                    player.playTrack(item)
                }
            }) {
                Label("Phát ngay", systemImage: "play.fill")
            }

            if SavedTracksService.shared.isSaved(id: item.id) {
                Label("✓ Đã lưu Offline", systemImage: "checkmark.circle.fill")
            } else {
                Button(action: {
                    Task { await SavedTracksService.shared.saveTrackAsync(item) }
                }) {
                    Label("Tải về nghe Offline", systemImage: "arrow.down.circle")
                }
            }

            Button(role: .destructive, action: {
                collectionService.removeTrack(from: collectionId, trackId: item.id)
            }) {
                Label("Xóa khỏi bộ sưu tập", systemImage: "trash")
            }
        }
    }

    // MARK: - Add To Collection Sheet
    private var addToCollectionSheetView: some View {
        NavigationView {
            ZStack {
                Color(white: 0.1).ignoresSafeArea()

                VStack(spacing: 0) {
                    if collectionService.playlists.isEmpty {
                        VStack(spacing: 16) {
                            Spacer()
                            Text("Chưa có bộ sưu tập nào")
                                .font(.system(size: 16, weight: .semibold))
                                .foregroundColor(.white)
                            Button("Tạo bộ sưu tập mới") {
                                showingAddToCollectionSheet = false
                                newCollectionName = ""
                                showingCreateCollectionDialog = true
                            }
                            .foregroundColor(.purple)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(collectionService.playlists) { col in
                                Button(action: {
                                    if let track = trackToAddToCollection {
                                        collectionService.addTrack(to: col.id, track: track)
                                    }
                                    showingAddToCollectionSheet = false
                                    trackToAddToCollection = nil
                                }) {
                                    HStack {
                                        Image(systemName: "folder.fill")
                                            .foregroundColor(.purple)
                                        Text(col.name)
                                            .font(.system(size: 15, weight: .medium))
                                            .foregroundColor(.white)
                                        Spacer()
                                        Text("\(col.items.count) bài")
                                            .font(.system(size: 12))
                                            .foregroundColor(.white.opacity(0.5))
                                    }
                                    .padding(.vertical, 6)
                                }
                                .listRowBackground(Color.clear)
                            }
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .navigationTitle("Chọn bộ sưu tập")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Đóng") {
                        showingAddToCollectionSheet = false
                        trackToAddToCollection = nil
                    }
                    .foregroundColor(.white)
                }
            }
        }
    }

    // MARK: - Helper Functions
    private func playEntireCollection(_ collection: UserPlaylist) {
        guard !collection.items.isEmpty else { return }
        player.setPlaylist(collection.items, startIndex: 0)
        if let first = collection.items.first {
            player.playTrack(first)
        }
    }

    private func downloadAllCollection(_ collection: UserPlaylist) {
        Task {
            for track in collection.items {
                if !SavedTracksService.shared.isSaved(id: track.id) {
                    await SavedTracksService.shared.saveTrackAsync(track)
                }
            }
        }
    }

    private func defaultCollectionName() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd HH:mm"
        return "Bộ sưu tập " + formatter.string(from: Date())
    }

    private func formattedDate(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "dd/MM/yyyy HH:mm"
        return formatter.string(from: date)
    }

    private func formattedDuration(_ items: [PlaylistItem]) -> String {
        let total = items.reduce(0) { $0 + $1.durationSeconds }
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        if hours > 0 {
            return "\(hours) giờ \(minutes) phút"
        } else {
            return "\(minutes) phút"
        }
    }
}
