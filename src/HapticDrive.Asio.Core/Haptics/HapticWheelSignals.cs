namespace HapticDrive.Asio.Core.Haptics;

public sealed record HapticWheelSignals<T>(
    T RearLeft,
    T RearRight,
    T FrontLeft,
    T FrontRight);
