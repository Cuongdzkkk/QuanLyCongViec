# Local collaboration fixture

This explicit local-only runner provisions deterministic `e2e-collab-<runId>`
users and collaboration rows, exercises the real API/SQL Server/SignalR stack,
and removes only data owned by the same run ID.

The connection string must be supplied through an environment variable. It is
never accepted on the command line, written to disk, or printed.

```powershell
$env:ConnectionStrings__DefaultConnection = '<local integrated-security connection>'

dotnet run --project tools/LocalCollaborationFixture -- `
  smoke --run-id audit01 --environment Testing
```

Or use the repository wrapper, which builds the runner in Release mode and
forwards only the name of the connection-string environment variable:

```powershell
.\scripts\local-collaboration-fixture.ps1 smoke -RunId audit01
```

Explicit lifecycle commands are also available:

```powershell
dotnet run --project tools/LocalCollaborationFixture -- `
  provision --run-id audit01 --environment Testing

dotnet run --project tools/LocalCollaborationFixture -- `
  cleanup --run-id audit01 --environment Testing
```

`smoke` cleans a previous fixture with the same run ID before provisioning and
cleans again in `finally`. Use `--keep` only when a retained local fixture is
deliberately needed, then run `cleanup` with the same run ID.

The runner refuses environments other than `Development`/`Testing` and refuses
any target other than `KHOI\SQLEXPRESS`, `TaskManagementDB`, Windows Integrated
Security. Password hashes and JWT signing material are random, process-local,
and never emitted.
