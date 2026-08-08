# COLLAB-TRUTH-01 Report

## A. Executive truth table

| Feature | UI visible | Frontend source | Backend support | Persistence | Realtime | Permission | Runtime evidence | Truth status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Channel discovery | Yes | `CollaborationChat.vue` defaults; attempts `/departments` | Department API/entity, not a Chat Channel API | Server/channel edits are browser-local | None | No chat-specific check | None | `CONTRACT_MISMATCH` |
| Channel text | Yes | `mockMessages`, `collaboration_messages` | `ChannelMessage` table only; no API/service | Same-browser `localStorage` only | None | None | Static evidence only | `FRONTEND_LOCAL_ONLY` |
| Direct message | Yes | Same local message map, keyed by selected user ID | `DirectMessage` table only; no API/service/conversation | Same-browser `localStorage` only | Simulated reply timer | None | Static evidence only | `MOCK_OR_SIMULATED` |
| User/DM directory | Yes | Calls `GET /api/users?pageSize=100` | Real user API | Fresh API read | None | JWT only; not workspace-scoped | No runtime | `REAL_BACKEND_NOT_CONNECTED` |
| Read/unread | Yes | Mutable `item.unread`; reset on select | `DirectMessage.IsRead` only | Local state; server list may be persisted with server data | None | None | None | `FRONTEND_LOCAL_ONLY` |
| Attachment | Yes | `URL.createObjectURL`; metadata copied into local message | Generic upload APIs exist, but no chat relation | Blob URL becomes invalid after browser lifecycle/reload | None | No message authorization | None | `MOCK_OR_SIMULATED` |
| Mention | Input can contain `@text` | Plain Vue interpolation only | `CommentMention` is unrelated to chat | None | None | None | None | `MOCK_OR_SIMULATED` |
| Voice channel | Yes | Local member array and mute/camera toggles | None | Local only | None | None | None | `MOCK_OR_SIMULATED` |
| DM voice/video | Yes | Dialogs, timers, booleans | None | None | No signaling | None | None | `MOCK_OR_SIMULATED` |

The Collaboration page displaying a message is not end-to-end evidence. Current channel and DM message truth is browser-local.

## B. Channel text

### Files and model

- Frontend: `Frontend/src/views/CollaborationChat.vue`.
- Backend entity: `Backend/src/TaskManagement.Domain/Entities/ChannelMessage.cs`.
- EF registration/configuration: `ApplicationDbContext.ChannelMessages`; sender FK to `User`; channel FK to `Department`.
- Migration: `20260709073448_AddChatTables` creates `ChannelMessages`.
- Snapshot: includes `ChannelMessage` and matches the current entity fields.
- There is no dedicated `Channel` entity. `ChannelMessage.ChannelId` points to `Department`.
- `DepartmentMember` exists, but no Chat code enforces it.

### Endpoints and current flow

- Send message endpoint: missing.
- History/list endpoint: missing.
- Create/open Chat Channel endpoint: missing. The existing Department API is not a Chat Channel contract.
- Pagination/cursor: missing.
- Edit/soft delete: missing from model and API.
- Ordering contract: missing; only separate `ChannelId` and `SenderId` indexes exist.
- Author metadata DTO: missing.
- Workspace/project scope: missing from `ChannelMessage`; `Department` has no workspace reference.
- JWT/current sender derivation: missing because there is no message endpoint.
- Read/send permission: missing.
- Frontend `sendMessage()` appends sender fields and content supplied by the browser to `mockMessages`, then writes `collaboration_messages`.
- Reload in the same browser profile can restore JSON from `localStorage`; it does not prove database persistence.
- Another browser/user receives nothing.
- The page attempts to map `/departments` into `channels.value`, but `channels` is a read-only computed value derived from `activeServer`; the assignment cannot establish a real channel source. Fallback channels remain local/demo data.

### Reproduction matrix

| Scenario | Current result | Truth |
| --- | --- | --- |
| Create channel | Adds to local server object and `collaboration_servers` | `FRONTEND_LOCAL_ONLY` |
| Open channel | Selects local object | `FRONTEND_LOCAL_ONLY` |
| Send message | Appends to local array and `localStorage` | `FRONTEND_LOCAL_ONLY` |
| Reload | Same browser may reload local JSON | `FRONTEND_LOCAL_ONLY` |
| Open as USER_B | No cross-user delivery/history | `MOCK_OR_SIMULATED` |
| History pagination | No endpoint/cursor | `REAL_BACKEND_NOT_CONNECTED` |
| Unauthorized user | No server authorization boundary to test | `UNKNOWN_NEEDS_RUNTIME` |
| Deleted channel | Local delete only; default server cannot be deleted | `MOCK_OR_SIMULATED` |
| Deleted message | No delete behavior | `REAL_BACKEND_NOT_CONNECTED` |
| Concurrent message | No concurrency/order/deduplication contract | `UNKNOWN_NEEDS_RUNTIME` |

Missing pieces: dedicated channel scope, membership policy, API/DTO/service, sender derivation, validation, pagination/order, edit/delete semantics, tests, and later realtime integration.

## C. Direct Message

- Entity: `DirectMessage` with `SenderId`, `ReceiverId`, `Content`, `SentAt`, `IsRead`, and nullable `AttachmentUrl`.
- Migration and ModelSnapshot contain the entity.
- Conversation/thread entity: missing.
- Participant model: missing; sender/receiver columns are the only relationship.
- Create/find conversation endpoint: missing.
- Send endpoint: missing.
- History endpoint and pagination: missing.
- Participant-only authorization: missing.
- Workspace/tenant boundary: missing from the entity.
- Frontend obtains a real global active-user list from `/users`, but DM content never calls a backend Chat API.
- The user directory endpoint is JWT-protected but is not automatically workspace-scoped when no filter is supplied.
- Frontend stores DM messages under the selected user ID in `collaboration_messages`.
- The browser supplies sender identity/name/avatar for local rendering.
- A two-second timer creates a random partner reply. This is not another user.
- Reload is same-browser localStorage behavior; cross-user behavior does not exist.

Truth: backend schema is `EXISTS_NOT_USED`; current DM experience is `MOCK_OR_SIMULATED`.

## D. Realtime

- ChatHub: missing.
- Program mappings: `/kanban-hub` and `/notification-hub` only.
- CollaborationChat HubConnection lifecycle: missing.
- Chat join/leave, send/receive events, reconnect, token refresh, ordering, and duplicate-event handling: missing.
- Polling/local event emitter fallback: none. A `setTimeout` creates fake DM replies and an incoming-call dialog.
- The existing frontend SignalR service is for Kanban project events, not Chat.
- Existing hubs do not have `[Authorize]` and accept caller-provided project/user group identifiers.
- JWT bearer configuration has no SignalR query-token extraction. Browser WebSocket access tokens therefore are not established as hub authentication.
- No relationship exists between a realtime event and a persisted chat transaction.

Chat realtime classification: `NO_SIGNALR`.

## E. Read/Attachment/Mention

### Read/unread

- `DirectMessage.IsRead` exists, but there is no read endpoint, timestamp, receipt entity, idempotency contract, or backend unread query.
- Channel read state has no database representation.
- Frontend resets `item.unread = 0` on selection.
- USER_A and USER_B cannot have independent persisted read positions.
- New message realtime count behavior does not exist.
- Truth: `FRONTEND_LOCAL_ONLY`.

### Attachment

- Chat attachment entity/relation: missing. Chat message rows only have an unvalidated `AttachmentUrl` string.
- Frontend selects a local file, creates a `blob:` URL, and stores display metadata in localStorage. It does not upload.
- Generic authenticated `/api/uploads/image`, `/api/uploads/file`, and owner-protected download endpoints exist with size, extension, MIME, signature, and path checks.
- Those generic uploads are not connected to messages and are owner-only, so recipients could not use them as a Chat attachment contract.
- Comment attachments have a separate protected relation and are unrelated to Chat.
- No Chat download authorization, message membership check, virus scanning policy, or content moderation policy exists.
- Truth: `MOCK_OR_SIMULATED`.

### Mention

- No message mention parser, entity, notification, user-ID resolution, or permission check exists.
- `CommentMention` is for comments and is not used by CollaborationChat.
- Message content is rendered with Vue text interpolation, which escapes HTML; there is no mention highlighting or persisted mention behavior.
- Truth: `MOCK_OR_SIMULATED`.

### Data-model status

| Model | Status | Evidence |
| --- | --- | --- |
| Channel | `PARTIAL` | Department is reused as FK target; no dedicated Chat channel/scope |
| ChannelMember | `PARTIAL` | DepartmentMember exists; unused for Chat authorization |
| ChannelMessage | `EXISTS_NOT_USED` | Entity/migration/snapshot only |
| DirectConversation | `MISSING` | No entity/table |
| DirectMessage | `EXISTS_NOT_USED` | Entity/migration/snapshot only |
| ReadReceipt | `MISSING` | A DM boolean is not a per-user receipt/read position |
| MessageAttachment | `MISSING` | Only nullable URL strings |
| Mention | `MISSING` | CommentMention is unrelated |

## F. Voice/video

- `navigator.mediaDevices`, `getUserMedia`, `RTCPeerConnection`, MediaStream handling, signaling, ICE, TURN/STUN, and room membership are absent from CollaborationChat.
- Join/leave voice mutates a local `users` array.
- Mute/camera actions only toggle booleans.
- Remote camera activation is simulated after 800 ms.
- Incoming calls are simulated after 15 seconds.
- Accept/decline/hang-up only show dialogs/messages and change local state.
- There is no permission-denial handling or multi-user media path.

Truth: `MOCK_OR_SIMULATED`. Runtime readiness: not ready.

## G. Mock/localStorage inventory

| File/line or pattern | Local/mock data | Feature | Proposed removal task |
| --- | --- | --- | --- |
| `CollaborationChat.vue:761` `defaultServers` | Hard-coded servers, channels, members, voice rooms, stock avatars | Channel/voice | `FE-COLLAB-TEXT-01`, later WebRTC task |
| `:785`, `:798` | `collaboration_servers` read/write | Server/channel/member state | `FE-COLLAB-TEXT-01` |
| `:1002` | Hard-coded current-user name fallback | Identity rendering | `FE-COLLAB-TEXT-01` |
| `:1008` | Demo channel/DM messages and attachment | Message history | `FE-COLLAB-TEXT-01`, `FE-COLLAB-DM-01` |
| `:1023`, `:1037` | `collaboration_messages` read/write | Channel and DM persistence | `FE-COLLAB-TEXT-01`, `FE-COLLAB-DM-01` |
| `:1057` | Timer simulates remote camera | Video | `COLLAB-WEBRTC-01` |
| `:1103` | Real `/users/me` call | Current user display only | Keep; reuse safely |
| `:1130` | Real `/users` call without workspace scope | DM directory | `CORE-COLLAB-DM-01`, `FE-COLLAB-DM-01` |
| `:1147` | `/departments` assigned to read-only computed channels | Channel discovery mismatch | `FE-COLLAB-TEXT-01` |
| `:1193` | Hard-coded friend-code fallback and localhost invite URL | Friends/invite | Separate collaboration membership task |
| `:1237` | Browser `blob:` attachment preview | Attachment | `CORE/FE-COLLAB-ATTACHMENT-01` |
| `:1303` | Local send and frontend-supplied sender metadata | Channel/DM send | Core then FE text/DM tasks |
| `:1336`, `:1380` | Random delayed partner response | DM | `FE-COLLAB-DM-01` |
| `:1421` onward | Call modal/state simulation | Voice/video | `COLLAB-WEBRTC-01` |
| `:1467` | Friend request simulated success | Membership | Separate collaboration membership task |
| `:1535` | Timed fake incoming call | Voice/video | `COLLAB-WEBRTC-01` |

No unrelated localStorage preference was changed.

## H. Security findings

### P0

- `P0_BLOCKER`: no directly exposed Chat API was found, so no active Chat endpoint exploit is asserted.
- Product release blocker: the visible UI must not be represented as real collaboration; messages and calls are local/simulated.

### P1

- `P1_REQUIRED`: derive sender/current user exclusively from validated JWT; never accept sender identity from the message payload.
- `P1_REQUIRED`: define workspace/channel membership and enforce read/send/history on every query and mutation.
- `P1_REQUIRED`: enforce DM participant-only access and prevent message-ID/conversation enumeration.
- `P1_REQUIRED`: scope user/DM discovery to an authorized workspace instead of listing all active users.
- `P1_REQUIRED`: authorize ChatHub connections and every group join using server-derived membership. Existing Kanban/Notification hub join methods are not a safe Chat template.
- `P1_REQUIRED`: persist message and outbox/realtime publication consistently so an event cannot exist without history.
- `P1_REQUIRED`: connect attachments through a message relation and authorize recipients on download.
- `P1_REQUIRED`: impose message length/empty-content validation and a Chat rate/spam policy.
- `P1_REQUIRED`: reject inactive/deleted users through JWT validation and again where membership state matters.

### P2

- `P2_HARDENING`: add deterministic ordering and a `(scope, SentAt, Id)` pagination/index strategy.
- `P2_HARDENING`: define idempotency/client message IDs and duplicate-event handling.
- `P2_HARDENING`: define edit/delete audit semantics and content retention.
- `P2_HARDENING`: keep message rendering as text or sanitize any future rich HTML.
- `P2_HARDENING`: define attachment malware/content-safety handling and sensitive-content logging policy.

### Permission matrix

| Operation | Required authority | Current implementation |
| --- | --- | --- |
| Channel list/read/send | Active workspace + channel membership | `NOT_IMPLEMENTED` |
| Channel create/delete | Workspace role/explicit permission | Local browser action only |
| DM create/read/send | Authenticated participant pair | `NOT_IMPLEMENTED` |
| Mark read | Current participant, idempotent | `NOT_IMPLEMENTED` |
| Attachment upload/download | Message participant/channel member | `NOT_IMPLEMENTED` for Chat |
| Mention resolution | Visible member in same scope | `NOT_IMPLEMENTED` |
| Hub join | Server-authorized membership | `NOT_IMPLEMENTED` for Chat |

## I. Tests and evidence

- Backend restore: PASS; existing NU1902 warning for AngleSharp 0.17.1.
- Backend build: PASS, 0 errors, 2 NU1902 warnings.
- Backend tests: PASS, 208 passed, 0 failed, 0 skipped.
- Frontend `npm ci`: PASS; 567 packages audited; existing 17 vulnerabilities (3 moderate, 14 high). No audit fix was run.
- Frontend production build: PASS; 4,529 modules transformed.
- Existing frontend warnings: PWA/Rolldown bundle assignment and chunks above 1,600 kB.
- Collaboration-specific automated tests: none for Channel, ChannelMessage, DirectMessage, ChatHub, permissions, read/unread, message attachment, or message mention.
- Generic upload security tests exist, but they do not prove Chat attachment behavior.
- No local backend/frontend listener was available on the expected ports, and no confirmed dev/test database, USER_A/USER_B/outsider, workspace, or channel fixture was available.
- `BROWSER_EVIDENCE_NOT_AVAILABLE`
- `COLLAB_RUNTIME_NOT_AVAILABLE`

## J. Recommended task sequence

| Order | Task | Owner | Scope and short acceptance | Dependency |
| --- | --- | --- | --- | --- |
| 1 | `CORE-COLLAB-TEXT-01` | Backend | Define channel scope/member model; authenticated CRUD/history/send; pagination/order; tests | None |
| 2 | `FE-COLLAB-TEXT-01` | Frontend | Use real channel APIs; remove channel message/server localStorage and demo fallbacks; reload from backend | 1 |
| 3 | `CORE-COLLAB-DM-01` | Backend | Conversation/participant model; participant-only create/history/send/read authorization; tests | Identity/workspace rules from 1 |
| 4 | `FE-COLLAB-DM-01` | Frontend | Scoped directory and real DM APIs; remove simulated replies/local persistence | 3 |
| 5 | `CORE-COLLAB-REALTIME-01` | Backend | Authorized ChatHub; persisted-message events; group checks; reconnect contract; tests | 1 and 3 |
| 6 | `FE-COLLAB-REALTIME-01` | Frontend | Authenticated lifecycle, rejoin, dedupe, order reconciliation | 5, plus 2 and 4 |
| 7 | `CORE-COLLAB-READ-01` | Backend | Per-user read position/receipt, idempotent mark-read, unread queries | 1, 3, 5 |
| 8 | `FE-COLLAB-READ-01` | Frontend | Server unread badges/read actions and realtime updates | 7 |
| 9 | `CORE/FE-COLLAB-ATTACHMENT-01` | Full stack | Private message attachment relation, validation, recipient authorization, upload UI/tests | 1 and 3 |
| 10 | `CORE/FE-COLLAB-MENTION-01` | Full stack | Scoped user-ID mention parsing, persistence, notification and safe rendering | 1, 5 |
| 11 | `COLLAB-WEBRTC-01` | Full stack | Real media permission flow, signaling, ICE/TURN/STUN, room authorization and multi-user tests | Authenticated realtime and membership complete |

Do not combine these into one implementation task. Voice/video remains last.

## K. Decision

`AUDIT_COMPLETE`

Truth is established for channel text, DM, realtime, read/unread, attachments, mentions, voice/video, mocks, storage, security, tests, and task dependencies. Backend tests and frontend build pass. No Collaboration implementation or source behavior was changed.
