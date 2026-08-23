using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TaskManagement.API.Hubs;

public sealed class CallHubInvocationDiagnosticsFilter : IHubFilter
{
    private readonly ILogger<CallHubInvocationDiagnosticsFilter> _logger;

    public CallHubInvocationDiagnosticsFilter(ILogger<CallHubInvocationDiagnosticsFilter> logger) => _logger = logger;

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception exception)
        {
            if (invocationContext.Hub is not CallHub)
                throw;
            _logger.LogError(
                exception,
                "[CALL_HUB_SERVER] event=INVOCATION_EXCEPTION method={Method} connectionId={ConnectionId} exceptionType={ExceptionType} safeMessage={SafeMessage}",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId,
                exception.GetType().FullName,
                SafeExceptionMessage(exception));
            throw;
        }
    }

    private static string SafeExceptionMessage(Exception exception)
    {
        var message = exception.Message.Replace("\r", " ").Replace("\n", " ");
        var accessTokenIndex = message.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase);
        if (accessTokenIndex >= 0)
        {
            var end = message.IndexOfAny(new[] { '&', ' ', '"' }, accessTokenIndex);
            message = message[..accessTokenIndex] + "access_token=[redacted]" + (end >= 0 ? message[end..] : string.Empty);
        }
        return message.Length <= 256 ? message : message[..256];
    }
}
