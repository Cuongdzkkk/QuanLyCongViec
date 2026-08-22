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
    Task<AiCreditReservationResult> ReserveDetailedAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Guid> ReserveAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default);
    Task FinalizeReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
}

public sealed record AiCreditReservationResult(Guid ReservationId, bool Acquired, string Status);
