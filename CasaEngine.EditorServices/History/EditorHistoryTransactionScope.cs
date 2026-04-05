namespace CasaEngine.EditorServices.History;

/// <summary>
/// RAII helper for editor history transactions.
/// </summary>
public sealed class EditorHistoryTransactionScope : IDisposable
{
    private readonly EditorHistoryStack _stack;
    private bool _completed;

    internal EditorHistoryTransactionScope(EditorHistoryStack stack)
    {
        _stack = stack;
    }

    public void Commit(string? description = null)
    {
        if (_completed)
        {
            return;
        }

        _stack.CommitTransaction(description);
        _completed = true;
    }

    public void Cancel()
    {
        if (_completed)
        {
            return;
        }

        _stack.CancelTransaction();
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _stack.CancelTransaction();
            _completed = true;
        }
    }
}