# COLLAB-READ-STATE-01 Report

## A. Previous state

- Channel and DM messages were persisted, but neither resource had a per-user read cursor.
- `DirectMessage.IsRead` was a legacy field with no working list/API/realtime flow.
- Channel/DM list DTOs exposed no unread metadata; the collaboration UI showed no real badges.
- `collaboration_servers` local storage is unrelated legacy server/voice configuration and was not used as unread state.

## B. Data model

- Added `CollaborationChannelReadState` keyed by `(ChannelId, UserId)`.
- Added `DirectConversationReadState` keyed by `(ConversationId, UserId)`.
- Both store nullable `LastReadMessageId` plus `LastReadAt`; unread totals are calculated, never stored.
- Resource/user/message foreign keys and lookup indexes are configured in `ApplicationDbContext`.
- Cursor updates use a serializable transaction and a SQL Server transaction-owned application lock per resource/user.
- Cursor comparison is monotonic by `SentAt`, then provider-ordered message ID for stable ties.

## C. API contract

- `POST /api/channels/{channelId}/read` with `{ "messageId": "..." }`.
- `POST /api/direct-conversations/{conversationId}/read` with the same body.
- The current user comes only from the authenticated JWT; no request accepts `userId`.
- Channel and DM list items now return `unreadCount` and `lastReadMessageId`.
- Mark responses return `resourceType`, `resourceId`, `lastReadMessageId`, `lastReadAt`, and `unreadCount`.

## D. Unread calculation

- Counts messages after the persisted cursor and excludes messages sent by the current user.
- Channel discovery and DM list metadata are calculated with batched state/message queries, not per-item queries.
- Repeating a cursor is idempotent; an older cursor cannot replace a newer cursor.
- Current entities have no deleted/hidden message policy, so all persisted resource messages remain visible/countable.

## E. SignalR behavior

- Added `CollaborationReadStateChanged`.
- Updates are sent through `Clients.User(...)`, with no `userId` in the client payload.
- A new message publishes the recipient's authoritative unread total; sender totals are not incremented.
- Mark-read publishes the caller's authoritative zero/current total privately.
- REST remains the source of truth; reconnect refreshes history and the relevant list.

## F. Frontend integration

- Channel and DM rows render real accessible unread badges from REST/realtime metadata, capped visually at `99+`.
- A successful active history load schedules mark-read only after render and only for its last message.
- Active realtime messages are deduplicated, appended, rendered, and then marked with a 180 ms debounce.
- Inactive resources update from the private backend event; no fake increment is performed.
- Pending marks are invalidated on selection changes, logout/account reset, abort, and unmount.
- Reconnect reloads the active history plus its authoritative list without replacing the active selection.
- No local storage is used as an unread source of truth.

## G. Security

- Existing Channel read permission and DM participant checks gate history and mark-read.
- A cursor message must belong to the requested resource; outsiders/cross-resource IDs return the existing 404 convention.
- Private unread/read events are isolated to the target authenticated user connection.
- Runtime USER_C could neither read nor mark USER_A/USER_B resources and received no private read-state event.

## H. Tests

- `dotnet build Backend/TaskManagement.Tests/TaskManagement.Tests.csproj --no-restore`: PASS, 0 errors.
- `dotnet test Backend/TaskManagement.Tests/TaskManagement.Tests.csproj --no-build`: PASS, 278/278.
- Added service tests for Channel/DM counts, self exclusion, persistence, denial, wrong resource, idempotency, and monotonicity.
- Added SQL Server concurrency coverage proving concurrent old/new marks retain the newest cursor.
- `npm run build`: PASS; existing PWA/Rolldown and large-chunk warnings remain non-fatal.
- `git diff --check`: PASS; only line-ending normalization notices were emitted.

## I. Runtime fixture evidence

- Applied only migration `20260804062616_AddCollaborationReadState` to local `KHOI\\SQLEXPRESS / TaskManagementDB / Testing`; production was not touched.
- Channel REST: PASS for unread, self exclusion, monotonic cursor, persistence, C denial, pagination, and Unicode.
- DM REST: PASS for the equivalent A/B/C matrix and no duplicate conversation.
- SignalR: PASS for private unread/read updates, C isolation, reconnect, and account switch.
- Automatic run-scoped cleanup: PASS, including both new read-state tables.
- Browser smoke at 390x844: page loaded with meaningful content, no Vite overlay/page errors, and responsive layout remained intact.
- Authenticated badge behavior was validated by compiled UI contracts plus runtime REST/SignalR because the fixture intentionally never emits credentials/tokens.

## J. Migration

- New additive migration: `20260804062616_AddCollaborationReadState`.
- Creates only the two read-state tables, composite primary keys, foreign keys, and indexes.
- No old migration was edited and no database was dropped.

## K. Files/commits

- Backend/domain/application/infrastructure/API/tests/runtime fixture: `9cf85c0a feat(collaboration): persist per-user read state`.
- Frontend/API/realtime/UI and this report: `feat(frontend): connect collaboration unread state`.
- Package manifests, stashed `appsettings.json`, and stashed `useI18n.js` were not changed.
- No push was performed.

## L. Deferred features

- Per-message “seen by” receipts, attachments, mentions, typing/presence, voice/video, OAuth, AI, and payment remain out of scope.
- Deleted/hidden-message filtering remains deferred until Channel/DM message visibility semantics exist.

## M. Decision

**PASS**

SQL cursors persist per user; Channel/DM counts, self exclusion, authorization, monotonic/idempotent marks, reload, realtime isolation/reconnect, runtime A/B/C, full tests, and builds all pass.
