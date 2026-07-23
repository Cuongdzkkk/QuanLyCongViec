# SPRINTA CORE STABILIZATION PLAN

> Bắt đầu sau khi P0 Release Gate pass.
>
> Mục tiêu: biến source nhiều tính năng thành một sản phẩm ổn định, dễ dùng, có test và dễ nâng cấp.

---

## Phase P1-01 — Frontend Quality Gate

### Việc làm

- Bổ sung scripts:
  - `lint`
  - `typecheck`
  - `test:unit`
  - `test:e2e`
  - `test:e2e:smoke`
  - `check`
- Cài và cấu hình:
  - ESLint
  - vue-tsc
  - Vitest
  - Vue Test Utils
  - Playwright
  - axe-core
- Thêm GitHub Actions frontend.
- Chặn merge khi build/test fail.

### Critical smoke

1. Login.
2. Chọn Workspace.
3. Tạo Project.
4. Tạo Task.
5. Update status.
6. F5 còn dữ liệu.
7. AI Preview → Cancel.
8. AI Preview → Confirm.
9. Inbox → Create Task.
10. Buy Credits sandbox.

### Exit

- `npm run check` chạy được ở local và CI.
- Không còn file E2E “có nhưng không được gọi”.

---

## Phase P1-02 — Backend Test Architecture

### Việc làm

- Xóa test placeholder `UnitTest1`.
- Dùng xUnit + FluentAssertions.
- Dùng `WebApplicationFactory`.
- Dùng Testcontainers SQL Server cho:
  - transaction;
  - unique constraint;
  - RowVersion;
  - query filter;
  - migration.
- Tạo test data builders.
- Không dùng InMemory cho test cần relational behavior.

### Test suite

```text
Unit
Application/UseCase
API Integration
SQL Integration
Authorization
Concurrency
Migration Smoke
```

### Exit

- 36 fail có automated regression test hoặc explicit manual-only rationale.
- Critical permission có negative test.

---

## Phase P1-03 — Tách `Program.cs`

### Mục tiêu

Startup chỉ còn composition root dễ đọc.

### Cấu trúc đề xuất

```text
TaskManagement.API/
  Extensions/
    AuthenticationExtensions.cs
    AuthorizationExtensions.cs
    CorsExtensions.cs
    RateLimitExtensions.cs
    AiExtensions.cs
    IntegrationExtensions.cs
    PersistenceExtensions.cs
    ObservabilityExtensions.cs
  Hosting/
    DevelopmentDataSeeder.cs
    MigrationCommand.cs
  Middleware/
    ExceptionHandlingMiddleware.cs
    CorrelationIdMiddleware.cs
    SecurityHeadersMiddleware.cs
```

### Quy tắc

- Không raw SQL repair trong startup.
- Không catch migration rồi tiếp tục.
- Không seed mặc định.
- Không duplicate middleware registration.
- OpenAPI chỉ expose phù hợp môi trường.

---

## Phase P1-04 — API Contract và Error Handling

### Chuẩn hóa

- RFC ProblemDetails.
- Validation error có field/path.
- 401: chưa xác thực.
- 403: thiếu quyền.
- 404: không thấy resource trong phạm vi quyền.
- 409: duplicate/concurrency.
- 422: business rule nếu cần.
- 429: rate limit/provider quota.
- Correlation ID.
- Pagination envelope thống nhất.
- Idempotency header cho create/mutation nhạy cảm.

### Patch semantics

Không dùng DTO mơ hồ giữa:

- field không gửi;
- `null`;
- chuỗi rỗng;
- giá trị mới.

Dùng Patch DTO/Optional wrapper hoặc endpoint rõ.

---

## Phase P1-05 — Application Shell và State

### Audit bắt buộc

```text
Route
→ Layout
→ View
→ Store
→ API
→ Permission
```

### Mục tiêu

- Một nguồn Current User.
- Một nguồn Workspace/Site hiện tại.
- Một nguồn Project context.
- Avatar reactive.
- Không có hai shell cạnh tranh.
- Không xóa route trước khi có migration plan.

### Exit

- Login → Workspace → Project → Task có context nhất quán.
- AI/Notes/Integration dùng cùng context resolver.
- F5 restore context có kiểm soát.

---

## Phase P1-06 — Design System và UX Consistency

### Giữ stack

- Vue 3
- Element Plus
- Pinia
- Tailwind hiện có
- CSS variables/design tokens
- Lucide icons
- Motion cho transition nhỏ

### Chuẩn hóa Foundation

- AppButton
- AppIconButton
- AppInput
- AppSelect
- AppAvatar
- AppBadge
- AppCard
- AppModal
- AppDrawer
- AppEmptyState
- AppErrorState
- AppSkeleton
- AppFileCard
- AppContextChip
- AppActionPreview
- AppDataTable
- AppPageHeader

### Design token

```text
color-bg
color-surface
color-surface-elevated
color-border
color-text
color-text-muted
color-accent
color-success/warning/danger
radius-sm/md/lg
space-1..8
shadow-1..3
z-index scale
motion-fast/normal
```

### Rule

- Không redesign toàn bộ.
- Mỗi PR một screen/flow.
- Light/dark/responsive/accessibility bắt buộc.
- Dashboard không dùng particle/3D nền.

---

## Phase P1-07 — Report Dashboard

### Acceptance từ file AI Document Test

Trong viewport desktop đầu tiên phải thấy:

- Tổng Task.
- Completed.
- In Progress.
- Overdue.

### Bố cục

```text
Page Header + Date/Scope Filter
KPI row 4 cards
Trend + Status Distribution
Workload + Deadline Risk
Project Health table
Export actions
```

### Backend

- Server-side aggregate.
- Filter Workspace/Project/date/member.
- Query index.
- Cache theo scope/version.
- Không tải toàn bộ Task về frontend để đếm.

### UX

- Click KPI drill-down Task list.
- Skeleton.
- Empty/error.
- Responsive card grid.
- ECharts là chart stack chính.

---

## Phase P1-08 — Export Excel/CSV

### Pipeline

```text
Query
→ Permission-filtered Export DTO
→ Flatten nested fields
→ Format timezone/status/user/labels
→ CSV/XLSX writer
→ Authorized download
```

### Test

- Assignee object/list.
- Labels.
- Null.
- Vietnamese Unicode.
- CSV formula injection (`=`, `+`, `-`, `@`).
- Timezone.
- 10k rows.
- Export/import round trip.
- Không `[object Object]`.

---

## Phase P1-09 — Pages/Docs Reliability

### Dùng stack hiện có

- TipTap.
- DOMPurify.
- Existing upload pipeline sau khi được bảo vệ.

### Chức năng

- JSON TipTap renderer.
- Autosave state.
- Revision history.
- Icon/cover optional.
- Mentions.
- Task/Project link.
- Attachment.
- Search.
- Permission.
- Restore version.
- Empty/loading/error.

### Security

- Sanitize rendered HTML.
- Không thực thi script/embed lạ.
- Permission theo Workspace/Page.

---

## Phase P1-10 — AI Document Intake

### File support MVP

- TXT
- DOCX
- PDF

### Pipeline

```text
Upload
→ Validate MIME/signature/size
→ Secure storage
→ Extract text
→ Chunk
→ Redact secret
→ Analyze
→ Summary
→ Task candidates
→ Source citation
→ Preview
→ Confirm
→ Transaction create
```

### Task Candidate fields

- Title.
- Description.
- Owner suggestion.
- Deadline.
- Priority.
- Project destination.
- Confidence.
- Source page/paragraph.
- Selected flag.

### Exit

- Chạy pass acceptance test trên cả ba loại file.
- Không bịa owner/date.
- Cancel 0 Task.
- Confirm đúng Task được chọn.
- Retry không duplicate.

---

## Phase P1-11 — Dependency và Bundle Cleanup

Frontend đang dùng ApexCharts, Chart.js và ECharts cùng lúc.

### Quyết định đề xuất

- Giữ ECharts + `vue-echarts`.
- Migrate chart active theo từng trang.
- Sau khi không còn import:
  - gỡ ApexCharts/vue3-apexcharts;
  - gỡ Chart.js/vue-chartjs.

### Khác

- Route lazy loading.
- Editor/chart/admin chunks.
- Bundle budget.
- Không import icon toàn bộ.
- Virtual list cho dữ liệu lớn.
- Abort request khi search/filter đổi.

---

## Phase P1-12 — Observability

### Backend

- Serilog structured logs.
- Correlation ID.
- OpenTelemetry traces.
- Health checks:
  - live;
  - ready;
  - SQL;
  - storage.
- Slow query log.
- AI provider latency/cost.
- Integration sync metrics.

### Frontend

- Global error boundary/handler.
- API correlation ID display trong error details.
- Web Vitals.
- Route load/bundle metrics.
- Không log token hoặc sensitive payload.

---

## P1 Release Gate

- P0 vẫn pass.
- Frontend và backend CI pass.
- SQL integration pass.
- Dashboard acceptance pass.
- Export không object/raw formula issue.
- Pages không JSON thô/XSS.
- AI Document Intake pass TXT/DOCX/PDF.
- Không critical console/network errors.
- Responsive 390px, tablet, laptop.
