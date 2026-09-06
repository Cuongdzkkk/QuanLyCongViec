param(
    [switch]$SkipMachineTools
)

$ErrorActionPreference = "Stop"
$skillsCliVersion = '1.5.23'

function Has-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Add-MissingSkills([string]$Source, [string[]]$Skills) {
    if ($Skills.Count -eq 0) {
        return
    }

    if (-not (Has-Command 'skills')) {
        throw "The reviewed skills CLI is required for missing project skills. Install it manually with: npm install --global skills@$skillsCliVersion"
    }

    $arguments = @('add', $Source, '--agent', 'codex', '--copy', '--yes')
    foreach ($skill in $Skills) {
        $arguments += @('--skill', $skill)
    }

    & skills @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install missing skills from $Source."
    }
}

$scriptRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = (& git -C $scriptRoot rev-parse --show-toplevel).Trim()
if ([IO.Path]::GetFullPath($repoRoot) -ne [IO.Path]::GetFullPath($scriptRoot)) {
    throw "Run this script from the repository copy that contains scripts/."
}
Set-Location $repoRoot

Write-Host "== SprintA AI bootstrap ==" -ForegroundColor Cyan

$missingMatt = @('to-tickets', 'wayfinder', 'research', 'prototype') | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $repoRoot ".agents\skills\$_\SKILL.md"))
}
$missingTaste = @('gpt-taste', 'design-taste-frontend') | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $repoRoot ".agents\skills\$_\SKILL.md"))
}

if ($missingMatt.Count -gt 0) {
    Write-Host "Installing missing Matt Pocock skills: $($missingMatt -join ', ')"
    Add-MissingSkills 'mattpocock/skills' $missingMatt
} else {
    Write-Host 'Matt Pocock skills are already present; skipped.'
}

if ($missingTaste.Count -gt 0) {
    Write-Host "Installing missing Taste skills: $($missingTaste -join ', ')"
    Add-MissingSkills 'Leonxlnx/taste-skill' $missingTaste
} else {
    Write-Host 'Taste skills are already present; skipped.'
}

if (-not $SkipMachineTools) {
    Write-Host "`n== Optional machine-level tools ==" -ForegroundColor Cyan

    if (Has-Command 'codex') {
        Write-Host 'Ponytail is available as an optional Codex plugin.'
        Write-Host 'Review and run manually: codex plugin marketplace add DietrichGebert/ponytail; codex plugin add ponytail@ponytail'
        Write-Host 'After installation, open /hooks, review/trust hooks, then restart Codex.'
    } else {
        Write-Host 'Codex CLI not found; skipped Ponytail machine setup.'
    }

    if (Has-Command 'rtk') {
        Write-Host 'RTK detected. Review and run manually: rtk init -g --codex'
    } else {
        Write-Host 'RTK is not installed; no executable was downloaded. Install it separately, then run: rtk init -g --codex'
    }
} else {
    Write-Host 'Skipped machine-level tools.'
}

Write-Host "`nDone. Review with: git status --short; git diff --check"
