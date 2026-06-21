namespace HapticDrive.Asio.Core.Safety;

public interface IOutputInterlock
{
    OutputInterlockSnapshot Current { get; }

    bool AllowsOutput { get; }

    void Trip(OutputInterlockReason reason, string message);

    bool Reset(string message);

    event EventHandler<OutputInterlockSnapshot>? Changed;
}
