using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprAbstractionTests
{
    [Fact]
    public void SafetyLimits_DefaultsDisableRealWritesAndUseControlledPulseCaps()
    {
        var limits = PHprSafetyLimits.Default;

        Assert.False(limits.AllowRealDeviceWrites);
        Assert.Equal(1.0d, limits.MaxStrength01, precision: 6);
        Assert.Equal(1_000, limits.MaxDurationMs);
        Assert.InRange(limits.MaxCommandsPerSecond, 1, 20);
        Assert.True(limits.MaxContinuousDurationMs >= limits.MaxDurationMs);
    }

    [Fact]
    public void PHprCommand_ClampsUnsafeValuesToSafetyDefaults()
    {
        var command = PHprCommand.Create(
            PHprModuleId.Both,
            strength01: 1.5d,
            frequencyHz: 999d,
            durationMs: 2_000,
            PHprCommandSource.PaddleShiftIntent);

        var clamped = command.ClampTo(PHprSafetyLimits.Default);

        Assert.Equal(PHprSafetyLimits.Default.MaxStrength01, clamped.Strength01, precision: 6);
        Assert.Equal(PHprSafetyLimits.Default.MaxFrequencyHz, clamped.FrequencyHz, precision: 6);
        Assert.Equal(PHprSafetyLimits.Default.MaxDurationMs, clamped.DurationMs);
        Assert.True(clamped.SafetyFlags.HasFlag(PHprSafetyFlags.ClampedStrength));
        Assert.True(clamped.SafetyFlags.HasFlag(PHprSafetyFlags.ClampedFrequency));
        Assert.True(clamped.SafetyFlags.HasFlag(PHprSafetyFlags.ClampedDuration));
    }

    [Fact]
    public async Task MockPhprOutputDevice_RecordsSafeMockCommandWithoutHardwareWrite()
    {
        await using var output = new MockPhprOutputDevice();
        var command = PHprCommand.Create(
            PHprModuleId.Both,
            strength01: 0.05d,
            frequencyHz: 80d,
            durationMs: 60,
            PHprCommandSource.PaddleShiftIntent);

        var result = await output.SendAsync(command);
        var snapshot = output.GetSnapshot();

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Command);
        Assert.True(result.Command.SafetyFlags.HasFlag(PHprSafetyFlags.MockOnly));
        Assert.Single(output.CommandHistory);
        Assert.True(snapshot.IsMock);
        Assert.True(snapshot.IsConnected);
        Assert.False(snapshot.IsEmergencyStopActive);
        Assert.Equal(1, snapshot.AcceptedCommandCount);
        Assert.Equal(0, snapshot.RejectedCommandCount);
        Assert.Equal(PHprCommandStatus.Accepted, snapshot.LastStatus);
    }

    [Fact]
    public async Task MockPhprOutputDevice_EmergencyStopSuppressesLaterCommands()
    {
        await using var output = new MockPhprOutputDevice();

        await output.EmergencyStopAsync();
        var result = await output.SendAsync(PHprCommand.Create(
            PHprModuleId.Brake,
            strength01: 0.05d,
            frequencyHz: 80d,
            durationMs: 50,
            PHprCommandSource.TestBench));
        var snapshot = output.GetSnapshot();

        Assert.False(result.Succeeded);
        Assert.Equal(PHprCommandStatus.RejectedEmergencyStop, result.Status);
        Assert.True(snapshot.IsEmergencyStopActive);
        Assert.Equal(0, snapshot.AcceptedCommandCount);
        Assert.Equal(1, snapshot.RejectedCommandCount);
        Assert.Equal(PHprCommandStatus.RejectedEmergencyStop, snapshot.LastStatus);
        Assert.Contains("emergency", snapshot.LastMessage, StringComparison.OrdinalIgnoreCase);
    }
}
