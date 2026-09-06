#!/usr/bin/env bash
set -euo pipefail

SKIP_MACHINE_TOOLS="${SKIP_MACHINE_TOOLS:-0}"
SKILLS_CLI_VERSION="1.5.23"
SCRIPT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(git -C "$SCRIPT_ROOT" rev-parse --show-toplevel)"

if [[ "$ROOT" != "$SCRIPT_ROOT" ]]; then
  echo "Run this script from the repository copy that contains scripts/." >&2
  exit 1
fi
cd "$ROOT"

echo "== SprintA AI bootstrap =="
has_skill() {
  [[ -f ".agents/skills/$1/SKILL.md" ]]
}

add_missing_skills() {
  local source="$1"
  shift
  local skills=("$@")
  [[ "${#skills[@]}" -gt 0 ]] || return 0

  if ! command -v skills >/dev/null; then
    echo "The reviewed skills CLI is required for missing project skills. Install it manually with: npm install --global skills@$SKILLS_CLI_VERSION" >&2
    exit 1
  fi

  local args=(add "$source" --agent codex --copy --yes)
  local skill
  for skill in "${skills[@]}"; do
    args+=(--skill "$skill")
  done
  skills "${args[@]}"
}

missing_matt=()
for skill in to-tickets wayfinder research prototype; do
  if ! has_skill "$skill"; then
    missing_matt+=("$skill")
  fi
done

missing_taste=()
for skill in gpt-taste design-taste-frontend; do
  if ! has_skill "$skill"; then
    missing_taste+=("$skill")
  fi
done

if [[ "${#missing_matt[@]}" -gt 0 ]]; then
  echo "Installing missing Matt Pocock skills: ${missing_matt[*]}"
  add_missing_skills mattpocock/skills "${missing_matt[@]}"
else
  echo "Matt Pocock skills are already present; skipped."
fi

if [[ "${#missing_taste[@]}" -gt 0 ]]; then
  echo "Installing missing Taste skills: ${missing_taste[*]}"
  add_missing_skills Leonxlnx/taste-skill "${missing_taste[@]}"
else
  echo "Taste skills are already present; skipped."
fi

if [[ "$SKIP_MACHINE_TOOLS" != "1" ]]; then
  echo
  echo "== Optional machine-level tools =="
  if command -v codex >/dev/null; then
    echo "Ponytail is available as an optional Codex plugin."
    echo "Review and run manually: codex plugin marketplace add DietrichGebert/ponytail; codex plugin add ponytail@ponytail"
    echo "After installation, open /hooks, review/trust hooks, then restart Codex."
  else
    echo "Codex CLI not found; skipped Ponytail machine setup."
  fi

  if command -v rtk >/dev/null; then
    echo "RTK detected. Review and run manually: rtk init -g --codex"
  else
    echo "RTK is not installed; no executable was downloaded. Install it separately, then run: rtk init -g --codex"
  fi
else
  echo "Skipped machine-level tools."
fi

echo
echo "Done. Review with: git status --short; git diff --check"
