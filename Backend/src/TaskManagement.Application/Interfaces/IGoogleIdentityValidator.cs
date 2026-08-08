using TaskManagement.Application.Auth;

namespace TaskManagement.Application.Interfaces;

public interface IGoogleIdentityValidator
{
    Task<GoogleIdentity> ValidateAsync(string credential, CancellationToken cancellationToken = default);
}
