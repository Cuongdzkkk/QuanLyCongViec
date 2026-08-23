using TaskManagement.Application.DTOs.AI;

namespace TaskManagement.Application.Interfaces;

public interface IAiCreditUsageService
{
    Task<AiCreditUsageDto> GetUsageAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task EnsureWithinQuotaAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsureLegacyCutoverAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AiCreditReservationResult> ReserveAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default);
    Task FinalizeAsync(Guid reservationId, int actualCredits, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task ExpireAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task ConsumeAsync(Guid userId, int credits, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}

public sealed record AiCreditReservationResult(Guid ReservationId, bool Acquired, string Status, int ReservedCredits);
