namespace TaskManagement.Application.Interfaces;

public interface IOAuthStateStore
{
    void Store(string nonce, Guid userId, string provider, string codeVerifier, DateTime expiresAt);

    bool TryConsume(string nonce, Guid userId, string provider, out string codeVerifier);
}
