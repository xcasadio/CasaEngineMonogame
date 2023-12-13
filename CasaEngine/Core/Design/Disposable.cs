namespace CasaEngine.Core.Design;

public abstract class Disposable : IDisposable
{
    private bool _disposed;

    public bool IsDisposed => _disposed;

    ~Disposable()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        // This object will be cleaned up by the Dispose method.
        // Therefore, you should call GC.SupressFinalize to
        // take this object off the finalization queue
        // and prevent finalization code for this object
        // from executing a second time.
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisposeManagedResources();
            }

            DisposeUnmanagedResources();

            _disposed = true;
        }
    }

    protected virtual void DisposeManagedResources()
    {
    }

    protected virtual void DisposeUnmanagedResources()
    {
    }
}
