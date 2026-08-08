# KHOI P2 Rework Report

Date: 2026-08-04  |  Branch: `KhoiSigma`

## A. Scope and safety

- P2 checkpoint preserved at `a73e595c`; base was `0cbdaf76`.
- `Backend/src/TaskManagement.API/appsettings.json` was kept untouched and never staged.
- No pull, reset, clean, stash pop/drop, push, or OAuth/payment configuration was performed.

## B. Scan → Diagnose → Fix

- Scan: Vue 3 + scoped CSS, existing local assets, existing scroll/reveal observers, no new dependency.
- Diagnose: the hero's depth was mostly hover-only; Product Suite was a uniform six-card grid; AI scene lacked a visible Z-plane; pricing was a generic three-column presentation and the API model had no audience/recommendation/publication metadata.
- Fix: retained the original reveal/scroll/aurora/ticker/parallax motion and strengthened the static 3D baseline, bento layout, AI context path, and server-backed pricing switcher.
- Taste audit followed the existing-project sequence and avoided a rewrite: typography, hierarchy, contrast, depth, responsive layout, focus states, reduced motion, and overflow were checked against the [Taste Skill redesign guidance](https://github.com/Leonxlnx/taste-skill/blob/main/skills/redesign-skill/SKILL.md).

## C. Landing visual result

- Hero dashboard now has visible perspective, rotateX/rotateY, translateZ depth, floating rails, and scroll-depth parallax before pointer input.
- Pointer tilt remains bounded and resets on leave; touch retains a static 3D composition.
- Product Suite uses an asymmetric bento grid rather than six equal cards.
- AI scene includes context/command/confirm chips and a depth path; motion is CSS transform/opacity only.
- Product video frame has a visible 3D baseline and bounded pointer tilt; play still loads the real YouTube embed.
- Pricing has Personal/Team modes, database recommendation state, contact handling, and no invented extra-credit price.
- Existing Be Vietnam Pro project font is used; no new font/dependency was added.

## D. Approved pricing source of truth

| Plan | Monthly VND | Included credits | Audience |
|---|---:|---:|---|
| Free | 0 | 100 | Personal |
| Starter | 49,000 | 500 | Personal |
| Plus | 99,000 | 1,200 | Personal |
| Pro | 199,000 | 3,000 | Personal |
| Team | 499,000 | 9,000 shared | Team |
| Enterprise | Contact | By agreement | Team |

- Added `Audience`, `IsRecommended`, and `IsPublished` to the pricing contract.
- Added additive migration `20260804160000_ApplyApprovedMvpAiPricing`; the already-applied P1 migration was restored and not modified.
- Local SQL Server migration was applied successfully; production was not touched.
- Extra credit purchase pricing/payment remains `AI_CREDIT_PURCHASE_DEFERRED` / `NOT_DECIDED`.

## E. Runtime/API evidence

- `GET http://localhost:5136/api/public/pricing`: HTTP 200, `source=database`, six published plans, approved values above.
- `GET /api/ai/usage-summary` anonymous: auth-protected as expected; no fake usage was displayed.
- Frontend served at `http://localhost:5173/`; the active Vite process rendered the landing successfully.
- Browser runtime exception capture: no `Runtime.exceptionThrown` or `Log.entryAdded` errors after reload.
- Mobile DOM check: `scrollWidth=390`, `clientWidth=390`; no horizontal overflow.
- Pointer check: video tilt variables changed within the bounded handler; Team switch rendered 499,000 VND and Enterprise Contact.

## F. Browser screenshots

Captured with Chrome headless/CDP after runtime load:

- Desktop 1440×900: `C:\Users\phucl\.codex\p2-rework-evidence\landing-desktop-1440x900.png`
- Tablet 768×1024: `C:\Users\phucl\.codex\p2-rework-evidence\landing-tablet-768x1024.png`
- Mobile 390×844: `C:\Users\phucl\.codex\p2-rework-evidence\landing-mobile-390x844.png`
- Reduced motion 1440×900: `C:\Users\phucl\.codex\p2-rework-evidence\landing-reduced-motion-1440x900.png`
- Desktop visual inspection shows visible static 3D depth, hero rails, dashboard perspective, and no blank/error state.

## G. Accessibility/performance

- Added a keyboard-visible skip link to `#main-content`.
- Existing semantic `header/nav/main/section/footer`, labels, alt text, focus-visible outlines, and reduced-motion media rules retained.
- No raw HTML or `v-html`; no new package; no broad bundle change attributable to this rework.

## H. Verification

- `dotnet build Backend/TaskManagement.slnx --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet test Backend/TaskManagement.Tests/TaskManagement.Tests.csproj --no-build`: PASS, 294/294.
- `npm run build` in `Frontend`: PASS. Existing PWA/Rolldown advisory and large-chunk advisory remain non-blocking.
- `dotnet ef database update`: PASS; additive pricing migration applied.
- `git diff --check`: PASS.

## I. Commits and decision

- Pricing commit: `feat(pricing): apply approved MVP AI credit plans`.
- Landing commit: `feat(landing): restore motion and deliver visible 3d redesign`.
- No push performed.
- Decision: `PASS` for P2 rework; external OAuth and payment remain deferred blockers outside this scope.
