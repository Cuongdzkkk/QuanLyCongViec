# CORE-COLLAB-CHANNEL-DISCOVERY-01 Report

## A. Blocker reproduction

- `CHANNEL_ID_CONTRACT_GAP` was reproduced: `CollaborationChat.vue` uses
  browser-local servers and channels such as `ch-general`, timestamp-derived
  IDs, and an invalid attempt to assign Department rows to a read-only channel
  computed value.
- `CHANNEL_LIST_BACKEND_GAP` was reproduced: before this task there was no
  backend endpoint that listed accessible `CollaborationChannel` rows.
- A Department ID is not a Collaboration Channel ID. No Department/name/first
  row mapping or generated frontend GUID was introduced.
- The existing Channel Message API requires the real
  `CollaborationChannel.Id` at
  `GET/POST /api/channels/{channelId}/messages`.

## B. Data model

| Entity | Key and scope | Membership/authorization data | State and indexes |
| --- | --- | --- | --- |
| `CollaborationChannel` | GUID `Id`; required `WorkspaceId` and `ProjectId` | `CreatedByUserId`; project roles determine management | `Name`, nullable `Description`, timestamps, `IsArchived`, `IsDeleted`; project discovery indexes |
| `CollaborationChannelMember` | Composite `(ChannelId, UserId)` | `IsActive`, `LeftAt`, `CanSendMessages` | User/active index; FK to the real channel |
| `ChannelMessage` | GUID `Id`; nullable `CollaborationChannelId` for the new contract | Sender comes from JWT; message service checks workspace, project, and channel membership | Stable `(CollaborationChannelId, SentAt, Id)` history index |

- Channel scope is always both Workspace and Project. `ProjectId` is required.
- The current model supports private membership channels only. Public channels
  are rejected rather than simulated.
- Every discoverable channel requires active channel membership.
- There is no Position field. Discovery orders by Name, CreatedAt, then ChannelId.
- Duplicate names are allowed within a project because identity is the server
  GUID, never the name.
- `CreatedByUserId` exists and is always derived from JWT.
- New migration:
  `20260728095916_AddCollaborationChannelDiscovery`.
- The migration adds nullable `Description`, nullable internal
  `ProvisioningKey`, and a filtered unique index on
  `(ProjectId, CreatedByUserId, ProvisioningKey)`.
- The earlier committed `AddCollaborationChannelText` migration was not edited.

## C. Discovery contract

`GET /api/projects/{projectId}/channels?page=1&pageSize=50`

- `projectId`: required route GUID; no workspace or first-project fallback.
- `page`: minimum 1.
- `pageSize`: 1 through 100.
- Scope checks: active JWT user, active Workspace membership, active Project
  membership, active Project, non-deleted Workspace.
- Results contain only active, non-archived, non-deleted channels for the route
  Project where the current user has an active Channel membership.
- Private channels are not visible to non-members. Duplicate membership rows
  cannot occur because membership uses a composite primary key.
- Ordering:
  `name_asc,createdAt_asc,channelId_asc`.

Response uses the standard API envelope. `data` is:

```json
{
  "items": [
    {
      "channelId": "server-guid",
      "name": "general",
      "description": "Project discussion",
      "workspaceId": "workspace-guid",
      "projectId": "project-guid",
      "visibility": "Private",
      "isMember": true,
      "canRead": true,
      "canSend": true,
      "canManage": true,
      "createdAt": "utc-timestamp",
      "updatedAt": "utc-timestamp"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1,
  "ordering": "name_asc,createdAt_asc,channelId_asc"
}
```

Errors: `400` invalid pagination, `401` missing/invalid identity, and concealed
`404` for a missing or unauthorized Project scope.

## D. Provisioning contract

`POST /api/projects/{projectId}/channels`

Required header:

```text
Idempotency-Key: client-generated-stable-request-key
```

The key must contain 1-100 ASCII letters, digits, `-`, `_`, `.`, or `:`. The
frontend must reuse the same key only when retrying the same create request.

Request:

```json
{
  "name": "general",
  "description": "Project discussion",
  "visibility": "Private"
}
```

- Name is trimmed, Unicode Form C normalized, required, at most 100 characters,
  and cannot contain control characters.
- Description is optional, trimmed, Unicode Form C normalized, and at most 500
  characters.
- Only `Private` visibility is currently accepted.
- The request cannot set channel ID, workspace ID, creator/user ID, role, or
  timestamps.
- Project write permission is required. Creator identity comes from JWT.
- Channel and creator membership are saved atomically. The creator can read,
  send, and manage through their active Project manager permission.
- SQL Server takes an update/hold lock on the active Project row inside a
  serializable transaction, preventing create from racing an archive update.
- First success returns `201`. An exact retry returns `200` with the original
  Channel DTO and ID. Reusing a key for another payload returns `409`.
- Same-name channels are allowed and receive distinct server GUIDs. Concurrent
  same-name creates do not merge identity or corrupt membership.
- Other errors: `400` validation, `401` unauthenticated, `403` insufficient
  management permission, `404` missing/archived Project.
- Policy is explicit creation. GET has no side effect and creates no demo rows.
- `DEFAULT_CHANNEL_POLICY_DEFERRED`.

## E. Integration with Channel Text

- Discovery and create return the real `CollaborationChannel.Id` as `channelId`.
- That value is used without translation:
  - `GET /api/channels/{channelId}/messages?page=1&pageSize=50`
  - `POST /api/channels/{channelId}/messages` with `{ "content": "..." }`
- Automated coverage creates a Channel, sends through `ChannelTextService`,
  opens a new DbContext, discovers the same ID, and reads persisted history.
- Removing Channel membership removes discovery, history, and send access on
  the next request.
- Cross-workspace/project and deleted/archived channels are excluded.

## F. Security

- Current user and creator are derived only from the JWT
  `ClaimTypes.NameIdentifier`.
- Fake `creatorId` or `userId` JSON properties cannot alter ownership.
- Workspace and Project authorization use `IResourceAuthorizationService`.
- Discovery requires active Channel membership and does not expose the member
  list, role strings, provisioning key, EF entity, or private user fields.
- Private Channel GUID enumeration remains concealed by the Message API as
  `404`.
- Inactive/deleted users, missing memberships, inactive Projects, archived
  Projects, deleted Workspaces, and deleted/archived Channels are rejected or
  excluded according to the route contract.
- Public behavior is deliberately unavailable until a real public-channel model
  and policy exist.

## G. Tests

- New logic/API tests: 15 PASS.
- New SQL Server relational test: 1 PASS.
- SQL evidence covers atomic creator membership, persistent discovery,
  idempotent retry, filtered unique index, concurrent same-name creation,
  deterministic identity, and Message API compatibility.
- Existing Channel Text SQL fixture was versioned to a fresh integration
  database after the first full run found its prior `EnsureCreated` database
  retained the pre-discovery schema. No database was dropped or reset.
- Final restore: PASS.
- Final backend build: PASS, 0 errors, 2 existing `NU1902` AngleSharp warnings.
- Final backend tests: 240 passed, 0 failed, 0 skipped.
- The existing clean-database migration deployment test passed as part of the
  full suite.
- `dotnet ef migrations list`: PASS; 28 migrations. The Cycle, Google,
  Collaboration Text, and Collaboration Discovery migrations are pending in the
  configured local runtime. No migration was applied by this task.
- Frontend `npm ci`: PASS; 567 packages audited, 17 existing vulnerabilities
  (3 moderate, 14 high). No audit fix was run.
- Frontend production build: PASS; existing PWA/Rolldown and chunk-size warnings.

## H. Frontend handoff

1. Continue the existing blocked `FE-COLLAB-TEXT-01` task.
2. Obtain a real Project ID from the existing authorized Project context.
3. Call `GET /api/projects/{projectId}/channels`.
4. Render only returned `items`; remove local/demo Channel IDs and do not use
   Department IDs, name matching, first-row fallback, or generated GUIDs.
5. Use `item.channelId` directly for Channel Message GET and POST.
6. Use `canRead`, `canSend`, and `canManage` for frontend affordances while
   treating backend authorization as authoritative.
7. To create, call `POST /api/projects/{projectId}/channels` with a stable
   `Idempotency-Key` and the documented request body; use the returned
   `data.channelId`.
8. Handle `400`, `401`, `403`, `404`, and `409` without fake success.

Runtime migration status: `AddCollaborationChannelText` and
`AddCollaborationChannelDiscovery` are pending. They must be applied through the
approved deployment process before runtime frontend verification.

## I. Decision

`PASS`

The backend now provides authenticated project-scoped discovery and explicit
provisioning of real Collaboration Channel IDs, atomic creator membership,
stable pagination/order, isolation, idempotent retry, SQL Server relational
evidence, direct Channel Text compatibility, and a complete frontend handoff.
No frontend source, dependency, production database, or unrelated feature was
changed.
