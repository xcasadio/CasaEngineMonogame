using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.ContentBrowser.Views;

public interface IContentView
{
    MGElement RootElement { get; }

    /// <summary>Element that holds the keyboard focus for this view. Keyboard events are only delivered
    /// to the focused element, so panel shortcuts must be subscribed on it and focus restored to it.</summary>
    MGElement KeyboardFocusElement { get; }

    IReadOnlyList<ContentItem> SelectedItems { get; }

    event Action<IReadOnlyList<ContentItem>> SelectionChanged;
    event Action<ContentItem> FileDoubleClicked;
    event Action<ContentItem> DirectoryDoubleClicked;

    void SetItems(IReadOnlyList<ContentItem> items);
    void ClearSelection();
    void RestoreSelection(IReadOnlyList<ContentItem> items);
    bool TryGetPrimarySelectionBounds(out Rectangle bounds);
}