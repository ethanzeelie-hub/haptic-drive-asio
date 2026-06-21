using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App.Controllers;

internal sealed class ProfileTuningController : IAsyncDisposable
{
    private readonly Func<HapticDriveProfile, CancellationToken, ValueTask<HapticProfileSaveResult>> _saveAsync;
    private readonly Action<HapticDriveProfile> _applyLivePreview;
    private readonly Action<HapticDriveProfile> _updateControlText;
    private readonly Action<AudioProfileWorkflowFeedback> _publishFeedback;
    private readonly Action<string> _reportSaveFailure;
    private readonly Func<HapticDriveProfile> _currentProfileAccessor;
    private readonly Func<bool> _hapticsStartedAccessor;
    private readonly Action<HapticDriveProfile> _replaceCurrentProfile;
    private readonly IHapticEffectRegistry _effectRegistry;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _scheduledSaveCts;

    public ProfileTuningController(
        EffectSettingsListViewModel effectSettingsViewModel,
        IHapticEffectRegistry effectRegistry,
        Func<HapticDriveProfile> currentProfileAccessor,
        Func<bool> hapticsStartedAccessor,
        Action<HapticDriveProfile> replaceCurrentProfile,
        Action<HapticDriveProfile> applyLivePreview,
        Action<HapticDriveProfile> updateControlText,
        Func<HapticDriveProfile, CancellationToken, ValueTask<HapticProfileSaveResult>> saveAsync,
        Action<AudioProfileWorkflowFeedback> publishFeedback,
        Action<string> reportSaveFailure,
        TimeProvider? timeProvider = null,
        TimeSpan? debounceDelay = null)
    {
        EffectSettingsViewModel = effectSettingsViewModel ?? throw new ArgumentNullException(nameof(effectSettingsViewModel));
        _effectRegistry = effectRegistry ?? throw new ArgumentNullException(nameof(effectRegistry));
        _currentProfileAccessor = currentProfileAccessor ?? throw new ArgumentNullException(nameof(currentProfileAccessor));
        _hapticsStartedAccessor = hapticsStartedAccessor ?? throw new ArgumentNullException(nameof(hapticsStartedAccessor));
        _replaceCurrentProfile = replaceCurrentProfile ?? throw new ArgumentNullException(nameof(replaceCurrentProfile));
        _applyLivePreview = applyLivePreview ?? throw new ArgumentNullException(nameof(applyLivePreview));
        _updateControlText = updateControlText ?? throw new ArgumentNullException(nameof(updateControlText));
        _saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
        _publishFeedback = publishFeedback ?? throw new ArgumentNullException(nameof(publishFeedback));
        _reportSaveFailure = reportSaveFailure ?? throw new ArgumentNullException(nameof(reportSaveFailure));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(250);
    }

    public EffectSettingsListViewModel EffectSettingsViewModel { get; }

    public void ApplyLiveTuning(HapticDriveProfile profile, bool hapticsStarted)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _replaceCurrentProfile(profile);
        _applyLivePreview(profile);
        _updateControlText(profile);
        RefreshEffectSettings(profile);
        ScheduleDebouncedSave(profile, hapticsStarted);
    }

    public async Task CommitProfileNameAsync(HapticDriveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        CancelScheduledSave();
        _replaceCurrentProfile(profile);
        _applyLivePreview(profile);
        _updateControlText(profile);
        RefreshEffectSettings(profile);
        var saveResult = await PersistAsync(profile, CancellationToken.None).ConfigureAwait(false);
        Publish(AudioProfileWorkflowFeedbackPlanner.BuildProfileNameCommitFeedback(saveResult));
    }

    public void RefreshEffectSettings(HapticDriveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        EffectSettingsViewModel.ReplaceWith(
            EffectSettingsListViewModel.BuildItems(
                profile,
                _effectRegistry,
                ResetEffectToDefaults));
    }

    private void ResetEffectToDefaults(string effectKey)
    {
        var current = _currentProfileAccessor();
        var defaults = _effectRegistry.GetRequired(effectKey).CreateDefaultSettings();
        var updatedDocuments = new Dictionary<string, EffectSettingsDocument>(
            current.EffectSettings,
            StringComparer.OrdinalIgnoreCase)
        {
            [effectKey] = defaults
        };

        var normalized = HapticEffectSettingsTranslator.NormalizeDocuments(
            updatedDocuments,
            _effectRegistry,
            messages: null);
        var updatedProfile = HapticProfileValidator.Validate(current with
        {
            EffectSettings = normalized,
            Effects = HapticEffectSettingsTranslator.ToLegacyTuning(normalized)
        }).Profile;

        ApplyLiveTuning(updatedProfile, _hapticsStartedAccessor());
    }

    private void ScheduleDebouncedSave(HapticDriveProfile profile, bool hapticsStarted)
    {
        CancelScheduledSave();
        var cts = new CancellationTokenSource();
        _scheduledSaveCts = cts;
        _ = RunDebouncedSaveAsync(profile, hapticsStarted, cts.Token);
    }

    private async Task RunDebouncedSaveAsync(
        HapticDriveProfile profile,
        bool hapticsStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_debounceDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
            var saveResult = await PersistAsync(profile, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Publish(AudioProfileWorkflowFeedbackPlanner.BuildTuningChangedFeedback(saveResult, hapticsStarted));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask<HapticProfileSaveResult> PersistAsync(
        HapticDriveProfile profile,
        CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _saveAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void Publish(AudioProfileWorkflowFeedback feedback)
    {
        _publishFeedback(feedback);
        if (!string.IsNullOrWhiteSpace(feedback.FooterStatusText)
            && feedback.ProfileStatusMessage is not null
            && feedback.ProfileStatusMessage.Contains("could not be saved", StringComparison.OrdinalIgnoreCase))
        {
            _reportSaveFailure(feedback.FooterStatusText);
        }
    }

    private void CancelScheduledSave()
    {
        var cts = Interlocked.Exchange(ref _scheduledSaveCts, null);
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        CancelScheduledSave();
        _saveGate.Dispose();
        return ValueTask.CompletedTask;
    }
}
