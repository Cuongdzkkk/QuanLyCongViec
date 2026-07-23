# SPRINTA MASTER BACKLOG

> Đây là thứ tự thực thi chính thức. Không kéo P2/P3 lên trước P0.

## WAVE 0 — Freeze và Baseline

| ID | P | Nhiệm vụ | Exit |
|---|---|---|---|
| BASE-01 | P0 | Tạo branch integration, backup DB, ghi commit SHA | Có baseline và rollback |
| BASE-02 | P0 | Sửa 9 test malformed; đổi 555 pass thành PASS_REPORTED | Test data sạch |
| BASE-03 | P0 | Reproduce 36 fail trên source hiện tại | Defect evidence |
| BASE-04 | P0 | Secret scan toàn Git history | Không secret active |
| BASE-05 | P0 | Map route → component → API → permission | Không sửa nhầm component |

## WAVE 1 — Security và Authorization

| ID | P | Nhiệm vụ | Test liên quan |
|---|---|---|---|
| SEC-01 | P0 | OTP rate limit, retry lock, secure generation | AUTH-003, AUTH-007 |
| SEC-02 | P0 | Chặn deleted/inactive login và refresh | AUTH-029 |
| SEC-03 | P0 | WorkspaceMember.IsActive policy toàn hệ thống | WS-006 |
| SEC-04 | P0 | Canonical role/permission; loại bypass ngoài policy | AI-002, RBAC-009 |
| SEC-05 | P0 | Sprint lock backend | RBAC-045 |
| SEC-06 | P0 | Upload authorization, HTTPS, secret redaction | Repo audit |
| SEC-07 | P0 | AI prompt-injection/permission guard | AI Document Test |

## WAVE 2 — Data Integrity và Concurrency

| ID | P | Nhiệm vụ | Test liên quan |
|---|---|---|---|
| DATA-01 | P0 | Graph cycle detection | DEP-003, DEP-025 |
| DATA-02 | P0 | Chặn rollover cross-project | SPRINT-020 |
| DATA-03 | P0 | Invitation unique + transaction | MEM-006 |
| DATA-04 | P0 | Soft-delete assignment history | MEM-023 |
| DATA-05 | P0 | Workspace context từ route/current selection | GOAL-005 |
| DATA-06 | P0 | Reporter từ current authenticated user | TASK-038 |
| DATA-07 | P0 | RowVersion conflict contract 409 | RBAC-050, TASK-024 |
| DATA-08 | P0 | Reward ledger rollback/idempotency | GAMIFY-003 |

## WAVE 3 — Hosting, Database và Repository Safety

| ID | P | Nhiệm vụ | Exit |
|---|---|---|---|
| HOST-01 | P0 | Không InMemory fallback ngoài Testing | Production fail fast |
| HOST-02 | P0 | Tách migration khỏi app startup | Deploy job riêng |
| HOST-03 | P0 | Xóa raw schema repair khỏi Program.cs | Migration là source of truth |
| HOST-04 | P0 | Seed mock chỉ bằng flag Development | Không data giả production |
| HOST-05 | P0 | HTTPS/HSTS/forwarded headers đúng | Secure transport |
| HOST-06 | P0 | Private file download có authorization | Không public `/uploads` tùy tiện |
| HOST-07 | P1 | Dọn artifact/log/script tạm khỏi tracked source | Repo sạch |

## WAVE 4 — Test và CI

| ID | P | Nhiệm vụ | Exit |
|---|---|---|---|
| QA-01 | P1 | Thêm frontend lint/typecheck/unit/build scripts | Local pass |
| QA-02 | P1 | Playwright critical smoke | Login/Project/Task/AI |
| QA-03 | P1 | SQL Server Testcontainers | Concurrency/transaction |
| QA-04 | P1 | Frontend GitHub Actions | PR gate |
| QA-05 | P1 | Secret/dependency scan | PR gate |
| QA-06 | P1 | Coverage và test evidence report | Measured quality |

## WAVE 5 — Core UX và Architecture

| ID | P | Nhiệm vụ | Exit |
|---|---|---|---|
| CORE-01 | P1 | Tách Program.cs thành extension/host services | Startup dễ kiểm soát |
| CORE-02 | P1 | ProblemDetails, validation, correlation ID | API contract thống nhất |
| CORE-03 | P1 | Audit hai application shell và user context | Một nguồn state |
| CORE-04 | P1 | Chuẩn hóa chart stack về ECharts | Bundle/UI nhất quán |
| CORE-05 | P1 | Report 4 KPI trong một viewport | AI Document acceptance |
| CORE-06 | P1 | Export DTO flatten + CSV/XLSX safety | Không `[object Object]` |
| CORE-07 | P1 | Pages TipTap renderer + DOMPurify | Không JSON thô |
| CORE-08 | P1 | AI TXT/DOCX/PDF intake E2E | Preview/Confirm |
| CORE-09 | P1 | Health checks, logs, tracing | Operable |

## WAVE 6 — AI Credits và Billing

| ID | P | Nhiệm vụ | Exit |
|---|---|---|---|
| BILL-01 | P1 | Tách Point Wallet và AI Credit Wallet | Ledger độc lập |
| BILL-02 | P1 | Package/Price/Usage schema | Admin-configurable |
| BILL-03 | P1 | Credit reservation/finalization | Không âm/không trừ sai |
| BILL-04 | P1 | VNPAY sandbox adapter đầu tiên | Signed callback/query/refund |
| BILL-05 | P1 | Pricing/Wallet/Checkout UI | Dễ hiểu |
| BILL-06 | P1 | Invoice/history/refund/admin reconciliation | Audit đầy đủ |
| BILL-07 | P2 | MoMo/ZaloPay adapters | Sau VNPAY |

## WAVE 7 — Product Completion

- Custom Fields
- Recurring Tasks
- Templates
- Automation Rules
- Time Tracking
- Workload/Capacity
- Global Search/Command Palette
- Saved Views
- Forms
- Docs/Pages nâng cao
- Activity Feed → Check-in → Chat
- Integration incremental sync
- Global/Floating Stickies
- AI RAG/File/Voice/Tool expansion

## WAVE 8 — Enterprise

- SSO/OIDC
- Audit export
- Retention
- Workspace billing/team wallet
- Backup/restore
- Advanced observability
- Enterprise AI policies
