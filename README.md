# KahoiiMusic - YouTube Sync (Extension)

Tiện ích mở rộng trình duyệt (Chrome, Edge, Cốc Cốc, Brave) dành cho **KahoiiMusic**. Cho phép bạn đăng nhập và nhanh chóng nạp các bài hát yêu thích trên YouTube trực tiếp vào danh sách **"Đã lưu"** trong tài khoản đám mây KahoiiMusic.

---

## 🌟 Tính năng nổi bật

1. **Đăng nhập tài khoản KahoiiMusic:**
   - Dùng chung tài khoản Email / Mật khẩu với ứng dụng Desktop & Android.
   - Cơ chế bảo mật và đồng bộ trực tiếp qua Firebase Auth & Firestore.
   - Phiên đăng nhập được lưu tự động, tự làm mới Token (Refresh Token).

2. **Nạp nhạc trực tiếp từ YouTube:**
   - **Cách 1: Nút bấm trên YouTube**
     - Khi bạn đang xem video bất kỳ trên YouTube, extension tự động thêm nút **"🎵 Nạp vào Kahoii"** ngay cạnh các nút Like / Share của YouTube.
     - Bấm 1 click là bài hát được thêm vào tài khoản, có thông báo góc màn hình.
   - **Cách 2: Popup Extension**
     - Bấm vào biểu tượng KahoiiMusic trên thanh tiện ích của trình duyệt.
     - Tự động nhận diện video đang phát (Thumbnail, Tiêu đề, Kênh, Video ID).
     - Bấm nút **"⚡ Nạp vào KahoiiMusic"**.
     - Có ô dán link YouTube thủ công nếu bạn muốn nạp link từ tab khác.

3. **Xem danh sách bài đã lưu:**
   - Xem nhanh số lượng bài hát và 5 bài vừa nạp gần nhất.
   - Tự động đồng bộ với KahoiiMusic trên máy tính khi bạn mở app lên.

4. **Siêu nhẹ & Tiết kiệm RAM:**
   - Xây dựng theo chuẩn **Manifest V3**.
   - Chạy theo cơ chế ngủ đông (Event-driven Service Worker) $\rightarrow$ **0 MB RAM** khi không dùng đến.

---

## 🚀 Hướng dẫn cài đặt vào trình duyệt (Chỉ mất 30 giây)

### Bước 1: Mở trang quản lý tiện ích của trình duyệt
* **Google Chrome / Cốc Cốc:** Mở tab mới, gõ vào thanh địa chỉ: `chrome://extensions`
* **Microsoft Edge:** Mở tab mới, gõ vào thanh địa chỉ: `edge://extensions`
* **Brave:** Mở tab mới, gõ vào thanh địa chỉ: `brave://extensions`

### Bước 2: Bật "Chế độ dành cho nhà phát triển" (Developer mode)
* Gạt công tắc **Chế độ dành cho nhà phát triển** (góc trên bên phải màn hình) sang trạng thái **BẬT (ON)**.

### Bước 3: Tải tiện ích vào trình duyệt
* Bấm vào nút **Tải tiện ích đã giải nén** (hoặc **Load unpacked**).
* Chọn đúng thư mục:
  ```
  ...\KahoiiMusic-Extension
  ```
* Bấm **Select Folder** (Chọn thư mục).

### Bước 4: Ghim Extension lên thanh công cụ
* Bấm vào biểu tượng mảnh ghép (Extensions) trên góc phải trình duyệt $\rightarrow$ Bấm nút Ghim (Pin) cạnh **KahoiiMusic - Đồng Bộ YouTube**.

---

## 🎯 Cách sử dụng

1. **Đăng nhập:**
   - Bấm vào biểu tượng KahoiiMusic trên thanh công cụ.
   - Điền Email & Mật khẩu tài khoản KahoiiMusic của bạn $\rightarrow$ Bấm **Đăng nhập**.

2. **Nạp nhạc:**
   - Mở bất kỳ bài hát nào trên YouTube (`youtube.com/watch?v=...`).
   - Bấm nút **"🎵 Nạp vào Kahoii"** ngay trên giao diện YouTube, hoặc mở Popup extension rồi bấm **"⚡ Nạp vào KahoiiMusic"**.
   - Mở ứng dụng KahoiiMusic trên máy tính $\rightarrow$ Bài hát đã có ngay trong mục **"Đã lưu"**!

