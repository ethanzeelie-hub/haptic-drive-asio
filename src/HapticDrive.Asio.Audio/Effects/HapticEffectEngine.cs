using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;

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
    private readonly IEffectSlot[] _effectSlots;
    private readonly AudioMixerInput[] _mixerInputs;
    private IEffectSlot[] _renderSlots;
    private int _lastActiveEffectCount;
    private float _lastPeakLevel;

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
            static effectOptions => effectOptions.IsEnabled,
            static effectOptions => new EngineVibrationEffect(effectOptions));
        _gearShiftEffect = new EffectSlot<GearShiftEffect, GearShiftEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.GearShift,
            static effectOptions => effectOptions.IsEnabled,
            static effectOptions => new GearShiftEffect(effectOptions));
        _kerbEffect = new EffectSlot<KerbEffect, KerbEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Kerb,
            static effectOptions => effectOptions.IsEnabled,
            static effectOptions => new KerbEffect(effectOptions));
        _impactEffect = new EffectSlot<ImpactEffect, ImpactEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Impact,
            static effectOptions => effectOptions.IsEnabled,
            static effectOptions => new ImpactEffect(effectOptions));
        _roadTextureEffect = new EffectSlot<RoadTextureEffect, RoadTextureEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.RoadTexture,
            static effectOptions => effectOptions.IsEnabled,
            static effectOptions => new RoadTextureEffect(effectOptions));
        _slipEffect = new EffectSlot<SlipEffect, SlipEffectOptions>(
            format,
            Options,
            static engineOptions => engineOptions.Slip,
            static effectOptions => effectOptions.IsEnabled,
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
        _mixerInputs = new AudioMixerInput[_effectSlots.Length];
        _renderSlots = BuildRenderSlots();
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options { get; private set; }

    public void UpdateOptions(HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_gate)
        {
            ReconfigureEffects(options);
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
            _lastActiveEffectCount = 0;
            _lastPeakLevel = 0f;
        }
    }

    public void NotifyRoadTextureGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        lock (_gate)
        {
            _roadTextureEffect.Effect.NotifyGearPulseAccepted(timestampUtc);
        }
    }

    public void Update(HapticEffectInput input)
    {
        lock (_gate)
        {
            foreach (var effectSlot in _effectSlots)
            {
                effectSlot.Update(input);
            }
        }
    }

    public void Update(HapticDrive.Asio.Core.Vehicle.VehicleState vehicleState)
    {
        Update(LegacyHapticEffectInputFactory.FromVehicleState(vehicleState));
    }

    public HapticEffectEngineSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return CreateSnapshot(_lastActiveEffectCount, _lastPeakLevel, includeActivityItems: true);
        }
    }

    public HapticEffectEngineRenderResult RenderNextBuffer()
    {
        lock (_gate)
        {
            var activeEffectCount = RenderInto(_mixerInputs.AsSpan());
            return new HapticEffectEngineRenderResult(
                new ReadOnlyMemory<AudioMixerInput>(_mixerInputs, 0, activeEffectCount),
                CreateSnapshot(activeEffectCount, _lastPeakLevel, includeActivityItems: false));
        }
    }

    internal int RenderInto(Span<AudioMixerInput> mixerInputs)
    {
        lock (_gate)
        {
            return RenderInto(mixerInputs, alreadyLocked: true);
        }
    }

    private void ReconfigureEffects(HapticEffectEngineOptions options)
    {
        Options = options;

        foreach (var effectSlot in _effectSlots)
        {
            effectSlot.UpdateOptions(options);
        }

        _renderSlots = BuildRenderSlots();
    }

    private HapticEffectEngineSnapshot CreateSnapshot(
        int activeEffectCount,
        float peakLevel,
        bool includeActivityItems)
    {
        var snapshot = new HapticEffectEngineSnapshot(
            _engineEffect.Effect.Snapshot,
            _gearShiftEffect.Effect.Snapshot,
            _kerbEffect.Effect.Snapshot,
            _impactEffect.Effect.Snapshot,
            _roadTextureEffect.Effect.Snapshot,
            _slipEffect.Effect.Snapshot,
            activeEffectCount,
            peakLevel);
        if (includeActivityItems)
        {
            snapshot = snapshot with { ActivityItems = BuildActivityItems() };
        }

        return snapshot;
    }

    private int RenderInto(Span<AudioMixerInput> mixerInputs, bool alreadyLocked)
    {
        var activeEffectCount = 0;
        var peakLevel = 0f;

        foreach (var effectSlot in _renderSlots)
        {
            var renderResult = effectSlot.Render();
            peakLevel = Math.Max(peakLevel, renderResult.PeakLevel);
            if (effectSlot.TryCreateMixerInput(renderResult, out var mixerInput))
            {
                mixerInputs[activeEffectCount++] = mixerInput;
            }
        }

        _lastActiveEffectCount = activeEffectCount;
        _lastPeakLevel = peakLevel;
        return activeEffectCount;
    }

    private IEffectSlot[] BuildRenderSlots()
    {
        return _effectSlots.Where(static slot => slot.IsEnabled).ToArray();
    }

    private IReadOnlyList<HapticEffectActivityItem> BuildActivityItems()
    {
        return
        [
            new HapticEffectActivityItem("engine-rpm", _engineEffect.Effect.Snapshot.IsActive ? "active" : "idle"),
            new HapticEffectActivityItem("gear-shift", _gearShiftEffect.Effect.Snapshot.IsActive ? "pulse active" : "idle"),
            new HapticEffectActivityItem("kerb", _kerbEffect.Effect.Snapshot.IsActive ? "active" : "idle"),
            new HapticEffectActivityItem("impact", _impactEffect.Effect.Snapshot.IsActive ? "pulse active" : "idle"),
            new HapticEffectActivityItem("road-texture", _roadTextureEffect.Effect.Snapshot.IsActive ? "bst-1 active" : "idle"),
            new HapticEffectActivityItem(
                "slip-lock",
                _slipEffect.Effect.Snapshot.IsActive
                    ? $"{(_slipEffect.Effect.Snapshot.ActiveSource ?? "slip").Trim().ToLowerInvariant()} active"
                    : "idle")
        ];
    }

    private interface IEffectSlot
    {
        bool IsEnabled { get; }

        void UpdateOptions(HapticEffectEngineOptions options);

        void Reset();

        void Update(HapticEffectInput input);

        HapticEffectRenderResult Render();

        bool TryCreateMixerInput(HapticEffectRenderResult renderResult, out AudioMixerInput mixerInput);
    }

    private sealed class EffectSlot<TEffect, TOptions> : IEffectSlot
        where TEffect : class, IHapticEffectSource, IConfigurableHapticEffectSource<TOptions>
    {
        private readonly Func<HapticEffectEngineOptions, TOptions> _optionsSelector;
        private readonly Func<TOptions, bool> _enabledSelector;
        private readonly Func<TOptions, TEffect> _factory;
        private readonly AudioSampleBuffer _buffer;

        public EffectSlot(
            AudioSampleFormat format,
            HapticEffectEngineOptions initialOptions,
            Func<HapticEffectEngineOptions, TOptions> optionsSelector,
            Func<TOptions, bool> enabledSelector,
            Func<TOptions, TEffect> factory)
        {
            _optionsSelector = optionsSelector ?? throw new ArgumentNullException(nameof(optionsSelector));
            _enabledSelector = enabledSelector ?? throw new ArgumentNullException(nameof(enabledSelector));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _buffer = AudioSampleBuffer.Allocate(format ?? throw new ArgumentNullException(nameof(format)));
            Effect = _factory(_optionsSelector(initialOptions ?? throw new ArgumentNullException(nameof(initialOptions))));
            IsEnabled = _enabledSelector(_optionsSelector(initialOptions));
        }

        public TEffect Effect { get; private set; } = default!;

        public bool IsEnabled { get; private set; }

        public void UpdateOptions(HapticEffectEngineOptions options)
        {
            var effectOptions = _optionsSelector(options);
            IsEnabled = _enabledSelector(effectOptions);
            if (Effect is null)
            {
                Effect = _factory(effectOptions);
                return;
            }

            Effect.UpdateOptions(effectOptions);
        }

        public void Reset()
        {
            Effect.Reset();
        }

        public void Update(HapticEffectInput input)
        {
            Effect.Update(input);
        }

        public HapticEffectRenderResult Render()
        {
            return Effect.Render(_buffer);
        }

        public bool TryCreateMixerInput(HapticEffectRenderResult renderResult, out AudioMixerInput mixerInput)
        {
            if (renderResult.IsActive)
            {
                mixerInput = new AudioMixerInput(_buffer, name: Effect.Name);
                return true;
            }

            mixerInput = default;
            return false;
        }
    }
}

public readonly record struct HapticEffectEngineRenderResult(
    ReadOnlyMemory<AudioMixerInput> MixerInputs,
    HapticEffectEngineSnapshot Snapshot);

public readonly record struct HapticEffectActivityItem(
    string Label,
    string StatusText);

public readonly record struct HapticEffectEngineSnapshot(
    EngineVibrationEffectSnapshot Engine,
    GearShiftEffectSnapshot GearShift,
    KerbEffectSnapshot Kerb,
    ImpactEffectSnapshot Impact,
    RoadTextureEffectSnapshot RoadTexture,
    SlipEffectSnapshot Slip,
    int ActiveEffectCount,
    float PeakLevel)
{
    public IReadOnlyList<HapticEffectActivityItem> ActivityItems { get; init; } = Array.Empty<HapticEffectActivityItem>();
}
