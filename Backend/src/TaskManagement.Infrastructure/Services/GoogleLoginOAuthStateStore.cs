using System.Collections.Concurrent;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class GoogleLoginOAuthStateStore : IGoogleLoginOAuthStateStore
{
    private readonly ConcurrentDictionary<string, DateTime> _states = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Store(string state, DateTime expiresAt)
        => _states[state] = expiresAt;

    public bool TryConsume(string state)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(state, out var expiresAt)) return false;

            if (expiresAt <= DateTime.UtcNow)
            {
                _states.TryRemove(state, out _);
                return false;
            }

            return _states.TryRemove(state, out _);
        }
    }
}
