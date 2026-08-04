# COLLAB-MENTION-01 Report

## A. Previous state

- Channel messages, attachments, realtime delivery, and read state were real and persisted, but Channel sends carried no mention identity.
- The existing `Notification` table/API/dropdown was reusable; it lacked Channel/message context.
- No Channel-scoped member-search endpoint or plain-text mention composer existed.

## B. Mention identity/model

- Added `ChannelMessageMention` with message FK, mentioned internal `UserId`, exact plain-text span, backend-derived `DisplayText`, and creation time.
- A unique `(ChannelMessageId, MentionedUserId)` index guarantees one mention row/notification target per user per message.
- Span metadata survives display-name changes while `UserId` remains authoritative.
- Backend verifies each visible token equals the current selected member name; it never guesses identity from `@text`.

## C. API contract

- Existing Channel JSON and multipart routes now accept `mentions[{ userId, startIndex, length }]` alongside content/files.
- Existing attachment-only and non-mention sends remain compatible.
- History/send/realtime message DTOs return safe mention metadata.
- `GET /api/channels/{channelId}/members?query=&limit=` returns only safe active-member fields.

## D. Notification integration

- Reused the existing `Notification` entity, REST list/read state, dropdown, and notifications view.
- Added nullable Channel/message context, related project, actor, short whitespace-normalized preview, and deep link.
- Type is `collaboration_channel_mention`; no full attachment data, storage key, email, or token is stored/emitted.
- REST remains authoritative; private events trigger a deduplicated REST refresh.

## E. SignalR behavior

- Added `CollaborationMentionCreated` through `Clients.User(recipientUserId)` only.
- Payload is limited to notification/message/channel IDs, safe actor metadata, preview, and creation time.
- Runtime proved USER_B receives one event and USER_C receives none.
- Reconnect refreshes notification REST; notification IDs prevent duplicate event handling.

## F. Frontend autocomplete/rendering

- Channel textarea opens debounced/cancelable member search after `@`; stale responses are rejected.
- Arrow Up/Down, Enter/Tab, Escape, mouse, and touch selection are supported.
- Selection stores internal `UserId` plus maintained text spans; duplicate users and more than 20 mentions are prevented.
- Shift+Enter, normal Enter send, attachment selection, and failure retention remain intact.
- History/realtime messages render Vue text nodes and styled spans only; no `v-html`, contenteditable, or rich HTML was added.
- Long names wrap; focus, theme tokens, and 390 px sizing match the existing dashboard density.
- Deep links select project/Channel and center the target message when it is in loaded history.

## G. Authorization/security

- Sender still comes only from JWT and must retain Channel send permission.
- Autocomplete and send validation require active user plus active membership in the exact Channel.
- Outsider, inactive/deleted, cross-Channel, forged ID, invalid span, mismatched display token, and overlapping spans are rejected.
- Self-mentions are ignored by explicit policy; duplicate recipient IDs are deduplicated; maximum is 20 users.
- Notification REST remains user-filtered and Channel history remains membership-gated.

## H. Atomicity/error handling

- Message, attachment metadata, mention rows, and notification rows are staged in one EF save/SQL transaction.
- Private events publish only after persistence succeeds.
- Storage failure creates no database rows; database failure removes newly stored attachment files.
- A forced save failure test proves message, mention, and notification all remain absent.
- Runtime cleanup deletes run-scoped notifications/mentions before roots and verifies no rows/files remain.

## I. Tests/runtime A/B/C

- Backend build: PASS, 0 errors; existing AngleSharp advisory warning only.
- Full backend suite: PASS, 287/287.
- Frontend production build: PASS; existing PWA/Rolldown and chunk-size warnings remain non-fatal.
- Runtime `mention01`: PASS with automatic cleanup.
- USER_A mentioned USER_B twice in one request: one mention, one SQL notification, one private event.
- Fresh USER_B REST session retained the notification and history retained Unicode mention metadata.
- USER_C forged/outside-Channel mention returned 403 and USER_C received no notification/event.
- Self mention produced no mention/notification; inactive USER_B was rejected and restored.
- Mention plus PNG/PDF attachments persisted and reloaded together; attachment/read-state regressions passed.
- Browser smoke at 390x844: light and dark both had content, no overlay/error logs, and no horizontal overflow.
- Authenticated behavior was verified by the token-isolated runtime fixture; the browser fixture intentionally emits no credentials.

## J. Migration

- Added `20260804080930_AddChannelMessageMentions`; no old migration was edited.
- Adds mention table/FKs/indexes plus Channel/message context and unique message-recipient index on notifications.
- `has-pending-model-changes`: none.
- Applied only to confirmed local SQL Server `KHOI\SQLEXPRESS / TaskManagementDB`; production was not touched.
- SQL integration fixtures moved to fresh versioned local databases without dropping prior databases.

## K. Files/commits

- Backend/domain/application/infrastructure/API/tests/runtime fixture: `62315460 feat(collaboration): persist authorized channel mentions`.
- Frontend composer/rendering/notifications and this report: `feat(frontend): connect channel mentions and notifications`.
- Package manifests, stashed `appsettings.json`, and stashed `useI18n.js` were unchanged.
- No push was performed.

## L. Deferred

- Direct Message mentions, typing/presence, reactions, voice/video, OAuth, AI/payment, and rich HTML remain out of scope.
- Loading a target message outside the currently fetched history page remains pagination-driven rather than a new message-by-ID endpoint.

## M. Decision

**PASS**

Identity uses internal `UserId`; SQL mention/notification persistence, private realtime delivery, member authorization, duplicate/self policy, attachments, reconnect/account reset, runtime A/B/C, builds, tests, migration, and cleanup all pass.
