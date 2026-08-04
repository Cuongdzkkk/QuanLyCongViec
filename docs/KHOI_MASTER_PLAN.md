# KHOI MASTER PLAN — QUẢN LÝ CÔNG VIỆC

> **Chủ nhiệm phần việc:** Đinh Tuấn Khôi
> **Cập nhật:** 2026-08-04
> **Repo làm việc:** `C:\Users\phucl\OneDrive\Desktop\QLCV_2\QuanLyCongViec`
> **Branch mục tiêu:** `KhoiSigma`
> **Mục đích:** Giúp Codex/Antigravity/Kombai đọc một lần, hiểu toàn bộ bối cảnh, làm theo batch lớn, giảm token và tránh sửa đi sửa lại.

---

## 1. Nguyên tắc sử dụng file này

1. AI phải đọc toàn bộ file trước khi sửa code.
2. AI phải kiểm tra **live Git state** trước khi tin bất kỳ commit, test count hoặc worktree state nào trong tài liệu.
3. Không lặp lại audit đã có nếu source không đổi.
4. Không mở rộng phạm vi sang task khác.
5. Gặp việc cần cấu hình hoặc thao tác tay bên ngoài thì ghi `BLOCKED_EXTERNAL`, tiếp tục phần còn lại.
6. Chỉ sửa lỗi có reproduction rõ.
7. Không push nếu chưa được Khôi yêu cầu rõ ràng.
8. Không dùng:
   - `git pull`
   - `git reset --hard`
   - `git clean`
   - `git add .`
   - `git add -A`
   - `git stash pop`
   - `git stash drop`
   - `npm audit fix`
   - `npm audit fix --force`
   - `dotnet ef database drop`
9. Stage từng file cụ thể.
10. Không in, commit hoặc đưa vào báo cáo:
    - API key
    - Client secret
    - JWT
    - Google credential
    - GitHub token
    - Connection string đầy đủ
    - Password
    - Cookie
    - Refresh token

---

## 2. Nhiệm vụ chính thức của Khôi

Theo file phân công, Khôi phụ trách **2 module**:

1. **Integration Hub**
2. **Intakes**

Mục tiêu trước mắt:

- Liệt kê chức năng của từng module.
- Xác định có giao diện để xem hay không.
- Xác định có sử dụng thật được hay không.
- Test `PASS` hoặc `FAIL`.
- Ghi rõ lỗi, blocker và tình trạng phân quyền.
- Ưu tiên hoàn thiện phân quyền.

Bảng báo cáo bắt buộc:

| Tên Module | Chức năng | Xem được | Có thể sử dụng | Test | Ghi chú |
|---|---|---:|---:|---|---|

Nhiệm vụ cải tiến thêm của Khôi:

- Cải tiến AI.
- Cải tiến landing page.
- Landing page có chiều sâu/3D.
- Hiển thị giá plan.
- Hiển thị lượng AI credit.
- Hiển thị số credit người dùng đã dùng và còn lại.
- Chức năng mua/thêm credit chỉ làm khi đã có quyết định giá và payment provider.

---

## 3. Trạng thái dự án đã hoàn thành

> Đây là baseline từ các báo cáo đã hoàn tất trong cuộc trò chuyện. AI vẫn phải xác minh live repo trước khi tiếp tục.

### 3.1 Collaboration đã hoàn thiện

Đã hoàn thành và có runtime/test evidence:

- Channel discovery/create.
- Channel message lưu SQL.
- Direct Message find/create/send/history.
- SignalR realtime cho Channel và DM.
- Reconnect, group isolation và account switch.
- Deduplicate theo `messageId`.
- Read/unread riêng từng user.
- Mark-read idempotent, cursor không lùi.
- Attachment private.
- Metadata attachment lưu SQL.
- Download kiểm tra quyền mỗi lần.
- Chặn SVG, executable, giả MIME và path traversal.
- Mention bằng internal UserId.
- Mention notification lưu SQL.
- SignalR mention gửi riêng theo user.
- Runtime fixture `USER_A / USER_B / USER_C`.
- USER_C bị chặn đúng quyền.
- Cleanup fixture theo run ID, idempotent.

### 3.2 Các kết quả gần nhất đã được báo cáo

- Backend từng đạt `287/287 tests PASS`.
- Frontend production build từng `PASS`.
- Collaboration runtime A/B/C từng `PASS`.
- Browser smoke 390 px light/dark từng `PASS`.
- 0 migration pending tại checkpoint đã báo cáo.
- Worktree từng sạch.
- Stash local từng còn nguyên.
- Chưa push các commit mới tại các checkpoint trước.

### 3.3 Các commit quan trọng đã biết

- `8fd2f742` — local multi-user runtime harness
- `9cf85c0a` — persist per-user read state
- `f0e218d1` — connect collaboration unread state
- `eb2cd719` — persist authorized message attachments
- `edc5071a` — connect collaboration attachments
- `62315460` — persist authorized channel mentions
- `e41f01a6` — connect channel mentions and notifications
- `a5c21efe` — authentication and release checkpoint report

> Có thể đã có commit mới hơn sau thời điểm trên. Luôn dùng `git log` để xác minh.

---

## 4. Trạng thái hiện tại cần chú ý

### 4.1 OAuth

Google và GitHub hiện chưa được chứng minh runtime thật.

Lỗi cũ:

```text
TypeError: tr is not a function
```

đã được sửa theo ảnh kiểm tra gần nhất vì trang không còn crash.

Trạng thái mới:

```text
Google Sign-In chưa được cấu hình.
GitHub OAuth chưa được cấu hình.
```

Điều này nghĩa là UI fallback đang hoạt động, nhưng credential/runtime configuration chưa sẵn sàng.

Xếp OAuth xuống cuối vì cần thao tác tay bên ngoài:

- Google Cloud Console.
- Google OAuth Client ID.
- Authorized JavaScript origin.
- GitHub OAuth App.
- GitHub Client ID.
- GitHub Client Secret.
- Callback URL chính xác.

Không tiếp tục đốt token cho OAuth khi thiếu credential hoặc console configuration.

### 4.2 Stash

Stash local đã biết chứa:

```text
Backend/src/TaskManagement.API/appsettings.json
Frontend/src/composables/useI18n.js
```

Quy tắc:

- Không `stash pop`.
- Không `stash drop`.
- `appsettings.json` được xem là local configuration/private.
- `useI18n.js` chỉ được review riêng, không tự restore.
- Khi cần, chỉ đọc từng file từ stash.
- Không in secret.

### 4.3 Vite cold-start

Known issue:

```text
KNOWN_DEV_COLD_START_RACE
```

Biểu hiện:

- Lazy route `/login` hoặc layout có thể lỗi tạm thời lúc Vite optimize dependency lần đầu.
- Trang có thể phục hồi sau khi optimization hoàn tất.
- Production build vẫn PASS.

Chỉ sửa khi reproduction ổn định và root cause rõ. Không sửa phỏng đoán.

### 4.4 Cảnh báo dependency

AngleSharp từng có advisory mức Moderate.

AI phải:

- Xác minh advisory còn tồn tại hay đã được xử lý.
- Xác minh thay đổi `.csproj` hiện tại.
- Không nâng dependency hàng loạt.
- Không nâng major version nếu chưa cần.
- Chỉ tạo dependency commit khi full tests/build PASS.

---

## 5. Module Integration Hub

### 5.1 Chức năng đã thấy trong source/UI

- Xem integration.
- Connect/disconnect provider.
- Google Calendar.
- Gmail.
- Slack.
- Sync từng provider.
- Sync tất cả.
- Unified Inbox.
- Lọc theo nguồn.
- Lọc theo trạng thái.
- Đánh dấu đã đọc.
- Xem chi tiết inbox.
- Tạo task từ inbox.
- Tạo nhiều task từ inbox.
- AI tóm tắt.
- AI gợi ý task.

### 5.2 Tình trạng sơ bộ

| Chức năng | Xem được | Có thể sử dụng | Trạng thái sơ bộ |
|---|---:|---:|---|
| Integration list | Có | Có | Cần runtime confirm |
| Unified Inbox | Có | Có | Cần permission matrix |
| Filter nguồn/trạng thái | Có | Có | Cần focused test |
| Mark read | Có | Có | Cần focused test |
| Tạo task từ inbox | Có | Có | Cần verify project membership |
| Google Calendar OAuth | Có | Chưa xác minh | `BLOCKED_EXTERNAL` |
| Gmail OAuth | Có | Chưa xác minh | `BLOCKED_EXTERNAL` |
| Slack OAuth | Có | Chưa xác minh | `BLOCKED_EXTERNAL` |

### 5.3 Việc cần kiểm tra

- Dữ liệu integration có bị lộ giữa các user không.
- Inbox có bị lộ giữa các user không.
- User có thể chọn project ngoài quyền để tạo task không.
- Connect/disconnect có kiểm tra current user không.
- Sync có kiểm tra ownership không.
- API có nhận `userId` từ client hay luôn lấy từ JWT.
- Có endpoint nào chỉ có `[Authorize]` nhưng thiếu resource-level authorization không.
- Có focused tests cho member, manager và outsider chưa.

---

## 6. Module Intakes

### 6.1 Chức năng frontend đã có

- Xem danh sách intake.
- Tạo intake.
- Xem chi tiết intake.
- Chọn priority.
- Chọn desired due date.
- Accept intake.
- Decline intake.
- Tạo task khi Accept.
- Mở task đã tạo.

### 6.2 Rủi ro phân quyền đã xác định

Backend Intakes cần được audit kỹ vì có khả năng chỉ dùng `[Authorize]` mà thiếu resource-level authorization.

Các rủi ro cần kiểm tra:

- User biết `projectId` có đọc được intake của project khác không.
- User có tạo intake vào project khác không.
- User thường có Accept/Decline được không.
- User có tạo task gián tiếp qua Accept không.
- UI có hiển thị nút review cho user không đủ quyền không.
- Status có bị truyền tự do ngoài `Accepted/Declined` không.
- Project inactive có nhận intake không.
- Default TaskStatus/TaskType có tồn tại trước khi tạo task không.

### 6.3 Definition of Done cho Intakes

- Project member phù hợp mới được xem/tạo.
- Reviewer role phù hợp mới được Accept/Decline.
- Outsider bị chặn.
- UI ẩn hoặc disable action theo quyền backend thật.
- Backend không tin role từ frontend.
- Status transition hợp lệ.
- Input validation đầy đủ.
- Task creation atomic.
- Focused unit/integration/SQL tests.
- Runtime A/B/C:
  - Manager
  - Member
  - Outsider
- Có bảng kết quả đúng mẫu phân công.

---

## 7. AI và credit

### 7.1 Phần đã có

- AI chat.
- Context chat.
- Project assistant.
- Action preview/confirm/cancel.
- Attachment.
- Audio transcription.
- File analysis.
- Repository analysis.
- Breakdown task.
- Estimate suggestion.
- Assignee suggestion.
- Conversation persistence.
- Usage ledger.
- Pricing/credit data model ở mức nhất định.
- ZenMux cho text.
- Gemini cho multimodal.

### 7.2 Khoảng trống cần audit

- Frontend/backend field credit có khớp không.
- Có hard-code credit mặc định không.
- Included credit có lấy từ plan thật không.
- Credit consumed/remaining có đúng không.
- Có quota check trước AI action không.
- Hết credit có bị từ chối không.
- Có chuỗi tiếng Việt lỗi mã hóa trong AI UI không.
- Có subscription/user-plan source of truth không.
- Có payment/order/top-up model chưa.

### 7.3 Quy tắc triển khai

Làm ngay:

- Sửa contract usage frontend/backend.
- Sửa lỗi mã hóa text.
- Tính usage đúng.
- Bỏ hard-code khi đã có nguồn plan thật.
- Thêm test ledger/quota.
- Hiển thị credit đúng trên landing/dashboard.

Để cuối:

```text
AI_CREDIT_PURCHASE_DEFERRED
```

khi chưa có:

- Giá chính thức.
- Payment provider.
- Subscription policy.
- Billing cycle.
- Refund policy.
- Webhook design.

Không để AI tự nghĩ giá bán thật.

---

## 8. Landing page

### 8.1 Đã có

- Landing page mới.
- Responsive.
- Animation.
- Pricing section.
- Pricing lấy từ API.
- AI usage section.
- Light/dark visual.
- Workflow/video section.

### 8.2 Còn thiếu

- Visual 3D rõ hơn.
- Credit contract chính xác.
- Giá thật được duyệt.
- Checkout/mua credit.
- Mobile performance.
- Reduced-motion fallback.
- Visual polish đồng bộ.

### 8.3 Hướng 3D ưu tiên

Ưu tiên theo thứ tự:

1. CSS 3D:
   - perspective
   - layered cards
   - subtle tilt
   - parallax
   - depth shadow
   - lighting
2. Motion hiện có.
3. Chỉ dùng Three.js/WebGL khi:
   - Có lợi ích rõ.
   - Bundle không tăng quá lớn.
   - Có fallback mobile.
   - Có reduced-motion.
   - Không làm giảm Lighthouse nghiêm trọng.

Không redesign toàn ứng dụng.

---

## 9. Roadmap tối ưu token

## P0 — BATCH 1: Hoàn thành nhiệm vụ Integration Hub + Intakes

**AI chính:** Codex
**Browser evidence:** Antigravity khi cần

### Phạm vi trong một task

1. Audit live Git.
2. Audit Integration Hub.
3. Audit Intakes.
4. Sửa toàn bộ permission Intakes.
5. Đồng bộ UI action theo quyền thật.
6. Validate status/input.
7. Thêm focused tests Intakes.
8. Thêm focused permission tests Integration Hub/Inbox.
9. Runtime A/B/C.
10. Tạo bảng chức năng đúng mẫu phân công.
11. Build/test/report/commit.
12. OAuth ngoài hệ thống ghi `BLOCKED_EXTERNAL` rồi tiếp tục.

### Không làm trong batch này

- Google/GitHub OAuth console.
- AI credit.
- Landing 3D.
- Payment.
- Typing/presence.
- Voice/video.

### Definition of Done

- Intakes permission PASS.
- Integration Hub internal permission PASS.
- External OAuth được tách blocker.
- Backend full tests PASS.
- Frontend build PASS.
- Runtime evidence đủ.
- Có report cho Khôi.
- Worktree sạch.
- Không push.

---

## P1 — BATCH 2: AI usage + credit + pricing data

**AI chính:** Codex

### Phạm vi trong một task

1. Audit contract AI usage.
2. Sửa field mismatch.
3. Sửa text encoding.
4. Audit plan/entitlement model.
5. Loại hard-code khi có nguồn thật.
6. Tính included/consumed/remaining đúng.
7. Quota guard nếu model đủ.
8. Backend tests.
9. Landing/dashboard hiển thị đúng credit.
10. Không làm payment nếu chưa có quyết định.
11. Build/test/report/commit.

### Blocker rule

Thiếu subscription/payment design:

```text
AI_CREDIT_PURCHASE_DEFERRED
```

Tiếp tục phần usage/credit hiện có, không dừng task.

---

## P2 — BATCH 3: Landing page visual 3D

**AI chính:** Kombai hoặc Antigravity
**Codex chỉ review/build**

### Phạm vi

- Frontend only.
- Hero 3D.
- Dashboard preview có chiều sâu.
- Pricing sang và dễ hiểu.
- Hiển thị credit đúng.
- Responsive.
- Light/dark.
- Reduced motion.
- Browser smoke.
- Không đổi API.
- Không tự đặt giá.
- Không thêm dependency khi không cần.

### Definition of Done

- Desktop/tablet/390 px PASS.
- Không overflow.
- Không vỡ accessibility.
- Production build PASS.
- Không làm tăng bundle vô lý.
- Có before/after evidence.
- Không push.

---

## P3 — BATCH 4: Final release audit

**AI chính:** Codex
**Browser:** Antigravity

### Phạm vi

- Git status.
- Migration status.
- Backend full tests.
- Frontend production build.
- Integration Hub regression.
- Intakes permission regression.
- AI usage regression.
- Landing regression.
- Collaboration regression.
- Security scan.
- Secret scan.
- Stash review.
- Remote divergence.
- Final report.
- `READY_TO_PUSH` hoặc `NOT_READY_TO_PUSH`.

### Không làm

- Feature mới.
- Dependency upgrade không cần thiết.
- OAuth console configuration.
- Payment.
- Voice/video.

---

## P4 — External/manual tasks để cuối

| Task | Lý do |
|---|---|
| Google Sign-In thật | Cần Google Cloud Console và Client ID |
| GitHub OAuth thật | Cần GitHub OAuth App và Client Secret |
| Gmail/Calendar sync | Cần OAuth consent + redirect configuration |
| Slack sync | Cần Slack App |
| Giá plan thật | Cần Khôi/nhóm/PO quyết định |
| Mua credit | Cần chọn payment provider và policy |
| ZIP attachment | Cần archive/scanning policy |
| Voice/video/WebRTC | Không thuộc nhiệm vụ hiện tại |

Quy tắc:

```text
Thiếu credential/console/payment decision
→ ghi BLOCKED_EXTERNAL
→ lưu hướng dẫn thao tác tay ngắn
→ chuyển sang task tiếp theo
```

---

## 10. Phân công công cụ theo thế mạnh

### Codex

Dùng cho:

- Backend.
- Authorization.
- Database.
- Migration.
- Tests.
- API contract.
- Security.
- Refactor có kiểm soát.
- Git.
- Report.
- Batch lớn end-to-end.

### Antigravity

Dùng cho:

- Chạy browser.
- Multi-step flow.
- Responsive.
- Runtime evidence.
- Form interaction.
- Network/console.
- Chụp trạng thái thực tế.
- Hai hoặc nhiều browser/profile khi hỗ trợ.

Không giao Antigravity tự sửa backend permission phức tạp nếu Codex có thể làm chính xác hơn.

### Kombai

Dùng cho:

- Landing page.
- CSS/layout.
- Visual polish.
- Responsive frontend.
- 3D visual nhẹ.
- UI consistency.

Không giao Kombai:

- Database.
- Migration.
- Authorization backend.
- Security-critical code.

### ChatGPT

Dùng cho:

- Quản lý roadmap.
- Đọc report.
- Rút gọn context.
- Tạo prompt.
- Phân loại lỗi.
- Quyết định task tiếp theo.
- Kiểm tra task có vượt scope không.

### Khôi

Khôi xử lý:

- Google/GitHub/Slack console.
- Client ID/Secret nhập trực tiếp trên máy.
- Quyết định giá.
- Chọn payment provider.
- Duyệt UI.
- Test tài khoản thật.
- Cho phép push.

---

## 11. Quy tắc xử lý lỗi

| Tình huống | Hành động |
|---|---|
| Lỗi có reproduction rõ | Sửa trong scope |
| Thiếu cấu hình ngoài | `BLOCKED_EXTERNAL`, tiếp tục |
| Không chắc là lỗi | Test trước |
| Lỗi ngoài scope | Ghi backlog |
| Build fail do thay đổi task | Sửa trước khi commit |
| Build fail do môi trường | Ghi blocker, không đoán |
| Code đã tồn tại | Kế thừa, không viết lại |
| Có worktree dở dang | Review rồi resume |
| Có secret/private config | Không in, không commit |
| Cần thao tác tay | Giao checklist ngắn cho Khôi |
| Task quá khó nhưng không chặn P0 | Đưa xuống cuối |
| Có thể làm nhiều phần an toàn | Gộp một batch |
| Hai phần có risk khác nhau | Tách commit, không nhất thiết tách task |

---

## 12. Chuẩn báo cáo sau mỗi batch

AI chỉ cần trả:

```text
Decision:
Commits:
Files changed:
Backend build/tests:
Frontend build:
Runtime evidence:
Fixed:
Deferred:
External blockers:
Migration:
Security:
Worktree:
Push:
Report:
```

Không kể lại toàn bộ quá trình.

Report trong repo tối đa khoảng 180–220 dòng.

---

## 13. Prompt siêu ngắn dùng từ nay

### P0 — Integration Hub + Intakes

```text
Đọc docs/KHOI_MASTER_PLAN.md.

Thực hiện P0 — BATCH 1 đúng phạm vi.
Kiểm tra live Git trước khi sửa.
Ưu tiên Intakes authorization và bảng test 2 module của Khôi.
External OAuth thiếu credential thì ghi BLOCKED_EXTERNAL rồi tiếp tục.
Build, full test, runtime A/B/C, report, commit và dừng.
Không push. Không pop/drop stash.
```

### P1 — AI credit

```text
Đọc docs/KHOI_MASTER_PLAN.md.

Thực hiện P1 — BATCH 2 đúng phạm vi.
Sửa contract usage/credit, encoding và quota từ source of truth.
Thiếu subscription/payment design thì ghi AI_CREDIT_PURCHASE_DEFERRED rồi tiếp tục.
Build, full test, runtime, report, commit và dừng.
Không push. Không pop/drop stash.
```

### P2 — Landing 3D

```text
Đọc docs/KHOI_MASTER_PLAN.md.

Thực hiện P2 — BATCH 3, frontend only.
Nâng landing thành visual 3D sang, nhẹ, responsive và accessible.
Không đổi API, không nghĩ giá, không thêm dependency nếu chưa chứng minh cần.
Browser test desktop/tablet/390px, production build, report, commit và dừng.
Không push.
```

### P3 — Final release audit

```text
Đọc docs/KHOI_MASTER_PLAN.md.

Thực hiện P3 — BATCH 4.
Không phát triển tính năng mới.
Chạy final build/test/runtime/security/secret/Git audit.
External OAuth/payment chưa cấu hình thì ghi blocker riêng.
Kết luận READY_TO_PUSH hoặc NOT_READY_TO_PUSH.
Report, commit và dừng. Không push.
```

---

## 14. Git safety checklist cho mọi batch

Chạy đầu task:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git log -8 --oneline --decorate
git --no-pager stash list
```

Dừng khi:

- Không ở `KhoiSigma`.
- Worktree có thay đổi không rõ nguồn gốc.
- Stash quan trọng bị mất.
- Có process/task khác đang sửa cùng file.

Cuối task:

```powershell
git diff --check
git status --short
```

Không push.

---

## 15. Status Log

AI phải cập nhật phần này sau mỗi batch.

| Batch | Trạng thái | Commit | Tests/build | Blocker | Ngày |
|---|---|---|---|---|---|
| Collaboration foundation | PASS | Nhiều commit | 287/287 tại checkpoint gần nhất | Không | Trước 2026-08-04 |
| OAuth runtime | DEFERRED | Chưa chốt | UI fallback hoạt động | External credentials/console | 2026-08-04 |
| P0 Integration Hub + Intakes | PASS | feat(p0): secure intakes and integration permissions | 290/290 backend; frontend build PASS; runtime A/B/C PASS | BLOCKED_EXTERNAL: provider credentials/console | 2026-08-04 |
| P1 AI usage/credit | NOT STARTED | — | — | Product/payment có thể deferred | — |
| P2 Landing 3D | NOT STARTED | — | — | Giá thật cần quyết định | — |
| P3 Final release | NOT STARTED | — | — | Phụ thuộc P0–P2 | — |

---

## 16. Definition of Done toàn dự án cho phạm vi của Khôi

Dự án được xem là hoàn thành phạm vi Khôi khi:

- Integration Hub có bảng chức năng rõ.
- Integration Hub permission tests PASS cho phần nội bộ.
- External OAuth blocker được ghi riêng.
- Intakes permission backend PASS.
- Intakes frontend không lộ action sai quyền.
- Runtime manager/member/outsider PASS.
- AI credit hiển thị đúng source of truth.
- Không còn hard-code sai.
- Landing có visual 3D phù hợp.
- Landing responsive/accessibility PASS.
- Backend full tests PASS.
- Frontend production build PASS.
- 0 migration pending.
- Không có security blocker.
- Không có secret tracked.
- Stash được review.
- Worktree sạch.
- Final report có `READY_TO_PUSH`.

---

## 17. Những điều không được tuyên bố khi chưa có bằng chứng

Không được ghi `PASS` khi chỉ:

- Build thành công nhưng chưa runtime.
- Unit test pass nhưng chưa permission matrix.
- UI có nút nhưng API chưa hoạt động.
- OAuth button render nhưng chưa login thật.
- Migration tồn tại nhưng chưa applied.
- Message gửi được nhưng outsider chưa bị chặn.
- Pricing hiện số nhưng không có source of truth.
- Credit hiện số nhưng frontend/backend lệch contract.
- Landing có rotate CSS nhưng chưa đạt visual 3D yêu cầu.

---

## 18. Task bắt đầu ngay sau file này

```text
P0 — BATCH 1:
Integration Hub + Intakes audit, authorization, focused tests,
runtime A/B/C và bảng báo cáo đúng mẫu phân công của Khôi.
```

Dùng prompt siêu ngắn ở mục 13. Không dán lại toàn bộ kế hoạch.
