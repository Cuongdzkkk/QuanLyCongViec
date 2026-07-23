# SPRINTA OPEN-SOURCE & DESIGN EXECUTION GUIDE

> Mục tiêu: tận dụng công cụ tốt để làm nhanh hơn nhưng không thêm dependency trùng, không copy sai framework và không phá kiến trúc hiện có.

---

## 1. Quy tắc chọn thư viện

Chỉ thêm thư viện khi đáp ứng đủ:

1. Giải quyết vấn đề thật.
2. Không trùng stack hiện có.
3. License phù hợp.
4. Maintained và có security posture chấp nhận được.
5. Tree-shake/lazy-load được nếu frontend.
6. Có test và cleanup.
7. Có owner chịu trách nhiệm.
8. Được ghi vào dependency decision log.

Không cài library chỉ để làm một hover/fade có thể viết bằng CSS.

---

## 2. Stack hiện tại phải ưu tiên

| Nhu cầu | Công cụ đang có | Cách dùng |
|---|---|---|
| UI primitives | Element Plus + Foundation wrappers | Không thêm UI framework lớn |
| State | Pinia | Một store/domain, không tạo store song song |
| Router | Vue Router | Route meta cho auth/permission/layout |
| Utility | VueUse | Dùng composable đã có trước khi tự viết |
| Rich editor | TipTap | Pages/Docs/description |
| Sanitize | DOMPurify | Render HTML an toàn |
| Realtime | SignalR | Notification/Feed/Chat/progress |
| Drag reorder | vuedraggable | Kanban/reorder; floating note cần scope rõ |
| Animation | `motion` | Drawer/list/state transition nhỏ |
| Charts | ECharts + vue-echarts | Chọn làm chart stack chính |
| Icons | Lucide Vue + Element icons | Chuẩn hóa mapping |
| HTTP | Axios | Một client/interceptor/cancellation |
| PWA | Vite PWA hiện có | Offline read trước mutation queue |

---

## 3. Nguồn thiết kế người dùng đã chọn

### Mobbin

- Là nguồn **tham khảo UX**, không phải package để copy.
- Chỉ lấy hierarchy, spacing, responsive, interaction.
- Tối đa 3 reference cùng loại màn hình.
- Không copy brand/assets/content.

### Taste Skill

- Dùng như checklist audit existing design.
- Không cho phép AI tự redesign toàn app.
- Khóa chức năng trước visual.

### Hallmark / 21st

- Dùng để tham khảo composition và polish component.
- Viết lại bằng Vue 3/Foundation.
- Không copy React/Tailwind nguyên khối nếu không phù hợp stack.

### React Bits

- React Bits là React; không paste JSX/hooks vào Vue.
- Official project hiện chỉ rõ có Vue official port.
- Với SprintA: ưu tiên Vue Bits hoặc tự port pattern nhỏ.
- Kiểm tra license của component trước khi dùng.
- Không dùng background WebGL/particle trong dashboard nghiệp vụ.

Nguồn:
- `https://github.com/DavidHDev/react-bits`
- `https://vue-bits.dev/`

### Vue Bits

Chỉ chọn component:

- Có giá trị UX rõ.
- Không làm bundle nặng.
- Có reduced motion.
- Không thay Foundation component đang nối API.
- Được copy vào source và review thay vì phụ thuộc runtime nếu license/architecture phù hợp.

Ứng dụng phù hợp:

- Landing text reveal.
- Count-up KPI một lần.
- Spotlight rất nhẹ.
- Empty-state illustration motion.
- Pricing package selection.
- AI processing indicator.

Không phù hợp:

- Task table.
- Form.
- Kanban card chạy animation liên tục.
- Report chart background.
- Admin permission matrix.

### GSAP

Dùng cho:

- Landing Product Showcase.
- Scroll reveal có kiểm soát.
- FLIP layout animation.
- Command palette/opening sequence nâng cao.
- Widget rearrangement khi `motion` không đủ.

Không dùng cho mọi button/input/table.

GSAP hiện được công bố miễn phí/no-charge cho commercial use, nhưng vẫn phải kiểm tra standard license tại thời điểm implement.

Nguồn:
- `https://github.com/greensock/GSAP`
- `https://gsap.com/standard-license/`

---

## 4. Open-source đề xuất cho Engineering

### Frontend Quality

| Tool | Vai trò | Priority |
|---|---|---|
| ESLint | Static quality | P1 |
| vue-tsc | Type checking | P1 |
| Vitest | Unit tests | P1 |
| Vue Test Utils | Component tests | P1 |
| Playwright | E2E | P1 |
| axe-core | Accessibility tests | P1 |
| MSW | Mock network trong unit test, không dùng làm production fallback | P2 |

### Backend Quality

| Tool | Vai trò | Priority |
|---|---|---|
| xUnit | Test framework | Có/giữ |
| FluentAssertions | Assertion rõ | P1 |
| WebApplicationFactory | API integration | P1 |
| Testcontainers for .NET | SQL Server thật | P1 |
| FluentValidation | Validation thống nhất nếu audit thấy phù hợp | P1 |
| Serilog | Structured logging | P1 |
| OpenTelemetry .NET | Trace/metrics | P1 |
| AspNetCore HealthChecks | Liveness/readiness | P1 |

### Background jobs

Chọn **một**:

- Hangfire: dashboard, retry, recurring jobs; phù hợp nhanh cho project.
- Quartz.NET: scheduler mạnh, ít phụ thuộc dashboard.

Đề xuất SprintA: Hangfire cho document processing, integration sync, reconciliation và recurring task; lưu job DB thật. Không chạy job quan trọng bằng `Task.Run` trong request.

### Security/CI

- Gitleaks.
- Dependabot hoặc Renovate, chọn một.
- CodeQL nếu GitHub hỗ trợ.
- Trivy cho container sau.
- OWASP ZAP baseline cho staging sau.

---

## 5. Dependency Decision Record

Mỗi dependency mới cần ghi:

```text
Name
Version
Purpose
Alternatives considered
Why existing stack is insufficient
License
Bundle/runtime impact
Security notes
Owner
Removal plan
```

Không dùng version range quá rộng nếu gây cập nhật bất ngờ. Lockfile phải commit.

---

## 6. Chart Standard

Dự án hiện có ba hệ chart. Quyết định:

- ECharts là chart chính.
- Wrapper: `vue-echarts`.
- Mỗi chart dùng shared theme.
- Lazy import chart module.
- Tooltip/legend/dark/responsive thống nhất.
- Export và accessibility fallback table.
- Chỉ gỡ Apex/Chart.js sau khi grep không còn import.

---

## 7. Editor Standard

TipTap:

- Lưu JSON document chuẩn.
- Render bằng TipTap hoặc renderer rõ.
- DOMPurify cho HTML output.
- Migration/version cho document schema.
- Không render `JSON.stringify(content)` cho user.
- Autosave debounce + save status.
- Attachment dùng secure upload.

---

## 8. Animation Budget

```text
Dashboard motion budget: low
Landing motion budget: medium
Critical form/checkout: minimal
Reduced motion: mandatory
```

Mỗi animation cần:

- Trigger.
- Duration.
- Cleanup.
- Mobile behavior.
- Reduced-motion fallback.
- Performance test.

---

## 9. Prompt-driven implementation protocol

Mọi AI Agent phải:

1. Đọc Project Bible và file phase.
2. Đọc `AGENTS.md`.
3. `git status --short`.
4. Chỉ tìm file trực tiếp liên quan.
5. Báo current state.
6. Chốt acceptance.
7. Sửa phase duy nhất.
8. Build/test.
9. `git diff --name-only`.
10. Báo cáo A–E.

Khi task UI:

- Không sửa backend.
- Nếu cần backend ghi `CẦN BACKEND`.
- Không thay component API thật bằng mock.
- Không cài UI framework mới.
- Không copy React code.

Khi task backend:

- Không redesign frontend.
- Không thay API contract nếu chưa được chốt.
- Migration riêng, review riêng.
- Permission và transaction test bắt buộc.

---

## 10. Source URLs cần giữ trong docs

- SprintA: `https://github.com/DinhTuanKhoiTB01696/QuanLyCongViec`
- React Bits: `https://github.com/DavidHDev/react-bits`
- Vue Bits: `https://vue-bits.dev/`
- GSAP: `https://github.com/greensock/GSAP`
- Gemini pricing: `https://ai.google.dev/gemini-api/docs/pricing`
- VNPAY: `https://sandbox.vnpayment.vn/apis/`
- MoMo: `https://developers.momo.vn/`
- ZaloPay: `https://docs.zalopay.vn/`

Re-check version/license/security trước khi cài.
