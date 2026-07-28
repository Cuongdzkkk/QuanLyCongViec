# CORE-GOOGLE-OAUTH-01 Report

## A. Reproduction matrix

| Feature | Current/previous behavior | Finding |
| --- | --- | --- |
| Frontend component | `Login.vue` uses `GoogleLogin` with `popup-type="TOKEN"` | The callback prefers `response.access_token` over `response.credential`. |
| Frontend request | `POST /api/auth/google-login`, body `{ "Credential": token }` | The deployed frontend flow supplies an OAuth access token, not a Google ID token. |
| Backend endpoint | `AuthController.GoogleLogin` | Previously returned `400` for every exception and wrote the full exception to the console. |
| Backend validation | `AuthService.GoogleLoginAsync` | Previously tried ID-token validation, caught every failure, then accepted the same value through Google UserInfo. The fallback did not prove that the access token was issued for SprintA's Client ID. |
| Configuration | `Google:ClientId` existed as an empty tracked value | There was no provider enable flag or startup validation when Google sign-in was enabled. |
| User lookup | Email-only lookup | There was no persistent Google subject, and an existing password account was automatically reused by matching email. |
| JWT issuance | Inline copy of the normal token logic | It duplicated refresh-token/JWT issuance rather than consistently using the shared path. |
| Root cause | Mixed token types, broad exception fallback, and no external identity model | `OAUTH_FLOW_MISMATCH` and an account-linking/account-takeover risk were present. |

## B. OAuth flow

- Selected flow: **Google ID token credential only**.
- Endpoint: `POST /api/auth/google-login`.
- Request: `{ "credential": "<Google ID token>" }`; ASP.NET JSON property matching remains case-insensitive.
- `GoogleTokenVerifier` uses `GoogleJsonWebSignature.ValidateAsync` with the configured SprintA Client ID as the only accepted audience. This verifies the Google signature and standard token validity.
- `GoogleIdentityValidator` additionally requires a stable subject, allowed Google issuer, matching audience, non-expired token, verified and syntactically valid email, and bounded subject/email lengths.
- Identity fields used by account logic come only from the validated token claims. Extra frontend `email`, `userId`, name, or avatar fields are ignored by the request DTO.
- Authorization code exchange, redirect URI, and state are not part of this sign-in flow.
- The current frontend does not supply a nonce. The backend therefore does not claim nonce-based replay prevention. A valid ID token can be retried during its lifetime, but account resolution is idempotent and the raw Google token is not stored.
- Access-token/UserInfo fallback was removed because it did not satisfy the required audience guarantee.

## C. Account model

- Added `ExternalLogins`: `Id`, `UserId`, `Provider`, `ProviderSubject`, `ProviderEmail`, `CreatedAt`, and `LastLoginAt`.
- Unique indexes enforce `(Provider, ProviderSubject)` and `(UserId, Provider)`.
- Lookup order is Google provider subject first, then canonical email only when deciding whether creation is safe.
- Linking policy: an existing canonical email without a Google external login is **not auto-linked**. Active accounts return `409`; inactive/deleted accounts return `403`. An explicit authenticated linking flow is required later.
- Existing linked users are selected by provider subject. A changed/conflicting Google email cannot redirect the login to another internal account.
- Password hash, internal email, display name, roles, and workspace/project memberships are not overwritten for existing users.
- New users receive the existing default-role policy and an empty password hash. JWT/refresh tokens use the existing shared token method.
- Inactive/deleted linked users do not receive tokens and deleted email owners are not recreated.
- Relational creation uses a serializable transaction, EF execution strategy, unique constraints, and post-conflict lookup. Concurrent first login creates one user and one external login.
- Migration: `20260728053502_AddGoogleExternalLogins` with migration, Designer, and updated ModelSnapshot. It is pending and was not applied to the project database or production.

## D. Changes

- Authentication contract and errors: `AuthController.cs`, Google auth exceptions, and sanitized category logging.
- Validation abstraction: `IGoogleIdentityValidator`, `IGoogleTokenVerifier`, `GoogleIdentityValidator`, `GoogleTokenVerifier`, and validated claim/identity records.
- Account resolution: `AuthService.GoogleLoginAsync` now uses provider subject, safe creation/conflict policy, shared JWT issuance, and concurrency recovery.
- Persistence: `ExternalLogin`, `User.ExternalLogins`, DbContext mappings/indexes, migration, Designer, and snapshot.
- Configuration: `Google:Enabled` defaults to `false`; tracked `Google:ClientId` remains empty. Startup requires a non-placeholder Client ID only when the provider is enabled.
- Registration: verifier/validator services are registered in dependency injection.
- No frontend, package, dependency, Chat, AI, Payment, or unrelated migration source was changed.

## E. Tests

- Added 35 Google authentication tests.
- Validator tests cover configured audience propagation, wrong audience, wrong issuer, expiry, invalid signature/provider result, unverified email, invalid email format, disabled provider, and provider network failure.
- Service tests cover new account creation, repeat login, stable subject mapping, fake frontend identity fields, password/role/membership preservation, email-link conflict, inactive/deleted users, JWT response shape, and empty credential.
- Controller tests cover `400`, `401`, `403`, `409`, `503`, sanitized response bodies, HttpOnly refresh-token response behavior, and absence of credential data in logs.
- SQL Server relational test: two concurrent first logins returned the same user and left exactly one user/external-login record; test records were cleaned without dropping the database.
- Focused Google tests: `35/35 PASS`.
- Full backend tests: `208/208 PASS`, `0 failed`, `0 skipped`.
- Backend restore/build: PASS, `0 errors`; existing `NU1902` warning remains for `AngleSharp 0.17.1`.
- Frontend `npm ci`: PASS; existing 17 vulnerabilities (3 moderate, 14 high), no audit fix.
- Frontend production build: PASS; existing PWA/Rolldown and large-chunk warnings remain.
- Migration list: Cycle and Google external-login migrations are pending; no migration was applied.
- Encoding scan: no new Google-auth mojibake. Existing findings outside Google Auth scope were left unchanged.
- Secret scan: no usable Google credential, token, Client ID, Client Secret, API key, or password was added. Tracked Google configuration remains disabled/empty.

## F. Frontend handoff

`FRONTEND_HANDOFF_REQUIRED`

- Endpoint: `POST /api/auth/google-login`.
- Request body: `{ "credential": "<Google ID token>" }`.
- Required frontend change: stop using `popup-type="TOKEN"`/`response.access_token`; obtain and send `response.credential` from the Google Identity Services ID-token flow.
- Response: `{ statusCode, message, data }`; `data` contains `accessToken`, `id`, `fullName`, `email`, `avatarUrl`, and `systemRoles`. Refresh token remains an HttpOnly cookie.
- Errors: `400` missing/malformed request, `401` invalid/expired/audience/issuer/signature/email-verification failure, `403` inactive/deleted account, `409` existing email requires explicit linking, `503` disabled/unconfigured/unavailable Google provider, and sanitized `500` for unexpected failures.
- Session storage and post-login redirect can keep the existing `data` handling. No callback route is required for this ID-token flow.

## G. Security decisions

- Email linking: no automatic linking to an existing account, even with a verified Google email. This avoids account takeover and preserves password accounts.
- A future explicit linking action must require an authenticated internal session and fresh Google verification.
- Provider identity is `Provider = Google` plus the validated Google subject; email is not the external primary key.
- Token logging/storage: raw Google credentials are neither logged nor persisted. Logs contain only failure category or resolved internal user ID.
- Secret storage: `Google:ClientId` must come from environment variables, User Secrets, or the deployment secret/configuration provider. No Client Secret is required for ID-token validation.
- Replay limit: no nonce exists in the current frontend contract. Token expiry and idempotent subject resolution are enforced, but nonce-based replay defense is deferred to the frontend handoff.
- No `SECURITY_DECISION_REQUIRED` remains for the backend policy selected here.

## H. Decision

**PASS**

Backend Google identity verification, safe subject persistence, conflict policy, account-state enforcement, concurrency protection, shared JWT/session issuance, migration integrity, and full regression all pass. The current frontend token type is intentionally rejected until `FE-GOOGLE-OAUTH-01` completes the documented ID-token handoff.
