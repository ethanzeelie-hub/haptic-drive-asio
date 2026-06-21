using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileWorkflowFeedbackPlannerTests
{
    [Fact]
    public void BuildTuningChangedFeedback_ReturnsFailureFooterAndProfileStatus_WhenSaveFails()
    {
        var saveResult = HapticProfileSaveResult.Failure("profile.json", "Profile could not be saved.");

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildTuningChangedFeedback(saveResult, hapticsStarted: true);

        Assert.Equal("Profile could not be saved.", feedback.FooterStatusText);
        Assert.True(feedback.ShouldUpdateProfileStatus);
        Assert.Equal("Profile could not be saved.", feedback.ProfileStatusMessage);
        Assert.Empty(feedback.ProfileValidationMessages);
    }

    [Fact]
    public void BuildTuningChangedFeedback_ReturnsRunningFooter_WhenSaveSucceedsAndHapticsRunning()
    {
        var saveResult = HapticProfileSaveResult.Success("profile.json", wasRepaired: false, validationMessages: []);

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildTuningChangedFeedback(saveResult, hapticsStarted: true);

        Assert.Equal("Tuning applied to the output-owned render path.", feedback.FooterStatusText);
        Assert.False(feedback.ShouldUpdateProfileStatus);
        Assert.Null(feedback.ProfileStatusMessage);
        Assert.Empty(feedback.ProfileValidationMessages);
    }

    [Fact]
    public void BuildProfileNameCommitFeedback_PreservesFailureMessageForFooterAndProfileStatus()
    {
        var saveResult = HapticProfileSaveResult.Failure("profile.json", "Profile name save failed.");

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildProfileNameCommitFeedback(saveResult);

        Assert.Equal("Profile name save failed.", feedback.FooterStatusText);
        Assert.True(feedback.ShouldUpdateProfileStatus);
        Assert.Equal("Profile name save failed.", feedback.ProfileStatusMessage);
    }

    [Fact]
    public void BuildSaveProfilesFeedback_ReturnsSavedProfileFooter_WhenBothSucceed()
    {
        var audio = HapticProfileSaveResult.Success("C:\\profiles\\default.hdprofile.json", wasRepaired: false, validationMessages: []);
        var phpr = PhprEffectProfileSaveResult.Success("C:\\profiles\\p-hpr.hdphprprofile.json", wasRepaired: false, validationMessages: []);

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildSaveProfilesFeedback(audio, phpr);

        Assert.Equal("Saved profiles default.hdprofile.json and p-hpr.hdphprprofile.json.", feedback.FooterStatusText);
        Assert.True(feedback.ShouldUpdateProfileStatus);
        Assert.Equal("Profile saved. P-HPR profile saved.", feedback.ProfileStatusMessage);
    }

    [Fact]
    public void BuildLoadProfilesFeedback_ReturnsCombinedMessage()
    {
        var audio = HapticProfileLoadResult.Success(HapticDriveProfile.Default, wasRepaired: false, validationMessages: []);
        var phpr = PhprEffectProfileLoadResult.Success(PhprEffectProfile.Default, wasRepaired: false, validationMessages: []);

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildLoadProfilesFeedback(audio, phpr);

        Assert.Equal("Profile loaded. P-HPR profile loaded.", feedback.FooterStatusText);
        Assert.True(feedback.ShouldUpdateProfileStatus);
        Assert.Equal("Profile loaded. P-HPR profile loaded.", feedback.ProfileStatusMessage);
    }

    [Fact]
    public void BuildResetFeedback_ReturnsRunningResetFooter_WhenSaveSucceedsAndHapticsRunning()
    {
        var audio = HapticProfileSaveResult.Success("profile.json", wasRepaired: false, validationMessages: []);

        var feedback = AudioProfileWorkflowFeedbackPlanner.BuildResetFeedback(audio, hapticsStarted: true);

        Assert.Equal("Reset tuning to the current rig defaults for the output-owned render path.", feedback.FooterStatusText);
        Assert.True(feedback.ShouldUpdateProfileStatus);
        Assert.Equal("Reset to current rig audio, BST-1 local gear, and P-HPR defaults.", feedback.ProfileStatusMessage);
    }
}
