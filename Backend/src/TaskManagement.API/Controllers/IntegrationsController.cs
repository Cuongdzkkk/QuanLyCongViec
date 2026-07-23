using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/integrations")]
    [Authorize]
    public class IntegrationsController : ControllerBase
    {
        private const string GoogleCalendarProvider = "google-calendar";
        private const string GmailProvider = "gmail";
        private const string SlackProvider = "slack";
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDataProtector _protector;

        public IntegrationsController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _protector = dataProtectionProvider.CreateProtector("SprintA.IntegrationTokens");
        }

        [HttpGet]
        public async Task<IActionResult> GetIntegrations()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var accounts = await _context.IntegrationAccounts
                .AsNoTracking()
                .Where(account => account.UserId == userId.Value && account.IsActive)
                .ToListAsync();

            var google = accounts.FirstOrDefault(account => account.Provider == GoogleCalendarProvider);
            var gmail = accounts.FirstOrDefault(account => account.Provider == GmailProvider);
            var slack = accounts.FirstOrDefault(account => account.Provider == SlackProvider);
            var googleConfigured = IsProviderConfigured(GoogleCalendarProvider);
            var gmailConfigured = IsProviderConfigured(GmailProvider);
            var slackConfigured = IsProviderConfigured(SlackProvider);
            var histories = await _context.SyncHistories
                .AsNoTracking()
                .Where(history => history.UserId == userId.Value)
                .OrderByDescending(history => history.StartedAt)
                .Take(10)
                .Select(history => new
                {
                    history.Id,
                    history.Provider,
                    history.Status,
                    history.ItemsImported,
                    history.Message,
                    history.StartedAt,
                    history.CompletedAt
                })
                .ToListAsync();

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    providers = new object[]
                    {
                        new
                        {
                            id = google?.Id,
                            provider = GoogleCalendarProvider,
                            name = "Google Calendar",
                            source = "calendar",
                            status = google == null ? "not_connected" : "connected",
                            accountEmail = google?.AccountEmail,
                            lastSyncedAt = google?.LastSyncedAt,
                            connectedAt = google?.CreatedAt,
                            supportsConnect = googleConfigured,
                            supportsSync = true
                        },
                        new
                        {
                            id = gmail?.Id,
                            provider = GmailProvider,
                            name = "Gmail",
                            source = "email",
                            status = gmail == null ? "not_connected" : "connected",
                            accountEmail = gmail?.AccountEmail,
                            lastSyncedAt = gmail?.LastSyncedAt,
                            connectedAt = gmail?.CreatedAt,
                            supportsConnect = gmailConfigured,
                            supportsSync = true
                        },
                        new
                        {
                            id = slack?.Id,
                            provider = SlackProvider,
                            name = "Slack",
                            source = "slack",
                            status = slack == null ? "not_connected" : "connected",
                            accountEmail = slack?.AccountEmail,
                            lastSyncedAt = slack?.LastSyncedAt,
                            connectedAt = slack?.CreatedAt,
                            supportsConnect = slackConfigured,
                            supportsSync = true
                        }
                    },
                    syncHistory = histories
                }
            });
        }

        [HttpGet("google-calendar/connect")]
        public IActionResult ConnectGoogleCalendar()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var config = GetOAuthConfig(GoogleCalendarProvider);
            var clientId = config.ClientId;
            var clientSecret = config.ClientSecret;
            var redirectUri = _configuration["IntegrationOAuth:GoogleCalendar:RedirectUri"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            {
                return BadRequest(new { message = "Google Calendar OAuth chưa được cấu hình đầy đủ" });
            }

            var stateJson = JsonSerializer.Serialize(new
            {
                userId = userId.Value,
                provider = GoogleCalendarProvider,
                nonce = Guid.NewGuid(),
                createdAt = DateTime.UtcNow
            });
            var state = Base64UrlEncode(Encoding.UTF8.GetBytes(stateJson));
            var scope = Uri.EscapeDataString("openid email profile https://www.googleapis.com/auth/calendar.readonly");
            var url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                $"&scope={scope}" +
                "&access_type=offline" +
                "&prompt=consent" +
                $"&state={Uri.EscapeDataString(state)}";

            return Ok(new { statusCode = 200, data = new { authorizationUrl = url } });
        }

        [HttpGet("gmail/connect")]
        public IActionResult ConnectGmail()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var config = GetOAuthConfig(GmailProvider);
            if (!config.IsConfigured)
            {
                return BadRequest(new { message = "Gmail OAuth chưa được cấu hình đầy đủ" });
            }

            var state = BuildOAuthState(userId.Value, GmailProvider);
            var scope = Uri.EscapeDataString("openid email profile https://www.googleapis.com/auth/gmail.readonly");
            var url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(config.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
                "&response_type=code" +
                $"&scope={scope}" +
                "&access_type=offline" +
                "&prompt=consent" +
                $"&state={Uri.EscapeDataString(state)}";

            return Ok(new { statusCode = 200, data = new { authorizationUrl = url } });
        }

        [HttpGet("slack/connect")]
        public IActionResult ConnectSlack()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var config = GetOAuthConfig(SlackProvider);
            if (!config.IsConfigured)
            {
                return BadRequest(new { message = "Slack OAuth chưa được cấu hình đầy đủ" });
            }

            var state = BuildOAuthState(userId.Value, SlackProvider);
            var userScope = Uri.EscapeDataString("channels:read,channels:history,groups:read,groups:history,im:read,im:history,mpim:read,mpim:history,users:read,team:read");
            var url =
                "https://slack.com/oauth/v2/authorize" +
                $"?client_id={Uri.EscapeDataString(config.ClientId)}" +
                $"&user_scope={userScope}" +
                $"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
                $"&state={Uri.EscapeDataString(state)}";

            return Ok(new { statusCode = 200, data = new { authorizationUrl = url } });
        }

        [HttpGet("google-calendar/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCalendarCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription)
        {
            var frontendUrl = GetFrontendIntegrationUrl();
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", errorDescription ?? error));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", "Thiếu mã xác thực từ Google"));
            }

            var statePayload = DecodeState(state);
            if (statePayload == null || statePayload.UserId == Guid.Empty || statePayload.Provider != GoogleCalendarProvider)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", "OAuth state không hợp lệ"));
            }

            var userId = statePayload.UserId;

            var googleConfig = GetOAuthConfig(GoogleCalendarProvider);
            var clientId = googleConfig.ClientId;
            var clientSecret = googleConfig.ClientSecret;
            var redirectUri = googleConfig.RedirectUri;

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", "Google Calendar OAuth chưa được cấu hình đầy đủ"));
            }

            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }));

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", "Không đổi được Google OAuth token"));
            }

            var token = JsonSerializer.Deserialize<GoogleTokenResponse>(tokenJson);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, "error", "Google không trả access token hợp lệ"));
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            var userInfoResponse = await client.GetAsync("https://openidconnect.googleapis.com/v1/userinfo");
            var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfo = userInfoResponse.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<GoogleUserInfoResponse>(userInfoJson)
                : null;

            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.UserId == userId && item.Provider == GoogleCalendarProvider);

            if (account == null)
            {
                account = new IntegrationAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = GoogleCalendarProvider,
                    CreatedAt = DateTime.UtcNow
                };
                _context.IntegrationAccounts.Add(account);
            }

            account.AccountEmail = userInfo?.Email ?? account.AccountEmail;
            account.ExternalAccountId = userInfo?.Sub ?? account.ExternalAccountId;
            account.AccessToken = ProtectToken(token.AccessToken);
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                account.RefreshToken = ProtectToken(token.RefreshToken);
            }
            account.AccessTokenExpiresAt = token.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60) : null;
            account.Scopes = token.Scope ?? string.Empty;
            account.IsActive = true;
            account.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Redirect(BuildFrontendRedirect(frontendUrl, "success", null));
        }

        [HttpGet("gmail/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GmailCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription)
            => await GoogleOAuthCallback(GmailProvider, "Gmail", code, state, error, errorDescription);

        [HttpGet("slack/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> SlackCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error)
        {
            var frontendUrl = GetFrontendIntegrationUrl();
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "error", error));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "error", "Thiếu mã xác thực từ Slack"));
            }

            var statePayload = DecodeState(state);
            if (statePayload == null || statePayload.UserId == Guid.Empty || statePayload.Provider != SlackProvider)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "error", "OAuth state không hợp lệ"));
            }

            var config = GetOAuthConfig(SlackProvider);
            if (!config.IsConfigured)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "error", "Slack OAuth chưa được cấu hình đầy đủ"));
            }

            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync("https://slack.com/api/oauth.v2.access", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["redirect_uri"] = config.RedirectUri
            }));

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<SlackOAuthResponse>(tokenJson);
            var accessToken = token?.AccessToken;
            var scopes = token?.Scope;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                accessToken = token?.AuthedUser?.AccessToken;
                scopes = token?.AuthedUser?.Scope ?? scopes;
            }

            if (!tokenResponse.IsSuccessStatusCode || token == null || !token.Ok || string.IsNullOrWhiteSpace(accessToken))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "error", token?.Error ?? "Không đổi được Slack OAuth token"));
            }

            await UpsertIntegrationAccountAsync(
                statePayload.UserId,
                SlackProvider,
                token.Team?.Name ?? "Slack workspace",
                token.Team?.Id,
                accessToken,
                null,
                null,
                scopes);

            await _context.SaveChangesAsync();
            return Redirect(BuildFrontendRedirect(frontendUrl, SlackProvider, "success", null));
        }

        [HttpPost("google-calendar/sync")]
        public async Task<IActionResult> SyncGoogleCalendar()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.UserId == userId.Value && item.Provider == GoogleCalendarProvider && item.IsActive);

            if (account == null)
            {
                return BadRequest(new { message = "Google Calendar chưa kết nối" });
            }

            var history = new SyncHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                IntegrationAccountId = account.Id,
                Provider = GoogleCalendarProvider,
                Status = "running",
                StartedAt = DateTime.UtcNow
            };
            _context.SyncHistories.Add(history);
            await _context.SaveChangesAsync();

            try
            {
                await EnsureGoogleAccessTokenAsync(account);

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DecryptToken(account.AccessToken));
                var timeMin = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-7).ToString("O"));
                var timeMax = Uri.EscapeDataString(DateTime.UtcNow.AddDays(30).ToString("O"));
                var eventsResponse = await client.GetAsync(
                    $"https://www.googleapis.com/calendar/v3/calendars/primary/events?singleEvents=true&orderBy=startTime&timeMin={timeMin}&timeMax={timeMax}");

                var eventsJson = await eventsResponse.Content.ReadAsStringAsync();
                if (!eventsResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(eventsJson);
                }

                var calendarEvents = JsonSerializer.Deserialize<GoogleCalendarEventsResponse>(eventsJson);
                var imported = 0;
                foreach (var calendarEvent in calendarEvents?.Items ?? new List<GoogleCalendarEvent>())
                {
                    if (string.IsNullOrWhiteSpace(calendarEvent.Id)) continue;

                    var externalId = calendarEvent.Id;
                    var item = await _context.InboxItems
                        .FirstOrDefaultAsync(existing => existing.UserId == userId.Value
                            && existing.Provider == GoogleCalendarProvider
                            && existing.ExternalId == externalId);

                    if (item == null)
                    {
                        item = new InboxItem
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId.Value,
                            IntegrationAccountId = account.Id,
                            Source = "calendar",
                            Provider = GoogleCalendarProvider,
                            ExternalId = externalId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.InboxItems.Add(item);
                    }

                    item.Title = string.IsNullOrWhiteSpace(calendarEvent.Summary)
                        ? "Sự kiện Google Calendar"
                        : calendarEvent.Summary;
                    item.Content = calendarEvent.Description;
                    item.Location = calendarEvent.Location;
                    item.StartsAt = ParseGoogleDate(calendarEvent.Start);
                    item.EndsAt = ParseGoogleDate(calendarEvent.End);
                    item.UpdatedAt = DateTime.UtcNow;
                    imported += 1;
                }

                account.LastSyncedAt = DateTime.UtcNow;
                account.UpdatedAt = DateTime.UtcNow;
                history.Status = "success";
                history.ItemsImported = imported;
                history.Message = $"Đã đồng bộ {imported} sự kiện Google Calendar";
                history.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { statusCode = 200, data = new { imported, account.LastSyncedAt } });
            }
            catch (Exception ex)
            {
                history.Status = "error";
                history.Message = ex.Message;
                history.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return StatusCode(502, new { message = "Không đồng bộ được Google Calendar", detail = ex.Message });
            }
        }

        [HttpPost("gmail/sync")]
        public async Task<IActionResult> SyncGmail()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.UserId == userId.Value && item.Provider == GmailProvider && item.IsActive);

            if (account == null)
            {
                return BadRequest(new { message = "Gmail chưa kết nối" });
            }

            var history = CreateSyncHistory(userId.Value, account, GmailProvider);
            _context.SyncHistories.Add(history);
            await _context.SaveChangesAsync();

            try
            {
                await EnsureGoogleAccessTokenAsync(account, GmailProvider, "Gmail");

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DecryptToken(account.AccessToken));
                var listResponse = await client.GetAsync("https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=25&q=newer_than:30d");
                var listJson = await listResponse.Content.ReadAsStringAsync();
                if (!listResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(listJson);
                }

                var list = JsonSerializer.Deserialize<GmailMessagesResponse>(listJson);
                var imported = 0;
                foreach (var messageRef in list?.Messages ?? new List<GmailMessageRef>())
                {
                    if (string.IsNullOrWhiteSpace(messageRef.Id)) continue;

                    var detailResponse = await client.GetAsync(
                        $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageRef.Id)}?format=metadata&metadataHeaders=Subject&metadataHeaders=From");
                    var detailJson = await detailResponse.Content.ReadAsStringAsync();
                    if (!detailResponse.IsSuccessStatusCode) continue;

                    var message = JsonSerializer.Deserialize<GmailMessage>(detailJson);
                    if (message == null || string.IsNullOrWhiteSpace(message.Id)) continue;

                    var subject = message.Payload?.Headers?.FirstOrDefault(header => header.Name.Equals("Subject", StringComparison.OrdinalIgnoreCase))?.Value;
                    var from = message.Payload?.Headers?.FirstOrDefault(header => header.Name.Equals("From", StringComparison.OrdinalIgnoreCase))?.Value;
                    var item = await _context.InboxItems
                        .FirstOrDefaultAsync(existing => existing.UserId == userId.Value
                            && existing.Provider == GmailProvider
                            && existing.ExternalId == message.Id);

                    if (item == null)
                    {
                        item = new InboxItem
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId.Value,
                            IntegrationAccountId = account.Id,
                            Source = "email",
                            Provider = GmailProvider,
                            ExternalId = message.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.InboxItems.Add(item);
                    }

                    item.Title = string.IsNullOrWhiteSpace(subject) ? "Gmail message" : subject;
                    item.Content = string.Join(Environment.NewLine, new[] { from, message.Snippet }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    item.StartsAt = ParseUnixMilliseconds(message.InternalDate);
                    item.UpdatedAt = DateTime.UtcNow;
                    imported += 1;
                }

                CompleteSync(account, history, imported, $"Đã đồng bộ {imported} email Gmail");
                await _context.SaveChangesAsync();
                return Ok(new { statusCode = 200, data = new { imported, account.LastSyncedAt } });
            }
            catch (Exception ex)
            {
                FailSync(history, ex);
                await _context.SaveChangesAsync();
                return StatusCode(502, new { message = "Không đồng bộ được Gmail", detail = ex.Message });
            }
        }

        [HttpPost("slack/sync")]
        public async Task<IActionResult> SyncSlack()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.UserId == userId.Value && item.Provider == SlackProvider && item.IsActive);

            if (account == null)
            {
                return BadRequest(new { message = "Slack chưa kết nối" });
            }

            var history = CreateSyncHistory(userId.Value, account, SlackProvider);
            _context.SyncHistories.Add(history);
            await _context.SaveChangesAsync();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DecryptToken(account.AccessToken));
                var channelsResponse = await client.GetAsync("https://slack.com/api/conversations.list?types=public_channel,private_channel,im,mpim&limit=20");
                var channelsJson = await channelsResponse.Content.ReadAsStringAsync();
                var channels = JsonSerializer.Deserialize<SlackConversationsResponse>(channelsJson);
                if (!channelsResponse.IsSuccessStatusCode || channels == null || !channels.Ok)
                {
                    throw new InvalidOperationException(channels?.Error ?? channelsJson);
                }

                var imported = 0;
                foreach (var channel in channels.Channels.Where(channel => !string.IsNullOrWhiteSpace(channel.Id)).Take(10))
                {
                    var historyResponse = await client.GetAsync($"https://slack.com/api/conversations.history?channel={Uri.EscapeDataString(channel.Id)}&limit=10");
                    var historyJson = await historyResponse.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<SlackMessagesResponse>(historyJson);
                    if (!historyResponse.IsSuccessStatusCode || messages == null || !messages.Ok) continue;

                    foreach (var message in messages.Messages.Where(message => !string.IsNullOrWhiteSpace(message.Ts)))
                    {
                        var externalId = $"{channel.Id}:{message.Ts}";
                        var item = await _context.InboxItems
                            .FirstOrDefaultAsync(existing => existing.UserId == userId.Value
                                && existing.Provider == SlackProvider
                                && existing.ExternalId == externalId);

                        if (item == null)
                        {
                            item = new InboxItem
                            {
                                Id = Guid.NewGuid(),
                                UserId = userId.Value,
                                IntegrationAccountId = account.Id,
                                Source = "slack",
                                Provider = SlackProvider,
                                ExternalId = externalId,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.InboxItems.Add(item);
                        }

                        item.Title = string.IsNullOrWhiteSpace(channel.Name) ? "Slack message" : $"Slack · #{channel.Name}";
                        item.Content = message.Text;
                        item.StartsAt = ParseSlackTimestamp(message.Ts);
                        item.UpdatedAt = DateTime.UtcNow;
                        imported += 1;
                    }
                }

                CompleteSync(account, history, imported, $"Đã đồng bộ {imported} tin nhắn Slack");
                await _context.SaveChangesAsync();
                return Ok(new { statusCode = 200, data = new { imported, account.LastSyncedAt } });
            }
            catch (Exception ex)
            {
                FailSync(history, ex);
                await _context.SaveChangesAsync();
                return StatusCode(502, new { message = "Không đồng bộ được Slack", detail = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Disconnect(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value);

            if (account == null) return NotFound(new { message = "Không tìm thấy kết nối" });

            account.IsActive = false;
            account.AccessToken = string.Empty;
            account.RefreshToken = null;
            account.AccessTokenExpiresAt = null;
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Đã ngắt kết nối" });
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private bool IsProviderConfigured(string provider)
        {
            var config = GetOAuthConfig(provider);
            return config.IsConfigured;
        }

        private OAuthProviderConfig GetOAuthConfig(string provider)
        {
            var sectionName = provider switch
            {
                GoogleCalendarProvider => "GoogleCalendar",
                GmailProvider => "Gmail",
                SlackProvider => "Slack",
                _ => provider
            };

            var clientId = _configuration[$"IntegrationOAuth:{sectionName}:ClientId"];
            var clientSecret = _configuration[$"IntegrationOAuth:{sectionName}:ClientSecret"];

            if (provider == GmailProvider)
            {
                clientId = FirstConfigured(clientId, _configuration["IntegrationOAuth:GoogleCalendar:ClientId"], _configuration["Google:ClientId"]);
                clientSecret = FirstConfigured(clientSecret, _configuration["IntegrationOAuth:GoogleCalendar:ClientSecret"]);
            }

            var redirectUri = _configuration[$"IntegrationOAuth:{sectionName}:RedirectUri"];
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                redirectUri = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";
            }

            return new OAuthProviderConfig(clientId ?? string.Empty, clientSecret ?? string.Empty, redirectUri);
        }

        private static string? FirstConfigured(params string?[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private static string BuildOAuthState(Guid userId, string provider)
        {
            var stateJson = JsonSerializer.Serialize(new
            {
                userId,
                provider,
                nonce = Guid.NewGuid(),
                createdAt = DateTime.UtcNow
            });
            return Base64UrlEncode(Encoding.UTF8.GetBytes(stateJson));
        }

        private async Task<IActionResult> GoogleOAuthCallback(string provider, string displayName, string code, string state, string? error, string? errorDescription)
        {
            var frontendUrl = GetFrontendIntegrationUrl();
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", errorDescription ?? error));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", $"Thiếu mã xác thực từ {displayName}"));
            }

            var statePayload = DecodeState(state);
            if (statePayload == null || statePayload.UserId == Guid.Empty || statePayload.Provider != provider)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", "OAuth state không hợp lệ"));
            }

            var config = GetOAuthConfig(provider);
            if (!config.IsConfigured)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", $"{displayName} OAuth chưa được cấu hình đầy đủ"));
            }

            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["redirect_uri"] = config.RedirectUri,
                ["grant_type"] = "authorization_code"
            }));

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", $"Không đổi được {displayName} OAuth token"));
            }

            var token = JsonSerializer.Deserialize<GoogleTokenResponse>(tokenJson);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return Redirect(BuildFrontendRedirect(frontendUrl, provider, "error", $"{displayName} không trả access token hợp lệ"));
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            var userInfoResponse = await client.GetAsync("https://openidconnect.googleapis.com/v1/userinfo");
            var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfo = userInfoResponse.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<GoogleUserInfoResponse>(userInfoJson)
                : null;

            await UpsertIntegrationAccountAsync(
                statePayload.UserId,
                provider,
                userInfo?.Email ?? displayName,
                userInfo?.Sub,
                token.AccessToken,
                token.RefreshToken,
                token.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60) : null,
                token.Scope);

            await _context.SaveChangesAsync();
            return Redirect(BuildFrontendRedirect(frontendUrl, provider, "success", null));
        }

        private async Task<IntegrationAccount> UpsertIntegrationAccountAsync(
            Guid userId,
            string provider,
            string accountEmail,
            string? externalAccountId,
            string accessToken,
            string? refreshToken,
            DateTime? accessTokenExpiresAt,
            string? scopes)
        {
            var account = await _context.IntegrationAccounts
                .FirstOrDefaultAsync(item => item.UserId == userId && item.Provider == provider);

            if (account == null)
            {
                account = new IntegrationAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = provider,
                    CreatedAt = DateTime.UtcNow
                };
                _context.IntegrationAccounts.Add(account);
            }

            account.AccountEmail = accountEmail;
            account.ExternalAccountId = externalAccountId ?? account.ExternalAccountId;
            account.AccessToken = ProtectToken(accessToken);
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                account.RefreshToken = ProtectToken(refreshToken);
            }
            account.AccessTokenExpiresAt = accessTokenExpiresAt;
            account.Scopes = scopes ?? string.Empty;
            account.IsActive = true;
            account.UpdatedAt = DateTime.UtcNow;
            return account;
        }

        private static SyncHistory CreateSyncHistory(Guid userId, IntegrationAccount account, string provider)
            => new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                IntegrationAccountId = account.Id,
                Provider = provider,
                Status = "running",
                StartedAt = DateTime.UtcNow
            };

        private static void CompleteSync(IntegrationAccount account, SyncHistory history, int imported, string message)
        {
            account.LastSyncedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;
            history.Status = "success";
            history.ItemsImported = imported;
            history.Message = message;
            history.CompletedAt = DateTime.UtcNow;
        }

        private static void FailSync(SyncHistory history, Exception ex)
        {
            history.Status = "error";
            history.Message = ex.Message;
            history.CompletedAt = DateTime.UtcNow;
        }

        private async Task EnsureGoogleAccessTokenAsync(IntegrationAccount account, string provider = GoogleCalendarProvider, string displayName = "Google Calendar")
        {
            if (!string.IsNullOrWhiteSpace(DecryptToken(account.AccessToken))
                && (!account.AccessTokenExpiresAt.HasValue || account.AccessTokenExpiresAt.Value > DateTime.UtcNow))
            {
                return;
            }

            var decRefreshToken = DecryptToken(account.RefreshToken);
            if (string.IsNullOrWhiteSpace(decRefreshToken))
            {
                throw new InvalidOperationException("Google token đã hết hạn, vui lòng kết nối lại");
            }

            var clientId = _configuration["IntegrationOAuth:GoogleCalendar:ClientId"];
            var clientSecret = _configuration["IntegrationOAuth:GoogleCalendar:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("Google Calendar OAuth chưa được cấu hình đầy đủ");
            }

            var client = _httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = decRefreshToken,
                ["grant_type"] = "refresh_token"
            }));

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            if (!tokenResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(tokenJson);
            }

            var token = JsonSerializer.Deserialize<GoogleTokenResponse>(tokenJson);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("Google không trả access token hợp lệ");
            }

            account.AccessToken = ProtectToken(token.AccessToken);
            account.AccessTokenExpiresAt = token.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60) : null;
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private static DateTime? ParseGoogleDate(GoogleDateTime? value)
        {
            if (value == null) return null;
            if (!string.IsNullOrWhiteSpace(value.DateTime) && DateTimeOffset.TryParse(value.DateTime, out var dto))
            {
                return dto.UtcDateTime;
            }
            if (!string.IsNullOrWhiteSpace(value.Date) && DateTime.TryParse(value.Date, out var date))
            {
                return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            }
            return null;
        }

        private static DateTime? ParseUnixMilliseconds(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !long.TryParse(value, out var milliseconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        }

        private static DateTime? ParseSlackTimestamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000)).UtcDateTime;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private string GetFrontendIntegrationUrl()
        {
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                frontendBaseUrl = "http://localhost:5173";
            }

            return $"{frontendBaseUrl}/integrations";
        }

        private string ProtectToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            return _protector.Protect(token);
        }

        private string DecryptToken(string? protectedToken)
        {
            if (string.IsNullOrEmpty(protectedToken)) return string.Empty;
            try
            {
                return _protector.Unprotect(protectedToken);
            }
            catch
            {
                // Fallback cho token cũ dạng plaintext
                return protectedToken;
            }
        }

        private static string BuildFrontendRedirect(string frontendUrl, string status, string? message)
            => BuildFrontendRedirect(frontendUrl, GoogleCalendarProvider, status, message);

        private static string BuildFrontendRedirect(string frontendUrl, string provider, string status, string? message)
        {
            var query = new List<string>
            {
                $"provider={Uri.EscapeDataString(provider)}",
                status == "success" ? "connected=success" : "connected=error"
            };

            if (!string.IsNullOrWhiteSpace(message))
            {
                query.Add($"message={Uri.EscapeDataString(message)}");
            }

            return $"{frontendUrl}?{string.Join("&", query)}";
        }

        private static OAuthState? DecodeState(string state)
        {
            try
            {
                var padded = state.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
                return JsonSerializer.Deserialize<OAuthState>(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            }
            catch
            {
                return null;
            }
        }

        private sealed record OAuthProviderConfig(string ClientId, string ClientSecret, string RedirectUri)
        {
            public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId)
                && !string.IsNullOrWhiteSpace(ClientSecret)
                && !string.IsNullOrWhiteSpace(RedirectUri);
        }

        private sealed class OAuthState
        {
            [JsonPropertyName("userId")]
            public Guid UserId { get; set; }

            [JsonPropertyName("provider")]
            public string Provider { get; set; } = string.Empty;
        }

        private sealed class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }

        private sealed class GoogleUserInfoResponse
        {
            [JsonPropertyName("sub")]
            public string? Sub { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }
        }

        private sealed class GmailMessagesResponse
        {
            [JsonPropertyName("messages")]
            public List<GmailMessageRef> Messages { get; set; } = new();
        }

        private sealed class GmailMessageRef
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
        }

        private sealed class GmailMessage
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("snippet")]
            public string? Snippet { get; set; }

            [JsonPropertyName("internalDate")]
            public string? InternalDate { get; set; }

            [JsonPropertyName("payload")]
            public GmailPayload? Payload { get; set; }
        }

        private sealed class GmailPayload
        {
            [JsonPropertyName("headers")]
            public List<GmailHeader> Headers { get; set; } = new();
        }

        private sealed class GmailHeader
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("value")]
            public string? Value { get; set; }
        }

        private sealed class SlackOAuthResponse
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("team")]
            public SlackTeam? Team { get; set; }

            [JsonPropertyName("authed_user")]
            public SlackAuthedUser? AuthedUser { get; set; }
        }

        private sealed class SlackAuthedUser
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }

            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        private sealed class SlackTeam
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class SlackConversationsResponse
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("channels")]
            public List<SlackChannel> Channels { get; set; } = new();
        }

        private sealed class SlackChannel
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class SlackMessagesResponse
        {
            [JsonPropertyName("ok")]
            public bool Ok { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("messages")]
            public List<SlackMessage> Messages { get; set; } = new();
        }

        private sealed class SlackMessage
        {
            [JsonPropertyName("ts")]
            public string Ts { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        private sealed class GoogleCalendarEventsResponse
        {
            [JsonPropertyName("items")]
            public List<GoogleCalendarEvent> Items { get; set; } = new();
        }

        private sealed class GoogleCalendarEvent
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("summary")]
            public string? Summary { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("location")]
            public string? Location { get; set; }

            [JsonPropertyName("start")]
            public GoogleDateTime? Start { get; set; }

            [JsonPropertyName("end")]
            public GoogleDateTime? End { get; set; }
        }

        private sealed class GoogleDateTime
        {
            [JsonPropertyName("dateTime")]
            public string? DateTime { get; set; }

            [JsonPropertyName("date")]
            public string? Date { get; set; }
        }
    }
}
