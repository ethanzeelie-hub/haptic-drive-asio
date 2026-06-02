using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public interface IHapticEffectSource
{
    string Name { get; }

    void Reset();

    void Update(VehicleState vehicleState);

    HapticEffectRenderResult Render(AudioSampleBuffer destination);
}

public sealed record HapticEffectRenderResult(
    string Name,
    bool IsEnabled,
    bool IsActive,
    float PeakLevel);
