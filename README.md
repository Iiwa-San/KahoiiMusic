# Kahoii Music - Android App

Ứng dụng nghe nhạc **Kahoii Music** chuyển đổi từ phiên bản C# (Windows Forms) sang **Android** nguyên bản (Kotlin + Jetpack Compose + AndroidX Media3 ExoPlayer).

---

## ✨ Tính năng nổi bật

1. **Giao diện chuẩn phong cách Kahoii Music gốc:**
   - Nền Gradient chuyển màu đặc trưng từ Tím sâu (`#2B107F`) sang Hồng Neon (`#FF2680`).
   - Khung thẻ phát nhạc trong suốt với viền phản quang.
   - Đĩa than **Vinyl Artwork** xoay mượt mà 360 độ theo thời gian thực khi đang phát nhạc, tự động dừng khi tạm dừng.
   - Nhãn chữ chạy **Marquee** cho tên bài hát và thông báo trạng thái.
   - Thanh tìm kiếm dạng **Search Pill Box** ("link youtube...") hỗ trợ dán link YouTube (video/playlist/shorts) hoặc tìm kiếm theo từ khóa.
   - Thanh tua bài hát **Music Track Bar** (Line đỏ rực `#EB0F0F` cho phần đã phát, line trắng phần chưa phát) kèm hiển thị thời gian hiện tại / tổng thời lượng.
   - Các nút điều khiển: Lặp lại (Tắt / Lặp 1 bài / Lặp danh sách), Lùi bài, Phát/Tạm dừng phát sáng viền neon, Chuyển bài, Chỉnh âm lượng.

2. **Phát nhạc nền & Điều khiển hệ thống (Background Service):**
   - Sử dụng **AndroidX Media3 ExoPlayer** kết hợp **MediaSessionService**.
   - Hỗ trợ phát nhạc nền khi tắt màn hình hoặc chuyển sang ứng dụng khác.
   - Thông báo điều khiển MediaStyle trên thanh thông báo và màn hình khóa (Lock screen).
   - Hỗ trợ nút điều khiển trên tai nghe Bluetooth / xe hơi.
   - Quản lý Audio Focus: tự động dừng khi có cuộc gọi và tiếp tục khi kết thúc.

3. **Xử lý luồng âm thanh YouTube thông minh:**
   - Tự động trích xuất Video ID và luồng Audio trực tiếp từ YouTube InnerTube API mà không cần chạy file nhị phân `yt-dlp.exe` của Windows.
   - Cơ chế Fallback đa tầng (InnerTube -> Piped API -> Invidious API) đảm bảo độ ổn định cao.
   - Hỗ trợ Playlist: tự động tải danh sách bài hát và chuyển bài liên tục.
   - Hỗ trợ Tìm kiếm từ khóa: tự động tìm và phát bài đầu tiên, hiển thị danh sách kết quả.

4. **Danh sách phát & Lịch sử (Playlist & History):**
   - Popup BottomSheet gồm 2 tab: **Danh sách phát** (Queue hiện tại) và **Lịch sử** (Lịch sử các bài đã nghe).
   - Lưu trữ lịch sử vĩnh viễn (`history.json`) giống bản C#.
   - Bấm vào bất kỳ bài nào để phát ngay lập tức.
   - Xóa từng bài hoặc xóa toàn bộ lịch sử.

---

## 🚀 Hướng dẫn mở và chạy trên Android Studio

1. Mở **Android Studio**.
2. Chọn **File -> Open...** (hoặc **Open Project** ở màn hình khởi động).
3. Duyệt đến thư mục:
   ```
   e:\Downloads\musicApp\KahoiiMusicAndroid
   ```
4. Đợi Android Studio đồng bộ Gradle (Gradle Sync) tự động.
5. Chọn thiết bị giả lập (Emulator) hoặc cắm điện thoại Android thực tế bật USB Debugging.
6. Bấm nút **Run (tam giác xanh)** hoặc nhấn tổ hợp `Shift + F10` để trải nghiệm ứng dụng!

---

## 📦 Cấu trúc thư mục dự án

```
KahoiiMusicAndroid/
├── app/
│   ├── build.gradle.kts
│   └── src/main/
│       ├── AndroidManifest.xml
│       ├── java/com/kahoii/music/
│       │   ├── KahoiiMusicApp.kt          # Khởi tạo App & Notification Channel
│       │   ├── MainActivity.kt            # Activity chính, nhận URL chia sẻ
│       │   ├── model/                     # PlaylistItem, LoopMode, PlayerState
│       │   ├── data/                      # YouTubeRepository, HistoryRepository
│       │   ├── service/                   # MusicPlaybackService (Media3 Service)
│       │   ├── viewmodel/                 # MainViewModel quản lý logic & phát nhạc
│       │   └── ui/
│       │       ├── theme/                 # Theme, Màu gradient, Typography
│       │       ├── components/            # VinylArtwork, SearchPill, TrackBar...
│       │       └── screens/               # MusicPlayerScreen, PlaylistBottomSheet
│       └── res/                           # Icon, Drawable, Logo, Theme XMLs
├── gradle/
│   └── libs.versions.toml                 # Version Catalog
├── build.gradle.kts
├── settings.gradle.kts
└── gradlew / gradlew.bat
```

