# SprintA Frontend Agent Rules

These rules apply to everything under `Frontend/` and extend the repository root `AGENTS.md`.

## Design system first

Before adding page-specific CSS, search for an existing token, wrapper or shared component.

Prefer:

1. existing SprintA wrapper or component,
2. existing Element Plus primitive,
3. existing Tailwind or CSS token,
4. a new small reusable wrapper only when the pattern repeats.

Do not introduce another large UI framework.

Unify buttons, icon buttons, inputs, textareas, selects, date controls, modals, drawers, popovers, dropdowns, tooltips, tables, cards, tabs, badges, loading, skeleton, empty and error states. Keep typography, spacing, radius, shadows, motion and z-index in CSS variables or design tokens instead of scattered magic values.

## Responsive

Check every changed user-facing page at 360, 390, 480, 768, 1024, 1280 and 1440 px. No page-level horizontal overflow is allowed unless the component intentionally scrolls, such as a wide data table.

Mobile is not a shrunk desktop layout. Reflow controls, drawers, toolbars and actions intentionally.

## UX and data

For every interaction consider the primary action, keyboard and focus behavior, hover, pressed, focus, disabled and loading states, nearby validation, useful empty state, non-blocking success feedback, recoverable errors and discoverability. Do not add dead controls.

For new-account flows, optimize:

`Register -> Welcome -> Create workspace/site -> Create project -> choose Scrum/Kanban -> first task -> dashboard`

Do not replace real API data with mock arrays or fake counts, charts, users, tasks, rewards or AI results. Preserve auth, state and API contracts unless the task explicitly includes business/backend changes.

## Validation

For meaningful frontend changes, run the relevant subset and preferably all:

- `npm run test`
- `npm run typecheck`
- `npm run lint`
- `npm run build`

Add or update E2E coverage for user journeys when the existing test setup supports it.
