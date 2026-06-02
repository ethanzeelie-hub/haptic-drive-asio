using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.Diagnostics;

public sealed record AudioRuntimeDiagnosticsSnapshot(
    AudioOutputStatus Output,
    HapticEffectEngineSnapshot Effects,
    AudioRenderPipelineSnapshot? Pipeline,
    AudioTestBenchSnapshot TestBench,
    bool HardwareAbsentMode,
    bool RequiresPhysicalHardware,
    bool IsManualDebugOutput,
    bool EmergencyMute,
    int ActiveEffectCount,
    float EffectPeakLevel,
    float MixerPeakLevel,
    float OutputPeakLevel,
    int LimitedSampleCount,
    int ClippedSampleCount)
{
    public static AudioRuntimeDiagnosticsSnapshot Create(
        AudioOutputStatus output,
        HapticEffectEngineSnapshot effects,
        AudioRenderPipelineSnapshot? pipeline,
        AudioTestBenchSnapshot testBench)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(testBench);

        var emergencyMute = pipeline?.EmergencyMute
            ?? testBench.EmergencyMute;

        return new AudioRuntimeDiagnosticsSnapshot(
            output,
            effects,
            pipeline,
            testBench,
            HardwareAbsentMode: output.Kind == AudioOutputDeviceKind.Null
                && !output.RequiresPhysicalHardware,
            RequiresPhysicalHardware: output.RequiresPhysicalHardware,
            IsManualDebugOutput: output.IsManualDebugOnly,
            emergencyMute,
            effects.ActiveEffectCount,
            effects.PeakLevel,
            pipeline?.MixerPeakLevel ?? testBench.MixerPeakLevel,
            pipeline?.OutputPeakLevel ?? testBench.OutputPeakLevel,
            pipeline?.LimitedSampleCount ?? testBench.LimitedSampleCount,
            pipeline?.ClippedSampleCount ?? testBench.ClippedSampleCount);
    }
}
