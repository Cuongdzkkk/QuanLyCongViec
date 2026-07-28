# CORE-COLLAB-DM-01 Report

## A. Reproduction matrix

| Scenario | Before | After |
| --- | --- | --- |
| User discovery | `GET /api/users` was real but unscoped when no filter was supplied | Reuse the existing active-user API only with its project scope; DM create independently enforces a shared active workspace |
| Find/create conversation | Missing | `POST /api/direct-conversations`; canonical pair lookup and atomic create |
| Conversation list | Missing | `GET /api/direct-conversations`; current participant only |
| Message history | Missing | `GET /api/direct-conversations/{id}/messages`; participant-only backend pagination |
| Send message | Missing | `POST /api/direct-conversations/{id}/messages`; sender comes from JWT |
| Persistence after request/new `DbContext` | Frontend `localStorage` only | SQL rows verified from a fresh `DbContext` |
| Participant permission | Missing | Both active participants in the stored active workspace may read/send; other users receive non-disclosing 404 |
| Frontend local/mock | `collaboration_messages` and delayed simulated replies | Deliberately unchanged; deferred to `FE-COLLAB-DM-01` |
| Current root cause | No conversation aggregate, participant model, API, authorization, or query contract | Addressed in backend; realtime/read state remain out of scope |

Audit was completed before implementation. `artifacts/collaboration/COLLAB_TRUTH_01_REPORT.md`, the existing channel implementation, user/workspace membership model, frontend local storage, and tests were inspected.

## B. Data model

The original `DirectMessage` used only `SenderId` and `ReceiverId`. There was no conversation, participant table, scope, `LastMessageAt`, unique user pair, or conversation-history index. This was a `DATA_MODEL_GAP`.

The new one-to-one model contains:

- `DirectConversation`: `WorkspaceId`, canonical `UserLowId`/`UserHighId`, `CreatedAt`, and `LastMessageAt`.
- A database unique index on `(UserLowId, UserHighId)`. A-B and B-A therefore map to the same row.
- A check constraint preventing self-pairs.
- `DirectConversationParticipant` with composite primary key `(ConversationId, UserId)`.
- `DirectMessage.ConversationId` with a foreign key and `(ConversationId, SentAt, Id)` history index.
- Foreign keys to both users, workspace, participants, conversation, and sender.
- No group-DM model and no DM soft delete.

`ConversationId` is nullable only to preserve compatibility with any legacy `DirectMessages` rows created before this feature. Every message created through the new API always has a conversation. The migration contains no demo data and was listed but not applied.

## C. API contract

| Method | Route | Contract |
| --- | --- | --- |
| POST | `/api/direct-conversations` | Body `{ "participantUserId": "..." }`; returns the existing or newly created one-to-one conversation |
| GET | `/api/direct-conversations?page=1&pageSize=50` | Current user's conversations; `lastMessageAt DESC`, `createdAt DESC`, `conversationId DESC` |
| GET | `/api/direct-conversations/{conversationId}/messages?page=1&pageSize=50` | Newest-first history; `createdAt DESC`, `messageId DESC`; includes correct `totalCount` |
| POST | `/api/direct-conversations/{conversationId}/messages` | Body `{ "content": "..." }`; sender is the JWT subject |

Response DTOs expose only internal user ID, display name, and avatar URL for participants/senders. Request DTOs do not contain sender, recipient, name, avatar, timestamp, or frontend-generated conversation ID.

## D. Changes

- Added conversation and participant domain entities and EF mappings.
- Added DTOs, service interface, SQL-backed service, controller, and dependency registration.
- Added canonical-pair transaction locking on SQL Server using a transaction-owned application lock. The database unique index remains the final duplicate barrier.
- Conversation and its two participant rows are saved in one transaction.
- Concurrent message sends use a conditional SQL update so an older send cannot move `LastMessageAt` backwards.
- Added migration `20260728144925_AddDirectConversationPersistence`.
- Added unit/controller tests and SQL Server relational tests.
- No frontend source or dependency file was changed.

## E. Security

- Identity is the validated `ClaimTypes.NameIdentifier` GUID from JWT.
- Self-DM, missing users, inactive/deleted users, and users without a shared active workspace are rejected.
- The stored workspace is the deterministic first shared active workspace. Both users must remain active members of that workspace for list/read/send.
- Every list/history/send query is participant-scoped and revalidates users and workspace membership.
- Unknown, foreign, revoked, or enumerated conversation IDs return the same non-disclosing 404.
- Message content is trimmed, line endings are normalized, Unicode and line breaks are retained, and length is limited to 4,000 characters.
- HTML/script input is persisted and returned as plain text through JSON; no rich HTML interpretation is introduced.
- Message content is not written to application logs.

## F. Tests

Focused direct-conversation suite: **12 passed, 0 failed**, including a real SQL Server LocalDB test.

Coverage includes canonical reverse create, retry behavior, SQL unique pair, concurrent create, atomic two-participant creation, self/missing/inactive/deleted/out-of-scope rejection, JWT sender, forged sender fields, participant history, outsider enumeration/read/send denial, fresh-context persistence, isolated conversation list, deterministic list/history ordering, pagination without loss/duplication, correct `totalCount`, whitespace/length validation, Vietnamese Unicode, script text handling, safe DTO fields, foreign keys, concurrent send, and monotonic `LastMessageAt`.

Regression results:

- `dotnet restore`: PASS; existing `AngleSharp 0.17.1` NU1902 warning.
- `dotnet build --no-restore`: PASS, 0 errors.
- Full backend tests: **252 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations list`: PASS; the new migration is pending and was not applied.
- `npm ci`: PASS; existing audit result is 17 vulnerabilities (3 moderate, 14 high); no audit fix was run.
- Frontend production build: PASS; existing PWA/Rolldown and large-chunk warnings remain.
- `git diff --check`: PASS.

The SQL integration test used a dedicated LocalDB database, cleaned only its test rows, and did not drop any database.

## G. Frontend handoff

The backend now supplies all DM persistence calls needed to replace local storage:

1. Use an already scoped project user discovery request; do not call the global directory without scope.
2. Find/create using only `participantUserId`.
3. Load conversation list and paginated history from the new endpoints.
4. Send only `{ content }`.
5. Continue rendering message content as text.

Frontend mock messages, `localStorage`, simulated replies, realtime, unread state, attachments, mentions, and calls were intentionally not changed here.

`REAL_BACKEND_PERSISTENCE`

`REALTIME_DEFERRED_TO_CORE_COLLAB_REALTIME_01`

`READ_STATE_DEFERRED_TO_CORE_COLLAB_READ_01`

## H. Decision

`PASS`

One canonical database-enforced conversation exists per user pair; conversation and new messages persist in SQL; sender identity comes from JWT; access is participant and workspace isolated; pagination and ordering are deterministic; SQL Server relational tests and the complete backend suite pass; frontend source remains unchanged.
