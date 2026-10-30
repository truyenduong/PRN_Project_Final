using System.Collections.Concurrent;

namespace PRN232.Application.Security;

public sealed class TokenBlacklist
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new(StringComparer.Ordinal);

    public void Add(string token, DateTimeOffset expiresAt)
    {
        if (expiresAt > DateTimeOffset.UtcNow)
        {
            _tokens[token] = expiresAt;
        }
    }

    public bool Contains(string token)
    {
        if (!_tokens.TryGetValue(token, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _tokens.TryRemove(token, out _);
        return false;
    }
}
