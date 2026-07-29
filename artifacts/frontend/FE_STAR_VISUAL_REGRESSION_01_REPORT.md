# FE-STAR-VISUAL-REGRESSION-01

## Phạm vi

- Audit commit `28feb068` và bản `StarredDropdown.vue` trước commit; không restore nguyên file cũ.
- Chỉ sửa frontend Starred/Recent và các nút star/unstar liên quan ở Project, WorkTask, Goal và Space.
- Không đổi backend, API contract, migration, persistence, Channel, DM, SignalR hoặc dependency.

## Kết quả

- Reset đầy đủ native button cho các star action: `appearance`, `background`, `border`, `color`, `font`, `padding`, `cursor`; thêm vùng chạm 40px, `touch-action` và `focus-visible`.
- Dropdown/popup có khung ổn định giữa loading, empty, error và content; error có retry; icon pending giữ kích thước cố định.
- Item có thể mở bằng mouse, Enter và Space; star action luôn hiển thị trên thiết bị không hover.
- Layout dùng `min-width: 0`, `box-sizing`, width theo viewport và breakpoint 390px để tránh overflow.
- Light/dark dùng token Foundation hiện có và `color-mix`, không thêm design system mới.
- Chuẩn hóa key entity trong starred store và dùng cache `isStarred()` thay vì riêng mảng trang hiện tại; các màn tổng hợp nạp tối đa 100 trạng thái để tránh sai hiển thị khi phân trang.
- Mutation chỉ cập nhật state sau khi API thành công; lỗi được giữ lại để render/notify và `Goal` không còn nuốt lỗi như thành công.

## Verification

- `npm run build`: PASS (Vite production build, exit code 0).
- Cảnh báo không chặn build: PWA/Rolldown `generateBundle` và chunk size hiện hữu.
- `git diff --check`: PASS.
- Browser smoke test ở viewport 390px: route bảo vệ chuyển về login; không kiểm thử trực quan authenticated để tránh dùng thông tin đăng nhập.
- Source audit responsive: dropdown `calc(100vw - 24px)`, sheet `min(400px, 100vw)`, mobile padding/gap và grid `minmax(0, 1fr)`.

## Ghi chú

- Hai thay đổi có sẵn trước task ở `Backend/src/TaskManagement.API/appsettings.json` và `Frontend/src/composables/useI18n.js` được giữ nguyên và không đưa vào commit này.
