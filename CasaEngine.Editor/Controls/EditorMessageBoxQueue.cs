using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The ordering behind <see cref="EditorMessageBoxes"/> (ADR-0039): one question shown at a time, the next one shown when
/// the previous one is answered, in the order they were asked. Pure: the presenter that actually shows a box is injected,
/// so the ordering is tested without MGUI.
/// </summary>
internal sealed class EditorMessageBoxQueue
{
    internal readonly record struct Request(
        string Title,
        string Message,
        IReadOnlyList<string> ButtonLabels,
        int DefaultButtonIndex,
        int CancelButtonIndex,
        Action<int> Answered);

    private readonly Queue<Request> _pending = new();
    private readonly Action<Request, Action<int>> _present;
    private bool _isOpen;

    /// <param name="present">Shows one request and calls the given callback with the chosen button index once it is
    /// answered. Called again only after that callback ran.</param>
    public EditorMessageBoxQueue(Action<Request, Action<int>> present)
    {
        _present = present ?? throw new ArgumentNullException(nameof(present));
    }

    /// <summary>True while a question is shown or waiting to be shown.</summary>
    public bool HasPendingOrOpen => _isOpen || _pending.Count > 0;

    /// <summary>Queues <paramref name="request"/>; it is shown at once when nothing else is.</summary>
    /// <exception cref="ArgumentException">The labels are missing, there are none or more than three, or an index does
    /// not name a label.</exception>
    public void Enqueue(Request request)
    {
        if (request.ButtonLabels == null || request.ButtonLabels.Count == 0 || request.ButtonLabels.Count > 3)
        {
            throw new ArgumentException("A question needs one to three button labels.", nameof(request));
        }

        if (request.DefaultButtonIndex < 0 || request.DefaultButtonIndex >= request.ButtonLabels.Count
            || request.CancelButtonIndex < 0 || request.CancelButtonIndex >= request.ButtonLabels.Count)
        {
            throw new ArgumentException("The default and cancel button indices must name a label.", nameof(request));
        }

        _pending.Enqueue(request);
        if (!_isOpen)
        {
            ShowNext();
        }
    }

    private void ShowNext()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        Request request = _pending.Dequeue();
        _isOpen = true;
        bool answered = false;
        try
        {
            _present(request, index =>
            {
                if (answered)
                {
                    return;
                }

                answered = true;
                Complete(request, index);
            });
        }
        catch
        {
            //  A presenter that failed shows nothing: without this the queue would wait forever for an answer.
            _isOpen = false;
            throw;
        }
    }

    private void Complete(Request request, int index)
    {
        _isOpen = false;
        try
        {
            request.Answered?.Invoke(index);
        }
        finally
        {
            //  The answer may have asked a new question (then shown already, after the older ones): only show the next one
            //  if nothing is open yet.
            if (!_isOpen)
            {
                ShowNext();
            }
        }
    }
}
