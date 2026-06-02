using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.Audio.TestBench;

public sealed class AudioTestBench : IAsyncDisposable
{
    private readonly AudioOutputConfiguration _configuration;
    private readonly AudioRenderPipeline _pipeline;
    private readonly AudioSampleBuffer _signalBuffer;
    private readonly AudioSampleBuffer _outputBuffer;
    private readonly IAudioOutputDevice _outputDevice;
    private readonly bool _ownsOutputDevice;

    private IAudioTestSignalGenerator _signalGenerator;
    private bool _isActive;
    private long _renderedBufferCount;
    private long _renderedFrameCount;
    private string _statusMessage = "Test bench idle.";
    private AudioRenderPipelineSnapshot? _lastPipelineSnapshot;

    public AudioTestBench()
        : this(AudioOutputConfiguration.Default, new NullAudioOutputDevice(), ownsOutputDevice: true)
    {
    }

    public AudioTestBench(AudioOutputConfiguration configuration, IAudioOutputDevice outputDevice)
        : this(configuration, outputDevice, ownsOutputDevice: false)
    {
    }

    private AudioTestBench(
        AudioOutputConfiguration configuration,
        IAudioOutputDevice outputDevice,
        bool ownsOutputDevice)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _outputDevice = outputDevice ?? throw new ArgumentNullException(nameof(outputDevice));
        _ownsOutputDevice = ownsOutputDevice;

        var format = AudioSampleFormat.FromConfiguration(configuration);
        _pipeline = new AudioRenderPipeline(format);
        _signalBuffer = AudioSampleBuffer.Allocate(format);
        _outputBuffer = AudioSampleBuffer.Allocate(format);
        _signalGenerator = AudioTestSignalGeneratorFactory.Create(
            AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.SineTone));
    }

    public bool IsMuted { get; set; }

    public bool EmergencyMute { get; set; }

    public float MasterGain { get; set; } = AudioMixerSettings.Default.MasterGain;

    public AudioSafetyProcessorOptions SafetyOptions { get; set; } = AudioSafetyProcessorOptions.Default;

    public AudioTestBenchSnapshot GetSnapshot()
    {
        var outputStatus = _outputDevice.GetStatus();
        var effectiveEmergencyMute = EmergencyMute
            || SafetyOptions.EmergencyMute
            || (_lastPipelineSnapshot?.EmergencyMute ?? false);

        return new AudioTestBenchSnapshot(
            _isActive,
            _signalGenerator.Definition.Kind,
            _signalGenerator.Definition.DisplayName,
            _configuration.SampleRate,
            _configuration.ChannelCount,
            _configuration.BufferSize,
            IsMuted,
            effectiveEmergencyMute,
            _renderedBufferCount,
            _renderedFrameCount,
            _lastPipelineSnapshot?.MixerPeakLevel ?? 0f,
            _lastPipelineSnapshot?.OutputPeakLevel ?? 0f,
            _lastPipelineSnapshot?.SanitizedSampleCount ?? 0,
            _lastPipelineSnapshot?.LimitedSampleCount ?? 0,
            _lastPipelineSnapshot?.ClippedSampleCount ?? 0,
            outputStatus.Kind,
            outputStatus.DisplayName,
            outputStatus.State,
            outputStatus.RequiresPhysicalHardware,
            outputStatus.IsManualDebugOnly,
            _statusMessage);
    }

    public void SelectSignal(AudioTestSignalDefinition definition)
    {
        _signalGenerator = AudioTestSignalGeneratorFactory.Create(definition);
        _signalGenerator.Reset();
        _lastPipelineSnapshot = null;
        _renderedBufferCount = 0;
        _renderedFrameCount = 0;
        _statusMessage = $"Selected {_signalGenerator.Definition.DisplayName}.";
    }

    public async ValueTask<AudioTestBenchOperationResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_isActive)
        {
            return Success("Test bench already active.");
        }

        var status = _outputDevice.GetStatus();
        if (status.State == AudioOutputDeviceState.Created)
        {
            var openResult = await _outputDevice.OpenAsync(_configuration, cancellationToken).ConfigureAwait(false);
            if (!openResult.Succeeded)
            {
                return Failure(openResult.Message);
            }

            status = _outputDevice.GetStatus();
        }

        if (status.State is AudioOutputDeviceState.Open or AudioOutputDeviceState.Stopped)
        {
            var startResult = await _outputDevice.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!startResult.Succeeded)
            {
                return Failure(startResult.Message);
            }
        }
        else if (status.State != AudioOutputDeviceState.Started)
        {
            return Failure("Output device must be open before the test bench can start.");
        }

        _signalGenerator.Reset();
        _lastPipelineSnapshot = null;
        _renderedBufferCount = 0;
        _renderedFrameCount = 0;
        _isActive = true;
        return Success($"Test bench started with {_signalGenerator.Definition.DisplayName}.");
    }

    public async ValueTask<AudioTestBenchOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isActive)
        {
            return Success("Test bench already stopped.");
        }

        var stopResult = await _outputDevice.StopAsync(cancellationToken).ConfigureAwait(false);
        if (!stopResult.Succeeded)
        {
            return Failure(stopResult.Message);
        }

        _isActive = false;
        return Success("Test bench stopped.");
    }

    public async ValueTask<AudioTestBenchOperationResult> RenderNextBufferAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isActive)
        {
            return Failure("Test bench must be started before rendering a test buffer.");
        }

        _signalGenerator.Generate(_signalBuffer);
        _pipeline.MixerSettings = new AudioMixerSettings(MasterGain, IsMuted, EmergencyMute);
        _pipeline.SafetyOptions = SafetyOptions with
        {
            EmergencyMute = EmergencyMute || SafetyOptions.EmergencyMute
        };

        _lastPipelineSnapshot = _pipeline.Process(
            [new AudioMixerInput(_signalBuffer, name: _signalGenerator.Definition.DisplayName)],
            _outputBuffer);

        var submitResult = await _outputDevice.SubmitBufferAsync(_outputBuffer, cancellationToken).ConfigureAwait(false);
        if (!submitResult.Succeeded)
        {
            return Failure(submitResult.Message);
        }

        _renderedBufferCount++;
        _renderedFrameCount += _configuration.BufferSize;
        return Success($"Rendered {_configuration.BufferSize:N0} frame(s) through the test bench.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_isActive)
        {
            await StopAsync().ConfigureAwait(false);
        }

        if (_ownsOutputDevice)
        {
            await _outputDevice.DisposeAsync().ConfigureAwait(false);
        }
    }

    private AudioTestBenchOperationResult Success(string message)
    {
        _statusMessage = message;
        return new AudioTestBenchOperationResult(true, message, GetSnapshot());
    }

    private AudioTestBenchOperationResult Failure(string message)
    {
        _statusMessage = message;
        return new AudioTestBenchOperationResult(false, message, GetSnapshot());
    }
}
