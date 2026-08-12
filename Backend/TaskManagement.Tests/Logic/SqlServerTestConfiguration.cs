using Microsoft.Data.SqlClient;

namespace TaskManagement.Tests.Logic;

internal static class SqlServerTestConfiguration
{
    public static string ConnectionString(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("SPRINTA_TEST_SQL_SERVER")
                ?? "localhost",
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = 30
        };
        return builder.ConnectionString;
    }
}
