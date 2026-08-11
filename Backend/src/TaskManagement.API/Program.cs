using TaskManagement.Application.Common;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
HostingConfigurationExtensions.ValidateEnvironmentConfiguration(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "data-protection-keys";
if (!Path.IsPathRooted(dataProtectionKeysPath))
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("SprintA");
builder.Services.AddHostedService<TaskManagement.API.Services.PrivateUploadCleanupService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfterSeconds = 60;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        context.HttpContext.Response.Headers["Retry-After"] =
            retryAfterSeconds.ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status429TooManyRequests,
            success = false,
            message = "Bạn đang thao tác AI quá nhanh. Vui lòng thử lại sau.",
            data = new
            {
                code = "AI_RATE_LIMITED",
                retryAfterSeconds
            }
        }, cancellationToken);
    };

    options.AddPolicy("AiGeneration", httpContext =>
    {
        var userKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var identity = !string.IsNullOrWhiteSpace(userKey)
            ? $"user:{userKey}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"ai-generation:{identity}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("AiAction", httpContext =>
    {
        var userKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var identity = !string.IsNullOrWhiteSpace(userKey)
            ? $"user:{userKey}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"ai-action:{identity}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("AiHeavy", httpContext =>
    {
        var userKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var identity = !string.IsNullOrWhiteSpace(userKey)
            ? $"user:{userKey}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"ai-heavy:{identity}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddAuthServices(builder.Configuration);
builder.Services.AddWorkspaceServices();
builder.Services.AddAuditLogServices();
builder.Services.AddEnvironmentSafeDatabase(builder.Configuration, builder.Environment);

const string corsPolicy = "SprintAOrigins";
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }
    else
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(origins);
    }
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("OpenApi:Enabled")) app.MapOpenApi();

if (app.Environment.IsProduction()) app.UseHsts();

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
{
    if (!System.Net.IPAddress.TryParse(proxy, out var address))
        throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains invalid IP '{proxy}'.");
    forwardedHeaders.KnownProxies.Add(address);
}
app.UseForwardedHeaders(forwardedHeaders);

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AiCreditsExhaustedException ex)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status402PaymentRequired,
            success = false,
            message = ex.Message,
            data = new
            {
                code = "AI_CREDITS_EXHAUSTED",
                includedCredits = ex.IncludedCredits,
                usedCredits = ex.UsedCredits,
                remainingCredits = ex.RemainingCredits
            }
        });
    }
    catch (AiProviderException ex)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        if (ex.RetryAfterSeconds is { } retryAfterSeconds)
        {
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        }

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status503ServiceUnavailable,
            success = false,
            message = ex.Message,
            data = new
            {
                code = ex.Kind == AiProviderErrorKind.RateLimited
                    ? "AI_PROVIDER_RATE_LIMITED"
                    : "AI_PROVIDER_UNAVAILABLE",
                retryAfterSeconds = ex.RetryAfterSeconds
            }
        });
    }
});
app.UseMiddleware<TaskManagement.API.Middlewares.PerformanceMiddleware>();
app.UseMiddleware<TaskManagement.API.Middlewares.IpWhitelistMiddleware>();
if (!app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Security:UseHttpsRedirection"))
    app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), geolocation=(), microphone=(self)";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});
app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseDefaultFiles();

MapPublicUploadDirectory("avatars");
MapPublicUploadDirectory("covers");
MapPublicUploadDirectory("project-covers");
app.UseWhen(context => context.Request.Path.StartsWithSegments("/uploads"), branch =>
    branch.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Not found.");
    }));
app.UseStaticFiles();

app.MapControllers();
app.MapHub<TaskManagement.API.Hubs.KanbanHub>("/kanban-hub");
app.MapHub<TaskManagement.API.Hubs.NotificationHub>("/notification-hub");
app.MapHub<TaskManagement.API.Hubs.ChatHub>(TaskManagement.API.Hubs.ChatHub.Route);

if (await app.Services.RunDatabaseDeploymentCommandAsync(args, app.Environment, builder.Configuration)) return;

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider
        .GetRequiredService<TaskManagement.Infrastructure.Data.ApplicationDbContext>();
    if (!context.Database.IsRelational())
    {
        await TaskManagement.Infrastructure.Data.DataSeeder.SeedMockDataAsync(context);
    }
}

app.MapFallbackToFile("index.html");
app.Run();

void MapPublicUploadDirectory(string directoryName)
{
    var path = Path.Combine(builder.Environment.ContentRootPath, "uploads", directoryName);
    Directory.CreateDirectory(path);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(path),
        RequestPath = $"/uploads/{directoryName}",
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Context.Response.Headers.CacheControl = "public,max-age=86400";
        }
    });
}

public partial class Program;
