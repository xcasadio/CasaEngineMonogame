using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Project;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A two-pane content browser panel that shows a folder tree on the left and
/// an asset list on the right. Subscribes to <see cref="AssetCatalog"/> events
/// and <see cref="ProjectSettingsHelper.ProjectLoaded"/> so it stays in sync.
/// </summary>
public class ContentBrowserPanel
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inner folder model
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class FolderNode
    {
        public string Name { get; }
        public FolderNode? Parent { get; }
        public List<FolderNode> SubFolders { get; } = new();
        public List<AssetInfo> Assets { get; } = new();

        public FolderNode(string name, FolderNode? parent)
        {
            Name = name;
            Parent = parent;
        }

        /// <summary>Gets or creates a direct child folder with the given name.</summary>
        public FolderNode GetOrCreateChild(string folderName)
        {
            var existing = SubFolders.FirstOrDefault(f =>
                string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            var child = new FolderNode(folderName, this);
            SubFolders.Add(child);
            return child;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────────────

    private readonly MGWindow _window;

    private MGTreeView _treeView = null!;
    private MGListBox<AssetInfo> _assetList = null!;
    private FolderNode _root = new("All", null);

    /// <summary>Maps each tree view item back to the folder node it represents.</summary>
    private readonly Dictionary<MGTreeViewItem, FolderNode> _itemToFolder = new();

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public ContentBrowserPanel(MGWindow window)
    {
        _window = window;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and returns the root MGUI element for this panel.
    /// Intended to be used as a <c>DockPanelNode.ContentFactory</c> result.
    /// </summary>
    public MGElement CreateContent()
    {
        // ── Toolbar ──────────────────────────────────────────────────────────
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 2, 4, 2),
            Spacing = 4,
        };

        var titleLabel = new MGTextBlock(_window, "[b]Content Browser[/b]")
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        var saveButton = new MGButton(_window, _ => OnSaveClicked())
        {
            Padding = new Thickness(6, 2, 6, 2),
        };
        saveButton.SetContent(new MGTextBlock(_window, "💾 Save"));

        toolbar.TryAddChild(titleLabel);
        toolbar.TryAddChild(saveButton);

        // ── Tree view (left pane) ─────────────────────────────────────────
        _treeView = new MGTreeView(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _treeView.SelectionChanged += OnFolderSelectionChanged;

        // Right-click context menu on tree view
        _treeView.MouseHandler.RMBReleasedInside += OnTreeViewRightClick;

        var treeScroll = new MGScrollViewer(_window);
        treeScroll.SetContent(_treeView);

        // ── Asset list (right pane) ───────────────────────────────────────
        _assetList = new MGListBox<AssetInfo>(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ItemTemplate = BuildAssetItemTemplate,
        };

        // Right-click context menu on asset list
        _assetList.MouseHandler.RMBReleasedInside += OnAssetListRightClick;

        var listScroll = new MGScrollViewer(_window);
        listScroll.SetContent(_assetList);

        // ── Grid splitter ─────────────────────────────────────────────────
        var splitter = new MGGridSplitter(_window);

        // ── Content grid: [tree | splitter | list] ────────────────────────
        var contentGrid = new MGGrid(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        contentGrid.AddRow(GridLength.CreateWeightedLength(1));
        contentGrid.AddColumn(GridLength.CreateWeightedLength(1));
        contentGrid.AddColumn(GridLength.CreatePixelLength(splitter.Size));
        contentGrid.AddColumn(GridLength.CreateWeightedLength(2));

        contentGrid.TryAddChild(0, 0, treeScroll);
        contentGrid.TryAddChild(0, 1, splitter);
        contentGrid.TryAddChild(0, 2, listScroll);

        // ── Outer dock panel: toolbar on top, grid fills the rest ─────────
        var outerPanel = new MGDockPanel(_window);
        outerPanel.TryAddChild(toolbar, Dock.Top);
        outerPanel.TryAddChild(contentGrid, Dock.Top); // last child => fills

        // ── Subscribe to catalog / project events ─────────────────────────
        AssetCatalog.AssetAdded += OnAssetAdded;
        AssetCatalog.AssetRemoved += OnAssetRemoved;
        AssetCatalog.AssetRenamed += OnAssetRenamed;
        AssetCatalog.AssetCleared += OnAssetCleared;
        ProjectSettingsHelper.ProjectLoaded += OnProjectLoaded;

        // Initial population
        RebuildTree();

        return outerPanel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item template
    // ─────────────────────────────────────────────────────────────────────────

    private MGElement BuildAssetItemTemplate(AssetInfo assetInfo)
    {
        var ext = Path.GetExtension(assetInfo.FileName);
        var icon = string.IsNullOrEmpty(ext) ? "📁" : "📄";
        var label = new MGTextBlock(_window, $"{icon} {assetInfo.Name}{ext}")
        {
            Padding = new Thickness(4, 2, 4, 2),
        };
        return label;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Context menus
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTreeViewRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var menu = new MGContextMenu(_window);
        menu.AddButton("📁 New Folder", _ => OnNewFolderRequested());
        menu.AddButton("✏ Rename", _ => OnRenameFolderRequested());
        menu.AddSeparator();
        menu.AddButton("🗑 Delete", _ => OnDeleteFolderRequested());
        _treeView.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    private void OnAssetListRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var selected = _assetList.SelectedValue;
        if (selected == null) return;

        var menu = new MGContextMenu(_window);
        menu.AddButton("✏ Rename", _ => OnRenameAssetRequested(selected));
        menu.AddSeparator();
        menu.AddButton("🗑 Delete", _ => OnDeleteAssetRequested(selected));
        _assetList.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Action stubs (to be expanded in a future task)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnSaveClicked()
    {
        // TODO: persist any pending changes via AssetCatalog.Save()
        AssetCatalog.Save();
    }

    private void OnNewFolderRequested()
    {
        // TODO: prompt for name and create a folder on disk
    }

    private void OnRenameFolderRequested()
    {
        // TODO: prompt for new name and rename selected folder
    }

    private void OnDeleteFolderRequested()
    {
        // TODO: confirm and delete selected folder + its assets
    }

    private void OnRenameAssetRequested(AssetInfo asset)
    {
        // TODO: prompt for new name and call AssetCatalog.Rename
    }

    private void OnDeleteAssetRequested(AssetInfo asset)
    {
        // TODO: confirm and call AssetCatalog.Remove(asset.Id)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Catalog / project event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnProjectLoaded(object? sender, EventArgs e) => RebuildTree();

    private void OnAssetAdded(object? sender, AssetInfo e) => RebuildTree();

    private void OnAssetRemoved(object? sender, AssetInfo e) => RebuildTree();

    private void OnAssetRenamed(object? sender, EventArgs<AssetInfo, string> assetRenamedArgs) => RebuildTree();

    private void OnAssetCleared(object? sender, EventArgs e) => RebuildTree();

    // ─────────────────────────────────────────────────────────────────────────
    // Tree building
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the full folder tree from <see cref="AssetCatalog.AssetInfos"/>
    /// then refreshes both the tree view and the asset list.
    /// </summary>
    private void RebuildTree()
    {
        _root = new FolderNode("All", null);
        _itemToFolder.Clear();

        var projectPath = EngineEnvironment.ProjectPath ?? string.Empty;

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            // Strip project root to get a path relative to it
            var relativePath = assetInfo.FileName;
            if (!string.IsNullOrEmpty(projectPath) &&
                relativePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath[projectPath.Length..].TrimStart('\\', '/');
            }

            // Walk directory segments, creating FolderNodes as needed
            var dirPart = Path.GetDirectoryName(relativePath) ?? string.Empty;
            var segments = dirPart.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = _root;
            foreach (var seg in segments)
                current = current.GetOrCreateChild(seg);

            current.Assets.Add(assetInfo);
        }

        RefreshTreeView();
        RefreshAssetList();
    }

    /// <summary>Rebuilds the <see cref="_treeView"/> items from <see cref="_root"/>.</summary>
    private void RefreshTreeView()
    {
        _treeView.ClearItems();

        var rootItem = BuildTreeItem(_root);
        rootItem.IsExpanded = true;
        _treeView.AddItem(rootItem);
    }

    private MGTreeViewItem BuildTreeItem(FolderNode node)
    {
        var item = new MGTreeViewItem(_window)
        {
            Header = $"📁 {node.Name}",
            IsExpanded = false,
        };

        _itemToFolder[item] = node;

        foreach (var child in node.SubFolders)
            item.AddItem(BuildTreeItem(child));

        return item;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tree selection → asset list
    // ─────────────────────────────────────────────────────────────────────────

    private void OnFolderSelectionChanged(object? sender, MGTreeViewItem? item)
    {
        RefreshAssetList();
    }

    private void RefreshAssetList()
    {
        var selectedItem = _treeView?.SelectedItem;

        FolderNode displayFolder;
        if (selectedItem != null && _itemToFolder.TryGetValue(selectedItem, out var folder))
            displayFolder = folder;
        else
            displayFolder = _root;

        var assets = CollectAssets(displayFolder).ToList();
        _assetList.SetItemsSource(assets);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all assets within <paramref name="folder"/> and all descendants.</summary>
    private static IEnumerable<AssetInfo> CollectAssets(FolderNode folder)
    {
        foreach (var asset in folder.Assets)
            yield return asset;

        foreach (var sub in folder.SubFolders)
            foreach (var asset in CollectAssets(sub))
                yield return asset;
    }
}
