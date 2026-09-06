# SprintA Backend Agent Rules

These rules apply to everything under `Backend/` and extend the repository root `AGENTS.md`.

## Architecture and compatibility

Preserve the existing separation between API, Application, Domain and Infrastructure. Put new business logic in the appropriate layer instead of accumulating it in controllers. Do not rewrite working architecture just to adopt a new pattern.

Unless a task explicitly requests a breaking change, keep existing routes, field names, response contracts and auth behavior. Make migrations backward-conscious.

## Real data and authorization

Production features use persisted real data. Do not add mock repositories or hard-coded records to make the UI appear complete. Keep seed/demo data explicitly separate from production user data.

Authorization belongs on the backend, not only in hidden frontend controls. For every resource-changing or read endpoint, consider authentication, role/permission, workspace/project membership, object-level authorization and IDOR resistance, input validation, sensitive-data exposure, rate limits for expensive or AI endpoints, transactions and data integrity, safe logging, and file-upload validation where applicable.

Never disable a security check merely to make a flow pass.

## Scrum, Kanban and realtime

Use the domain terms in `CONTEXT.md`. Scrum supports Product Backlog, Sprint Planning, Sprint Goal, Sprint Backlog and Active Sprint when implemented by product scope. Kanban uses continuous flow and optional WIP rules; a pure Kanban project does not require a Sprint.

SignalR is not a second source of truth. Persist and validate on the server first, then broadcast an authorized result/event that clients can reconcile. Check authorization for hub connections, groups and event payloads.

## Validation

For backend changes, run relevant tests and preferably `dotnet test`. Persistence-sensitive behavior should have integration-level coverage when practical. Permission/security fixes should have a regression test that fails without the fix.
