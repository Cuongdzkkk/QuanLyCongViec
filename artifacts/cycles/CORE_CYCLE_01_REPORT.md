# CORE-CYCLE-01 Report

## A. Reproduction matrix

| Feature | Frontend route/component | Frontend API call | Backend endpoint/controller/service | Entity/table | Current failure before fix | Expected behavior |
| --- | --- | --- | --- | --- | --- | --- |
| Close current cycle | Project Settings / Cycles | `POST /api/projects/{projectId}/sprints/{cycleId}/close` with `{ "targetSprintId": null | GUID }` | `SprintsController.Close` / `SprintService.CloseAsync` | `Sprint` and related `WorkTask` rows | A cycle was selected by cycle ID without enforcing the route project ID; date-based read synchronization could restore an old state | Only an active cycle in the authorized project can close; retry returns the persisted completed cycle |
| Start next cycle | Project Settings / Cycles | `POST /api/projects/{projectId}/sprints/{cycleId}/start` with no body | `SprintsController.Start` / `SprintService.StartAsync` | `Sprint` | A start only set a boolean and a later read could reset it from dates; no database guarantee prevented two active cycles | Start the requested planned cycle, persist the state, and guarantee at most one active cycle per project |
| Current cycle query | Cycles store/view | `GET /api/projects/{projectId}/sprints` | `SprintsController.GetByProject` / `SprintService.GetByProjectAsync` | `Sprint` | GET mutated cycle state according to dates; planned and completed were both represented by `Status = false` | Read-only deterministic query returns persisted `Upcoming`, `Active`, or `Completed` state |
| Permissions | Project resource guard | Same endpoints | `ProjectAuthorize(sprint.manage)` / `ResourceAuthorizationService` | User and workspace/project membership | Resource checks did not reject an inactive project, and service close did not bind the cycle to the route project | Active user, active memberships, active/non-deleted project and `sprint.manage` are required; cross-project GUIDs are rejected |
| Concurrency | N/A | Concurrent close/start requests | `SprintService` relational transaction | `Sprint` | Check-then-update had no project-scoped serialization or unique active constraint | Serializable transaction, SQL Server application lock, and filtered unique index keep the result consistent |

Root causes:

- Cycle state was derived and mutated during reads from dates and a legacy boolean instead of being persisted as a state machine.
- Planned and completed cycles were not distinguishable in storage.
- Close did not require the cycle to belong to the route project.
- Active-cycle uniqueness was enforced only by application checks.
- Invalid transitions could surface as generic exceptions instead of stable HTTP 409 responses.

The active-cycle business scope is **Project**, as established by the `Sprint.ProjectId` relationship, project-scoped routes, authorization and existing queries.

## B. State machine

Persisted internal states are `Planned`, `Active`, and `Completed`. API compatibility maps `Planned` to response value `Upcoming`.

| Current state | Action | Result | Decision |
| --- | --- | --- | --- |
| Planned | Start | Active | Allowed with `sprint.manage` |
| Active | Close | Completed | Allowed with `sprint.manage` |
| Active | Start retry | Active | Idempotent success |
| Completed | Close retry | Completed | Idempotent success; no duplicate rollover/audit |
| Completed | Start | None | Rejected with 409 `CYCLE_ALREADY_COMPLETED` |
| Planned | Close | None | Rejected with 409 `CYCLE_NOT_ACTIVE` |
| Any | Start while another project cycle is active | None | Rejected with 409 `ACTIVE_CYCLE_EXISTS` |
| Active | Close to itself or to a non-planned target | None | Rejected with 409 `INVALID_TARGET_CYCLE` |
| Planned | Start with invalid date range | None | Rejected with 409 `CYCLE_DATES_INVALID` |
| Deleted cycle | Start/close | None | Excluded by query filter; 404 |
| Inactive/deleted project or inactive user/membership | Start/close | None | Rejected by resource authorization |

At most one non-deleted `Active` cycle may exist per project. Cycles in other projects do not block a transition. Ordering is deterministic by `StartDate`, `CreatedAt`, then `Id`.

## C. Data model

Existing `Sprint.Status` is retained for compatibility. The model now also persists:

- `State` (`Planned`, `Active`, `Completed`)
- `StartedAt`
- `CompletedAt`
- `IsDeleted`

Migration `20260727141026_EnforceCycleTransitions` was created with its Designer and updated ModelSnapshot. It backfills state and timestamps, resolves legacy duplicate active cycles deterministically without deleting rows, and creates:

- Filtered unique index `UX_Sprints_Project_Active` on `ProjectId` for non-deleted active rows.
- Ordering index `IX_Sprints_Project_State_Order`.

The migration is listed as **Pending** and was not applied to any production database. The existing `PersistStarredAndRecentlyViewedItems` migration was not modified.

Concurrency protection combines a serializable relational transaction, a SQL Server `sp_getapplock` resource scoped to the project, a state recheck inside the transaction, and the filtered unique index.

The established close contract controls unfinished tasks: `targetSprintId` moves them to that planned cycle; `null` moves them to backlog. Close preserves task rows, task status and assignments. Therefore `UNFINISHED_TASK_RULE_GAP` does not apply. No task is automatically marked completed or deleted.

## D. Changes

- Domain: centralized cycle state constants and persisted transition fields.
- API: kept existing endpoints; close now binds the cycle to `projectId`, uses the JWT `NameIdentifier` as audit actor, and returns the latest DTO.
- DTO: added `WorkspaceId`, `StartedAt`, and `CompletedAt`.
- Service: removed state mutation from GET, implemented validated/idempotent close and start transitions, deterministic reads, project-scoped rollover and relational transaction handling.
- Authorization: active project is now required in addition to existing active user and workspace/project membership checks.
- Database: added safe backfill and database-level single-active-cycle enforcement.
- Errors: business/state conflicts return HTTP 409 with stable `code` and `message`; wrong project/deleted entity returns 404; invalid request/scope returns 400; permission denial remains 403.

No frontend source, dependency manifest, unrelated feature, or existing migration was changed.

## E. Tests

New automated coverage includes:

- Valid close/start, persistence after a new DbContext, idempotent retries, no duplicate audit, and task/assignment retention.
- Planned/completed invalid transitions, active conflict, cross-project rejection, deleted-cycle exclusion and deterministic ordering.
- JWT actor usage and HTTP 409 mapping.
- Inactive/deleted user, inactive project, manager/member/outsider authorization through the existing policy tests.
- Database model assertion for the filtered unique active-cycle index.
- SQL Server LocalDB tests for concurrent closes, concurrent starts of two cycles, and close/start races. These verify a maximum of one active cycle, consistent persisted state, retained task status/assignment and one audit record.

Regression results:

- Backend restore: PASS.
- Backend build: PASS, 0 errors, 2 `NU1902` warnings for the existing AngleSharp 0.17.1 moderate-severity advisory.
- Backend tests: **166 passed, 0 failed, 0 skipped** (baseline was 157).
- Focused cycle/authorization/SQL Server tests: **35 passed, 0 failed**.
- EF migration list: PASS; `20260727141026_EnforceCycleTransitions` is pending.
- Frontend `npm ci`: PASS; 567 packages audited with 17 vulnerabilities (3 moderate, 14 high). No dependency fix was run.
- Frontend production build: PASS. Existing warnings concern deprecated packages, PWA assignment compatibility with Rolldown, large chunks, and plugin timing.
- Encoding scan found no cycle-scope mojibake to change. Existing unrelated matches were left untouched.
- `git diff --check`: PASS; only line-ending conversion warnings were emitted.

## F. Frontend handoff

`FRONTEND_HANDOFF_REQUIRED`

Endpoints and methods:

- List: `GET /api/projects/{projectId}/sprints`
- Start: `POST /api/projects/{projectId}/sprints/{cycleId}/start`; no request body
- Close: `POST /api/projects/{projectId}/sprints/{cycleId}/close`; body `{ "targetSprintId": null }` for backlog or `{ "targetSprintId": "<planned-cycle-guid>" }` for rollover

Successful start/close responses retain the project `ApiResponse<SprintResponseDto>` envelope. The DTO contains `id`, `projectId`, `workspaceId`, `name`, `startDate`, `endDate`, `status`, `state`, `startedAt`, `completedAt`, task metrics, `isFavorite`, and `createdAt`.

Response `state` values are:

- `Upcoming`
- `Active`
- `Completed`

Relevant transition error codes:

- `CYCLE_ALREADY_COMPLETED`
- `CYCLE_NOT_ACTIVE`
- `CYCLE_DATES_INVALID`
- `ACTIVE_CYCLE_EXISTS`
- `INVALID_TARGET_CYCLE`

HTTP behavior:

- 400: invalid request or rollover scope
- 401: missing/invalid backend identity for close
- 403: missing resource permission
- 404: cycle missing, deleted, or from another project
- 409: invalid state transition or active-cycle conflict

The Cycles UI should only show Start for `Upcoming`, only show Close for `Active`, never offer Start for `Completed`, reload the list after a successful transition, and display 409 business messages. No frontend changes were made in this task.

## G. Decision

**PASS**

The active cycle can be closed, the next planned cycle can be started, project-scoped uniqueness and permissions are enforced, concurrent transitions remain consistent, task/assignment data is retained, all backend tests pass, and the migration artifacts are complete without being applied to production.
