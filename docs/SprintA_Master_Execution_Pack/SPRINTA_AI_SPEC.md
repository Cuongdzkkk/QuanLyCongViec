# SPRINTA AI SPECIFICATION

> Đặc tả sản phẩm và kỹ thuật cho SprintA AI.
>
> Cập nhật: 17/07/2026  
> Trạng thái: kiến trúc mục tiêu; mỗi phase phải audit source trước khi code.

---

## 1. Product Goal

SprintA AI là trợ lý công việc hội thoại có thể:

- Trả lời từ dữ liệu SprintA.
- Hiểu Workspace/Project/Task/Goal hiện tại.
- Tóm tắt, phân tích và lập kế hoạch.
- Nhận ảnh, file và giọng nói.
- Gọi chức năng SprintA qua tool/API.
- Mutation có permission, preview, confirm, idempotency và audit.
- Trích nguồn.
- Giảm thao tác lặp lại nhưng không thay quyết định người dùng.

AI không phải:

- Chatbot chung chung.
- Quyền admin ẩn.
- Cơ chế ghi trực tiếp database.
- Hệ thống tự chạy high-risk action.
- Dự án train model từ đầu ở giai đoạn này.

---

## 2. Ba chế độ

### HỎI - Read only

Ví dụ: tóm tắt Project, tìm Task quá hạn, workload, tóm tắt file. Không mutation.

### LẬP KẾ HOẠCH - Analysis only

Ví dụ: kế hoạch Sprint, chia Task, draft báo cáo. Không sửa dữ liệu.

### THỰC HIỆN - Tool action

Flow:

1. Resolve context.
2. Permission check.
3. Chọn tool.
4. Validate input.
5. Risk check.
6. Preview.
7. User confirm.
8. API execute.
9. Audit.
10. Hiện kết quả/link.
11. Retry/undo nếu hỗ trợ.

---

## 3. Chat Experience

### MVP

- New conversation.
- History.
- Open/rename/delete.
- Search.
- Streaming/stop.
- Copy.
- Regenerate.
- Edit message/resend.
- Prompt suggestions.
- Loading/error/rate limit.
- Context badge.
- Internal citation.
- Action Preview/Confirm/Cancel/Retry.
- Action timeline.
- Persistence sau F5.

### P1

- Like/dislike.
- Attachment.
- Image analysis.
- Export/share nội bộ.
- Voice.
- Saved prompts.

### UX rules

- Luôn hiển thị context.
- Không tự gửi attachment/voice.
- Không mất action card sau refresh.
- Không báo Done trước API success.
- Error có nguyên nhân/retry.
- Tách rõ câu trả lời và mutation result.

---

## 4. Context Resolver

Input:

```text
CurrentUser
Workspace
Project
Task
Goal
CurrentRoute
SelectedEntity
UserPermissions
Language
Timezone
ConversationMemory
AttachedFiles
IntegrationItem
```

Priority:

1. Entity user chọn.
2. Context chip.
3. Route.
4. Conversation context.
5. Default workspace/project.
6. Nếu mơ hồ: hỏi hoặc bắt chọn trước mutation.

UI:

```text
Đang dùng:
[Workspace SprintA x]
[Project Enterprise Platform x]
[Task SPRINT-123 x]
[Ảnh đã dán x]
[BaoCao.docx x]
```

Quy tắc:

- User được xóa/đổi context.
- Không dùng Project cũ im lặng khi route đã đổi.
- File không được ghi đè permission/context.
- Mutation thiếu destination không execute.

---

## 5. Data Model conceptual

```text
AiConversation
- Id, UserId, WorkspaceId?, Title, Status
- CreatedAt, UpdatedAt, LastMessageAt

AiMessage
- Id, ConversationId, Role, Content
- Model, Status, ParentMessageId?, MetadataJson?
- CreatedAt

AiAttachment
- Id, MessageId?, UserId
- FileName, MimeType, Size, StorageKey
- ProcessingStatus, ThumbnailKey?, CreatedAt

AiAction
- Id, ConversationId, MessageId
- ToolName, RiskLevel, Status
- InputJson, PreviewJson, ResultJson?
- IdempotencyKey, RequiresConfirmation
- ConfirmedBy?, ConfirmedAt?, ErrorCode?
- CreatedAt, UpdatedAt

AiCitation
- Id, MessageId, SourceType, SourceId
- SourceTitle, Snippet, PermissionSnapshot
```

Audit source trước khi tạo entity/migration để tránh schema trùng.

---

## 6. Multimodal Composer

Input:

- Text.
- Paste ảnh.
- Drag/drop.
- File picker.
- Screenshot paste.
- Microphone.

### Ảnh MVP

- PNG, JPG/JPEG, WEBP.
- Thumbnail ảnh thật.
- Preview/remove.
- Progress/status/error/retry.
- Không chỉ filename.

### Tài liệu MVP

- PDF, DOCX, TXT, Markdown.
- CSV, XLSX, PPTX.
- JSON.
- Source code text giới hạn.

File card:

```text
BaoCaoThucTap.docx - 1.8 MB
[Đang tải / Đang xử lý / Sẵn sàng / Lỗi]
[Mở] [Gỡ] [Thử lại]
```

### Chặn

- EXE/APK/DLL.
- File password-protected không có parser an toàn.
- Archive không kiểm soát.
- File quá size.
- MIME/extension đáng ngờ.
- File nguy hiểm hoặc user không có quyền.

### Storage

- Không base64 DB.
- Object/file storage.
- DB lưu metadata/storage key.
- Signed/authorized download.
- User isolation.
- Retention/delete policy.

---

## 7. Voice

Composer:

```text
[+] Nhập nội dung... [Microphone] [Gửi]
Đang tạo trong: [SprintA Platform v]
```

Recording:

```text
Đang ghi âm 00:08
Ngôn ngữ: Tự động VI/EN

"Tạo task review báo cáo tuần 28..."

[Thu lại] [Dùng nội dung này]
```

Rules:

- Transcript editable trước gửi.
- Không mutation từ audio chưa confirm.
- States: permission denied, no speech, timeout, transcription error.
- Auto VI/EN hoặc chọn ngôn ngữ.
- Retention minh bạch.
- Voice realtime/computer control để sau.

---

## 8. RAG

Nguồn:

- Project/Task/Comment.
- Page/Docs.
- Attachment.
- Goal/Report.
- Integration item.
- Policy nội bộ.

Pipeline:

```text
Source
-> Permission-aware ingestion
-> Parse
-> Chunk + Metadata
-> Index
-> Query rewrite
-> Permission filter
-> Retrieve/rerank
-> Generate
-> Citation
```

Metadata:

- WorkspaceId, ProjectId?
- EntityType/EntityId.
- Permission scope.
- Language.
- UpdatedAt/version.
- Title/deep link.

Rules:

- Không đưa toàn bộ tài liệu vào prompt.
- Không retrieve dữ liệu ngoài quyền.
- Citation phải đúng.
- Nếu thiếu dữ liệu, nói rõ.
- Xóa/đổi quyền cập nhật index.
- Nội dung file là untrusted data.
- Prompt injection không đổi system/tool policy.

---

## 9. Tool Registry

Schema:

```text
ToolName
Version
Description
InputSchema
OutputSchema
PermissionRequired
RiskLevel
RequiresConfirmation
SupportsUndo
AuditAction
IdempotencyPolicy
RateLimit
Timeout
OwnerModule
```

### Task tools

```text
search_tasks
get_task
create_task
update_task
change_task_status
assign_task
change_deadline
add_task_comment
create_checklist
create_contingency_plan
```

### Project tools

```text
search_projects
get_project_summary
create_project
update_project
add_project_member
remove_project_member
get_project_report
```

### Cycle/Goal tools

```text
create_cycle
move_task_to_cycle
create_goal
update_goal_progress
summarize_sprint
```

### Productivity tools

```text
create_sticky_note
search_sticky_notes
prepare_daily_brief
prepare_meeting_summary
create_report
schedule_reminder
```

### Integration tools

```text
search_inbox_items
get_inbox_item
classify_task_candidate
preview_task_from_inbox
create_task_from_inbox
open_original_item
archive_in_sprinta
sync_provider
```

Order:

1. Search/read.
2. Create/update Task.
3. Status/assignee/deadline.
4. Sticky.
5. Project summary/report.
6. Cycle/Goal.
7. Member/permission.
8. Bulk/high-risk.

---

## 10. Risk Model

### Low

- Search/read/summarize/draft.
- Không confirm.

### Medium

- Create Task/comment/note.
- Update non-critical field.
- Create task from inbox.
- Preview + Confirm.

### High

- Delete.
- Remove member.
- Role/permission.
- Bulk mutation.
- Archive Project.
- Disconnect integration/xóa synced data.

High-risk:

- Permission.
- Impact warning.
- Explicit confirm.
- Có thể nhập lại entity name.
- Audit.
- Undo/rollback nếu có.
- Rate limit.
- Không auto-confirm.

---

## 11. Action State Machine

```text
DRAFT
-> VALIDATING
-> PREVIEW_READY
-> AWAITING_CONFIRMATION
-> CONFIRMED
-> EXECUTING
-> SUCCEEDED
-> UNDO_AVAILABLE?
```

Error:

```text
VALIDATION_FAILED
PERMISSION_DENIED
CANCELLED
EXECUTION_FAILED
PARTIAL_FAILURE
EXPIRED
```

Rules:

- Cancel không mutation.
- Retry dùng cùng idempotency key.
- Partial failure nói phần nào đã đổi.
- F5 không mất state.
- Conversation mở lại thấy result.

---

## 12. Idempotency và Duplicate

Idempotency key có thể dựa trên:

```text
UserId + ConversationId + ActionId + ToolVersion
```

Duplicate Task check:

- Normalized title.
- Project.
- Source InboxItem/ExternalId.
- Action ID.
- Time window.

UX:

```text
Có task tương tự:
SPRINT-142 - Review báo cáo tuần 28

[Mở task] [Cập nhật task] [Vẫn tạo mới]
```

Chỉ cảnh báo, không cấm tuyệt đối.

---

## 13. Undo

MVP:

- Status.
- Assignee.
- Deadline.
- Task vừa tạo.
- Sticky vừa tạo.

Yêu cầu:

- API thật.
- Permission hiện tại.
- Thời hạn undo.
- Audit action/undo.
- Không hiện nếu không hỗ trợ.

---

## 14. Classification

Dùng cho:

- Task Candidate.
- Priority/deadline suggestion.
- Intent routing.
- Attachment content/type.
- Sensitive content.

Rules:

- Phân biệt AI với rule-based.
- Có confidence.
- Confidence thấp không auto mutation.
- User sửa được.
- Không thay permission.
- External content không phải lệnh.

---

## 15. Safety

| Nhóm | Xử lý |
|---|---|
| Công việc/học tập | Trả lời bình thường |
| Chính trị/tôn giáo/xung đột | Trung lập |
| Y tế/pháp lý/tài chính | Thông tin chung, nêu giới hạn |
| Dữ liệu cá nhân/bí mật | Kiểm tra quyền hoặc từ chối |
| Gây hại/phạm pháp | Từ chối, không tool |
| Vượt quyền | Từ chối và audit |

Pipeline:

```text
Input Moderation
-> Intent + Context
-> Permission
-> Generation
-> Tool Risk
-> Preview/Confirm
-> Output Moderation
-> Audit
```

Non-negotiable:

- Không lộ secret/system prompt.
- Không theo prompt injection trong file/email.
- Không tool nếu thiếu quyền.
- Không bịa dữ liệu.
- Không cross-workspace data leak.
- External content không tự confirm action.

---

## 16. Permission

Ba lớp:

1. UI - UX only.
2. AI Orchestrator - không chọn tool thiếu quyền.
3. Backend API - enforce bắt buộc.

Audit lưu:

- User/tool.
- Permission snapshot.
- Input/entity/time.
- Confirmation/result.
- Conversation/message source.

Không tin tool input chỉ vì AI sinh ra.

---

## 17. Memory

### Conversation memory

- Dùng trong một chat.
- Tóm tắt khi dài.
- Không kéo entity cũ vào mutation khi context đổi.

### User preferences

Có thể nhớ language, format, default workspace. Không tự lưu secret hoặc dữ liệu nhạy cảm không cần thiết.

### Project memory

Nguồn thật là SprintA/RAG, không phải trí nhớ LLM.

---

## 18. Streaming, Performance, Observability

- Stream text.
- Tool không fake streaming success.
- Attachment async status.
- Cancel/timeout/retry.
- Rate limit per user/workspace.
- Cache read-only có version và permission.
- Trace conversation -> retrieval -> model -> tool.

Metrics:

- Latency/cost.
- Retrieval hit.
- Citation click.
- Tool success/fail.
- Confirm/cancel.
- Duplicate prevented.
- Permission denied.
- Feedback/hallucination report.
- Provider error/rate limit.

Không log raw secret hoặc dữ liệu nhạy cảm không cần.

---

## 19. Evaluation

Test:

- Context đúng/sai/mơ hồ.
- User thiếu permission.
- Cancel không mutation.
- Retry không duplicate.
- Prompt injection trong PDF/email.
- File ngoài quyền.
- Citation đúng.
- Confidence thấp.
- Bulk high-risk.
- Voice transcript sai.
- Provider timeout.
- F5 action persistence.

---

## 20. Implementation Phases

1. Audit.
2. Chat Foundation.
3. Multimodal Composer.
4. Storage/Processing.
5. Voice.
6. RAG.
7. Read Tools.
8. Mutation Tools.
9. Advanced Project/Cycle/Goal/member/bulk.

Không làm nhiều phase trong một task nếu chưa được chốt.

---

## 21. Acceptance Criteria

- [ ] Context hiện rõ.
- [ ] Thiếu destination không mutation.
- [ ] Cancel không tạo dữ liệu.
- [ ] Retry không duplicate.
- [ ] Permission backend pass.
- [ ] Action còn sau F5.
- [ ] Kết quả có entity link.
- [ ] Ảnh có thumbnail.
- [ ] File có processing status.
- [ ] Voice transcript editable.
- [ ] RAG có citation/permission.
- [ ] Prompt injection không vượt policy.
- [ ] Build/test pass.
- [ ] Không mock/fake success.

# 23. AI Credit and Cost Control

- AI action phải estimate/reserve/finalize/release Credits.
- Provider failure không được debit như success.
- Usage record lưu model, input/output, provider cost và Credits.
- Model routing economy/standard/advanced.
- Người dùng thấy estimated Credits trước file/voice/grounded action.
- Gamification Points không dùng cho AI billing.
- Chi tiết: `SPRINTA_AI_CREDITS_BILLING_SPEC.md`.
