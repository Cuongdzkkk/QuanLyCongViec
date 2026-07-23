using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Extensions;

public static class DatabaseDeploymentExtensions
{
    public static async Task<bool> RunDatabaseDeploymentCommandAsync(
        this IServiceProvider services,
        string[] args,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var migrate = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
        var seed = args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase);
        if (!migrate && !seed) return false;

        if (seed && (!environment.IsDevelopment() || !configuration.GetValue<bool>("Hosting:SeedDemoData")))
            throw new InvalidOperationException("Demo seed requires Development and Hosting:SeedDemoData=true.");

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (migrate)
        {
            if (!context.Database.IsRelational())
                throw new InvalidOperationException("The migration command requires a relational database.");
            await context.Database.MigrateAsync();
        }

        if (seed) await DataSeeder.SeedMockDataAsync(context);
        return true;
    }
}
