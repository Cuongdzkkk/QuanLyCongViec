param(
    [string]$Server = "KHOI\SQLEXPRESS",
    [string]$Database = "TaskManagementDB",
    [string]$InputFile = "",
    [string]$Query = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($InputFile) -and [string]::IsNullOrWhiteSpace($Query)) {
    throw "Provide either -InputFile or -Query."
}

Add-Type -AssemblyName System.Data

# Build connection string matching appsettings.json
$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=SSPI;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;Connection Timeout=300"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()

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
