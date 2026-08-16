param(
    [string]$PreferredServer = ""
)

$ErrorActionPreference = "SilentlyContinue"

Add-Type -AssemblyName System.Data

$candidates = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($PreferredServer)) {
    $candidates.Add($PreferredServer)
}

$serviceCandidates = Get-Service -Name "MSSQL*" |
    Where-Object { $_.Status -eq "Running" } |
    ForEach-Object {
        if ($_.Name -eq "MSSQLSERVER") {
            "localhost"
        } elseif ($_.Name -like "MSSQL`$*") {
            ".\$($_.Name.Substring(6))"
        }
    }

foreach ($server in $serviceCandidates) {
    if (-not [string]::IsNullOrWhiteSpace($server) -and -not $candidates.Contains($server)) {
        $candidates.Add($server)
    }
}

foreach ($server in @(".\SQL2022", ".\SQLEXPRESS01", ".\SQLEXPRESS", "localhost", "(localdb)\MSSQLLocalDB")) {
    if (-not $candidates.Contains($server)) {
        $candidates.Add($server)
    }
}

foreach ($server in $candidates) {
    $connectionString = "Data Source=$server;Initial Catalog=master;Integrated Security=SSPI;TrustServerCertificate=True;Encrypt=False;Connection Timeout=3"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    try {
        $connection.Open()
        Write-Output $server
        exit 0
    } catch {
        continue
    } finally {
        $connection.Dispose()
    }
}

Write-Error "Khong tim thay SQL Server local dang ket noi duoc. Hay start SQL Server hoac set DEV_SQL_SERVER, vi du: set DEV_SQL_SERVER=.\SQL2022"
exit 1
