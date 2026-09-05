# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"SprintA" — a task/project management system (Vietnamese: Quản Lý Công Việc) with workspaces, projects, sprints, Kanban boards, gamification, AI assistance (Gemini), and real-time updates. Two independent apps:

- **Backend/** — ASP.NET Core Web API on **.NET 10**, EF Core code-first with SQL Server, SignalR. Runs at `http://localhost:5136`.
- **Frontend/** — Vue 3 (`<script setup>`) + Vite SPA, Pinia, Tailwind CSS 4, Element Plus. Dev server at `http://localhost:5173`.

Comments, commit messages, and UI text are largely in Vietnamese; keep that style when editing nearby code.

## Commands

### Backend (run from `Backend/`)

```bash
dotnet restore
dotnet build --configuration Release
dotnet test                                    # xUnit tests in TaskManagement.Tests
dotnet test --filter "FullyQualifiedName~SprintLogicTests"          # single test class
dotnet test --filter "FullyQualifiedName~SprintLogicTests.MethodName"  # single test
```

Run the API (from `Backend/src/TaskManagement.API/`):

```bash
dotnet run        # listens on http://localhost:5136 (launchSettings.json)
```

EF Core migrations (from `Backend/src/TaskManagement.API/`):

```bash
dotnet ef migrations add <Name> --project ../TaskManagement.Infrastructure --startup-project .
dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project .
```

### Frontend (run from `Frontend/`)

```bash
npm install
npm run dev       # Vite on port 5173 (the script's --port flag overrides vite.config.js's port 3000)
npm run build
```

There is no frontend test suite or lint script. CI (`.github/workflows/dotnet-ci.yml`) only restores/builds/tests the backend with .NET 10 on pushes to `main`, `develop`, `feature/*` and PRs to `main`/`develop`.

`run.bat` is a Windows-only all-in-one launcher (optional DB reset + start both apps); `force_reset_db.bat` resets the DB. Loose `.js`/`.cjs`/`.txt` files at `Frontend/` root and `Backend/src/TaskManagement.API/` (e.g. `find_dups.js`, `fix_modal.cjs`, `build_output.txt`) are one-off debug scripts/artifacts, not application source.

## Backend Architecture

Clean architecture with four projects under `Backend/src/` (solution file: `Backend/TaskManagement.slnx`):

- **TaskManagement.Domain** — entities only (`Entities/`, ~45 entities: `WorkTask`, `Project`, `Sprint`, `Workspace`, `User`, gamification, AI, audit tables) and `Constants/`. No dependencies.
- **TaskManagement.Application** — DTOs (grouped by feature under `DTOs/`), service interfaces (`Interfaces/I*Service.cs`), and pure helpers in `Common/`.
- **TaskManagement.Infrastructure** — `Data/ApplicationDbContext.cs`, entity configurations, `Data/DataSeeder.cs`, EF migrations, and all service implementations (`Services/`).
- **TaskManagement.API** — controllers, SignalR hubs (`Hubs/KanbanHub.cs`, `NotificationHub.cs` mapped at `/kanban-hub` and `/notification-hub`), middlewares (performance, IP whitelist), and DI registration in `Extensions/ServiceCollectionExtensions.cs` (`AddAuthServices`, `AddWorkspaceServices`, `AddAuditLogServices`).

Key conventions and behaviors:

- **All API responses** wrap payloads in `ApiResponse<T>` (`{ statusCode, message, data }`) from `Application/DTOs/Common/ApiResponse.cs`. New service methods follow the interface-in-Application / implementation-in-Infrastructure pattern, registered in `ServiceCollectionExtensions.cs`.
- **Auth**: short-lived JWT access tokens (15 min) + HttpOnly refresh-token cookie, plus Google/GitHub OAuth and email OTP (Resend). Login endpoints are rate-limited via the `FixedWindow` policy in `Program.cs`.
- **Startup magic in `Program.cs`** (don't remove): in Development, if SQL Server is unreachable the app silently falls back to an EF InMemory database. On every startup it runs migrations, a raw-SQL "schema guard" block that patches missing columns/tables idempotently, and `DataSeeder.SeedMockDataAsync`. The codebase deliberately keeps a single squashed migration (`PlaneRenovation`); the schema guard exists to reconcile databases that predate it. If you change the schema, prefer a proper migration and mirror critical changes into the schema guard only when older databases must be patched in place.
- **Seeded dev accounts**: `admin@example.com`/`Admin@123`, `test@example.com`/`Test@123`, `dev@sprinta.local`/`dev123`.
- **Audit logging** is asynchronous: controllers enqueue to the `IAuditLogQueue` singleton, drained by the `AuditLogWorker` hosted service.
- **Tests** (`Backend/TaskManagement.Tests/Logic/`) instantiate Infrastructure services directly against an InMemory `ApplicationDbContext` — no mocking framework, no WebApplicationFactory.

## Frontend Architecture

- **API access** goes through `src/api/axiosClient.js`: base URL from `VITE_API_BASE_URL` (`.env`, default `http://localhost:5136/api`), attaches the Bearer token from `sessionStorage` (via `src/utils/authSession.js`), and transparently refreshes on 401 with request queueing. Always use this client (or `src/api/signalrService.js` for SignalR), never raw `axios`/`fetch`.
- **Routing**: `src/router/index.js` composes per-area route modules (`homeRoutes`, `authRoutes`, `dashboardRoutes`, `spaceRoutes`, `aiRoutes`, `logRoutes`, `adminRoutes`). A global `beforeEach` guard enforces login, `meta.requiresSystemAdminAccess`, `meta.requiresProjectSettingsAccess`, and `meta.requiredRoles` (checked via `src/utils/permissions.js`).
- **State**: Pinia stores in `src/store/use*Store.js` (project, sprint, work task, activity, admin users).
- **Layouts**: `src/components/layout/` has two shells — `NexusLayout` (main app) and `AdminLayout` (admin area under `src/views/admin/`).
- **Element Plus components are auto-imported** by `unplugin-vue-components`/`unplugin-auto-import` (see `vite.config.js`) — don't add manual `el-*` imports. Path alias `@` → `src/`.
- Rich text uses TipTap; sanitize rendered HTML with `dompurify`.
- Vite dev server proxies `/api` and `/kanban-hub` (websocket) to `localhost:5136`. Backend CORS only allows origins `localhost:5173`/`5174` — keep ports consistent if you change them.
