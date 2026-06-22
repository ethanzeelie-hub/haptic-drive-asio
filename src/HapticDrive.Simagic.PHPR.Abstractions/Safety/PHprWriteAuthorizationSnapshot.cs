namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public sealed record PHprWriteAuthorizationSnapshot(
    bool IsAuthorized,
    DateTimeOffset? AuthorizedAtUtc,
    long Generation,
    string Reason);
