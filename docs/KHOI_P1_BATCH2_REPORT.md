# KHOI P1 — BATCH 2 REPORT

Date: 2026-08-04
Branch: `KhoiSigma`
Scope: AI usage, credit contract, pricing source, encoding and quota

## Decision

PASS for P1 usage/credit scope.

`AI_CREDIT_PURCHASE_DEFERRED`

The repository has no user-subscription, order, checkout, payment-provider,
billing-cycle, refund or webhook design. No price and no payment flow was invented.

## Source of truth

- Included credit: `AiPricingPlans`, current entitlement code `free`.
- Consumed credit: reconciled from `AiUsageLedgerEntries` and `AITokenUsages`.
- Reconciliation uses the larger recorded result so incomplete historical ledger
  rows cannot hide token usage.
- Token conversion keeps the existing rule: ceiling of total tokens / 1,000.
- Operational provider guard remains `Gemini:MonthlyTokenQuota`.
- Credit quota guard reads the configured plan and reconciled monthly usage.
- Missing entitlement configuration is reported; it is not treated as an invented
  zero-credit commercial decision.

## Contract fixed

`GET /api/ai/usage-summary` now returns canonical fields:

- `planCode`
- `entitlementSource`
- `usageSource`
- `entitlementConfigured`
- `includedCredits`
- `usedCredits`
- `remainingCredits`
- `isQuotaExceeded`

Compatibility aliases remain:

- `creditsConsumed`
- `remainingIncludedCredits`

`GET /api/ai/usage` also exposes plan and credit usage alongside the existing
token quota fields.

## Pricing data

Migration `20260804140000_SeedAiCreditSourceOfTruth` conditionally seeds the
existing repository definitions:

- Free: 100 included AI credits.
- Team: price `null`, included credit pending.
- Business: price `null`, included credit pending.
- Six existing estimated credit rules.

All commercial prices remain `null` with `PendingConfirmation` status.
The migration uses conditional inserts and does not overwrite existing rows.

## UI and encoding

- Landing usage panel reads canonical `usedCredits`, `includedCredits` and
  `remainingCredits`.
- Authenticated AI page shows used/included/remaining credit from the same API.
- Fixed Vietnamese mojibake in AI page labels, errors and backend local fallback.
- Fixed corrupted middle-dot separators in AI backlog estimates.
- Quota copy no longer tells the user to buy/upgrade without a product decision.

## Tests

Focused P1 tests: 4/4 PASS.

Coverage:

- Canonical API contract and compatibility aliases.
- Plan-backed included credit.
- Ledger/token reconciliation.
- Configured-plan quota rejection.
- Missing-plan behavior does not invent entitlement.

Full backend: 294/294 PASS.

Backend build: PASS, 0 warnings, 0 errors.

Frontend production build: PASS.

Known non-blocking frontend build warnings remain for PWA/Rolldown plugin behavior
and large chunks; the build completed successfully.

## Runtime evidence

Backend: `http://localhost:5136`
Frontend: `http://localhost:5173`

- Backend DI/startup: PASS.
- Frontend localhost response: HTTP 200.
- Public pricing response: HTTP 200.
- Pricing source: database.
- Plan count: 3.
- Free entitlement present: true.
- Included free credits: 100.
- Paid prices all pending/null: true.
- Active credit rules: 6.
- Anonymous usage-summary: HTTP 401 as required.
- Migration applied and no pending model changes.

Runtime processes started for verification were stopped afterward.

## Migration

- Added and applied: `20260804140000_SeedAiCreditSourceOfTruth`.
- Pending model changes: none.

## Security

- No credential, token, secret, connection string or private config was printed
  into this report or added to the commit.
- `Backend/src/TaskManagement.API/appsettings.json` was preserved and excluded
  from staging.
- Usage endpoint remains authenticated.

## Deferred

`AI_CREDIT_PURCHASE_DEFERRED`

Requires product decisions for subscription ownership, official paid plan prices,
payment provider, billing cycle, refund policy and webhook design.

## Git

- No push.
- No stash pop/drop.
- Existing stashes remain intact.
- Local `appsettings.json` change remains unstaged.
