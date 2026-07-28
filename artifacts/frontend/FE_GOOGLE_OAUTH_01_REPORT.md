# FE-GOOGLE-OAUTH-01 Report

## A. Contract mapping

- Endpoint: `POST /api/auth/google-login`.
- Request body: `{ "credential": "<Google ID credential>" }`.
- The backend is the only identity verifier. The frontend does not send or trust Google email, name, avatar, subject, user ID, or access token.
- Success envelope: `{ statusCode, message, data }`.
- Session data used by the frontend: `data.accessToken`, `data.id`, `data.fullName`, `data.email`, `data.avatarUrl`, and `data.systemRoles`.
- The SprintA access token is stored through the existing `saveAuthSession` flow. The refresh token is issued by the backend as an HttpOnly cookie.
- Error mapping: `400` malformed/missing credential, `401` invalid or expired Google credential, `403` inactive/deleted/blocked account, `409` unsafe account-link conflict, and `503` unavailable or unconfigured provider.
- A `409` does not trigger email-based linking or fallback authentication.

## B. Previous flow

| Feature | Previous implementation |
| --- | --- |
| Component | `Frontend/src/views/Login.vue` |
| Google SDK/API | `vue3-google-login` registered globally |
| Callback value | `response.access_token || response.credential` |
| Popup mode | `popup-type="TOKEN"` |
| Backend request | Direct component request with `{ Credential: token }` |
| Session update | Existing `saveAuthSession(response.data.data)` |
| Defect | `OAUTH_FLOW_MISMATCH`: Google access token was preferred although the backend accepts only a Google ID credential |
| Required contract | GIS `response.credential` sent as `{ credential }` |

## C. Changes

- Added a shared Google Identity Services loader for `https://accounts.google.com/gsi/client`.
- The loader reuses a single global promise/state across component remounts and development hot reload, avoids duplicate script tags, initializes GIS once per client ID, and releases component callbacks on unmount.
- The Login view renders one official GIS button and consumes only `response.credential`.
- Added a focused auth API client that posts only `{ credential }` to `/auth/google-login`; it does not log, persist, decode, or retry the credential.
- Successful responses continue through the existing SprintA `saveAuthSession` and internal `/site-selection` redirect.
- Added safe user-facing handling for `400`, `401`, `403`, `409`, and `503`. Raw backend/provider details are not displayed.
- The `409` message states: "Tài khoản Google này chưa được liên kết an toàn với tài khoản hiện có."
- A request lock, abort controller, request sequence, and short-lived in-memory credential fingerprint prevent parallel requests, duplicate callbacks, stale response updates, duplicate success messages, and duplicate redirects.
- Email/password login is blocked only while Google verification is active; it otherwise retains its existing API and session behavior.
- Script loading, missing configuration, verifying, and retry states preserve access to email/password login.
- Removed the global `vue3-google-login` registration and token-popup usage. Package and dependency files were not changed.

## D. Configuration

- Frontend Google Client ID key: `VITE_GOOGLE_CLIENT_ID`.
- The Client ID is read from Vite configuration and is not hard-coded.
- When the key is absent or uses the documented placeholder, GIS is not loaded; the Google action reports that configuration is unavailable without crashing the page.
- No Google Client Secret is present or required in the frontend.
- A real development test additionally requires the frontend development origin to be registered as an authorized JavaScript origin for the configured Google OAuth client.

## E. Verification

- Git preflight: branch `agent/kimi-my-work`; starting HEAD `caf09652751f881152e6aa6b9dc0dc98144cee6b`; backend Google OAuth checkpoint is present in history; worktree was clean.
- Pre-change `npm ci`: PASS. It audited 567 packages and reported the existing 17 vulnerabilities (3 moderate, 14 high); no audit fix was run.
- Pre-change production build: PASS.
- Post-change `npm run build`: PASS, 4,529 modules transformed.
- Existing build warnings remain: PWA/Rolldown bundle assignment and chunks larger than 1,600 kB. They are outside this task.
- The project has no frontend test or lint script; no dependency or package file was changed.
- Credential scan: PASS. No Google access-token flow, credential logging, credential persistence, credential URL use, Client Secret, or hard-coded Google credential was found. Matches for `accessToken` are the valid SprintA backend session token.
- Static responsive review: the Google shell/button and error state are width-constrained, stack at the existing mobile breakpoint, and use existing light/dark CSS variables. Authenticated visual/runtime evidence was not available.
- `git diff --check`: PASS.
- `BROWSER_EVIDENCE_NOT_AVAILABLE`
- `RUNTIME_MIGRATION_NOT_APPLIED`
- `GOOGLE_OAUTH_CONFIGURATION_NOT_AVAILABLE`

## F. Deferred

- Apply `AddGoogleExternalLogins` only to an approved local/shared development database.
- Run authenticated browser E2E after a valid development Client ID, origin, backend, and migrated non-production database are available.
- A dedicated account-linking UI is outside this task.
- QA automation belongs to `FE-QA-BASELINE-01`.
- Other pending migrations are outside this task.

## G. Decision

`PASS`

The code/build scope passes: the frontend now sends only the GIS ID credential, uses the backend-issued SprintA session, maps the backend contract safely, prevents duplicate requests, preserves email/password login, and changes no backend, migration, dependency, or package files. Runtime Google OAuth is not claimed as passed because its required configuration and database migration are unavailable.
