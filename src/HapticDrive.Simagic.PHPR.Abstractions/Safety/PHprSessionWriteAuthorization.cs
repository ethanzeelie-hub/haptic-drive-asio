using System.Threading;

namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed class PHprSessionWriteAuthorization : IPHprWriteAuthorization
{
    private readonly object _gate = new();
    private PHprWriteAuthorizationSnapshot _current = new(
        IsAuthorized: false,
        AuthorizedAtUtc: null,
        Generation: 0,
        Reason: "Not authorized");

    public PHprWriteAuthorizationSnapshot Current => Volatile.Read(ref _current);

    public bool TryAuthorize(string? phrase)
    {
        if (!PHprControlledWriteApproval.IsApproved(phrase))
        {
            return false;
        }

        lock (_gate)
        {
            var current = _current;
            Volatile.Write(ref _current, new PHprWriteAuthorizationSnapshot(
                IsAuthorized: true,
                AuthorizedAtUtc: DateTimeOffset.UtcNow,
                Generation: current.Generation + 1,
                Reason: "Authorized for this session"));
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
            ? "Authorization revoked"
            : reason.Trim();
    }
}
