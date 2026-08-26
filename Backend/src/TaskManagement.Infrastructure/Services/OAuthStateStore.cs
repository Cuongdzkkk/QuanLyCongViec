using System.Collections.Concurrent;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class OAuthStateStore : IOAuthStateStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Store(string nonce, Guid userId, string provider, string codeVerifier, DateTime expiresAt)
    {
        _entries[nonce] = new Entry(userId, provider, codeVerifier, expiresAt);
    }

    public bool TryConsume(string nonce, Guid userId, string provider, out string codeVerifier)
    {
        lock (_gate)
        {
            codeVerifier = string.Empty;
            if (!_entries.TryGetValue(nonce, out var entry)) return false;

            if (entry.ExpiresAt <= DateTime.UtcNow)
            {
                _entries.TryRemove(nonce, out _);
                return false;
            }

            if (entry.UserId != userId || !entry.Provider.Equals(provider, StringComparison.Ordinal)) return false;

            _entries.TryRemove(nonce, out _);

            codeVerifier = entry.CodeVerifier;
            return true;
        }
    }

    private sealed record Entry(Guid UserId, string Provider, string CodeVerifier, DateTime ExpiresAt);
}
