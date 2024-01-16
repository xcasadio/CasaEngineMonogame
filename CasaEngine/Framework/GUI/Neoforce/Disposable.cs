namespace TomShane.Neoforce.Controls;

public abstract class Disposable : Unknown, IDisposable
{
    public static int Count { get; private set; }

    protected Disposable()
    {
        Count += 1;
    }

    ~Disposable()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Count -= 1;
        }
    }
}