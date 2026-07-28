# CORE-COLLAB-REALTIME-01 Report

## A. Reproduction and previous state

| Feature | Backend class | Frontend service | Authentication | Group naming | Event | Persistence relationship | Previous status / gap |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Project task realtime | `KanbanHub` | `signalrService.js` | Existing behavior unchanged | Raw project ID | Task/project events | Separate existing feature | Present; not modified |
| Notifications | `NotificationHub` | `NotificationsDropdown.vue` | Existing behavior unchanged | `user_{id}` | `ReceiveNotification` | Existing notification flow | Present; not modified |
| Channel chat realtime | None | None for Collaboration Chat | None | None | None | REST/SQL only | Missing |
| Direct Message realtime | None | None for Collaboration Chat | None | None | None | REST/SQL only | Missing |
| JWT Hub query token | JWT Bearer configuration | `accessTokenFactory` exists in an unrelated frontend service | Header JWT only | N/A | N/A | N/A | `access_token` was not accepted for a Hub transport |

`AddSignalR()` and the ASP.NET SignalR server framework were already present. No `ChatHub`, authorized chat groups, chat event constants, or chat publisher existed. Existing Channel and Direct Message REST/SQL services were already the source of truth.

## B. Hub contract

Hub URL:

`/hubs/chat`

`ChatHub` is `[Authorize]` and exposes only group lifecycle methods:

| Method | Argument | Server behavior |
| --- | --- | --- |
| `JoinChannel` | Channel GUID string | Parses the ID, authorizes the JWT user with existing Channel read/membership semantics, then joins `channel:{channelId}` |
| `LeaveChannel` | Channel GUID string | Removes only the current connection from `channel:{channelId}` |
| `JoinDirectConversation` | Conversation GUID string | Parses the ID, authorizes the JWT user with existing participant/workspace semantics, then joins `dm:{conversationId}` |
| `LeaveDirectConversation` | Conversation GUID string | Removes only the current connection from `dm:{conversationId}` |

Group names are produced only by centralized server functions:

- Channel: `channel:{channelId:D}`
- Direct conversation: `dm:{conversationId:D}`

Clients cannot pass arbitrary group names, sender IDs, participant IDs, email, display name, Channel name, or Project name. The Hub has no method that accepts message content or broadcasts an arbitrary client event.

Reconnect creates a new connection. SignalR group membership is not stored in SQL and is not restored automatically; the client must invoke the authorized join methods again.

Sanitized Hub error codes:

- `AUTH_REQUIRED`
- `USER_INACTIVE`
- `CHANNEL_NOT_FOUND_OR_FORBIDDEN`
- `CONVERSATION_NOT_FOUND_OR_FORBIDDEN`
- `INVALID_ID`
- `JOIN_FAILED`

No raw provider exception or stack trace is returned through a Hub method.

## C. Authentication and authorization

- `ChatHub` requires the existing JWT Bearer scheme through `[Authorize]`.
- The JWT subject is the only current-user identity. Hub methods do not accept `userId` or `senderId`.
- `access_token` query input is accepted only when the request path starts with the exact `/hubs/chat` path segment.
- The same issuer, audience, signing key, expiry, zero clock skew, and active/non-deleted user validation remain in effect.
- Missing and expired tokens fail the transport handshake.
- An `access_token` query on a non-Hub API remains unauthorized.
- Channel join composes the existing `IChannelTextService.GetHistoryAsync` authorization path, which already checks active/non-deleted/non-archived Channel, Workspace and Project isolation, resource permission, Channel membership, active user, and membership state.
- Direct join composes the existing `IDirectConversationService.GetHistoryAsync` authorization path, which already checks active participant relation, active users, active Workspace memberships, Workspace isolation, and non-disclosing conversation visibility.
- A small realtime authorization service adds an explicit active-user classification before calling those existing service paths; permission queries are not copied into the Hub.

USER_C cannot join USER_A–USER_B's conversation. An outsider or cross-workspace user cannot join a Channel by knowing its GUID.

Connection logs contain only internal user ID, connection ID, and join/leave category. They do not contain JWTs or message content.

## D. Persist-first event flow

```text
Authenticated REST POST
  -> existing authorization and validation
  -> existing SQL SaveChanges / transaction commit
  -> persisted response DTO is loaded
  -> controller invokes realtime publisher once
  -> publisher sends the event to the deterministic group
```

The existing endpoints remain:

- `POST /api/channels/{channelId}/messages`
- `POST /api/direct-conversations/{conversationId}/messages`

The publisher is invoked only after the persistence service returns successfully. Validation, permission, database, rollback, or pre-commit cancellation failures never reach the publisher. Tests cover database failure and permission failure with zero publish calls, and transport integration verifies event `messageId` equals the stored SQL-model record ID.

SignalR is a best-effort, non-durable delivery path. A realtime transport failure is logged without message content and does not undo or misreport successful persistence. History/recovery remains REST-based. The implementation makes one publish invocation per successful REST creation path; it does not claim exactly-once network delivery.

## E. Event DTOs and delivery policy

Event constants:

- `ChannelMessageCreated`
- `DirectMessageCreated`

`ChannelMessageCreated` payload:

```text
messageId
channelId
content
sender { userId, displayName, avatarUrl }
createdAt
```

`DirectMessageCreated` payload:

```text
messageId
conversationId
content
sender { userId, displayName, avatarUrl }
createdAt
```

Messages currently have no editable `updatedAt` field, so none is invented. Payloads are dedicated records mapped from persisted REST DTOs; raw EF entities, JWTs, password/security fields, provider subjects, email addresses, and participant private data are not broadcast.

Delivery policy is consistent for both event types: `Clients.Group(...)` broadcasts to the entire authorized group, including the sender. Frontend code must deduplicate the REST response and realtime event by `messageId`.

Channel events go only to their Channel group. Direct events go only to their conversation group. Integration tests verify no cross-Channel or cross-conversation delivery.

## F. Changes

- Added authenticated `ChatHub` and mapped `/hubs/chat`.
- Added centralized event names, group name factories, and event DTOs.
- Added scoped realtime authorization composition over existing Channel/DM services.
- Added a Chat realtime publisher using `IHubContext<ChatHub>`.
- Added persist-first publish calls to the two existing REST send controllers.
- Scoped SignalR query-token extraction to `/hubs/chat`.
- Registered authorization and publisher services.
- Added TestServer/SignalR client packages to the backend test project only; no production dependency was upgraded.
- Added transport-level integration tests and Hub/publisher/controller unit tests.
- Updated existing controller tests for the persist-first publisher dependency.
- No frontend source, backend migration, database schema, Kanban Hub, Notification Hub, or Admin realtime implementation was changed.

## G. Tests and evidence

| Verification | Result |
| --- | --- |
| Focused Hub/transport/publisher suite | PASS, 9/9 |
| Missing JWT connection | PASS, rejected |
| Expired JWT connection | PASS, rejected |
| Query token accepted only for `/hubs/chat` | PASS |
| Authorized Channel joins | PASS |
| Outsider/cross-Channel/inactive joins | PASS, rejected |
| Authorized Direct participant joins | PASS |
| USER_C joining conversation AB | PASS, rejected |
| Persist-first Channel/DM event IDs | PASS, match stored records |
| Database/permission failure publish count | PASS, zero |
| Sender delivery | PASS, sender receives one group event |
| Cross-group isolation | PASS |
| Reconnect | PASS, no event before explicit rejoin; event after authorized rejoin |
| Invalid ID / sanitized Hub error | PASS |
| Event security fields | PASS |
| `dotnet restore` | PASS; existing AngleSharp NU1902 warning remains |
| `dotnet build --no-restore` | PASS, 0 errors |
| Full backend suite | PASS, 261 passed, 0 failed, 0 skipped |
| `npm ci` | PASS; existing 17 vulnerabilities, no audit fix |
| Frontend production build | PASS; existing PWA/Rolldown and chunk warnings remain |
| Frontend source diff | None |
| `git diff --check` | PASS |

The integration factory uses an isolated, randomly named InMemory database and a runtime-generated test signing key. No production database or migration was applied.

## H. Frontend handoff

- Connect to `/hubs/chat` with the normal SignalR `accessTokenFactory`; return the current access token without adding sender identity.
- Invoke `JoinChannel(channelId)` after connection and whenever a Channel becomes active.
- Invoke `LeaveChannel(channelId)` when leaving that Channel if the connection remains active.
- Invoke `JoinDirectConversation(conversationId)` after connection and whenever a Direct conversation becomes active.
- Invoke `LeaveDirectConversation(conversationId)` when leaving that conversation if the connection remains active.
- Subscribe to `ChannelMessageCreated` and `DirectMessageCreated`.
- Treat payload content as text and deduplicate by `messageId`.
- The sender receives the realtime event as well as the REST response.
- After reconnect, explicitly rejoin every still-needed group. No group is automatically restored.
- REST remains persistence and source of truth.
- Refresh REST history after reconnect, detected event gaps, authorization/context changes, or uncertain ordering. SignalR is not history storage.

## I. Deferred features

`READ_STATE_DEFERRED`

`TYPING_DEFERRED`

`PRESENCE_DEFERRED`

`ATTACHMENT_DEFERRED`

`MENTION_DEFERRED`

`WEBRTC_DEFERRED`

Also deferred: reactions, edit/delete realtime, durable delivery, message acknowledgements, and frontend integration task `FE-COLLAB-REALTIME-01`.

## J. Decision

`PASS`

A real authenticated ChatHub now exists; Channel and Direct group joins apply existing permission semantics; messages broadcast only from successful persisted REST creation paths; event/group contracts are stable and isolated; sender policy is consistent; transport integration and full regression tests pass; and frontend source is unchanged.
