# SPRINTA ROADMAP

> Quản lý thứ tự ưu tiên, dependency và tiêu chí hoàn thành.
>
> Cập nhật: 17/07/2026  
> Nguyên tắc: ổn định luồng thật trước, sau đó mới mở rộng AI và integration.

---

## 1. Legend

| Ký hiệu | Ý nghĩa |
|---|---|
| ✅ | Có hoặc được xem là frozen |
| 🟡 | Partial/review |
| 🔵 | Planned/next |
| ⚪ | Backlog |
| ⛔ | Không làm hiện tại |
| 🔒 | Không viết lại |

---

## 2. North Star

SprintA cần đạt trạng thái:

- Login, Workspace, Project và Task không có dữ liệu giả.
- Dữ liệu quan trọng còn sau F5.
- Permission backend đúng.
- AI hiểu context và thao tác an toàn.
- Inbox biến thông tin thành Task có đích rõ.
- Notes gọi được ở mọi trang.
- UI hiện đại nhưng không làm mất nghiệp vụ.
- Có build/test/CI và tài liệu bàn giao.

---

# MILESTONE 0 - Baseline và phân loại

Mục tiêu: xác định REAL, PARTIAL, LOCAL_ONLY, MOCK, PLACEHOLDER.

- [ ] Chạy frontend/backend ở môi trường sạch.
- [ ] Kiểm tra migration và secrets.
- [ ] Map route -> layout -> component.
- [ ] Map frontend action -> API -> database.
- [ ] Gắn trạng thái trung thực.
- [ ] Cập nhật `SPRINTA_FEATURE_STATUS.md`.
- [ ] Chụp evidence demo flow.

Exit:

- Không còn fake success.
- Có module/action/API/permission matrix.
- Có danh sách P0 runtime bug.

---

# MILESTONE 1 - Core/Demo Stabilization

Ưu tiên: P0  
Trạng thái: 🟡

## 1. Identity

### TU-01 - User Context và Avatar

- [ ] Một current-user reactive source.
- [ ] Session/store/component cùng payload.
- [ ] Dọn key cũ.
- [ ] Đổi avatar cập nhật không reload.
- [ ] F5 không mất avatar.
- [ ] Logout xóa sạch.
- [ ] Không user/email fake.

### TU-02 - Site Selection

- [ ] Brand/action bên trái.
- [ ] Avatar/name/dropdown/logout bên phải.
- [ ] Site API thật.
- [ ] Create/join không hỏng.
- [ ] Action thiếu flow disable + tooltip.
- [ ] Responsive 390px.

## 2. Project

### KHOI-01 - Project Cover frontend

- [ ] Chọn ảnh trong Create Project.
- [ ] Preview và alt text.
- [ ] PNG/JPG/WEBP.
- [ ] Validation size.
- [ ] Không cover vẫn tạo Project.
- [ ] Đồng bộ list/sidebar/header/settings.
- [ ] Thay/xóa không cache sai.
- [ ] Double-click không duplicate.

## 3. Task

### TU-03 - Contingency Plan frontend

- [ ] Tìm đúng Task Detail đang dùng.
- [ ] CRUD thật.
- [ ] Project member thật.
- [ ] Risk/impact/status badge.
- [ ] Validation.
- [ ] 403 đúng.
- [ ] Reload còn dữ liệu.
- [ ] Delete confirm.
- [ ] Chống duplicate.

### Draft conversion

- [ ] Audit field bị mất.
- [ ] Endpoint transaction.
- [ ] Tạo Task + relation + xóa Draft atomic.
- [ ] Idempotency.
- [ ] Rollback test.

## 4. Productivity

### KIET-02 - Daily Focus

- [ ] Widget vào dashboard.
- [ ] Task thật.
- [ ] Postpone local-only được ghi rõ hoặc chuyển backend.
- [ ] Responsive/light/dark.
- [ ] Empty/loading/error.

## 5. i18n

- [ ] VI/EN cho demo flow.
- [ ] Không key thô.
- [ ] Text dài không vỡ.
- [ ] Server error hiển thị hợp lý.

## 6. Permission

- [ ] Module/action matrix.
- [ ] Map endpoint.
- [ ] Backend guard critical.
- [ ] Không quyền trùng.
- [ ] Role UI chỉ QA/sửa nhỏ.
- [ ] Role History để sau nếu gấp.

## 7. AI Smoke

- [ ] Preview trước mutation.
- [ ] Cancel không tạo entity.
- [ ] Confirm tạo đúng một entity.
- [ ] Retry không duplicate.
- [ ] Action state sau F5.
- [ ] Entity link.
- [ ] Permission/audit.

## 8. Demo Reliability

- [ ] Frontend build.
- [ ] Backend build/test.
- [ ] Không secret trong git.
- [ ] Không 404/500 demo flow.
- [ ] Không console error nghiêm trọng.
- [ ] Priority mapping đúng.
- [ ] Reload không mất dữ liệu.
- [ ] Seed và tài khoản demo ổn định.

---

# MILESTONE 2 - Layout và Navigation

Ưu tiên: P0/P1  
Trạng thái: 🔵

## Application Shell

- [ ] Audit NexusLayout/HomeSiteLayout.
- [ ] Xác định shell chính.
- [ ] Không hai profile/dashboard mâu thuẫn.
- [ ] Header/sidebar/project nav sticky đúng.
- [ ] Một scroll strategy rõ.
- [ ] Không content bị cắt/khoảng trắng.
- [ ] Z-index scale.

## Integration layout fix

Làm trước Floating Stickies vì cùng chạm global layout.

- [ ] Sửa scroll `/integrations`.
- [ ] List/detail không bị cắt.
- [ ] Pane responsive.
- [ ] Empty/error/loading.
- [ ] Không redesign ngoài trang.

## Global Utility Rail

- [ ] Rail AI + Notes.
- [ ] Chỉ một drawer mở.
- [ ] Content width đúng.
- [ ] Keyboard/focus.
- [ ] Mobile bottom sheet/chips.
- [ ] Reduced motion.

---

# MILESTONE 3 - Integration Core

Ưu tiên: P1  
Trạng thái: 🟡/🔵

## Provider baseline

- [ ] Gmail credentials/OAuth.
- [ ] Calendar credentials/OAuth.
- [ ] Slack credentials/OAuth.
- [ ] Secret configuration guide.
- [ ] OAuth state/redirect validation.
- [ ] Connect/disconnect thật.
- [ ] GitHub/Zalo không fake connected.

## Sync architecture

- [ ] Provider adapter.
- [ ] Normalized InboxItem.
- [ ] Cursor/incremental sync.
- [ ] Pagination.
- [ ] Lazy loading.
- [ ] Retry/backoff.
- [ ] Sync history.
- [ ] Last synced/error.
- [ ] Không sync toàn mailbox một lần.

## Unified Inbox

- [ ] Provider filter.
- [ ] Backend search.
- [ ] Read/unread.
- [ ] Task candidate.
- [ ] Created-task filter.
- [ ] Open original.
- [ ] Archive trong SprintA.
- [ ] Attachment metadata.
- [ ] Bulk selection sau single flow.

## Create Task từ Inbox

- [ ] Hiện Project đích.
- [ ] Chưa có Project thì bắt chọn.
- [ ] Status/list/module nếu áp dụng.
- [ ] Preview title/description/deadline/source.
- [ ] Confirm.
- [ ] Idempotency.
- [ ] Lưu InboxItem-Task link.
- [ ] Hiện Task ID/title/project/status.
- [ ] Mở Task/Work Items.
- [ ] F5 vẫn biết đã tạo.

---

# MILESTONE 4 - Global Stickies

Ưu tiên: P1  
Trạng thái: 🔵

## Phase A - Backend

- [ ] Audit entity Sticky/Note.
- [ ] User isolation.
- [ ] CRUD API.
- [ ] Search/pin/color.
- [ ] Context link.
- [ ] Autosave debounce.
- [ ] Concurrency/version nếu cần.

## Phase B - Drawer

- [ ] Nút Notes mọi route.
- [ ] Drawer 360-420px.
- [ ] Create/search/pin.
- [ ] Link `/stickies`.
- [ ] AI/Notes mutual exclusion.
- [ ] Mobile.
- [ ] API persistence.

## Phase C - Floating MVP

- [ ] Drag handle.
- [ ] Drop vào content.
- [ ] Clamp viewport.
- [ ] Tối đa khoảng 5 note.
- [ ] Move/focus/z-index.
- [ ] Close không delete.
- [ ] F5 còn vị trí.
- [ ] Scope route/project/global.

## Phase D - Nâng cao

- [ ] Resize.
- [ ] Minimize.
- [ ] Mobile chips.
- [ ] Collision/stacking.
- [ ] Keyboard move.
- [ ] Viewport normalization.
- [ ] Undo delete.

---

# MILESTONE 5 - AI Chat Foundation

Ưu tiên: P1  
Trạng thái: 🟡/🔵

- [ ] New conversation.
- [ ] History.
- [ ] Open/rename/delete.
- [ ] Search.
- [ ] Streaming.
- [ ] Copy/regenerate/edit.
- [ ] Loading/error/rate limit.
- [ ] Prompt suggestions.
- [ ] Context badges.
- [ ] Action timeline.
- [ ] Preview/Confirm/Cancel/Retry.
- [ ] Action persistence.
- [ ] Internal citations.

## Context Resolver

- [ ] Current user.
- [ ] Workspace.
- [ ] Project.
- [ ] Task.
- [ ] Goal.
- [ ] Route.
- [ ] Permission.
- [ ] Language/timezone.
- [ ] User đổi/xóa context.

---

# MILESTONE 6 - AI Multimodal

Ưu tiên: P1/P2  
Trạng thái: 🔵

## Phase 0 - Audit

- [ ] Chat/attachment/storage schema.
- [ ] Không làm lại history/action flow.
- [ ] Provider limits.

## Phase 1 - Composer

- [ ] Nút `+`.
- [ ] Paste ảnh.
- [ ] Drag/drop.
- [ ] Upload.
- [ ] Thumbnail ảnh thật.
- [ ] File card.
- [ ] Remove trước gửi.
- [ ] Progress/error/retry.
- [ ] Context chips.

## Phase 2 - Validation

Hỗ trợ MVP:

- PNG, JPG/JPEG, WEBP.
- PDF, DOCX, TXT, MD.
- CSV, XLSX, PPTX.
- JSON.
- Source code text giới hạn.

Chặn EXE/APK/DLL, file mã hóa, archive không kiểm soát, file nguy hiểm và parser không an toàn.

## Phase 3 - Processing

- [ ] Object/file storage.
- [ ] Không base64 DB.
- [ ] Extract text.
- [ ] Thumbnail.
- [ ] Malware/type validation.
- [ ] Permission.
- [ ] Retention/delete.

---

# MILESTONE 7 - Voice

Ưu tiên: P2  
Trạng thái: 🔵

- [ ] Microphone permission.
- [ ] Recording/timer.
- [ ] VI/EN auto/manual.
- [ ] Transcript editable.
- [ ] Thu lại.
- [ ] Dùng nội dung này.
- [ ] Error/no speech.
- [ ] Không gửi tự động.
- [ ] Retention policy.

Không ưu tiên voice realtime liên tục hoặc computer control.

---

# MILESTONE 8 - RAG

Ưu tiên: P2  
Trạng thái: 🔵

- [ ] Data source inventory.
- [ ] Permission-aware ingestion.
- [ ] Chunk/metadata.
- [ ] Index/embedding.
- [ ] Retrieval/rerank.
- [ ] Citation.
- [ ] Freshness/delete.
- [ ] Prompt injection defense.
- [ ] Evaluation dataset.
- [ ] Hallucination/fallback UX.

Nguồn: Page/Docs, attachment, Task comment, Project, Report, Integration item, policy nội bộ.

---

# MILESTONE 9 - Tool Registry Expansion

Ưu tiên: P2  
Trạng thái: 🟡/🔵

## Wave 1 - Read

- [ ] Search/get task.
- [ ] Project/Sprint summary.
- [ ] Workload.
- [ ] Overdue/blocker.
- [ ] Report draft.

## Wave 2 - Task mutation

- [ ] Create/update Task.
- [ ] Status/assignee/deadline.
- [ ] Comment/checklist.
- [ ] Contingency Plan.

## Wave 3 - Productivity

- [ ] Create/search Sticky.
- [ ] Daily brief.
- [ ] Meeting summary.
- [ ] Reminder/report.

## Wave 4 - Project/Cycle/Goal

- [ ] Project update/member.
- [ ] Cycle/move task.
- [ ] Goal/progress.
- [ ] Bulk/high-risk sau permission/audit/undo.

---

# MILESTONE 10 - Collaboration

Ưu tiên: P2/P3  
Trạng thái: ⚪

1. Activity Feed thật.
2. Daily Check-in thật.
3. Chat channel/DM.
4. Mention/notification.
5. Search/history.
6. File/link integration.

Không bắt đầu full Chat trước khi core P0 ổn định.

---

# MILESTONE 11 - Product Expansion

Ưu tiên: P3  
Trạng thái: ⚪

- Forms.
- Docs integration.
- GitHub.
- Zalo.
- Development metrics.
- Advanced Reports.
- Advanced Notification Preferences.
- Conversation export/share.
- AI evaluation dashboard.
- Enterprise audit/retention.

---

# Không làm khi còn P0

- ⛔ Video call.
- ⛔ Full DM ecosystem.
- ⛔ GitHub metrics hoàn chỉnh.
- ⛔ Confluence.
- ⛔ Full form builder.
- ⛔ 14 nhóm notification preferences.
- ⛔ TOTP/backup codes nếu P0 chưa xong.
- ⛔ Zalo.
- ⛔ 3D/particle dashboard.
- ⛔ Redesign toàn Admin.
- ⛔ Microservice migration.
- ⛔ Thay framework/refactor toàn backend.
- ⛔ Viết lại Goal/Project/Task core.
- ⛔ AI mutation không confirm.

---

# Kế hoạch 4 ngày khi cực gấp

## Ngày 1

- Tú: Avatar + Site Selection.
- Khôi: Project Cover.
- Kiệt: i18n.
- Quân Đạt: baseline/test matrix.
- Phát: permission inventory.
- Quân: backend/test, chưa làm Chat.

## Ngày 2

- Tú: Contingency UI.
- Kiệt: Daily Focus.
- Khôi: AI smoke + integration.
- Quân: Feed tối thiểu hoặc P0 fix.
- Quân Đạt/Phát: test.

## Ngày 3

- Merge.
- Regression.
- Fix P0/P1.
- Báo cáo.

## Ngày 4

- Freeze.
- Demo rehearsal.
- Không thêm feature.

---

# Branch và merge order

```text
integration/demo-release
feat/khoi-project-cover
fix/khoi-ai-smoke
fix/tu-auth-avatar-context
feat/tu-task-contingency-ui
feat/quan-activity-feed
feat/quan-daily-checkin
perf/qdat-measured-optimizations
fix/kiet-i18n-critical
feat/kiet-daily-focus-widget
docs/phat-function-permission-catalog
```

Merge:

1. Identity.
2. i18n.
3. Project Cover.
4. Contingency.
5. Daily Focus.
6. Performance.
7. Permission catalog.
8. Backend permission fixes.
9. Activity Feed.
10. Daily Check-in.

Shared file/API/migration cần Khôi review.

---

# Release Gate

Không release/demo nếu còn:

- Secret placeholder.
- Migration lỗi.
- 404/500 demo flow.
- Fake success.
- Duplicate mutation.
- Avatar/user sai.
- Task tạo không rõ Project.
- Dữ liệu mất sau F5.
- Permission chỉ frontend.
- UI cắt content nghiêm trọng.
- Build fail.
- Console error nghiêm trọng.

# 19. EXECUTION UPDATE — 17/07/2026

Thứ tự mới:

```text
W0 Baseline/Test Truth
→ W1 Security/Authorization
→ W2 Data Integrity/Concurrency
→ W3 Hosting/Database Safety
→ W4 Test/CI
→ W5 Core UX/Architecture
→ W6 AI Credits/Billing
→ W7 Product Completion
→ W8 Enterprise
```

Chi tiết task ID nằm trong `SPRINTA_MASTER_BACKLOG.md`.

Billing chỉ bắt đầu sau P0 Release Gate. Advanced features thực hiện một feature mỗi phase.
