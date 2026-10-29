# 🎵 KahoiiMusic cho iOS (iPhone & iPad)

Phiên bản Native iOS của ứng dụng nghe nhạc **KahoiiMusic**, được phát triển bằng **Swift** và **SwiftUI**, tối ưu hóa cho hệ điều hành iOS (iOS 16.0 trở lên).

---

## 🌟 Điểm nổi bật & Tính năng tương đồng với bản Desktop

Bản iOS kế thừa toàn bộ trải nghiệm từ bản Windows C# (.NET WinForms):

| Tính năng | KahoiiMusic Windows (C#) | KahoiiMusic iOS (Swift/SwiftUI) |
| :--- | :--- | :--- |
| **Giao diện** | Giao diện tối hiện đại, Dynamic Gradient | SwiftUI Glassmorphism, Mesh Gradient đổi màu theo bìa bài hát |
| **Phát nhạc YouTube** | Gọi ngầm `yt-dlp.exe` | Multi-tier Stream Resolver (Invidious, Piped) không cần binary ngoài |
| **Tìm kiếm & URL** | Thanh tìm kiếm Pill, dán link YouTube/Playlist | `SearchPillBar` tích hợp nút dán Clipboard, tìm kiếm thời gian thực |
| **Phát trong nền** | Chạy thu nhỏ xuống System Tray | `AVAudioSessionCategoryPlayback` (nghe khi khóa màn hình hoặc chuyển app) |
| **Màn hình khóa** | Không có (chỉ có Tooltip Tray) | **Control Center & Lock Screen (`MPNowPlayingInfoCenter`)** đầy đủ ảnh bìa & scrubber |
| **Lặp lại / Trộn bài** | Nút Loop 1/All, Shuffle | Chế độ Lặp 1 bài, Lặp toàn bộ, Tắt lặp, Trộn ngẫu nhiên |
| **Danh sách chờ** | `PlaylistPopup` | `PlaylistQueueSheet` kéo thả sắp xếp, xóa, nạp link YouTube Playlist |
| **Lịch sử & Yêu thích** | Lưu `history.json` | `HistoryService` lưu JSON chuẩn Codable, hỗ trợ mục Yêu thích (Favorites) |

---

## 📱 Cấu trúc thư mục dự án

```text
KahoiiMusic-iOS/
├── Package.swift                     # Khai báo SwiftPM Package (tương thích xtool & Xcode)
├── xtool.yml                         # Cấu hình đóng gói chéo hệ điều hành bằng xtool
├── Info.plist                        # Quyền phát nhạc nền (audio background mode) & ATS
├── KahoiiMusic.xcodeproj/            # Dự án Xcode hoàn chỉnh (mở trên Mac chỉ bằng 1 click)
│   ├── project.pbxproj
│   └── xcshareddata/xcschemes/KahoiiMusic.xcscheme
├── Resources/                        # AppIcon 1024x1024, Logo KahoiiMusic
│   ├── AppIcon.png
│   ├── app_logo.png
│   ├── logo_icon.png
│   └── AppIcon.appiconset/
└── Sources/KahoiiMusic/
    ├── KahoiiMusicApp.swift          # Điểm khởi chạy ứng dụng (@main)
    ├── Models/
    │   └── PlaylistItem.swift        # Model bài hát, thời lượng, trạng thái phát nhạc
    ├── Services/
    │   ├── AudioPlayerService.swift  # Trình phát AVPlayer, phát nền, Lock Screen Controls
    │   ├── YouTubeResolver.swift     # Phân giải luồng âm thanh YouTube, tìm kiếm & playlist
    │   ├── HistoryService.swift      # Quản lý lưu trữ JSON lịch sử nghe & yêu thích
    │   └── ColorExtractor.swift      # Trích xuất màu chủ đạo từ ảnh bìa để tạo Gradient
    └── Views/
        ├── MainPlayerView.swift      # Giao diện chính phát nhạc
        ├── Components/
        │   ├── DynamicGradientView.swift     # Nền gradient đổi màu mượt mà
        │   ├── ArtworkView.swift             # Hiển thị ảnh bìa bo góc, đổ bóng, hiệu ứng nhịp đập
        │   ├── MarqueeText.swift             # Chữ chạy ngang tự động khi tên bài quá dài
        │   ├── MusicTrackBarView.swift       # Thanh tua bài hát tương tác kéo mượt mà
        │   ├── SearchPillBar.swift           # Thanh tìm kiếm dạng viên thuốc (kèm nút dán)
        │   ├── EqualizerAnimationView.swift  # Hoạt ảnh sóng nhạc nhảy theo điệu nhạc
        │   └── MiniPlayerView.swift          # Thanh phát nhạc thu nhỏ ở đáy màn hình
        └── Sheets/
            ├── PlaylistQueueSheet.swift      # Bảng danh sách chờ & nạp playlist
            ├── HistorySheet.swift            # Bảng xem và phát lại lịch sử
            ├── SearchResultsSheet.swift      # Bảng kết quả tìm kiếm trực tiếp trên YouTube
            └── FavoritesSheet.swift          # Bảng các bài hát đã gắn trái tim yêu thích
```

---

## 🚀 Hướng dẫn Cài đặt & Chạy ứng dụng

Bạn có 3 cách để chạy KahoiiMusic trên thiết bị iOS:

### Cách 1: Sử dụng Xcode trên macOS (Chuẩn nhất)
1. Chép thư mục `KahoiiMusic-iOS` sang máy Mac.
2. Mở file `KahoiiMusic.xcodeproj` (hoặc mở `Package.swift`).
3. Cắm iPhone/iPad qua cáp USB hoặc chọn iOS Simulator (ví dụ iPhone 15/16 Pro).
4. Chọn tài khoản Apple ID trong tab **Signing & Capabilities**.
5. Nhấn **Cmd + R** (hoặc nút Play ▶) để cài đặt và chạy app.

### Cách 2: Sử dụng `xtool` trên Windows / Linux / WSL
Nếu bạn đang dùng bộ công cụ `xtool` có trong thư mục `../xtool`:
```bash
cd KahoiiMusic-iOS
xtool dev
```
`xtool` sẽ tự động biên dịch dự án SwiftPM thành ứng dụng iOS và ký số cài lên thiết bị đã kết nối.

### Cách 3: Đóng gói file `.ipa` và Sideload (AltStore / SideStore / TrollStore / Sideloadly)
1. Tạo file `.ipa` thông qua Xcode Archive hoặc `xtool`.
2. Dùng **AltStore**, **SideStore**, **TrollStore** hoặc **Sideloadly** trên máy tính/điện thoại để cài trực tiếp vào iPhone mà không cần tài khoản nhà phát triển trả phí.

---

## 🔒 Cơ chế phát nhạc khi tắt màn hình (Background Audio)
- Ứng dụng đã được tích hợp `UIBackgroundModes: ["audio"]` trong [Info.plist](Info.plist).
- Khi người dùng khóa màn hình hoặc chuyển sang ứng dụng khác:
  - Nhạc vẫn tiếp tục phát liền mạch mà không bị gián đoạn.
  - Trung tâm điều khiển (Control Center) và Màn hình khóa hiển thị tên bài, tên nghệ sĩ, thời gian và ảnh bìa chất lượng cao.
  - Hỗ trợ đầy đủ các nút bấm: Tạm dừng/Tiếp tục, Tiến/Lùi 15 giây, Bài tiếp theo/Trước đó, và thanh tua trực tiếp trên Lock Screen.
  - Tự động tạm dừng nếu bạn rút tai nghe hoặc ngắt kết nối tai nghe Bluetooth.

