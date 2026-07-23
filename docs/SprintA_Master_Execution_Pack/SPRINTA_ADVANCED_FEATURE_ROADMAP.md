# SPRINTA ADVANCED FEATURE ROADMAP

> Mục tiêu: hoàn chỉnh nhưng không biến dự án thành tập hợp feature rời rạc.
>
> Chỉ mở phase mới khi phase trước có release gate.

---

## Nguyên tắc chọn feature

Đánh giá theo:

```text
User Value
× Frequency
× Product Fit
÷ Complexity
÷ Risk
```

Không chọn feature chỉ vì sản phẩm khác có.

---

# RELEASE 1.0 — Reliable Work Management

## Must-have

- Auth/Workspace/Project/Task ổn định.
- List/Kanban/Calendar/Timeline.
- Assignee/priority/deadline/status.
- Subtask/checklist/comment.
- Dependency không cycle.
- Sprint/Cycle.
- Goal.
- Notification.
- Search/filter.
- Permission backend.
- Audit.
- Report 4 KPI.
- Import/export.
- Responsive/light/dark.
- Test/CI.

## UX completion

- Onboarding checklist.
- Empty states có action.
- Recent items.
- Favorites.
- Command palette.
- Keyboard shortcuts.
- Undo toast.
- Consistent loading/error.
- Mobile navigation.

---

# RELEASE 1.1 — Productivity

## Custom Fields

Types:

- Text.
- Number.
- Currency.
- Date.
- Select.
- Multi-select.
- Checkbox.
- User.
- URL.
- Formula để sau.

Phải có project scope, validation, filter, export và permission.

## Recurring Tasks

- Daily/weekly/monthly/custom.
- Timezone.
- Create-next vs create-all.
- Pause/end.
- Idempotent scheduler.
- Recurrence history.

## Task Templates

- Project template.
- Task template.
- Checklist/default fields.
- Permission.
- Versioning.

## Saved Views

- Filter/sort/group/columns.
- Personal/team view.
- Share permission.
- Default view.

## Bulk Actions

- Status.
- Assignee.
- Deadline.
- Priority.
- Sprint.
- Archive.

Bắt buộc Preview, count, permission và partial result.

## Time Tracking

- Manual timer.
- Time entry.
- Billable flag sau.
- Report.
- Permission.
- No overlapping timer per user.

## Workload/Capacity

- Working days.
- Capacity.
- Estimate.
- Overload warning.
- Member leave/availability sau.

---

# RELEASE 1.2 — Automation

## Rule Engine MVP

```text
Trigger
→ Conditions
→ Actions
```

Triggers:

- Task created.
- Status changed.
- Due date reached.
- Assignment changed.
- Form submitted.
- Inbox candidate.

Actions:

- Change status.
- Assign.
- Add comment.
- Create subtask.
- Notify.
- Move sprint.
- Invoke approved AI draft.

Rules:

- Dry run.
- Audit.
- Loop prevention.
- Rate limit.
- Permission snapshot.
- Disable on repeated failure.

## Approval Workflow

- Request approval.
- Approver.
- Approve/reject/comment.
- Status transition.
- History.
- SLA reminder.

---

# RELEASE 1.3 — Knowledge and Intake

## Pages/Docs

- Rich editor.
- Revision.
- Mention/link.
- Search.
- Templates.
- Permission.
- Embed Task/Report.
- Export PDF/Markdown sau.

## Forms

- Field builder giới hạn.
- Submit creates intake/draft.
- Public/private link.
- Spam/rate limit.
- Mapping to Task.
- Confirmation.
- Analytics.

## AI Document Intake

- TXT/DOCX/PDF.
- Task candidates.
- Citations.
- Preview.
- Duplicate detection.
- Project destination.
- Source link.

---

# RELEASE 1.4 — Collaboration

Thứ tự:

1. Activity Feed.
2. Daily Check-in.
3. Mentions/reactions.
4. Channel Chat.
5. Direct Message.
6. File/link.
7. Search/history.

Không làm video call.

SignalR:

- Notification.
- Feed update.
- Chat.
- AI action progress.
- Payment credited notification.

---

# RELEASE 1.5 — Integrations

## Core

- Gmail.
- Google Calendar.
- Slack.
- Incremental sync.
- Pagination.
- Retry/backoff.
- Sync history.
- Task Candidate.
- InboxItem–Task link.
- Open original.
- Provider permission.

## Later

- GitHub issues/PR.
- Microsoft Outlook.
- Webhook/API integration.
- Zalo chỉ khi có business case và hợp đồng/API phù hợp.

---

# RELEASE 1.6 — AI Workspace

- Chat history/search/rename/delete.
- Context chips.
- File/image.
- RAG citation.
- Voice input.
- Tool registry.
- Preview/Confirm/Cancel/Retry.
- Undo safe actions.
- AI budget.
- Model routing.
- Prompt templates.
- Daily brief.
- Project/Sprint summary.
- Workload analysis.
- Meeting summary.
- Evaluation dashboard.

Không auto-run high-risk action.

---

# RELEASE 1.7 — Notes and Personal Productivity

- Global Notes Drawer.
- `/stickies` full manager.
- Floating Stickies.
- Project/Task/route scope.
- Search/pin/color.
- Autosave/API.
- Position persistence.
- Minimize.
- Mobile chips.
- AI “convert note to task” qua Preview.

---

# RELEASE 2.0 — Billing and Team Plans

- AI Credits.
- VNPAY/MoMo/ZaloPay adapters.
- Subscription.
- Workspace Team Wallet.
- Spending limit.
- Invoice.
- Coupon.
- Refund.
- Reconciliation.
- Pricing admin.
- Usage analytics.

---

# RELEASE 2.1 — Enterprise

- SSO OIDC/SAML theo nhu cầu.
- SCIM sau.
- Session/device management.
- IP allowlist sau.
- Audit export.
- Retention.
- Legal hold sau.
- Backup/restore.
- Workspace policy.
- Enterprise AI provider/policy.
- Data residency chỉ khi hạ tầng hỗ trợ.
- SLA/health dashboard.

---

# Advanced Reporting

- Portfolio dashboard.
- Project health.
- Forecast completion.
- Cycle velocity.
- Burndown/burnup.
- Lead/cycle time.
- Cumulative flow.
- Workload/capacity.
- Overdue risk.
- AI usage/cost.
- Integration conversion.
- Billing/revenue.

Mỗi chart có drill-down và data definition.

---

# Global Search

Search:

- Task.
- Project.
- Goal.
- Page.
- Comment.
- User.
- Note.
- Inbox item.

MVP dùng SQL full-text/search strategy phù hợp. Vector search chỉ dùng cho semantic knowledge khi có permission-aware index.

---

# Command Palette

Actions:

- Go to route.
- Open recent Task.
- Create Task.
- Search.
- Open AI/Notes.
- Switch Workspace.
- Change theme.
- Keyboard shortcut reference.

Không cho high-risk action execute ngay; mở Preview/form.

---

# Mobile/PWA

- Installable.
- Offline read cache trước.
- Offline mutation queue chỉ sau khi có conflict/idempotency design.
- Push notification.
- Touch-friendly Kanban.
- Bottom sheet.
- Safe area.
- Low bandwidth mode.

Không tuyên bố Offline Mode hoàn chỉnh trước sync conflict test.

---

# Feature không ưu tiên

- Video call.
- Full spreadsheet clone.
- 3D/particle dashboard.
- Microservices chỉ vì “enterprise”.
- Blockchain/reward token.
- Unconfirmed autonomous AI.
- Confluence/Jira clone đầy đủ.
- Marketplace plugin trước API stability.
