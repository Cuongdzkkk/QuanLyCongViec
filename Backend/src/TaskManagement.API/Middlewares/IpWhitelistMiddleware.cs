using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Threading.Tasks;
using TaskManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace TaskManagement.API.Middlewares
{
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "AppIpWhitelist";

        public IpWhitelistMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            // Always allow localhost/dev environment dynamically or you can omit this in prod
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(ipAddress))
            {
                await _next(context);
                return;
            }

            if (!_cache.TryGetValue(CacheKey, out List<string>? whitelistedIps))
            {
                whitelistedIps = new List<string>();
                try
                {
                    var tenantConfig = await dbContext.TenantConfigs.AsNoTracking().OrderBy(t => t.Id).FirstOrDefaultAsync();
                    if (tenantConfig != null && !string.IsNullOrEmpty(tenantConfig.IpWhitelist))
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(tenantConfig.IpWhitelist);
                            foreach (var item in jsonDoc.RootElement.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    var textIp = item.GetString();
                                    if (!string.IsNullOrWhiteSpace(textIp)) whitelistedIps.Add(textIp);
                                }
                                else if (item.ValueKind == JsonValueKind.Object &&
                                         item.TryGetProperty("ip", out var ipProp) &&
                                         ipProp.ValueKind == JsonValueKind.String)
                                {
                                    var textIp = ipProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(textIp)) whitelistedIps.Add(textIp);
                                }
                            }
                        }
                        catch
                        {
                            // Ignore malformed whitelist JSON in development/runtime fallback.
                        }
                    }
                }
                catch
                {
                    // If DB is unavailable, fail open so local development can still boot and serve requests.
                    await _next(context);
                    return;
                }

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                _cache.Set(CacheKey, whitelistedIps, cacheEntryOptions);
            }

            // If empty, it means no whitelist is configured (allow all)
            if (whitelistedIps != null && whitelistedIps.Count > 0)
            {
                if (!whitelistedIps.Contains(ipAddress) && ipAddress != "::1" && ipAddress != "127.0.0.1")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { statusCode = 403, message = "Access Denied: Your IP is not whitelisted by the organization." });
                    return;
                }
            }

            await _next(context);
        }
    }
}
