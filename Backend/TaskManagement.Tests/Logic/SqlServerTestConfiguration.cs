using Microsoft.Data.SqlClient;

namespace TaskManagement.Tests.Logic;

internal static class SqlServerTestConfiguration
{
    public static string ConnectionString(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("DEV_SQL_SERVER") ?? @"Quan",
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = 30
        };
        return builder.ConnectionString;
    }
}
