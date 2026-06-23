using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

internal sealed class HapticEffectGraph
{
    private readonly RuntimeEffectSlot<EngineEffectRuntime> _engineEffect;
    private readonly RuntimeEffectSlot<GearShiftEffectRuntime> _gearShiftEffect;
    private readonly RuntimeEffectSlot<KerbEffectRuntime> _kerbEffect;
    private readonly RuntimeEffectSlot<ImpactEffectRuntime> _impactEffect;
    private readonly RuntimeEffectSlot<RoadTextureEffectRuntime> _roadTextureEffect;
    private readonly RuntimeEffectSlot<SlipLockEffectRuntime> _slipEffect;
    private readonly IRuntimeEffectSlot[] _allEffects;
    private readonly IRuntimeEffectSlot[] _renderEffects;

    public HapticEffectGraph(
        AudioSampleFormat format,
        IHapticEffectRegistry registry,
        IReadOnlyDictionary<string, EffectSettingsDocument> effectSettings)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(effectSettings);

        var normalized = HapticEffectSettingsTranslator.NormalizeDocuments(effectSettings, registry);
        var storedSettings = new Dictionary<string, EffectSettingsDocument>(normalized, StringComparer.OrdinalIgnoreCase);
        Snapshot = new HapticEffectGraphSnapshot(
            storedSettings,
            HapticEffectSettingsTranslator.ToEngineOptions(storedSettings));

        _engineEffect = CreateSlot<EngineEffectRuntime>(registry, format, "engine-rpm", static runtime => runtime.Snapshot.IsActive, storedSettings);
        _gearShiftEffect = CreateSlot<GearShiftEffectRuntime>(registry, format, "gear-shift", static runtime => runtime.Snapshot.IsActive, storedSettings);
        _kerbEffect = CreateSlot<KerbEffectRuntime>(registry, format, "kerb", static runtime => runtime.Snapshot.IsActive, storedSettings);
        _impactEffect = CreateSlot<ImpactEffectRuntime>(registry, format, "impact", static runtime => runtime.Snapshot.IsActive, storedSettings);
        _roadTextureEffect = CreateSlot<RoadTextureEffectRuntime>(registry, format, "road-texture", static runtime => runtime.Snapshot.IsActive, storedSettings);
        _slipEffect = CreateSlot<SlipLockEffectRuntime>(registry, format, "slip-lock", static runtime => runtime.Snapshot.IsActive, storedSettings);

        _allEffects =
        [
            _engineEffect,
            _gearShiftEffect,
            _kerbEffect,
            _impactEffect,
            _roadTextureEffect,
            _slipEffect
        ];

        var renderCount = 0;
        for (var i = 0; i < _allEffects.Length; i++)
        {
            if (_allEffects[i].IsEnabled)
            {
                renderCount++;
            }
        }

        _renderEffects = new IRuntimeEffectSlot[renderCount];
        var renderIndex = 0;
        for (var i = 0; i < _allEffects.Length; i++)
        {
            if (_allEffects[i].IsEnabled)
            {
                _renderEffects[renderIndex++] = _allEffects[i];
            }
        }
    }

    public HapticEffectGraphSnapshot Snapshot { get; }

    public int Render(
        in HapticRenderFrame frame,
        Span<AudioMixerInput> mixerInputs,
        out float peakLevel)
    {
        var activeEffectCount = 0;
        peakLevel = 0f;

        for (var i = 0; i < _renderEffects.Length; i++)
        {
            var effectSlot = _renderEffects[i];
            var renderResult = effectSlot.Render(frame);
            peakLevel = Math.Max(peakLevel, renderResult.PeakLevel);
            if (effectSlot.TryCreateMixerInput(renderResult, out var mixerInput))
            {
                mixerInputs[activeEffectCount++] = mixerInput;
            }
        }

        return activeEffectCount;
    }

    public void ClearRenderBuffers()
    {
        for (var i = 0; i < _renderEffects.Length; i++)
        {
            _renderEffects[i].Clear();
        }
    }

    public void NotifyRoadTextureGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        _roadTextureEffect.Runtime.NotifyGearPulseAccepted(timestampUtc);
    }

    public HapticEffectEngineSnapshot CreateSnapshot(
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
            snapshot = snapshot with
            {
                ActivityItems =
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
                ]
            };
        }

        return snapshot;
    }

    private static RuntimeEffectSlot<TRuntime> CreateSlot<TRuntime>(
        IHapticEffectRegistry registry,
        AudioSampleFormat format,
        string key,
        Func<TRuntime, bool> isActiveSelector,
        IReadOnlyDictionary<string, EffectSettingsDocument> effectSettings)
        where TRuntime : BufferedHapticEffectRuntime
    {
        var descriptor = registry.GetRequired(key);
        var settings = effectSettings.TryGetValue(key, out var configured)
            ? configured
            : descriptor.CreateDefaultSettings();
        var runtime = descriptor.CreateRuntime(settings) as TRuntime
            ?? throw new InvalidOperationException($"Descriptor '{key}' did not create runtime '{typeof(TRuntime).Name}'.");
        return new RuntimeEffectSlot<TRuntime>(descriptor.Key, runtime, format, settings.Enabled, isActiveSelector);
    }

    private interface IRuntimeEffectSlot
    {
        bool IsEnabled { get; }

        void Clear();

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
            bool isEnabled,
            Func<TRuntime, bool> isActiveSelector)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _isActiveSelector = isActiveSelector ?? throw new ArgumentNullException(nameof(isActiveSelector));
            _buffer = AudioSampleBuffer.Allocate(format ?? throw new ArgumentNullException(nameof(format)));
            _left = new float[format.FrameCount];
            _right = format.ChannelCount > 1 ? new float[format.FrameCount] : Array.Empty<float>();
            IsEnabled = isEnabled;
            if (!IsEnabled)
            {
                Clear();
            }
        }

        public string Key { get; }

        public TRuntime Runtime { get; }

        public bool IsEnabled { get; }

        public void Clear()
        {
            _buffer.Clear();
            Array.Clear(_left);
            if (_right.Length > 0)
            {
                Array.Clear(_right);
            }
        }

        public HapticEffectRenderResult Render(in HapticRenderFrame frame)
        {
            Clear();
            if (!IsEnabled)
            {
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
            return new HapticEffectRenderResult(Runtime.DisplayName, true, _isActiveSelector(Runtime), peak);
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
