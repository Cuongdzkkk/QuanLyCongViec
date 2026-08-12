namespace TaskManagement.Tests.Logic;

internal static class SqlServerTestConnection
{
    public static string Build(string databaseName, int timeoutSeconds = 30)
    {
        var server = Environment.GetEnvironmentVariable("SPRINTA_TEST_SQL_SERVER") ?? "localhost";

        return $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout={timeoutSeconds}";
    }
}
