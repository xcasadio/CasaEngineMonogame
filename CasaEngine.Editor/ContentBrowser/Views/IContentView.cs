using System;
using System.Collections.Generic;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.ContentBrowser.Views;

public interface IContentView
{
    MGElement RootElement { get; }

    IReadOnlyList<ContentItem> SelectedItems { get; }

    event Action<IReadOnlyList<ContentItem>>? SelectionChanged;
    event Action<ContentItem>? FileDoubleClicked;
    event Action<ContentItem>? DirectoryDoubleClicked;

    void SetItems(IReadOnlyList<ContentItem> items);
    void ClearSelection();
    void RestoreSelection(IReadOnlyList<ContentItem> items);
    bool TryGetPrimarySelectionBounds(out Rectangle bounds);
}