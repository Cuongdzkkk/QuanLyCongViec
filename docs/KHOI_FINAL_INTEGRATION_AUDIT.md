# KHOI FINAL INTEGRATION AUDIT

Task: `FINAL-KHOISIGMA-MAIN-INTEGRATION-01`  
Date: 2026-08-06 (Asia/Saigon)  
Decision: **PARTIAL**

## A. Branch divergence

- Audited live Git state before integration.
- `main`: `fff6d47f`.
- `KhoiSigma`: `b18b58e5`.
- Pre-merge `main...KhoiSigma`: `2 / 41` commits.
- `KhoiSigma` is an ancestor of the integration result; all 41 KhoiSigma-only commits are preserved.
- Post-merge `main...integration/khoisigma-main-final`: `0 / 42` (41 feature commits plus the merge commit).
- ZIP/line-ending noise was not treated as source change.

## B. Merge result

- Created branch `integration/khoisigma-main-final` from `KhoiSigma` in a separate worktree.
- Merged `main` with a real merge commit and no conflict.
- Merge parents are `b18b58e5` and `fff6d47f`.
- No pull was performed in a dirty worktree. No reset, clean, push, stash pop, or stash drop was performed.
- No uncommitted Antigravity landing changes were present or imported. Landing redesign was not continued or modified by this integration.

## C. Main-only changes resolution

| Main-only area | Resolution |
| --- | --- |
| `Backend/Directory.Build.props` | Removed the global `NuGetAudit=false` override. Real NuGet audit is enabled and clean. |
| SQL helper scripts | Replaced personal machine names with `DEV_SQL_SERVER`; generic default is `localhost\SQLEXPRESS`. |
| Design-time DbContext factory | Uses `ConnectionStrings__DefaultConnection` first, then `DEV_SQL_SERVER` / `DEV_SQL_DATABASE`, then generic local defaults. |
| SQL integration tests/fixture | Removed personal server literals; tests use a shared environment-aware connection builder. Fixture still requires the configured server, `TaskManagementDB`, and Integrated Security. |
| `SpaceDashboard.vue` | Replaced nested full-page `DailyFocusView` with the existing compact `DailyFocusWidget`. The widget consumes the dashboard task list and does not issue a duplicate task request. |

Local SQL configuration:

- Full connection override: `ConnectionStrings__DefaultConnection`.
- Server/database override: `DEV_SQL_SERVER` and `DEV_SQL_DATABASE`.
- Documented generic development default: `localhost\SQLEXPRESS / TaskManagementDB`.
- Machine-specific values remain process/private configuration, not new tracked integration code.

## D. Features preserved from KhoiSigma

The merge preserves the implementation and test history for:

- Personal Work JWT scope.
- Starred/Recent persistence and isolation.
- Cycle transitions and concurrency guards.
- Module detail scope.
- Google authentication backend contract.
- Channel/DM REST and SQL persistence.
- SignalR group authorization, reconnect, account switch, and deduplication.
- Per-user read/unread state.
- Private attachments and authorized download.
- Internal-UserId mentions and notifications.
- Intakes permission matrix.
- Integration/Inbox user and project isolation.
- AI usage ledger, credit quota, and approved pricing plans.
- Collaboration USER_A / USER_B / USER_C runtime fixture and cleanup.

No existing KhoiSigma feature was rewritten during integration.

## E. Runtime/build/tests

| Check | Result |
| --- | --- |
| `dotnet restore Backend/TaskManagement.Tests/TaskManagement.Tests.csproj` | PASS |
| NuGet vulnerable package audit, including transitive packages | PASS, no vulnerable package reported |
| Backend build `--no-restore` | PASS, 0 warnings, 0 errors |
| Backend full tests `--no-build` | PASS, 294/294 |
| `npm ci` | PASS; package/lock files unchanged; 11 npm advisories remain (3 moderate, 8 high) |
| Frontend production build | PASS, 4,533 modules |
| EF migration list | PASS, 34 migrations |
| Pending migrations on confirmed local SQL Server Testing | PASS, 0 pending after applying 11 existing additive migrations |
| Pending model changes | PASS, none |
| Collaboration A/B/C runtime smoke | PASS, automatic run-scoped cleanup PASS |
| Browser login/session/logout/route guard | PASS |
| Integration Hub, AI usage/pricing, Intakes, Collaboration UI | PASS for available local/internal flows |
| SpaceDashboard + compact Daily Focus widget | PASS after reload |

Only the confirmed local Testing database was migrated. No production database was contacted or changed.

## F. Confirmed working features

- Local password login, session persistence after reload, logout, and protected-route redirect.
- P0 Intakes manager actions render and backend tests prove manager/member/outsider enforcement.
- Integration Hub reports disconnected/external OAuth state without fake success.
- AI usage rendered `0 / 100` credits for the local user.
- Approved Personal pricing rendered Free, Starter, Plus, and Pro values.
- Team pricing rendered Team at 499,000 VND and Enterprise as Contact.
- Channel/DM persistence, SignalR, read state, attachment, mention, isolation, reconnect, account switch, and cleanup passed the A/B/C fixture.
- Space Dashboard rendered project metrics and the compact Daily Focus widget without nested page layout or duplicate task API loading.

## G. Confirmed broken/partial features

| Gap | Evidence | Next task |
| --- | --- | --- |
| Draft to Task atomicity/idempotency | Frontend creates the task, then separately deletes the draft. A failure/retry can leave both or create duplicates. | `CORE-DRAFT-TO-TASK-ATOMIC-01` |
| Virtual Check-in | API failures are swallowed and data is written to localStorage/mock fixtures; AI summary also has synthetic fallback text. | `CORE-FE-CHECKIN-PERSISTENCE-01` |
| Activity Feed | `/auditlogs` failure replaces the result with named mock activities. | `FE-ACTIVITY-TRUTH-01` |
| Daily Focus postpone | Explicitly stored per-device/per-day in localStorage only. | `CORE-FE-DAILY-FOCUS-POSTPONE-01` |
| Frontend tests/CI | `package.json` has only dev/build/preview; workflow has no frontend test/lint job. | `FE-CI-TEST-BASELINE-01` |
| Payment | No approved provider, checkout/order/webhook/refund implementation. | `BILLING-PAYMENT-DESIGN-01` |
| Collaboration voice/video | UI remains simulated; no WebRTC/signaling/ICE/TURN flow. AI voice transcription is separate and does not satisfy calls. | `COLLAB-WEBRTC-01` |
| ZIP collaboration attachment | Raw ZIP is outside the collaboration allow-list/policy; archive scanning/extraction policy is absent. | `COLLAB-ZIP-ATTACHMENT-POLICY-01` |
| Vite dev cold start | Lazy imports can fail briefly during first optimization; reload recovers. Production build passes. | `FE-VITE-COLD-START-01` |

## H. External blockers

- Google real-account sign-in requires the matching frontend client ID and Google Console origin.
- Google Calendar, Gmail, Slack, and other provider sync require provider credentials, consent, scopes, and callback configuration.
- Payment requires product/provider/refund/webhook decisions.
- No credential or external console setting was invented or committed.

## I. Next task backlog

Recommended order:

1. `CORE-DRAFT-TO-TASK-ATOMIC-01`.
2. `CORE-FE-CHECKIN-PERSISTENCE-01`.
3. `FE-ACTIVITY-TRUTH-01`.
4. `FE-CI-TEST-BASELINE-01`.
5. `CORE-FE-DAILY-FOCUS-POSTPONE-01`.
6. `FE-VITE-COLD-START-01`.
7. External OAuth configuration and real-account smoke.
8. Payment design, ZIP policy, then voice/video as separate large scopes.

## J. Security/config

- NuGet audit is enabled; AngleSharp's former advisory is no longer reported.
- No secret, token, provider credential, or full private connection string was added to this report or commit.
- `Backend/src/TaskManagement.API/appsettings.json` was not edited or staged.
- The original `main` worktree's six SQL machine overrides remain `PRIVATE_LOCAL` and unstaged.
- All four pre-existing stashes remain intact.
- npm still reports 11 dependency advisories; no `npm audit fix` or dependency expansion was performed in this integration task.

## K. Worktree/commit

- Worktree: `C:\Users\phucl\OneDrive\Desktop\QLCV_2\QuanLyCongViec-integration-final`.
- Branch: `integration/khoisigma-main-final`.
- Commit type: merge commit with parents `b18b58e5` and `fff6d47f`.
- Commit message: `merge(project): integrate KhoiSigma features with latest main`.
- No push performed.

## L. Decision

**PARTIAL**

The integration itself is healthy: all KhoiSigma commits are preserved, main is merged, configuration regressions are resolved, NuGet audit/build/tests/frontend build/migrations/runtime fixtures pass, and the reviewed user flows work locally. The repository is not an overall PASS because Draft conversion, Check-in/Activity mock fallbacks, local-only postpone, frontend CI, external OAuth, payment, ZIP policy, voice/video, npm advisories, and the Vite cold-start race remain explicit follow-up work.
