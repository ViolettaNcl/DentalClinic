using Microsoft.Extensions.Caching.Memory;

namespace DentalClinic.Services;

/// <summary>
/// Short-lived cache of the authoritative account token version.
///
/// Every authenticated API request used to perform an extra SQL query only to
/// re-check TokenVersion. On a remote development database that doubled the
/// number of database round trips for dashboard pages and made simultaneous
/// dashboard requests extremely slow. This cache keeps revocation semantics while
/// avoiding that duplicate query on every request.
///
/// All application paths which intentionally increment TokenVersion update this
/// cache immediately, so logout/password/admin-access revocation remains immediate
/// inside the running application. The short absolute expiry is a final safety net
/// for out-of-process database changes.
/// </summary>
public sealed class TokenVersionCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(45);
    private readonly IMemoryCache _cache;

    public TokenVersionCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(string role, int userId, out int tokenVersion)
        => _cache.TryGetValue(Key(role, userId), out tokenVersion);

    public void Set(string role, int userId, int tokenVersion)
        => _cache.Set(
            Key(role, userId),
            tokenVersion,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime
            });

    /// <summary>Revoke one specific token (logout) until it would expire anyway.</summary>
    public void RevokeToken(string tokenId, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenId)) return;
        var ttl = expiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        _cache.Set($"auth-revoked-jti:{tokenId}", true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });
    }

    public bool IsTokenRevoked(string? tokenId)
        => !string.IsNullOrWhiteSpace(tokenId)
            && _cache.TryGetValue($"auth-revoked-jti:{tokenId}", out _);

    public void Remove(string role, int userId)
        => _cache.Remove(Key(role, userId));

    private static string Key(string role, int userId)
        => $"auth-token-version:{role.Trim().ToLowerInvariant()}:{userId}";
}
