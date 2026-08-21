using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentProvider _provider;
    private readonly IBillingService _billing;

    public PaymentsController(IPaymentProvider provider, IBillingService billing)
    {
        _provider = provider;
        _billing = billing;
    }

    [AllowAnonymous]
    [HttpPost("webhooks/sepay")]
    public async Task<IActionResult> SePayWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        var verification = await _provider.VerifyWebhookAsync(
            rawBody,
            Request.Headers["X-SePay-Signature"].FirstOrDefault(),
            Request.Headers["X-SePay-Timestamp"].FirstOrDefault(),
            cancellationToken);
        if (!verification.IsValid)
            return Unauthorized(new { success = false, message = verification.Error });
        await _billing.ProcessProviderPaymentAsync(_provider.Code, verification, rawBody, cancellationToken);
        return Ok(new { success = true });
    }
}
