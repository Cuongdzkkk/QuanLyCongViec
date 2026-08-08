# FE-MODULE-01 Report

## A. Contract mapping

- Endpoint: `GET /api/projects/{projectId}/modules/{moduleId}`.
- Route parameters: `projectId`, `moduleId`; both are URL-encoded by the shared client.
- Query parameters: `page` defaults to `1`; `pageSize` defaults to `20` and is clamped to `1..100`.
- Response envelope: `{ statusCode, message, data }`.
- Module data: `id`, `projectId`, `workspaceId`, `name`, `description`, `status`, `startDate`, `targetDate`, `leadId`, `leadName`, `taskCount`, `completedCount`, `inProgressCount`, `overdueCount`, `progressPercent`, `tasks`, `createdAt`, `updatedAt`.
- Task data: `id`, `sequenceId`, `title`, `statusName`, `priority`, `dueDate`, `projectId`, `projectName`, `moduleId`, `sprintId`, `sprintName`, `assignedUserId`, `assigneeName`, `assignees`, `parentTaskId`, `updatedAt`.
- Pagination: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`. The backend page is rendered directly without another client-side slice or ordering pass.
- Authorization/isolation: JWT is required; the backend applies workspace/project membership, task visibility, deleted/archived exclusion, and direct `IssueModules` membership.
- Errors: `400` invalid request, `401` invalid/missing authentication, `403` insufficient project access, `404` missing/disabled/cross-project module, and `500`/network failure.

## B. Previous behavior

- Module cards in `ModulesTab.vue` navigated to `SpaceSummary.vue` with `moduleId`.
- `SpaceSummary.vue` called the project-wide `useWorkTaskStore.fetchTasks()` endpoint and then inferred Module membership in the browser.
- The client-side membership check depended on singular/cached Module fields, so a task linked through the many-to-many `IssueModules` relation could be omitted.
- Counts and the visible list could therefore be based on incomplete or stale project task data. Pagination and ordering were client-side rather than the new scoped backend contract.
- Root cause: Module Detail had no dedicated API client/data path and reused the broad Project task collection.

## C. Changes

- Added `Frontend/src/api/moduleApi.js` with `getModuleDetail(projectId, moduleId, { page, pageSize, signal })`, envelope validation, pagination normalization, safe route encoding, and no mock/fallback path.
- `SpaceSummary.vue` now uses the scoped Module endpoint whenever `moduleId` is active. Project-wide task fetching remains only for non-Module views.
- Module name, description, status, task counts, progress, task items, task metadata, total count, pagination, and ordering now come from the backend response.
- Module pages render backend items directly. Page and page-size changes fetch the server; Project/Module changes reset to page 1. An out-of-range page is fetched again at the last valid backend page.
- Previous task data is cleared before each request. `AbortController`, request IDs, and a user/project/module/page/page-size context key prevent stale responses from replacing the current context.
- Added explicit loading, empty, error, and retry states. The empty text is `Module này chưa có công việc.` and is not used for `403`/`404` failures.
- Task rows use the real task ID as their stable key and open the existing Task Detail flow with the backend task object/ID. The compact Module projection is read-only in the spreadsheet.
- Module summary uses only backend values and safely bounds progress rendering without deriving totals from the current page.
- Existing Module create/edit flows persist `taskIds`; no backend add/remove contract was changed. Realtime task updates in Module context trigger a fresh scoped detail request instead of inserting a broad Project event into the list.

## D. Verification

- Pre-change `npm ci`: PASS; 566 packages installed. NPM reported 17 existing vulnerabilities (3 moderate, 14 high) and existing deprecation warnings. No audit fix was run.
- Pre-change `npm run build`: PASS.
- Post-change `npm run build`: PASS; 4,528 modules transformed and PWA assets generated.
- Existing build warnings remain: `vite-plugin-pwa` assigns to a bundle variable unsupported by Rolldown, and the `charts` chunk exceeds the configured 1,600 kB warning threshold.
- No frontend test script is defined in `package.json`; no dependency or package file was changed.
- Browser: `BROWSER_EVIDENCE_NOT_AVAILABLE`. The local app loaded without console errors, but the Module route redirected to Login because no authenticated MEMBER/OUTSIDER runtime session and fixture IDs were available. Authenticated E2E results are not claimed.
- Responsive/accessibility static review: the summary grid changes from four to two columns at 640px, the existing table remains horizontally scrollable, pagination retains fixed controls, retry is keyboard-focusable, and task rows have keyboard handling plus `aria-label`/`title`.
- Light/dark static review: new UI uses existing theme tokens and `color-mix`; no separate hard-coded light-only surface was introduced.
- Encoding: newly added Vietnamese strings are UTF-8 and were verified by exact-text search.
- `git diff --check`: PASS.
- Scope check: only `moduleApi.js`, `SpaceSummary.vue`, `SpreadsheetTab.vue`, and this report are included. No Backend, migration, package, lock, secret, or generated build file is included.

## E. Deferred

- Authenticated browser E2E with MEMBER/OUTSIDER, PROJECT_A/PROJECT_B, and MODULE_A/MODULE_B fixtures.
- `FE-QA-BASELINE-01`.
- Full add/remove/drag Module-relation E2E if a later task expands the current read-only Module Detail scope.
- Pending Star/Recent and Cycle migrations are outside FE-MODULE-01 and were not changed.

## F. Decision

**PASS**

The Module Detail read flow uses the real scoped endpoint, no longer fetches and filters the full Project task list, honors backend summary/order/pagination, protects context changes from stale responses, passes the production build, and contains no out-of-scope source changes.
