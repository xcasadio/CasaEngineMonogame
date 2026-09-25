using System;
using System.Collections.Generic;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

/// <summary>The answer to <see cref="EditorMessageBoxes.AskSave"/>.</summary>
internal enum EditorSaveAnswer
{
    Save,
    DontSave,
    Cancel,
}

/// <summary>
/// The editor's questions and messages, shown as MGUI message boxes over the whole editor desktop (ADR-0041,
/// <see cref="MGMessageBox"/>): one at a time, in the order they were asked. An answer arrives in a callback, never on
/// the line after the call.
/// </summary>
internal sealed class EditorMessageBoxes
{
    private static readonly string[] OkLabels = ["OK"];
    private static readonly string[] YesNoLabels = ["Yes", "No"];
    private static readonly string[] SaveLabels = ["Save", "Don't Save", "Cancel"];

    private readonly EditorMessageBoxQueue _queue;

    public EditorMessageBoxes(MGDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        _queue = new EditorMessageBoxQueue((request, answered) => MGMessageBox.Show(
            desktop,
            request.Title,
            request.Message,
            request.ButtonLabels,
            request.DefaultButtonIndex,
            request.CancelButtonIndex,
            answered));
    }

    /// <summary>True while a question is shown or waiting to be shown.</summary>
    public bool HasPendingOrOpen => _queue.HasPendingOrOpen;

    /// <summary>Queues a question with 1 to 3 buttons; <paramref name="answered"/> receives the chosen button's index.</summary>
    public void Show(string title, string message, IReadOnlyList<string> buttonLabels, int defaultButtonIndex, int cancelButtonIndex,
        Action<int> answered)
        => _queue.Enqueue(new EditorMessageBoxQueue.Request(title, message, buttonLabels, defaultButtonIndex, cancelButtonIndex, answered));

    /// <summary>Queues a message with a single "OK" button (errors, warnings and information alike: no icon, D9).</summary>
    public void ShowMessage(string title, string message, Action closed = null)
        => Show(title, message, OkLabels, 0, 0, closed == null ? null : _ => closed());

    /// <summary>Queues a "Yes" / "No" question; Enter answers Yes, Escape answers No.</summary>
    public void AskYesNo(string title, string message, Action<bool> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);
        Show(title, message, YesNoLabels, 0, 1, index => answered(index == 0));
    }

    /// <summary>Queues a "Save" / "Don't Save" / "Cancel" question (D6); Enter answers Save, Escape answers Cancel.</summary>
    public void AskSave(string title, string message, Action<EditorSaveAnswer> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);
        Show(title, message, SaveLabels, 0, 2, index => answered((EditorSaveAnswer)index));
    }
}
