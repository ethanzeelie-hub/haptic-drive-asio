using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class HapticEffectEngine
{
    private readonly object _gate = new();
    private EngineVibrationEffect _engineEffect;
    private GearShiftEffect _gearShiftEffect;
    private KerbEffect _kerbEffect;
    private ImpactEffect _impactEffect;
    private RoadTextureEffect _roadTextureEffect;
    private SlipEffect _slipEffect;
    private readonly AudioSampleBuffer _engineBuffer;
    private readonly AudioSampleBuffer _gearShiftBuffer;
    private readonly AudioSampleBuffer _kerbBuffer;
    private readonly AudioSampleBuffer _impactBuffer;
    private readonly AudioSampleBuffer _roadTextureBuffer;
    private readonly AudioSampleBuffer _slipBuffer;

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
        _kerbEffect = new KerbEffect(Options.Kerb);
        _impactEffect = new ImpactEffect(Options.Impact);
        _roadTextureEffect = new RoadTextureEffect(Options.RoadTexture);
        _slipEffect = new SlipEffect(Options.Slip);
        _engineBuffer = AudioSampleBuffer.Allocate(format);
        _gearShiftBuffer = AudioSampleBuffer.Allocate(format);
        _kerbBuffer = AudioSampleBuffer.Allocate(format);
        _impactBuffer = AudioSampleBuffer.Allocate(format);
        _roadTextureBuffer = AudioSampleBuffer.Allocate(format);
        _slipBuffer = AudioSampleBuffer.Allocate(format);
        _snapshot = CreateSnapshot(
            _engineEffect.Snapshot,
            _gearShiftEffect.Snapshot,
            _kerbEffect.Snapshot,
            _impactEffect.Snapshot,
            _roadTextureEffect.Snapshot,
            _slipEffect.Snapshot,
            activeEffectCount: 0,
            peakLevel: 0f);
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options { get; private set; }

    public void UpdateOptions(HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_gate)
        {
            Options = options;
            _engineEffect = new EngineVibrationEffect(Options.Engine);
            _gearShiftEffect = new GearShiftEffect(Options.GearShift);
            _kerbEffect = new KerbEffect(Options.Kerb);
            _impactEffect = new ImpactEffect(Options.Impact);
            _roadTextureEffect = new RoadTextureEffect(Options.RoadTexture);
            _slipEffect = new SlipEffect(Options.Slip);
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                _kerbEffect.Snapshot,
                _impactEffect.Snapshot,
                _roadTextureEffect.Snapshot,
                _slipEffect.Snapshot,
                activeEffectCount: 0,
                peakLevel: 0f);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _engineEffect.Reset();
            _gearShiftEffect.Reset();
            _kerbEffect.Reset();
            _impactEffect.Reset();
            _roadTextureEffect.Reset();
            _slipEffect.Reset();
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                _kerbEffect.Snapshot,
                _impactEffect.Snapshot,
                _roadTextureEffect.Snapshot,
                _slipEffect.Snapshot,
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
            _kerbEffect.Update(vehicleState);
            _impactEffect.Update(vehicleState);
            _roadTextureEffect.Update(vehicleState);
            _slipEffect.Update(vehicleState);
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                _kerbEffect.Snapshot,
                _impactEffect.Snapshot,
                _roadTextureEffect.Snapshot,
                _slipEffect.Snapshot,
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
            var inputs = new List<AudioMixerInput>(capacity: 6);
            var engineResult = _engineEffect.Render(_engineBuffer);
            var gearShiftResult = _gearShiftEffect.Render(_gearShiftBuffer);
            var kerbResult = _kerbEffect.Render(_kerbBuffer);
            var impactResult = _impactEffect.Render(_impactBuffer);
            var roadTextureResult = _roadTextureEffect.Render(_roadTextureBuffer);
            var slipResult = _slipEffect.Render(_slipBuffer);

            if (engineResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_engineBuffer, name: _engineEffect.Name));
            }

            if (gearShiftResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_gearShiftBuffer, name: _gearShiftEffect.Name));
            }

            if (kerbResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_kerbBuffer, name: _kerbEffect.Name));
            }

            if (impactResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_impactBuffer, name: _impactEffect.Name));
            }

            if (roadTextureResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_roadTextureBuffer, name: _roadTextureEffect.Name));
            }

            if (slipResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_slipBuffer, name: _slipEffect.Name));
            }

            var peakLevel = Math.Max(
                Math.Max(engineResult.PeakLevel, gearShiftResult.PeakLevel),
                Math.Max(
                    Math.Max(kerbResult.PeakLevel, impactResult.PeakLevel),
                    Math.Max(roadTextureResult.PeakLevel, slipResult.PeakLevel)));
            _snapshot = CreateSnapshot(
                _engineEffect.Snapshot,
                _gearShiftEffect.Snapshot,
                _kerbEffect.Snapshot,
                _impactEffect.Snapshot,
                _roadTextureEffect.Snapshot,
                _slipEffect.Snapshot,
                inputs.Count,
                peakLevel);

            return new HapticEffectEngineRenderResult(inputs, _snapshot);
        }
    }

    private static HapticEffectEngineSnapshot CreateSnapshot(
        EngineVibrationEffectSnapshot engine,
        GearShiftEffectSnapshot gearShift,
        KerbEffectSnapshot kerb,
        ImpactEffectSnapshot impact,
        RoadTextureEffectSnapshot roadTexture,
        SlipEffectSnapshot slip,
        int activeEffectCount,
        float peakLevel)
    {
        return new HapticEffectEngineSnapshot(
            engine,
            gearShift,
            kerb,
            impact,
            roadTexture,
            slip,
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
    KerbEffectSnapshot Kerb,
    ImpactEffectSnapshot Impact,
    RoadTextureEffectSnapshot RoadTexture,
    SlipEffectSnapshot Slip,
    int ActiveEffectCount,
    float PeakLevel);
