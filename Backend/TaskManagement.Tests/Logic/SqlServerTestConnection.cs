namespace TaskManagement.Tests.Logic;

internal static class SqlServerTestConnection
{
    private const string DefaultServer = @".\SQLEXPRESS01";

    public static string Build(string databaseName, int timeoutSeconds = 30)
    {
        var server = Environment.GetEnvironmentVariable("SPRINTA_TEST_SQL_SERVER");
        if (string.IsNullOrWhiteSpace(server))
        {
            server = DefaultServer;
        }

        return $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout={timeoutSeconds}";
    }
}
