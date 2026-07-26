# CORE-STAR-RECENT-01 Report

## A. Reproduction matrix

| Feature | Existing route/call | Reproduced behavior | Root cause | Result |
| --- | --- | --- | --- | --- |
| Star | `POST /api/workspaces/{workspaceId}/starreditems/toggle` | Project/Goal/Team/User could toggle; task used `Task` and could not satisfy the database constraint | Frontend alias differed from the database canonical values; no entity/permission validation | Fixed with canonical boundary normalization and readable-entity authorization |
| Unstar | Same toggle endpoint | Retry could invert the requested outcome; no explicit idempotent operation | Toggle semantics were the only contract | Added explicit idempotent `DELETE`; compatibility toggle retained |
| Starred list | `GET /api/workspaces/{workspaceId}/starreditems` | No pagination, no permission revalidation, orphan/deleted rows could be returned, and task metadata was absent | Raw polymorphic records were returned after separate name lookups | Fixed with batched entity projections, stable metadata, filtering, ordering, and pagination |
| Record viewed | `POST /api/recentviews` | Project and Goal persisted, but client-controlled title/URL were trusted and entity access was not checked | Controller upserted payload directly | Fixed; only type/ID are accepted as identity and metadata is resolved server-side after authorization |
| Recently viewed list | `GET /api/recentviews?limit=...` | UserId isolation existed, but deleted/orphan/unauthorized resources remained visible | Query only filtered by UserId | Fixed with current permission/entity revalidation and deterministic pagination |
| Task recent | LocalStorage only in current frontend | Did not persist across account/browser sessions | Frontend never called the backend record-view endpoint | Backend now supports canonical `WorkTask`; `FRONTEND_HANDOFF_REQUIRED` |
| Project favorite | Some screens call `PUT /api/projects/{id}/favorite` | Favorite state and personal StarredItems are separate persistence mechanisms | Project navigation config is not a per-user starred record | `FRONTEND_HANDOFF_REQUIRED` to use the personal starred API |

## B. Data model

- Existing tables retained: `StarredItems` and `RecentViews`.
- Existing unique policies retained:
  - Starred: `(UserId, WorkspaceId, ItemType, ItemId)`.
  - Recent: `(UserId, EntityType, EntityId)`.
- Migration added: `20260726152821_PersistStarredAndRecentlyViewedItems`.
- Migration adds `StarredItems.UpdatedAt`, ordering indexes for both lists, and canonical type constraints.
- Migration normalizes case and task aliases to `WorkTask`, deduplicates by newest timestamp before normalization, backfills `UpdatedAt` from `CreatedAt`, and removes unsupported type rows that cannot map to a valid resource.
- Canonical starred types: `Project`, `WorkTask`, `Goal`, `Team`, `User`.
- Accepted task aliases at the API boundary: `Task`, `work-task`, `work_task`, `worktask`.
- Database rows always store canonical values. Invalid types return HTTP 400.
- Re-view updates the single row's `ViewedAt`; history is not truncated when a user reads page 1.
- The migration is pending and was not applied to a production or development database.

## C. Changes

Main files:

- Controllers: `StarredItemsController.cs`, `RecentViewsController.cs`.
- DTO/interfaces: `PersonalEntityCollectionDto.cs`, `IStarredItemService.cs`, `IRecentViewService.cs`, `IPersonalEntityReferenceResolver.cs`.
- Services: `StarredItemService.cs`, `RecentViewService.cs`, `PersonalEntityReferenceResolver.cs`, `PersonalEntityMutationLock.cs`.
- Model/migration: `StarredItem.cs`, `ApplicationDbContext.cs`, migration/Designer/snapshot.
- Tests: `StarredRecentPersistenceTests.cs`.

Behavior:

- Current user always comes from the JWT name identifier claim; no client user ID is accepted.
- Explicit star and unstar are idempotent. The old toggle endpoint remains for frontend compatibility.
- Mutations verify active workspace membership and resource read access before persistence.
- Project and WorkTask require active project membership; Goal requires active workspace membership.
- Team/User starred records are resolved only under an explicit authorized workspace.
- Deleted, archived, orphaned, and no-longer-readable resources remain internally recoverable but are suppressed from API lists.
- Polymorphic metadata is resolved in bounded batch queries by entity type, avoiding per-item N+1 queries.
- Ordering is `CreatedAt DESC, Id ASC` for Starred and `ViewedAt DESC, Id ASC` for Recent.
- List responses preserve the existing `data` array and add `pagination: { totalCount, page, pageSize }`.
- In-process keyed locks plus database unique constraints protect concurrent star/view requests. Database constraint exceptions are handled as idempotent retries.

## D. Tests

- New tests: 15 PASS.
- Full backend tests: **157 passed, 0 failed, 0 skipped**.
- Backend restore/build: PASS, 0 errors.
- Existing warning: `AngleSharp 0.17.1` has known moderate advisory `GHSA-pgww-w46g-26qg`; no dependency was changed.
- SQL Server LocalDB relational test: PASS.
  - Verified both unique constraints reject duplicate rows.
  - Verified canonical type check constraint rejects unsupported values.
  - Used isolated fixture IDs and deleted only fixture rows; no database drop/reset was run.
- InMemory service/controller tests cover JWT identity, alias normalization, idempotency, two-user isolation, outsider denial, new DbContext persistence, deleted/orphan suppression, pagination, stable ordering/metadata, list-read semantics, re-view update, and concurrent requests.
- Migration list includes `PersistStarredAndRecentlyViewedItems (Pending)`.
- `dotnet ef migrations has-pending-model-changes`: no model changes after the migration.
- Frontend `npm ci`: PASS; reported 17 existing vulnerabilities (3 moderate, 14 high). No audit fix was run.
- Frontend production build: PASS, 4,524 modules. Existing PWA/Rolldown and large-chunk warnings remain.
- Encoding scan: no new mojibake in changed files.

## E. Frontend handoff

`FRONTEND_HANDOFF_REQUIRED`

### Starred contract

- List: `GET /api/workspaces/{workspaceId}/starreditems?page=1&pageSize=50`
- Star: `POST /api/workspaces/{workspaceId}/starreditems`
- Star body: `{ "itemType": "Project|WorkTask|Goal|Team|User", "itemId": "guid" }`
- Unstar: `DELETE /api/workspaces/{workspaceId}/starreditems/{itemType}/{itemId}`
- Compatibility toggle: `POST /api/workspaces/{workspaceId}/starreditems/toggle?itemType=...&itemId=...`
- Response envelope: `{ statusCode, message, data, pagination? }`.
- List item fields: `id`, `itemType`, `itemId`, `workspaceId`, `projectId`, `itemName`, `title`, `subtitle`, `url`, `icon`, `createdAt`, `updatedAt`.

Frontend behavior to change in `FE-STAR-RECENT-01`:

- Project star controls intended for the personal Starred list must use the Starred contract rather than project-global `favorite`.
- Use canonical `WorkTask` for task stars; legacy `Task` remains accepted only as a compatibility alias.
- Use explicit POST/DELETE instead of toggle when the desired state is known.
- Consume server navigation metadata and pagination.

### Recently Viewed contract

- Record view: `POST /api/recentviews`
- Request: `{ "entityType": "Project|WorkTask|Goal", "entityId": "guid" }`
- List: `GET /api/recentviews?page=1&pageSize=50`
- Legacy list query `limit` remains accepted.
- List item fields: `id`, `entityType`, `entityId`, `workspaceId`, `projectId`, `title`, `subtitle`, `url`, `icon`, `viewedAt`.

Frontend behavior to change:

- Call record-view only after the detail entity loaded successfully.
- Add WorkTask record-view calls and remove LocalStorage as business-data persistence/fallback.
- Do not send display title, URL, icon, or user ID as authoritative data.
- Parse the top-level pagination metadata.

## F. Decision

**PASS**

Starred and Recently Viewed now persist across request contexts, enforce current-user and resource permissions, isolate users, prevent duplicates, filter inaccessible records, and paginate deterministically. Full backend tests and SQL Server relational evidence pass; the migration is consistent and intentionally remains unapplied.
