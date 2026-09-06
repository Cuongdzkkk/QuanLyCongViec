# SprintA AI Tooling

## Portable repository configuration

These files travel with a normal Git commit and `git pull`:

- `AGENTS.md`, `Frontend/AGENTS.md`, `Backend/AGENTS.md`
- `CONTEXT.md`
- `.agents/skills/**` and `skills-lock.json`
- `CLAUDE.md`, `.github/copilot-instructions.md` and `.cursor/rules/sprinta.mdc`

They provide project context and instructions only. They do not execute code after a pull.

## Project skills

The repository keeps one project-level copy of each skill. `skills-lock.json` records its GitHub source and hash. Existing Matt Pocock skills are preserved; the bootstrap adds only missing `to-tickets`, `wayfinder`, `research` and `prototype`. It also adds `gpt-taste` and `design-taste-frontend` from the Taste Skill repository.

Global user-level skills with the same names may exist on a developer machine. They are a separate scope and are not committed; the project copy is the portable, repo-local source of truth. The bootstrap never removes global skills.

Do not install the same skill through multiple mechanisms or duplicate a skill directory under another name.

## One-time setup per machine

From the repository root, after reviewing the diff:

### Windows PowerShell

```powershell
.scripts\setup-ai-tools.ps1 -SkipMachineTools
```

### macOS / Linux / WSL

```bash
SKIP_MACHINE_TOOLS=1 bash ./scripts/setup-ai-tools.sh
```

Normal teammate setup does not require a network install: the committed skill directories are already project-level. Only when a project skill is missing, install the reviewed CLI once with `npm install --global skills@1.5.23`, then rerun the script.

The scripts check dependencies and install only missing project skills through a pre-installed, reviewed `skills@1.5.23` CLI. They never invoke `npx`, download a package, update existing skills, enable Git hooks, install application dependencies or download RTK executables. If the CLI is missing, install it manually, review it, then rerun the script. Review the resulting skill diff before committing it.

## Machine-level tools

Machine-level setup is deliberately opt-in because a Git pull must not change another developer's machine.

- **Ponytail**: if the Codex CLI is available, run the setup script without `-SkipMachineTools` (or with `SKIP_MACHINE_TOOLS=0`) to print the manual install command. Review the command and plugin source, run it yourself, then restart/open Codex, open `/hooks`, review and trust the Ponytail hooks, and start a fresh task. Do not bypass hook review.
- **RTK**: install it separately from its official project/release for the current operating system. If `rtk` already exists, the setup script prints `rtk init -g --codex` for manual review/run; otherwise it prints the next step and leaves the machine unchanged.

## Scope guardrails

This bootstrap does not install Storybook, Playwright, axe, Testcontainers, Gitleaks, Trivy, Semgrep, OpenTelemetry or another large toolchain. First produce a phase-next proposal with owner, scope and acceptance criteria; install those tools only when a concrete feature or quality gap requires them.

The bootstrap does not change application architecture, framework, API, authentication, billing or business behavior. It adds repository guidance and setup tooling only.

## Phase-next proposal

1. Map the highest-value onboarding journey from account creation through first work item.
2. Select one vertical slice and document its route-to-persistence trace.
3. Add focused regression coverage using the repository's existing test setup.
4. Decide which missing observability, security or browser-test capability is justified by evidence.
5. Review the proposed scope before adding any new large dependency.
