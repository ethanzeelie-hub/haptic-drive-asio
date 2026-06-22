using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Runtime.Pipeline;

namespace HapticDrive.Asio.Runtime.Safety;

public sealed class AudioOutputSafetyParticipant : IOutputSafetyParticipant
{
    private readonly Func<HapticPipelineCoordinator> _pipelineProvider;
    private OutputSafetyParticipantSnapshot _current;

    public AudioOutputSafetyParticipant(HapticPipelineCoordinator pipeline, string? name = null)
        : this(() => pipeline, name)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
    }

    public AudioOutputSafetyParticipant(Func<HapticPipelineCoordinator> pipelineProvider, string? name = null)
    {
        _pipelineProvider = pipelineProvider ?? throw new ArgumentNullException(nameof(pipelineProvider));
        Name = string.IsNullOrWhiteSpace(name) ? "Audio output" : name.Trim();
        _current = CreateSnapshot(isSilent: IsOutputSilent(), hasFault: false, "Audio output participant ready.");
    }

    public string Name { get; }

    public OutputSafetyParticipantSnapshot Current => _current;

    public async ValueTask SilenceAsync(OutputInterlockSnapshot interlock, CancellationToken cancellationToken)
    {
        var pipeline = _pipelineProvider();
        pipeline.StopManualAsioHardwareTest($"Global output interlock latched: {interlock.Reason}.");

        var status = pipeline.OutputDevice.GetStatus();
        if (status.State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Started or AudioOutputDeviceState.Stopped)
        {
            var stopResult = await pipeline.OutputDevice.StopAsync(cancellationToken).ConfigureAwait(false);
            if (!stopResult.Succeeded)
            {
                _current = CreateSnapshot(isSilent: false, hasFault: true, stopResult.Message);
                return;
            }
        }

        _current = CreateSnapshot(isSilent: IsOutputSilent(), hasFault: false, "Audio output silenced by global output interlock.");
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
        _current = CreateSnapshot(isSilent: IsOutputSilent(), hasFault: false, "Audio output interlock reset observed.");
    }

    private bool IsOutputSilent()
    {
        var status = _pipelineProvider().OutputDevice.GetStatus();
        return status.State is not AudioOutputDeviceState.Started || !status.IsStreaming;
    }

    private OutputSafetyParticipantSnapshot CreateSnapshot(bool isSilent, bool hasFault, string message)
    {
        return new OutputSafetyParticipantSnapshot(Name, isSilent, hasFault, message);
    }
}
