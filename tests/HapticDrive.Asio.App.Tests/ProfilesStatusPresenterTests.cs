namespace HapticDrive.Asio.App.Tests;

public sealed class ProfilesStatusPresenterTests
{
    [Fact]
    public void Build_WhenIdle_ShowsProfilePathsAndPersistenceBoundary()
    {
        var presentation = ProfilesStatusPresenter.Build(CreateSnapshot());

        Assert.Equal("Active profile: Race.", presentation.ProfileStatusText);
        Assert.Equal("Audio profile path: profiles/default.hdprofile.json; normal BST-1/audio tuning auto-saves here for the next launch.", presentation.ProfilePathText);
        Assert.Equal("P-HPR profile path: profiles/p-hpr.hdphprprofile.json; saves shift intent plus gear, road, slip, and lock preferences. Live output, emergency state, private device paths, direct arming, and paddle bench state remain runtime-only.", presentation.ProfilePhprStatusText);
        Assert.Equal("Profile values are repaired to the current software ranges on load and save.", presentation.ProfileValidationText);
        Assert.Equal("Active profile Race; audio JSON version 1; P-HPR JSON version 2; startup restores saved tuning without restoring live hardware state.", presentation.ProfilesPageStatusText);
    }

    [Fact]
    public void Build_WhenStatusAndValidationMessagesExist_PrefersThem()
    {
        var presentation = ProfilesStatusPresenter.Build(CreateSnapshot(
            statusMessage: "Saved audio and P-HPR profiles.",
            validationMessages: ["Audio values were clamped.", "P-HPR duration was repaired."]));

        Assert.Equal("Saved audio and P-HPR profiles.", presentation.ProfileStatusText);
        Assert.Equal("Audio values were clamped. P-HPR duration was repaired.", presentation.ProfileValidationText);
    }

    private static ProfilesStatusSnapshot CreateSnapshot(
        string currentProfileName = "Race",
        string? statusMessage = null,
        IReadOnlyList<string>? validationMessages = null,
        string audioProfilePath = "profiles/default.hdprofile.json",
        string phprProfilePath = "profiles/p-hpr.hdphprprofile.json",
        int audioProfileVersion = 1,
        int phprProfileVersion = 2)
    {
        return new ProfilesStatusSnapshot(
            CurrentProfileName: currentProfileName,
            StatusMessage: statusMessage,
            ValidationMessages: validationMessages ?? [],
            AudioProfilePath: audioProfilePath,
            PhprProfilePath: phprProfilePath,
            AudioProfileVersion: audioProfileVersion,
            PhprProfileVersion: phprProfileVersion);
    }
}
