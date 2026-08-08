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
}
