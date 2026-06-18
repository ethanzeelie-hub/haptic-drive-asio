namespace HapticDrive.Asio.App;

internal sealed record ProfilesStatusSnapshot(
    string CurrentProfileName,
    string? StatusMessage,
    IReadOnlyList<string> ValidationMessages,
    string AudioProfilePath,
    string PhprProfilePath,
    int AudioProfileVersion,
    int PhprProfileVersion);

internal sealed record ProfilesStatusPresentation(
    string ProfileStatusText,
    string ProfilePathText,
    string ProfilePhprStatusText,
    string ProfileValidationText,
    string ProfilesPageStatusText);

internal static class ProfilesStatusPresenter
{
    public static ProfilesStatusPresentation Build(ProfilesStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProfilesStatusPresentation(
            ProfileStatusText: snapshot.StatusMessage ?? $"Active profile: {snapshot.CurrentProfileName}.",
            ProfilePathText: $"Audio profile path: {snapshot.AudioProfilePath}; normal BST-1/audio tuning auto-saves here for the next launch.",
            ProfilePhprStatusText: $"P-HPR profile path: {snapshot.PhprProfilePath}; saves shift intent plus gear, road, slip, and lock preferences. Live output, emergency state, private device paths, direct arming, and paddle bench state remain runtime-only.",
            ProfileValidationText: snapshot.ValidationMessages.Count > 0
                ? string.Join(" ", snapshot.ValidationMessages)
                : "Profile values are repaired to the current software ranges on load and save.",
            ProfilesPageStatusText: $"Active profile {snapshot.CurrentProfileName}; audio JSON version {snapshot.AudioProfileVersion}; P-HPR JSON version {snapshot.PhprProfileVersion}; startup restores saved tuning without restoring live hardware state.");
    }
}
