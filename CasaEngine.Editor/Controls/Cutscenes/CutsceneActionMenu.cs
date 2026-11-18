using System;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.Cutscenes;

/// <summary>
/// The context menu of the structure operations of a cutscene (P8), shared by the Hierarchy tree and the timeline. It is
/// built when it is opened, for the selected action of a document: an entry whose operation the document would refuse is not
/// added, and separators never follow each other and never end the menu. Without a selection it offers only Add and Paste,
/// which insert at the end of the root.
/// </summary>
internal static class CutsceneActionMenu
{
    /// <summary>
    /// Builds the menu of the selected action of <paramref name="document"/>, or returns null when no operation is allowed.
    /// </summary>
    /// <param name="onResult">Told the refusal message of an operation after it ran, or null when it was done.</param>
    internal static MGContextMenu? Create(MGWindow window, CutsceneAssetInspectorPanel document, Action<string?>? onResult)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(document);

        var menu = new MGContextMenu(window, null);
        int groupStart = 0;

        // Add, wrap and ungroup: the operations that build structure.
        if (document.GetAddRefusal() == null)
        {
            AddAddSubmenu(menu, document, onResult);
        }

        if (document.GetWrapRefusal(CutsceneActionTypes.Sequence) == null)
        {
            AddWrapSubmenu(menu, document, onResult);
        }

        if (document.GetUngroupRefusal() == null)
        {
            menu.AddButton("Ungroup", _ => Report(onResult, document.UngroupSelectedAction()));
        }

        groupStart = StartGroup(menu, groupStart);

        // Order inside the container.
        if (document.GetMoveUpRefusal() == null)
        {
            menu.AddButton("Move up", _ => Report(onResult, document.MoveSelectedActionUp()));
        }

        if (document.GetMoveDownRefusal() == null)
        {
            menu.AddButton("Move down", _ => Report(onResult, document.MoveSelectedActionDown()));
        }

        groupStart = StartGroup(menu, groupStart);

        // Clipboard and duplication.
        if (document.GetCutRefusal() == null)
        {
            menu.AddButton("Cut", _ => Report(onResult, document.CutSelectedAction()));
        }

        if (document.GetCopyRefusal() == null)
        {
            menu.AddButton("Copy", _ => Report(onResult, document.CopySelectedAction()));
        }

        if (document.GetPasteRefusal() == null)
        {
            menu.AddButton("Paste", _ => Report(onResult, document.PasteAction()));
        }

        if (document.GetDuplicateRefusal() == null)
        {
            menu.AddButton("Duplicate", _ => Report(onResult, document.DuplicateSelectedAction()));
        }

        groupStart = StartGroup(menu, groupStart);

        if (document.GetRemoveRefusal() == null)
        {
            menu.AddButton("Delete", _ => Report(onResult, document.RemoveSelectedAction()));
        }

        // The last group never adds a separator of its own: one that ends the menu is removed.
        TrimTrailingSeparator(menu);
        return menu.Items.Count == 0 ? null : menu;
    }

    // Closes a group with a separator when it added entries; an empty group adds none, so separators never follow each
    // other. Returns the item count after the separator, which is where the next group starts.
    private static int StartGroup(MGContextMenu menu, int previousStart)
    {
        int count = menu.Items.Count;
        if (count == previousStart)
        {
            return count;
        }

        menu.AddSeparator();
        return menu.Items.Count;
    }

    private static void TrimTrailingSeparator(MGContextMenu menu)
    {
        int last = menu.Items.Count - 1;
        if (last >= 0 && menu.Items[last] is MGContextMenuSeparator)
        {
            menu.Items.RemoveAt(last);
        }
    }

    private static void AddAddSubmenu(MGContextMenu menu, CutsceneAssetInspectorPanel document, Action<string?>? onResult)
    {
        var addButton = menu.AddButton("Add", _ => { });
        var addMenu = new MGContextMenu(menu);

        string? category = null;
        MGContextMenu? categoryMenu = null;
        var kinds = CutsceneActionCatalog.Kinds;
        for (int index = 0; index < kinds.Count; index++)
        {
            var kind = kinds[index];
            if (categoryMenu == null || !string.Equals(category, kind.Category, StringComparison.Ordinal))
            {
                category = kind.Category;
                var categoryButton = addMenu.AddButton(category, _ => { });
                categoryMenu = new MGContextMenu(addMenu);
                categoryButton.Submenu = categoryMenu;
            }

            string type = kind.Type;
            categoryMenu.AddButton(type, _ => Report(onResult, document.AddAction(type)));
        }

        addButton.Submenu = addMenu;
    }

    private static void AddWrapSubmenu(MGContextMenu menu, CutsceneAssetInspectorPanel document, Action<string?>? onResult)
    {
        var wrapButton = menu.AddButton("Wrap in", _ => { });
        var wrapMenu = new MGContextMenu(menu);
        AddWrapButton(wrapMenu, document, onResult, CutsceneActionTypes.Sequence);
        AddWrapButton(wrapMenu, document, onResult, CutsceneActionTypes.Parallel);
        AddWrapButton(wrapMenu, document, onResult, CutsceneActionTypes.Repeat);
        AddWrapButton(wrapMenu, document, onResult, CutsceneActionTypes.At);
        wrapButton.Submenu = wrapMenu;
    }

    private static void AddWrapButton(MGContextMenu menu, CutsceneAssetInspectorPanel document, Action<string?>? onResult, string type)
    {
        menu.AddButton(type, _ => Report(onResult, document.WrapSelectedAction(type)));
    }

    private static void Report(Action<string?>? onResult, string? refusal)
    {
        onResult?.Invoke(refusal);
    }
}
