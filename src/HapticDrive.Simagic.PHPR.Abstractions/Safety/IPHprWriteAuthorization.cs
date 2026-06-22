namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public interface IPHprWriteAuthorization
{
    PHprWriteAuthorizationSnapshot Current { get; }

    bool TryAuthorize(string? phrase);

    void Revoke(string reason);
}
