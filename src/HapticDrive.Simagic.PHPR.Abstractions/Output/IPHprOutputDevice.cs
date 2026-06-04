using HapticDrive.Simagic.PHPR.Abstractions.Commands;

namespace HapticDrive.Simagic.PHPR.Abstractions.Output;

public interface IPHprOutputDevice : IAsyncDisposable
{
    PHprOutputSnapshot GetSnapshot();

    ValueTask<PHprCommandResult> SendAsync(PHprCommand command, CancellationToken cancellationToken = default);

    ValueTask EmergencyStopAsync(CancellationToken cancellationToken = default);
}
