using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Vehicle;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class HapticEffectEngine
{
    private readonly object _gate = new();
    private readonly EffectSlot<EngineVibrationEffect, EngineVibrationEffectOptions> _engineEffect;
    private readonly EffectSlot<GearShiftEffect, GearShiftEffectOptions> _gearShiftEffect;
    private readonly EffectSlot<KerbEffect, KerbEffectOptions> _kerbEffect;
    private readonly EffectSlot<ImpactEffect, ImpactEffectOptions> _impactEffect;
    private readonly EffectSlot<RoadTextureEffect, RoadTextureEffectOptions> _roadTextureEffect;
    private readonly EffectSlot<SlipEffect, SlipEffectOptions> _slipEffect;
    private readonly IReadOnlyList<IEffectSlot> _effectSlots;
    private HapticEffectEngineSnapshot _snapshot;

    public HapticEffectEngine(AudioSampleFormat format)
        : this(format, HapticEffectEngineOptions.Default)
    {
    }

    public HapticEffectEngine(AudioSampleFormat format, HapticEffectEngineOptions options)
    {
        Format = format ?? throw new ArgumentNullException(nameof(format));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _engineEffect = new EffectSlot<EngineVibrationEffect, EngineVibrationEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Engine,
            static effectOptions => new EngineVibrationEffect(effectOptions));
        _gearShiftEffect = new EffectSlot<GearShiftEffect, GearShiftEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.GearShift,
            static effectOptions => new GearShiftEffect(effectOptions));
        _kerbEffect = new EffectSlot<KerbEffect, KerbEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Kerb,
            static effectOptions => new KerbEffect(effectOptions));
        _impactEffect = new EffectSlot<ImpactEffect, ImpactEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Impact,
            static effectOptions => new ImpactEffect(effectOptions));
        _roadTextureEffect = new EffectSlot<RoadTextureEffect, RoadTextureEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.RoadTexture,
            static effectOptions => new RoadTextureEffect(effectOptions));
        _slipEffect = new EffectSlot<SlipEffect, SlipEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Slip,
            static effectOptions => new SlipEffect(effectOptions));
        _effectSlots =
        [
            _engineEffect,
            _gearShiftEffect,
            _kerbEffect,
            _impactEffect,
            _roadTextureEffect,
            _slipEffect
        ];
        _snapshot = CreateSnapshot(activeEffectCount: 0, peakLevel: 0f);
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options { get; private set; }

    public void UpdateOptions(HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_gate)
        {
            RecreateEffects(options);
            _snapshot = CreateSnapshot(activeEffectCount: 0, peakLevel: 0f);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            foreach (var effectSlot in _effectSlots)
            {
                effectSlot.Reset();
            }

            _snapshot = CreateSnapshot(activeEffectCount: 0, peakLevel: 0f);
        }
    }

    public void NotifyRoadTextureGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        lock (_gate)
        {
            _roadTextureEffect.Effect.NotifyGearPulseAccepted(timestampUtc);
            _snapshot = CreateSnapshot(_snapshot.ActiveEffectCount, _snapshot.PeakLevel);
        }
    }

    public void Update(VehicleState vehicleState)
    {
        ArgumentNullException.ThrowIfNull(vehicleState);

        lock (_gate)
        {
            foreach (var effectSlot in _effectSlots)
            {
                effectSlot.Update(vehicleState);
            }

            _snapshot = CreateSnapshot(activeEffectCount: 0, peakLevel: 0f);
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
            var inputs = new List<AudioMixerInput>(capacity: _effectSlots.Count);
            var peakLevel = 0f;

            foreach (var effectSlot in _effectSlots)
            {
                var renderResult = effectSlot.Render();
                peakLevel = Math.Max(peakLevel, renderResult.PeakLevel);
                effectSlot.TryAddMixerInput(renderResult, inputs);
            }

            _snapshot = CreateSnapshot(inputs.Count, peakLevel);

            return new HapticEffectEngineRenderResult(inputs, _snapshot);
        }
    }

    private void RecreateEffects(HapticEffectEngineOptions options)
    {
        Options = options;

        foreach (var effectSlot in _effectSlots)
        {
            effectSlot.Recreate(options);
        }
    }

    private HapticEffectEngineSnapshot CreateSnapshot(
        int activeEffectCount,
        float peakLevel)
    {
        return new HapticEffectEngineSnapshot(
            _engineEffect.Effect.Snapshot,
            _gearShiftEffect.Effect.Snapshot,
            _kerbEffect.Effect.Snapshot,
            _impactEffect.Effect.Snapshot,
            _roadTextureEffect.Effect.Snapshot,
            _slipEffect.Effect.Snapshot,
            activeEffectCount,
            peakLevel);
    }

    private interface IEffectSlot
    {
        void Recreate(HapticEffectEngineOptions options);

        void Reset();

        void Update(VehicleState vehicleState);

        HapticEffectRenderResult Render();

        void TryAddMixerInput(HapticEffectRenderResult renderResult, List<AudioMixerInput> inputs);
    }

    private sealed class EffectSlot<TEffect, TOptions> : IEffectSlot
        where TEffect : class, IHapticEffectSource
    {
        private readonly Func<HapticEffectEngineOptions, TOptions> _optionsSelector;
        private readonly Func<TOptions, TEffect> _factory;
        private readonly AudioSampleBuffer _buffer;

        public EffectSlot(
            AudioSampleFormat format,
            HapticEffectEngineOptions initialOptions,
            Func<HapticEffectEngineOptions, TOptions> optionsSelector,
            Func<TOptions, TEffect> factory)
        {
            _optionsSelector = optionsSelector ?? throw new ArgumentNullException(nameof(optionsSelector));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _buffer = AudioSampleBuffer.Allocate(format ?? throw new ArgumentNullException(nameof(format)));
            Recreate(initialOptions ?? throw new ArgumentNullException(nameof(initialOptions)));
        }

        public TEffect Effect { get; private set; } = default!;

        public void Recreate(HapticEffectEngineOptions options)
        {
            Effect = _factory(_optionsSelector(options));
        }

        public void Reset()
        {
            Effect.Reset();
        }

        public void Update(VehicleState vehicleState)
        {
            Effect.Update(vehicleState);
        }

        public HapticEffectRenderResult Render()
        {
            return Effect.Render(_buffer);
        }

        public void TryAddMixerInput(HapticEffectRenderResult renderResult, List<AudioMixerInput> inputs)
        {
            if (renderResult.IsActive)
            {
                inputs.Add(new AudioMixerInput(_buffer, name: Effect.Name));
            }
        }
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
