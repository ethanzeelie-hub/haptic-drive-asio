using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Haptics;

namespace HapticDrive.Asio.Audio.Effects;

public sealed class HapticEffectEngine
{
    private readonly IHapticEffectRegistry _registry;
    private readonly AudioMixerInput[] _mixerInputs;
    private HapticEffectGraph _graph;
    private RenderFrameState _renderFrameState = RenderFrameState.Empty;
    private int _lastActiveEffectCount;
    private int _lastPeakLevelBits;
    private long _renderFailureCount;
    private int _lastRenderFailureCode;

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
        _mixerInputs = new AudioMixerInput[Math.Max(1, _registry.All.Count)];
        _graph = BuildGraph(settings);
    }

    public AudioSampleFormat Format { get; }

    public HapticEffectEngineOptions Options => Volatile.Read(ref _graph).Snapshot.Options;

    public IReadOnlyDictionary<string, EffectSettingsDocument> EffectSettings => Volatile.Read(ref _graph).Snapshot.EffectSettings;

    public HapticRenderFailureState RenderFailureState => new(
        Interlocked.Read(ref _renderFailureCount),
        (HapticRenderFailureCode)Volatile.Read(ref _lastRenderFailureCode));

    public void UpdateOptions(HapticEffectEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        UpdateEffectSettings(HapticEffectSettingsTranslator.CreateDocumentsFromOptions(options, _registry));
    }

    public void UpdateEffectSettings(IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Volatile.Write(ref _graph, BuildGraph(settings));
    }

    public void Reset()
    {
        Volatile.Write(ref _graph, BuildGraph(EffectSettings));
        Volatile.Write(ref _renderFrameState, RenderFrameState.Empty);
        Volatile.Write(ref _lastActiveEffectCount, 0);
        Interlocked.Exchange(ref _lastPeakLevelBits, BitConverter.SingleToInt32Bits(0f));
    }

    public void NotifyRoadTextureGearPulseAccepted(DateTimeOffset? timestampUtc = null)
    {
        Volatile.Read(ref _graph).NotifyRoadTextureGearPulseAccepted(timestampUtc);
    }

    public void Update(HapticEffectInput input)
    {
        Update(input.RenderFrame);
    }

    public void Update(HapticRenderFrame frame)
    {
        Volatile.Write(ref _renderFrameState, new RenderFrameState(frame));
    }

    public HapticEffectEngineSnapshot GetSnapshot()
    {
        var graph = Volatile.Read(ref _graph);
        return graph.CreateSnapshot(
            Volatile.Read(ref _lastActiveEffectCount),
            BitConverter.Int32BitsToSingle(Interlocked.CompareExchange(ref _lastPeakLevelBits, 0, 0)),
            includeActivityItems: true);
    }

    public HapticEffectEngineRenderResult RenderNextBuffer()
    {
        var activeEffectCount = RenderInto(_mixerInputs.AsSpan());
        var peakLevel = BitConverter.Int32BitsToSingle(Interlocked.CompareExchange(ref _lastPeakLevelBits, 0, 0));
        return new HapticEffectEngineRenderResult(
            new ReadOnlyMemory<AudioMixerInput>(_mixerInputs, 0, activeEffectCount),
            Volatile.Read(ref _graph).CreateSnapshot(activeEffectCount, peakLevel, includeActivityItems: false));
    }

    internal int RenderInto(Span<AudioMixerInput> mixerInputs)
    {
        var frameState = Volatile.Read(ref _renderFrameState);
        if (!frameState.HasFrame)
        {
            Volatile.Write(ref _lastActiveEffectCount, 0);
            Interlocked.Exchange(ref _lastPeakLevelBits, BitConverter.SingleToInt32Bits(0f));
            return 0;
        }

        var graph = Volatile.Read(ref _graph);
        try
        {
            var activeEffectCount = graph.Render(frameState.Frame, mixerInputs, out var peakLevel);
            Volatile.Write(ref _lastActiveEffectCount, activeEffectCount);
            Interlocked.Exchange(ref _lastPeakLevelBits, BitConverter.SingleToInt32Bits(peakLevel));
            return activeEffectCount;
        }
        catch
        {
            graph.ClearRenderBuffers();
            Volatile.Write(ref _lastActiveEffectCount, 0);
            Interlocked.Exchange(ref _lastPeakLevelBits, BitConverter.SingleToInt32Bits(0f));
            Interlocked.Increment(ref _renderFailureCount);
            Volatile.Write(ref _lastRenderFailureCode, (int)HapticRenderFailureCode.RuntimeException);
            return 0;
        }
    }

    private HapticEffectGraph BuildGraph(IReadOnlyDictionary<string, EffectSettingsDocument> settings)
    {
        return new HapticEffectGraph(Format, _registry, settings);
    }

    private sealed class RenderFrameState
    {
        public static RenderFrameState Empty { get; } = new(default, hasFrame: false);

        public RenderFrameState(HapticRenderFrame frame, bool hasFrame = true)
        {
            Frame = frame;
            HasFrame = hasFrame;
        }

        public HapticRenderFrame Frame { get; }

        public bool HasFrame { get; }
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
