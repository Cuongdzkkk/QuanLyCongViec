using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManagement.Infrastructure.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var server = Environment.GetEnvironmentVariable("DEV_SQL_SERVER") ?? "KHOI\\SQLEXPRESS";
                var database = Environment.GetEnvironmentVariable("DEV_SQL_DATABASE") ?? "TaskManagementDB";
                var connectionBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    IntegratedSecurity = true,
                    MultipleActiveResultSets = true,
                    TrustServerCertificate = true,
                    Encrypt = false,
                    ConnectTimeout = 60
                };
                connectionString = connectionBuilder.ConnectionString;
            }

            optionsBuilder.UseSqlServer(connectionString, opts => opts.CommandTimeout(180));

            return new ApplicationDbContext(optionsBuilder.Options, null, null);
        }
    }
}
