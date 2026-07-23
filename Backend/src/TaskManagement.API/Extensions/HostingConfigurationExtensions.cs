using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Extensions;

public static class HostingConfigurationExtensions
{
    private const string JwtConfigurationKey = "Jwt:SecretKey";
    private const string JwtEnvironmentVariable = "Jwt__SecretKey";
    private static readonly string[] PlaceholderMarkers =
    {
        "CHANGE_ME", "PLACEHOLDER", "YOUR_", "REPLACE_ME", "SET_IN_ENVIRONMENT"
    };

    public static IServiceCollection AddEnvironmentSafeDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var provider = configuration["Database:Provider"]?.Trim();
        var allowDevelopmentInMemory = configuration.GetValue<bool>("Database:AllowDevelopmentInMemory");

        if (environment.IsEnvironment("Testing") && string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            return AddInMemory(services, configuration["Database:InMemoryName"] ?? "SprintA-Testing");

        if (environment.IsDevelopment() && allowDevelopmentInMemory && string.IsNullOrWhiteSpace(connectionString))
            return AddInMemory(services, configuration["Database:InMemoryName"] ?? "SprintA-Development");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"ConnectionStrings:DefaultConnection is required in {environment.EnvironmentName}. InMemory must be explicitly selected for Testing or explicitly enabled for Development.");

        ValidateSqlConnectionPolicy(connectionString, environment);
        return services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                sqlOptions.MigrationsAssembly("TaskManagement.Infrastructure");
            }).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
    }

    public static void ValidateEnvironmentConfiguration(IConfiguration configuration, IHostEnvironment environment)
        => ValidateEnvironmentConfiguration(configuration, environment, Environment.GetEnvironmentVariable);

    public static void ValidateEnvironmentConfiguration(
        IConfiguration configuration,
        IHostEnvironment environment,
        Func<string, string?> externalSecretReader)
    {
        RequireJwtSecret(configuration, environment, externalSecretReader);
        if (!environment.IsEnvironment("Testing") && configuration.GetValue("Features:AIEnabled", true))
        {
            RequireSecret(configuration, "ZenMux:ApiKey", environment, 20);
            RequireSecret(configuration, "Gemini:ApiKey", environment, 20);
        }

        foreach (var provider in new[] { "GoogleCalendar", "Gmail", "Slack", "MicrosoftMail" })
        {
            if (!configuration.GetValue<bool>($"IntegrationOAuth:{provider}:Enabled")) continue;
            RequireSecret(configuration, $"IntegrationOAuth:{provider}:ClientId", environment, 5);
            RequireSecret(configuration, $"IntegrationOAuth:{provider}:ClientSecret", environment, 12);
        }

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            if (origins.Length == 0 || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Cors:AllowedOrigins must contain an explicit HTTPS allowlist outside Development/Testing.");
        }
    }

    private static IServiceCollection AddInMemory(IServiceCollection services, string databaseName) =>
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning);
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
            }));

    private static void RequireSecret(IConfiguration configuration, string key, IHostEnvironment environment, int minimumLength)
    {
        var value = configuration[key]?.Trim();
        if (IsInvalidSecret(value, minimumLength))
            throw new InvalidOperationException($"{key} must come from environment variables or a secret store for {environment.EnvironmentName}; placeholders are rejected.");
    }

    private static void RequireJwtSecret(
        IConfiguration configuration,
        IHostEnvironment environment,
        Func<string, string?> externalSecretReader)
    {
        var configuredValue = configuration[JwtConfigurationKey]?.Trim();

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            if (IsInvalidSecret(configuredValue, 32))
                throw JwtConfigurationException(environment);
            return;
        }

        var externalValue = externalSecretReader(JwtEnvironmentVariable)?.Trim();
        if (IsInvalidSecret(externalValue, 32) ||
            !string.Equals(configuredValue, externalValue, StringComparison.Ordinal))
        {
            throw JwtConfigurationException(environment);
        }
    }

    private static bool IsInvalidSecret(string? value, int minimumLength) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Length < minimumLength ||
        PlaceholderMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static InvalidOperationException JwtConfigurationException(IHostEnvironment environment) =>
        new($"Jwt:SecretKey must be supplied through external configuration and meet security requirements for {environment.EnvironmentName}.");

    private static void ValidateSqlConnectionPolicy(string connectionString, IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return;
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        if (!builder.Encrypt) throw new InvalidOperationException("SQL encryption cannot be disabled outside Development/Testing.");

        var source = builder.DataSource.Trim();
        if (source is "." or "(local)" || source.Contains("localhost", StringComparison.OrdinalIgnoreCase) || source.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) || source.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A machine-local SQL Server name is not allowed outside Development/Testing.");
    }
}
