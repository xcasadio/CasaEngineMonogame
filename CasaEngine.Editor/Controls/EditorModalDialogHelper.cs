using System;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

internal static class EditorModalDialogHelper
{
    public static MGWindow CreateCenteredModalWindow(MGWindow owner, int width, int height, string title)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var screenBounds = owner.Desktop.ValidScreenBounds;
        int left = screenBounds.Left + (screenBounds.Width - width) / 2;
        int top = screenBounds.Top + (screenBounds.Height - height) / 2;

        var dialog = new MGWindow(owner, left, top, width, height)
        {
            TitleText = title,
        };

        owner.PushModalWindow(dialog);
        return dialog;
    }
}