# SPRINTA P0 FIX PLAN

> Chỉ làm các phase theo thứ tự. Mỗi phase là một PR/commit nhỏ.
>
> P0 hoàn thành trước khi làm AI Credits UI hoặc feature nâng cao.

## Phase P0-00 — Freeze, backup và reproduce

### Mục tiêu

- Ghi commit SHA và environment.
- Backup database.
- Tạo branch `integration/p0-stabilization`.
- Chạy build hiện tại.
- Sửa format chín test malformed.
- Reproduce từng lỗi P0.

### Không được

- Redesign.
- Migration lớn.
- Đổi API contract không cần thiết.
- Đánh dấu fail là fixed khi chỉ đọc code.

### Exit

- Defect log có request, response, DB evidence và file nghi ngờ.
- Có rollback point.

---

## Phase P0-01 — OTP và Account Authentication

### Scope

- Secure OTP generation.
- Attempt limit theo email/IP.
- Cooldown/resend policy.
- Chặn user `IsDeleted=true` hoặc `IsActive=false`.
- Invalidate refresh/session phù hợp.

### Acceptance

- OTP chỉ 6 số.
- Không modulo `% 10`.
- Thử sai quá ngưỡng bị khóa/429.
- Không phân biệt hoa thường và trim email theo canonical rule.
- Deleted/inactive user không login/refresh.
- Không leak “email có tồn tại hay không” ngoài contract.

### Test

- `TC_AUTH_003`
- `TC_AUTH_007`
- `TC_AUTH_029`
- Auth E2E và rate-limit test.

---

## Phase P0-02 — Workspace, Project Role và AI Permission

### Scope

- Resource authorization handler chung.
- Check active workspace membership.
- Canonical role/status code.
- Bỏ bypass từ system role không được PO phê duyệt.
- Sprint lock ở backend.
- AI tool dùng chính policy API.

### Acceptance

- Workspace inactive member: 403.
- Developer thiếu permission không tạo Sprint qua UI, API hoặc AI.
- `PM`, `pm`, `Pm` cho cùng kết quả.
- Sprint đóng không update Task.
- Permission check không chỉ nằm frontend.

### Test

- `TC_AI_002`
- `TC_WS_006`
- `TC_RBAC_009`
- `TC_RBAC_045`

---

## Phase P0-03 — Dependency, Rollover và Context Integrity

### Scope

- DFS/BFS cycle detection.
- Cross-project TargetSprint validation.
- Workspace/Project context lấy từ explicit route/request.
- ReporterId từ current authenticated user.
- Unique invite concurrency.

### Acceptance

- Chặn A→B→C→A.
- Không rollover Task sang Sprint Project khác.
- User nhiều Workspace tạo Goal đúng Workspace đã chọn.
- Không dùng Guid.Empty hoặc “user đầu tiên”.
- Hai invite đồng thời chỉ tạo một membership.

### Test

- `TC_DEP_003`, `TC_DEP_025`
- `TC_SPRINT_020`
- `TC_GOAL_005`
- `TC_TASK_038`
- `TC_MEM_006`

---

## Phase P0-04 — History, Ledger và Concurrency

### Scope

- Soft-delete/deactivate assignment.
- Reward transaction ledger.
- Rollback tham chiếu transaction gốc.
- RowVersion contract.
- Idempotency.

### Acceptance

- Xóa member không mất contribution history.
- Done → In Progress hoàn điểm đúng một lần.
- Retry không cộng/trừ lại.
- Concurrent update trả 409 và current version.
- Không dùng InMemory để chứng minh concurrency.

### Test

- `TC_MEM_023`
- `TC_GAMIFY_003`
- `TC_TASK_024`
- `TC_RBAC_050`

---

## Phase P0-05 — Hosting, Migration, HTTPS và Upload

### Current risk cần xử lý

- Development có logic fallback InMemory.
- `Program.cs` tự migrate, raw SQL schema repair và seed.
- HTTPS redirect đang tắt.
- `/uploads` được serve static.
- Startup catch migration lỗi rồi tiếp tục.

### Scope

- `Testing`: cho phép InMemory hoặc test DB.
- `Development`: SQL rõ ràng; demo seed bằng flag.
- `Staging/Production`: thiếu connection string phải fail.
- Migration là deployment command/job.
- Không raw SQL schema repair trong startup.
- HTTPS/HSTS và forwarded headers đúng.
- Private file qua authorized endpoint/signed URL.

### Acceptance

- Production không khởi động bằng InMemory.
- Migration fail làm deployment fail.
- Không seed mock production.
- Private attachment không truy cập anonymous.
- MIME/signature/size/path traversal test pass.

---

## Phase P0-06 — AI Safety và Cost Integrity

### Scope

- Input redaction cho secret/token/password.
- Untrusted document/email content không điều khiển tool.
- Tool permission backend.
- Preview/Confirm/Cancel.
- Idempotency.
- Credit reservation chỉ sau validation và finalize theo actual usage.
- Provider failure không trừ credit.

### Acceptance

- Cancel tạo 0 entity.
- Retry tạo đúng 1 entity.
- Prompt injection trong PDF/email không đổi Project/permission.
- File ngoài quyền không được retrieve.
- Provider timeout/429 trả lỗi thân thiện.
- Không trừ credit khi call thất bại trước completion.

---

## Phase P0-07 — Regression Gate

Phải pass:

```text
dotnet build
dotnet test
SQL integration tests
frontend build
critical Playwright smoke
secret scan
```

Manual:

- Login/deleted user.
- Workspace inactive permission.
- Create/update Task.
- Sprint lock.
- Circular dependency.
- AI Cancel/Confirm/Retry.
- Upload private file.
- Reload.
- Double-click.

## P0 Release Gate

Không được chuyển sang P1 nếu còn:

- Auth bypass.
- Cross-workspace/project mutation.
- Data history bị hard-delete.
- Duplicate do concurrency.
- Migration fail nhưng app vẫn chạy.
- Private upload public.
- AI mutation không confirm/idempotency.
