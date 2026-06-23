using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class HapticEffectEngine
{
    private readonly object _gate = new();
    private readonly IHapticEffectRegistry _registry;
    private readonly RuntimeEffectSlot<EngineEffectRuntime> _engineEffect;
    private readonly RuntimeEffectSlot<GearShiftEffectRuntime> _gearShiftEffect;
    private readonly RuntimeEffectSlot<KerbEffectRuntime> _kerbEffect;
    private readonly RuntimeEffectSlot<ImpactEffectRuntime> _impactEffect;
    private readonly RuntimeEffectSlot<RoadTextureEffectRuntime> _roadTextureEffect;
    private readonly RuntimeEffectSlot<SlipLockEffectRuntime> _slipEffect;
    private readonly IRuntimeEffectSlot[] _effectSlots;
    private readonly AudioMixerInput[] _mixerInputs;
    private IRuntimeEffectSlot[] _renderSlots;
    private HapticRenderFrame _latestFrame;
    private bool _hasFrame;
    private int _lastActiveEffectCount;
    private float _lastPeakLevel;

    public HapticEffectEngine(AudioSampleFormat format)
        : this(format, BuiltInHapticEffectRegistry.Instance, HapticEffectSettingsTranslator.CreateDefaultDocuments(BuiltInHapticEffectRegistry.Instance))
    {
    }

    public HapticEffectEngine(AudioSampleFormat format, HapticEffectEngineOptions options)
        : this(
            format,
            BuiltInHapticEffectRegistry.Instance,
            HapticEffectSettingsTranslator.CreateDocumentsFromOptions(
                options ?? throw new ArgumentNullException(nameof(options)),
                BuiltInHapticEffectRegistry.Instance))
    {
    }

    internal HapticEffectEngine(
        AudioSampleFormat format,
        IHapticEffectRegistry registry,
        IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        Format = format ?? throw new ArgumentNullException(nameof(format));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _engineEffect = CreateSlot<EngineEffectRuntime>("engine-rpm", static runtime => runtime.Snapshot.IsActive);
        _gearShiftEffect = CreateSlot<GearShiftEffectRuntime>("gear-shift", static runtime => runtime.Snapshot.IsActive);
        _kerbEffect = CreateSlot<KerbEffectRuntime>("kerb", static runtime => runtime.Snapshot.IsActive);
        _impactEffect = CreateSlot<ImpactEffectRuntime>("impact", static runtime => runtime.Snapshot.IsActive);
        _roadTextureEffect = CreateSlot<RoadTextureEffectRuntime>("road-texture", static runtime => runtime.Snapshot.IsActive);
        _slipEffect = CreateSlot<SlipLockEffectRuntime>("slip-lock", static runtime => runtime.Snapshot.IsActive);
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
        _renderSlots = Array.Empty<IRuntimeEffectSlot>();
        UpdateEffectSettings(settings);
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options { get; private set; } = HapticEffectEngineOptions.Default;

    public IReadOnlyDictionary<string, EffectSettingsDocument> EffectSettings { get; private set; } =
        new Dictionary<string, EffectSettingsDocument>(StringComparer.OrdinalIgnoreCase);

    public void UpdateOptions(HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        UpdateEffectSettings(HapticEffectSettingsTranslator.CreateDocumentsFromOptions(options, _registry));
    }

    public void UpdateEffectSettings(IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            var normalized = HapticEffectSettingsTranslator.NormalizeDocuments(settings, _registry);
            EffectSettings = normalized;
            Options = HapticEffectSettingsTranslator.ToEngineOptions(normalized);

            foreach (var effectSlot in _effectSlots)
            {
                var document = normalized.TryGetValue(effectSlot.Key, out var configured)
                    ? configured
                    : _registry.GetRequired(effectSlot.Key).CreateDefaultSettings();
                effectSlot.ApplySettings(document);
            }

            _renderSlots = BuildRenderSlots();
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

            _hasFrame = false;
            _lastActiveEffectCount = 0;
            _lastPeakLevel = 0f;
        }
    }

    public void NotifyRoadTextureGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        lock (_gate)
        {
            _roadTextureEffect.Runtime.NotifyGearPulseAccepted(timestampUtc);
        }
    }

    public void Update(HapticEffectInput input)
    {
        Update(input.RenderFrame);
    }

    public void Update(HapticRenderFrame frame)
    {
        lock (_gate)
        {
            _latestFrame = frame;
            _hasFrame = true;
        }
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

    private int RenderInto(Span<AudioMixerInput> mixerInputs, bool alreadyLocked)
    {
        if (!_hasFrame)
        {
            _lastActiveEffectCount = 0;
            _lastPeakLevel = 0f;
            return 0;
        }

        var activeEffectCount = 0;
        var peakLevel = 0f;

        foreach (var effectSlot in _renderSlots)
        {
            var renderResult = effectSlot.Render(_latestFrame);
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

    private HapticEffectEngineSnapshot CreateSnapshot(
        int activeEffectCount,
        float peakLevel,
        bool includeActivityItems)
    {
        var snapshot = new HapticEffectEngineSnapshot(
            _engineEffect.Runtime.Snapshot with { IsEnabled = _engineEffect.IsEnabled },
            _gearShiftEffect.Runtime.Snapshot with { IsEnabled = _gearShiftEffect.IsEnabled },
            _kerbEffect.Runtime.Snapshot with { IsEnabled = _kerbEffect.IsEnabled },
            _impactEffect.Runtime.Snapshot with { IsEnabled = _impactEffect.IsEnabled },
            _roadTextureEffect.Runtime.Snapshot with { IsEnabled = _roadTextureEffect.IsEnabled },
            _slipEffect.Runtime.Snapshot with { IsEnabled = _slipEffect.IsEnabled },
            activeEffectCount,
            peakLevel);
        if (includeActivityItems)
        {
            snapshot = snapshot with { ActivityItems = BuildActivityItems() };
        }

        return snapshot;
    }

    private IRuntimeEffectSlot[] BuildRenderSlots()
    {
        return _effectSlots.Where(static slot => slot.IsEnabled).ToArray();
    }

    private IReadOnlyList<HapticEffectActivityItem> BuildActivityItems()
    {
        return
        [
            new HapticEffectActivityItem("engine-rpm", _engineEffect.Runtime.Snapshot.IsActive ? "active" : "idle"),
            new HapticEffectActivityItem("gear-shift", _gearShiftEffect.Runtime.Snapshot.IsActive ? "pulse active" : "idle"),
            new HapticEffectActivityItem("kerb", _kerbEffect.Runtime.Snapshot.IsActive ? "active" : "idle"),
            new HapticEffectActivityItem("impact", _impactEffect.Runtime.Snapshot.IsActive ? "pulse active" : "idle"),
            new HapticEffectActivityItem("road-texture", _roadTextureEffect.Runtime.Snapshot.IsActive ? "bst-1 active" : "idle"),
            new HapticEffectActivityItem(
                "slip-lock",
                _slipEffect.Runtime.Snapshot.IsActive
                    ? $"{(_slipEffect.Runtime.Snapshot.ActiveSource ?? "slip").Trim().ToLowerInvariant()} active"
                    : "idle")
        ];
    }

    private RuntimeEffectSlot<TRuntime> CreateSlot<TRuntime>(
        string key,
        Func<TRuntime, bool> isActiveSelector)
        where TRuntime : BufferedHapticEffectRuntime
    {
        var descriptor = _registry.GetRequired(key);
        var runtime = descriptor.CreateRuntime(descriptor.CreateDefaultSettings()) as TRuntime
            ?? throw new InvalidOperationException($"Descriptor '{key}' did not create runtime '{typeof(TRuntime).Name}'.");
        return new RuntimeEffectSlot<TRuntime>(descriptor.Key, runtime, Format, isActiveSelector);
    }

    private interface IRuntimeEffectSlot
    {
        string Key { get; }

        bool IsEnabled { get; }

        void ApplySettings(EffectSettingsDocument settings);

        void Reset();

        HapticEffectRenderResult Render(in HapticRenderFrame frame);

        bool TryCreateMixerInput(HapticEffectRenderResult renderResult, out AudioMixerInput mixerInput);
    }

    private sealed class RuntimeEffectSlot<TRuntime> : IRuntimeEffectSlot
        where TRuntime : BufferedHapticEffectRuntime
    {
        private readonly AudioSampleBuffer _buffer;
        private readonly float[] _left;
        private readonly float[] _right;
        private readonly Func<TRuntime, bool> _isActiveSelector;

        public RuntimeEffectSlot(
            string key,
            TRuntime runtime,
            AudioSampleFormat format,
            Func<TRuntime, bool> isActiveSelector)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _isActiveSelector = isActiveSelector ?? throw new ArgumentNullException(nameof(isActiveSelector));
            _buffer = AudioSampleBuffer.Allocate(format ?? throw new ArgumentNullException(nameof(format)));
            _left = new float[format.FrameCount];
            _right = format.ChannelCount > 1 ? new float[format.FrameCount] : Array.Empty<float>();
        }

        public string Key { get; }

        public TRuntime Runtime { get; }

        public bool IsEnabled { get; private set; }

        public void ApplySettings(EffectSettingsDocument settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Runtime.ApplySettings(settings.Parameters);
            IsEnabled = settings.Enabled;
            if (!IsEnabled)
            {
                Runtime.Reset();
                _buffer.Clear();
                Array.Clear(_left);
                if (_right.Length > 0)
                {
                    Array.Clear(_right);
                }
            }
        }

        public void Reset()
        {
            Runtime.Reset();
            _buffer.Clear();
            Array.Clear(_left);
            if (_right.Length > 0)
            {
                Array.Clear(_right);
            }
        }

        public HapticEffectRenderResult Render(in HapticRenderFrame frame)
        {
            _buffer.Clear();
            if (!IsEnabled)
            {
                Array.Clear(_left);
                if (_right.Length > 0)
                {
                    Array.Clear(_right);
                }

                return new HapticEffectRenderResult(Runtime.DisplayName, IsEnabled: false, IsActive: false, PeakLevel: 0f);
            }

            Runtime.Render(
                frame,
                _left.AsSpan(),
                _right.Length == 0 ? Span<float>.Empty : _right.AsSpan(),
                _buffer.SampleRate,
                _buffer.FrameCount);
            InterleaveChannels();
            var peak = HapticEffectMath.CalculatePeak(_buffer);
            return new HapticEffectRenderResult(Runtime.DisplayName, IsEnabled, _isActiveSelector(Runtime), peak);
        }

        public bool TryCreateMixerInput(HapticEffectRenderResult renderResult, out AudioMixerInput mixerInput)
        {
            if (renderResult.IsActive)
            {
                mixerInput = new AudioMixerInput(_buffer, name: Runtime.DisplayName);
                return true;
            }

            mixerInput = default;
            return false;
        }

        private void InterleaveChannels()
        {
            if (_buffer.ChannelCount == 1)
            {
                _left.AsSpan().CopyTo(_buffer.Samples);
                return;
            }

            for (var frame = 0; frame < _buffer.FrameCount; frame++)
            {
                _buffer[frame, 0] = _left[frame];
                _buffer[frame, 1] = _right[frame];
            }
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
