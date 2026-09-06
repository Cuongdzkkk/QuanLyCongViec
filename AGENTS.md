# SprintA Codex Rules

## Scope

* Khi task là UI, chỉ sửa frontend.
* Giữ nguyên endpoint và tên field API trừ khi task ghi rõ yêu cầu thay đổi contract.
* Không thêm mock data.
* Không hard-code dữ liệu để làm đẹp.
* Không thay component đang nối API thật bằng ừ component visual/mock.
* Không xóa chức năng đang chạy được.
* UI đẹp nhưng làm hỏng chức năng là task thất bại.

## Frontend stack

* Giữ Vue 3 + Element Plus + Pinia + Tailwind hiện có.
* Không cài thêm UI framework lớn như Naive UI, PrimeVue, Vuetify, Ant Design Vue.
* Ưu tiên sửa CSS, component wrapper, theme token.
* Không refactor toàn web trong một task.
* Mỗi task chỉ sửa đúng khu vực được yêu cầu.

## UI style

* Giao diện theo hướng modern SaaS productivity app.
* Tham khảo tinh thần Plane, Twenty, Vben Admin, shadcn-vue, Reka UI, Taste Skill.
* Không copy code/assets/logo từ repo tham khảo.
* Light mode sạch, sáng vừa đủ, không trắng chói.
* Dark mode xanh đen mềm, không đen bệt.
* Card, table, modal, input, button, popover phải dùng cùng hệ màu.
* Ưu tiên CSS variables/design tokens thay vì hard-code màu rải rác.
* Thay đổi UI phải nhìn thấy rõ bằng mắt.

## Git

* Không dùng `git reset --hard`.
* Không dùng `git add .`.
* Một task một commit nhỏ nếu có commit.
* Trước khi sửa phải xem `git status --short`.
* Sau khi sửa phải báo `git diff --name-only`.

## Required report

Sau mỗi task báo ngắn:

1. File đã sửa.
2. UI thay đổi rõ ở đâu.
3. Chức năng/API đã giữ nguyên.
4. Có sửa Backend không.
5. Build result.
6. Test UI đã làm.

## Agent skills

### Issue tracker

Issues and specs for this repository are tracked in GitHub Issues using the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

Use a single-context domain documentation layout. See `docs/agents/domain.md`.

## SprintA Product Priorities

Apply these priorities across SprintA unless the task explicitly narrows scope:

1. Unify the design system and responsive UX from mobile to desktop.
2. Keep Agile, Scrum and Kanban domain mechanics correct and distinct.
3. A visible function is not complete until its real end-to-end flow works.
4. Optimize first-user onboarding: account -> workspace/site -> project -> first work item.
5. Use real persisted data; production mock data is forbidden.
6. Security, permissions, accessibility and regression tests are part of the feature.

## Agent Workflow

For substantial work:

- Read `CONTEXT.md` before changing product behavior or domain terminology.
- Use the `sprinta-product-hardening` project skill when the task spans UX, domain behavior, integration, onboarding or hardening.
- Prefer the installed Matt Pocock skills for specifications, debugging, TDD, design and review when relevant.
- For visual frontend work, use `gpt-taste` or `design-taste-frontend` when installed, while treating SprintA rules and the existing component system as authoritative.
- Prefer the smallest complete vertical slice: reuse existing code before creating abstractions, use native/framework capabilities before adding dependencies, and avoid over-engineering.

Never remove validation, security, accessibility or data-integrity checks to make a change smaller. Do not install large tools or modify application architecture, API contracts, authentication, billing or business behavior unless the task explicitly requests it.
