# KIMI TASK — FRONTEND STABILIZATION AFTER RECONCILIATION

Owner: KIMI
Layer: Frontend only

Read:
- AGENTS.md
- Frontend/AGENTS.md
- SPRINTA_LOCAL_VS_GITHUB_COMPARISON.md
- docs/SPRINTA_UI_UX_GUIDELINES.md
- docs/SPRINTA_OPEN_SOURCE_USAGE_GUIDE.md

Do not edit Backend, migrations, appsettings, API contracts or security rules.

Phase FE-01 — Cross-platform dependencies:
- Remove direct `@rolldown/binding-win32-x64-msvc`.
- Regenerate lockfile correctly on the supported Node/npm version.
- Do not remove local functional dependencies by copying GitHub package.json.
- Add scripts: lint, typecheck, test:unit, test:e2e:smoke, check.
- Build on Windows and Linux CI.

Phase FE-02 — Security upgrades:
- Upgrade Axios, DOMPurify, ECharts, Vite and safe transitive packages one group at a time.
- Do not use `npm audit fix --force`.
- For xlsx: inspect real imports and propose replacement or isolation; do not silently remove export.
- Build/test after every group.

Phase FE-03 — Login responsive:
- Mobile form first.
- No horizontal overflow at 320/360/390/430/768.
- Desktop remains two columns.
- Light/dark and error states.
- Add Playwright viewport assertions.

Phase FE-04 — Bundle:
- Audit ApexCharts, Chart.js and ECharts imports.
- Migrate one screen at a time to ECharts.
- Remove old chart library only after grep shows no imports.
- Lazy-load chart/editor/admin routes.
- Report chunk sizes.

Open-source rules:
- Existing Vue 3, Element Plus, Pinia, VueUse, TipTap, DOMPurify, ECharts, Lucide, motion.
- Mobbin/Taste/Hallmark/21st/Vue Bits are references only.
- No React JSX/hooks.
- No new UI framework.
- GSAP only landing/showcase, not dashboards.

After each phase:
- npm ci
- lint/typecheck/unit/build/E2E available
- git diff --name-only
- A–E report
- Stop. Do not start next phase automatically.
