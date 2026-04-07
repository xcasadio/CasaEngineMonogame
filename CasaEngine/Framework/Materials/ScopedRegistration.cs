namespace CasaEngine.Framework.Materials;

internal sealed class ScopedRegistration : IDisposable
{
    private Action? _disposeAction;

    public ScopedRegistration(Action disposeAction)
    {
        ArgumentNullException.ThrowIfNull(disposeAction);
        _disposeAction = disposeAction;
    }

    public void Dispose()
    {
        var disposeAction = System.Threading.Interlocked.Exchange(ref _disposeAction, null);
        disposeAction?.Invoke();
    }
}