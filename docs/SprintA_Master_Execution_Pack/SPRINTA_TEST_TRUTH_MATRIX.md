# SPRINTA TEST TRUTH MATRIX

> Mục tiêu: phân biệt **source tồn tại**, **reported pass** và **verified pass**.
>
> Không dùng tổng số dòng Excel làm phần trăm hoàn thiện sản phẩm.

---

## 1. Kết quả phân tích

### File 1.000 test

- Tổng dòng test: **1000**
- Reported pass: **1000**
- Kịch bản gốc sau khi loại `(Biến thể n)`: **47**
- Mỗi kịch bản bị lặp khoảng 12–30 lần với câu evidence chung chung.
- Kết luận: **CATALOG_ONLY / UNVERIFIED**, không phải 1.000 test độc lập.

### File 600 test

- Test ID hợp lệ: **600**
- Reported pass: **555**
- Reported fail: **36**
- Dòng lệch cột/malformed: **9**
- Kết luận: dùng 36 fail làm defect backlog; 555 pass chỉ nâng thành `PASS_VERIFIED` khi có evidence.

---

## 2. Evidence Level

| Level | Ý nghĩa | Được phép ghi “hoàn thành”? |
|---|---|---|
| `CATALOG_ONLY` | Chỉ có mô tả test | Không |
| `SOURCE_PRESENT` | Có source/controller/view/entity | Không |
| `PASS_REPORTED` | Excel ghi Pass nhưng thiếu evidence | Không |
| `PASS_MANUAL` | Có người chạy, ngày/build/screenshot | Có điều kiện |
| `PASS_AUTOMATED` | Test tự động pass trong CI | Có |
| `PASS_E2E_DB` | E2E + SQL Server + reload/permission | Mạnh nhất |
| `FAIL_REPRODUCED` | Lỗi tái hiện được | Phải đưa backlog |
| `BLOCKED` | Thiếu secret/env/dependency | Không |
| `OBSOLETE` | Test không còn đúng contract | Không tính |

Một feature chỉ được ghi `REAL/COMPLETE` khi critical path có `PASS_E2E_DB` và các nhánh lỗi quan trọng có test.

---

## 3. 47 kịch bản gốc trong file 1.000 dòng

### AI Integration

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| AI tóm tắt nội dung các cuộc thảo luận trong công việc | 30 |
| AI tự động tạo công việc từ đề xuất | 30 |
| Gửi câu hỏi cho Trợ lý AI trên trang AIPage | 30 |
| Kiểm tra quyền sử dụng AI của thành viên thường | 30 |
| Yêu cầu AI phân tích file tài liệu intake | 30 |
### Authentication & Profile

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Đăng ký tài khoản với thông tin hợp lệ | 15 |
| Đăng ký với email đã tồn tại | 15 |
| Đăng ký với mật khẩu yếu | 15 |
| Đăng nhập bỏ trống email | 15 |
| Đăng nhập bỏ trống mật khẩu | 15 |
| Đăng nhập với email hợp lệ và mật khẩu đúng | 15 |
| Đăng nhập với email không tồn tại | 15 |
| Đăng nhập với mật khẩu sai | 15 |
| Đổi mật khẩu thành công | 15 |
| Đổi mật khẩu với mật khẩu cũ không đúng | 15 |
### Collaboration & Chat

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Chia sẻ file trong cuộc trò chuyện | 25 |
| Gửi tin nhắn chat thời gian thực trong Space | 25 |
| Tag tên thành viên (@mention) trong chat | 25 |
| Xem Activity Feed của dự án | 25 |
### Gamification & Rewards

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Mở khóa huy hiệu (Badge) mới | 25 |
| Tích điểm khi hoàn thành công việc trước hạn | 25 |
| Xem bảng xếp hạng (Leaderboard) thành viên | 25 |
| Đổi quà bằng điểm tích lũy | 25 |
### Performance & Edge Cases

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Kiểm tra khả năng chịu tải khi tải lên file dung lượng cực lớn | 13 |
| Kiểm tra responsive trên thiết bị màn hình nhỏ (Mobile) | 12 |
| Kiểm tra tự động đồng bộ khi mất kết nối mạng đột ngột (Offline Mode) | 13 |
| Tải danh sách 1000 công việc cùng lúc | 12 |
### Space Management

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Khôi phục Space từ lưu trữ | 18 |
| Lưu trữ Space (Archive) | 19 |
| Thay đổi quyền của thành viên trong Space | 19 |
| Thêm thành viên vào Space | 19 |
| Tạo Space mới với tên hợp lệ | 19 |
| Tạo Space với tên trùng lặp trong cùng Workspace | 19 |
| Xóa thành viên ra khỏi Space | 19 |
| Xóa vĩnh viễn Space | 18 |
### System Administration

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Cấu hình kết nối Integration Hub (GitHub Integration) | 25 |
| Phân quyền vai trò người dùng (Roles & Permissions) | 25 |
| Tìm kiếm nâng cao trong Audit Log | 25 |
| Xem Audit Log hệ thống | 25 |
### Task Management

| Kịch bản gốc | Số biến thể lặp |
|---|---:|
| Bình luận vào công việc | 25 |
| Gán người thực hiện (Assignee) cho công việc | 25 |
| Thay đổi trạng thái công việc kéo thả (Kanban) | 25 |
| Thiết lập phụ thuộc công việc (Task Dependency) | 25 |
| Thiết lập độ ưu tiên (Priority) cho công việc | 25 |
| Tạo Task con (Subtask) | 25 |
| Tạo công việc mới với đầy đủ thông tin | 25 |
| Tạo công việc thiếu tiêu đề | 25 |

---

## 4. 36 lỗi bắt buộc xử lý

| Test ID | P | Nhóm | Lỗi được báo cáo | Hướng sửa bắt buộc | Bằng chứng đóng lỗi |
|---|---|---|---|---|---|
| `TC_AUTH_003` | **P1** | Security | Fail — Modulo bias: randomBytes[i] % 10 phân bố không đều khi byte > 249, một số chữ số xuất hiện nhiều hơn | Sinh OTP bằng rejection sampling hoặc RandomNumberGenerator.GetInt32(0, 10); thêm statistical test có tolerance. | Unit test 100k mẫu, chỉ ký tự số, không modulo bias rõ rệt. |
| `TC_AUTH_007` | **P0** | Security | Fail — Thiếu giới hạn số lần thử sai (brute-force), attacker có thể thử tối đa 10^6 lần trong 5 phút | Giới hạn số lần thử OTP theo email + IP, lock/cooldown, audit và rate limit; OTP vẫn single-use. | Test vượt ngưỡng bị 429/locked; OTP đúng sau lock không được dùng; không leak mã. |
| `TC_AUTH_018` | **P1** | Wallet | Fail — Thiếu khởi tạo ví khi đăng ký, dẫn đến null reference khi hiển thị trang cá nhân nếu chưa hoàn thành task nào | Khởi tạo ví điểm an toàn khi đăng ký hoặc dùng GetOrCreateWallet; không để UI phụ thuộc record bắt buộc. | User mới mở Profile/Rewards không null reference; concurrent create không tạo hai ví. |
| `TC_AUTH_029` | **P0** | Authentication | Fail — Thiếu check IsDeleted trong LoginAsync, User bị xóa mềm vẫn login được nếu IsActive=true | Login phải chặn IsDeleted và IsActive=false trước khi cấp token; refresh token/session cũng bị vô hiệu. | Deleted user không login/refresh; token cũ bị từ chối ở policy/version check. |
| `TC_GAMIFY_003` | **P0** | Data Integrity | Pass — rollback không còn bị chặn bởi active assignment; ledger immutable tham chiếu transaction gốc và có idempotency key. | SQL application lock, unique filtered index và reversal reference. | Unit regression cùng SQL lifecycle/race/migration tests pass. |
| `TC_GAMIFY_011` | **P1** | Gamification | Fail — HasChildTasks chỉ check IsDeleted=false, nếu tất cả subtask đã xóa thì Task cha vẫn được cộng điểm dù logic nghiệp vụ coi nó là cha | Định nghĩa rõ task cha sau soft-delete; query child phải dùng trạng thái nghiệp vụ nhất quán. | Root/child soft-delete matrix cho kết quả điểm đúng. |
| `TC_GAMIFY_019` | **P2** | Gamification | Fail — Ratio = 8.4/10 = 0.84 nhưng do Math.Max(estimatedHours, 0.5) và floating point, biên 0.85 có thể bị sai lệch ở edge case | Dùng decimal và epsilon/rounding rule cho ratio; viết rõ biên 0.85. | Boundary tests 0.8499/0.85/0.8501. |
| `TC_GAMIFY_031` | **P1** | Gamification | Fail — ApplyStatusChangeRewardsAsync return sớm khi có active TaskAssignment (dòng 65-69), CollaborationBonus chỉ áp dụng trong flow ApplyAssignmentProgressRewardsAsync | Hợp nhất reward pipeline giữa status và assignment progress; tránh return sớm làm mất bonus. | 2+ assignee nhận Collaboration Bonus đúng một lần. |
| `TC_SPRINT_002` | **P1** | Sprint | Fail — SprintService chỉ check EndDate <= StartDate bằng "<=", chưa validate trường hợp StartDate/EndDate là null | Validation required/null và EndDate > StartDate bằng validator chung. | Null/start=end/end<start đều trả ProblemDetails 400. |
| `TC_SPRINT_020` | **P0** | Cross-project Integrity | Fail — Thiếu validation cross-project TargetSprintId khi đóng Sprint, Task có thể bị rollover sang Sprint dự án khác | TargetSprint phải cùng Project/Workspace với Task và source Sprint; enforce backend. | Không rollover cross-project kể cả request thủ công/AI. |
| `TC_SPRINT_027` | **P2** | Reporting | Fail — Khi fallback về task count, hệ thống đếm cả subtask, đáng lẽ chỉ nên đếm root task | Burndown fallback đếm root task theo rule đã chốt, không cộng subtask. | Dataset có root/subtask/SP=0 cho biểu đồ đúng. |
| `TC_SPRINT_040` | **P2** | Sprint State | Fail — Sprint được tạo nhưng chưa set StartDate (null) cũng trả về "Upcoming" thay vì "Not Scheduled" | Thêm trạng thái Not Scheduled khi thiếu ngày; state machine tập trung. | Null date không trả Upcoming. |
| `TC_DEP_003` | **P0** | Dependency | Fail — Chỉ chặn circular 1 bậc (A→B→A), không phát hiện chuỗi dài A→B→C→A | Phát hiện cycle bằng DFS/BFS từ dependency mới tới task nguồn; transaction + depth/visited guard. | Chặn A→B→C→A và chuỗi dài; không false positive. |
| `TC_DEP_025` | **P0** | Dependency | Fail — Logic chỉ check reverse trực tiếp, bỏ sót vòng lặp A→B→C→A có thể gây deadlock workflow | Cùng fix graph traversal với TC_DEP_003; tạo một implementation duy nhất. | Graph integration tests trên SQL Server. |
| `TC_AI_002` | **P0** | Authorization | Fail — Nếu Developer có thêm system role "admin" ở bảng UserRoles, bypass được check project role vì SystemOverrideRoles check trước | Bỏ system-role bypass không được chốt; policy phải kết hợp workspace/project role và action permission. | Developer có system role phụ vẫn không tạo Sprint nếu thiếu permission dự án. |
| `TC_AI_018` | **P1** | AI Reliability | Fail — Nếu GeminiAI API timeout hoặc trả lỗi 429 (rate limit), hệ thống không có fallback/retry, trả về lỗi 500 thay vì thông báo thân thiện | Timeout, retry/backoff có giới hạn, map 429/timeout sang lỗi thân thiện; circuit breaker. | Provider timeout/429 không trả 500 thô, không trừ credit khi thất bại. |
| `TC_TASK_003` | **P1** | Validation | Fail — Check AssigneeIds dùng Count so sánh, nếu truyền list có duplicate [A,A] với A là member thì count=2 nhưng valid=1, ném lỗi sai | Distinct AssigneeIds trước validation; báo ID không thuộc dự án. | List duplicate hợp lệ không bị lỗi sai; outsider bị chặn. |
| `TC_TASK_017` | **P1** | Authorization | Fail — Thiếu check SystemOverrideRoles trong UpdateAsync, superadmin không phải member vẫn bị chặn update | Chuẩn hóa policy cho system override được PO cho phép; không hard-code rải rác. | Superadmin đúng quyền update; user khác bị 403. |
| `TC_TASK_024` | **P1** | Concurrency | Pass — SQL Server rowversion đã được kiểm chứng bằng hai DbContext cùng version. | Client version bắt buộc; EF OriginalValue được đặt trước khi save. | Request đầu thành công, stale writer bị DbUpdateConcurrencyException và không ghi đè field khác. |
| `TC_TASK_038` | **P0** | Data Integrity | Fail — Nếu DB không có User nào, FirstOrDefaultAsync trả null → reporterId vẫn là Guid.Empty → FK violation | ReporterId bắt buộc từ authenticated user hợp lệ; không fallback Guid.Empty/First user. | Không user context thì 401/400; không FK violation. |
| `TC_PROJ_002` | **P1** | Project | Fail — Thiếu validation unique Identifier trong code service, chỉ dựa vào DB unique constraint → exception không thân thiện | Pre-check normalized identifier và giữ DB unique constraint; map unique violation sang 409. | Concurrent create cùng identifier chỉ một bản ghi, lỗi thân thiện. |
| `TC_PROJ_019` | **P2** | UI Contract | Fail — ParseProjectUiConfig trả (null,null) nhưng không phân biệt giữa "chưa set" vs "set rỗng", frontend hiển thị sai placeholder | Dùng explicit HasValue/default DTO thay vì tuple null mơ hồ. | Frontend phân biệt chưa cấu hình và cấu hình rỗng. |
| `TC_MEM_006` | **P0** | Concurrency | Fail — Race condition: 2 request invite cùng lúc có thể tạo 2 ProjectMember trùng vì check AnyAsync xong chưa SaveChanges | Unique constraint ProjectId+User/Email normalized; transaction và map conflict 409. | Hai invite đồng thời không tạo trùng. |
| `TC_MEM_023` | **P0** | Audit History | Fail — RemoveRange xóa vật lý TaskAssignment thay vì soft delete, mất lịch sử đóng góp của member | Không hard-delete assignment lịch sử; dùng IsActive/RemovedAt/RemovedBy. | Xóa member vẫn giữ lịch sử contribution, query active không trả assignment cũ. |
| `TC_MEM_031` | **P1** | Soft Delete | Fail — Phụ thuộc vào Global Query Filter của Entity User, nếu query dùng IgnoreQueryFilters sẽ lộ user đã xóa | Repository/query policy không dùng IgnoreQueryFilters tùy tiện; explicit active user filter. | Deleted user không xuất hiện ở member list/export. |
| `TC_DEPT_002` | **P2** | Normalization | Fail — ToLower() không xử lý Unicode normalization, "Phòng Ban" và "Phong Ban" (không dấu) coi là khác nhau | Chuẩn Unicode normalization + case-insensitive collation; không tự động bỏ dấu nếu nghiệp vụ chưa chốt. | Tên canonically equivalent bị phát hiện; tên khác nghĩa không bị gộp. |
| `TC_DEPT_031` | **P2** | API Contract | Fail — Silent failure: không thông báo cho caller biết member không tồn tại, có thể ẩn bug phía UI | RemoveMember không tồn tại trả idempotent result rõ hoặc 404 theo contract; không silent. | Frontend nhận trạng thái có thể xử lý. |
| `TC_GOAL_005` | **P0** | Workspace Context | Fail — OrderBy(JoinedAt) lấy workspace đầu tiên, nếu user thuộc nhiều workspace sẽ luôn chọn workspace cũ nhất thay vì workspace đang dùng | WorkspaceId phải đến từ route/context đã chọn và được permission-check; không lấy membership đầu tiên. | User nhiều workspace tạo Goal đúng workspace hiện tại. |
| `TC_GOAL_017` | **P1** | Patch Semantics | Fail — Thiếu phân biệt Description=null (không update) vs Description="" (xóa nội dung), cả 2 đều ghi đè | Dùng Optional/Patch DTO để phân biệt omitted, null và empty. | Không gửi field thì giữ nguyên; gửi empty thì xóa theo rule. |
| `TC_GOAL_037` | **P2** | Validation | Fail — GetGuid chỉ handle ValueKind.String và ValueKind.Null, nếu field là number hoặc object thì trả null thay vì throw, ẩn lỗi data | JSON sai type phải trả validation error có path, không nuốt thành null. | Number/object cho GUID trả 400 rõ. |
| `TC_WS_006` | **P0** | Authorization | Fail — Một số endpoint không check IsActive workspace member, chỉ check project member Status | Tất cả workspace-scoped endpoint check WorkspaceMember.IsActive qua policy/resource handler. | Inactive member bị 403 ở read và mutation. |
| `TC_WS_039` | **P2** | Email UX | Fail — Nếu inviterName=null, email body hiển thị "null" thay vì fallback về "Thành viên" | Fallback inviter display name; encode HTML; template test. | Email không chứa 'null' và không XSS. |
| `TC_RBAC_009` | **P0** | Authorization | Fail — VisibilityOverrideRoles chứa "PM" (uppercase) nhưng NormalizeProjectRole trả "pm" (lowercase), so sánh case-sensitive sẽ trả false | Normalize role bằng enum/canonical lower-case và comparer OrdinalIgnoreCase. | PM/pm/Pm cho cùng kết quả permission. |
| `TC_RBAC_029` | **P1** | Status Normalization | Fail — NormalizeStatusName chỉ replace underscore bằng space, không handle trường hợp "IN PROGRESS" hay "inprogress" | Canonical status ID/code thay vì so sánh text; migration/map legacy values. | IN PROGRESS/in_progress/inprogress map đúng hoặc bị reject rõ. |
| `TC_RBAC_045` | **P0** | Sprint Lock | Fail — Sprint Lock check dùng Sprint.Status, nhưng Sprint đã đóng có thể vẫn status=true nếu EndDate chưa qua (sync chưa chạy) | Lock dựa trên state tính toán/ClosedAt, không phụ thuộc sync status cũ; enforce mutation API. | Sprint đóng không đổi Task dù EndDate chưa sync. |
| `TC_RBAC_050` | **P0** | Concurrency | Pass — REST/AI Task mutation dùng optimistic concurrency thống nhất. | REST trả 400 khi thiếu version và 409 `TASK_VERSION_CONFLICT` kèm current version/entity; AI gửi hoặc resolve version rồi xử lý cùng conflict. | SQL stale-write test và full regression pass. |

---

## 5. Chín dòng test bị lệch cột

| Test ID | Module | Dữ liệu đang bị dồn vào cột Steps | Hành động |
|---|---|---|---|
| `TC_AUTH_045` | Register | 1. Đăng ký user1+test@gmail.com và user1@gmail.com Tạo 2 User riêng biệt	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_AUTH_049` | OTP | 1. StoreOtp " USER@test.com " → Validate " user@test.com "	Trả về true	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_AUTH_050` | OTP | 1. StoreOtp " user@test.com " → Validate " user@test.com "	Trả về true	Pass PHẦN 2: GAME HÓA VÀ ĐIỂM THƯỞNG (GAMIFICATION) — 50 TC Mã TC	Module	Mô tả kịch bản (Scenario)	Các bước thực hiện	Kết quả mong đợi	Trạng thái | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_008` | Invite | 1. Invite " User@TEST.com "	Lưu " user@test.com "	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_014` | Invite | 1. Invite " john.doe@test.com "	User.FullName = "John Doe"	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_036` | Invite | 1. " john.doe@test.com "	"John Doe"	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_037` | Invite | 1. " john_doe@test.com "	"John Doe"	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_038` | Invite | 1. " john-doe@test.com "	"John Doe"	Pass | Tách lại Steps / Expected / Status rồi chạy lại |
| `TC_MEM_039` | Invite | 1. " john@test.com "	"John"	Pass | Tách lại Steps / Expected / Status rồi chạy lại |

Không tính chín dòng này là pass hoặc fail trước khi sửa workbook.

---

## 6. Acceptance Test: AI Document Intake

Input là biên bản họp TXT/DOCX/PDF có yêu cầu:

- Report hiển thị tổng task, completed, in progress, overdue trong một màn hình.
- Export không còn `[object Object]`.
- Pages không hiển thị JSON thô.
- AI phân tích TXT, DOCX và PDF.
- AI không tự tạo Task trước Confirm.
- Không gửi token/password/secret sang provider.

Expected AI:

1. Tóm tắt đúng nội dung.
2. Đề xuất khoảng 5 Task liên quan.
3. Nhận diện owner, deadline, priority.
4. Mỗi đề xuất có source snippet/page.
5. Không bịa Task ngoài tài liệu.
6. User sửa được proposal.
7. Cancel tạo 0 Task.
8. Confirm tạo đúng số Task đã chọn.
9. Retry không duplicate.
10. F5 còn Action result và links.

Test này phải chạy trên cả TXT, DOCX và PDF.

---

## 7. Cấu trúc test chuẩn hóa

Mỗi test cần các cột:

```text
TestId
RequirementId
Module
Risk
Preconditions
Steps
Expected
AutomationLevel
TestFile
Endpoint
Permission
DatabaseEvidence
BuildCommit
ExecutedAt
ExecutedBy
EvidenceUrl
Status
DefectId
```

## 8. Test stack đề xuất

### Frontend

- Vitest
- Vue Test Utils
- Playwright
- axe-core
- vue-tsc
- ESLint

### Backend

- xUnit
- FluentAssertions
- WebApplicationFactory
- Testcontainers for SQL Server
- Integration tests cho transaction, permission và concurrency

### CI gate

```text
Frontend: install → lint → typecheck → unit → build → E2E smoke
Backend: restore → build → unit → SQL integration → coverage
Security: secret scan → dependency scan
```

## 9. Rule đóng defect

- Có test tái hiện trước khi sửa, trừ trường hợp security cần vá khẩn.
- Có test pass sau sửa.
- Có API/database evidence.
- Có negative permission test.
- Có reload/double-click/retry khi liên quan.
- Không đổi test expected để làm test pass nếu chưa có quyết định PO.
