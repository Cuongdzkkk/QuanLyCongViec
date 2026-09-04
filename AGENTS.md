# SprintA Codex Rules

## Scope

* Khi task là UI, chỉ sửa frontend.
* Tuyệt đối không sửa Backend, Controller, Entity, Migration, DbContext, API contract.
* Không đổi endpoint API.
* Không đổi tên field API.
* Không thêm mock data.
* Không hard-code dữ liệu để làm đẹp.
* Không thay component đang nối API thật bằng ừ component visual/mock.
* Không xóa chức năng đang chạy được.
* UI đẹp nhưng làm hỏng chức năng là task thất bại.
* Nếu cần backend thì phải báo `CẦN BACKEND`, không tự sửa.

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

## Agent skills

### Issue tracker

GitHub Issues hold this repository's issues and specs; use the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

Use the single-context layout with root `CONTEXT.md` and `docs/adr/`, creating them lazily only when domain decisions are resolved. See `docs/agents/domain.md`.

## Required report

Sau mỗi task báo ngắn:

1. File đã sửa.
2. UI thay đổi rõ ở đâu.
3. Chức năng/API đã giữ nguyên.
4. Có sửa Backend không.
5. Build result.
6. Test UI đã làm.
