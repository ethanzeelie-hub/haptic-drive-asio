using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Telemetry.F1_25;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HapticDrive.Asio.App;

public partial class MainWindow : Window
{
    private readonly object _headerParserGate = new();
    private readonly IAudioOutputDevice _selectedOutputDevice = new NullAudioOutputDevice();
    private readonly AudioRenderPipeline _audioPipeline = new(AudioSampleFormat.FromConfiguration(AudioOutputConfiguration.Default));
    private readonly AudioSampleBuffer _audioOutputBuffer = AudioSampleBuffer.Allocate(AudioOutputConfiguration.Default);
    private readonly HapticEffectEngine _hapticEffectEngine = new(AudioSampleFormat.FromConfiguration(AudioOutputConfiguration.Default));
    private readonly AudioTestBench _testBench = new();
    private readonly IUdpTelemetryReceiver _telemetryReceiver = new UdpTelemetryReceiver();
    private readonly IUdpTelemetryForwarder _telemetryForwarder = new UdpTelemetryForwarder();
    private readonly TelemetryRecordingService _recordingService = new();
    private readonly F125VehicleStateAdapter _vehicleStateAdapter = new();
    private readonly DispatcherTimer _telemetryStatusTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };
    private readonly IReadOnlyList<AudioTestSignalDefinition> _testBenchSignals =
    [
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Silence),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.SineTone),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.FrequencySweep),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Pulse),
        AudioTestSignalDefinition.DefaultFor(AudioTestSignalKind.Constant)
    ];

    private readonly IReadOnlyList<ShellPageDefinition> _pages =
    [
        new(
            "Dashboard",
            "Dashboard",
            "A safe overview for raw UDP telemetry, output state, and hardware-absent operation.",
            "Stage 13 effect status",
            [
                "UDP listener starts on port 20778 by default.",
                "Packets are counted, preserved as raw datagrams, and offered to the forwarder.",
                "Recording captures raw incoming UDP payload bytes to a versioned replay file.",
                "Forwarding is byte-preserving and parser-independent.",
                "The F1 25 parser validates packet format, year, ID, version, exact length, and Stage 07 packet bodies.",
                "Stage 08 maps parsed packets into shared last-known VehicleState samples.",
                "Stage 10 adds a deterministic mixer, conservative safety chain, and null-output sample consumption.",
                "Stage 11 adds deterministic synthetic test signals for hardware-absent audio path validation.",
                "Stage 12 adds conservative engine vibration and gear shift effect generators from VehicleState.",
                "Stage 13 adds conservative kerb, impact, road texture, and slip / brake-lock effect generators from VehicleState.",
                "NullAudioOutputDevice is the default safe output.",
                "The app remains safe to open without ASIO hardware or shaker hardware."
            ]),
        new(
            "Effects",
            "Effects",
            "Hardware-safe generated effect diagnostics.",
            "Stage 13 effects available",
            [
                "Gear shift is synthesized from valid forward gear changes in VehicleState telemetry.",
                "Engine vibration is synthesized from RPM, throttle, idle RPM, max RPM, gear, speed, and pause/status gates where available.",
                "Kerb vibration is synthesized from rumble strip / ridged surface IDs, speed, and optional suspension/contact data.",
                "Impact pulses are synthesized from player collision events and abrupt vertical-G, wheel-force, or suspension-acceleration spikes.",
                "Road texture is synthesized from surface IDs, speed, and optional suspension / vertical-G motion.",
                "Slip and minimal brake-lock vibration are synthesized from wheel slip ratio/angle, wheel speed, throttle, brake, speed, TC, and ABS fields.",
                "Defaults are conservative and inspired by SimHub-style frequency, gain, pulse-duration, speed, and threshold controls.",
                "The current shell renders deterministic validation buffers through the mixer, safety chain, and Null output; it is not a real audio callback.",
                "A full tuning editor, effect profiles, live graphs, and physical calibration are deferred."
            ]),
        new(
            "Mixer / Routing",
            "Mixer / Routing",
            "Placeholder for mono output routing, mixer level controls, priority, ducking, and safety-chain visibility.",
            "Mixer and safety chain available",
            [
                "The Stage 10 mixer can combine source buffers with source gain, master gain, mute, and emergency mute.",
                "The safety chain sanitises invalid samples, applies conservative output gain, limits peaks, and hard-clips overflow.",
                "Stage 12 and Stage 13 effect buffers feed this same mixer and safety path.",
                "Null output can consume final sample buffers deterministically without hardware.",
                "Mono BST-1 routing is the first hardware target.",
                "Future output adapters can be added without coupling effects to a specific device."
            ]),
        new(
            "Devices",
            "Devices",
            "Placeholder for Null, WASAPI debug, and ASIO output device selection.",
            "Output abstractions added",
            [
                "NullAudioOutputDevice is available for automated tests and safe app startup.",
                "WasapiDebugOutputDevice exists as a manual debug placeholder only.",
                "AsioAudioOutputDevice exists behind the same interface and fails gracefully when no driver is available.",
                "WASAPI remains a manual debug fallback only.",
                "ASIO absence must fail gracefully and never block automated tests."
            ]),
        new(
            "Telemetry / UDP Router",
            "Telemetry / UDP Router",
            "Raw F1 25 UDP input, byte-preserving forwarding, and packet parser status.",
            "UDP listener, forwarder, recorder, parser, and VehicleState adapter active",
            [
                "Default listen port is 20778.",
                "Raw packets are counted and timestamped.",
                "Active recordings receive raw packet bytes before parser validation.",
                "Forwarding sends exact packet bytes to enabled destinations.",
                "Stage 07 packet bodies are parsed from the official F1 25 v3 spec.",
                "Stage 08 selects the player car from packet headers and keeps last-known VehicleState slices.",
                "Stage 12 and Stage 13 effects consume VehicleState only and do not read parser packet bodies directly.",
                "No forwarding destinations are configured in the shell yet.",
                "Replay is implemented in the Recording project for deterministic tests."
            ]),
        new(
            "Recordings",
            "Recordings",
            "Placeholder for telemetry capture and deterministic replay without running F1 25.",
            "Recording and replay service available",
            [
                "Use Start Recording to capture raw incoming UDP packets to a versioned file.",
                "Replay service emits recorded packets through the same parser and VehicleState path used by live packets.",
                "Deterministic VehicleState sequences can drive Stage 12 and Stage 13 effects in tests.",
                "Replay tests do not require F1 25, UDP sockets, audio output, ASIO hardware, or shaker hardware.",
                "A polished recording library UI is deferred."
            ]),
        new(
            "Test Bench",
            "Test Bench",
            "Safe synthetic signals for validating the internal audio path.",
            "Test bench available",
            [
                "Silence, sine tone, frequency sweep, pulse transient, and constant-value signals are available.",
                "Signals render through the existing mixer and safety chain into NullAudioOutputDevice.",
                "Start Test Bench renders deterministic validation buffers only; it is not a real audio callback.",
                "Emergency mute is applied through the test bench mixer and safety settings.",
                "Null output remains the automated-test target.",
                "Physical shaker tuning waits for later manual hardware stages."
            ]),
        new(
            "Profiles",
            "Profiles",
            "Placeholder for versioned JSON tuning profiles and presets.",
            "Profiles planned",
            [
                "Profiles are per game and human-readable JSON.",
                "Built-in presets will include Immersion, Performance, Strong, Night / Quiet, and Testing / Debug.",
                "Device settings should remain separate from effect profiles."
            ]),
        new(
            "Settings",
            "Settings",
            "Placeholder for app preferences, theme selection, close behavior, and safe defaults.",
            "Settings planned",
            [
                "Dark theme is active by default; the light theme button currently demonstrates theme scaffolding.",
                "Close/minimize-to-tray support is represented by the disabled footer setting.",
                "No setting should require admin rights or physical haptic hardware."
            ]),
        new(
            "Diagnostics",
            "Diagnostics",
            "Placeholder for packet rate, parser errors, output status, peak levels, limiter activity, and reports.",
            "Diagnostics partially available",
            [
                "Output status is available for the selected safe output device.",
                "Mixer and safety diagnostics are available in the audio pipeline tests and minimal shell status.",
                "Stage 13 reports engine, gear, kerb, impact, road texture, and slip effect state with conservative read-only diagnostics.",
                "Test bench diagnostics report selected synthetic signal, output peak, limiter count, and output mode.",
                "UDP packet count, packet rate, and no-packet warning are available.",
                "Forwarded datagram count, forwarded byte count, and forwarding errors are available.",
                "F1 25 packet parser success, ignored, and failure counts are available.",
                "VehicleState update count, player index, speed, and gear are available when telemetry packets arrive.",
                "Diagnostics become more meaningful as telemetry, parser, audio, and replay stages are implemented.",
                "Logging must not block telemetry, UI, disk, or audio paths.",
                "A copy diagnostics report action is planned for Stage 14."
            ])
    ];

    private bool _hapticsStarted;
    private bool _emergencyMuted;
    private bool _lightTheme;
    private string? _telemetryStartError;
    private string? _forwardingError;
    private string? _recordingError;
    private long _packetParseSuccessCount;
    private long _packetParseIgnoredCount;
    private long _packetParseFailureCount;
    private long _vehicleStateUpdateCount;
    private string _lastPacketParserMessage = "Waiting for F1 25 packets.";
    private string _lastVehicleStateMessage = "Waiting for parsed F1 25 packets.";
    private AudioRenderPipelineSnapshot? _lastAudioPipelineSnapshot;
    private HapticEffectEngineSnapshot? _lastHapticEffectSnapshot;

    public MainWindow()
    {
        InitializeComponent();

        NavigationList.ItemsSource = _pages;
        NavigationList.SelectedIndex = 0;
        TestBenchSignalComboBox.ItemsSource = _testBenchSignals;
        TestBenchSignalComboBox.SelectedIndex = 1;
        ApplyTheme(lightTheme: false);
        _telemetryReceiver.PacketReceived += TelemetryReceiver_PacketReceived;
        _telemetryStatusTimer.Tick += TelemetryStatusTimer_Tick;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var result = await _selectedOutputDevice.OpenAsync(AudioOutputConfiguration.Default);
        UpdateOutputStatus(result.Status);
        FooterStatusText.Text = result.Message;

        try
        {
            await _telemetryReceiver.StartAsync();
            _telemetryStatusTimer.Start();
        }
        catch (Exception ex)
        {
            _telemetryStartError = ex.Message;
        }

        UpdateTelemetryStatus();
        UpdateTestBenchStatus();
    }

    private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is ShellPageDefinition page)
        {
            PageTitleText.Text = page.Title;
            PageSummaryText.Text = page.Summary;
            PageStatusText.Text = page.Status;
            PageItemsControl.ItemsSource = page.Items;
            EffectsPanel.Visibility = page.NavigationLabel == "Effects"
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestBenchPanel.Visibility = page.NavigationLabel == "Test Bench"
                ? Visibility.Visible
                : Visibility.Collapsed;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - Stage 13 effects";
            UpdateTelemetryStatus();
            UpdateEffectStatus();
            UpdateTestBenchStatus();
        }
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _hapticsStarted
            ? await _selectedOutputDevice.StopAsync()
            : await _selectedOutputDevice.StartAsync();

        if (!result.Succeeded)
        {
            FooterStatusText.Text = result.Message;
            UpdateOutputStatus(result.Status);
            return;
        }

        var starting = !_hapticsStarted;
        if (starting)
        {
            var submitResult = await ProcessAndSubmitHapticBufferAsync();
            if (!submitResult.Succeeded)
            {
                FooterStatusText.Text = submitResult.Message;
                return;
            }
        }

        _hapticsStarted = !_hapticsStarted;
        StartStopButton.Content = _hapticsStarted ? "Stop Haptics" : "Start Haptics";
        UpdateHapticsStateText();
        FooterStatusText.Text = _hapticsStarted
            ? "Haptics started with the Stage 13 effect engine feeding the mixer/safety pipeline and NullAudioOutputDevice."
            : "Haptics stopped";
        UpdateOutputStatus(result.Status);
        UpdateEffectStatus();
    }

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _recordingService.GetSnapshot();
        if (snapshot.IsRecording)
        {
            var stopResult = await _recordingService.StopAsync();
            StartRecordingButton.Content = "Start Recording";
            FooterStatusText.Text = stopResult.Message;
            if (!stopResult.Succeeded)
            {
                _recordingError = stopResult.Message;
            }

            UpdateRecordingStatus();
            return;
        }

        try
        {
            var path = CreateDefaultRecordingPath();
            var startResult = await _recordingService.StartAsync(path);
            if (startResult.Succeeded)
            {
                _recordingError = null;
                StartRecordingButton.Content = "Stop Recording";
                FooterStatusText.Text = $"Recording raw UDP packets to {Path.GetFileName(path)}.";
            }
            else
            {
                _recordingError = startResult.Message;
                FooterStatusText.Text = startResult.Message;
            }
        }
        catch (Exception ex)
        {
            _recordingError = ex.Message;
            FooterStatusText.Text = $"Recording could not start: {ex.Message}";
        }

        UpdateRecordingStatus();
    }

    private async void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _emergencyMuted = !_emergencyMuted;
        _audioPipeline.MixerSettings = _audioPipeline.MixerSettings with { EmergencyMute = _emergencyMuted };
        _audioPipeline.SafetyOptions = _audioPipeline.SafetyOptions with { EmergencyMute = _emergencyMuted };
        _testBench.EmergencyMute = _emergencyMuted;
        EmergencyMuteButton.Content = _emergencyMuted ? "Clear Mute" : "Emergency Mute";
        UpdateHapticsStateText();

        if (_hapticsStarted)
        {
            var submitResult = await ProcessAndSubmitHapticBufferAsync();
            if (!submitResult.Succeeded)
            {
                FooterStatusText.Text = submitResult.Message;
                return;
            }
        }

        if (_testBench.GetSnapshot().IsActive)
        {
            var testBenchResult = await _testBench.RenderNextBufferAsync();
            if (!testBenchResult.Succeeded)
            {
                FooterStatusText.Text = testBenchResult.Message;
                UpdateTestBenchStatus();
                return;
            }
        }

        FooterStatusText.Text = _emergencyMuted
            ? "Emergency mute is active in the mixer, safety chain, and test bench."
            : "Emergency mute cleared in the mixer, safety chain, and test bench.";
        UpdateEffectStatus();
        UpdateTestBenchStatus();
    }

    private async Task<AudioOutputDeviceResult> ProcessAndSubmitHapticBufferAsync()
    {
        var effectRender = _hapticEffectEngine.RenderNextBuffer();
        _audioPipeline.MixerSettings = _audioPipeline.MixerSettings with { EmergencyMute = _emergencyMuted };
        _audioPipeline.SafetyOptions = _audioPipeline.SafetyOptions with { EmergencyMute = _emergencyMuted };
        _lastHapticEffectSnapshot = effectRender.Snapshot;
        _lastAudioPipelineSnapshot = _audioPipeline.Process(
            effectRender.MixerInputs,
            _audioOutputBuffer);
        var result = await _selectedOutputDevice.SubmitBufferAsync(_audioOutputBuffer);
        UpdateEffectStatus();
        return result;
    }

    private void UpdateHapticsStateText()
    {
        if (_emergencyMuted)
        {
            HapticsStateText.Text = "Emergency muted";
            return;
        }

        if (!_hapticsStarted)
        {
            HapticsStateText.Text = "Stopped";
            return;
        }

        var effectSnapshot = _lastHapticEffectSnapshot ?? _hapticEffectEngine.GetSnapshot();
        HapticsStateText.Text = _lastAudioPipelineSnapshot is null
            ? "Mixer idle"
            : $"{effectSnapshot.ActiveEffectCount} effect(s); peak {_lastAudioPipelineSnapshot.OutputPeakLevel:0.000}";
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _lightTheme = !_lightTheme;
        ApplyTheme(_lightTheme);
    }

    private void ApplyTheme(bool lightTheme)
    {
        var palette = lightTheme
            ? new ThemePalette("#F5F7FA", "#FFFFFF", "#E9EEF4", "#CBD5E1", "#17212B", "#5F6C7B", "#187CA8", "#C23B35", "#1D8A60")
            : new ThemePalette("#0B0F14", "#111820", "#17212B", "#263241", "#E8EEF6", "#99A8B8", "#3BAFDA", "#E5534B", "#39B980");

        Resources["AppBackgroundBrush"] = BrushFrom(palette.Background);
        Resources["AppSurfaceBrush"] = BrushFrom(palette.Surface);
        Resources["AppSurfaceAltBrush"] = BrushFrom(palette.SurfaceAlt);
        Resources["AppBorderBrush"] = BrushFrom(palette.Border);
        Resources["AppTextBrush"] = BrushFrom(palette.Text);
        Resources["AppMutedTextBrush"] = BrushFrom(palette.MutedText);
        Resources["AppAccentBrush"] = BrushFrom(palette.Accent);
        Resources["AppDangerBrush"] = BrushFrom(palette.Danger);
        Resources["AppSuccessBrush"] = BrushFrom(palette.Success);
        ThemeButton.Content = lightTheme ? "Theme: Light" : "Theme: Dark";
    }

    private async void TestBenchSignalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TestBenchSignalComboBox.SelectedItem is not AudioTestSignalDefinition signal)
        {
            return;
        }

        _testBench.SelectSignal(signal);

        if (_testBench.GetSnapshot().IsActive)
        {
            var renderResult = await _testBench.RenderNextBufferAsync();
            FooterStatusText.Text = renderResult.Message;
        }

        UpdateTestBenchStatus();
    }

    private async void TestBenchStartStopButton_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _testBench.GetSnapshot();
        AudioTestBenchOperationResult result;

        if (snapshot.IsActive)
        {
            result = await _testBench.StopAsync();
        }
        else
        {
            if (TestBenchSignalComboBox.SelectedItem is AudioTestSignalDefinition signal)
            {
                _testBench.SelectSignal(signal);
            }

            _testBench.EmergencyMute = _emergencyMuted;
            var startResult = await _testBench.StartAsync();
            result = startResult.Succeeded
                ? await _testBench.RenderNextBufferAsync()
                : startResult;
        }

        FooterStatusText.Text = result.Message;
        UpdateTestBenchStatus();
    }

    private void UpdateTestBenchStatus()
    {
        var snapshot = _testBench.GetSnapshot();

        TestBenchStartStopButton.Content = snapshot.IsActive ? "Stop Test Bench" : "Start Test Bench";
        TestBenchStateText.Text = snapshot.EmergencyMute
            ? "Emergency muted"
            : snapshot.IsActive
                ? $"Active: {snapshot.SelectedSignalName}"
                : "Idle";
        TestBenchPeakText.Text = snapshot.OutputPeakLevel.ToString("0.000");
        TestBenchLimiterText.Text = $"{snapshot.LimitedSampleCount:N0} limited";
        TestBenchOutputText.Text = $"{snapshot.OutputDisplayName} ({snapshot.OutputState})";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Test Bench" })
        {
            PageStatusText.Text = snapshot.IsActive
                ? $"{snapshot.SelectedSignalName}; {snapshot.RenderedBufferCount:N0} buffer(s); peak {snapshot.OutputPeakLevel:0.000}; limiter {snapshot.LimitedSampleCount:N0} sample(s)."
                : "Test bench idle; select a synthetic signal and start a hardware-absent validation buffer.";
        }
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private void UpdateOutputStatus(AudioOutputStatus status)
    {
        OutputModeValueText.Text = status.DisplayName;
        OutputModeDetailText.Text = status.StatusMessage;
    }

    private void TelemetryStatusTimer_Tick(object? sender, EventArgs e)
    {
        UpdateTelemetryStatus();
    }

    private void TelemetryReceiver_PacketReceived(object? sender, UdpTelemetryPacketReceivedEventArgs e)
    {
        RecordTelemetryPacket(e.Packet);
        ParseTelemetryPacket(e.Packet);
        _ = ForwardTelemetryPacketAsync(e.Packet);
        Dispatcher.InvokeAsync(UpdateTelemetryStatus);
    }

    private void RecordTelemetryPacket(UdpTelemetryPacket packet)
    {
        var result = _recordingService.RecordPacket(packet);
        if (result.Status == TelemetryRecordingOperationStatus.Failure)
        {
            _recordingError = result.Message;
        }
    }

    private void ParseTelemetryPacket(UdpTelemetryPacket packet)
    {
        F125PacketParseResult result;

        try
        {
            result = F125PacketParser.Parse(packet.Payload);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _packetParseFailureCount);

            lock (_headerParserGate)
            {
                _lastPacketParserMessage = $"Packet parser error: {ex.Message}";
            }

            return;
        }

        switch (result.Status)
        {
            case F125PacketParseStatus.Success:
                Interlocked.Increment(ref _packetParseSuccessCount);
                break;
            case F125PacketParseStatus.Ignored:
                Interlocked.Increment(ref _packetParseIgnoredCount);
                break;
            case F125PacketParseStatus.Failure:
                Interlocked.Increment(ref _packetParseFailureCount);
                break;
        }

        lock (_headerParserGate)
        {
            _lastPacketParserMessage = result.Succeeded && result.Definition is not null
                ? $"{result.Definition.Name} packet parsed."
                : result.Message;
        }

        var vehicleStateUpdate = _vehicleStateAdapter.Apply(result);

        if (vehicleStateUpdate.WasApplied)
        {
            Interlocked.Increment(ref _vehicleStateUpdateCount);
            _hapticEffectEngine.Update(vehicleStateUpdate.State);
        }

        lock (_headerParserGate)
        {
            _lastVehicleStateMessage = vehicleStateUpdate.Message;
        }
    }

    private async Task ForwardTelemetryPacketAsync(UdpTelemetryPacket packet)
    {
        try
        {
            await _telemetryForwarder.ForwardAsync(packet);
        }
        catch (Exception ex)
        {
            _forwardingError = ex.Message;
        }

        await Dispatcher.InvokeAsync(UpdateTelemetryStatus);
    }

    private void UpdateTelemetryStatus()
    {
        if (_telemetryStartError is not null)
        {
            TelemetryStatusText.Text = "UDP: unavailable";
            UdpListenerValueText.Text = "Unavailable";
            UdpListenerDetailText.Text = _telemetryStartError;
            PacketCountValueText.Text = "0";
            PacketRateDetailText.Text = "0.00 packets/s";
            UpdateForwardingStatus();
            UpdateHeaderParserStatus();
            UpdateVehicleStateStatus();
            UpdateEffectStatus();
            UpdateRecordingStatus();
            return;
        }

        var snapshot = _telemetryReceiver.GetSnapshot();
        var status = snapshot.HasNoPacketWarning
            ? "No packets yet"
            : snapshot.IsRunning
                ? "Listening"
                : "Stopped";

        TelemetryStatusText.Text = $"UDP: {status}";
        UdpListenerValueText.Text = snapshot.IsRunning
            ? $"Listening {snapshot.BoundPort}"
            : "Stopped";
        UdpListenerDetailText.Text = snapshot.LastPacketAtUtc is null
            ? $"Default port {UdpTelemetryReceiverOptions.DefaultPort}; waiting for packets."
            : $"Last packet {snapshot.TimeSinceLastPacket?.TotalSeconds:0.0}s ago.";
        PacketCountValueText.Text = snapshot.PacketCount.ToString("N0");
        PacketRateDetailText.Text = $"{snapshot.PacketRatePerSecond:0.00} packets/s";
        UpdateForwardingStatus();
        UpdateHeaderParserStatus();
        UpdateVehicleStateStatus();
        UpdateEffectStatus();
        UpdateRecordingStatus();

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Telemetry / UDP Router" })
        {
            var forwardingSnapshot = _telemetryForwarder.GetSnapshot();
            var parsedPackets = Interlocked.Read(ref _packetParseSuccessCount);
            var vehicleStateUpdates = Interlocked.Read(ref _vehicleStateUpdateCount);
            var recordingSnapshot = _recordingService.GetSnapshot();
            PageStatusText.Text = $"{status} on port {snapshot.BoundPort}; forwarding {forwardingSnapshot.ForwardedDatagramCount:N0} datagrams; recording {recordingSnapshot.PacketCount:N0} packets; parsed {parsedPackets:N0} packets; VehicleState {vehicleStateUpdates:N0} updates";
        }

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Recordings" })
        {
            var recordingSnapshot = _recordingService.GetSnapshot();
            PageStatusText.Text = recordingSnapshot.IsRecording
                ? $"Recording {recordingSnapshot.PacketCount:N0} raw packets to {Path.GetFileName(recordingSnapshot.FilePath)}"
                : "Recording idle; replay services are available for deterministic tests.";
        }
    }

    private void UpdateForwardingStatus()
    {
        var snapshot = _telemetryForwarder.GetSnapshot();

        if (_forwardingError is not null)
        {
            ForwardingValueText.Text = "Error";
            ForwardingDetailText.Text = _forwardingError;
            return;
        }

        ForwardingValueText.Text = snapshot.IsEnabled
            ? $"{snapshot.EnabledDestinationCount} enabled"
            : "Disabled";
        ForwardingDetailText.Text = snapshot.IsEnabled
            ? $"{snapshot.ForwardedDatagramCount:N0} datagrams, {snapshot.ForwardedByteCount:N0} bytes."
            : $"{snapshot.DestinationCount} destinations configured; {snapshot.InputPacketCount:N0} packets observed.";
    }

    private void UpdateHeaderParserStatus()
    {
        var successCount = Interlocked.Read(ref _packetParseSuccessCount);
        var ignoredCount = Interlocked.Read(ref _packetParseIgnoredCount);
        var failureCount = Interlocked.Read(ref _packetParseFailureCount);
        string lastMessage;

        lock (_headerParserGate)
        {
            lastMessage = _lastPacketParserMessage;
        }

        HeaderParserValueText.Text = successCount == 0 && ignoredCount == 0 && failureCount == 0
            ? "Waiting"
            : $"{successCount:N0} valid";
        HeaderParserDetailText.Text = successCount == 0 && ignoredCount == 0 && failureCount == 0
            ? "Validates headers and parses Stage 07 packet bodies."
            : $"Ignored {ignoredCount:N0}, failed {failureCount:N0}. {lastMessage}";
    }

    private void UpdateVehicleStateStatus()
    {
        var updateCount = Interlocked.Read(ref _vehicleStateUpdateCount);
        var state = _vehicleStateAdapter.Current;
        string lastMessage;

        lock (_headerParserGate)
        {
            lastMessage = _lastVehicleStateMessage;
        }

        VehicleStateValueText.Text = updateCount == 0
            ? "Waiting"
            : $"{updateCount:N0} updates";

        if (state.Telemetry is not null)
        {
            VehicleStateDetailText.Text = $"Player {state.Frame.PlayerCarIndex}, {state.Telemetry.Value.SpeedKph} km/h, gear {state.Telemetry.Value.Gear}.";
            return;
        }

        VehicleStateDetailText.Text = updateCount == 0
            ? "Maps parsed Stage 07 packet bodies into shared VehicleState samples."
            : lastMessage;
    }

    private void UpdateEffectStatus()
    {
        var snapshot = _lastHapticEffectSnapshot ?? _hapticEffectEngine.GetSnapshot();

        EngineEffectStateText.Text = snapshot.Engine.IsActive ? "Active" : "Idle";
        EngineEffectDetailText.Text = snapshot.Engine.LastRpm is null
            ? "Waiting for RPM telemetry."
            : $"{snapshot.Engine.LastRpm:N0} RPM -> {snapshot.Engine.CurrentFrequencyHz:0.0} Hz, peak {snapshot.Engine.PeakLevel:0.000}.";
        EngineEffectDefaultsText.Text = $"Gain {EngineVibrationEffectOptions.Default.Gain:P0}; base {EngineVibrationEffectOptions.Default.MinimumFrequencyHz:0}-{EngineVibrationEffectOptions.Default.MaximumFrequencyHz:0} Hz; high {EngineVibrationEffectOptions.Default.HighFrequencyHz:0} Hz.";

        GearShiftEffectStateText.Text = snapshot.GearShift.IsActive ? "Pulse active" : "Idle";
        GearShiftEffectDetailText.Text = snapshot.GearShift.LastObservedGear is null
            ? "Waiting for gear telemetry."
            : $"Last gear {snapshot.GearShift.LastObservedGear}; last shift frame {snapshot.GearShift.LastShiftFrameIdentifier?.ToString("N0") ?? "none"}; peak {snapshot.GearShift.PeakLevel:0.000}.";
        GearShiftEffectDefaultsText.Text = $"Gain {GearShiftEffectOptions.Default.Gain:P0}; {GearShiftEffectOptions.Default.PulseFrequencyHz:0} Hz pulse; {GearShiftEffectOptions.Default.PulseDuration.TotalMilliseconds:0} ms; {GearShiftEffectOptions.Default.EngagingDebounceDuration.TotalMilliseconds:0} ms debounce.";

        KerbEffectStateText.Text = snapshot.Kerb.IsActive ? "Active" : "Idle";
        KerbEffectDetailText.Text = snapshot.Kerb.DominantSurfaceTypeId is null
            ? "Waiting for rumble strip / ridged surface telemetry."
            : $"{snapshot.Kerb.DominantSurfaceName}; {snapshot.Kerb.ActiveWheelCount} wheel(s); {snapshot.Kerb.CurrentFrequencyHz:0.0} Hz; peak {snapshot.Kerb.PeakLevel:0.000}.";
        KerbEffectDefaultsText.Text = $"Gain {KerbEffectOptions.Default.Gain:P0}; {KerbEffectOptions.Default.BaseFrequencyHz:0} Hz + {KerbEffectOptions.Default.HighFrequencyHz:0} Hz; {KerbEffectOptions.Default.MinimumSpeedKph:0}-{KerbEffectOptions.Default.FullIntensitySpeedKph:0} km/h.";

        ImpactEffectStateText.Text = snapshot.Impact.IsActive ? "Pulse active" : "Idle";
        ImpactEffectDetailText.Text = snapshot.Impact.LastImpactFrameIdentifier is null
            ? "Waiting for collision, vertical-G, force, or suspension spikes."
            : $"Last impact frame {snapshot.Impact.LastImpactFrameIdentifier:N0}; intensity {snapshot.Impact.CurrentIntensity:0.00}; peak {snapshot.Impact.PeakLevel:0.000}.";
        ImpactEffectDefaultsText.Text = $"Gain {ImpactEffectOptions.Default.Gain:P0}; {ImpactEffectOptions.Default.PulseFrequencyHz:0} Hz; {ImpactEffectOptions.Default.PulseDuration.TotalMilliseconds:0} ms; {ImpactEffectOptions.Default.CooldownDuration.TotalMilliseconds:0} ms cooldown.";

        RoadTextureEffectStateText.Text = snapshot.RoadTexture.IsActive ? "Active" : "Idle";
        RoadTextureEffectDetailText.Text = snapshot.RoadTexture.DominantSurfaceTypeId is null
            ? "Waiting for speed and surface telemetry."
            : $"{snapshot.RoadTexture.DominantSurfaceName}; mix {snapshot.RoadTexture.SurfaceMix:0.00}; {snapshot.RoadTexture.CurrentFrequencyHz:0.0} Hz; peak {snapshot.RoadTexture.PeakLevel:0.000}.";
        RoadTextureEffectDefaultsText.Text = $"Gain {RoadTextureEffectOptions.Default.Gain:P0}; {RoadTextureEffectOptions.Default.MinimumSpeedKph:0}-{RoadTextureEffectOptions.Default.FullIntensitySpeedKph:0} km/h; tarmac is low-level.";

        SlipEffectStateText.Text = snapshot.Slip.IsActive ? "Active" : "Idle";
        SlipEffectDetailText.Text = snapshot.Slip.CurrentSlipIntensity <= 0f && snapshot.Slip.CurrentLockIntensity <= 0f
            ? "Waiting for Motion Ex slip ratio / angle telemetry."
            : $"Slip {snapshot.Slip.CurrentSlipIntensity:0.00}; lock {snapshot.Slip.CurrentLockIntensity:0.00}; {snapshot.Slip.CurrentFrequencyHz:0.0} Hz; peak {snapshot.Slip.PeakLevel:0.000}.";
        SlipEffectDefaultsText.Text = $"Gain {SlipEffectOptions.Default.Gain:P0}; {SlipEffectOptions.Default.BaseFrequencyHz:0} Hz slip; {SlipEffectOptions.Default.BrakeLockFrequencyHz:0} Hz lock; min {SlipEffectOptions.Default.MinimumSpeedKph:0} km/h.";

        if (NavigationList.SelectedItem is ShellPageDefinition { NavigationLabel: "Effects" })
        {
            PageStatusText.Text = $"{snapshot.ActiveEffectCount} active effect source(s); engine {EngineEffectStateText.Text.ToLowerInvariant()}, gear {GearShiftEffectStateText.Text.ToLowerInvariant()}, kerb {KerbEffectStateText.Text.ToLowerInvariant()}, impact {ImpactEffectStateText.Text.ToLowerInvariant()}, road {RoadTextureEffectStateText.Text.ToLowerInvariant()}, slip {SlipEffectStateText.Text.ToLowerInvariant()}; peak {snapshot.PeakLevel:0.000}.";
        }
    }

    private void UpdateRecordingStatus()
    {
        var snapshot = _recordingService.GetSnapshot();

        if (_recordingError is not null)
        {
            RecordingValueText.Text = "Error";
            RecordingDetailText.Text = _recordingError;
            return;
        }

        RecordingValueText.Text = snapshot.IsRecording
            ? $"{snapshot.PacketCount:N0} packets"
            : "Idle";
        if (snapshot.IsRecording)
        {
            RecordingDetailText.Text = snapshot.LastPacketRelativeTime is null
                ? $"Writing {Path.GetFileName(snapshot.FilePath)}; waiting for first packet."
                : $"Writing {Path.GetFileName(snapshot.FilePath)}; last packet {snapshot.LastPacketRelativeTime.Value.TotalSeconds:0.000}s.";
            return;
        }

        RecordingDetailText.Text = "Ready to capture raw UDP packets to versioned replay files.";
    }

    private static string CreateDefaultRecordingPath()
    {
        var recordingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HapticDrive.Asio",
            "Recordings");
        Directory.CreateDirectory(recordingsDirectory);

        return Path.Combine(
            recordingsDirectory,
            $"f1-25-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.hdrec");
    }

    protected override void OnClosed(EventArgs e)
    {
        _telemetryStatusTimer.Stop();
        _testBench.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _recordingService.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _telemetryReceiver.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _telemetryForwarder.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _selectedOutputDevice.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnClosed(e);
    }

    private sealed record ShellPageDefinition(
        string NavigationLabel,
        string Title,
        string Summary,
        string Status,
        IReadOnlyList<string> Items);

    private sealed record ThemePalette(
        string Background,
        string Surface,
        string SurfaceAlt,
        string Border,
        string Text,
        string MutedText,
        string Accent,
        string Danger,
        string Success);
}
