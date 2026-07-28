# CORE-COLLAB-TEXT-01 Report

## A. Reproduction matrix

| Scenario | Before | After |
| --- | --- | --- |
| Send channel message | Frontend appended to `collaboration_messages` | Authenticated REST API saves `ChannelMessage` in SQL |
| History | Browser-local array only | Paginated channel-scoped database query |
| Reload/new request | Same browser localStorage only | New DbContext reads the persisted row |
| Cross-user | No real delivery/history | Another authorized channel member can request history |
| Outsider | No server boundary | Concealed `404`; cannot read or send |
| Sender identity | Browser supplied ID/name/avatar locally | Sender ID comes only from JWT; profile is projected from `User` |
| Channel isolation | Local object keys | Query filters by collaboration channel FK |
| Workspace/project isolation | Missing | Active workspace, project, and channel membership are all required |

Root causes were the missing channel scope/membership model and the absence of a service/controller over the existing message table. The current frontend remains localStorage-based until `FE-COLLAB-TEXT-01`.

## B. Data model

- `DATA_MODEL_GAP` was confirmed: the legacy `ChannelMessage.ChannelId` referenced `Department`, which has no unambiguous workspace scope.
- Added `CollaborationChannel` with required `WorkspaceId`, `ProjectId`, creator, name, timestamps, archive, and soft-delete state.
- Added `CollaborationChannelMember` with composite `(ChannelId, UserId)` key, active/left state, and `CanSendMessages`.
- `ChannelMessage` keeps the existing Department FK as nullable `LegacyDepartmentId` mapped to the existing `ChannelId` column. Existing rows are not deleted.
- New REST messages use nullable `CollaborationChannelId` with a restrictive FK to `CollaborationChannels`.
- History index: `(CollaborationChannelId, SentAt, Id)`.
- Channel indexes cover workspace/project lookup and active project channel lookup.
- Migration: `20260728091647_AddCollaborationChannelText`, with migration, Designer, and updated ModelSnapshot.
- Migration is pending and was not applied to production or an unidentified database.
- Legacy Department messages are intentionally excluded from the new API because they cannot be assigned a safe workspace/project scope automatically.
- Channel uses archive/soft-delete state. Message edit/delete is not part of the existing model and is deferred.

## C. API contract

### History

`GET /api/channels/{channelId}/messages?page=1&pageSize=50`

- `page`: minimum 1.
- `pageSize`: 1 through 100.
- Response:

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {
    "items": [
      {
        "messageId": "guid",
        "channelId": "guid",
        "content": "plain text",
        "sender": {
          "userId": "guid",
          "displayName": "name",
          "avatarUrl": null
        },
        "createdAt": "server timestamp",
        "orderingId": "same stable message guid"
      }
    ],
    "page": 1,
    "pageSize": 50,
    "totalCount": 1,
    "ordering": "createdAt_desc,messageId_desc"
  }
}
```

History is newest-first, then message ID descending as a deterministic tie-breaker.

### Send

`POST /api/channels/{channelId}/messages`

```json
{
  "content": "Nội dung"
}
```

- Returns `201` with the persisted message DTO in the normal API envelope.
- Content is trimmed, CRLF/CR normalized to LF, required, and limited to 4,000 characters.
- Request DTO has no sender, user, profile, workspace, project, timestamp, attachment, or mention field.

### Errors

- `400`: invalid page/page size or invalid content.
- `401`: missing/invalid authenticated user.
- `403`: known read member lacks send permission.
- `404`: missing/deleted/archived channel or concealed membership/scope denial.
- `409`: reserved for a future concurrent channel-state contract; no current relation mutation occurs in these endpoints.
- `429`: deferred until a Chat-specific rate policy is approved.

## D. Changes

- Controller: `ChannelMessagesController`.
- Application DTOs: send request, sender projection, message DTO, and paginated result.
- Application contract/exceptions: `IChannelTextService`.
- Infrastructure: `ChannelTextService`.
- Domain: collaboration channel/member entities and legacy-safe ChannelMessage mapping.
- EF: DbSets, relationships, indexes, migration, Designer, and snapshot.
- DI: scoped channel text service registration.
- Authorization reuses `IResourceAuthorizationService` for workspace/project policy and adds channel membership checks.
- Persistence uses one server-created GUID/timestamp and one `SaveChangesAsync`; no static collection, cache, file, localStorage, or mock fallback.
- Sender projection is performed in the database query, avoiding N+1 sender lookups.
- No frontend, DM, SignalR, attachment, mention, or WebRTC source was changed.

## E. Security

- `P0_BLOCKER`: resolved for channel text persistence/API; no sender identity is accepted from request data.
- Workspace and project must both be active and match the channel.
- User must be active, not deleted, and have active workspace, project, and channel memberships.
- Read-only channel members receive `403` on send.
- Outsiders and users from another workspace/project receive concealed `404`.
- Deleted/archived channels reject history and send.
- There is no message-by-ID endpoint, so this task introduces no message ID enumeration surface.
- Content policy is plain text. The backend does not trust/sanitize frontend HTML into rich text; JSON encoding escapes dangerous markup and the frontend handoff must render with text interpolation.
- Message content is not written to application logs.
- `P1_REQUIRED`: create/list/provision channel management must preserve the same scope checks when introduced.
- `P2_HARDENING`: Chat-specific rate limiting/spam control and audit metadata are deferred.
- `DEFERRED`: attachments, mentions, read receipts, message edit/delete, and realtime.

## F. Tests

- Added 16 ChannelText tests.
- Covered JWT sender identity, fake sender field rejection, SQL persistence across DbContexts, member read, outsider read/send denial, inactive user denial, read-only send denial, deleted channel denial, channel/workspace isolation, empty/whitespace/over-limit validation, Unicode, plain-text HTML policy, deterministic pagination, no lost/duplicate rows, total count, permission changes, and safe response fields.
- SQL Server relational evidence uses the dedicated `SprintACollabText01Integration` LocalDB database.
- The SQL test verifies persistence through a fresh DbContext, channel FK enforcement, and the history index. It cleans only its uniquely identified fixture rows and does not drop the database.
- Targeted ChannelText tests: 16/16 PASS.
- Full backend tests: 224 passed, 0 failed, 0 skipped.
- Backend build: PASS, 0 errors; two existing NU1902 AngleSharp warnings.
- Frontend `npm ci`: PASS; existing 17 vulnerabilities, no audit fix.
- Frontend production build: PASS; existing PWA/Rolldown and large-chunk warnings.
- Migration list: `AddCollaborationChannelText` is pending; no database migration was applied.
- Persistence evidence: `REAL_BACKEND_PERSISTENCE`.
- Realtime status: `REALTIME_DEFERRED_TO_CORE_COLLAB_REALTIME_01`.

## G. Frontend handoff

- Replace Channel history reads with `GET /api/channels/{channelId}/messages`.
- Replace Channel sends with `POST /api/channels/{channelId}/messages` and body `{ content }`.
- Use the backend sender and server timestamp from the response.
- Render history in the returned newest-first order or reverse once for an oldest-to-newest thread; do not re-sort by timestamp alone.
- Respect `page`, `pageSize`, `totalCount`, and `ordering`.
- Remove `collaboration_messages` and Channel message entries in `mockMessages` as the source of truth.
- Do not remove unrelated preferences or DM local behavior in the frontend text task.
- Add loading, empty, error, retry, and disabled-send states without fake success.
- Treat `404` as unavailable/not authorized without disclosing membership details; handle `400`, `401`, and `403` distinctly.
- Maximum content length is 4,000 characters after newline normalization/trim.
- Realtime is not available. Refresh history after successful sends or explicit user refresh; do not simulate realtime.
- No `clientMessageId` or idempotency key exists in this contract. Until a server idempotency contract is added, the frontend must allow only one send request at a time and must not automatically retry POST.
- Channel IDs must refer to backend-provisioned `CollaborationChannel` rows. Channel creation/list management is not introduced by this persistence task.

## H. Decision

`PASS`

Channel text now has real database persistence, JWT-derived sender identity, workspace/project/channel authorization, deterministic paginated history, SQL Server relational evidence, and full regression coverage. Frontend migration and realtime remain explicitly deferred.
