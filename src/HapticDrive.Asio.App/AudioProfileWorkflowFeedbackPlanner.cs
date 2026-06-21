using System.IO;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App;

internal sealed record AudioProfileWorkflowFeedback(
    string? FooterStatusText,
    bool ShouldUpdateProfileStatus,
    string? ProfileStatusMessage,
    IReadOnlyList<string> ProfileValidationMessages);

internal static class AudioProfileWorkflowFeedbackPlanner
{
    public static AudioProfileWorkflowFeedback BuildTuningChangedFeedback(
        HapticProfileSaveResult saveResult,
        bool hapticsStarted)
    {
        ArgumentNullException.ThrowIfNull(saveResult);

        if (!saveResult.Succeeded)
        {
            return new AudioProfileWorkflowFeedback(
                FooterStatusText: saveResult.Message,
                ShouldUpdateProfileStatus: true,
                ProfileStatusMessage: saveResult.Message,
                ProfileValidationMessages: saveResult.ValidationMessages);
        }

        return new AudioProfileWorkflowFeedback(
            FooterStatusText: hapticsStarted
                ? "Tuning applied to the output-owned render path."
                : "Tuning applied; haptics are still stopped.",
            ShouldUpdateProfileStatus: false,
            ProfileStatusMessage: null,
            ProfileValidationMessages: []);
    }

    public static AudioProfileWorkflowFeedback BuildProfileNameCommitFeedback(HapticProfileSaveResult saveResult)
    {
        ArgumentNullException.ThrowIfNull(saveResult);

        return new AudioProfileWorkflowFeedback(
            FooterStatusText: saveResult.Succeeded ? null : saveResult.Message,
            ShouldUpdateProfileStatus: true,
            ProfileStatusMessage: saveResult.Message,
            ProfileValidationMessages: saveResult.ValidationMessages);
    }

    public static AudioProfileWorkflowFeedback BuildSaveProfilesFeedback(
        HapticProfileSaveResult audioResult,
        PhprEffectProfileSaveResult phprResult)
    {
        ArgumentNullException.ThrowIfNull(audioResult);
        ArgumentNullException.ThrowIfNull(phprResult);

        var combinedMessage = JoinMessages(audioResult.Message, phprResult.Message);
        return new AudioProfileWorkflowFeedback(
            FooterStatusText: audioResult.Succeeded && phprResult.Succeeded
                ? $"Saved profiles {Path.GetFileName(audioResult.Path)} and {Path.GetFileName(phprResult.Path)}."
                : combinedMessage,
            ShouldUpdateProfileStatus: true,
            ProfileStatusMessage: combinedMessage,
            ProfileValidationMessages: audioResult.ValidationMessages.Concat(phprResult.ValidationMessages).ToArray());
    }

    public static AudioProfileWorkflowFeedback BuildLoadProfilesFeedback(
        HapticProfileLoadResult audioResult,
        PhprEffectProfileLoadResult phprResult)
    {
        ArgumentNullException.ThrowIfNull(audioResult);
        ArgumentNullException.ThrowIfNull(phprResult);

        var combinedMessage = JoinMessages(audioResult.Message, phprResult.Message);
        return new AudioProfileWorkflowFeedback(
            FooterStatusText: combinedMessage,
            ShouldUpdateProfileStatus: true,
            ProfileStatusMessage: combinedMessage,
            ProfileValidationMessages: audioResult.ValidationMessages.Concat(phprResult.ValidationMessages).ToArray());
    }

    public static AudioProfileWorkflowFeedback BuildResetFeedback(
        HapticProfileSaveResult audioSaveResult,
        bool hapticsStarted)
    {
        ArgumentNullException.ThrowIfNull(audioSaveResult);

        if (!audioSaveResult.Succeeded)
        {
            return new AudioProfileWorkflowFeedback(
                FooterStatusText: audioSaveResult.Message,
                ShouldUpdateProfileStatus: true,
                ProfileStatusMessage: audioSaveResult.Message,
                ProfileValidationMessages: audioSaveResult.ValidationMessages);
        }

        return new AudioProfileWorkflowFeedback(
            FooterStatusText: hapticsStarted
                ? "Reset tuning to the current rig defaults for the output-owned render path."
                : "Reset tuning to the current rig defaults.",
            ShouldUpdateProfileStatus: true,
            ProfileStatusMessage: "Reset to current rig audio, BST-1 local gear, and P-HPR defaults.",
            ProfileValidationMessages: audioSaveResult.ValidationMessages);
    }

    private static string JoinMessages(string first, string second)
    {
        return $"{first} {second}";
    }
}
