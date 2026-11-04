# 🎵 KahoiiMusic - Phiên bản React Native (Expo Go)

Ứng dụng phát nhạc trực tuyến từ YouTube dành cho thiết bị di động (iOS & Android), được xây dựng bằng **React Native** và nền tảng **Expo**, tương thích 100% với ứng dụng **Expo Go**.

---

## 🌟 Tính năng chính

- **Phát trực tiếp YouTube (Multi-Tier Stream Resolver)**:
  - Tự động trích xuất luồng âm thanh định dạng AAC/m4a nguyên bản (không qua chuyển đổi, độ trễ 0s) thông qua YouTube InnerTube API (VisionOS, Android VR, Invidious fallback).
  - Tự động nhận diện link video YouTube (`https://youtu.be/...`, `https://youtube.com/watch?v=...`) hoặc link danh sách phát (`list=...`).
- **Tìm kiếm thời gian thực**:
  - Tìm kiếm bài hát, ca sĩ, album trực tiếp từ YouTube với ảnh bìa thumbnail, nghệ sĩ và thời lượng chuẩn xác.
- **Dán liên kết nhanh (Clipboard Support)**:
  - Nút dán Clipboard tự động đọc đường dẫn trong bộ nhớ tạm và phát ngay lập tức.
- **Giao diện Glassmorphism & Dynamic Gradient**:
  - Hình nền gradient đổi màu hài hòa theo màu sắc và nhịp điệu của bài hát.
  - Hoạt ảnh sóng nhạc Equalizer nhảy theo điệu nhạc thời gian thực.
  - Hiệu ứng nhịp thở (breathing glow) cho ảnh bìa album khi đang phát.
- **Điều khiển phát nhạc chuyên nghiệp**:
  - Trộn bài (Shuffle), Lặp lại 1 bài (Loop One), Lặp lại toàn bộ (Loop All), Tắt lặp (Loop Off).
  - Thanh tua nhạc (Scrubber) mượt mà với hiển thị số giây chính xác.
  - Thanh chỉnh âm lượng tích hợp.
- **Hẹn giờ tắt nhạc (Sleep Timer)**:
  - Các mốc 15m, 30m, 45m, 60m, 90m kèm đồng hồ đếm ngược.
- **Danh sách chờ, Yêu thích & Lịch sử**:
  - Quản lý hàng đợi bài hát (Queue), xóa từng bài hoặc xóa toàn bộ.
  - Lưu bài hát yêu thích với nút trái tim.
  - Tự động ghi nhớ lịch sử nghe nhạc qua AsyncStorage.
- **Phát trong nền (Background Audio)**:
  - Cấu hình chuẩn `expo-av` chạy ngầm khi khóa màn hình hoặc chuyển app.

---

## 🚀 Hướng dẫn chạy thử bằng Expo Go

### Bước 1: Cài ứng dụng Expo Go trên điện thoại
- **iOS (iPhone/iPad)**: Mở App Store, tìm tải **Expo Go**.
- **Android**: Mở Google Play Store, tìm tải **Expo Go**.

### Bước 2: Khởi động máy chủ phát triển
Mở terminal tại thư mục `KahoiiMusic-Expo`:

```bash
cd KahoiiMusic-Expo
npm start
```
*(hoặc `npx expo start`)*

Terminal sẽ hiển thị một mã QR Code lớn.

### Bước 3: Quét mã QR
- **Đối với Android**: Mở ứng dụng **Expo Go**, bấm **Scan QR Code** và quét mã QR trên màn hình máy tính.
- **Đối với iOS (iPhone)**: Mở ứng dụng **Camera** mặc định của iPhone, quét mã QR và nhấn vào thông báo mở bằng **Expo Go**.

> **Lưu ý**: Đảm bảo điện thoại và máy tính kết nối chung một mạng Wi-Fi (hoặc sử dụng lệnh `npx expo start --tunnel` nếu khác mạng).

---

## 🌐 Hướng dẫn chạy & cài đặt phiên bản PWA (Progressive Web App)

Phiên bản **PWA** cho phép bạn phát nhạc trực tiếp trên trình duyệt Web (Chrome, Edge, Safari) trên máy tính hoặc điện thoại, đồng thời có thể **Cài đặt thành ứng dụng độc lập (Add to Home Screen)**.

> 💡 **Đặc biệt cho iOS (iPhone/iPad):** Khi thêm PWA vào Màn hình chính trên iOS Safari, ứng dụng sẽ có khả năng **phát nhạc trong nền (Background Audio) và hiện bộ điều khiển ở Màn hình khóa** mà không cần Mac hay tài khoản Apple Developer!

### Cách 1: Chạy phát triển (Development)
```bash
npm run web
# hoặc: npx expo start --web
```
Mở trình duyệt tại `http://localhost:8081`.

### Cách 2: Biên dịch và chạy Server Production
```bash
# 1. Build PWA tối ưu tĩnh
npm run build:web

# 2. Khởi động server PWA độc lập (kèm API proxy tích hợp)
npm run serve
```
Mở trình duyệt tại `http://localhost:3000`.

### Cách 3: Cài đặt PWA lên thiết bị
- **Trên iPhone (Safari):** Bấm nút **Chia sẻ (Share)** -> Chọn **Thêm vào màn hình chính (Add to Home Screen)**.
- **Trên Android (Chrome):** Bấm menu 3 chấm -> Chọn **Cài đặt ứng dụng (Install App)**.
- **Trên Máy tính (Chrome/Edge):** Bấm biểu tượng Cài đặt trên thanh địa chỉ URL.

```text
KahoiiMusic-Expo/
├── assets/                  # Biểu tượng AppIcon, Logo chính thức KahoiiMusic
├── src/
│   ├── types/
│   │   └── music.ts         # Khai báo kiểu PlaylistItem, PlaybackState, GradientPalette
│   ├── services/
│   │   ├── YouTubeResolver.ts # Bộ giải mã luồng phát YouTube InnerTube + Tìm kiếm + Playlist
│   │   └── StorageService.ts  # Quản lý lưu trữ AsyncStorage (Lịch sử, Yêu thích, Hàng đợi)
│   ├── context/
│   │   └── PlayerContext.tsx  # Quản lý toàn bộ State trình phát, expo-av Sound, Sleep timer
│   ├── components/
│   │   ├── DynamicGradient.tsx      # Nền Gradient đa sắc mượt mà
│   │   ├── SearchPillBar.tsx        # Thanh tìm kiếm dạng viên thuốc kèm nút dán link
│   │   ├── ArtworkView.tsx          # Hiển thị ảnh bìa bo góc với hiệu ứng phát sáng
│   │   ├── TrackInfoSection.tsx     # Tên bài hát, ca sĩ, nút tim yêu thích
│   │   ├── EqualizerBars.tsx        # Sóng nhạc Equalizer nhảy theo điệu nhạc
│   │   ├── MusicTrackBar.tsx        # Thanh tua bài hát và thời gian
│   │   ├── PlaybackControls.tsx     # Bộ nút Shuffle, Prev, Play/Pause, Next, Loop
│   │   ├── VolumeSlider.tsx         # Thanh trượt âm lượng
│   │   └── BottomActions.tsx        # Các nút Danh sách, Yêu thích, Lịch sử
│   ├── sheets/
│   │   ├── SearchResultsSheet.tsx   # Modal tìm kiếm bài hát YouTube
│   │   ├── PlaylistSheet.tsx        # Modal danh sách chờ phát nhạc
│   │   ├── FavoritesSheet.tsx       # Modal các bài hát đã thích
│   │   ├── HistorySheet.tsx         # Modal lịch sử các bài đã nghe
│   │   └── SleepTimerModal.tsx      # Modal hẹn giờ tắt nhạc
│   └── views/
│       └── MainPlayerScreen.tsx     # Màn hình phát nhạc chính kết hợp toàn bộ thành phần
├── App.tsx                  # Điểm khởi chạy ứng dụng (Entry point)
├── app.json                 # Cấu hình Expo, quyền Audio Background, Icon
└── package.json             # Danh sách gói phụ thuộc Expo tương thích
```

