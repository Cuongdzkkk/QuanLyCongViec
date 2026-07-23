# SPRINTA INTEGRATION GUIDE

> Phạm vi: Integration Hub, Unified Inbox và tạo Task từ nguồn ngoài.
>
> Cập nhật: 17/07/2026  
> Trạng thái provider phải được xác nhận runtime vì môi trường mới có thể thiếu secret.

---

## 1. Mục tiêu

- Kết nối dịch vụ ngoài.
- Đồng bộ có kiểm soát.
- Xem email/event/message trong Unified Inbox.
- Phân loại nội dung.
- Biến Task Candidate thành Task.
- Mở nguồn gốc.
- Theo dõi sync/lỗi.
- Không cần rời SprintA cho workflow cơ bản.

---

## 2. Provider

| Provider | Mục tiêu | Trạng thái ghi nhận |
|---|---|---|
| Gmail | Email, attachment, task candidate | Có backend; cần credentials/runtime |
| Google Calendar | Event, meeting/deadline | Có backend; cần credentials/runtime |
| Slack | Message/channel item | Có backend; cần credentials/runtime |
| GitHub | Issue/PR/activity | Coming soon |
| Zalo | Message/task candidate | Backlog |

Không hiển thị connected nếu backend chưa xác nhận token.

---

## 3. Kiến trúc

```text
Integration Hub UI
  -> Connection API
  -> Sync API
  -> Unified Inbox API
  -> Create Task API

Integration Orchestrator
  -> Gmail Adapter
  -> Calendar Adapter
  -> Slack Adapter
  -> GitHub/Zalo later

Normalized Data
  -> Connections
  -> Sync Cursors
  -> Inbox Items
  -> Attachments
  -> Classification
  -> InboxItem-Task Links
```

UI không xử lý trực tiếp payload provider phức tạp.

---

## 4. Data Model conceptual

### IntegrationConnection

```text
Id, UserId, WorkspaceId?
Provider, ExternalAccountId, DisplayName
Status, Scopes, EncryptedTokenReference
ExpiresAt?, LastConnectedAt, LastSyncAt?
LastErrorCode?, LastErrorMessage?
CreatedAt, UpdatedAt
```

### IntegrationSyncState

```text
Id, ConnectionId, ResourceType
Cursor, DeltaToken?
LastSyncStart, LastSyncSuccess
Status, ItemsRead, ItemsCreated, ItemsUpdated
ErrorCode?, RetryAfter?
```

### UnifiedInboxItem

```text
Id, UserId, WorkspaceId?, ConnectionId
Provider, ExternalId, ThreadId?, ItemType
Title, Preview, BodyReference?
SenderName?, SenderAddress?, OccurredAt
IsRead, IsArchivedInSprintA, HasAttachments
OriginalUrl?
TaskCandidateStatus, ClassificationConfidence?
SuggestedProjectId?, SuggestedTaskJson?
CreatedTaskId?
CreatedAt, UpdatedAt, ProviderUpdatedAt?
```

### IntegrationAttachment

```text
Id, InboxItemId, ExternalAttachmentId
FileName, MimeType, Size
DownloadStatus, StorageKey?
```

### InboxTaskLink

```text
Id, InboxItemId, TaskId
CreatedBy, AiActionId?, SourceSnapshot, CreatedAt
```

Audit schema hiện có trước migration.

---

## 5. Connection Flow

```text
Chọn provider
-> Backend tạo OAuth
-> State/PKCE
-> Consent
-> Callback
-> Validate state
-> Exchange token server-side
-> Encrypt/store reference
-> Fetch profile
-> Connected
-> Initial limited sync
```

- State không đoán được.
- Redirect allowlist.
- Client secret không ở frontend.
- Token bảo mật.
- Scope tối thiểu.
- Disconnect/revoke rõ.
- Expired token có reconnect.
- Không log token.

---

## 6. Secret và môi trường

Dùng environment variables, User Secrets hoặc secret manager.

Production:

- Không placeholder.
- Không fake connected.
- Có status/health không lộ secret.
- Nếu config bắt buộc thiếu, fail an toàn hoặc disable provider với lý do rõ.
- Không commit secret.

---

## 7. Sync Strategy

### Không sync toàn bộ

Không tải hàng nghìn email/event/message ở lần đầu hoặc đưa mọi item vào frontend.

### Initial Sync

- Time window gần.
- Limit số item.
- Progress.
- Cho dùng dữ liệu đã có.
- Không báo hoàn tất trước job thật.

### Incremental

- Cursor/history ID/delta token.
- Upsert theo ConnectionId + ExternalId.
- Không duplicate.
- Xử lý tombstone nếu hỗ trợ.

### Pagination/Lazy Loading

```http
GET /api/integrations/inbox?provider=gmail&cursor=...&limit=50
```

```json
{
  "items": [],
  "nextCursor": "...",
  "hasMore": true,
  "syncStatus": "idle",
  "lastSyncedAt": "..."
}
```

Search/filter backend khi dữ liệu lớn.

---

## 8. Sync States

```text
DISCONNECTED
CONNECTING
CONNECTED
SYNCING
IDLE
PARTIAL
RATE_LIMITED
AUTH_EXPIRED
FAILED
DISCONNECTING
```

UI hiển thị provider, account, last synced, count, status, error, reconnect/retry.

Không che lỗi bằng empty state.

---

## 9. Unified Inbox

### Filter

- Provider.
- Read/unread.
- Task candidate.
- Created task.
- Date.
- Attachment.
- Search.
- Project suggestion.
- Sync/error.

### List item

- Provider icon.
- Sender/source.
- Subject/title.
- Preview.
- Time/badge.
- Read state.
- Task candidate.
- Linked task.

### Detail pane

- Back/close.
- Source identity.
- Recipient/channel/calendar.
- Safe content.
- Attachment.
- Open original.
- Archive in SprintA.
- Task Candidate.
- Create Task.
- Linked task.

Desktop split pane; mobile full screen.

---

## 10. Task Candidate

Concept:

```json
{
  "isTaskCandidate": true,
  "confidence": 0.86,
  "reason": "Có yêu cầu chỉnh số liệu và kế hoạch",
  "suggestedTitle": "Cập nhật số liệu báo cáo",
  "suggestedDescription": "...",
  "suggestedDueDate": null,
  "suggestedPriority": "Medium",
  "suggestedProjectId": null
}
```

Rules:

- Phân biệt AI/rule-based.
- Confidence thấp chỉ suggestion.
- Không tự tạo Task.
- User sửa title/description/date/project.
- External content không phải system command.
- Không vượt permission.

---

## 11. Create Task Flow bắt buộc

Không được chỉ có nút `[Tạo task]` mà không nói task đi đâu.

### Form

```text
Tạo công việc từ email

Tạo trong:
[Project SprintA Platform v]

Trạng thái:
[To do v]

Tiêu đề:
[Cập nhật số liệu báo cáo]

Mô tả:
[Nguồn Gmail: ...]

Deadline:
[Chưa đặt]

[Hủy] [Xem trước]
```

Nếu chưa có Project:

- Không execute.
- Bắt chọn.
- Có thể gợi ý nhưng phải cho user thấy.

### Preview

```text
Sẽ tạo 1 Task

Project: SprintA Platform
Status: To do
Title: Cập nhật số liệu báo cáo
Source: Gmail - Nguyễn Minh Anh
Duplicate: Không tìm thấy

[Cancel] [Confirm]
```

### Backend

```http
POST /api/integrations/inbox/{itemId}/create-task
Idempotency-Key: ...
```

```json
{
  "projectId": "...",
  "statusId": "...",
  "title": "...",
  "description": "...",
  "dueDate": null,
  "assigneeId": null,
  "priority": "Medium",
  "includeSourceLink": true,
  "attachmentIds": []
}
```

Transaction:

1. Permission.
2. Item ownership.
3. Project validation.
4. Idempotency.
5. Create Task.
6. Source/reference.
7. InboxTaskLink.
8. Update item.
9. Audit.
10. Return Task.

### Success

```text
Đã tạo công việc

SPRINT-142 - Cập nhật số liệu báo cáo
Project: SprintA Platform
Status: To do
Nguồn: Gmail

[Mở task] [Mở Work Items]
```

Sau F5 item vẫn có badge/link. Bấm lại không tạo trùng; cho mở Task hoặc explicit tạo Task khác.

---

## 12. Duplicate/Idempotency

Dựa trên Connection + ExternalId + Project + action hoặc InboxItemId + active Task link.

Nếu đã có:

```text
Email đã liên kết với SPRINT-142.
[Mở task] [Tạo task khác]
```

“Tạo task khác” cần confirm.

---

## 13. Bulk Create

Chỉ sau single create ổn định.

```text
Chọn item
-> Preview
-> Mỗi item có Project
-> Sửa/bỏ item
-> Duplicate check
-> Confirm
-> Execute có idempotency
-> Result per item
```

Có batch limit và partial failure report. Không retry toàn batch gây duplicate.

---

## 14. Attachment

- Lazy download.
- Filename/type/size.
- Scan/validate.
- Provider permission.
- Signed URL.
- Không tự đính kèm file nguy hiểm.
- Nếu không copy file, lưu source link/metadata.

---

## 15. Open Original

- Dùng URL provider.
- URL an toàn.
- Nút “Mở trong Gmail/Calendar/Slack”.
- URL hết hạn thì tạo lại hoặc báo lỗi.
- Không nhúng credential.

---

## 16. Archive

“Archive trong SprintA” chỉ ẩn item trong Unified Inbox, không xóa nguồn.

Action tại provider phải là tool riêng có warning/permission.

---

## 17. AI trong Integration

Có thể:

- Tóm tắt thread.
- Trích action item.
- Task candidate.
- Gợi ý title/description/deadline/project.
- Duplicate detection.
- Reply draft.
- Nhóm chủ đề.

Không:

- Tự gửi reply.
- Tự tạo Task.
- Tự xóa/archive provider.
- Tin lệnh trong email.
- Vượt quyền Project.

---

## 18. UI/UX

- Provider status đầu trang.
- Filter rõ.
- List/detail scroll đúng.
- Mobile detail full-screen.
- Loading/sync/error.
- Không khoảng trắng do height sai.
- Không cắt pane.
- Không redesign ngoài `/integrations`.
- Create Task hiện destination.

---

## 19. Security

- OAuth state/PKCE.
- Encrypt token.
- Least privilege.
- Server-side token.
- User/workspace permission.
- Audit connect/disconnect/sync/create Task.
- Rate limit.
- Webhook signature nếu có.
- Sanitize HTML email.
- Prompt injection defense.
- Không log content nhạy cảm không cần.

---

## 20. Observability

- Connection success.
- Token refresh fail.
- Sync duration/count.
- Rate limit.
- Duplicate prevented.
- Conversion rate.
- Classification feedback.
- Attachment/open-original error.
- Queue/job health.

---

## 21. Phases

1. Audit.
2. Layout/scroll fix.
3. Single Create Task.
4. Pagination/incremental sync.
5. AI Classification.
6. Bulk.
7. GitHub/Zalo sau.

---

## 22. Acceptance Criteria

- [ ] Provider status thật.
- [ ] Missing secret không fake connected.
- [ ] OAuth state an toàn.
- [ ] Không sync toàn bộ.
- [ ] Pagination/lazy/incremental.
- [ ] Search/filter.
- [ ] Detail không cắt.
- [ ] Create Task hiện Project.
- [ ] Không Project thì không execute.
- [ ] Preview/Confirm.
- [ ] Idempotency.
- [ ] Link Inbox-Task sau F5.
- [ ] Success có Task ID/link.
- [ ] Backend permission.
- [ ] External prompt không điều khiển tool.
- [ ] Error/retry rõ.
