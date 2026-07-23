$conn = New-Object System.Data.SqlClient.SqlConnection('Server=KHOI\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT session_id FROM sys.dm_exec_sessions WHERE database_id = DB_ID('TaskManagementDB')"
$reader = $cmd.ExecuteReader()
$sessions = @()
while ($reader.Read()) {
    $sessions += $reader.GetInt16(0)
}
$reader.Close()

foreach ($s in $sessions) {
    $cmd.CommandText = "KILL $s"
    try {
        $cmd.ExecuteNonQuery()
        Write-Host "Killed session $s"
    } catch {
        Write-Host "Failed to kill session $s"
    }
}
$conn.Close()
Write-Host "Done kill"
