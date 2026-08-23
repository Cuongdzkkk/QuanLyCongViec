using TaskManagement.Application.DTOs.Billing;

namespace TaskManagement.Application.Interfaces;

public interface IBillingService
{
    Task<BillingSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrderDto>> GetOrdersAsync(Guid? userId, string? status, CancellationToken cancellationToken = default);
    Task<PaymentOrderPageDto> SearchOrdersAsync(Guid? userId, BillingOrderQuery query, CancellationToken cancellationToken = default);
    Task<PaymentOrderDetailsDto> GetOrderDetailsAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<PaymentReceiptDto> GetReceiptAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<PaymentEmailDeliveryDto> ResendReceiptAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<PaymentOrderDto> CreateOrderAsync(Guid userId, string planCode, CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> ActivateFreeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> ChangePlanAsync(Guid userId, string planCode, bool autoRenew, Guid adminUserId, string? reason, CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> ExtendAsync(Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> CancelAsync(Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> AddAdjustmentAsync(Guid userId, int amount, Guid adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<BillingSummaryDto> ResetCurrentPeriodUsageAsync(Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<PaymentOrderDto> ApproveOrderAsync(Guid orderId, Guid adminUserId, string? note, CancellationToken cancellationToken = default);
    Task<PaymentOrderDto> RejectOrderAsync(Guid orderId, Guid adminUserId, string reason, CancellationToken cancellationToken = default);
    Task<PaymentOrderDto?> ProcessProviderPaymentAsync(string provider, PaymentWebhookVerificationResult webhook, string rawPayload, CancellationToken cancellationToken = default);
}
