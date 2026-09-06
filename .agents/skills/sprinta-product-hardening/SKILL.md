---
name: sprinta-product-hardening
description: Use for SprintA work that spans UX, Scrum/Kanban semantics, real data, onboarding, integration, security, regression testing, or production hardening. Guides the agent to trace the real flow before editing and to finish end-to-end instead of polishing isolated UI.
---

# SprintA Product Hardening

Use this skill when a SprintA task is larger than a tiny isolated code edit or asks to improve a page, flow or product area.

## Trace before changing

Trace the real path:

`route -> page -> component -> store/state -> API -> backend -> database -> permission -> SignalR -> tests`

Identify the user entry point, existing tests and every applicable step. Do not assume a visible UI problem is only CSS or that an apparently unused endpoint is unused.

## Classify the task

Choose one or more:

- **Design-system / UI**: fix the shared primitive before repeating page-specific patches.
- **Scrum / Kanban domain**: verify workflow terms against `CONTEXT.md`; do not force Scrum concepts into Kanban.
- **Functional integration**: finish UI -> state -> API -> business logic -> persistence -> response -> UI feedback.
- **Onboarding**: optimize account -> workspace/site -> project -> method -> first work item -> usable dashboard.
- **Hardening**: inspect loading/error states, authorization, concurrency/realtime consistency, tests, responsive behavior and accessibility.

## Smallest complete solution

Before writing new code, check whether the feature is present but undiscoverable, whether an existing component/service/helper can be reused, and whether a native or installed framework capability is sufficient. Add the smallest new abstraction only after those checks. Never trade away validation, security, accessibility or data integrity for fewer lines.

## Real data

Production mock data is forbidden. Do not hard-code fake metrics, records, charts, users, tasks, rewards or AI results. If backend support is missing, identify the missing contract and report it rather than faking the UI.

## Quality gates

Before declaring done, check as applicable:

- desktop and mobile behavior at 360, 390, 480, 768, 1024, 1280 and 1440 px;
- keyboard, focus and accessibility behavior;
- loading, empty, error and disabled states;
- real API, persisted data and refresh/re-login persistence;
- authorization and realtime synchronization;
- automated regression coverage and absence of dead controls;
- no unrelated framework or dependency churn.

## Vertical slices

For large requests, deliver one coherent user journey, page/flow and testable outcome at a time. Review each slice before starting another. Avoid a giant whole-site refactor.

Use supporting skills when installed and relevant: `to-spec`, `diagnosing-bugs`, `tdd`, `codebase-design`, `code-review`, `gpt-taste` and `design-taste-frontend`. SprintA rules and actual application behavior outrank generic skill advice.
