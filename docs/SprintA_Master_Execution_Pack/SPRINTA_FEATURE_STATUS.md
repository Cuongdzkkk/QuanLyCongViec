# SPRINTA FEATURE STATUS

> Snapshot theo thông tin ghi nhận đến 17/07/2026.
>
> Đây không phải chứng nhận E2E. Mục chưa có runtime evidence giữ trạng thái REVIEW_REQUIRED.

---

## 1. Legend

| Status | Ý nghĩa |
|---|---|
| REAL/FROZEN | Có luồng thật được ghi nhận, không viết lại |
| REAL | Có backend/API thật |
| PARTIAL | Có một phần thật |
| FRONTEND_MISSING | Backend có, UI thiếu |
| BACKEND_MISSING/MOCK | UI có, backend chưa |
| LOCAL_ONLY | Browser/device |
| PLACEHOLDER/COMING_SOON | Chưa đầy đủ |
| PLANNED | Đã có kế hoạch |
| REVIEW_REQUIRED | Cần chạy thật |
| BLOCKED | Thiếu secret/quyền/dependency |

---

## 2. Authentication và Workspace

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Login | REAL/FROZEN | Chỉ fix khi reproduce |
| Email OTP | REAL/FROZEN | Runtime smoke |
| Landing login context/logout | REAL/FROZEN | Không viết lại |
| Auth session utility | REAL | Chuẩn hóa reactive source |
| User Context | PARTIAL | Dọn nguồn/key |
| Avatar display | PARTIAL | URL/initials/icon |
| Avatar realtime update | PARTIAL | Store/session/UI |
| Logout cleanup | REVIEW_REQUIRED | Test key cũ |
| Site Selection | PARTIAL | TU-02 |
| Workspace/Site API | REAL/REVIEW | Runtime |
| Application shell | PARTIAL | Audit hai shell |

---

## 3. Project

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Project CRUD | REAL/REVIEW | Smoke |
| Project Cover backend | REAL/FROZEN | Không sửa nếu pass |
| Cover Create Project | FRONTEND_MISSING/PARTIAL | KHOI-01 |
| Cover Settings | PARTIAL | Save/thay/xóa |
| Cover list/sidebar/header | PARTIAL | Đồng bộ |
| Validation | PLANNED | Type/size |
| Không cover vẫn tạo | REQUIRED | Acceptance |
| Project member | REAL/REVIEW | Permission |
| Project report | PARTIAL | Chỉ sửa lỗi demo |

---

## 4. Task

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Task CRUD | REAL/FROZEN | Regression |
| List | REAL/FROZEN | Không redesign |
| Kanban | REAL/FROZEN | Regression |
| Calendar | REAL/FROZEN | Regression |
| Spreadsheet | REAL/FROZEN | Regression |
| Timeline | REAL/FROZEN | Regression |
| Status | REAL | Permission/idempotency |
| Priority mapping | REVIEW_REQUIRED | Verify P0 |
| Assignee | REAL | Member thật |
| Deadline | REAL | Timezone/reload |
| Checklist/Comments | REAL/REVIEW | Verify |
| Import | REAL/FROZEN | Smoke |
| Export | REAL/FROZEN | Smoke |
| Drafts | PARTIAL | Audit |
| Draft -> Task | DATA_RISK | Transaction |
| Duplicate protection | PARTIAL | Verify all flows |

---

## 5. Contingency

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Entity/API CRUD | REAL/FROZEN | API smoke |
| Task Detail UI | FRONTEND_MISSING | TU-03 |
| Support person | PLANNED | Member thật |
| Risk/impact/status | PLANNED | Enum/badge |
| Permission/403 | REQUIRED | Test |
| Audit | REAL/REVIEW | Verify |
| Reload persistence | REQUIRED | Test |

---

## 6. Cycle, Module, Goal

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Cycle/Sprint | REAL/REVIEW | AI Registry smoke |
| Module | REAL/REVIEW | AI Registry smoke |
| Goal | REAL/FROZEN | Không redesign |
| Goal Detail | REAL/FROZEN | Regression |
| Goal AI mutation | PLANNED | Tool later |

---

## 7. Productivity

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Daily Focus task data | PARTIAL | Verify |
| Daily Focus postpone | LOCAL_ONLY | Ghi rõ/backend later |
| Dashboard widget | FRONTEND_MISSING/PARTIAL | KIET-02 |
| `/stickies` | LOCAL_ONLY/PARTIAL | Giữ route |
| Global Notes Drawer | PLANNED | Utility rail |
| Floating Stickies | PLANNED | Drag/persist |
| Search/pin/color | PLANNED | API |
| Context link | PLANNED | Entity/route |

---

## 8. Notifications

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Notification core | REAL | Regression |
| Dropdown | PARTIAL/REVIEW | Avatar/source |
| Preferences cơ bản | PARTIAL | Verify |
| Preferences nâng cao | BACKLOG | Later |
| Integration notification | PLANNED | Later |

---

## 9. Permission

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Role Tree | REAL/FROZEN | Không xây lại |
| Permission Matrix | REAL/FROZEN | QA |
| Search/filter/bulk | REAL/FROZEN | Regression |
| Backend role/permission | REAL | Guard audit |
| Function Catalog | PARTIAL | Inventory |
| Action-endpoint map | PLANNED | Matrix |
| Backend enforcement | PARTIAL | Fix |
| Role History | BACKLOG/PARTIAL | Later |
| AI permission tools | BACKLOG | High risk |

---

## 10. Reports/Admin

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Reports | PARTIAL | Không viết lại |
| Report AI Registry | REAL/REVIEW | Smoke |
| Admin | PARTIAL | Scope only |
| Pricing data | REAL/REVIEW | Verify |
| AI credits | REAL/REVIEW | Verify |
| Permission Catalog | PARTIAL | Complete |

---

## 11. AI Copilot

| Feature | Status | Ghi chú/Next |
|---|---|---|
| AI Drawer | REAL/FROZEN | Smoke |
| Action Preview | REAL/FROZEN | Test |
| Confirm | REAL/FROZEN | Test |
| Cancel | REAL/REVIEW | No mutation |
| Retry | REAL/REVIEW | Idempotency |
| Duplicate protection | PARTIAL | Verify |
| Action Registry | REAL/PARTIAL | Expand later |
| Conversation history | PARTIAL/PLANNED | Audit |
| Search history | PLANNED | MVP |
| Rename/delete | PLANNED | MVP |
| Streaming | PARTIAL/PLANNED | Provider |
| Edit/regenerate/copy | PLANNED | MVP |
| Context badges | PLANNED | Implement |
| Action persistence | REQUIRED/REVIEW | F5 test |
| Citation | PLANNED | RAG |
| File upload | PLANNED | Multimodal |
| Image thumbnail | PLANNED | Composer |
| Voice | PLANNED | P2 |
| RAG | PLANNED | P2 |
| Safety pipeline | POLICY/PLANNED | Implement |
| Undo | PLANNED | Safe actions |
| Fine-tuning | DEFERRED | Eval first |

---

## 12. Integration

| Feature | Status | Ghi chú/Next |
|---|---|---|
| `/integrations` | PARTIAL | Scroll/layout fix |
| Provider cards | PARTIAL | Status thật |
| Gmail | REAL/BLOCKED | Secret/config |
| Calendar | REAL/BLOCKED | Secret/config |
| Slack | REAL/BLOCKED | Secret/config |
| GitHub | COMING_SOON | Không fake |
| Zalo | COMING_SOON | Backlog |
| Unified Inbox | PARTIAL | Stabilize |
| Provider filter | PLANNED | Implement |
| Read/unread | PLANNED | Backend |
| Search backend | PLANNED | Implement |
| Pagination | REQUIRED/PLANNED | Implement |
| Lazy loading | REQUIRED/PLANNED | Implement |
| Incremental sync | REQUIRED/PLANNED | Implement |
| Sync history/error | PLANNED | Implement |
| Task Candidate | PARTIAL/PLANNED | AI |
| Create Task button | UX_BUG | Mất destination |
| Project picker | REQUIRED | Implement |
| Preview/Confirm | REQUIRED | Reuse AI |
| Inbox-Task link | REQUIRED | F5 |
| Open original | PARTIAL/PLANNED | Verify |
| Archive in SprintA | PLANNED | Không xóa source |
| Bulk create | BACKLOG/P1 | Sau single |

---

## 13. Collaboration

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Activity Feed | MOCK | Làm MVP sau P0 |
| Daily Check-in | MOCK | Sau Feed/P0 |
| Chat | MOCK/PLACEHOLDER | Giữ route |
| SignalR | REAL/REVIEW | Verify |
| Mentions/realtime | BACKLOG | Later |

---

## 14. Forms, Docs, Development

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Forms | PLACEHOLDER | Backlog |
| Docs | PLACEHOLDER | Backlog |
| Development | PARTIAL/PLACEHOLDER | GitHub chưa thật |
| Page/View/Intake AI | REAL/REVIEW | Smoke |
| GitHub metrics | BACKLOG | Later |
| Confluence | BACKLOG | Later |

---

## 15. UI/UX

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Foundation | REAL/FROZEN | Dùng trước |
| Visual style | DECIDED | Scoped apply |
| Light/dark | REVIEW_REQUIRED | Regression |
| Responsive | REVIEW_REQUIRED | 390px+ |
| Mobbin workflow | DECIDED | Limited |
| Taste workflow | DECIDED | Audit |
| React/Vue Bits | DECIDED | Pattern only |
| GSAP | DECIDED | Reduced motion |
| Marketing dashboard | REJECTED | Không làm |
| Integration scroll | BUG/REVIEW | Fix |
| Utility rail | PLANNED | AI + Notes |

---

## 16. Engineering Quality

| Feature | Status | Ghi chú/Next |
|---|---|---|
| Backend CI | REAL/REVIEW | Verify |
| Frontend build | REAL | Gate |
| Frontend typecheck | MISSING/REVIEW | Add |
| Frontend lint | MISSING/REVIEW | Add |
| Frontend tests | MISSING/REVIEW | Add |
| Frontend E2E | MISSING/REVIEW | Smoke |
| Frontend Actions | REQUIRED | Add |
| Secrets | RISK | Fix placeholders |
| Migration gate | RISK | Fail safely |
| InMemory production | REJECTED | Enforce |
| A-E reporting | PROCESS | Mandatory |
| Definition of Done | PROCESS | Mandatory |

---

## 17. Ưu tiên gần nhất

1. Identity/avatar/site.
2. Project Cover.
3. Contingency UI.
4. i18n/Daily Focus.
5. Permission mapping/guards.
6. AI smoke.
7. Integration scroll/layout.
8. Inbox create-task destination.
9. Notes/Floating Stickies.
10. AI chat/multimodal.
11. Voice/RAG/tools.
12. Feed/Check-in/Chat.

# 18. Test Evidence and Billing Addendum

| Area | Status | Evidence rule |
|---|---|---|
| 1.000 test file | CATALOG_ONLY | 47 scenario gốc, không tính 1.000 independent pass |
| 600 test file | PARTIAL/FAIL_REPORTED | 555 reported pass, 36 fail, 9 malformed |
| Frontend automated test | MISSING/P1 | Chưa có scripts lint/typecheck/unit/E2E trong package snapshot |
| Backend SQL integration | MISSING/P1 | InMemory không xác minh RowVersion |
| AI Credits wallet | PARTIAL/PLANNED | Có usage/wallet concepts nhưng cần ledger tách |
| Pricing UI | PARTIAL/PLANNED | Package phải lấy API |
| Payment checkout | BACKEND_MISSING/PLANNED | VNPAY sandbox trước |
| Invoice/refund/reconciliation | PLANNED | Bắt buộc trước production billing |
