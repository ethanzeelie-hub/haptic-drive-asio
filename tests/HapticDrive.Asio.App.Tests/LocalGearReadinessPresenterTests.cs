using HapticDrive.Asio.App;

namespace HapticDrive.Asio.App.Tests;

public sealed class LocalGearReadinessPresenterTests
{
    [Fact]
    public void Build_MapsRepresentativeReadyState()
    {
        var presentation = LocalGearReadinessPresenter.Build(
            new LocalGearTestReadiness(
                IsEnabled: true,
                IsReady: true,
                CanStartListener: false,
                Message: "Local gear test ready; Start Haptics and F1 telemetry are not required."),
            autoStartListener: true);

        Assert.Equal(
            "Local gear test ready; Start Haptics and F1 telemetry are not required. Auto-start listener True; Start Haptics required: NO; F1 telemetry required: NO; live telemetry effects started: NO.",
            presentation.StatusText);
        Assert.False(presentation.StartListenerEnabled);
        Assert.Equal(
            "Local gear test ready; Start Haptics and F1 telemetry are not required.",
            presentation.StartListenerToolTip);
    }

    [Fact]
    public void Build_MapsStartListenerPromptWhenReadinessAllowsManualStart()
    {
        var presentation = LocalGearReadinessPresenter.Build(
            new LocalGearTestReadiness(
                IsEnabled: true,
                IsReady: false,
                CanStartListener: true,
                Message: "Blocked: paddle listener stopped; auto-start is available."),
            autoStartListener: true);

        Assert.True(presentation.StartListenerEnabled);
        Assert.Contains("Auto-start listener True", presentation.StatusText);
        Assert.Equal(
            "Start the read-only paddle listener for Local Gear Test Mode without Start Haptics or F1 telemetry.",
            presentation.StartListenerToolTip);
    }

    [Fact]
    public void Build_NullReadinessFallsBackSafely()
    {
        var presentation = LocalGearReadinessPresenter.Build(
            readiness: null,
            autoStartListener: false);

        Assert.Equal(
            "Local gear test status unavailable. Auto-start listener False; Start Haptics required: NO; F1 telemetry required: NO; live telemetry effects started: NO.",
            presentation.StatusText);
        Assert.False(presentation.StartListenerEnabled);
        Assert.Equal("Local gear test status unavailable.", presentation.StartListenerToolTip);
    }
}
