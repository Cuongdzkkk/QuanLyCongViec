using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Text.Json;
using TaskManagement.API.Controllers;
using TaskManagement.API.Extensions;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class P006HostingConfigurationTests
{
    [Fact]
    public void HOST_01_ProductionWithoutConnectionStringFailsFast()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>());
        var action = () => services.AddEnvironmentSafeDatabase(configuration, Environment("Production"));
        action.Should().Throw<InvalidOperationException>().WithMessage("*DefaultConnection is required*");
    }

    [Fact]
    public void HOST_01_ProductionRejectsUnencryptedLocalSqlConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=KHOI\\SQLEXPRESS;Database=SprintA;User Id=sa;Password=not-a-real-secret;Encrypt=False"
        });
        var action = () => services.AddEnvironmentSafeDatabase(configuration, Environment("Production"));
        action.Should().Throw<InvalidOperationException>().WithMessage("*encryption cannot be disabled*");
    }

    [Fact]
    public void HOST_01_TestingExplicitInMemoryProviderStillRuns()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Database:InMemoryName"] = Guid.NewGuid().ToString("N")
        });
        services.AddEnvironmentSafeDatabase(configuration, Environment("Testing"));
        using var provider = services.BuildServiceProvider();
        using var context = provider.GetRequiredService<ApplicationDbContext>();
        context.Database.IsInMemory().Should().BeTrue();
    }

    [Fact]
    public void HOST_01_PlaceholderJwtSecretIsRejected()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "CHANGE_ME_USE_ENVIRONMENT_VARIABLE_123456789",
            ["Features:AIEnabled"] = "false"
        });
        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            Environment("Production"),
            _ => configuration["Jwt:SecretKey"]);
        action.Should().Throw<InvalidOperationException>().WithMessage("*external configuration*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("__SET_IN_ENVIRONMENT__")]
    public void HOST_02_ProductionRejectsMissingEmptyShortOrPlaceholderJwtSecret(string? externalSecret)
    {
        var configuration = JwtConfiguration(externalSecret);
        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            Environment("Production"),
            _ => externalSecret);

        var exception = action.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Jwt:SecretKey");
        if (!string.IsNullOrEmpty(externalSecret))
        {
            exception.Message.Should().NotContain(externalSecret);
        }
    }

    [Fact]
    public void HOST_02_ProductionRejectsUsableTrackedSecretWithoutExternalProvenance()
    {
        var configuration = JwtConfiguration("valid-test-signing-material-that-is-not-a-real-secret-123");
        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            Environment("Production"),
            _ => null);

        action.Should().Throw<InvalidOperationException>().WithMessage("*external configuration*");
    }

    [Fact]
    public void HOST_02_ProductionExternalJwtSecretPassesJwtValidation()
    {
        const string externalSecret = "valid-test-signing-material-that-is-not-a-real-secret-456";
        var configuration = JwtConfiguration(externalSecret);

        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            Environment("Production"),
            _ => externalSecret);

        action.Should().NotThrow();
    }

    [Fact]
    public void HOST_02_TestingInjectedJwtSecretRegistersAuthentication()
    {
        var configuration = JwtConfiguration("testing-injected-signing-material-not-for-deployment-789");
        var services = new ServiceCollection();

        HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            Environment("Testing"),
            _ => null);
        services.AddAuthServices(configuration);

        services.Should().Contain(descriptor => descriptor.ServiceType.Name.Contains("Authentication"));
    }

    [Fact]
    public async Task GitHubProjectIntegration_EncryptsCredentialAndNeverReturnsIt()
    {
        const string credential = "github-test-credential-not-real";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var projectId = Guid.NewGuid();
        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Credential security test",
            Identifier = "CST",
            CreatorId = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new ProjectsController(
            Mock.Of<IProjectService>(),
            context,
            new EphemeralDataProtectionProvider());
        var updateResult = await controller.UpdateProjectIntegrations(projectId, new ProjectsController.UpdateProjectIntegrationsRequest
        {
            Items = new List<ProjectsController.ProjectIntegrationSetting>
            {
                new()
                {
                    Provider = "github",
                    DisplayName = "GitHub",
                    Enabled = true,
                    Secret = credential
                }
            }
        });

        updateResult.Should().BeOfType<OkObjectResult>();
        var stored = await context.SystemSettings.SingleAsync();
        stored.Value.Should().NotContain(credential);

        var getResult = await controller.GetProjectIntegrations(projectId);
        var getOkResult = getResult.Should().BeOfType<OkObjectResult>().Subject;
        var serializedResponse = JsonSerializer.Serialize(getOkResult.Value);
        serializedResponse.Should().NotContain(credential);
        serializedResponse.Should().Contain("\"Secret\":null");
    }

    private static IConfiguration JwtConfiguration(string? value) => Configuration(new Dictionary<string, string?>
    {
        ["Jwt:SecretKey"] = value,
        ["Jwt:Issuer"] = "SprintA-Test",
        ["Jwt:Audience"] = "SprintA-Test-Users",
        ["Features:AIEnabled"] = "false",
        ["Cors:AllowedOrigins:0"] = "https://example.invalid"
    });

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment Environment(string name) => new TestHostEnvironment { EnvironmentName = name };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "TaskManagement.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class P006MigrationDeploymentTests
{
    private const string PreviousMigration = "20260718003622_AddImmutableRewardLedger";

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task HOST_02_CleanDatabaseMigrationCreatesIntegrationSchema()
    {
        var options = SqlOptions($"TaskManagement_P006_Clean_{Guid.NewGuid():N}");
        await using var context = new ApplicationDbContext(options);
        try
        {
            await context.Database.MigrateAsync();
            (await TableExistsAsync(context, "IntegrationAccounts")).Should().BeTrue();
            (await TableExistsAsync(context, "InboxItems")).Should().BeTrue();
            (await TableExistsAsync(context, "SyncHistories")).Should().BeTrue();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task HOST_03_ExistingGuardTableAndDataSurviveAdoptionMigration()
    {
        var options = SqlOptions($"TaskManagement_P006_Existing_{Guid.NewGuid():N}");
        await using var context = new ApplicationDbContext(options);
        try
        {
            var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            context.Users.Add(new TaskManagement.Domain.Entities.User
            {
                Id = userId,
                Email = $"legacy-{userId:N}@example.com",
                PasswordHash = "unused",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.IntegrationAccounts.Add(new TaskManagement.Domain.Entities.IntegrationAccount
            {
                Id = id,
                UserId = userId,
                Provider = "legacy",
                AccountEmail = "legacy@example.com",
                AccessToken = "encrypted-token",
                Scopes = "scope"
            });
            await context.SaveChangesAsync();

            await migrator.MigrateAsync();
            (await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.IntegrationAccounts WHERE Id = {0}", id).SingleAsync())
                .Should().Be(1);
            (await TableExistsAsync(context, "InboxItems")).Should().BeTrue();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task HOST_02_MigrationCommandFailureIsNotSwallowed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory"
        }).Build();
        var services = new ServiceCollection();
        services.AddEnvironmentSafeDatabase(configuration, TestEnvironment("Testing"));
        await using var provider = services.BuildServiceProvider();
        var action = () => provider.RunDatabaseDeploymentCommandAsync(new[] { "--migrate" }, TestEnvironment("Testing"), configuration);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requires a relational database*");
    }

    [Fact]
    public async Task HOST_04_SeedFlagOffDoesNotSeed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Hosting:SeedDemoData"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddEnvironmentSafeDatabase(configuration, TestEnvironment("Testing"));
        await using var provider = services.BuildServiceProvider();
        (await provider.RunDatabaseDeploymentCommandAsync(Array.Empty<string>(), TestEnvironment("Testing"), configuration)).Should().BeFalse();
        await using var scope = provider.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync()).Should().Be(0);
    }

    private static DbContextOptions<ApplicationDbContext> SqlOptions(string databaseName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=KHOI\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30")
            .Options;

    private static async Task<bool> TableExistsAsync(ApplicationDbContext context, string tableName) =>
        await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name = {0}", tableName).SingleAsync() == 1;

    private static IHostEnvironment TestEnvironment(string name) => new MigrationHostEnvironment { EnvironmentName = name };

    private sealed class MigrationHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "TaskManagement.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
