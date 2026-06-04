using HapticDrive.Input.Abstractions.Devices;

namespace HapticDrive.Input.Windows;

public sealed class WindowsInputDeviceDiscovery : IInputDeviceDiscovery
{
    private readonly IReadOnlyList<IWindowsInputDeviceEnumerator> _enumerators;
    private readonly IWheelInputCandidateProvider _candidateProvider;

    public WindowsInputDeviceDiscovery()
        : this(
            [
                new RawInputDeviceEnumerator(),
                new WindowsGameControllerDeviceEnumerator()
            ],
            new WheelInputCandidateProvider())
    {
    }

    public WindowsInputDeviceDiscovery(
        IEnumerable<IWindowsInputDeviceEnumerator> enumerators,
        IWheelInputCandidateProvider? candidateProvider = null)
    {
        ArgumentNullException.ThrowIfNull(enumerators);

        _enumerators = enumerators.ToArray();
        _candidateProvider = candidateProvider ?? new WheelInputCandidateProvider();
    }

    public ValueTask<InputDeviceDiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var discoveredAtUtc = DateTimeOffset.UtcNow;
        var methods = new List<InputDiscoveryMethod>();
        var errors = new List<string>();
        var devices = new List<InputDeviceInfo>();

        foreach (var enumerator in _enumerators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            methods.Add(enumerator.Method);

            try
            {
                foreach (var device in enumerator.DiscoverDevices(discoveredAtUtc))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    devices.Add(_candidateProvider.ScoreDevice(device));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{enumerator.Method}: {ex.Message}");
            }
        }

        var snapshot = InputDeviceDiscoverySnapshot.Create(
            devices,
            methods.Distinct().ToArray(),
            errors,
            discoveredAtUtc);
        return ValueTask.FromResult(snapshot);
    }
}
