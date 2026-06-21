# 🎮 Trình Tối Ưu Hóa RAM Roblox (RobloxRAM Optimizer)

RobloxRAM Optimizer là một công cụ hữu ích được thiết kế dành riêng cho các game thủ/farmer chạy cùng lúc nhiều tab game Roblox (50+ tabs). Giao diện Web Dashboard hiện đại sử dụng công nghệ Glassmorphic, real-time WebSocket updates và chạy hoàn toàn Portable.

---

## ✨ Các Tính Năng Nổi Bật

1. **🛡️ 3 Cấp Độ Tối Ưu Hóa RAM (Performance-Safe):**
   - **Tầng 1 (Safe - Mặc định/Tự động):** Tự động phát hiện và gửi gợi ý Working Set cho Windows để thu hồi các trang nhớ "lạnh" không được truy cập. **Cam kết 100% không làm giảm FPS/hiệu năng của game.**
   - **Tầng 2 (Moderate):** Đặt giới hạn làm việc ở mức vừa phải kèm tối ưu hóa luồng CPU cho các tab chạy ẩn (background processes).
   - **Tầng 3 (Aggressive - Thủ công):** Ép Windows giải phóng toàn bộ vùng nhớ (EmptyWorkingSet). *Lưu ý: Có thể gây lag 2-5 giây khi bạn tương tác lại với game.*

2. **🔒 Giới Hạn RAM Cứng (RAM Limit) qua Windows Job Objects:**
   - Đặt giới hạn RAM cụ thể (300MB, 500MB, 800MB, 1GB hoặc Custom) cho từng tab Roblox riêng biệt hoặc áp dụng làm mặc định cho tất cả các tabs mới mở.
   - Khi tab Roblox đạt giới hạn, Windows tự động kiểm soát lượng RAM của process đó không cho vượt ngưỡng, tránh tình trạng tràn bộ nhớ RAM (OOM) làm treo đơ máy phụ.

3. **❄️ Đóng Băng / Giải Phóng CPU (Suspend/Resume):**
   - Tạm dừng (đóng băng hoàn toàn) Roblox process để giảm mức sử dụng CPU/RAM về gần như bằng 0.
   - Tích hợp **bộ đếm giờ tự động khôi phục (Auto-resume timer)** đề phòng game bị kick khỏi server do treo quá lâu.

4. **🗑️ Tối Ưu Cấp Hệ Thống (System-level):**
   - Giải phóng Standby Cache của Windows.
   - Ghi dữ liệu đã sửa đổi trên RAM xuống ổ đĩa (Flush Modified Page List) để tăng dung lượng RAM trống vật lý.

5. **📊 Real-time Dashboard:**
   - Dashboard web hiện đại hiển thị danh sách các tab Roblox, dung lượng RAM sử dụng thực tế, CPU, trạng thái và uptime cập nhật liên tục mỗi 2 giây.
   - Có thể sắp xếp, lọc, tìm kiếm và chọn nhiều tab để thực hiện thao tác hàng loạt (Bulk Action).

---

## 🚀 Hướng Dẫn Sử Dụng Nhanh

### Chạy ứng dụng:
1. Đảm bảo máy tính của bạn chạy Windows 10 hoặc Windows 11 (đã cài sẵn .NET Framework 4.8). Không cần cài đặt Python hay bất cứ phần mềm bên thứ ba nào khác.
2. Click đúp chuột vào file `RobloxRAM_Optimizer_v5.exe`.
3. Hộp thoại UAC hiện lên hỏi quyền Administrator, hãy chọn **Yes** (Ứng dụng cần quyền Admin để quản lý RAM, Job Objects và tiến trình Roblox).
4. Sau khi khởi chạy, một icon màu tím sẽ xuất hiện ở khay hệ thống (System Tray). Giao diện Dashboard sẽ tự động mở lên trong một **cửa sổ ứng dụng độc lập (Edge App Mode)**. Để mở lại, click đúp vào icon ở khay hệ thống hoặc click chuột phải chọn **Mở Dashboard**.

---

## 📦 Hướng Dẫn Chuyển Sang Máy Phụ (Portable Migration)

Ứng dụng được thiết kế hoàn toàn độc lập, siêu nhẹ (file EXE chỉ khoảng 58 KB) và không có bất kỳ thư viện phụ thuộc nào khác ngoài Windows Standard API. Để chuyển sang máy phụ:

1. **Copy file `RobloxRAM_Optimizer_v5.exe`** sang máy phụ.
2. Chạy trực tiếp file `RobloxRAM_Optimizer_v5.exe` với quyền Administrator. Ứng dụng tiêu tốn chưa đến 10MB RAM ở chế độ nền.

---

## ⚠️ Khuyến Cáo An Toàn & Bảo Mật

- Tool này **KHÔNG** can thiệp, sửa đổi hay chèn (inject) bất cứ đoạn mã code nào vào trong bộ nhớ trong của Roblox. Do đó nó cực kỳ an toàn và không gây lỗi phát hiện hack/anti-cheat của Roblox.
- Tuy nhiên, hãy thận trọng khi sử dụng tính năng **Freeze (Suspend)** hoặc **RAM Limit quá thấp (dưới 200MB)** vì có thể khiến client Roblox của bạn bị crash hoặc mất kết nối mạng với máy chủ.
