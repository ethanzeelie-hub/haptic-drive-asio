using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Core.Audio;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HapticDrive.Asio.App;

public partial class MainWindow : Window
{
    private readonly IAudioOutputDevice _selectedOutputDevice = new NullAudioOutputDevice();

    private readonly IReadOnlyList<ShellPageDefinition> _pages =
    [
        new(
            "Dashboard",
            "Dashboard",
            "A safe overview for the app before telemetry, audio output, and replay are implemented.",
            "Stage 02 hardware-absent output status",
            [
                "NullAudioOutputDevice is the default safe output.",
                "Haptics start/stop drives the null output state only.",
                "Telemetry is not connected until the UDP listener stage.",
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
            "Placeholder for F1 25 UDP input, packet status, and byte-preserving forwarding.",
            "Telemetry and routing planned",
            [
                "Default listen port will be 20778.",
                "UDP receive is scheduled for Stage 04.",
                "UDP forwarding is scheduled for Stage 05.",
                "F1 25 parsing must come from the official v3 PDF, not guessed layouts."
            ]),
        new(
            "Recordings",
            "Recordings",
            "Placeholder for telemetry capture and deterministic replay without running F1 25.",
            "Recording and replay planned",
            [
                "Recording and replay are scheduled for Stage 09.",
                "Replay mode is required before physical hardware validation.",
                "Raw UDP bytes must be preserved for replay and forwarding."
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
                "Diagnostics become more meaningful as telemetry, parser, audio, and replay stages are implemented.",
                "Logging must not block telemetry, UI, disk, or audio paths.",
                "A copy diagnostics report action is planned for Stage 14."
            ])
    ];

    private bool _hapticsStarted;
    private bool _emergencyMuted;
    private bool _lightTheme;

    public MainWindow()
    {
        InitializeComponent();

        NavigationList.ItemsSource = _pages;
        NavigationList.SelectedIndex = 0;
        ApplyTheme(lightTheme: false);
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var result = await _selectedOutputDevice.OpenAsync(AudioOutputConfiguration.Default);
        UpdateOutputStatus(result.Status);
        FooterStatusText.Text = result.Message;
    }

    private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is ShellPageDefinition page)
        {
            PageTitleText.Text = page.Title;
            PageSummaryText.Text = page.Summary;
            PageStatusText.Text = page.Status;
            PageItemsControl.ItemsSource = page.Items;
            FooterStatusText.Text = $"Viewing {page.NavigationLabel} - Stage 01 shell only";
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

    private void EmergencyMuteButton_Click(object sender, RoutedEventArgs e)
    {
        _emergencyMuted = !_emergencyMuted;
        SafetyStateText.Text = _emergencyMuted ? "Muted" : "Normal";
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
        TelemetryStatusText.Text = $"Output: {status.State}";
    }

    protected override void OnClosed(EventArgs e)
    {
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
