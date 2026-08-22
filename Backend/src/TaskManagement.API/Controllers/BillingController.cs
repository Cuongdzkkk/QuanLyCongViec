using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public sealed class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.GetSummaryAsync(GetUserId(), cancellationToken)));

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.GetOrdersAsync(GetUserId(), null, cancellationToken)));

    [HttpGet("orders/history")]
    public async Task<IActionResult> OrderHistory([FromQuery] BillingOrderQuery query, CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.SearchOrdersAsync(GetUserId(), query, cancellationToken)));

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> OrderDetails(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.GetOrderDetailsAsync(id, GetUserId(), false, cancellationToken))); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
    }

    [HttpGet("orders/{id:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.GetReceiptAsync(id, GetUserId(), false, cancellationToken))); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
    }

    [HttpPost("orders/{id:guid}/receipt/resend")]
    public async Task<IActionResult> ResendReceipt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.ResendReceiptAsync(id, GetUserId(), false, cancellationToken), "Đã yêu cầu gửi lại receipt.")); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
        catch (InvalidOperationException ex) { return Conflict(Error(ex.Message, 409)); }
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreatePaymentOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _billingService.CreateOrderAsync(GetUserId(), request.PlanCode, cancellationToken);
            return Ok(ApiEnvelope(order, "Đã tạo đơn thanh toán. Vui lòng chuyển khoản đúng nội dung."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Error(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Error(ex.Message, 409));
        }
    }

    [HttpPost("free/activate")]
    public async Task<IActionResult> ActivateFree(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(ApiEnvelope(
                await _billingService.ActivateFreeAsync(GetUserId(), cancellationToken),
                "Đã kích hoạt gói Free."));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(Error(ex.Message));
        }
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : throw new UnauthorizedAccessException("Invalid user identity.");

    private static object ApiEnvelope(object data, string message = "Success") => new { statusCode = 200, message, data };
    private static object Error(string message, int statusCode = 400) => new { statusCode, message };
}
