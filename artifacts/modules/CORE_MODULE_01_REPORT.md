# CORE-MODULE-01 Report

## A. Reproduction matrix

| Feature | Current implementation before fix | Failure | Expected / result |
| --- | --- | --- | --- |
| Module list | `GET /api/projects/{projectId}/modules` returns module metadata, `taskIds`, and counts | It does not return task DTOs for a detail view | Existing list contract remains unchanged |
| Module detail | No backend module-detail endpoint existed | No authoritative scoped task list was available | Added `GET /api/projects/{projectId}/modules/{moduleId}` |
| Frontend route | `ModulesTab.openModuleTaskView` routes to `SpaceSummary?tab=spreadsheet&moduleId=...` | The page loads all project tasks and filters locally | `FRONTEND_HANDOFF_REQUIRED`: use the new detail endpoint |
| Task-module mapping | `IssueModules` is an M:N join table | General task DTO exposes only the earliest linked `moduleId`; a task linked to multiple modules disappears from every other module filter | Detail query uses `IssueModules.Any(link => link.ModuleId == moduleId)` |
| Task count and summary | Module list computes separate aggregate values | A detail list and its summary did not share one authoritative scope | Counts and page data derive from the same permission-filtered query |
| Permission | Controller has JWT authorization and `ProjectAuthorize`; project task API applies task visibility | Client-side filtering starts from a broad project response and cannot be the module authorization boundary | Endpoint reads user ID only from JWT and verifies active user, workspace membership, project membership, resource policy, and task visibility |

Root cause: the data model correctly supports multiple modules per task, but the frontend module view filtered the broad project task response by a single projected `moduleId`. That projection intentionally selected only the first `IssueModules` row, so valid secondary module links were invisible.

## B. Data model

- Relationship: many-to-many `WorkTask` to `Module` through `IssueModules`.
- Scope: every Module belongs to one Project; WorkTask stores both Project and Workspace IDs. The detail query requires exact Module, Project, and Workspace scope.
- Unassigned task: represented by no `IssueModules` row.
- Constraints: composite primary key `(WorkTaskId, ModuleId)` prevents duplicate links; foreign keys reference `WorkTasks` and `Modules`.
- Index: `IX_IssueModules_ModuleId` supports lookup by Module.
- Soft-delete policy: tasks with `IsDeleted` or `IsArchived` are excluded; Modules with status `Disabled`, inactive/deleted Projects, and deleted Workspaces are not returned.
- Migration: none created. Existing schema, constraints, index, migration Designer files, and ModelSnapshot already represent the required relationship.
- Pre-existing pending migrations remain untouched: `PersistStarredAndRecentlyViewedItems` and `20260727141026_EnforceCycleTransitions`.

Add/remove audit:

- Module create/update writes `IssueModules`, validates task Project scope, de-duplicates requested IDs, and modifies only join rows.
- WorkTask create/update validates Module Project scope. The single-module task edit path replaces its join links but does not change assignment, Sprint, or Project fields.
- No add/remove code was changed because persistence was not the direct cause of the detail failure.

## C. Changes

Files:

- `Backend/src/TaskManagement.API/Controllers/ModulesController.cs`
- `Backend/src/TaskManagement.Application/DTOs/Module/ModuleDetailDto.cs`
- `Backend/src/TaskManagement.Application/Interfaces/IWorkTaskService.cs`
- `Backend/src/TaskManagement.Infrastructure/Services/WorkTaskService.cs`
- `Backend/TaskManagement.Tests/Logic/ModuleDetailTests.cs`
- `Backend/TaskManagement.Tests/Logic/ModuleDetailSqlServerTests.cs`

Endpoint:

- Method/route: `GET /api/projects/{projectId}/modules/{moduleId}`
- Query: `page` defaults to 1; `pageSize` defaults to 20 and is clamped to 1-100.
- Authorization: JWT `NameIdentifier`, controller `ProjectAuthorize` read policy, active user, active Workspace membership, active Project membership, and existing task visibility rules.
- Query: direct projection, exact join membership, exact Project/Workspace, no deleted/archived task, no broad `Include`, and no per-task metadata query.
- Ordering: `UpdatedAt DESC`, then task `Id ASC`.
- Summary: `taskCount`, completed, in-progress, overdue, and progress percentage use the same visible task set as pagination.
- Read-only behavior: no Module, task, assignment, Sprint, Project, or join relation is mutated.

## D. Tests

New focused tests: 7/7 PASS.

- Reproduces the M:N failure by linking a task to Module B first and Module A second.
- Excludes Module B tasks, unassigned tasks, soft-deleted/archived tasks, and a deliberately cross-Project/cross-Workspace join.
- Verifies member task visibility, outsider denial, inactive member denial, disabled Module behavior, inactive Project/deleted Workspace behavior, JWT identity, 401/404 responses, counts, summary, stable pagination, beyond-range pages, and reload persistence.
- Verifies assignment, Sprint, Project, and both Module links remain intact.

SQL Server relational evidence:

- Dedicated LocalDB database: `SprintAModule01Integration`.
- `EnsureCreated` only; no database drop and no production migration apply.
- Query/order behavior passed on SQL Server.
- Composite join key rejected a duplicate relation with `DbUpdateException`.
- Reload confirmed two Module links, active assignment, Sprint metadata, and unchanged Project relation.
- Cleanup deletes only test rows identified by generated GUIDs.

Regression:

- `dotnet restore`: PASS, with existing `NU1902` warning for AngleSharp 0.17.1.
- `dotnet build --no-restore`: PASS, 0 errors, 2 warnings.
- Full backend tests: PASS, 173 passed, 0 failed, 0 skipped.
- Frontend `npm ci`: PASS; 17 reported vulnerabilities (3 moderate, 14 high), unchanged because dependency work is out of scope.
- Frontend production build: PASS. Existing warnings remain for PWA/Rolldown bundle assignment and chunks above 1600 kB.
- `git diff --check`: PASS.
- Secret-pattern scan of changed files: no credential/token/API-key/password match.
- Module-scope encoding scan: no mojibake match.

## E. Frontend handoff

`FRONTEND_HANDOFF_REQUIRED`

- Call `GET /api/projects/{projectId}/modules/{moduleId}?page={page}&pageSize={pageSize}` when opening a Module.
- Do not fetch every Project task and filter by a single `task.moduleId`.
- Authentication remains the normal bearer JWT. Do not send a user ID in query or body.
- Success envelope: `{ statusCode, message, data }`.
- `data` contains Module metadata, `taskCount`, `completedCount`, `inProgressCount`, `overdueCount`, `progressPercent`, and `tasks`.
- `data.tasks` contains `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, and `hasNextPage`.
- Each task contains `id`, `sequenceId`, `title`, `statusName`, `priority`, `dueDate`, `projectId`, `projectName`, `moduleId`, `sprintId`, `sprintName`, `assignedUserId`, `assigneeName`, `assignees`, `parentTaskId`, and `updatedAt`.
- Navigation has stable `projectId`, `moduleId`, task `id`, and `parentTaskId`.
- Errors: 401 for missing/invalid JWT, 403 for inactive/missing membership or denied access, and 404 for missing/disabled/cross-Project Module.
- FE-MODULE-01 should replace the current broad project-task request/local Module filter. No frontend file was changed in this task.

## F. Decision

PASS
