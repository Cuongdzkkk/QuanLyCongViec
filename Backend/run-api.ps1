param(
    [string]$Url = "http://localhost:5136",
    [switch]$Migrate,
    [switch]$SeedDemo
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProject = Join-Path $scriptRoot "src\TaskManagement.API\TaskManagement.API.csproj"
$apiWorkingDirectory = Join-Path $scriptRoot "src\TaskManagement.API"

function Stop-ExistingApiProcesses {
    $matchingProcesses = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessId -ne $PID -and (
                $_.ProcessName -match 'TaskManagement\.API' -or
                ($_.CommandLine -and $_.CommandLine -match 'TaskManagement\.API(\.exe|\.dll)?')
            )
        } |
        Select-Object -Unique ProcessId, ProcessName, CommandLine

    foreach ($process in $matchingProcesses) {
        try {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
            Write-Host "Stopped existing API process $($process.ProcessName) [$($process.ProcessId)]"
        }
        catch {
            Write-Host "Could not stop process $($process.ProcessId): $($_.Exception.Message)"
        }
    }
}

Stop-ExistingApiProcesses

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SecretKey = "SprintA-development-local-signing-key-2026-change-outside-dev"
if ($SeedDemo) {
    $env:Hosting__SeedDemoData = "true"
}

Set-Location $apiWorkingDirectory
$appArgs = @()
if ($Migrate) { $appArgs += "--migrate" }
if ($SeedDemo) { $appArgs += "--seed-demo" }

$runArgs = @("run", "--project", $apiProject, "--no-launch-profile", "--verbosity", "minimal", "--")
if ($appArgs.Count -gt 0) {
    $runArgs += $appArgs
}
$runArgs += "--urls"
$runArgs += $Url

& dotnet @runArgs
