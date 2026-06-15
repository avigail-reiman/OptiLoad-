using System.Collections.Concurrent;

namespace OptiLoad.API.Services;

public sealed class LoginRateLimiter
{
    private const int MaxFailedAttempts = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private sealed record Entry(int Failures, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public bool IsLockedOut(string ip, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (!_store.TryGetValue(ip, out var entry) || entry.LockedUntil == null)
            return false;

        var remaining = entry.LockedUntil.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _store.TryRemove(ip, out _);
            return false;
        }

        retryAfterSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return true;
    }

    public void RecordFailure(string ip)
    {
        _store.AddOrUpdate(
            ip,
            _ => new Entry(1, null),
            (_, old) =>
            {
                if (old.LockedUntil.HasValue) return old;
                int failures = old.Failures + 1;
                DateTimeOffset? locked = failures >= MaxFailedAttempts
                    ? DateTimeOffset.UtcNow.Add(LockoutDuration)
                    : null;
                return new Entry(failures, locked);
            });
    }

    public void RecordSuccess(string ip) => _store.TryRemove(ip, out _);
}