namespace HapticDrive.Simagic.PHPR.Abstractions.Safety;

public static class PHprControlledWriteApproval
{
    public const string Phrase = "I approve Phase 2 controlled P-HPR write testing";

    public static bool IsApproved(string? phrase)
    {
        return string.Equals(phrase?.Trim(), Phrase, StringComparison.Ordinal);
    }
}
