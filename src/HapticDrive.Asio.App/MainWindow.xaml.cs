using HapticDrive.Asio.Audio.Devices;
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
    private readonly IUdpTelemetryReceiver _telemetryReceiver = new UdpTelemetryReceiver();
    private readonly IUdpTelemetryForwarder _telemetryForwarder = new UdpTelemetryForwarder();
    private readonly TelemetryRecordingService _recordingService = new();
    private readonly F125VehicleStateAdapter _vehicleStateAdapter = new();
    private readonly DispatcherTimer _telemetryStatusTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    private readonly IReadOnlyList<ShellPageDefinition> _pages =
    [
        new(
            "Dashboard",
            "Dashboard",
            "A safe overview for raw UDP telemetry, output state, and hardware-absent operation.",
            "Stage 09 recording and replay status",
            [
                "UDP listener starts on port 20778 by default.",
                "Packets are counted, preserved as raw datagrams, and offered to the forwarder.",
                "Recording captures raw incoming UDP payload bytes to a versioned replay file.",
                "Forwarding is byte-preserving and parser-independent.",
                "The F1 25 parser validates packet format, year, ID, version, exact length, and Stage 07 packet bodies.",
                "Stage 08 maps parsed packets into shared last-known VehicleState samples.",
                "NullAudioOutputDevice is the default safe output.",
                "No haptic effects are implemented yet.",
                "The app remains safe to open without ASIO hardware or shaker hardware."
            ]),
        new(
            "Effects",
            "Effects",
            "Placeholder home for gear shift, engine, kerb, road texture, impact, and slip tuning.",
            "Effect controls planned",
            [
                "Gear shift and engine effects are scheduled for Stage 12.",
                "Kerb, impact, road texture, and slip effects are scheduled for Stage 13.",
                "Per-effect strength, frequency, priority, and test buttons are not implemented yet."
            ]),
        new(
            "Mixer / Routing",
            "Mixer / Routing",
            "Placeholder for mono output routing, mixer level controls, priority, ducking, and safety-chain visibility.",
            "Mixer and routing planned",
            [
                "The audio mixer and safety processors are scheduled for Stage 10.",
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
                "Replay tests do not require F1 25, UDP sockets, audio output, ASIO hardware, or shaker hardware.",
                "A polished recording library UI is deferred."
            ]),
        new(
            "Test Bench",
            "Test Bench",
            "Placeholder for safe synthetic signals and effect simulations.",
            "Test bench planned",
            [
                "Sine, pulse, sweep, channel, and effect test signals are scheduled for Stage 11.",
                "Safe ramp-up must be used before any real hardware output.",
                "Null output remains the automated-test target."
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
            "Diagnostics planned",
            [
                "Output status is available for the selected safe output device.",
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

    public MainWindow()
    {
        InitializeComponent();

        NavigationList.ItemsSource = _pages;
        NavigationList.SelectedIndex = 0;
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
    }

    private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is ShellPageDefinition page)
        {
            PageTitleText.Text = page.Title;
            PageSummaryText.Text = page.Summary;
            PageStatusText.Text = page.Status;
            PageItemsControl.ItemsSource = page.Items;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - Stage 09 recording and replay";
            UpdateTelemetryStatus();
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

        _hapticsStarted = !_hapticsStarted;
        StartStopButton.Content = _hapticsStarted ? "Stop Haptics" : "Start Haptics";
        HapticsStateText.Text = _hapticsStarted ? "Null output running" : "Stopped";
        FooterStatusText.Text = _hapticsStarted
            ? "Haptics started with NullAudioOutputDevice. No sound or haptic signal is generated."
            : "Haptics stopped";
        UpdateOutputStatus(result.Status);
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

    private void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _emergencyMuted = !_emergencyMuted;
        EmergencyMuteButton.Content = _emergencyMuted ? "Clear Mute" : "Emergency Mute";
        FooterStatusText.Text = _emergencyMuted
            ? "Emergency mute placeholder is active"
            : "Emergency mute placeholder cleared";
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
