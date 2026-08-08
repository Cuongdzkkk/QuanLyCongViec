# PROJECT-LOCAL-COLLAB-E2E-FIXTURE-01

## A. Recovered state

- Branch: `KhoiSigma`.
- Starting HEAD: `d163559e` (`docs(project): record runtime status audit`).
- No commit after `d163559e` existed when recovery began.
- The only worktree change was an untracked `tools/LocalCollaborationFixture` directory.
- No staged changes existed.
- `stash@{0}` remained intact and listed only local `appsettings.json` and `useI18n.js` changes.
- Repository backend and Vite processes were listening on 5136 and 5173; their command lines were verified before they were stopped for cold-start/build checks.

## B. Work already completed

- The interrupted session had implemented the deterministic identities, SQL store, in-process API host, REST/SignalR runtime smoke, entry point, and README.
- That implementation was reviewed and retained rather than rewritten.
- Missing at recovery: automated fixture guard tests, repository wrapper script, final report, live matrix evidence, cold-start evidence, and commit.

## C. Fixture architecture

- Explicit console runner: `tools/LocalCollaborationFixture`.
- Commands: `provision`, `smoke`, and `cleanup`.
- Every entity ID is derived deterministically from `e2e-collab-<runId>`.
- An in-process `WebApplicationFactory` hosts the real API with SQL Server and real SignalR handlers.
- Fresh authenticated clients use separate, process-local JWTs for USER_A, USER_B, and USER_C.
- The fixture is never registered in normal backend startup.
- PowerShell entry point: `scripts/local-collaboration-fixture.ps1`.

## D. Safety guards

- Only `Development` and `Testing` are accepted.
- The runner refuses any target other than local `KHOI\SQLEXPRESS`, `TaskManagementDB`, and Windows Integrated Security.
- The connection string is accepted only through a named environment variable, never a command-line value.
- JWT signing material and password hashes are random and process-local.
- No credential, token, password, signing key, or connection secret is printed or stored in this report.
- Cleanup verifies prefix ownership before deletion and scopes every delete to deterministic fixture IDs.
- Guard and identity behavior is covered in `Backend/TaskManagement.Tests/RuntimeFixtures`.

## E. USER_A/B/C matrix

| Identity | Workspace | Project | Channel | DM AB |
| --- | --- | --- | --- | --- |
| USER_A | owner | project manager | active/send | participant |
| USER_B | member | developer | active/send | participant |
| USER_C | none | none | forbidden | forbidden |

- Live run ID `resume01` passed the exact membership assertion.
- Live run ID `resume02` passed two consecutive provisions without duplicates.

## F. Channel REST

- PASS: USER_A discovered the fixture channel and sent messages.
- PASS: sender identity came from USER_A's JWT.
- PASS: a fresh USER_B client read persisted messages.
- PASS: USER_C read and send returned the non-disclosing forbidden/not-found response.
- PASS: two pages contained every message ID exactly once.
- PASS: Vietnamese Unicode content round-tripped unchanged.

## G. DM REST

- PASS: USER_A found/created the USER_B conversation.
- PASS: reverse lookup by USER_B returned the same conversation ID.
- PASS: repeated lookup did not create a duplicate conversation.
- PASS: USER_A sent, a fresh USER_B client read persisted data, and sender came from JWT.
- PASS: USER_C read and send were denied.

## H. SignalR

- PASS: USER_A, USER_B, and USER_C connected with distinct identities.
- PASS: USER_A and USER_B joined Channel and DM groups.
- PASS: USER_C was denied both joins.
- PASS: USER_A and USER_B each received exactly one matching event.
- PASS: USER_C received no protected event; Channel and DM groups did not cross-leak.

## I. Reconnect/account switch

- PASS: reconnect did not retain the old Channel group.
- PASS: explicit Channel rejoin restored delivery.
- PASS: reconnect plus explicit DM rejoin restored DM delivery without restoring Channel membership.
- PASS: stopping USER_A, switching its token slot to USER_C, and reconnecting did not inherit USER_A groups.

## J. Vite cold start

- Three bounded forced cold starts were executed with Vite 8.0.8.
- Starts 1 and 2 rendered the landing page with meaningful interactive content and no captured console errors.
- Start 3 directly loaded lazy route `/login`; the page ultimately rendered, but Vite logged dependency re-optimization, reload, and a transient failure to fetch `Login.vue`.
- Production build passed, so this is not a production bundle failure.
- `KNOWN_DEV_COLD_START_RACE`: reproducible on the targeted lazy route but not uniformly observed on landing-page starts. No speculative frontend fix was made.

## K. Cleanup

- PASS: `smoke` cleaned `resume01` automatically in `finally`.
- PASS: two further `cleanup resume01` invocations both succeeded.
- PASS: `resume02` was removed after the double-provision check.
- Cleanup deletes only deterministic run-scoped roots and their fixture dependencies.
- Existing user data was not selected by prefix ownership or deterministic fixture IDs.

## L. Tests/build

- `dotnet build Backend/TaskManagement.Tests/TaskManagement.Tests.csproj`: PASS.
- `dotnet test Backend/TaskManagement.Tests/TaskManagement.Tests.csproj --no-build`: PASS, 272/272.
- `npm run build`: PASS, Vite production build completed.
- Existing warnings remain: AngleSharp NU1902, PWA/Rolldown bundle-assignment warning, and large frontend chunks.
- Live fixture smoke: PASS for REST, persistence, SignalR, reconnect, account switch, and cleanup.

## M. Files and commit

- Added `tools/LocalCollaborationFixture/*` (recovered implementation plus test visibility/README completion).
- Added `Backend/TaskManagement.Tests/RuntimeFixtures/LocalCollaborationFixtureTests.cs`.
- Updated `Backend/TaskManagement.Tests/TaskManagement.Tests.csproj` with the fixture project reference.
- Added `scripts/local-collaboration-fixture.ps1`.
- Added this report.
- Commit message: `test(collaboration): add local multi-user runtime harness`.

## N. Remaining limitations

- `KNOWN_DEV_COLD_START_RACE` remains a development-only Vite optimizer race.
- The local safety policy intentionally binds this runner to the approved machine/database target.
- No application production code or frontend source was changed.

## O. Decision

`PASS`

The project-local A/B/C fixture and runtime harness are complete, guarded, repeatable, live-verified, cleanup-safe, and ready to commit. The known Vite cold-start race is explicitly documented as required and does not invalidate the production build or collaboration runtime results.
