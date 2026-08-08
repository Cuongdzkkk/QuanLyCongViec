# FE-STAR-RECENT-01 Report

## A. Contract mapping

- Backend checkpoint requested: `a4787d64`. This hash is not an ancestor of the frontend worktree, but its stable patch-id is `0acdde7a082b3a1da85dd142b1fc51063740f230`, exactly matching the content-equivalent cherry-pick at HEAD `71bedd78`.
- Starred list: `GET /api/workspaces/{workspaceId}/starreditems?page={page}&pageSize={pageSize}`.
- Star: `POST /api/workspaces/{workspaceId}/starreditems` with `{ itemType, itemId }`.
- Unstar: `DELETE /api/workspaces/{workspaceId}/starreditems/{itemType}/{itemId}`.
- Recent list: `GET /api/recentviews?page={page}&pageSize={pageSize}`.
- Record viewed: `POST /api/recentviews` with identity only: `{ entityType, entityId }`.
- Canonical frontend mapping is centralized as `Project` and `WorkTask`. Goal/Team compatibility flows also use the same explicit shared Starred/Recent client.
- Collection responses are normalized from the API envelope and retain backend `title`, `subtitle`, `url`, `icon`, workspace/project IDs, timestamps, and pagination metadata.
- Backend returns `400` for invalid entity data, `401` for an invalid session, and `403` for inaccessible entities/workspaces. Mutation errors propagate to the calling UI.

## B. Previous local state

- `useWorkTaskStore`, `ForYou`, `SpaceSummary`, `RecentDropdown`, `RecentPopup`, and `NexusSidebar` read or wrote `localStorage["recently_viewed_tasks"]`.
- WorkTask stars were held in `useWorkTaskStore.starredTasks`, used the non-canonical `Task` type, optimistic temporary IDs, and the legacy toggle endpoint.
- Project stars were split between `/projects/{id}/favorite`, local `isFavorite` fields, and the Starred toggle endpoint.
- Starred/Recent pages and dropdowns fetched independently, reconstructed routes, swallowed errors, or performed client-side pagination.
- The legacy key removed is only `recently_viewed_tasks`. Theme, sidebar, session/auth, chat, and unrelated navigation preferences remain unchanged.

## C. Changes

- Added `Frontend/src/api/starredRecentApi.js` as the single URL/contract client.
- Reworked `useStarredStore` into the shared Starred/Recent state owner with normalized pagination, cancellation, request sequencing, propagated errors, explicit idempotent POST/DELETE mutations, pending-item guards, and stable canonical keys.
- Context changes (logout/login user/workspace) abort requests and clear lists, pagination, known star status, errors, and pending state. Late responses cannot write into a new context.
- Project and WorkTask star actions now wait for a successful backend response. Buttons on primary surfaces expose pending/disabled and accessible labels; errors retain the server message where available.
- Starred page/dropdown and Recent page/dropdown use backend metadata and server URLs. Full pages use backend pagination with loading, empty, error, and retry states.
- Project views are recorded only after project detail fetch completes successfully. WorkTask views are recorded after a successfully loaded task is opened in the detail modal. Tracking failure does not block detail display.
- Removed Starred/Recent business data from local state/localStorage sources. No mock fallback was added.

## D. Verification

- Baseline `npm ci`: PASS. Audit reported 17 existing vulnerabilities (3 moderate, 14 high).
- Baseline `npm run build`: PASS.
- Final `npm ci`: PASS. No package or lockfile changes.
- Final `npm run build`: PASS; 4525 modules transformed.
- Existing build warnings remain: PWA plugin bundle assignment under Rolldown and chunks larger than 1600 kB. Deprecated `source-map` and `glob` notices appeared during install.
- `git diff --check`: PASS.
- Static source scan: no Starred/Recent component reads `recently_viewed_tasks`; no legacy Starred toggle call; no `Task` item type; only the shared API client calls Recent endpoints.
- Responsive/light/dark/UTF-8: existing layouts and theme variables were retained; controls use wrapping/responsive constraints and Vietnamese source remains UTF-8. Runtime visual evidence was not claimed.
- `BROWSER_EVIDENCE_NOT_AVAILABLE`
- `RUNTIME_MIGRATION_NOT_APPLIED`

## E. Deferred

- Apply `PersistStarredAndRecentlyViewedItems` only to an explicitly approved local/shared non-production database.
- Run authenticated browser E2E with `USER_A` and `USER_B`, including refresh persistence, isolation, double-click, pagination, mobile 390 px, light, and dark checks.
- QA automation belongs to `FE-QA-BASELINE-01`.
- Chat localStorage and unrelated favorite features (Cycle, Sprint, Page, View, Module) were not changed.

## F. Decision

`PASS`

The frontend contract integration and production build meet the task acceptance conditions. Runtime persistence and authenticated browser claims remain explicitly deferred because the required migration has not been applied to a confirmed safe test database.
