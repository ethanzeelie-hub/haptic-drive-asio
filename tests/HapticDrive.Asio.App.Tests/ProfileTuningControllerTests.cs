using HapticDrive.Asio.App.Controllers;
using HapticDrive.Asio.App.ViewModels;
using HapticDrive.Asio.Audio.Effects.Registry;
using HapticDrive.Asio.Audio.Profiles;

namespace HapticDrive.Asio.App.Tests;

public sealed class ProfileTuningControllerTests
{
    [Fact]
    public async Task DebouncesPersistentSave()
    {
        var currentProfile = HapticDriveProfile.Default;
        var savedProfileNames = new List<string>();
        var feedback = new List<AudioProfileWorkflowFeedback>();

        await using var controller = CreateController(
            currentProfileAccessor: () => currentProfile,
            hapticsStartedAccessor: () => false,
            replaceCurrentProfile: profile => currentProfile = profile,
            applyLivePreview: _ => { },
            updateControlText: _ => { },
            saveAsync: (profile, _) =>
            {
                savedProfileNames.Add(profile.Name);
                return ValueTask.FromResult(HapticProfileSaveResult.Success("memory", wasRepaired: false, []));
            },
            publishFeedback: feedback.Add,
            reportSaveFailure: _ => { },
            debounceDelay: TimeSpan.FromMilliseconds(25));

        controller.ApplyLiveTuning(currentProfile with { Name = "First update" }, hapticsStarted: false);
        controller.ApplyLiveTuning(currentProfile with { Name = "Second update" }, hapticsStarted: false);

        await Task.Delay(120);

        Assert.Equal(["Second update"], savedProfileNames);
        Assert.Single(feedback);
        Assert.Equal("Tuning applied; haptics are still stopped.", feedback[0].FooterStatusText);
    }

    [Fact]
    public async Task LivePreviewDoesNotWaitForDisk()
    {
        var currentProfile = HapticDriveProfile.Default;
        var livePreviewCount = 0;
        var saveStartedCount = 0;
        var saveGate = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new EffectSettingsListViewModel();
        async ValueTask<HapticProfileSaveResult> SaveAsync(HapticDriveProfile _, CancellationToken __)
        {
            Interlocked.Increment(ref saveStartedCount);
            await saveGate.Task;
            return HapticProfileSaveResult.Success("memory", wasRepaired: false, []);
        }

        await using var controller = CreateController(
            effectSettingsViewModel: viewModel,
            currentProfileAccessor: () => currentProfile,
            hapticsStartedAccessor: () => true,
            replaceCurrentProfile: profile => currentProfile = profile,
            applyLivePreview: _ => livePreviewCount++,
            updateControlText: _ => { },
            saveAsync: SaveAsync,
            publishFeedback: _ => { },
            reportSaveFailure: _ => { },
            debounceDelay: TimeSpan.FromMilliseconds(20));

        var updatedProfile = currentProfile with { Name = "Live preview profile" };
        controller.ApplyLiveTuning(updatedProfile, hapticsStarted: true);

        Assert.Equal(1, livePreviewCount);
        Assert.Equal("Live preview profile", currentProfile.Name);
        Assert.Equal(BuiltInHapticEffectRegistry.Instance.All.Count, viewModel.Items.Count);
        Assert.Equal(0, Volatile.Read(ref saveStartedCount));

        await Task.Delay(80);

        Assert.Equal(1, Volatile.Read(ref saveStartedCount));

        saveGate.SetResult(null);
        await Task.Delay(20);
    }

    [Fact]
    public async Task SaveFailuresAreReported()
    {
        var currentProfile = HapticDriveProfile.Default;
        var feedback = new List<AudioProfileWorkflowFeedback>();
        var failures = new List<string>();

        await using var controller = CreateController(
            currentProfileAccessor: () => currentProfile,
            hapticsStartedAccessor: () => true,
            replaceCurrentProfile: profile => currentProfile = profile,
            applyLivePreview: _ => { },
            updateControlText: _ => { },
            saveAsync: (_, _) => ValueTask.FromResult(HapticProfileSaveResult.Failure("memory", "Profile could not be saved: disk full.")),
            publishFeedback: feedback.Add,
            reportSaveFailure: failures.Add,
            debounceDelay: TimeSpan.FromMilliseconds(20));

        controller.ApplyLiveTuning(currentProfile with { Name = "Broken save" }, hapticsStarted: true);

        await Task.Delay(100);

        Assert.Single(feedback);
        Assert.Equal("Profile could not be saved: disk full.", feedback[0].FooterStatusText);
        Assert.Equal("Profile could not be saved: disk full.", feedback[0].ProfileStatusMessage);
        Assert.Equal(["Profile could not be saved: disk full."], failures);
    }

    [Fact]
    public async Task ProfileCallbacks_AreDispatcherSafe()
    {
        var currentProfile = HapticDriveProfile.Default;
        var livePreviewCount = 0;
        var controlTextCount = 0;
        var dispatcher = new FakeMainWindowUiDispatcher(hasAccess: false);

        await using var controller = CreateController(
            currentProfileAccessor: () => currentProfile,
            replaceCurrentProfile: profile => currentProfile = profile,
            applyLivePreview: _ => livePreviewCount++,
            updateControlText: _ => controlTextCount++,
            dispatcher: dispatcher,
            debounceDelay: TimeSpan.FromMilliseconds(20));

        controller.ApplyLiveTuning(currentProfile with { Name = "Queued update" }, hapticsStarted: false);

        Assert.Equal(0, livePreviewCount);
        Assert.Equal(0, controlTextCount);
        Assert.Equal(HapticDriveProfile.Default.Name, currentProfile.Name);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.RunNextOnUiThread();

        Assert.Equal(1, livePreviewCount);
        Assert.Equal(1, controlTextCount);
        Assert.Equal("Queued update", currentProfile.Name);
    }

    private static ProfileTuningController CreateController(
        EffectSettingsListViewModel? effectSettingsViewModel = null,
        Func<HapticDriveProfile>? currentProfileAccessor = null,
        Func<bool>? hapticsStartedAccessor = null,
        Action<HapticDriveProfile>? replaceCurrentProfile = null,
        Action<HapticDriveProfile>? applyLivePreview = null,
        Action<HapticDriveProfile>? updateControlText = null,
        Func<HapticDriveProfile, CancellationToken, ValueTask<HapticProfileSaveResult>>? saveAsync = null,
        Action<AudioProfileWorkflowFeedback>? publishFeedback = null,
        Action<string>? reportSaveFailure = null,
        IMainWindowUiDispatcher? dispatcher = null,
        TimeSpan? debounceDelay = null)
    {
        return new ProfileTuningController(
            effectSettingsViewModel ?? new EffectSettingsListViewModel(),
            BuiltInHapticEffectRegistry.Instance,
            currentProfileAccessor ?? (() => HapticDriveProfile.Default),
            hapticsStartedAccessor ?? (() => false),
            replaceCurrentProfile ?? (_ => { }),
            applyLivePreview ?? (_ => { }),
            updateControlText ?? (_ => { }),
            saveAsync ?? ((_, _) => ValueTask.FromResult(HapticProfileSaveResult.Success("memory", wasRepaired: false, []))),
            publishFeedback ?? (_ => { }),
            reportSaveFailure ?? (_ => { }),
            dispatcher,
            debounceDelay: debounceDelay);
    }

    private sealed class FakeMainWindowUiDispatcher(bool hasAccess) : IMainWindowUiDispatcher
    {
        private readonly Queue<Action> _pending = [];
        private bool _hasAccess = hasAccess;

        public int PendingCount => _pending.Count;

        public bool CheckAccess()
        {
            return _hasAccess;
        }

        public void BeginInvoke(Action action)
        {
            _pending.Enqueue(action);
        }

        public ValueTask InvokeAsync(Action action)
        {
            var previous = _hasAccess;
            _hasAccess = true;
            try
            {
                action();
                return ValueTask.CompletedTask;
            }
            finally
            {
                _hasAccess = previous;
            }
        }

        public void RunNextOnUiThread()
        {
            var previous = _hasAccess;
            _hasAccess = true;
            try
            {
                _pending.Dequeue().Invoke();
            }
            finally
            {
                _hasAccess = previous;
            }
        }
    }
}
