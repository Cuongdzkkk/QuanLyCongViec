using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace LocalCollaborationFixture;

internal sealed record FixtureOptions(
    string Command,
    string RunId,
    string Prefix,
    string EnvironmentName,
    string ConnectionString,
    bool Keep)
{
    private static readonly Regex RunIdPattern = new(
        "^[a-z0-9][a-z0-9-]{1,31}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static FixtureOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("provision" or "smoke" or "cleanup"))
            throw new FixtureUsageException(Usage());

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keep = false;
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--keep")
            {
                keep = true;
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new FixtureUsageException($"Invalid argument: {argument}\n{Usage()}");
            values[argument] = args[++index];
        }

        if (!values.TryGetValue("--run-id", out var runId) ||
            !RunIdPattern.IsMatch(runId))
        {
            throw new FixtureUsageException(
                "--run-id is required and must match ^[a-z0-9][a-z0-9-]{1,31}$." );
        }

        if (!values.TryGetValue("--environment", out var environmentName) ||
            environmentName is not ("Development" or "Testing"))
        {
            throw new FixtureSafetyException(
                "Fixture execution is allowed only with --environment Development or Testing.");
        }

        var connectionVariable = values.GetValueOrDefault(
            "--connection-env",
            "ConnectionStrings__DefaultConnection");
        var connectionString = Environment.GetEnvironmentVariable(connectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new FixtureSafetyException(
                $"Environment variable {connectionVariable} is required; the connection string is never accepted as a command-line value.");
        }

        ValidateTarget(connectionString);
        if (keep && args[0] != "smoke")
            throw new FixtureUsageException("--keep is valid only for the smoke command.");

        return new FixtureOptions(
            args[0],
            runId,
            $"e2e-collab-{runId}",
            environmentName,
            connectionString,
            keep);
    }

    private static void ValidateTarget(string connectionString)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new FixtureSafetyException("The configured SQL connection string is invalid.", exception);
        }

        var expectedServer = Environment.GetEnvironmentVariable("DEV_SQL_SERVER")
            ?? "localhost";
        if (!string.Equals(
                builder.DataSource.Trim(),
                expectedServer,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                builder.InitialCatalog.Trim(),
                "TaskManagementDB",
                StringComparison.OrdinalIgnoreCase) ||
            !builder.IntegratedSecurity)
        {
            throw new FixtureSafetyException(
                "Refusing target: expected the DEV_SQL_SERVER target, TaskManagementDB, and Integrated Security.");
        }
    }

    private static string Usage() =>
        "Usage: dotnet run --project tools/LocalCollaborationFixture -- " +
        "<provision|smoke|cleanup> --run-id <id> " +
        "--environment <Development|Testing> " +
        "[--connection-env ConnectionStrings__DefaultConnection] [--keep]";
}

internal sealed class FixtureUsageException : Exception
{
    public FixtureUsageException(string message) : base(message) { }
}

internal sealed class FixtureSafetyException : Exception
{
    public FixtureSafetyException(string message) : base(message) { }
    public FixtureSafetyException(string message, Exception innerException)
        : base(message, innerException) { }
}
