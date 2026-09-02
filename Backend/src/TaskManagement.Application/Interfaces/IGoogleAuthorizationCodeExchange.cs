namespace TaskManagement.Application.Interfaces;

public interface IGoogleAuthorizationCodeExchange
{
    Task<string> ExchangeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken cancellationToken = default);
}
