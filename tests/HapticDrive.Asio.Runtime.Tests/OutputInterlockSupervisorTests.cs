using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Safety;

namespace HapticDrive.Asio.Runtime.Tests;

public sealed class OutputInterlockSupervisorTests
{
    [Fact]
    public async Task TripSilencesAllParticipants()
    {
        var interlock = ReadyInterlock();
        var first = new FakeParticipant("first");
        var second = new FakeParticipant("second");
        await using var supervisor = new OutputInterlockSupervisor(interlock, [first, second]);

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip for supervisor.");
        await WaitUntilAsync(() => first.SilenceCallCount == 1 && second.SilenceCallCount == 1);

        Assert.True(first.Current.IsSilent);
        Assert.True(second.Current.IsSilent);
    }

    [Fact]
    public async Task ParticipantExceptionRecordedAndRemainingParticipantsStillSilenced()
    {
        var interlock = ReadyInterlock();
        var failing = new FakeParticipant("failing") { ThrowOnSilence = true };
        var remaining = new FakeParticipant("remaining");
        await using var supervisor = new OutputInterlockSupervisor(interlock, [failing, remaining]);

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip for supervisor failure.");
        await WaitUntilAsync(() => supervisor.Current.ParticipantFailureCount == 1 && remaining.SilenceCallCount == 1);

        Assert.Contains("failing", supervisor.Current.LastFailure, StringComparison.Ordinal);
        Assert.True(remaining.Current.IsSilent);
    }

    [Fact]
    public async Task ParticipantTimeoutRecorded()
    {
        var interlock = ReadyInterlock();
        var timingOut = new FakeParticipant("slow") { DelaySilenceUntilCanceled = true };
        await using var supervisor = new OutputInterlockSupervisor(interlock, [timingOut]);

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip for timeout.");
        await WaitUntilAsync(() => supervisor.Current.ParticipantFailureCount == 1, timeout: TimeSpan.FromSeconds(2));

        Assert.Contains("timed out", supervisor.Current.LastFailure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetBlockedWhenParticipantCannotReset()
    {
        var interlock = new OutputInterlock();
        var participant = new FakeParticipant("blocked") { ResetBlocker = "participant fault remains" };
        await using var supervisor = new OutputInterlockSupervisor(interlock, [participant]);

        Assert.False(supervisor.CanReset(out var blocker));
        Assert.Contains("participant fault remains", blocker, StringComparison.Ordinal);
        Assert.False(interlock.Reset("Reset must remain blocked by participant."));
        Assert.True(interlock.Current.IsLatched);
    }

    [Fact]
    public async Task ResetNotifiesParticipantsAfterClear()
    {
        var interlock = new OutputInterlock();
        var participant = new FakeParticipant("participant");
        await using var supervisor = new OutputInterlockSupervisor(interlock, [participant]);

        Assert.True(supervisor.CanReset(out _));
        var reset = interlock.Reset("Reset after participant readiness.");
        await WaitUntilAsync(() => participant.ResetCallCount == 1);

        Assert.True(reset);
        Assert.False(participant.ResetObservedInterlockLatched);
    }

    [Fact]
    public async Task SupervisorUnsubscribesOnDispose()
    {
        var interlock = ReadyInterlock();
        var participant = new FakeParticipant("participant");
        var supervisor = new OutputInterlockSupervisor(interlock, [participant]);
        await supervisor.DisposeAsync();

        interlock.Trip(OutputInterlockReason.UserEmergencyMute, "Trip after dispose.");
        await Task.Delay(100);

        Assert.Equal(0, participant.SilenceCallCount);
    }

    [Fact]
    public async Task RapidTripsDropOldSnapshotsAndProcessLatest()
    {
        var interlock = ReadyInterlock();
        var participant = new FakeParticipant("slow") { SilenceDelay = TimeSpan.FromMilliseconds(40) };
        await using var supervisor = new OutputInterlockSupervisor(interlock, [participant]);

        for (var index = 0; index < 20; index++)
        {
            interlock.Trip(OutputInterlockReason.UserEmergencyMute, $"rapid trip {index}");
        }

        await WaitUntilAsync(() => supervisor.Current.Interlock.Message == "rapid trip 19", timeout: TimeSpan.FromSeconds(3));

        Assert.True(supervisor.Current.Interlock.IsLatched);
        Assert.Equal("rapid trip 19", supervisor.Current.Interlock.Message);
    }

    private static OutputInterlock ReadyInterlock()
    {
        var interlock = new OutputInterlock();
        Assert.True(interlock.Reset("Ready for supervisor test."));
        return interlock;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(1));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeParticipant : IOutputSafetyParticipant
    {
        public FakeParticipant(string name)
        {
            Name = name;
            Current = new OutputSafetyParticipantSnapshot(name, IsSilent: true, HasFault: false, "ready");
        }

        public string Name { get; }

        public OutputSafetyParticipantSnapshot Current { get; private set; }

        public bool ThrowOnSilence { get; init; }

        public bool DelaySilenceUntilCanceled { get; init; }

        public TimeSpan SilenceDelay { get; init; }

        public string? ResetBlocker { get; init; }

        public int SilenceCallCount { get; private set; }

        public int ResetCallCount { get; private set; }

        public bool ResetObservedInterlockLatched { get; private set; }

        public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
        {
            SilenceCallCount++;
            if (ThrowOnSilence)
            {
                throw new InvalidOperationException("silence failed");
            }

            if (DelaySilenceUntilCanceled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (SilenceDelay > TimeSpan.Zero)
            {
                await Task.Delay(SilenceDelay, cancellationToken);
            }

            Current = Current with { IsSilent = true, HasFault = false, Message = interlock.Message };
        }

        public bool CanReset(out string blocker)
        {
            blocker = ResetBlocker ?? string.Empty;
            return string.IsNullOrWhiteSpace(ResetBlocker);
        }

        public void OnInterlockReset(OutputInterlockSnapshot interlock)
        {
            ResetCallCount++;
            ResetObservedInterlockLatched = interlock.IsLatched;
        }
    }
}
