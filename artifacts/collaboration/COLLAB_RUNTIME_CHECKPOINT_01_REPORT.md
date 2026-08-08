# COLLAB-RUNTIME-CHECKPOINT-01

## A. Safe local database confirmation

- Retry date: 2026-07-29
- Branch: `KhoiSigma`
- Starting HEAD: `9a2bd1da`
- Starting worktree: clean
- `SAFE_LOCAL_DATABASE_CONFIRMED` by the project owner.
- Database type: SQL Server
- Server: `KHOI\SQLEXPRESS`
- Database: `TaskManagementDB`
- Authentication: Windows Integrated Security
- Host type: local
- Purpose: development/test
- Production/staging risk: none confirmed
- Only `ConnectionStrings.DefaultConnection` was read from the preserved stash.
- The connection string was passed through process environment only.
- No connection string, token, password, or other appsettings value was printed.
- The stash was not popped or modified.

## B. Backup result

- `sqlcmd` executable was present but its ODBC Driver 17 dependency was missing.
- SQL PowerShell `Invoke-Sqlcmd` fallback connected with Windows authentication.
- COPY_ONLY backup: PASS
- CHECKSUM: enabled
- `RESTORE VERIFYONLY WITH CHECKSUM`: PASS
- Backup file:
  `TaskManagementDB_before_runtime_checkpoint_20260729_201946.bak`
- Backup size: 16,867,840 bytes
- Existing backups were not overwritten.

## C. Migration before/after

- Pre-update EF migration list: PASS, 29 migrations, no pending marker.
- `dotnet ef database update`: PASS.
- Result: database already up to date; no migration needed application.
- Post-update EF migration list: PASS, no pending marker.
- Confirmed applied:
  - `20260726152821_PersistStarredAndRecentlyViewedItems`
  - `20260727141026_EnforceCycleTransitions`
  - `20260728053502_AddGoogleExternalLogins`
  - `20260728091647_AddCollaborationChannelText`
  - `20260728095916_AddCollaborationChannelDiscovery`
  - `20260728144925_AddDirectConversationPersistence`
- SignalR requires no separate schema migration.

## D. Backend tests/build

- Restore: PASS
- Initial build: PASS, 0 errors
- Initial full suite: 261 passed, 0 failed, 0 skipped
- Post-hotfix build: PASS, 0 errors
- Post-hotfix full suite: 261 passed, 0 failed, 0 skipped
- Post-hotfix SQL Server integration filter: 7 passed, 0 failed
- Known warning: `AngleSharp 0.17.1` moderate-severity advisory.
- No dependency or audit-fix action was taken.

## E. Frontend build

- `npm run build`: PASS
- Vite transformed 4,531 modules.
- Known warnings only: PWA/Rolldown bundle mutation, large chunks, and plugin
  timing.
- Package files were unchanged; `npm ci` and audit-fix were not run.

## F. Runtime startup

- Backend listen `127.0.0.1:5136`: PASS
- Frontend listen `127.0.0.1:5173`: PASS
- Backend root HTTP: 200 PASS
- Frontend root HTTP: 200 PASS
- Database connection: PASS through EF and authenticated runtime requests
- Protected API anonymous response: expected 401 PASS
- Anonymous SignalR negotiate: expected 401 PASS
- Authenticated SignalR negotiate: PASS, connection ID returned
- Advertised SignalR transports: 3
- Runtime processes were stopped after verification.

## G. Channel REST

Initial runtime reproduction:

- Channel create returned HTTP 500.
- Root cause: a user transaction was opened directly while
  `SqlServerRetryingExecutionStrategy` was enabled.
- No Channel was written by the failed request.

After hotfix:

- Channel create: PASS
- Real `channelId` GUID: PASS
- Three UTF-8 messages sent through REST: PASS
- Real unique `messageId` GUIDs: PASS
- Reload persistence: PASS
- Unicode Vietnamese persistence: PASS
- HTML/script payload persisted as content: PASS
- Pagination across page size 2: no missing or duplicate IDs
- Discovery reload includes the Channel: PASS
- USER_B read and USER_C denial: NOT VERIFIED

## H. Direct Message REST

Initial runtime reproduction:

- Find/create returned the same execution-strategy HTTP 500.

After hotfix:

- Shared participant discovery: PASS
- Find/create conversation: PASS
- Repeated find/create returns the same real `conversationId`: PASS
- Three messages sent: PASS
- Real unique `messageId` GUIDs: PASS
- Sender equals JWT user: PASS
- Conversation list and history reload persistence: PASS
- Unicode Vietnamese persistence: PASS
- Pagination: no missing or duplicate IDs
- USER_B reciprocal read and USER_C denial: NOT VERIFIED

## I. SignalR

- Real authenticated HubConnection start: PASS
- Join active Channel: PASS
- Channel REST send produced `ChannelMessageCreated`: PASS
- Channel payload matched `messageId` and `channelId`: PASS
- Sender event count for the message: exactly 1
- Leave Channel and join active DM: PASS
- DM REST send produced `DirectMessageCreated`: PASS
- DM payload matched `messageId` and `conversationId`: PASS
- Sender event count for the message: exactly 1
- Two-user delivery, outsider rejection, UI reconnect, old-group isolation,
  logout/login isolation, and browser-side REST dedup: NOT VERIFIED

## J. Starred

- Project star and reload persistence through REST: PASS
- Work Item star and reload persistence through REST: PASS
- Pagination sample returned unique entity keys: PASS
- Original Project and Work Item star state restored: PASS
- Dropdown states, keyboard focus, touch 390px, light/dark, and hover-independent
  unstar behavior: NOT VERIFIED because no controllable browser was available.

## K. Console/network

- Browser runtime inventory was empty; browser console evidence unavailable.
- Before hotfix: reproducible HTTP 500 on Channel create and DM find/create.
- After hotfix: tested Channel, DM, Starred, negotiate, and health paths completed
  without HTTP 500.
- Expected anonymous 401 responses were observed.
- One PowerShell request without explicit UTF-8 bytes returned 400; repeating the
  same Unicode payload with `application/json; charset=utf-8` passed and stored
  exact content.
- Duplicate event IDs were not observed in the one-user SignalR sample.

## L. Blockers

- `COLLAB_RUNTIME_FIXTURE_NOT_AVAILABLE`
- The local database has one known dev credential, not three independent
  authenticated USER_A/USER_B/USER_C profiles.
- Other real users exist and one shared participant was used for DM creation,
  but no safe credential was available to prove reciprocal or outsider access.
- No controllable in-app or Chrome browser was exposed by the browser runtime.
- Therefore two-browser delivery, outsider authorization, UI reconnect/dedup,
  XSS non-execution, and responsive/accessibility Starred checks remain open.

## M. Files changed

- `Backend/src/TaskManagement.Infrastructure/Services/CollaborationChannelService.cs`
- `Backend/src/TaskManagement.Infrastructure/Services/DirectConversationService.cs`
- `Backend/TaskManagement.Tests/Logic/CollaborationChannelSqlServerTests.cs`
- `Backend/TaskManagement.Tests/Logic/DirectConversationSqlServerTests.cs`
- `artifacts/collaboration/COLLAB_RUNTIME_CHECKPOINT_01_REPORT.md`
- No migration, appsettings, `useI18n.js`, package, AI, Payment, read/unread,
  attachment, mention, or voice/video file changed.

## N. Decision

`PARTIAL`

Migration, build, one-user persistence, authenticated negotiate, sender-side
Channel/DM SignalR events, and Starred REST persistence are proven. A real
runtime defect was repaired and regression-tested. PASS is not declared because
the required independent USER_B/USER_C credentials and browser profiles were
not available. Read/unread development must not begin from this checkpoint.
