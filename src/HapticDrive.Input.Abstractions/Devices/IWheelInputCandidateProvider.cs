namespace HapticDrive.Input.Abstractions.Devices;

public interface IWheelInputCandidateProvider
{
    InputDeviceInfo ScoreDevice(InputDeviceInfo device);

    IReadOnlyList<InputDeviceInfo> GetCandidates(InputDeviceDiscoverySnapshot snapshot);
}
