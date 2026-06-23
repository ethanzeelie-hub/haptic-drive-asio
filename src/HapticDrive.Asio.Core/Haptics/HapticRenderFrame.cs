namespace HapticDrive.Asio.Core.Haptics;

public readonly record struct HapticRenderFrame(
    HapticFrame Frame,
    HapticFrameFreshnessSnapshot Freshness);
