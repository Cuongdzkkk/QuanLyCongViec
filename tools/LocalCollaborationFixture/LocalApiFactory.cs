using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LocalCollaborationFixture;

internal sealed class LocalApiFactory : WebApplicationFactory<global::Program>
{
    private const string Issuer = "SprintA-Local-Collaboration-Fixture";
    private const string Audience = "SprintA-Local-Collaboration-Clients";
    private readonly FixtureOptions _options;
    private readonly string _jwtSecret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly string _dataProtectionPath = Path.Combine(
        Path.GetTempPath(),
        $"sprinta-collab-fixture-keys-{Guid.NewGuid():N}");

    public LocalApiFactory(FixtureOptions options)
    {
        _options = options;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(FindApiContentRoot());
        builder.UseEnvironment(_options.EnvironmentName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _options.ConnectionString);
        builder.UseSetting("Jwt:SecretKey", _jwtSecret);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Security:RequireHttpsMetadata", "false");
        builder.UseSetting("Features:AIEnabled", "false");
        builder.UseSetting("Google:Enabled", "false");
        builder.UseSetting("OpenApi:Enabled", "false");
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting("Database:AllowDevelopmentInMemory", "false");
        builder.UseSetting("DataProtection:KeysPath", _dataProtectionPath);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _options.ConnectionString,
                ["Jwt:SecretKey"] = _jwtSecret,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Security:RequireHttpsMetadata"] = "false",
                ["Features:AIEnabled"] = "false",
                ["Google:Enabled"] = "false",
                ["OpenApi:Enabled"] = "false",
                ["Database:Provider"] = "SqlServer",
                ["Database:AllowDevelopmentInMemory"] = "false",
                ["DataProtection:KeysPath"] = _dataProtectionPath
            });
        });
    }

    private static string FindApiContentRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "Backend",
                "src",
                "TaskManagement.API");
            if (File.Exists(Path.Combine(candidate, "TaskManagement.API.csproj")))
                return candidate;
            current = current.Parent;
        }
        throw new InvalidOperationException("TaskManagement.API content root could not be located.");
    }

    public string CreateToken(Guid userId)
    {
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)),
            SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "Local collaboration fixture")
            ],
            notBefore: now.AddSeconds(-5),
            expires: now.AddMinutes(15),
            signingCredentials: credentials));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !Directory.Exists(_dataProtectionPath)) return;
        try
        {
            Directory.Delete(_dataProtectionPath, recursive: true);
        }
        catch (IOException)
        {
            // OS cleanup can complete after the process exits; no fixture data is stored here.
        }
        catch (UnauthorizedAccessException)
        {
            // The directory is temporary and contains only per-process test keys.
        }
    }
}
