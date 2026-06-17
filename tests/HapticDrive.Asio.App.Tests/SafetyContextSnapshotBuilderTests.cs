using HapticDrive.Asio.App;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Asio.App.Tests;

public sealed class SafetyContextSnapshotBuilderTests
{
    [Fact]
    public void BuildMockRuntimeSnapshot_MapsStoppedTelemetryStaleEmergencyMuteContext()
    {
        var snapshot = SafetyContextSnapshotBuilder.BuildMockRuntimeSnapshot(
            OutputSnapshot(isConnected: true, emergencyStopActive: true, brakeAvailable: true, throttleAvailable: false),
            telemetryStale: true,
            hapticsStopped: true,
            emergencyMuteActive: true,
            drivingArmed: false,
            PHprSoftwareConflictStatus.SimHubRunning);

        Assert.True(snapshot.IsMockOutput);
        Assert.True(snapshot.IsDeviceConnected);
        Assert.True(snapshot.BrakeModuleAvailable);
        Assert.False(snapshot.ThrottleModuleAvailable);
        Assert.True(snapshot.TelemetryStale);
        Assert.True(snapshot.HapticsStopped);
        Assert.True(snapshot.EmergencyMuteActive);
        Assert.False(snapshot.DrivingArmed);
        Assert.True(snapshot.EmergencyStopActive);
        Assert.Equal(PHprSoftwareConflictStatus.SimHubRunning, snapshot.SoftwareConflictStatus);
        Assert.False(snapshot.RequiresRealDeviceWrites);
    }

    [Fact]
    public void BuildRealRuntimeSnapshot_MapsClearAllowedRealWriteContext()
    {
        var snapshot = SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
            OutputSnapshot(isConnected: true, emergencyStopActive: false, brakeAvailable: true, throttleAvailable: true),
            telemetryStale: false,
            hapticsStopped: false,
            emergencyMuteActive: false,
            drivingArmed: true,
            PHprSoftwareConflictStatus.Clear);

        var context = snapshot.ToSafetyContext();

        Assert.False(context.IsMockOutput);
        Assert.True(context.IsDeviceConnected);
        Assert.True(context.BrakeModuleAvailable);
        Assert.True(context.ThrottleModuleAvailable);
        Assert.False(context.TelemetryStale);
        Assert.False(context.HapticsStopped);
        Assert.False(context.EmergencyMuteActive);
        Assert.True(context.DrivingArmed);
        Assert.False(context.EmergencyStopActive);
        Assert.Equal(PHprSoftwareConflictStatus.Clear, context.SoftwareConflictStatus);
        Assert.True(context.RequiresRealDeviceWrites);
    }

    [Fact]
    public void BuildManualRealSnapshot_UsesSelectorStateForDeviceAndModuleAvailability()
    {
        var selected = SafetyContextSnapshotBuilder.BuildManualRealSnapshot(
            selectorIsSelected: true,
            emergencyMuteActive: false,
            emergencyStopActive: false,
            PHprSoftwareConflictStatus.Clear);
        var unselected = SafetyContextSnapshotBuilder.BuildManualRealSnapshot(
            selectorIsSelected: false,
            emergencyMuteActive: true,
            emergencyStopActive: true,
            PHprSoftwareConflictStatus.ActiveConflict);

        Assert.True(selected.IsDeviceConnected);
        Assert.True(selected.BrakeModuleAvailable);
        Assert.True(selected.ThrottleModuleAvailable);
        Assert.True(selected.DrivingArmed);
        Assert.False(selected.TelemetryStale);
        Assert.False(selected.HapticsStopped);
        Assert.True(selected.RequiresRealDeviceWrites);

        Assert.False(unselected.IsDeviceConnected);
        Assert.False(unselected.BrakeModuleAvailable);
        Assert.False(unselected.ThrottleModuleAvailable);
        Assert.True(unselected.EmergencyMuteActive);
        Assert.True(unselected.EmergencyStopActive);
        Assert.Equal(PHprSoftwareConflictStatus.ActiveConflict, unselected.SoftwareConflictStatus);
    }

    [Fact]
    public void BuildBenchMockSnapshot_ForcesSafeNoTelemetryNoConflictMockShape()
    {
        var snapshot = SafetyContextSnapshotBuilder.BuildBenchMockSnapshot(
            OutputSnapshot(isConnected: false, emergencyStopActive: true, brakeAvailable: false, throttleAvailable: true),
            emergencyMuteActive: true);

        Assert.True(snapshot.IsMockOutput);
        Assert.False(snapshot.IsDeviceConnected);
        Assert.False(snapshot.BrakeModuleAvailable);
        Assert.True(snapshot.ThrottleModuleAvailable);
        Assert.False(snapshot.TelemetryStale);
        Assert.False(snapshot.HapticsStopped);
        Assert.True(snapshot.DrivingArmed);
        Assert.Equal(PHprSoftwareConflictStatus.Clear, snapshot.SoftwareConflictStatus);
        Assert.False(snapshot.RequiresRealDeviceWrites);
    }

    [Fact]
    public void BuildBenchDirectSnapshot_UsesRealOutputAndCoexistenceStatus()
    {
        var snapshot = SafetyContextSnapshotBuilder.BuildBenchDirectSnapshot(
            OutputSnapshot(isConnected: true, emergencyStopActive: false, brakeAvailable: true, throttleAvailable: false),
            emergencyMuteActive: false,
            PHprSoftwareConflictStatus.ActiveConflict);

        Assert.False(snapshot.IsMockOutput);
        Assert.True(snapshot.IsDeviceConnected);
        Assert.True(snapshot.BrakeModuleAvailable);
        Assert.False(snapshot.ThrottleModuleAvailable);
        Assert.False(snapshot.TelemetryStale);
        Assert.False(snapshot.HapticsStopped);
        Assert.True(snapshot.DrivingArmed);
        Assert.Equal(PHprSoftwareConflictStatus.ActiveConflict, snapshot.SoftwareConflictStatus);
        Assert.True(snapshot.RequiresRealDeviceWrites);
    }

    [Fact]
    public void BuilderOutput_IsDeterministicAndDoesNotPersistUnsafeRuntimeState()
    {
        var first = SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
            OutputSnapshot(isConnected: true, emergencyStopActive: true, brakeAvailable: true, throttleAvailable: true),
            telemetryStale: true,
            hapticsStopped: false,
            emergencyMuteActive: true,
            drivingArmed: false,
            PHprSoftwareConflictStatus.SimProRunning);
        var second = SafetyContextSnapshotBuilder.BuildRealRuntimeSnapshot(
            OutputSnapshot(isConnected: true, emergencyStopActive: true, brakeAvailable: true, throttleAvailable: true),
            telemetryStale: true,
            hapticsStopped: false,
            emergencyMuteActive: true,
            drivingArmed: false,
            PHprSoftwareConflictStatus.SimProRunning);

        Assert.Equal(first, second);
        Assert.DoesNotContain("Selector", first.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DevicePath", first.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlEnabled", first.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DirectControlArmed", first.ToString(), StringComparison.Ordinal);
    }

    private static PHprOutputSnapshot OutputSnapshot(
        bool isConnected,
        bool emergencyStopActive,
        bool brakeAvailable,
        bool throttleAvailable)
    {
        return new PHprOutputSnapshot(
            IsMock: false,
            IsConnected: isConnected,
            IsEmergencyStopActive: emergencyStopActive,
            AcceptedCommandCount: 0,
            RejectedCommandCount: 0,
            LastCommand: null,
            LastStatus: null,
            LastMessage: null,
            LastCommandUtc: null,
            SafetyLimits: PHprSafetyLimits.Default,
            BrakeAvailable: brakeAvailable,
            ThrottleAvailable: throttleAvailable);
    }
}
