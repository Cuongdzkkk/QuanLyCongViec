# SPRINTA EXECUTION PROMPTS

> Mỗi prompt dưới đây là **một phiên làm việc riêng**.
>
> Không gửi nhiều prompt phase cùng lúc cho AI Agent.

---

# UNIVERSAL HEADER — Dán ở đầu mọi prompt

```text
Bạn đang làm dự án SprintA:
https://github.com/DinhTuanKhoiTB01696/QuanLyCongViec

Đọc theo thứ tự:
1. AGENTS.md
2. docs/SPRINTA_MASTER_CONTEXT.md
3. docs/SPRINTA_PROJECT_MEMORY.md
4. docs/SPRINTA_TEST_TRUTH_MATRIX.md
5. File plan đúng phase.

Quy tắc:
- Trước tiên chạy git status --short.
- Chỉ audit file trực tiếp liên quan; không quét và sửa toàn repo.
- Không mock, hard-code hoặc fake success.
- Không xóa chức năng đang chạy.
- Không đổi API contract/migration nếu phase không yêu cầu.
- Không làm nhiều phase.
- Khi UI: không sửa backend; nếu thiếu API ghi CẦN BACKEND.
- Khi backend: không redesign UI.
- Permission phải enforce backend.
- Mutation cần idempotency/concurrency khi liên quan.
- Trước code báo:
  A. Current state
  B. Bug reproduced/evidence
  C. Files dự kiến đọc/sửa
  D. Acceptance criteria
  E. Risks
- Chỉ code sau phần audit.
- Sau code chạy build/test phù hợp, kiểm tra reload/double-click/permission.
- Cuối cùng báo:
  A. Task
  B. Files
  C. Completed behavior
  D. Tests/evidence
  E. Remaining/blockers
- Báo git diff --name-only rồi dừng.
```

---

# PROMPT 00 — Test Baseline và Defect Reproduction

```text
[UNIVERSAL HEADER]

Nhiệm vụ: BASE-02 + BASE-03.

Đọc:
- docs/SPRINTA_TEST_TRUTH_MATRIX.md
- docs/SPRINTA_MASTER_BACKLOG.md

Chưa sửa business logic.

Hãy:
1. Xác định test project hiện có.
2. Map 36 test fail với service/controller/entity thật.
3. Chọn các lỗi P0 và reproduce bằng test nhỏ nhất.
4. Không đổi expected result để test pass.
5. Tạo defect matrix:
   Test ID | File source | Reproduced | Evidence | Suspected cause.
6. Chỉ được thêm test tái hiện và test helpers tối thiểu.
7. Không refactor production source trong phase này.

Exit:
- Build pass.
- Test fail đúng nguyên nhân cho từng P0 được chọn.
- Không file ngoài test/docs.
```

---

# PROMPT 01 — OTP và Deleted Account

```text
[UNIVERSAL HEADER]

Nhiệm vụ: SEC-01 + SEC-02.
Plan: docs/SPRINTA_P0_FIX_PLAN.md — Phase P0-01.
Tests: TC_AUTH_003, TC_AUTH_007, TC_AUTH_029.

Yêu cầu:
- Secure numeric OTP generation, không modulo bias.
- Attempt limit theo email/IP với cooldown.
- Giữ single-use.
- Canonical email trim/lowercase.
- Login/refresh chặn IsDeleted hoặc IsActive=false.
- Không leak thông tin tài khoản ngoài contract.
- Không sửa UI trừ khi cần hiển thị 429/lock message và được tách commit.

Test:
- Unit OTP.
- Rate-limit/attempt integration.
- Login deleted/inactive.
- Existing valid login regression.
```

---

# PROMPT 02 — Workspace/RBAC/AI Permission

```text
[UNIVERSAL HEADER]

Nhiệm vụ: SEC-03, SEC-04, SEC-05.
Plan: Phase P0-02.
Tests: TC_AI_002, TC_WS_006, TC_RBAC_009, TC_RBAC_045.

Yêu cầu:
- Tạo/chuẩn hóa resource authorization handler.
- Check active WorkspaceMember.
- Canonical role code, case-insensitive đúng.
- Không bypass project permission chỉ vì system role phụ.
- Sprint closed lock ở mutation backend.
- AI tools gọi cùng API/policy, không policy riêng yếu hơn.

Không:
- Đổi role catalog lớn.
- Redesign Role UI.
- Tạo permission mới không có action thật.
```

---

# PROMPT 03 — Dependency Cycle và Cross-project Integrity

```text
[UNIVERSAL HEADER]

Nhiệm vụ: DATA-01, DATA-02, DATA-05, DATA-06.
Plan: Phase P0-03.
Tests: TC_DEP_003, TC_DEP_025, TC_SPRINT_020, TC_GOAL_005, TC_TASK_038.

Yêu cầu:
- DFS/BFS cycle detection với visited set.
- TargetSprint cùng Project/Workspace.
- WorkspaceId từ explicit current context/request.
- Reporter từ authenticated user, không Guid.Empty/fallback user.
- Transaction và friendly ProblemDetails.

Test chuỗi:
A→B, B→C, C→A bị chặn.
Chuỗi không cycle được phép.
```

---

# PROMPT 04 — Invitation Race và History

```text
[UNIVERSAL HEADER]

Nhiệm vụ: DATA-03 + DATA-04.
Tests: TC_MEM_006, TC_MEM_023.

Yêu cầu:
- Normalized email/user unique constraint trong ProjectMember.
- Concurrent invite idempotent/409 rõ.
- Không hard-delete TaskAssignment khi remove member.
- Lưu RemovedAt/RemovedBy/IsActive hoặc schema tương đương.
- Active queries không trả assignment đã remove.
- Audit/history vẫn thấy contribution.

Migration nếu bắt buộc:
- Tạo migration riêng.
- Không raw SQL startup patch.
- Báo migration impact trước khi code.
```

---

# PROMPT 05 — Reward Ledger và RowVersion

```text
[UNIVERSAL HEADER]

Nhiệm vụ: DATA-07 + DATA-08.
Tests: TC_GAMIFY_003, TC_TASK_024, TC_RBAC_050.

Yêu cầu:
- Reward/rollback theo immutable ledger reference.
- Retry không cộng/trừ lặp.
- Không early-return bỏ rollback.
- RowVersion client/server contract.
- Conflict trả 409 + current version.
- Test bằng SQL Server Testcontainers, không InMemory.
```

---

# PROMPT 06 — Program.cs, Migration, HTTPS, Upload

```text
[UNIVERSAL HEADER]

Nhiệm vụ: HOST-01 đến HOST-06.
Plan: Phase P0-05.

Audit Program.cs hiện tại.

Yêu cầu:
- InMemory chỉ Testing.
- Missing production connection string fail fast.
- Migration tách command/deployment path.
- Bỏ raw SQL schema repair khỏi startup sau khi có migration tương ứng.
- Seed mock chỉ Development + explicit flag.
- Bật HTTPS/HSTS đúng proxy.
- Private upload không serve anonymous static.
- Authorized download/signed URL.
- MIME/signature/size/path validation.

Không thực hiện tất cả trong một commit:
1. Hosting/config.
2. Migration cleanup.
3. Upload security.
Mỗi bước build/test rồi mới tiếp.
```

---

# PROMPT 07 — AI Safety, Confirm và Credits Integrity

```text
[UNIVERSAL HEADER]

Nhiệm vụ: Phase P0-06.

Đọc:
- docs/SPRINTA_AI_SPEC.md
- docs/SPRINTA_AI_CREDITS_BILLING_SPEC.md

Yêu cầu:
- Context + backend permission.
- Untrusted file/email cannot override tool policy.
- Redact token/password/secret.
- Preview/Confirm/Cancel.
- Action idempotency.
- Provider 429/timeout friendly handling.
- Cancel no mutation/no debit.
- Failed provider call releases reserved credit.
- Retry no duplicate/no double debit.
- Action/usage/audit link.

Test prompt injection trong TXT/PDF/email.
```

---

# PROMPT 08 — Frontend CI và Tests

```text
[UNIVERSAL HEADER]

Nhiệm vụ: QA-01, QA-02, QA-04, QA-05.
Plan: docs/SPRINTA_CORE_STABILIZATION_PLAN.md Phase P1-01.

Chỉ thay frontend/config/workflow.

Thêm:
- lint
- typecheck
- unit
- build
- Playwright smoke
- axe critical page
- frontend GitHub Actions

Không sửa UI business trừ test selector/accessibility label tối thiểu.
Không mock production fallback.
```

---

# PROMPT 09 — Backend SQL Integration Suite

```text
[UNIVERSAL HEADER]

Nhiệm vụ: QA-03 + QA-06.
Plan: Phase P1-02.

Thêm Testcontainers SQL Server và WebApplicationFactory.
Ưu tiên:
- RowVersion.
- Unique invite.
- Transaction rollback.
- Soft-delete filters.
- Cross-project protection.
- AI idempotency.
- Wallet ledger concurrency.

Không sửa production logic ngoài testability hook tối thiểu và phải giải thích.
```

---

# PROMPT 10 — Report Dashboard và Export

```text
[UNIVERSAL HEADER]

Nhiệm vụ: CORE-05 + CORE-06.

Đọc:
- docs/SPRINTA_UI_UX_GUIDELINES.md
- docs/SPRINTA_OPEN_SOURCE_USAGE_GUIDE.md
- SprintA_AI_Document_Test acceptance trong TEST_TRUTH_MATRIX.

Phase A backend:
- Aggregate endpoint, filter, permission, export DTO.

Phase B frontend (task riêng):
- 4 KPI trong viewport.
- ECharts.
- Drill-down.
- Loading/empty/error.
- Export.

Không redesign trang khác.
Không dùng Apex/Chart.js mới.
Không xóa dependency cho tới khi grep không còn import.
```

---

# PROMPT 11 — Pages và AI Document Intake

```text
[UNIVERSAL HEADER]

Nhiệm vụ: CORE-07 + CORE-08, nhưng làm hai task tuần tự.

Task A Pages:
- TipTap JSON renderer.
- DOMPurify.
- icon/cover.
- autosave state.
- không JSON raw.

Task B Document Intake:
- TXT/DOCX/PDF.
- secure upload/extraction.
- summary + ~5 Task candidates.
- owner/deadline/priority/confidence/source.
- Project destination.
- Preview/Confirm.
- Cancel 0.
- Retry no duplicate.

Không tự tạo task trước Confirm.
```

---

# PROMPT 12 — AI Credit Wallet Foundation

```text
[UNIVERSAL HEADER]

Nhiệm vụ: BILL-01, BILL-02, BILL-03.
Đọc docs/SPRINTA_AI_CREDITS_BILLING_SPEC.md.

Chỉ backend/domain/admin API; chưa payment gateway.

Yêu cầu:
- Tách gamification points, promotional credits, purchased credits.
- Wallet + immutable ledger.
- Usage record.
- Package/model price config.
- Reserve/finalize/release.
- RowVersion/idempotency.
- Free credit grant.
- Usage/history endpoints.
- Không âm.
- Migration riêng.
- SQL concurrency tests.

Không tạo checkout UI trong task này.
```

---

# PROMPT 13 — VNPAY Sandbox

```text
[UNIVERSAL HEADER]

Nhiệm vụ: BILL-04 + backend phần BILL-06.

Đọc billing spec và tài liệu VNPAY mới nhất.

Yêu cầu:
- IPaymentGateway adapter.
- PaymentOrder snapshot.
- Create payment URL.
- Verify return/webhook/checksum.
- Query transaction.
- Duplicate callback idempotent.
- Transaction Paid → Credit ledger → Credited.
- Reconciliation.
- Refund foundation.
- Secrets từ environment.
- Không cộng credit từ frontend return URL.

Chỉ VNPAY sandbox. Không làm MoMo/ZaloPay cùng task.
```

---

# PROMPT 14 — Pricing, Wallet và Checkout UI

```text
[UNIVERSAL HEADER]

Nhiệm vụ: BILL-05.
Chỉ frontend; API phải có trước.

Đọc:
- SPRINTA_AI_CREDITS_BILLING_SPEC.md
- SPRINTA_UI_UX_GUIDELINES.md
- SPRINTA_OPEN_SOURCE_USAGE_GUIDE.md

UI:
- balance badge.
- overview.
- pricing packages.
- usage history.
- checkout redirect/status.
- invoice.
- low balance.
- estimated credits trước AI action.

Dùng Foundation + Element Plus.
Vue Bits/Motion chỉ micro-interaction nhẹ.
Không hard-code package; lấy API.
Không tin payment success query param; poll/read order API.
```

---

# PROMPT 15 — Một Advanced Feature

```text
[UNIVERSAL HEADER]

Chọn đúng MỘT feature từ SPRINTA_ADVANCED_FEATURE_ROADMAP.md.

Trước code:
- User problem.
- Existing source.
- Data model/API/UI.
- Permission.
- Migration.
- Test.
- Out of scope.

Không làm đồng thời Custom Fields + Recurring + Automation.
Ưu tiên lần lượt:
1. Custom Fields
2. Recurring Tasks
3. Templates
4. Saved Views
5. Time Tracking
6. Workload
7. Automation
```

---

# PROMPT REVIEW — Chỉ review, không sửa

```text
[UNIVERSAL HEADER]

Không sửa file.

Review branch/commit hiện tại theo:
- Scope.
- Security.
- Data integrity.
- Permission.
- API contract.
- Migration.
- Tests.
- UI regression.
- Project Bible.
- Release checklist.

Trả:
P0 blockers
P1 issues
P2 suggestions
Files/lines
Missing evidence
Go/No-Go
```
