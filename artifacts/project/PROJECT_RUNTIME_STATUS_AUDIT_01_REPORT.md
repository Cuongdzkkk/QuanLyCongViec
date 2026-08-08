# PROJECT-RUNTIME-STATUS-AUDIT-01

- Audit date: 2026-08-04 (Asia/Saigon)
- Repository: `QuanLyCongViec`
- Scope: audit/verification only; no feature implementation, migration update, stash mutation, secret edit, or push.
- Decision: **PARTIAL**

## A. Git status and commit inventory

- Branch: `KhoiSigma` (expected).
- Initial worktree: clean (`git status --short` returned no entries).
- Initial HEAD: `92ce40f20676795a9481c892247cc0ced17e55d8`.
- Expected commits are present in the latest 20 commits:
  - `53495f8b` — direct-conversation persistence.
  - `7b157459` — authenticated realtime messaging.
  - `093d6fda` — starred/recent visual regressions.
  - `83f38967` — frontend realtime collaboration messaging.
  - `9a2bd1da` — collaboration runtime checkpoint.
- Current HEAD also contains `92ce40f2` — runtime integration repairs.
- No pull, reset, clean, stash pop/drop, broad add, or push was run.

## B. Stash/local configuration

- `stash@{0}`: `wip: local appsettings and i18n before realtime`.
- `stash@{1}` remains untouched.
- `stash@{0}` contains exactly the expected scoped files:
  - `Backend/src/TaskManagement.API/appsettings.json`
  - `Frontend/src/composables/useI18n.js`
- The connection string was read only from the approved stashed file and used only as a process environment override.
- Sanitized connection facts: Server `KHOI\SQLEXPRESS`; Database `TaskManagementDB`; Integrated Security `True`.
- The full connection string and secrets were not printed, written, committed, or persisted with `setx`.

## C. Database and migration status

- `dotnet ef migrations list` built successfully and connected to the approved database.
- Source migrations: 29.
- Applied migrations reported by EF: 29.
- Pending migrations: 0 (no migration was marked `(Pending)`).
- Relevant applied migrations:
  - `20260726152821_PersistStarredAndRecentlyViewedItems`
  - `20260727141026_EnforceCycleTransitions`
  - `20260728053502_AddGoogleExternalLogins`
  - `20260728091647_AddCollaborationChannelText`
  - `20260728095916_AddCollaborationChannelDiscovery`
  - `20260728144925_AddDirectConversationPersistence`
- Because nothing was pending, no backup and no `database update` were run.
- `sqlcmd` could not independently query `__EFMigrationsHistory` because the installed client could not find its ODBC driver; EF's successful live listing is the migration authority used here.

## D. Backend build/tests

- Restore: PASS; all projects up to date.
- Build: PASS; 0 errors, 2 warning occurrences in the build summary.
- Tests: PASS — 261 passed, 0 failed, 0 skipped, 261 total (56 seconds).
- Known warning: `NU1902`, AngleSharp `0.17.1`, moderate-severity advisory `GHSA-pgww-w46g-26qg`.
- Relevant passing coverage includes starred isolation/persistence/pagination, channel discovery/access/pagination, DM reverse find-or-create/outsider isolation, and authenticated SignalR group isolation/reconnect.

## E. Frontend build

- `node_modules` existed, so `npm ci` was correctly skipped.
- `npm run build`: PASS; Vite transformed 4,531 modules and generated the PWA bundle.
- Existing warnings:
  - `vite-plugin-pwa` assigns to the bundle variable, unsupported by Rolldown and ignored.
  - `charts` chunk is larger than the configured 1,600 kB threshold.
  - Plugin timing warning, dominated by `vite-plugin-pwa`.
- No frontend build error occurred.

## F. Runtime startup

- Backend: PASS at `http://localhost:5136`, Development environment.
- Frontend: PASS at `http://127.0.0.1:5173` using the existing dev configuration.
- Database connection: PASS through authenticated queries, workspace/project/channel/history reads, and persisted writes.
- Authentication: unauthenticated context returned 401; predefined Development login succeeded; authenticated context returned 200.
- `/hubs/chat/negotiate`: unauthenticated 401, authenticated 200.
- Backend runtime logs: 0 invalid-object, invalid-column, schema/migration, SQL exception, or unhandled-exception matches.
- No new endpoint 404 was observed.

## G. Starred result

- Project star -> reload with a new authenticated session -> still present: PASS.
- Project unstar -> removed: PASS.
- WorkTask star -> reload with a new authenticated session -> still present: PASS.
- WorkTask unstar -> removed: PASS.
- Starred desktop view loaded in light and dark modes without console errors.
- At 390 px the page had `scrollWidth == clientWidth == 390`; no horizontal overflow was detected.
- Type filter is a native accessible combobox and programmatic selection worked; an actual touch gesture and a reliable keyboard-selection change could not be proven with the available browser-control surface.
- No starred dropdown misalignment was observed in desktop or 390 px layout.

## H. Channel REST result

- Discovery for the selected real project: PASS (200, stable page boundaries, no duplicate IDs).
- Existing channel opened: PASS.
- Message send: PASS (201).
- Reload with a new authenticated session retained the message: PASS.
- Page-size-1 history check did not lose or duplicate IDs: PASS.
- Browser channel history showed the newly persisted message once.
- USER_B read and USER_C blocked were not executed against the live runtime because distinct usable credentials are unavailable; corresponding automated tests passed.

## I. Direct Message result

- USER_A find/create with an existing active participant: PASS.
- Repeating find/create returned the same `conversationId`: PASS; no duplicate conversation.
- Send and history: PASS (201 and persisted history).
- Reload with a new authenticated session retained the message: PASS.
- Browser DM list/history showed the new conversation and message.
- Reverse lookup as USER_B and read denial as USER_C were not executable live without distinct credentials; corresponding automated tests passed.

## J. SignalR result

- Runtime chat connection and group join logged successfully; five connection and five group-join events were observed during navigation/reconnect checks.
- External REST send while the channel UI was open appeared immediately exactly once: PASS.
- External REST send while the DM UI was open appeared immediately exactly once: PASS.
- After navigating/reconnecting from Channel to DM, a new Channel event did not appear in the DM UI: PASS (no old-group leakage observed).
- Missing-token negotiate was rejected and authenticated negotiate succeeded.
- Full two-user simultaneous receive, USER_C forbidden join, and logout/login-as-another-user isolation were not live-tested because the required multi-user fixture is incomplete.
- Automated tests covering authorized channel/DM groups, outsider isolation, inactive-user rejection, and reconnect rejoin behavior all passed within the 261-test run.

## K. Google OAuth result

- `GOOGLE_OAUTH_RUNTIME_CONFIGURATION_NOT_AVAILABLE`
- A backend Client ID was present in local configuration, but no frontend runtime `VITE_GOOGLE_CLIENT_ID` was configured and no complete local Google runtime setup was available.
- No Client ID or secret was printed.

## L. Console/network errors

- On the first cold Vite navigation, dependency optimization triggered transient lazy-import failures for `Login.vue` and `NexusLayoutWrapper.vue` while Element Plus dependencies were rebundled.
- After optimization settled, direct reload/navigation rendered login, dashboard, starred, Channel, and DM views with no new console warnings/errors.
- This is a cold-start dev-server race worth tracking, not a production-build failure.
- Expected 401 responses were observed for unauthenticated context and SignalR negotiate.
- No schema-related server error or new runtime 404 was observed.

## M. Functions completed

- Source/database migrations for Starred/Recent, cycle transitions, Google external login, Channel text/discovery, and direct conversations are present and applied.
- Backend compiles and all 261 tests pass.
- Frontend production build completes.
- Database-backed Starred Project/WorkTask mutations persist across sessions.
- Channel discovery, message persistence, history, and pagination work for the authenticated fixture.
- DM find-or-create, deduplication, send, and persistence work for the authenticated fixture.
- Authenticated SignalR negotiate, connect, group join, live Channel delivery, live DM delivery, and tested group switching work.

## N. Functions not fully completed/verified

- Live USER_A/USER_B/USER_C authorization matrix is not verified end-to-end.
- Two-browser/two-user simultaneous Channel and DM delivery is not verified against the real local database.
- USER_B reverse DM lookup and USER_C REST/SignalR denial are covered by tests but not by live credentialed sessions.
- Logout/login as a different user and stale-connection isolation is covered by tests but not live-tested.
- Real touch interaction and keyboard selection on the Starred native filter are not fully proven.
- Google OAuth cannot be exercised without complete frontend/runtime configuration.
- Cold Vite startup may briefly fail lazy routes during first dependency optimization.

## O. Blockers

- The local database exposes one usable authenticated Development account for this audit. Existing seeded people do not have audit-safe usable credentials.
- There is no public API to add a second member directly to an existing collaboration channel; project/workspace membership alone is insufficient for the full Channel A matrix.
- Minimal safe local fixture procedure:
  1. Create three local-only accounts A/B/C through the normal OTP registration flow, storing passwords outside source control.
  2. Create or select a local workspace/project as A; add A and B as active workspace/project members and keep C outside the project.
  3. Create Channel A as A and add B through a temporary local-only seed utility/fixture transaction that inserts `CollaborationChannelMember`; do not use a migration and do not commit credentials.
  4. Create/find DM AB through the authenticated API; do not add C as a participant.
  5. Run two isolated browser sessions for A/B plus one for C, then remove the local fixture through the same scoped utility if desired.

## P. Proposed next task

- `PROJECT-LOCAL-COLLAB-E2E-FIXTURE-01`: add a repeatable, local-only, non-migration fixture runner and an automated three-user runtime smoke harness for Channel/DM REST and SignalR authorization, reconnect, and account-switch isolation.
- Include a cold-start Vite check so the dependency-optimizer lazy-route race is either prevented or consistently documented.
- This task is proposed only; it was not assigned or started.

## Q. Decision

**PARTIAL**

Core builds, migrations, database persistence, authenticated runtime startup, single-session REST flows, and live SignalR delivery are healthy. The audit cannot be `PASS` until the real local runtime is exercised with distinct USER_A/USER_B/USER_C credentials and Channel membership, including forbidden-user and account-switch cases. It is not `BLOCKED` because substantial runtime verification completed and the remaining gap has a concrete local-fixture path.

Audit-side local data created during the requested smoke tests consists only of messages/conversation activity with `audit-01-*` markers and Development login refresh-token activity; no secrets were committed.
