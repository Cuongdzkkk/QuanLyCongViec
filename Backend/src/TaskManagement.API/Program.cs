using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
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
    options.AddPolicy("FixedWindow", httpContext =>
    {
        var userKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = !string.IsNullOrWhiteSpace(userKey)
            ? $"user:{userKey}"
            : $"ip:{httpContext.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
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

if (await app.Services.RunDatabaseDeploymentCommandAsync(args, app.Environment, builder.Configuration)) return;

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
