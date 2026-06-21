namespace HapticDrive.Asio.App;

internal interface IAudioProfileProfilesViewSync
{
    string? BuildAudioProfileNameInput();

    void ApplyAudioProfileControlValues(AudioProfileControlValues values);
}

internal interface IAudioProfileEffectsViewSync
{
    Bst1AudioProfileEffectControlInputs BuildAudioProfileEffectControlInputs();

    void ApplyAudioProfileEffectControlValues(Bst1AudioProfileEffectControlValues values);

    void ApplyAudioProfileEffectControlText(Bst1AudioProfileEffectControlTextValues values);
}

internal interface IAudioProfileRoutingMixerViewSync
{
    AudioProfileMixerControlInputs BuildAudioProfileMixerControlInputs();

    void ApplyAudioProfileMixerControlValues(AudioProfileControlValues values);

    void ApplyAudioProfileMixerControlText(AudioProfileControlTextValues values);
}

internal static class AudioProfileViewSyncCoordinator
{
    public static AudioProfileControlInputs BuildCurrentControlInputs(
        IAudioProfileProfilesViewSync profilesView,
        IAudioProfileEffectsViewSync effectsView,
        IAudioProfileRoutingMixerViewSync routingMixerView)
    {
        ArgumentNullException.ThrowIfNull(profilesView);
        ArgumentNullException.ThrowIfNull(effectsView);
        ArgumentNullException.ThrowIfNull(routingMixerView);

        var mixerInputs = routingMixerView.BuildAudioProfileMixerControlInputs();
        return new AudioProfileControlInputs(
            ProfileName: profilesView.BuildAudioProfileNameInput(),
            Effects: effectsView.BuildAudioProfileEffectControlInputs(),
            MasterGainValue: mixerInputs.MasterGainValue,
            MixerMuted: mixerInputs.MixerMuted,
            SafetyOutputGainValue: mixerInputs.SafetyOutputGainValue);
    }

    public static void ApplyControlValues(
        AudioProfileControlValues values,
        IAudioProfileProfilesViewSync profilesView,
        IAudioProfileEffectsViewSync effectsView,
        IAudioProfileRoutingMixerViewSync routingMixerView)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(profilesView);
        ArgumentNullException.ThrowIfNull(effectsView);
        ArgumentNullException.ThrowIfNull(routingMixerView);

        profilesView.ApplyAudioProfileControlValues(values);
        effectsView.ApplyAudioProfileEffectControlValues(values.Effects);
        routingMixerView.ApplyAudioProfileMixerControlValues(values);
    }

    public static void ApplyControlText(
        AudioProfileControlTextValues values,
        IAudioProfileEffectsViewSync effectsView,
        IAudioProfileRoutingMixerViewSync routingMixerView)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(effectsView);
        ArgumentNullException.ThrowIfNull(routingMixerView);

        effectsView.ApplyAudioProfileEffectControlText(values.Effects);
        routingMixerView.ApplyAudioProfileMixerControlText(values);
    }
}
