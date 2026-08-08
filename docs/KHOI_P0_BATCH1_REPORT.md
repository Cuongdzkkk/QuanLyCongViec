# KHOI P0 — BATCH 1 REPORT

Ngày: 2026-08-04
Branch: `KhoiSigma`
Phạm vi: Integration Hub + Intakes

## Decision

P0 — BATCH 1: PASS.

Phần nội bộ của Integration Hub, Unified Inbox và Intakes đã có authorization,
focused tests, full test, frontend build và runtime A/B/C.

OAuth nhà cung cấp ngoài hệ thống:

`BLOCKED_EXTERNAL`

Không tuyên bố Google Calendar, Gmail hoặc Slack OAuth PASS khi chưa có
credential và console configuration thật.

## Live Git baseline

- HEAD trước batch: `0606c6928f2452c44ad1851842483c34071c7634`.
- Branch: `KhoiSigma`.
- Branch trước batch: ahead `origin/KhoiSigma` 39 commit.
- Local private config có sẵn: `Backend/src/TaskManagement.API/appsettings.json`.
- Master plan có sẵn nhưng chưa tracked: `docs/KHOI_MASTER_PLAN.md`.
- Ba stash có sẵn và vẫn được giữ nguyên.
- Không pull, reset, clean, pop/drop stash hoặc push.

## Fixed

### Intakes

- GET Intakes yêu cầu active workspace và active project membership.
- POST Intake yêu cầu active membership.
- Guest/Stakeholder không được ghi dữ liệu.
- Accept/Decline yêu cầu quyền `project.write`.
- Backend lấy user từ JWT, không nhận role hoặc user ID từ frontend.
- Outsider bị chặn 403 trước khi controller action chạy.
- API trả `canCreate` và `canReview` từ authorization backend thật.
- UI chỉ hiện nút tạo khi `canCreate=true`.
- UI chỉ hiện Accept/Decline khi `canReview=true`.
- UI hiển thị trạng thái từ chối truy cập thay vì bảng rỗng gây hiểu nhầm.
- Status review chỉ nhận `Accepted` hoặc `Declined`.
- Title được trim và không được rỗng.
- Title, description và source có giới hạn độ dài.
- Priority chỉ nhận 1–4.
- Source chỉ nhận FORM, MANUAL, EMAIL hoặc API.
- Desired due date không được nằm trong quá khứ.
- Project inactive, deleted, archived hoặc workspace deleted không nhận Intake.
- Accept yêu cầu project có default `TO DO` status và task type.
- Không tạo task với foreign key `Guid.Empty`.
- Intake status, reviewer, project sequence và task được lưu trong một SaveChanges.
- Intake đã review trả 409 và không tạo task lần hai.

### Integration Hub và Unified Inbox

- Integration account được lọc theo current JWT user.
- Sync history được lọc theo current JWT user.
- Inbox list/detail/mark-read được lọc theo current JWT user.
- Người dùng không thể mark-read Inbox item của user khác.
- Danh sách project tạo task chỉ chứa active authorized memberships.
- Project inactive/deleted/archived/workspace deleted không xuất hiện trong options.
- Tạo một hoặc nhiều task từ Inbox kiểm tra project membership trước.
- Outsider tạo task vào project ngoài quyền nhận 403 thay vì lỗi runtime 500.
- OAuth state được ký bằng Data Protection.
- OAuth state có thời hạn 10 phút.
- OAuth state bị sửa giả bị từ chối trước token exchange.
- Callback từ chối liên kết provider cho user inactive/deleted.
- Token provider không được trả về response hoặc report.

## Bảng chức năng bắt buộc

| Tên Module | Chức năng | Xem được | Có thể sử dụng | Test | Ghi chú |
|---|---|---:|---:|---|---|
| Integration Hub | Xem danh sách integration | Có | Có | PASS | Dữ liệu theo JWT user |
| Integration Hub | Xem trạng thái provider | Có | Có | PASS | Không trả token |
| Integration Hub | Connect provider | Có | Chưa | BLOCKED_EXTERNAL | Cần credential/console |
| Integration Hub | Disconnect provider | Có | Có | PASS | Chỉ account của current user |
| Integration Hub | Sync từng provider | Có | Chưa | BLOCKED_EXTERNAL | Cần provider thật |
| Integration Hub | Sync tất cả provider | Có | Chưa | BLOCKED_EXTERNAL | Cần provider thật |
| Integration Hub | Xem sync history | Có | Có | PASS | Cách ly theo user |
| Integration Hub | Unified Inbox | Có | Có | PASS | Cách ly theo user |
| Integration Hub | Lọc theo nguồn | Có | Có | PASS | Query/UI hiện hữu |
| Integration Hub | Lọc theo trạng thái | Có | Có | PASS | UI hiện hữu |
| Integration Hub | Xem chi tiết Inbox | Có | Có | PASS | Owner-only |
| Integration Hub | Đánh dấu đã đọc | Có | Có | PASS | Cross-user trả 404 |
| Integration Hub | Tạo task từ Inbox | Có | Có | PASS | Active project member |
| Integration Hub | Tạo nhiều task từ Inbox | Có | Có | PASS | Project authorization trước batch |
| Integration Hub | AI tóm tắt | Có | Có điều kiện | PASS_INTERNAL | Phụ thuộc AI provider/quota |
| Integration Hub | AI gợi ý task | Có | Có điều kiện | PASS_INTERNAL | Owner-only trước AI call |
| Intakes | Xem danh sách Intake | Có | Có | PASS | Manager/Member; Outsider 403 |
| Intakes | Tạo Intake | Có | Có | PASS | Active member |
| Intakes | Xem chi tiết Intake | Có | Có | PASS | Theo quyền list |
| Intakes | Chọn priority | Có | Có | PASS | Backend range 1–4 |
| Intakes | Chọn desired due date | Có | Có | PASS | Không nhận ngày quá khứ |
| Intakes | Accept Intake | Có theo quyền | Có | PASS | Manager; Member 403 |
| Intakes | Decline Intake | Có theo quyền | Có | PASS | Chỉ project.write |
| Intakes | Tạo task khi Accept | Có | Có | PASS | Default status/type bắt buộc |
| Intakes | Mở task đã tạo | Có | Có | PASS | Giữ route hiện hữu |

## Runtime A/B/C

| Actor | Vai trò | Evidence | Kết quả |
|---|---|---|---|
| USER_A | Manager / PM | Create, invalid transition, Accept, task persistence | PASS |
| USER_B | Member / Developer | List, create; review bị 403; Inbox task được tạo | PASS |
| USER_C | Outsider | Intakes GET/POST 403; project options rỗng; Inbox task 403 | PASS |

Runtime dùng ASP.NET test host, JWT thật của test, middleware authorization,
controller, EF Core InMemory và HTTP request/response đầy đủ.

## Backend build/tests

- Focused P0 tests: 3/3 PASS.
- Full backend tests: 290/290 PASS.
- Failed: 0.
- Skipped: 0.
- Target framework: net10.0.

## Frontend build

- `npm run build`: PASS.
- 4531 modules transformed.
- PWA artifacts generated.
- Cảnh báo vite-plugin-pwa/Rolldown: non-blocking known warning.
- Chunk-size warning: non-blocking, không xử lý ngoài scope P0.

## External blockers

`BLOCKED_EXTERNAL`

- Google Calendar: cần OAuth client, secret, consent và callback configuration.
- Gmail: cần OAuth client, secret, consent và callback configuration.
- Slack: cần Slack App, scopes, client secret và callback configuration.
- Không lưu credential vào report hoặc source.

## Migration

- Không thay đổi entity hoặc schema.
- Không tạo migration.
- P0 không yêu cầu database migration.

## Security

- Resource-level authorization: PASS.
- Cross-user Integration/Inbox isolation: PASS.
- Cross-project task creation guard: PASS.
- OAuth state integrity/expiry: PASS.
- Secret/token output: không có.
- Private appsettings: không stage, không commit.

## Files changed

- `Backend/src/TaskManagement.API/Controllers/IntakesController.cs`
- `Backend/src/TaskManagement.API/Controllers/InboxController.cs`
- `Backend/src/TaskManagement.API/Controllers/IntegrationsController.cs`
- `Backend/TaskManagement.Tests/Logic/P0ModuleAuthorizationTests.cs`
- `Frontend/src/components/IntakeInbox.vue`
- `docs/KHOI_MASTER_PLAN.md`
- `docs/KHOI_P0_BATCH1_REPORT.md`

## Deferred

- Provider OAuth runtime thật: `BLOCKED_EXTERNAL`.
- AI credit, Landing 3D, payment, typing/presence và voice/video: ngoài P0.

## Push

- Không push.
- Commit local theo yêu cầu của batch.
