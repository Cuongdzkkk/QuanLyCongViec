# AUTH-RELEASE-CHECKPOINT-01

- Date: 2026-08-04 (Asia/Saigon)
- Repository: `QuanLyCongViec`
- Branch: `KhoiSigma`
- Scope: authentication/release checkpoint only; no feature or dependency changes
- Decision: **PARTIAL**

## A. Git and repository state

- Starting HEAD: `e41f01a6`.
- Required commits `e41f01a6` and `62315460` are present.
- Starting worktree was clean; `git diff --check` passed.
- Existing stashes were left unchanged:
  - `stash@{0}`: `wip local appsettings and i18n before realtime`
  - `stash@{1}`: prior work
- No stash was applied, dropped, or rewritten.
- No pull, merge, rebase, dependency upgrade, or push was performed.
- Only this report is intended for the checkpoint commit.

## B. Migration/database

- `dotnet ef migrations list` built successfully against the configured local SQL Server.
- Pending migrations: **0**.
- Confirmed applied authentication/collaboration migrations include:
  - `20260728053502_AddGoogleExternalLogins`
  - `20260804062616_AddCollaborationReadState`
  - `20260804072953_AddCollaborationMessageAttachments`
  - `20260804080930_AddChannelMessageMentions`
- No migration update was necessary.

## C. Google OAuth configuration

- Backend configuration keys are `Google:Enabled` / `Google:ClientId`.
- Process-environment equivalents are `Google__Enabled` / `Google__ClientId`.
- Frontend configuration key is `VITE_GOOGLE_CLIENT_ID`.
- Runtime used process-only values recovered from the local stash; no tracked config or `.env` file was changed.
- The recovered client ID was non-empty and matched the Google web-client ID form; its value is intentionally omitted.
- `stash@{0}` keeps Google disabled in its appsettings copy, so that file should not be restored wholesale for release.
- Required local Authorized JavaScript Origin is exactly `http://localhost:5173`.
- This GIS button/popup flow does not use a client secret or redirect callback.

## D. Google OAuth runtime

- Backend and frontend started with Google enabled and the same process-only client ID.
- `/login` rendered the official Google Identity Services button without a visible client-ID warning.
- Clicking the GIS button did not expose or complete an account chooser in the Codex in-app browser.
- No real Google ID token was returned, so end-to-end success and persisted external-login creation were not proven.
- Google Console origin configuration could not be independently inspected in this checkpoint.
- Live negative probes passed:
  - empty credential -> HTTP 400
  - malformed/dummy credential -> HTTP 401
  - unauthenticated protected endpoint -> HTTP 401
- Automated validator/controller coverage passed for bad audience, issuer, expiry, unverified email, provider availability, conflict, inactive user, and sanitized logging.
- Result: **GOOGLE_OAUTH_CONFIGURATION_REQUIRED** for a final manual real-account run, not a code-test failure.

## E. Backend tests/build

- `dotnet restore`: PASS; projects already up to date.
- `dotnet build --no-restore`: PASS, 0 errors.
- `dotnet test --no-build`: PASS, **287/287**, 0 failed, 0 skipped.
- Three NU1902 warnings remain for `AngleSharp 0.17.1` and a moderate advisory.
- No package was changed because dependency upgrades are outside this task.

## F. Frontend build

- `npm run build`: PASS; 4,531 modules transformed.
- Existing PWA/Rolldown bundle-variable warning did not fail the build.
- No package or lockfile changes were made.

## G. Authentication regression

- Local development login completed and reached `/site-selection` and `/dashboard`.
- Reload preserved the authenticated dashboard session.
- Logout returned to `/login`.
- Browser console showed no authentication-flow errors during these checks.
- Protected API access without a session returned 401.
- Development login remains guarded by `IsDevelopment()` and returns 404 outside Development.
- Google linking policy remains explicit: provider subject first; an existing local account with the same email returns conflict rather than auto-linking.

## H. Personal Work / Starred / Cycle / Module

- The complete 287-test run passed Personal Work scoping and assigned/created/following/worked cases.
- Starred/recent persistence and authorization coverage passed.
- Cycle-transition coverage, including SQL-backed cases, passed.
- Module-detail coverage, including SQL-backed cases, passed.
- Authenticated dashboard routing rendered in the local browser.
- No product-data mutations outside run-scoped fixtures were introduced.

## I. Collaboration regression

- `local-collaboration-fixture.ps1 smoke -RunId auth-release-01 -Environment Testing`: PASS.
- Channel and DM unread counts, monotonic read cursor, persistence, pagination, and Unicode passed.
- SignalR delivery, private unread/read, reconnect, account switching, and user-C isolation passed.
- Mentions passed member discovery, SQL reload, private SignalR, duplicate/self/inactive policy, and isolation.
- Channel/DM attachments passed SQL metadata, private download, realtime, Unicode, path sanitization, validation, and user-C denial.
- Fixture cleanup completed automatically; no fixture data was intentionally retained.

## J. Vite cold start

- Three fresh frontend starts were observed on `/login`.
- Starts 1 and 3 rendered immediately with the GIS button.
- Start 2 briefly returned a blank frame during dev optimizer startup and self-recovered within two seconds.
- No error overlay or console error accompanied the transient frame.
- Production frontend build passed, so this is recorded as a dev cold-start observation rather than a release build failure.

## K. Stash review

- `stash@{0}` was inspected without printing the stored client ID or connection-string values.
- Its appsettings content contains local-only configuration and must remain uncommitted.
- Its `useI18n` change adds a language-dependent `tr(en, vi)` helper not present at HEAD.
- Recommendation for the i18n hunk: **RESTORE_LATER** only in a separate scoped task if still needed.
- Recommendation for the combined stash now: **KEEP_STASH**; do not restore wholesale.

## L. Security release check

- Tracked configuration was classified without disclosing values.
- Tracked SQL connection is local Integrated Security; tracked JWT key is a short placeholder, not a release secret.
- Tracked Google, IntegrationOAuth, Gemini, GitHub OAuth, and Resend credentials are empty.
- No tracked `private-uploads` or data-protection-key content was found.
- Only `Frontend/.env.example` matched the tracked environment-file risk scan.
- Collaboration attachment API output does not expose the private storage key.
- Private collaboration files are stored outside `wwwroot`; public static upload paths are limited to intended image categories.
- Non-development hosting requires explicit HTTPS CORS origins and rejects local/unencrypted SQL and invalid external JWT configuration.
- Google error responses are sanitized; credential-category logging tests passed and raw credentials are not logged.
- Remaining security maintenance item: triage and upgrade the flagged AngleSharp dependency in a dedicated task.

## M. GitHub divergence

- `origin` points to the expected GitHub repository.
- `origin/KhoiSigma` exists.
- Before this report commit, local `KhoiSigma` was ahead of `origin/KhoiSigma` by **30** commits and behind by 0.
- The checkpoint commit will make the branch ahead by **31** commits.
- A read-only fetch dry run reported updates on other remote branches only.
- The unpushed range contains the accumulated application, authentication, collaboration, test, and report work from `8caab721` through `e41f01a6`, plus this checkpoint report.
- The origin-range diff is large (204 files before this report), so the final push should be intentional and branch-specific.
- No push was performed.

## N. Remaining blockers

1. A real Google account must complete GIS sign-in from `http://localhost:5173` after confirming that exact Authorized JavaScript Origin in Google Cloud Console.
2. Verify the successful response creates/reuses the expected `ExternalLogin` record without exposing provider subject or token material.
3. The moderate AngleSharp advisory should be triaged before a production release; it does not invalidate this checkpoint's passing builds/tests.

## O. Recommended final actions

1. Keep the stash intact.
2. In Google Cloud Console, confirm `http://localhost:5173` on the same web client ID used by both processes.
3. Run one manual real-account login, reload, logout, and second login; verify the external-login row and sanitized logs.
4. Review the 31-commit ahead range, then push `KhoiSigma` explicitly if approved.
5. Schedule separate dependency and optional i18n-restoration tasks; do not mix them into this checkpoint.

## P. Decision

**PARTIAL**

All migrations, backend tests, production frontend build, local authentication, security/configuration checks, and the full collaboration smoke fixture are healthy. The repository is technically clean and ready for an intentional branch push after this report commit. Production authentication release readiness remains partial because the in-app browser could not complete a real Google account chooser/token exchange, and the Google Console origin was not independently verified. No real Google runtime PASS is claimed.
