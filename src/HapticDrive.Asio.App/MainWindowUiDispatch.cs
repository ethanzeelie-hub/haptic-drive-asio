using System.Windows.Threading;

namespace HapticDrive.Asio.App;

internal interface IMainWindowUiDispatcher
{
    bool CheckAccess();

    void BeginInvoke(Action action);

    ValueTask InvokeAsync(Action action);
}

internal static class MainWindowUiDispatch
{
    public static bool BeginInvokeIfRequired(IMainWindowUiDispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.CheckAccess())
        {
            return false;
        }

        dispatcher.BeginInvoke(action);
        return true;
    }

    public static ValueTask InvokeAsync(IMainWindowUiDispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.CheckAccess())
        {
            action();
            return ValueTask.CompletedTask;
        }

        return dispatcher.InvokeAsync(action);
    }
}

internal sealed class WpfMainWindowUiDispatcher(Dispatcher dispatcher) : IMainWindowUiDispatcher
{
    public bool CheckAccess()
    {
        return dispatcher.CheckAccess();
    }

    public void BeginInvoke(Action action)
    {
        _ = dispatcher.BeginInvoke(action);
    }

    public ValueTask InvokeAsync(Action action)
    {
        return new ValueTask(dispatcher.InvokeAsync(action).Task);
    }
}
