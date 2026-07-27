# FE-CYCLE-01 Report

## A. Contract mapping

- List: `GET /api/projects/{projectId}/sprints`.
- Detail: `GET /api/projects/{projectId}/sprints/{cycleId}`.
- Start: `POST /api/projects/{projectId}/sprints/{cycleId}/start`, without a request body.
- Close: `POST /api/projects/{projectId}/sprints/{cycleId}/close`, with `{ "targetSprintId": null }` to move unfinished tasks to backlog.
- Success uses the existing `ApiResponse<SprintResponseDto>` envelope. The shared client unwraps `data`.
- Response fields consumed or retained include `id`, `projectId`, `workspaceId`, `name`, `startDate`, `endDate`, `status`, `state`, `startedAt`, `completedAt`, task metrics, `isFavorite`, and `createdAt`.
- API state values are `Upcoming`, `Active`, and `Completed`. The mapper also accepts storage value `Planned` as `Upcoming` for compatibility.
- There is no RowVersion or other client concurrency field in this contract.
- Active-cycle scope is Project.
- Start of an already-active cycle and close of an already-completed cycle are idempotent backend operations.
- HTTP 400 is an invalid request/scope, 403 is denied permission, 404 is unavailable or hidden scope, and 409 is a transition/concurrency conflict.
- Stable 409 codes: `CYCLE_ALREADY_COMPLETED`, `CYCLE_NOT_ACTIVE`, `CYCLE_DATES_INVALID`, `ACTIVE_CYCLE_EXISTS`, and `INVALID_TARGET_CYCLE`.

## B. Previous behavior

| Surface | Previous behavior | Problem |
| --- | --- | --- |
| `CyclesView` | Derived the project from route or `lastProjectId` in localStorage and could select the first project | Cycle state could come from an old or guessed project instead of current route context |
| `useSprintStore` | Kept one `sprints` array while caching timestamps for multiple projects | A warm cache entry could leave another project's cycles visible; old responses were not sequenced |
| `CyclesTab` start | Called the real start endpoint through the store, then used a blocking alert for errors | No action lock, no status/permission guard on the button, and no structured 409 refresh |
| `CyclesTab` close | No close action | The primary Cycle screen could not complete the active Cycle |
| Project Settings start | Displayed Start for every state except `Active` | A completed Cycle incorrectly offered Start |
| Project Settings close | Called the endpoint directly after a generic confirmation | URL logic was duplicated, action was not locked, and 404/409 did not refresh local data |
| Session/context | Cycle state was not cleared on logout or leaving project context | Previous-user/project state could remain visible until another successful request |

Current Cycle was already derived from `activeSprint` in the shared store and is consumed by `SpaceDashboard`. Updating that store is therefore the controlled refresh path for Cycle list, Current Cycle, sidebar favorites and dashboard summary.

## C. Changes

- Added `sprintApi` as the single client for list, current, detail, favorite, start and close operations. It supports `AbortController.signal` and preserves Axios errors.
- Added centralized state metadata and business-error mapping in `sprintState`.
- Reworked `useSprintStore` to bind data to a project and authenticated session identity.
- Added list request sequencing and cancellation so an old response cannot overwrite a new project/session.
- Added one in-flight transition per action/project/cycle. Duplicate clicks return without issuing another request or success notification.
- A project/session change aborts transitions and clears Cycle list, Current Cycle, details, loading and error state.
- Successful transitions first apply the returned DTO and then force-refresh the server list. This refresh updates Current Cycle and all consumers of the shared store.
- HTTP 404 and 409 force-refresh the latest Cycle state before the error is shown.
- `CyclesView` now uses only `route.params.id`; Cycle state no longer uses localStorage as source of truth.
- Added a close action to active Cycle cards with an accessible confirmation modal. It names the Cycle, explains backlog behavior, preserves task/status/assignment expectations, blocks dismissal/submission while loading, focuses the confirm action and supports Escape when idle.
- Start is visible only for `Upcoming`; Close is visible only for `Active`; neither is shown for `Completed`.
- Added independent Start/Close loading labels and disabled states.
- Updated Project Settings to use the shared store for transitions, centralized states/errors, correct action visibility and conflict refresh.
- Logout and departure from project context clear the Cycle store.
- Burndown and local Cycle-panel state are sequenced/cleared when project context changes.

`PERMISSION_UI_CONTRACT_GAP`: the frontend only exposes project-role write capability, not the exact backend `sprint.manage` permission code. Actions are hidden when known project-write access is absent, while backend 403 remains authoritative. No role name was added or hard-coded.

## D. Verification

- Git preflight: branch `agent/kimi-my-work`, clean worktree, backend checkpoint present as cherry-pick `a2fe19e5`.
- Before changes: `npm ci` PASS; 567 packages audited, with 17 existing vulnerabilities (3 moderate, 14 high).
- Before changes: `npm run build` PASS.
- After changes: `npm run build` PASS; 4,527 modules transformed.
- Existing non-blocking warnings remain: deprecated packages, PWA/Rolldown bundle assignment, chunks over 1,600 kB and plugin timing.
- No test script exists in `Frontend/package.json`; automated frontend QA remains deferred.
- `git diff --check` PASS.
- Encoding scan of the changed Cycle route/store/client/component files found no mojibake.
- Responsive implementation includes a 390px modal layout, bounded viewport height, full-width mobile actions and wrapping text.
- Light/dark styling uses existing surface, border and text variables; destructive/warning colors retain contrast in both themes.
- Keyboard behavior includes focused confirmation and Escape dismissal only while no close request is running.
- No frontend package file, backend file, migration or generated build output is included in the diff.

`BROWSER_EVIDENCE_NOT_AVAILABLE`

API port 5136 and frontend dev/preview ports were not running, and authenticated Manager/Member/Outsider sessions were unavailable. No mock backend or fake browser success was introduced.

`RUNTIME_MIGRATION_NOT_APPLIED`

Migration `20260727141026_EnforceCycleTransitions` remains pending according to the backend checkpoint report. It was not edited or applied.

## E. Deferred

- Apply the pending migration only to an explicitly approved local/shared database.
- Run authenticated browser E2E with Manager, Member and Outsider after backend, migrated test database and sessions are available.
- Verify persisted close/start, 409 refresh, F5 behavior, task/assignment retention, project switching, 390px rendering and light/dark modes against that runtime.
- Automated frontend QA belongs to `FE-QA-BASELINE-01`.
- No unfinished-task rule gap is deferred: the backend contract explicitly moves unfinished tasks to the selected planned Cycle or backlog while preserving status and assignments.

## F. Decision

**PASS**

Close and Start use the real shared API client, state/action mapping follows the backend response contract, list/current/detail state is refreshed from the server, 400/403/404/409 errors remain failures, duplicate/stale requests are controlled, context cleanup is implemented, production build passes, and the diff is frontend-only. This is a code/build decision, not a runtime or browser PASS.
