# CONTINUE — DEEP FUNCTIONAL AUDIT, ERROR PATH & USER FEEDBACK & FIX

Tiếp tục audit từ kết quả hiện tại. **Không được kết luận hệ thống PASS chỉ vì build/typecheck/unit test đang pass.**

Mục tiêu tiếp theo là chuyển từ static/code audit sang:

> **DEEP FUNCTIONAL AUDIT + FUNCTION-BY-FUNCTION REVIEW + ERROR PATH REVIEW + USER FEEDBACK REVIEW + INTEGRATION FLOW REVIEW**

## 1. KHÔNG ĐƯỢC DỪNG VÌ TEST ENVIRONMENT BỊ BLOCK

Backend integration test hiện đang bị block bởi SQL Server database:

`TaskManagement_PaymentP0_20260822_01`

Đây là **test environment/infrastructure blocker**, không được coi là:

* backend functional failure;
* backend functional pass.

Hãy ghi riêng:

> BLOCKED BY ENVIRONMENT

và tiếp tục audit những phần có thể kiểm tra bằng static analysis, code tracing, dependency analysis, API contract inspection và frontend flow analysis.

Không được vì database test chưa chạy được mà bỏ qua backend code review.

---

# 2. XỬ LÝ ESLINT 642 LỖI THEO NHÓM

Không được chỉ ghi:

> ESLint: 642 errors

Hãy phân loại thành:

### Category A — Functional-risk

Các lỗi có khả năng gây runtime bug, crash, wrong state hoặc broken behavior.

### Category B — Error-handling risk

Các lỗi liên quan đến exception, missing error handling, missing imports, missing branches.

### Category C — UI-risk

Các lỗi có thể làm giao diện crash/broken.

### Category D — Maintainability

Các lỗi không trực tiếp gây functional failure nhưng ảnh hưởng chất lượng code.

### Category E — Cosmetic / low risk

Các lỗi ít ảnh hưởng runtime.

Ưu tiên xử lý A → B → C trước.

Không cần sửa toàn bộ 642 lỗi một cách máy móc nếu chúng không liên quan đến functional correctness.

---

# 3. AUDIT TỪNG FUNCTION — KHÔNG BỎ SÓT

Hãy lập danh sách function/method thực tế trong project và audit từng cái.

Đối với MỖI function, trả lời:

### INPUT

* Function nhận gì?
* Có thể nhận null không?
* Có thể nhận empty không?
* Có thể nhận invalid value không?
* Boundary values?
* Unexpected input?

### PROCESS

* Logic có đúng không?
* Các điều kiện có đầy đủ không?
* Có branch nào chưa xử lý?
* Có thể throw exception ở đâu?
* Có side effect không?

### OUTPUT

* Return gì?
* Có thể null không?
* Có thể empty không?
* Caller có xử lý đầy đủ không?
* Response có đúng contract không?

### ERROR

* Có try/catch cần thiết không?
* Error có được propagate đúng không?
* Có silent failure không?
* Có swallow exception không?
* Có biến lỗi thành success không?

### USER FEEDBACK

* Function này có tạo ra user-visible action không?
* Nếu success: user được thông báo gì?
* Nếu failure: user được thông báo gì?
* Nếu empty: user thấy gì?
* Nếu unauthorized: user thấy gì?
* Nếu conflict: user thấy gì?
* Nếu server error: user thấy gì?

### UI SAFETY

* Loading có reset không?
* Button có bị disable vĩnh viễn không?
* Modal có bị kẹt không?
* Component có thể crash không?
* Null/undefined có làm render lỗi không?

---

# 4. MỖI USER ACTION PHẢI CÓ OUTCOME

Rà soát tất cả event/action:

* Create
* Update
* Delete
* View
* Search
* Filter
* Sort
* Approve
* Reject
* Assign
* Remove
* Clone
* Restore
* Activate
* Deactivate
* Submit
* Cancel
* Save
* Upload
* Import
* Export
* Login
* Logout
* Permission
* Role
* Các action nghiệp vụ khác.

Với mỗi action phải xác định:

```text
ACTION
   ↓
VALIDATION
   ↓
REQUEST
   ↓
API / BUSINESS LOGIC
   ↓
SUCCESS / FAILURE
   ↓
UI STATE
   ↓
USER FEEDBACK
```

Tìm mọi chỗ có:

```text
click → request → error → nothing happens
```

Đây phải được coi là:

> SILENT FAILURE

và đánh dấu issue.

---

# 5. BẮT BUỘC AUDIT TOÀN BỘ ERROR PATH

Đừng chỉ test success path.

Với từng function/action, trace ít nhất:

```text
SUCCESS
VALIDATION_ERROR
EMPTY_DATA
NULL_DATA
NOT_FOUND
UNAUTHORIZED
FORBIDDEN
CONFLICT
BUSINESS_RULE_ERROR
NETWORK_ERROR
TIMEOUT
SERVER_ERROR
UNEXPECTED_EXCEPTION
```

Nếu một trạng thái có thể xảy ra nhưng code không xử lý:

> FLAG AS ISSUE

---

# 6. THÔNG BÁO PHẢI ĐƯỢC KIỂM TRA NHƯ MỘT FUNCTIONAL REQUIREMENT

Đừng coi notification là phần cosmetic.

Hãy audit toàn bộ:

* ElMessage
* Toast
* Notification
* Snackbar
* Alert
* Modal error
* Empty state
* Inline validation
* API error rendering

Tìm:

* Function success nhưng không thông báo
* Function fail nhưng không thông báo
* API fail nhưng frontend im lặng
* Message quá chung chung
* Message không đúng ngữ cảnh
* Success message xuất hiện dù operation thất bại
* Error message hiển thị technical exception
* Duplicate notification
* Một lỗi hiển thị nhiều message
* API error không được map sang user-friendly message

---

# 7. KIỂM TRA NOTIFICATION MATRIX

Mỗi action phải có bảng:

| Action | Scenario      | Backend Result | Frontend Handling | User Feedback | UI Safe? |
| ------ | ------------- | -------------- | ----------------- | ------------- | -------- |
| Create | Success       | 201            | handled           | success       | Yes      |
| Create | Validation    | 400            | handled           | validation    | Yes      |
| Create | Duplicate     | 409            | handled           | conflict      | Yes      |
| Create | Unauthorized  | 401            | handled           | login/session | Yes      |
| Create | Forbidden     | 403            | handled           | permission    | Yes      |
| Create | Server Error  | 500            | handled           | error         | Yes      |
| Create | Network       | none           | handled           | connection    | Yes      |
| Create | Empty/invalid | blocked        | handled           | validation    | Yes      |

Làm tương tự cho tất cả action quan trọng.

---

# 8. AUDIT 400 / 401 / 403 / 404 / 409 / 500

Tìm toàn bộ API/client error handlers.

Xác định:

### 400

Frontend có đọc và hiển thị validation/business error không?

### 401

Session/token handling có đúng không?

### 403

Có thông báo permission phù hợp không?

### 404

UI có xử lý resource không tồn tại không?

### 409

Có xử lý conflict/duplicate không?

### 500

Có fallback an toàn không?

### Network/Timeout

Có reset loading không?

Đặc biệt không được để:

```text
error → unhandled promise rejection
```

hoặc:

```text
500 → blank screen
```

---

# 9. TRACE FUNCTION CALL CHAIN

Đây là bước quan trọng nhất.

Với những flow chính, trace toàn bộ call chain:

```text
UI Event
→ Component Method
→ Store
→ Service
→ API
→ Controller
→ Application Service
→ Domain Logic
→ Repository
→ Database
→ Response
→ Service
→ Store
→ Component
→ UI
→ Notification
```

Kiểm tra tại mỗi boundary:

* Input contract
* Output contract
* Nullable value
* Error contract
* HTTP status
* State synchronization
* Async/await
* Exception propagation

Tìm mismatch giữa từng tầng.

---

# 10. NULL / EMPTY / UNDEFINED AUDIT

Quét các pattern nguy hiểm:

```ts
object.property
response.data.property
selectedItem.id
items.map(...)
items.length
foo.find(...)
foo.filter(...)
```

nếu các giá trị đó có khả năng:

```text
null
undefined
[]
empty
missing
```

Phải xác định liệu UI có crash hay không.

Đặc biệt kiểm tra:

* table
* list
* detail page
* dropdown
* select
* modal
* pagination
* dashboard
* chart
* summary
* relation data

---

# 11. LOADING STATE AUDIT

Tìm toàn bộ async action có:

```text
loading = true
```

và xác minh mọi outcome đều reset được:

```text
success
error
exception
timeout
network failure
```

Ưu tiên tìm:

```text
loading = true
try { ... }
catch { ... }
```

nhưng không có reset trong `finally`.

Cũng kiểm tra:

* double click;
* double submit;
* duplicate request;
* race condition;
* stale response.

---

# 12. UI CRASH AUDIT

Tìm mọi khả năng:

* blank page;
* render exception;
* undefined access;
* broken component;
* broken modal;
* stuck spinner;
* disabled button;
* invalid state;
* stale data;
* empty table crash;
* missing chart data;
* missing relation data.

Các lỗi có khả năng làm user không sử dụng được chức năng phải đánh dấu:

> HIGH / CRITICAL

---

# 13. KHÔNG SỬA BỪA

Giữ nguyên các thay đổi hiện tại của tôi trong:

```text
Frontend/src/components/CyclesTab.vue
Frontend/src/components/common/WorkItemsListTable.vue
```

Không revert.
Không overwrite.
Không reset.
Không checkout lại các file này.

Backend hiện tại:

> Audit trước, chưa tự ý sửa nếu chưa xác định rõ root cause.

Đặc biệt không sửa business logic chỉ để test pass.

---

# 14. ƯU TIÊN FIX

Chỉ sửa ngay các issue có root cause rõ ràng và có confidence cao.

Ưu tiên:

### P0 — Critical

* Crash
* 500 do bug code
* Data corruption
* Security/permission bypass
* Broken core flow

### P1 — High

* Silent failure
* Missing error handling
* Broken user flow
* Null/undefined runtime crash
* Stuck loading
* Wrong success/failure state

### P2 — Medium

* Incorrect notification
* Inconsistent UI state
* Poor error messaging
* Non-critical integration issue

### P3 — Low

* Cosmetic
* Code quality
* Minor lint issue

Không sửa P3 trước P0/P1.

---

# 15. SAU KHI FIX PHẢI RETEST

Mỗi fix phải:

1. Retest function đó.
2. Retest function gọi nó.
3. Retest function nó gọi.
4. Retest module liên quan.
5. Retest UI state.
6. Retest notification.
7. Retest error path.

Không được fix isolated rồi tuyên bố pass.

---

# 16. OUTPUT MÀ TÔI MUỐN NHẬN

Sau mỗi phase, báo cáo theo format:

## AUDIT STATUS

### Project Coverage

* Modules:
* Views:
* Components:
* Stores:
* Services:
* Controllers:
* Backend Services:

### Functional Coverage

* Functions audited:
* Functions with issues:
* Critical flows tested:
* Error paths checked:

### Notification Coverage

* Actions audited:
* Actions with success feedback:
* Actions with failure feedback:
* Actions with missing feedback:
* Actions with incorrect feedback:

### UI Safety

* Crash risks:
* Null risks:
* Loading issues:
* Modal issues:
* Broken state issues:

### Integration

* Frontend ↔ API issues:
* API ↔ Backend issues:
* Backend ↔ DB issues:

### Issues

Mỗi issue:

```text
ID:
Severity:
Module:
File:
Function:
Scenario:
Current behavior:
Expected behavior:
Root cause:
User impact:
Recommended fix:
Status:
```

### Environment Blockers

Phân biệt rõ:

```text
CODE BUG
TEST BUG
ENVIRONMENT BLOCKER
UNKNOWN
```

Không được trộn chúng thành một loại.

---

# 17. DEFINITION OF DONE

Chỉ được kết luận:

> PASS

khi đã đạt:

* Core modules audited
* Core functions audited
* Success paths tested
* Failure paths reviewed
* Null/empty paths reviewed
* API error paths reviewed
* Notification paths reviewed
* UI recovery reviewed
* Integration chains reviewed
* Regression completed

Nếu chưa đạt thì phải ghi:

> NOT PASS — AUDIT INCOMPLETE

và liệt kê chính xác phần còn thiếu.

## NGUYÊN TẮC CUỐI CÙNG

Hãy đánh giá hệ thống theo góc nhìn của một user thật:

> "Tôi bấm một nút. Dù thành công, thất bại, thiếu dữ liệu, sai dữ liệu, mất mạng, không có quyền, record không tồn tại, server lỗi hay exception xảy ra — hệ thống có luôn cho tôi một kết quả được kiểm soát, một trạng thái UI hợp lệ và một thông báo phù hợp hay không?"

Nếu câu trả lời là **không**:

> **Đó là một functional issue, không phải chỉ là UI issue.**

Tiếp tục audit từ trạng thái hiện tại và **không dừng lại ở ESLint/build/unit test**.
