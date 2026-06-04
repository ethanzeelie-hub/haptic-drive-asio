namespace HapticDrive.Input.Abstractions.Driving;

public interface IDrivingArmedStateProvider
{
    event EventHandler<DrivingArmedState>? DrivingArmedChanged;

    DrivingArmedState Current { get; }
}
