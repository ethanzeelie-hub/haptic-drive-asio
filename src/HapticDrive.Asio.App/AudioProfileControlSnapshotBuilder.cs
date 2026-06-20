using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;

namespace HapticDrive.Asio.App;

internal sealed record AudioProfileControlInputs(
    string? ProfileName,
    Bst1AudioProfileEffectControlInputs Effects,
    double MasterGainValue,
    bool MixerMuted,
    double SafetyOutputGainValue);

internal sealed record AudioProfileMixerControlInputs(
    double MasterGainValue,
    bool MixerMuted,
    double SafetyOutputGainValue);

internal sealed record AudioProfileControlValues(
    string ProfileName,
    Bst1AudioProfileEffectControlValues Effects,
    float MasterGain,
    bool MixerMuted,
    float SafetyOutputGain);

internal sealed record AudioProfileControlTextValues(
    Bst1AudioProfileEffectControlTextValues Effects,
    string MasterGainText,
    string SafetyOutputGainText);

internal sealed record AudioProfileControlApplicationPlan(
    HapticDriveProfile SafeProfile,
    AudioProfileControlValues ControlValues,
    AudioProfileControlTextValues TextValues);

internal static class AudioProfileControlSnapshotBuilder
{
    public static HapticDriveProfile BuildProfile(
        HapticDriveProfile currentProfile,
        AudioProfileControlInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(currentProfile);
        ArgumentNullException.ThrowIfNull(inputs);

        var name = string.IsNullOrWhiteSpace(inputs.ProfileName)
            ? currentProfile.Name
            : inputs.ProfileName.Trim();

        return HapticProfileValidator.Validate(currentProfile with
        {
            Name = name,
            Effects = Bst1AudioProfileEffectControlSnapshotBuilder.BuildProfileEffects(currentProfile.Effects, inputs.Effects),
            Mixer = currentProfile.Mixer with
            {
                MasterGain = (float)inputs.MasterGainValue,
                IsMuted = inputs.MixerMuted
            },
            Safety = currentProfile.Safety with
            {
                OutputGain = (float)inputs.SafetyOutputGainValue,
                OutputGainCeiling = AudioSafetyProcessorOptions.DefaultOutputGainCeiling,
                LimiterEnabled = true
            }
        }).Profile;
    }

    public static AudioProfileControlApplicationPlan BuildApplicationPlan(HapticDriveProfile? profile)
    {
        var safeProfile = HapticProfileValidator.Validate(profile).Profile;
        var effectSnapshot = Bst1AudioProfileEffectControlSnapshotBuilder.BuildApplicationSnapshot(safeProfile);
        var controlValues = new AudioProfileControlValues(
            ProfileName: safeProfile.Name,
            Effects: effectSnapshot.ControlValues,
            MasterGain: safeProfile.Mixer.MasterGain,
            MixerMuted: safeProfile.Mixer.IsMuted,
            SafetyOutputGain: safeProfile.Safety.OutputGain);

        return new AudioProfileControlApplicationPlan(
            SafeProfile: safeProfile,
            ControlValues: controlValues,
            TextValues: new AudioProfileControlTextValues(
                Effects: effectSnapshot.TextValues,
                MasterGainText: $"{safeProfile.Mixer.MasterGain:P0}",
                SafetyOutputGainText: $"{safeProfile.Safety.OutputGain:P0}"));
    }
}
