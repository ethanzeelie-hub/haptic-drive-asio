namespace HapticDrive.Asio.App.Tests;

public sealed class AudioProfileViewSyncCoordinatorTests
{
    [Fact]
    public void BuildCurrentControlInputs_ComposesInputsFromAllThreeViewSeams()
    {
        var profilesView = new FakeProfilesViewSync { ProfileName = "  My Profile  " };
        var effectsView = new FakeEffectsViewSync
        {
            Inputs = new Bst1AudioProfileEffectControlInputs(
                EngineEnabled: true,
                EngineGainValue: 0.6,
                EngineMinimumFrequencyValue: 30,
                EngineMaximumFrequencyValue: 80,
                GearShiftEnabled: true,
                GearShiftGainValue: 0.7,
                GearShiftDurationValue: 90,
                KerbEnabled: true,
                KerbGainValue: 0.5,
                KerbBaseFrequencyValue: 40,
                ImpactEnabled: true,
                ImpactGainValue: 0.8,
                ImpactDurationValue: 120,
                SharedRoadSignalEnabled: true,
                Bst1RoadOutputEnabled: true,
                RoadTextureGainValue: 0.4,
                RoadTextureMinimumSpeedValue: 25,
                RoadTextureSpeedReferenceValue: 120,
                RoadTextureLowSpeedFrequencyValue: 30,
                RoadTextureHighSpeedFrequencyValue: 65,
                RoadTextureSpeedFrequencyInfluenceValue: 0.5,
                RoadTextureGrainAmountValue: 0.3,
                SlipWheelSlipEnabled: true,
                SlipWheelSlipGainValue: 0.4,
                SlipWheelSlipFrequencyValue: 55,
                SlipWheelSlipNoiseValue: 0.2,
                SlipWheelLockEnabled: true,
                SlipWheelLockGainValue: 0.5,
                SlipWheelLockFrequencyValue: 60,
                SlipWheelLockNoiseValue: 0.25,
                SlipWheelLockSensitivityValue: 0.4,
                SlipThresholdValue: 0.15)
        };
        var routingView = new FakeRoutingMixerViewSync
        {
            Inputs = new AudioProfileMixerControlInputs(
                MasterGainValue: 0.9,
                MixerMuted: true,
                SafetyOutputGainValue: 0.35)
        };

        var inputs = AudioProfileViewSyncCoordinator.BuildCurrentControlInputs(
            profilesView,
            effectsView,
            routingView);

        Assert.Equal("  My Profile  ", inputs.ProfileName);
        Assert.Equal(effectsView.Inputs, inputs.Effects);
        Assert.Equal(0.9, inputs.MasterGainValue);
        Assert.True(inputs.MixerMuted);
        Assert.Equal(0.35, inputs.SafetyOutputGainValue);
    }

    [Fact]
    public void ApplyControlValues_DelegatesToAllThreeViewSeams()
    {
        var profilesView = new FakeProfilesViewSync();
        var effectsView = new FakeEffectsViewSync();
        var routingView = new FakeRoutingMixerViewSync();
        var values = new AudioProfileControlValues(
            ProfileName: "Profile",
            Effects: new Bst1AudioProfileEffectControlValues(
                EngineEnabled: true,
                EngineGain: 0.5f,
                EngineMinimumFrequencyHz: 30f,
                EngineMaximumFrequencyHz: 80f,
                GearShiftEnabled: true,
                GearShiftGain: 0.6f,
                GearShiftDurationMilliseconds: 90,
                KerbEnabled: true,
                KerbGain: 0.4f,
                KerbBaseFrequencyHz: 45f,
                ImpactEnabled: true,
                ImpactGain: 0.8f,
                ImpactDurationMilliseconds: 120,
                SharedRoadSignalEnabled: true,
                Bst1RoadOutputEnabled: true,
                RoadTextureGain: 0.3f,
                RoadTextureMinimumSpeedKph: 20f,
                RoadTextureSpeedReferenceKph: 100f,
                RoadTextureLowSpeedFrequencyHz: 30f,
                RoadTextureHighSpeedFrequencyHz: 60f,
                RoadTextureSpeedFrequencyInfluence: 0.5f,
                RoadTextureGrainAmount: 0.2f,
                SlipWheelSlipEnabled: true,
                SlipWheelSlipGain: 0.4f,
                SlipWheelSlipFrequencyHz: 50f,
                SlipWheelSlipNoiseAmount: 0.2f,
                SlipWheelLockEnabled: true,
                SlipWheelLockGain: 0.5f,
                SlipWheelLockFrequencyHz: 55f,
                SlipWheelLockNoiseAmount: 0.25f,
                SlipWheelLockSensitivity: 0.35f,
                SlipThreshold: 0.15f),
            MasterGain: 0.8f,
            MixerMuted: false,
            SafetyOutputGain: 0.25f);

        AudioProfileViewSyncCoordinator.ApplyControlValues(values, profilesView, effectsView, routingView);

        Assert.Equal(values, profilesView.AppliedValues);
        Assert.Equal(values.Effects, effectsView.AppliedValues);
        Assert.Equal(values, routingView.AppliedValues);
    }

    [Fact]
    public void ApplyControlText_DelegatesToEffectAndMixerViewSeams()
    {
        var effectsView = new FakeEffectsViewSync();
        var routingView = new FakeRoutingMixerViewSync();
        var values = new AudioProfileControlTextValues(
            Effects: new Bst1AudioProfileEffectControlTextValues(
                EngineGainText: "50%",
                EngineFrequencyText: "30-80 Hz",
                GearShiftGainText: "60%",
                GearShiftDurationText: "90 ms",
                KerbGainText: "40%",
                KerbFrequencyText: "45 Hz",
                ImpactGainText: "80%",
                ImpactDurationText: "120 ms",
                RoadTextureGainText: "30%",
                RoadTextureMinimumSpeedText: "20 km/h",
                RoadTextureSpeedReferenceText: "100 km/h",
                RoadTextureLowSpeedFrequencyText: "30 Hz",
                RoadTextureHighSpeedFrequencyText: "60 Hz",
                RoadTextureSpeedFrequencyInfluenceText: "50%",
                RoadTextureGrainAmountText: "20%",
                SlipWheelSlipGainText: "40%",
                SlipWheelSlipFrequencyText: "50 Hz",
                SlipWheelSlipNoiseText: "20%",
                SlipWheelLockGainText: "50%",
                SlipWheelLockFrequencyText: "55 Hz",
                SlipWheelLockNoiseText: "25%",
                SlipWheelLockSensitivityText: "35%",
                SlipThresholdText: "15%"),
            MasterGainText: "80%",
            SafetyOutputGainText: "25%");

        AudioProfileViewSyncCoordinator.ApplyControlText(values, effectsView, routingView);

        Assert.Equal(values.Effects, effectsView.AppliedText);
        Assert.Equal(values, routingView.AppliedText);
    }

    private sealed class FakeProfilesViewSync : IAudioProfileProfilesViewSync
    {
        public string? ProfileName { get; init; }

        public AudioProfileControlValues? AppliedValues { get; private set; }

        public string? BuildAudioProfileNameInput() => ProfileName;

        public void ApplyAudioProfileControlValues(AudioProfileControlValues values) => AppliedValues = values;
    }

    private sealed class FakeEffectsViewSync : IAudioProfileEffectsViewSync
    {
        public Bst1AudioProfileEffectControlInputs Inputs { get; init; } = new(
            EngineEnabled: false,
            EngineGainValue: 0,
            EngineMinimumFrequencyValue: 0,
            EngineMaximumFrequencyValue: 0,
            GearShiftEnabled: false,
            GearShiftGainValue: 0,
            GearShiftDurationValue: 0,
            KerbEnabled: false,
            KerbGainValue: 0,
            KerbBaseFrequencyValue: 0,
            ImpactEnabled: false,
            ImpactGainValue: 0,
            ImpactDurationValue: 0,
            SharedRoadSignalEnabled: false,
            Bst1RoadOutputEnabled: false,
            RoadTextureGainValue: 0,
            RoadTextureMinimumSpeedValue: 0,
            RoadTextureSpeedReferenceValue: 0,
            RoadTextureLowSpeedFrequencyValue: 0,
            RoadTextureHighSpeedFrequencyValue: 0,
            RoadTextureSpeedFrequencyInfluenceValue: 0,
            RoadTextureGrainAmountValue: 0,
            SlipWheelSlipEnabled: false,
            SlipWheelSlipGainValue: 0,
            SlipWheelSlipFrequencyValue: 0,
            SlipWheelSlipNoiseValue: 0,
            SlipWheelLockEnabled: false,
            SlipWheelLockGainValue: 0,
            SlipWheelLockFrequencyValue: 0,
            SlipWheelLockNoiseValue: 0,
            SlipWheelLockSensitivityValue: 0,
            SlipThresholdValue: 0);

        public Bst1AudioProfileEffectControlValues? AppliedValues { get; private set; }

        public Bst1AudioProfileEffectControlTextValues? AppliedText { get; private set; }

        public Bst1AudioProfileEffectControlInputs BuildAudioProfileEffectControlInputs() => Inputs;

        public void ApplyAudioProfileEffectControlValues(Bst1AudioProfileEffectControlValues values) => AppliedValues = values;

        public void ApplyAudioProfileEffectControlText(Bst1AudioProfileEffectControlTextValues values) => AppliedText = values;
    }

    private sealed class FakeRoutingMixerViewSync : IAudioProfileRoutingMixerViewSync
    {
        public AudioProfileMixerControlInputs Inputs { get; init; } = new(0, false, 0);

        public AudioProfileControlValues? AppliedValues { get; private set; }

        public AudioProfileControlTextValues? AppliedText { get; private set; }

        public AudioProfileMixerControlInputs BuildAudioProfileMixerControlInputs() => Inputs;

        public void ApplyAudioProfileMixerControlValues(AudioProfileControlValues values) => AppliedValues = values;

        public void ApplyAudioProfileMixerControlText(AudioProfileControlTextValues values) => AppliedText = values;
    }
}
