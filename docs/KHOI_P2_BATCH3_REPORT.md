# BÁO CÁO P2 — BATCH 3: LANDING PAGE VISUAL 3D

> **Chủ nhiệm phần việc:** Đinh Tuấn Khôi  
> **Thời gian:** 2026-08-04  
> **Commit mục tiêu:** `feat(frontend): elevate landing page with accessible 3d visuals`  
> **Phạm vi:** Frontend only  

---

## 1. Tóm tắt kết quả (Summary)

- **Mục tiêu:** Nâng cấp landing page thành giao diện visual 3D hiện đại, sang trọng, có chiều sâu đồng bộ với định hướng SprintA.
- **Trạng thái:** `PASS`
- **Thay đổi Backend:** **KHÔNG** (Không sửa Backend, Controller, DbContext, Migration, Db, hay contract API).
- **Dependency mới:** **KHÔNG** (Sử dụng thuần Vue 3 + CSS 3D Perspective + CSS Custom Properties, zero bundle bloat).

---

## 2. Chi tiết các file đã chỉnh sửa (Modified Files)

1. `Frontend/src/views/Home.vue`
   - Bổ sung hệ thống CSS 3D (`perspective`, `transform-style: preserve-3d`, dynamic Z-layering).
   - Thêm handler `setSpotlight` và `resetSpotlight` tính toán góc nghiêng 3D (`--tilt-x`, `--tilt-y`) và vị trí ánh sáng (`--spot-x`, `--spot-y`) mượt mà theo vị trí con trỏ chuột.
   - Thêm hiệu ứng 3D layer depth cho Hero Dashboard preview, context badge, live indicator strip, product cards, AI panel, pricing cards và final CTA card.
   - Đảm bảo hiển thị đúng dữ liệu Pricing và AI credits từ API `/public/pricing` và `/ai/usage-summary`.
   - Giữ nguyên trạng thái `Pending` ("Chưa công bố" / "Not published") khi `MonthlyPriceVnd` là `null`, tuyệt đối không tự đặt giá bán giả.
   - Bổ sung focus visible styling cho accessibility (`outline: 3px solid var(--accent)`).
   - Thêm fallback an toàn cho `prefers-reduced-motion: reduce` và các thiết bị cảm ứng (`hover: none`).

2. `Frontend/src/components/landing/ProductVideoSection.vue`
   - Bổ sung 3D spotlight tilt và viền ánh sáng glassmorphism cho khung video demo sản phẩm.
   - Nút Play và poster video được nâng độ cao không gian Z (`translateZ(35px)` và `translateZ(45px)` trên hover).
   - Thêm media query fallback cho `prefers-reduced-motion` và màn hình cảm ứng.

3. `docs/KHOI_MASTER_PLAN.md`
   - Cập nhật dòng bảng điều hướng trạng thái cho P2 Landing 3D sang `PASS`.

4. `docs/KHOI_P2_BATCH3_REPORT.md`
   - Báo cáo hoàn tất batch P2 theo chuẩn quy định.

---

## 3. UI Changes & Visual Features

1. **Hero 3D Perspective Stage:**
   - Khung hình Dashboard SprintA có góc nhìn chiều sâu (`perspective: 1400px`, `rotateX`, `rotateY`).
   - Các phần tử phụ trợ nổi bật theo tầng Z: Badge gợi ý thông minh (`translateZ(48px)`), Live strip (`translateZ(36px)`), Orbits quỹ đạo (`translateZ(-30px)` và `translateZ(55px)`).
   - Lớp phản chiếu ánh sáng và bóng đổ chiều sâu mượt mà khi di chuột.

2. **Interactive 3D Spotlight Cards:**
   - Card tính năng (Product Cards), AI Panel, Card bảng giá (Price Cards) và Final CTA nâng chiều sâu Z và nghiêng nhẹ theo góc di chuyển chuột.
   - Hiệu ứng vệt sáng quét 3D (radial spotlight highlight) theo tâm con trỏ.

3. **Usage & Pricing Transparency:**
   - Lấy dữ liệu gói và AI credits trực tiếp từ server backend hiện có.
   - Các gói chưa duyệt giá duy trì nhãn `Chưa công bố` / `Not published`.
   - Không có tính năng thanh toán hay subscription giả lập.

4. **Accessibility & Responsive Controls:**
   - `prefers-reduced-motion: reduce` và màn hình touch (`hover: none`) sẽ tắt hiệu ứng nghiêng 3D, trả về giao diện phẳng mượt mà, chống chóng mặt/jank.
   - Điều hướng bàn phím (`focus-visible`) có viền tương phản cao rõ ràng trên tất cả nút bấm và thẻ tương tác.
   - `overflow-x: clip` loại bỏ hoàn toàn hiện tượng vỡ khung ngang trên các kích thước màn hình.

---

## 4. Kiểm thử (Test Verification Results)

- **Desktop (1920×1080 / 1440×900):** `PASS` (Smooth 3D tilt & lighting reflection).
- **Tablet (768×1024 / 820×1180):** `PASS` (Grid co giãn chuẩn, 3D tilt mượt).
- **Mobile (390×844 iPhone 12/13/14 Pro):** `PASS` (Menu mobile hoạt động chuẩn, không overflow ngang, 3D tilt phẳng hóa mượt trên touch screen).
- **Light / Dark mode:** `PASS` (Hệ màu đồng bộ với SprintA design token).
- **Keyboard & Focus navigation:** `PASS` (Tab key chuyển focus rõ ràng, viền outline sáng).
- **Console error mới:** `0` (Không có lỗi console nào phát sinh).
- **Frontend Production Build:** `PASS` (`npm run build`).

---

## 5. Danh sách file thay đổi báo cáo Git (Git Diff File Summary)

```text
Frontend/src/components/landing/ProductVideoSection.vue
Frontend/src/views/Home.vue
docs/KHOI_MASTER_PLAN.md
docs/KHOI_P2_BATCH3_REPORT.md
```
