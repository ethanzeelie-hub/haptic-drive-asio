namespace HapticDrive.Asio.App.Tests;

public sealed class MainWindowUiDispatchTests
{
    [Fact]
    public void OffDispatcherRefreshIsPostedWithoutTouchingUiInline()
    {
        var dispatcher = new FakeMainWindowUiDispatcher(hasAccess: false);
        var status = new FakeStatusTarget();

        var posted = MainWindowUiDispatch.BeginInvokeIfRequired(dispatcher, status.SetFromUiThread);

        Assert.True(posted);
        Assert.False(status.WasSet);
        Assert.Equal(1, dispatcher.PendingCount);
        dispatcher.RunNextOnUiThread();
        Assert.True(status.WasSet);
    }

    [Fact]
    public void DirectStatusRefreshPatternReturnsAfterPostingWhenCalledOffDispatcher()
    {
        var dispatcher = new FakeMainWindowUiDispatcher(hasAccess: false);
        var status = new FakeStatusTarget();

        void UpdateRealPhprDirectControlStatusPattern()
        {
            if (MainWindowUiDispatch.BeginInvokeIfRequired(dispatcher, status.SetFromUiThread))
            {
                return;
            }

            status.SetFromUiThread();
        }

        UpdateRealPhprDirectControlStatusPattern();

        Assert.False(status.WasSet);
        Assert.Equal(1, dispatcher.PendingCount);
        dispatcher.RunNextOnUiThread();
        Assert.True(status.WasSet);
    }

    [Fact]
    public async Task AwaitedDispatcherPostReturnsUiExceptionsToPaddleEventHandler()
    {
        var dispatcher = new FakeMainWindowUiDispatcher(hasAccess: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await MainWindowUiDispatch.InvokeAsync(
                dispatcher,
                () => throw new InvalidOperationException("simulated WPF setter failure")).AsTask());

        Assert.Equal("simulated WPF setter failure", ex.Message);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    private sealed class FakeStatusTarget
    {
        public bool WasSet { get; private set; }

        public void SetFromUiThread()
        {
            WasSet = true;
        }
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
            try
            {
                var previous = _hasAccess;
                _hasAccess = true;
                try
                {
                    action();
                }
                finally
                {
                    _hasAccess = previous;
                }

                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
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
