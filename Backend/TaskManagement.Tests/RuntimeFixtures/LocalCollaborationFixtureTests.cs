using LocalCollaborationFixture;

namespace TaskManagement.Tests.RuntimeFixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FixtureEnvironmentCollection
{
    public const string Name = "Local collaboration fixture environment";
}

[Collection(FixtureEnvironmentCollection.Name)]
public sealed class LocalCollaborationFixtureTests : IDisposable
{
    private const string ConnectionVariable = "QLCV_E2E_FIXTURE_TEST_CONNECTION";
    private const string ApprovedConnection =
        "Server=KHOI\\SQLEXPRESS;Database=TaskManagementDB;Trusted_Connection=True;TrustServerCertificate=True";
    private readonly string? _originalValue = Environment.GetEnvironmentVariable(ConnectionVariable);
    private readonly string? _originalServer = Environment.GetEnvironmentVariable("DEV_SQL_SERVER");

    public LocalCollaborationFixtureTests()
    {
        Environment.SetEnvironmentVariable("DEV_SQL_SERVER", @"KHOI\SQLEXPRESS");
        Environment.SetEnvironmentVariable(ConnectionVariable, ApprovedConnection);
    }

    [Fact]
    public void Parse_AcceptsExplicitTestingTargetWithoutPuttingConnectionStringInArguments()
    {
        var options = FixtureOptions.Parse(Arguments("smoke", "audit01", "Testing"));

        Assert.Equal("smoke", options.Command);
        Assert.Equal("e2e-collab-audit01", options.Prefix);
        Assert.Equal(ApprovedConnection, options.ConnectionString);
        Assert.False(options.Keep);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    public void Parse_RefusesNonApprovedEnvironment(string environment)
    {
        var exception = Assert.Throws<FixtureSafetyException>(
            () => FixtureOptions.Parse(Arguments("smoke", "audit01", environment)));

        Assert.Contains("Development or Testing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RefusesUnexpectedDatabaseTarget()
    {
        Environment.SetEnvironmentVariable(
            ConnectionVariable,
            "Server=localhost\\SQLEXPRESS;Database=AnotherDatabase;Trusted_Connection=True");

        var exception = Assert.Throws<FixtureSafetyException>(
            () => FixtureOptions.Parse(Arguments("cleanup", "audit01", "Testing")));

        Assert.Contains("Refusing target", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("UPPERCASE")]
    [InlineData("contains_underscore")]
    [InlineData("this-run-id-is-longer-than-thirty-two-characters")]
    public void Parse_RefusesInvalidRunId(string runId)
    {
        Assert.Throws<FixtureUsageException>(
            () => FixtureOptions.Parse(Arguments("provision", runId, "Development")));
    }

    [Fact]
    public void Parse_AllowsKeepOnlyForSmoke()
    {
        var arguments = Arguments("provision", "audit01", "Testing").Append("--keep").ToArray();

        Assert.Throws<FixtureUsageException>(() => FixtureOptions.Parse(arguments));
    }

    [Fact]
    public void Identity_IsStablePerRunAndIsolatedAcrossRuns()
    {
        var first = FixtureIdentity.For("e2e-collab-audit01");
        var repeat = FixtureIdentity.For("e2e-collab-audit01");
        var other = FixtureIdentity.For("e2e-collab-audit02");

        Assert.Equal(first, repeat);
        Assert.NotEqual(first.UserAId, first.UserBId);
        Assert.NotEqual(first.UserAId, first.UserCId);
        Assert.NotEqual(first.UserAId, other.UserAId);
        Assert.All(first.UserIds, id => Assert.NotEqual(Guid.Empty, id));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConnectionVariable, _originalValue);
        Environment.SetEnvironmentVariable("DEV_SQL_SERVER", _originalServer);
    }

    private static string[] Arguments(string command, string runId, string environment) =>
    [
        command,
        "--run-id", runId,
        "--environment", environment,
        "--connection-env", ConnectionVariable
    ];
}
