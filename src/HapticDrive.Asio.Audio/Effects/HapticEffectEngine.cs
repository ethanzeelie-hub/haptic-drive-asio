using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class HapticEffectEngine
{
    private readonly object _gate = new();
    private readonly EngineVibrationEffect _engineEffect;
    private readonly GearShiftEffect _gearShiftEffect;
    private readonly AudioSampleBuffer _engineBuffer;
    private readonly AudioSampleBuffer _gearShiftBuffer;

    private HapticEffectEngineSnapshot _snapshot;

    public HapticEffectEngine(AudioSampleFormat format)
        : this(format, HapticEffectEngineOptions.Default)
    {
    }

    public HapticEffectEngine(AudioSampleFormat format, HapticEffectEngineOptions options)
    {
        Format = format ?? throw new ArgumentNullException(nameof(format));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _engineEffect = new EngineVibrationEffect(Options.Engine);
        _gearShiftEffect = new GearShiftEffect(Options.GearShift);
        _engineBuffer = AudioSampleBuffer.Allocate(format);
        _gearShiftBuffer = AudioSampleBuffer.Allocate(format);
        _snapshot = CreateSnapshot(
            _engineEffect.Snapshot,
            _gearShiftEffect.Snapshot,
            activeEffectCount: 0,
            peakLevel: 0f);
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options { get; }

    public void Reset()
    {
        lock (_gate)
        {
            _engineEffect.Reset();
            _gearShiftEffect.Reset();
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                activeEffectCount: 0,
                peakLevel: 0f);
        }
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        lock (_gate)
        {
            _engineEffect.Update(vehicleState);
            _gearShiftEffect.Update(vehicleState);
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                activeEffectCount: 0,
                peakLevel: 0f);
        }
    }

    public HapticEffectEngineSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public HapticEffectEngineRenderResult RenderNextBuffer()
    {
        lock (_gate)
        {
            var inputs = new List<AudioMixerInput>(capacity: 2);
            var engineResult = _engineEffect.Render(_engineBuffer);
            var gearShiftResult = _gearShiftEffect.Render(_gearShiftBuffer);

            if (engineResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_engineBuffer, name: _engineEffect.Name));
            }

            if (gearShiftResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_gearShiftBuffer, name: _gearShiftEffect.Name));
            }

            var peakLevel = Math.Max(engineResult.PeakLevel, gearShiftResult.PeakLevel);
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                inputs.Count,
                peakLevel);

            return new HapticEffectEngineRenderResult(inputs, _snapshot);
        }
    }

    private static HapticEffectEngineSnapshot CreateSnapshot(
        EngineVibrationEffectSnapshot engine,
        GearShiftEffectSnapshot gearShift,
        int activeEffectCount,
        float peakLevel)
    {
        return new HapticEffectEngineSnapshot(
            engine,
            gearShift,
            activeEffectCount,
            peakLevel);
    }
}

public sealed record HapticEffectEngineRenderResult(
    IReadOnlyList<AudioMixerInput> MixerInputs,
    HapticEffectEngineSnapshot Snapshot);

public sealed record HapticEffectEngineSnapshot(
    EngineVibrationEffectSnapshot Engine,
    GearShiftEffectSnapshot GearShift,
    int ActiveEffectCount,
    float PeakLevel);
