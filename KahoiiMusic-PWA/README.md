# 🎵 KahoiiMusic - Progressive Web App (PWA)

Phiên bản độc lập **PWA (Progressive Web App)** của KahoiiMusic, được xây dựng bằng **React 19 + TypeScript + Vite**. Ứng dụng hoạt động mượt mà trên mọi thiết bị (iPhone, iPad, Android, Máy tính) và có thể **cài đặt trực tiếp lên Màn hình chính (Add to Home Screen)** mà không cần qua App Store hay Google Play.

---

## 🌟 Tính năng nổi bật

1. **Phát nhạc nền khi khóa màn hình (Background Audio trên iOS & Android):**
   - Hoàn toàn vượt qua hạn chế của Expo Go trên iOS!
   - Khi mở link trên **Safari** và bấm **"Thêm vào Màn hình chính (Add to Home Screen)"**, iOS sẽ cấp quyền phát nhạc nền HTML5 và hiển thị widget điều khiển trên **Control Center & Màn hình khóa** (MediaSession API).
2. **Siêu nhẹ & Tiết kiệm RAM:**
   - Kích thước build sản phẩm siêu gọn (~80KB gzipped).
   - Không bị gánh nặng bởi React Native runtime hay bridge trên trình duyệt.
   - 0 animation ngốn RAM, giao diện Glassmorphism tối màu sang trọng.
3. **Giải mã luồng phát YouTube chính thống:**
   - Tự động trích xuất trực tiếp luồng AAC (m4a) 130kbps nguyên bản từ YouTube qua VisionOS / Android VR InnerTube client.
   - Tích hợp sẵn proxy backend triệt tiêu 100% lỗi CORS của trình duyệt.
4. **Offline Service Worker:**
   - Hoạt động ngoại tuyến với bộ nhớ đệm `sw.js`.
5. **Đầy đủ tính năng:**
   - Tìm kiếm bài hát YouTube theo thời gian thực.
   - Dán nhanh liên kết YouTube từ Clipboard.
   - Hàng đợi bài hát (Queue), Yêu thích (Favorites), Lịch sử nghe (History).
   - Hẹn giờ tắt nhạc (Sleep Timer: 15p, 30p, 45p, 60p, 90p).
   - Trộn bài (Shuffle), Lặp lại 1 bài (Loop One), Lặp lại toàn bộ (Loop All).

---

## 🚀 Hướng dẫn chạy thử nghiệm

### 1. Cài đặt thư viện
```bash
cd KahoiiMusic-PWA
npm install
```

### 2. Chạy ở chế độ Phát triển (Development)
```bash
npm run dev
```
Trình duyệt sẽ mở tại `http://localhost:3000` (hoặc mở trên điện thoại qua địa chỉ IP máy tính: `http://<IP_MÁY_TÍNH>:3000` khi chung Wi-Fi).

### 3. Biên dịch và chạy Server Production
```bash
# Biên dịch mã nguồn tối ưu
npm run build

# Khởi động server PWA độc lập
npm run serve
```

---

## 📱 Hướng dẫn Cài đặt lên Màn hình chính

- **Trên iPhone / iPad (Safari):**
  1. Mở trang web trong trình duyệt **Safari**.
  2. Bấm vào nút **Chia sẻ** (biểu tượng hình vuông có mũi tên đi lên).
  3. Cuộn xuống chọn **"Thêm vào Màn hình chính" (Add to Home Screen)**.
  4. Đặt tên và bấm **Thêm**. Ứng dụng sẽ có biểu tượng riêng trên màn hình và phát nhạc nền khi tắt màn hình!

- **Trên Android (Chrome):**
  1. Mở trang web trong **Chrome**.
  2. Bấm vào menu 3 chấm góc trên bên phải.
  3. Chọn **"Cài đặt ứng dụng"** (Install App).

- **Trên Máy tính (Chrome / Edge):**
  1. Bấm vào biểu tượng cài đặt trên thanh địa chỉ URL của trình duyệt.

