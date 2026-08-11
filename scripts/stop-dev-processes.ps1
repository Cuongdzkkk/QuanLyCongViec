param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$singleFileBackendRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA 'QuanLyCongViec\TaskManagement.API\singlefile-debug')
).TrimEnd('\') + '\'
$processes = @(Get-CimInstance Win32_Process)
$processById = @{}

foreach ($process in $processes) {
    $processById[[int]$process.ProcessId] = $process
}

$appProcesses = @($processes | Where-Object {
    $commandLine = [string]$_.CommandLine
    $executablePath = [string]$_.ExecutablePath
    $isSingleFileBackend = $_.Name -ieq 'TaskManagement.API.exe' -and
        -not [string]::IsNullOrWhiteSpace($executablePath) -and
        [System.IO.Path]::GetFullPath($executablePath).StartsWith(
            $singleFileBackendRoot,
            [System.StringComparison]::OrdinalIgnoreCase
        )

    if ([string]::IsNullOrWhiteSpace($commandLine)) {
        return $isSingleFileBackend
    }

    $belongsToRepository = $commandLine.IndexOf(
        $root,
        [System.StringComparison]::OrdinalIgnoreCase
    ) -ge 0

    if (-not $belongsToRepository) {
        return $isSingleFileBackend
    }

    $isBackend = $commandLine -like '*TaskManagement.API.dll*'
    $isFrontend = $commandLine -like '*Frontend\node_modules*' -and $commandLine -like '*vite*'
    return $isBackend -or $isFrontend
})

$processTrees = @{}
foreach ($appProcess in $appProcesses) {
    $treeRoot = $appProcess
    $current = $appProcess

    while ($current.ParentProcessId -and $processById.ContainsKey([int]$current.ParentProcessId)) {
        $parent = $processById[[int]$current.ParentProcessId]
        $parentCommandLine = [string]$parent.CommandLine
        $isDevCommandShell = $parent.Name -ieq 'cmd.exe' -and (
            $parentCommandLine -like '*dotnet run*' -or
            $parentCommandLine -like '*npm run dev*' -or
            $parentCommandLine -like '*vite*'
        )

        if ($isDevCommandShell) {
            $treeRoot = $parent
            $current = $parent
            continue
        }

        if ($parent.Name -notin @('dotnet.exe', 'node.exe', 'npm.exe')) {
            break
        }

        $treeRoot = $parent
        $current = $parent
    }

    $processTrees[[int]$treeRoot.ProcessId] = $treeRoot
}

foreach ($tree in $processTrees.Values) {
    & taskkill.exe /PID $tree.ProcessId /T /F *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stop development process tree $($tree.ProcessId)."
    }
}

if ($processTrees.Count -gt 0) {
    Start-Sleep -Milliseconds 500
    Write-Host "Stopped $($processTrees.Count) existing development process tree(s)."
}
