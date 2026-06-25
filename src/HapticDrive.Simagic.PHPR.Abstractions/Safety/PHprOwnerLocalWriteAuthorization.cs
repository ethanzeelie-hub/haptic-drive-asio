using System.Threading;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed class PHprOwnerLocalWriteAuthorization : IPHprWriteAuthorization
{
    private readonly object _gate = new();
    private PHprWriteAuthorizationSnapshot _current = new(
        IsAuthorized: true,
        AuthorizedAtUtc: DateTimeOffset.UtcNow,
        Generation: 1,
        Reason: "Owner-local authorization active");

    public PHprWriteAuthorizationSnapshot Current => Volatile.Read(ref _current);

    public bool TryAuthorize(string? phrase)
    {
        lock (_gate)
        {
            var current = _current;
            if (current.IsAuthorized)
            {
                return true;
            }

            Volatile.Write(ref _current, new PHprWriteAuthorizationSnapshot(
                IsAuthorized: true,
                AuthorizedAtUtc: DateTimeOffset.UtcNow,
                Generation: current.Generation + 1,
                Reason: string.IsNullOrWhiteSpace(phrase)
                    ? "Owner-local authorization restored"
                    : $"Owner-local authorization restored: {phrase.Trim()}"));
        }

        return true;
    }

    public void Revoke(string reason)
    {
        lock (_gate)
        {
            var current = _current;
            Volatile.Write(ref _current, new PHprWriteAuthorizationSnapshot(
                IsAuthorized: false,
                AuthorizedAtUtc: null,
                Generation: current.Generation + 1,
                Reason: NormalizeReason(reason)));
        }
    }

    private static string NormalizeReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "Owner-local authorization paused"
            : reason.Trim();
    }
}
