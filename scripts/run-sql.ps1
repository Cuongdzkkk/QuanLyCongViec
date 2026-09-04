param(
    [string]$Server = $(if ($env:DEV_SQL_SERVER) { $env:DEV_SQL_SERVER } else { ".\SQL2022" }),
    [string]$Database = $(if ($env:DEV_SQL_DATABASE) { $env:DEV_SQL_DATABASE } else { "TaskManagementDB_V4" }),
    [string]$InputFile = "",
    [string]$Query = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($InputFile) -and [string]::IsNullOrWhiteSpace($Query)) {
    throw "Provide either -InputFile or -Query."
}

Add-Type -AssemblyName System.Data

$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=SSPI;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;Connection Timeout=300"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
}
catch {
    Write-Error "Khong ket noi duoc SQL Server '$Server' database '$Database'. Hay start dung SQL instance hoac set DEV_SQL_SERVER. Chi tiet: $($_.Exception.Message)"
    exit 1
}

try {
    $sql = $Query
    if (-not [string]::IsNullOrWhiteSpace($InputFile)) {
        if (-not (Test-Path -LiteralPath $InputFile)) {
            throw "SQL file not found: $InputFile"
        }

        $sql = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $InputFile), [System.Text.Encoding]::UTF8)
    }

    $batches = [System.Text.RegularExpressions.Regex]::Split($sql, "(?im)^\s*GO\s*(?:--.*)?$")
    $index = 0

    foreach ($batch in $batches) {
        $index++
        if ([string]::IsNullOrWhiteSpace($batch)) {
            continue
        }

        $command = $connection.CreateCommand()
        $command.CommandTimeout = 300
        $command.CommandText = $batch

        try {
            [void]$command.ExecuteNonQuery()
        }
        catch {
            throw "SQL batch $index failed. $($_.Exception.Message)"
        }
    }
}
finally {
    $connection.Close()
}
