using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;

namespace HapticDrive.Simagic.PHPR.Tests;

public sealed class PHprSoftwareCoexistenceTests
{
    [Fact]
    public void NoProcessesReportsClear()
    {
        var detector = Detector([]);

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.Clear, snapshot.Status);
        Assert.False(snapshot.SimProRunning);
        Assert.False(snapshot.SimHubRunning);
    }

    [Fact]
    public void SimProOnlyReportsSimProRunning()
    {
        var detector = Detector([new PHprDetectedSoftwareProcess("SimProManager", 100)]);

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.SimProRunning, snapshot.Status);
        Assert.True(snapshot.SimProRunning);
        Assert.False(snapshot.SimHubRunning);
        Assert.Single(snapshot.SimProProcesses);
    }

    [Fact]
    public void SimHubOnlyReportsSimHubRunning()
    {
        var detector = Detector([new PHprDetectedSoftwareProcess("SimHubWPF", 200)]);

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.SimHubRunning, snapshot.Status);
        Assert.False(snapshot.SimProRunning);
        Assert.True(snapshot.SimHubRunning);
        Assert.Single(snapshot.SimHubProcesses);
    }

    [Fact]
    public void BothProcessesReportsActiveConflict()
    {
        var detector = Detector(
        [
            new PHprDetectedSoftwareProcess("SimProManager", 100),
            new PHprDetectedSoftwareProcess("SimHubWPF", 200)
        ]);

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.ActiveConflict, snapshot.Status);
        Assert.True(snapshot.SimProRunning);
        Assert.True(snapshot.SimHubRunning);
        Assert.Contains("blocked", snapshot.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderErrorsAreHandledSafelyAsUnknown()
    {
        var detector = new PHprSoftwareCoexistenceDetector(new ThrowingProcessProvider());

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.Unknown, snapshot.Status);
        Assert.False(snapshot.SimProRunning);
        Assert.False(snapshot.SimHubRunning);
        Assert.NotNull(snapshot.ErrorMessage);
    }

    [Fact]
    public void ProviderSnapshotErrorsAreHandledSafelyAsUnknown()
    {
        var detector = new PHprSoftwareCoexistenceDetector(
            new FakeProcessProvider(
                new PHprSoftwareProcessSnapshot(
                    [],
                    DateTimeOffset.UtcNow,
                    IsSupported: true,
                    "Access denied while reading process metadata.")));

        var snapshot = detector.Scan();

        Assert.Equal(PHprSoftwareConflictStatus.Unknown, snapshot.Status);
        Assert.Contains("Access denied", snapshot.ErrorMessage);
    }

    [Fact]
    public void NonWindowsProviderFallbackIsSafeAndUnknown()
    {
        var provider = new WindowsProcessSnapshotProvider(() => false);
        var detector = new PHprSoftwareCoexistenceDetector(provider);

        var processSnapshot = provider.GetSnapshot();
        var coexistence = detector.Scan();

        Assert.False(processSnapshot.IsSupported);
        Assert.Equal(PHprSoftwareConflictStatus.Unknown, coexistence.Status);
        Assert.False(coexistence.IsSupported);
    }

    [Fact]
    public void ActiveConflictRejectsStartCommandThroughSafetyLimiter()
    {
        var context = PHprSafetyContext.DefaultMock with
        {
            SoftwareConflictStatus = PHprSoftwareConflictStatus.ActiveConflict
        };
        var command = PHprCommand.Create(PHprModuleId.Brake, 0.05d, 50d, 50, PHprCommandSource.TestBench);

        var decision = new PHprSafetyLimiter().Evaluate(command, context);

        Assert.False(decision.Accepted);
        Assert.Equal(PHprSafetyViolationCode.SimProConflict, decision.Violation.Code);
    }

    [Fact]
    public void CoexistenceTypesDoNotExposeControlHookKillOrWriteApis()
    {
        var forbiddenTerms = new[] { "Kill", "Hook", "Inject", "Patch", "Write", "SetFeature", "Send", "StartProcess" };
        var methodNames = typeof(PHprSoftwareCoexistenceDetector).Assembly.GetTypes()
            .Where(type => type.Namespace == "HapticDrive.Simagic.PHPR.Abstractions.Coexistence")
            .SelectMany(type => type.GetMethods())
            .Where(method => method.DeclaringType != typeof(object))
            .Select(method => method.Name)
            .Distinct()
            .ToArray();

        foreach (var methodName in methodNames)
        {
            Assert.DoesNotContain(forbiddenTerms, term => methodName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static PHprSoftwareCoexistenceDetector Detector(IReadOnlyList<PHprDetectedSoftwareProcess> processes)
    {
        return new PHprSoftwareCoexistenceDetector(
            new FakeProcessProvider(
                new PHprSoftwareProcessSnapshot(
                    processes,
                    new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero),
                    IsSupported: true)));
    }

    private sealed class FakeProcessProvider(PHprSoftwareProcessSnapshot snapshot) : IPHprSoftwareProcessProvider
    {
        public PHprSoftwareProcessSnapshot GetSnapshot()
        {
            return snapshot;
        }
    }

    private sealed class ThrowingProcessProvider : IPHprSoftwareProcessProvider
    {
        public PHprSoftwareProcessSnapshot GetSnapshot()
        {
            throw new UnauthorizedAccessException("Access denied.");
        }
    }
}
