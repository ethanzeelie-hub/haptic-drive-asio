using HapticDrive.Asio.Audio.Devices;
using HapticDrive.Asio.Audio.Diagnostics;
using HapticDrive.Asio.Audio.DriverDiscovery;
using HapticDrive.Asio.Audio.Effects;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Mixing;
using HapticDrive.Asio.Audio.Pipeline;
using HapticDrive.Asio.Audio.Profiles;
using HapticDrive.Asio.Audio.Safety;
using HapticDrive.Asio.Audio.TestBench;
using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Core.Audio;
using HapticDrive.Asio.Core.Safety;
using HapticDrive.Asio.Core.Telemetry;
using HapticDrive.Asio.Core.Vehicle.Freshness;
using HapticDrive.Asio.Recording;
using HapticDrive.Asio.Runtime;
using HapticDrive.Asio.Runtime.Pipeline;
using HapticDrive.Asio.Runtime.Telemetry;
using HapticDrive.Actuation.Driving;
using HapticDrive.Actuation.PHpr;
using HapticDrive.Actuation.Shift;
using HapticDrive.Input.Abstractions.Devices;
using HapticDrive.Input.Abstractions.Paddles;
using HapticDrive.Input.Abstractions.Shift;
using HapticDrive.Input.Windows;
using HapticDrive.Simagic.PHPR.Abstractions.Coexistence;
using HapticDrive.Simagic.PHPR.Abstractions.Commands;
using HapticDrive.Simagic.PHPR.Abstractions.Output;
using HapticDrive.Simagic.PHPR.Abstractions.Readiness;
using HapticDrive.Simagic.PHPR.Abstractions.Safety;
using HapticDrive.Simagic.PHPR.Abstractions.Validation;
using HapticDrive.Simagic.PHPR.Output.Windows;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HapticDrive.Asio.Core.Games;

namespace HapticDrive.Asio.App;

internal sealed partial class AppRuntimeSession
{
    internal async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        // Guardrail: keep execution ownership inline at the shell boundary: ? await _hapticPipeline.StopAsync() : await _hapticPipeline.StartAsync();
        await RunSerializedLifecycleOperationAsync(
            async (_, _) =>
            {
                var result = _hapticsStarted
                    ? await _hapticPipeline.StopAsync().ConfigureAwait(true)
                    : await _hapticPipeline.StartAsync().ConfigureAwait(true);

                if (!result.Succeeded)
                {
                    FooterStatusText.Text = result.Message;
                    if (result.OutputResult is not null)
                    {
                        UpdateOutputStatus(result.OutputResult.Status);
                    }

                    return;
                }

                _hapticsStarted = !_hapticsStarted;
                var pipelineSnapshot = RefreshDrivingArmedAndShiftIntentTelemetry();
                UpdateHapticsControlState(pipelineSnapshot);
                FooterStatusText.Text = _hapticsStarted
                    ? "Haptics started with output-owned low-latency rendering; Null output remains the default unless ASIO was selected, routed, and armed."
                    : "Haptics stopped";
                UpdateOutputStatus(result.OutputResult?.Status ?? pipelineSnapshot.Output);
                UpdateManualAsioHardwareTestStatus();
                UpdateEffectStatus();
                UpdateShiftIntentStatus();
                UpdateMockPedalEffectsStatus();
                UpdateDiagnosticsStatus();
            },
            "Start/stop haptics failed");
    }

    internal async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSerializedLifecycleOperationAsync(
            async (_, _) =>
            {
                var snapshot = _hapticPipeline.RecordingService.GetSnapshot();
                if (snapshot.IsRecording)
                {
                    var stopResult = await _hapticPipeline.RecordingService.StopAsync().ConfigureAwait(true);
                    FooterStatusText.Text = stopResult.Message;
                    _recordingError = stopResult.Succeeded ? null : stopResult.Message;
                    UpdateRecordingStatus();
                    return;
                }

                try
                {
                    var path = CreateDefaultRecordingPath();
                    var metadata = BuildRecordingMetadata();
                    var startResult = await _hapticPipeline.RecordingService.StartAsync(path, metadata).ConfigureAwait(true);
                    if (startResult.Succeeded)
                    {
                        _recordingError = null;
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
            },
            "Recording lifecycle failed");
    }

    private async void StartReplayButton_Click(object sender, RoutedEventArgs e)
    {
        var replaySnapshot = _hapticPipeline.GetSnapshot().Replay;
        if (replaySnapshot.IsReplaying)
        {
            await _hapticPipeline.ReplayService.StopAsync();
            FooterStatusText.Text = "Replay stop requested.";
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            return;
        }

        var path = RecordingLibraryManager.FindLatestRecordingPath(GetRecordingsDirectory());
        if (path is null)
        {
            _replayError = "No local .hdrec recording is available to replay yet.";
            FooterStatusText.Text = _replayError;
            UpdateRecordingStatus();
            return;
        }

        _replayError = null;
        var replayMode = GetSelectedReplayTimingMode();
        FooterStatusText.Text = $"Replaying {Path.GetFileName(path)} in {replayMode.Label} mode through the output-owned haptic pipeline.";
        _activeReplayTask = ReplayRecordingAsync(path);
        UpdateRecordingStatus();
    }

    private async void ReplaySelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            _replayError = "Select a recording from the library before replaying.";
            FooterStatusText.Text = _replayError;
            UpdateRecordingStatus();
            return;
        }

        var replaySnapshot = _hapticPipeline.GetSnapshot().Replay;
        if (replaySnapshot.IsReplaying)
        {
            await _hapticPipeline.ReplayService.StopAsync();
            FooterStatusText.Text = "Replay stop requested.";
            UpdateRecordingStatus();
            UpdateDiagnosticsStatus();
            return;
        }

        _replayError = null;
        var replayMode = GetSelectedReplayTimingMode();
        FooterStatusText.Text = $"Replaying selected recording {Path.GetFileName(item.Path)} in {replayMode.Label} mode.";
        _activeReplayTask = ReplayRecordingAsync(item.Path);
        UpdateRecordingStatus();
    }

    private void ReplayTimingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveAppSettings();
        UpdateRecordingStatus();
    }

    private async void RefreshRecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRecordingLibraryAsync();
    }

    private async void DeleteSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = "Select a recording before deleting.";
            FooterStatusText.Text = RecordingLibraryStatusText.Text;
            return;
        }

        var recordingSnapshot = _hapticPipeline.GetSnapshot().Recording;
        var result = RecordingLibraryManager.DeleteSelected(
            GetRecordingsDirectory(),
            item.Path,
            recordingSnapshot.IsRecording ? recordingSnapshot.FilePath : null);
        await RefreshRecordingLibraryAsync();
        RecordingLibraryStatusText.Text = result.Message;
        FooterStatusText.Text = result.Message;
        UpdateRecordingStatus();
    }

    private async void RenameSelectedRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            RecordingLibraryStatusText.Text = "Select a recording before renaming.";
            FooterStatusText.Text = RecordingLibraryStatusText.Text;
            return;
        }

        var recordingSnapshot = _hapticPipeline.GetSnapshot().Recording;
        var result = RecordingLibraryManager.RenameSelected(
            GetRecordingsDirectory(),
            item.Path,
            RecordingRenameTextBox.Text,
            recordingSnapshot.IsRecording ? recordingSnapshot.FilePath : null);
        await RefreshRecordingLibraryAsync(result.RenamedPath);
        RecordingLibraryStatusText.Text = result.Message;
        FooterStatusText.Text = result.Message;
        UpdateRecordingStatus();
    }

    private void RecordingLibraryFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _recordingLibraryFilterText = RecordingLibraryFilterTextBox.Text.Trim();
        ApplyRecordingLibraryFilter();
    }

    private void ClearRecordingLibraryFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RecordingLibraryFilterTextBox.Text))
        {
            ApplyRecordingLibraryFilter();
            return;
        }

        RecordingLibraryFilterTextBox.Clear();
    }

    private async void RecordingLibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CancelRecordingLibraryAnalysis();

        if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem item)
        {
            if (_recordingLibraryHistogramTextByPath.TryGetValue(item.Path, out var cachedAnalysisText))
            {
                RecordingLibraryDetailText.Text = RecordingLibraryDetailFormatter.BuildDetailText(item.DetailText, cachedAnalysisText);
            }
            else
            {
                RecordingLibraryDetailText.Text = RecordingLibraryDetailFormatter.BuildDetailText(item.DetailText, "Packet histogram loading...");
                await LoadRecordingLibraryAnalysisIntoSelectedDetailAsync(item).ConfigureAwait(true);
            }

            RecordingRenameTextBox.Text = Path.GetFileNameWithoutExtension(item.Path);
            return;
        }

        RecordingRenameTextBox.Text = string.Empty;
        RecordingLibraryDetailText.Text = string.Empty;
        RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
    }

    private async Task ReplayRecordingAsync(string path)
    {
        var replayMode = GetSelectedReplayTimingMode();
        var result = await _hapticPipeline.ReplayFileAsync(path, replayMode.Options);
        await Dispatcher.InvokeAsync(() =>
        {
            _replayError = result.Succeeded ? null : result.Message;
            FooterStatusText.Text = $"{result.Message} Replay mode: {replayMode.Label}.";
            UpdateTelemetryStatus();
            UpdateRecordingStatus();
            UpdateEffectStatus();
            UpdateDiagnosticsStatus();
        });
    }

    private async Task RefreshRecordingLibraryAsync(string? selectedPath = null)
    {
        try
        {
            CancelRecordingLibraryAnalysis();
            _recordingLibraryHistogramTextByPath.Clear();
            _recordingLibraryItems = await RecordingLibraryManager.LoadAsync(GetRecordingsDirectory());
            ApplyRecordingLibraryFilter(selectedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            RecordingLibraryStatusText.Text = $"Recording library could not be refreshed: {ex.Message}";
            RecordingLibraryDetailText.Text = string.Empty;
        }

        UpdateTelemetryUdpPresentation();
    }

    private async void CopySelectedRecordingDetailButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            FooterStatusText.Text = "Select a recording before copying detail.";
            return;
        }

        CancelRecordingLibraryAnalysis();

        try
        {
            RecordingLibraryStatusText.Text = $"Preparing detail for {Path.GetFileName(item.Path)}...";
            var analysisText = await GetRecordingLibraryAnalysisTextAsync(item).ConfigureAwait(true);
            var clipboardText = RecordingLibraryDetailFormatter.BuildClipboardText(
                item.Path,
                item.DisplayText,
                item.DetailText,
                analysisText);
            Clipboard.SetText(clipboardText);

            if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem selectedItem
                && string.Equals(selectedItem.Path, item.Path, StringComparison.OrdinalIgnoreCase))
            {
                RecordingLibraryDetailText.Text = RecordingLibraryDetailFormatter.BuildDetailText(item.DetailText, analysisText);
            }

            RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
            FooterStatusText.Text = $"Selected recording detail copied for {Path.GetFileName(item.Path)}.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
            FooterStatusText.Text = $"Selected recording detail could not be copied: {ex.Message}";
        }
    }

    private async void ExportSelectedRecordingDetailButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingLibraryListBox.SelectedItem is not RecordingLibraryItem item)
        {
            FooterStatusText.Text = "Select a recording before exporting detail.";
            return;
        }

        CancelRecordingLibraryAnalysis();

        try
        {
            RecordingLibraryStatusText.Text = $"Preparing detail export for {Path.GetFileName(item.Path)}...";
            var analysisText = await GetRecordingLibraryAnalysisTextAsync(item).ConfigureAwait(true);
            var exportText = RecordingLibraryDetailFormatter.BuildClipboardText(
                item.Path,
                item.DisplayText,
                item.DetailText,
                analysisText);
            var path = _selectedRecordingDetailExporter.ExportText(
                new SelectedRecordingDetailExportInputs(
                    DateTimeOffset.UtcNow,
                    item.Path,
                    exportText),
                GetLocalValidationResultsDirectory());

            if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem selectedItem
                && string.Equals(selectedItem.Path, item.Path, StringComparison.OrdinalIgnoreCase))
            {
                RecordingLibraryDetailText.Text = RecordingLibraryDetailFormatter.BuildDetailText(item.DetailText, analysisText);
            }

            RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
            FooterStatusText.Text = $"Selected recording detail exported locally to {path}.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
            FooterStatusText.Text = $"Selected recording detail could not be exported: {ex.Message}";
        }
    }

    private async Task LoadRecordingLibraryAnalysisIntoSelectedDetailAsync(RecordingLibraryItem item)
    {
        try
        {
            var analysisText = await GetRecordingLibraryAnalysisTextAsync(item).ConfigureAwait(true);
            if (RecordingLibraryListBox.SelectedItem is RecordingLibraryItem selectedItem
                && string.Equals(selectedItem.Path, item.Path, StringComparison.OrdinalIgnoreCase))
            {
                RecordingLibraryDetailText.Text = RecordingLibraryDetailFormatter.BuildDetailText(item.DetailText, analysisText);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<string> GetRecordingLibraryAnalysisTextAsync(RecordingLibraryItem item)
    {
        if (_recordingLibraryHistogramTextByPath.TryGetValue(item.Path, out var cachedAnalysisText))
        {
            return cachedAnalysisText;
        }

        var analysisCts = new CancellationTokenSource();
        _recordingLibraryAnalysisCts = analysisCts;

        try
        {
            var analysisText = await RecordingPacketHistogramAnalyzer
                .AnalyzeAsync(item.Path, analysisCts.Token)
                .ConfigureAwait(true);

            _recordingLibraryHistogramTextByPath[item.Path] = analysisText;
            return analysisText;
        }
        finally
        {
            if (ReferenceEquals(_recordingLibraryAnalysisCts, analysisCts))
            {
                _recordingLibraryAnalysisCts.Dispose();
                _recordingLibraryAnalysisCts = null;
            }
        }
    }

    private void ApplyRecordingLibraryFilter(string? selectedPath = null)
    {
        var preferredPath = selectedPath
            ?? (RecordingLibraryListBox.SelectedItem as RecordingLibraryItem)?.Path;
        _filteredRecordingLibraryItems = RecordingLibraryManager.Filter(_recordingLibraryItems, _recordingLibraryFilterText);
        RecordingLibraryListBox.ItemsSource = _filteredRecordingLibraryItems;
        RecordingLibraryListBox.SelectedItem = string.IsNullOrWhiteSpace(preferredPath)
            ? null
            : _filteredRecordingLibraryItems.FirstOrDefault(item =>
                string.Equals(item.Path, preferredPath, StringComparison.OrdinalIgnoreCase));

        if (RecordingLibraryListBox.SelectedItem is null)
        {
            RecordingLibraryDetailText.Text = string.Empty;
            RecordingLibraryStatusText.Text = BuildRecordingLibraryStatusText();
        }
    }

    private void CancelRecordingLibraryAnalysis()
    {
        if (_recordingLibraryAnalysisCts is null)
        {
            return;
        }

        _recordingLibraryAnalysisCts.Cancel();
        _recordingLibraryAnalysisCts.Dispose();
        _recordingLibraryAnalysisCts = null;
    }

    private string BuildRecordingLibraryStatusText()
    {
        var recordingsDirectory = GetRecordingsDirectory();
        if (_recordingLibraryItems.Count == 0)
        {
            return $"No .hdrec files found in {recordingsDirectory}.";
        }

        if (string.IsNullOrWhiteSpace(_recordingLibraryFilterText))
        {
            return $"{_recordingLibraryItems.Count} recording(s) found in {recordingsDirectory}.";
        }

        if (_filteredRecordingLibraryItems.Count == 0)
        {
            return $"No recordings match '{_recordingLibraryFilterText}' in {recordingsDirectory}.";
        }

        return
            $"Showing {_filteredRecordingLibraryItems.Count} of {_recordingLibraryItems.Count} recording(s) matching '{_recordingLibraryFilterText}' in {recordingsDirectory}.";
    }

    private TelemetryRecordingMetadata BuildRecordingMetadata()
    {
        var descriptor = GameTelemetryCatalog.Registry.GetRequired(
            new GameIntegrationId(GameTelemetryCatalog.NormalizeGameId(_selectedGameId)));
        var validatedProfile = HapticProfileValidator.Validate(_currentProfile).Profile;
        return TelemetryRecordingMetadata.CreateDefault(
            DateTimeOffset.UtcNow,
            sourceGame: descriptor.DisplayName,
            sourceProfile: validatedProfile.Name,
            gameIntegrationId: descriptor.Id.Value,
            telemetryProtocolName: descriptor.TelemetryProtocolName,
            telemetryProtocolVersion: descriptor.TelemetryProtocolVersion,
            profileHash: ComputeProfileHash(validatedProfile),
            sourceEndpoint: "unknown",
            bindAddress: BuildTelemetryBindAddressText());
    }
}

