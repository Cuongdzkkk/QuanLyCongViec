[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('provision', 'smoke', 'cleanup')]
    [string]$Command,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{1,31}$')]
    [string]$RunId,

    [ValidateSet('Development', 'Testing')]
    [string]$Environment = 'Testing',

    [string]$ConnectionEnvironment = 'ConnectionStrings__DefaultConnection',

    [switch]$Keep
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'tools\LocalCollaborationFixture\LocalCollaborationFixture.csproj'

if (-not (Test-Path -LiteralPath "Env:$ConnectionEnvironment")) {
    throw "Required connection environment variable '$ConnectionEnvironment' is not set."
}

if ($Keep -and $Command -ne 'smoke') {
    throw '-Keep is valid only with the smoke command.'
}

$fixtureArguments = @(
    'run',
    '--configuration', 'Release',
    '--project', $projectPath,
    '--',
    $Command,
    '--run-id', $RunId,
    '--environment', $Environment,
    '--connection-env', $ConnectionEnvironment
)
if ($Keep) {
    $fixtureArguments += '--keep'
}

& dotnet @fixtureArguments
if ($LASTEXITCODE -ne 0) {
    throw "Local collaboration fixture failed with exit code $LASTEXITCODE."
}
