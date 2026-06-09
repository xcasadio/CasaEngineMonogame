using System;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.ContentBrowser.Controls;

public sealed class ContentContextMenu
{
    private readonly MGWindow _window;

    public ContentContextMenu(MGWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public MGContextMenu CreateTreeMenu(ContentItem currentFolder, bool hasClipboardItems,
        Action onOpen,
        Action onNewFolder,
        Action onRename,
        Action onCopy,
        Action onCut,
        Action onCopyPath,
        Action onPaste,
        Action onDelete)
    {
        var menu = new MGContextMenu(_window, null);

        if (currentFolder != null)
        {
            AddButton(menu, "Open", EditorIcons.FolderOpen, onOpen);
        }

        AddButton(menu, "New Folder", EditorIcons.Folder, onNewFolder);

        if (currentFolder?.Parent != null)
        {
            AddButton(menu, "Rename", EditorIcons.Pencil, onRename);
            AddButton(menu, "Copy", EditorIcons.Copy, onCopy);
            AddButton(menu, "Cut", EditorIcons.Scissors, onCut);
        }

        menu.AddSeparator();
        AddButton(menu, "Copy Path", EditorIcons.Copy, onCopyPath);
        if (hasClipboardItems)
        {
            AddButton(menu, "Paste", EditorIcons.Paste, onPaste);
        }

        if (currentFolder?.Parent != null)
        {
            menu.AddSeparator();
            AddButton(menu, "Delete", EditorIcons.Trash, onDelete);
        }

        return menu;
    }

    public MGContextMenu CreateContentMenu(ContentItem selectedItem, bool hasClipboardItems,
        Action onNewFolder,
        Action onImport,
        Action onRefresh,
        Action onPaste,
        Action onOpen = null,
        Action onRename = null,
        Action onDuplicate = null,
        Action onCopy = null,
        Action onCut = null,
        Action onCopyPath = null,
        Action onShowInExplorer = null,
        Action onProperties = null,
        Action onDelete = null)
    {
        var menu = new MGContextMenu(_window, null);

        if (selectedItem != null)
        {
            AddButton(menu, "Open", selectedItem.IsDirectory ? EditorIcons.FolderOpen : EditorIcons.FilePlus, onOpen);
            AddButton(menu, "Rename", EditorIcons.Pencil, onRename);
            AddButton(menu, "Duplicate", EditorIcons.CopyPlus ?? EditorIcons.Copy, onDuplicate);
            AddButton(menu, "Copy", EditorIcons.Copy, onCopy);
            AddButton(menu, "Cut", EditorIcons.Scissors, onCut);
            menu.AddSeparator();
            AddButton(menu, "Copy Path", EditorIcons.Copy, onCopyPath);
            AddButton(menu, "Show in Explorer", EditorIcons.FolderOpen, onShowInExplorer);
            AddButton(menu, "Properties", EditorIcons.Info, onProperties);
            menu.AddSeparator();
            AddButton(menu, "Delete", EditorIcons.Trash, onDelete);
            return menu;
        }

        AddButton(menu, "New Folder", EditorIcons.Folder, onNewFolder);
        AddButton(menu, "Import File...", EditorIcons.FilePlus, onImport);
        menu.AddSeparator();
        AddButton(menu, "Refresh", EditorIcons.RefreshCw, onRefresh);
        if (hasClipboardItems)
        {
            AddButton(menu, "Paste", EditorIcons.Paste, onPaste);
        }

        return menu;
    }

    private void AddButton(MGContextMenu menu, string text, Texture2D icon, Action action)
    {
        if (action == null)
        {
            return;
        }

        var button = menu.AddButton(text, _ => action());
        if (icon != null)
        {
            button.Icon = new MGImage(menu, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 14,
                PreferredHeight = 14,
            };
        }
    }
}