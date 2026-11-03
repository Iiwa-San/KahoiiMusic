import SwiftUI

public struct AddToCollectionSheet: View {
    @Environment(\.dismiss) private var dismiss
    @ObservedObject private var collectionService = PlaylistCollectionService.shared
    public let track: PlaylistItem

    @State private var showingCreateAlert: Bool = false
    @State private var newCollectionName: String = ""
    @State private var successToast: String? = nil

    public init(track: PlaylistItem) {
        self.track = track
    }

    public var body: some View {
        NavigationView {
            ZStack {
                Color(red: 0.08, green: 0.06, blue: 0.12).ignoresSafeArea()

                VStack(spacing: 0) {
                    // Header info of track
                    HStack(spacing: 12) {
                        if let url = track.artworkURL {
                            AsyncImage(url: url) { img in
                                img.resizable().aspectRatio(contentMode: .fill)
                            } placeholder: {
                                Color.white.opacity(0.1)
                            }
                            .frame(width: 44, height: 44)
                            .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                        }

                        VStack(alignment: .leading, spacing: 2) {
                            Text(track.title)
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundColor(.white)
                                .lineLimit(1)

                            Text(track.author.isEmpty ? "YouTube" : track.author)
                                .font(.system(size: 12))
                                .foregroundColor(.white.opacity(0.6))
                                .lineLimit(1)
                        }

                        Spacer()
                    }
                    .padding(16)
                    .background(Color.white.opacity(0.04))

                    // Action: Create new collection
                    Button(action: {
                        showingCreateAlert = true
                    }) {
                        HStack(spacing: 12) {
                            Image(systemName: "plus.circle.fill")
                                .font(.system(size: 20))
                                .foregroundColor(Color(red: 1.0, green: 0.2, blue: 0.6))

                            Text("Tạo bộ sưu tập mới")
                                .font(.system(size: 15, weight: .semibold))
                                .foregroundColor(.white)

                            Spacer()
                        }
                        .padding(.horizontal, 16)
                        .padding(.vertical, 14)
                    }

                    Divider().background(Color.white.opacity(0.1))

                    // Existing Collections list
                    if collectionService.playlists.isEmpty {
                        VStack(spacing: 12) {
                            Spacer()
                            Image(systemName: "music.note.list")
                                .font(.system(size: 40))
                                .foregroundColor(.white.opacity(0.3))

                            Text("Chưa có bộ sưu tập nào")
                                .font(.system(size: 15, weight: .medium))
                                .foregroundColor(.white.opacity(0.7))

                            Text("Nhấn vào 'Tạo bộ sưu tập mới' ở trên để bắt đầu thêm bài hát này.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.4))
                                .multilineTextAlignment(.center)
                                .padding(.horizontal, 32)
                            Spacer()
                        }
                    } else {
                        List {
                            ForEach(collectionService.playlists) { col in
                                let alreadyContains = col.items.contains(where: { $0.id == track.id })
                                Button(action: {
                                    if !alreadyContains {
                                        collectionService.addTrack(to: col.id, track: track)
                                        #if canImport(UIKit)
                                        UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                                        #endif
                                        dismiss()
                                    }
                                }) {
                                    HStack(spacing: 12) {
                                        Image(systemName: "folder.fill")
                                            .foregroundColor(alreadyContains ? .gray : Color(red: 1.0, green: 0.2, blue: 0.6))
                                            .font(.system(size: 18))

                                        VStack(alignment: .leading, spacing: 2) {
                                            Text(col.name)
                                                .font(.system(size: 15, weight: .medium))
                                                .foregroundColor(alreadyContains ? .white.opacity(0.4) : .white)

                                            Text(alreadyContains ? "Đã có trong danh sách • \(col.items.count) bài" : "\(col.items.count) bài hát")
                                                .font(.system(size: 12))
                                                .foregroundColor(alreadyContains ? Color(red: 1.0, green: 0.2, blue: 0.6).opacity(0.8) : .white.opacity(0.5))
                                        }

                                        Spacer()

                                        if alreadyContains {
                                            Image(systemName: "checkmark")
                                                .font(.system(size: 13, weight: .bold))
                                                .foregroundColor(Color(red: 1.0, green: 0.2, blue: 0.6))
                                        }
                                    }
                                    .padding(.vertical, 4)
                                }
                                .listRowBackground(Color.clear)
                                .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .navigationTitle("Thêm vào bộ sưu tập")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Đóng") {
                        dismiss()
                    }
                    .foregroundColor(.white)
                }
            }
            .alert("Tạo bộ sưu tập mới", isPresented: $showingCreateAlert) {
                TextField("Tên bộ sưu tập...", text: $newCollectionName)
                Button("Hủy", role: .cancel) {
                    newCollectionName = ""
                }
                Button("Tạo & Thêm") {
                    let trimmed = newCollectionName.trimmingCharacters(in: .whitespacesAndNewlines)
                    if !trimmed.isEmpty {
                        let created = collectionService.createPlaylist(name: trimmed)
                        collectionService.addTrack(to: created.id, track: track)
                        #if canImport(UIKit)
                        UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                        #endif
                        dismiss()
                    }
                    newCollectionName = ""
                }
            }
        }
    }
}
