# FE-COLLAB-DM-01 Report

## A. Contract mapping

| Feature | Contract |
| --- | --- |
| User discovery | `GET /api/users?projectId={internalProjectId}&page=1&pageSize=100`; the required `projectId` scope prevents use of the unscoped global directory. The response is `{ statusCode, data: User[], total, page, pageSize }`. `User.id` is the only recipient identity used. The current JWT user from `GET /api/users/me` is excluded. |
| Find/create | `POST /api/direct-conversations` with `{ "participantUserId": "<internal UserId>" }`. The response is `ApiResponse<DirectConversationDto>` and the frontend uses only its backend-issued `conversationId`. |
| Conversation list | `GET /api/direct-conversations?page={page}&pageSize={pageSize}`. Ordering is backend-owned: `lastMessageAt_desc,createdAt_desc,conversationId_desc`. |
| History | `GET /api/direct-conversations/{conversationId}/messages?page={page}&pageSize={pageSize}`. Backend pages are newest-first by `createdAt_desc,messageId_desc`; each page is reversed for chronological chat display, then prepended and deduplicated by `messageId`. |
| Send | `POST /api/direct-conversations/{conversationId}/messages` with `{ "content": "..." }`. Sender identity comes from JWT. The frontend does not send sender/user/name/timestamp fields and appends only the returned `DirectMessageDto`. |

Direct conversation response:

- `conversationId`
- `otherParticipant: { userId, displayName, avatarUrl }`
- `lastMessagePreview`
- `lastMessageAt`
- `createdAt`

Direct message response:

- `messageId`
- `conversationId`
- `content`
- `sender: { userId, displayName, avatarUrl }`
- `createdAt`

Direct API responses use the existing `ApiResponse.data` envelope. Page DTOs contain `items`, `page`, `pageSize`, `totalCount`, and `ordering`. Valid page size is 1–100. Content is normalized and trimmed by the backend and is limited to 4,000 characters.

The controller explicitly emits 400 for invalid participant/content/page, 401 when the JWT subject is unavailable, and non-disclosing 404 for a missing, revoked, foreign, or out-of-scope participant/conversation. The frontend also handles 403 from authorization middleware and defensively refreshes on 409, although this controller does not currently declare a 409 response. Network/5xx GET failures expose retry controls; POST is not automatically retried.

## B. Previous local behavior

| Feature | Component/store | Previous source | Local key | Hard-coded/mock behavior | Replacement |
| --- | --- | --- | --- | --- | --- |
| User selector | `CollaborationChat.vue` | Unscoped `GET /api/users` | None | Fake `online` status and unread count; accepted mock friends could enter the DM list | Project-scoped real users keyed by internal `User.id` |
| DM conversation | `CollaborationChat.vue` | Recipient ID used as local conversation identity | `collaboration_dm_messages` | Hard-coded `user-phat` conversation and fallback `user-quan` identity | Backend find/create and backend `conversationId` |
| Conversation list | `CollaborationChat.vue` | Member list | None | No persistent conversation metadata | Paginated backend conversation list |
| Message history | `CollaborationChat.vue` | Browser object keyed by recipient | `collaboration_dm_messages`; legacy migration read `collaboration_messages` | Local-only history and legacy browser migration | Paginated SQL-backed history |
| Send | `CollaborationChat.vue` | Local append | `collaboration_dm_messages` | Frontend-created sender/timestamp and delayed simulated reply | POST content only; append server response only |
| Realtime/read | `CollaborationChat.vue` | Timers/local fields | Local DM object | Fake typing, online, unread, partner reply, and timed incoming DM call | Removed from the real DM flow; no realtime/read claims |

`collaboration_servers`, Channel Chat state, theme, auth/session, locale, sidebar preferences, and unrelated local settings were not removed or migrated.

## C. Changes

- Extended `Frontend/src/api/collaborationApi.js` with centralized scoped user discovery and find/create/list/history/send DM methods.
- Added a Project-scoped recipient selector that uses stable internal `UserId`, excludes the current user, and never derives identity from email, display name, or array position.
- Added backend-owned conversation list metadata, pagination, loading, empty, error, retry, and stable `conversationId` keys.
- Added guarded find/create behavior with disabled controls and one in-flight request, preventing double-click local duplication.
- Added paginated history with request cancellation, request sequence guards, conversation-scope validation, `messageId` deduplication, deterministic server order preservation, older-page prepend, and scroll-position preservation.
- Added send trimming, whitespace rejection, 4,000-character enforcement, one in-flight POST, no optimistic/fake message, composer retention on error, and append-only-from-response behavior.
- Removed DM reads/writes for `collaboration_dm_messages` and removed legacy DM migration from `collaboration_messages`. No local demo data is uploaded.
- Clear and abort DM state on recipient/conversation/Project changes, tab context changes, 401, access revocation, unmount, and a new component/session mount.
- DM content remains Vue text interpolation, not `v-html`; CSS `white-space: pre-wrap` and `overflow-wrap: anywhere` preserve line breaks and safely wrap long text.
- Channel Chat API logic was retained.

## D. Verification

| Check | Result |
| --- | --- |
| Required branch | `agent/kimi-my-work` |
| Backend checkpoint | `9ab72da0` is the cherry-picked equivalent of `53495f8b`; both have stable patch-id `09c78317d1527e5d47c56d0b66706f389b3fdd34` |
| Initial worktree | Clean |
| `npm ci` before edits | PASS; package files unchanged |
| Baseline `npm run build` | PASS |
| Post-change `npm run build` | PASS; existing PWA/Rolldown and large-chunk warnings remain |
| LocalStorage/fake-state scan | PASS: no DM key, legacy DM migration, fake reply, fake typing/online/unread, or hard-coded DM identity remains |
| Plain-text/XSS static check | PASS: interpolation only; no `v-html`; line breaks and long content use safe wrapping |
| Pagination/static race review | PASS: server paging, stable IDs, dedupe, abort controllers, request IDs, scope checks, prepend scroll preservation |
| Responsive/static CSS review | Desktop and the existing 720px mobile breakpoint retain bounded sidebar, wrapping message cards, scrollable history, and sticky composer; authenticated 390px visual evidence is unavailable |
| Light/dark static review | Existing CSS variables are retained; no theme-specific hard-coded DM surface was introduced |
| Keyboard/accessibility | Enter sends, Shift+Enter remains a newline, controls disable during requests, and recipient/retry/send controls have accessible labels |
| `git diff --check` | PASS |

Browser smoke was not run because the backend report states the DM migration is pending and no authenticated USER_A/USER_B/USER_C collaboration fixture was supplied. No runtime PASS is claimed.

`BROWSER_EVIDENCE_NOT_AVAILABLE`

`RUNTIME_MIGRATION_NOT_APPLIED`

`COLLAB_DM_FIXTURE_NOT_AVAILABLE`

`REAL_BACKEND_PERSISTENCE`

`REALTIME_NOT_AVAILABLE`

`READ_STATE_NOT_AVAILABLE`

## E. Deferred

- SignalR realtime
- Read/unread state
- Attachments
- Mentions
- Voice/video
- Runtime migration apply
- Authenticated end-to-end/browser verification
- FE-QA-BASELINE-01

## F. Decision

`PASS`

Within code/build scope, DM now uses real internal user IDs and backend conversation IDs for find/create/list/history/send; localStorage and hard-coded DM identity are no longer sources of truth; sender identity is not sent; pagination/order, cancellation, plain-text rendering, loading/empty/error/retry, and context cleanup are implemented; fake realtime/read behavior is absent; the frontend build passes; and no backend, migration, dependency, or package file was changed.
