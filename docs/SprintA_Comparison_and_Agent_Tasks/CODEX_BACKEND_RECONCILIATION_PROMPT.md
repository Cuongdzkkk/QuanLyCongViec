# CODEX TASK — RECONCILE LOCAL WITH CURRENT GITHUB SAFELY

Repository: QuanLyCongViec
Owner: CODEX
Layer: Git, Backend, Security, Migrations

Read:
- AGENTS.md
- SPRINTA_LOCAL_VS_GITHUB_COMPARISON.md
- docs/SprintA_Master_Execution_Pack files relevant to P0

Rules:
- Do not run git pull on dirty worktree.
- Do not use reset --hard, clean -fd, add . or add -A.
- Do not restore Program.cs/appsettings.json from main without security review.
- Do not print secrets.
- Do not modify frontend business UI.

Steps:
1. Record branch, HEAD, status and worktree list.
2. Create binary patch backup and untracked manifest.
3. Run `git fetch origin --prune --tags`.
4. Determine remote default HEAD and compare:
   - HEAD...origin/main
   - HEAD...origin/KhoiSigma if present
   - security checkpoint 62c6b913 presence.
5. Generate tables: REMOTE_ONLY, LOCAL_ONLY, BOTH_MODIFIED, LOCAL_DELETED.
6. Reproduce backend build/test from a clean worktree.
7. Review all untracked P0 source/tests/migrations.
8. Create one recovery branch from the safest tested local commit.
9. Port/commit by groups:
   A. P0 data/security services + tests
   B. migrations + snapshot
   C. hosting/upload/AI safety
   D. line-ending normalization separately
10. Restore remote files only when:
    - missing import/build proves it is required;
    - remote version does not regress security;
    - file is not generated/runtime/config secret.
11. Keep IntegrationSchemaGuard deleted only if replacement migration/deployment tests pass.
12. Move Data Protection key ring outside Git and ensure .gitignore.
13. Run:
    dotnet restore
    dotnet build
    dotnet test
    migration list / clean DB / existing DB tests
14. Produce A–E report and stop before push.

Required result:
- Clean auditable branch.
- No secret.
- No unrelated docs deletion in source commit.
- No remote security regression.
