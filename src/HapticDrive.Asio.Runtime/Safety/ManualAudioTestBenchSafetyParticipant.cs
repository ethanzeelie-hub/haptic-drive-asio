using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Safety;

namespace HapticDrive.Asio.Runtime.Safety;

public sealed class ManualAudioTestBenchSafetyParticipant : IOutputSafetyParticipant
{
    private readonly AudioTestBench _testBench;
    private OutputSafetyParticipantSnapshot _current;

    public ManualAudioTestBenchSafetyParticipant(AudioTestBench testBench, string? name = null)
    {
        _testBench = testBench ?? throw new ArgumentNullException(nameof(testBench));
        Name = string.IsNullOrWhiteSpace(name) ? "Manual audio test bench" : name.Trim();
        _current = CreateSnapshot(isSilent: !testBench.GetSnapshot().IsActive, hasFault: false, "Manual audio test bench participant ready.");
    }

    public string Name { get; }

    public OutputSafetyParticipantSnapshot Current => _current;

    public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
    {
        _testBench.EmergencyMute = true;
        var snapshot = _testBench.GetSnapshot();
        if (snapshot.IsActive)
        {
            var stopResult = await _testBench.StopAsync(cancellationToken).ConfigureAwait(false);
            if (!stopResult.Succeeded)
            {
                _current = CreateSnapshot(isSilent: false, hasFault: true, stopResult.Message);
                return;
            }
        }

        _current = CreateSnapshot(isSilent: true, hasFault: false, "Manual audio test bench silenced by global output interlock.");
    }

    public bool CanReset(out string blocker)
    {
        if (_current.HasFault)
        {
            blocker = _current.Message;
            return false;
        }

        blocker = string.Empty;
        return true;
    }

    public void OnInterlockReset(OutputInterlockSnapshot interlock)
    {
        _testBench.EmergencyMute = false;
        _current = CreateSnapshot(isSilent: !_testBench.GetSnapshot().IsActive, hasFault: false, "Manual audio test bench interlock reset observed.");
    }

    private OutputSafetyParticipantSnapshot CreateSnapshot(bool isSilent, bool hasFault, string message)
    {
        return new OutputSafetyParticipantSnapshot(Name, isSilent, hasFault, message);
    }
}
