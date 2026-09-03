namespace TaskManagement.Application.Interfaces;

public interface IGoogleLoginOAuthStateStore
{
    void Store(string state, DateTime expiresAt);

    bool TryConsume(string state);
}
