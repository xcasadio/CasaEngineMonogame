#nullable enable

using System;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;

namespace CasaEngine.Editor.Controls.Cutscenes;

/// <summary>
/// Calls a commit callback when the user presses Enter in a text box or when the box loses the keyboard focus. The callback
/// receives true for Enter. It listens until <see cref="Detach"/>: the owner of a field must detach it when the field is
/// removed, since the focus event belongs to the desktop, which outlives the field.
/// </summary>
internal sealed class CutsceneTextCommitBinding
{
    private readonly MGWindow _window;
    private readonly MGTextBox _textBox;
    private readonly Action<bool> _commit;
    private bool _isAttached;

    public CutsceneTextCommitBinding(MGWindow window, MGTextBox textBox, Action<bool> commit)
    {
        _window = window;
        _textBox = textBox;
        _commit = commit;

        _textBox.KeyboardHandler.Pressed += OnKeyPressed;
        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged += OnFocusedKeyboardHandlerChanged;
        }

        _isAttached = true;
    }

    public void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _isAttached = false;
        _textBox.KeyboardHandler.Pressed -= OnKeyPressed;
        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged -= OnFocusedKeyboardHandlerChanged;
        }
    }

    private void OnKeyPressed(object? sender, BaseKeyPressedEventArgs args)
    {
        // The text box handles every key of its own handler, which runs first: only a key handled by another element is skipped.
        if ((args.IsHandled && !ReferenceEquals(args.HandledBy, _textBox)) || args.Key != Microsoft.Xna.Framework.Input.Keys.Enter)
        {
            return;
        }

        _commit(true);
        args.SetHandledBy(_textBox, false);
    }

    private void OnFocusedKeyboardHandlerChanged(object? sender, EventArgs<MGElement> args)
    {
        if (ReferenceEquals(args.PreviousValue, _textBox) && !ReferenceEquals(args.NewValue, _textBox))
        {
            _commit(false);
        }
    }
}
