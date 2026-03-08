using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Project;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework.Graphics;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A two-pane content browser panel.
/// Left pane  : folder tree  (<see cref="MGTreeView"/>).
/// Right pane : file list    (<see cref="MGListBox{T}"/>).
/// The data model is <see cref="ContentItem"/> (file-system based) and the
/// tree is populated by <see cref="FileSystemScanner"/>.
/// </summary>
public class ContentBrowserPanel
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a file is selected (single click).</summary>
    public event Action<ContentItem>? FileSelected;

    /// <summary>Raised when a file is opened (double-click).</summary>
    public event Action<ContentItem>? FileOpened;

    /// <summary>Raised when a file or folder is deleted.</summary>
    public event Action<ContentItem>? FileDeleted;

    /// <summary>Raised when a file or folder is renamed (item, old name).</summary>
    public event Action<ContentItem, string>? FileRenamed;

    /// <summary>Raised when the selection set changes.</summary>
    public event Action<IReadOnlyList<ContentItem>>? SelectionChanged;

    // ─────────────────────────────────────────────────────────────────────────
    //  Fields
    // ─────────────────────────────────────────────────────────────────────────

    private readonly MGWindow _window;

    // UI controls
    private MGTreeView _treeView = null!;
    private MGListBox<ContentItem> _assetList = null!;
    private MGStackPanel _breadcrumbBar = null!;
    private MGTextBox _searchBox = null!;
    private MGButton _btnBack = null!;
    private MGButton _btnForward = null!;
    private MGButton _btnParent = null!;

    // Data model
    private ContentItem? _rootItem;
    private ContentItem? _currentFolder;

    /// <summary>Maps each tree-view item → ContentItem (folder).</summary>
    private readonly Dictionary<MGTreeViewItem, ContentItem> _itemToFolder = new();

    // Navigation history
    private readonly Stack<ContentItem> _backHistory = new();
    private readonly Stack<ContentItem> _forwardHistory = new();

    // Search filter
    private string _searchFilter = string.Empty;

    // Double-click tracking
    private DateTime _lastClickTime;
    private ContentItem? _lastClickedItem;
    private const double DoubleClickMs = 400;

    // ─────────────────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public ContentBrowserPanel(MGWindow window)
    {
        _window = window;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and returns the root MGUI element for this panel.
    /// Intended to be used as a <c>DockPanelNode.ContentFactory</c> result.
    /// </summary>
    public MGElement CreateContent()
    {
        // ────────────────────────────────────────
        //  Toolbar row
        // ────────────────────────────────────────
        var toolbar = BuildToolbar();

        // ────────────────────────────────────────
        //  Tree view (left pane)
        // ────────────────────────────────────────
        _treeView = new MGTreeView(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _treeView.SelectionChanged += OnFolderSelectionChanged;
        _treeView.MouseHandler.RMBReleasedInside += OnTreeViewRightClick;

        var treeScroll = new MGScrollViewer(_window);
        treeScroll.SetContent(_treeView);

        // ────────────────────────────────────────
        //  File list (right pane)
        // ────────────────────────────────────────
        _assetList = new MGListBox<ContentItem>(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ItemTemplate = BuildFileItemTemplate,
        };
        _assetList.MouseHandler.RMBReleasedInside += OnAssetListRightClick;
        _assetList.SelectionChanged += OnAssetSelectionChanged;

        // ────────────────────────────────────────
        //  Grid: [tree | splitter | list]
        // ────────────────────────────────────────
        var splitter = new MGGridSplitter(_window);
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
        contentGrid.TryAddChild(0, 2, _assetList);

        // ────────────────────────────────────────
        //  Outer dock: toolbar on top, content fills
        // ────────────────────────────────────────
        var outerPanel = new MGDockPanel(_window);
        outerPanel.TryAddChild(toolbar, Dock.Top);
        outerPanel.TryAddChild(contentGrid, Dock.Top); // last child fills

        // ────────────────────────────────────────
        //  Events
        // ────────────────────────────────────────
        AssetCatalog.AssetAdded += OnAssetAdded;
        AssetCatalog.AssetRemoved += OnAssetRemoved;
        AssetCatalog.AssetRenamed += OnAssetRenamed;
        AssetCatalog.AssetCleared += OnAssetCleared;
        ProjectSettingsHelper.ProjectLoaded += OnProjectLoaded;

        // ── Initial population ───────────────────────────────────────────
        RebuildTree();

        return outerPanel;
    }

    /// <summary>Forces a complete rescan from disk.</summary>
    public void Refresh()
    {
        RebuildTree();
    }

    /// <summary>Navigates to the given folder, updating history.</summary>
    public void NavigateTo(ContentItem folder)
    {
        if (folder == null || !folder.IsDirectory)
            return;

        if (_currentFolder != null && _currentFolder != folder)
        {
            _backHistory.Push(_currentFolder);
            _forwardHistory.Clear();
        }

        _currentFolder = folder;
        UpdateBreadcrumb();
        RefreshAssetList();
        UpdateNavButtons();

        // Try to select matching tree node
        SelectTreeNode(folder);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Toolbar
    // ─────────────────────────────────────────────────────────────────────────

    private MGElement BuildToolbar()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Margin = new Thickness(4, 2, 4, 2),
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // ◄ Back
        _btnBack = MakeIconButton(EditorIcons.Undo, "Back", GoBack);
        toolbar.TryAddChild(_btnBack);

        // ► Forward
        _btnForward = MakeIconButton(EditorIcons.Redo, "Forward", GoForward);
        toolbar.TryAddChild(_btnForward);

        // ↑ Parent
        _btnParent = MakeIconButton(EditorIcons.FolderOpen, "Parent folder", GoUp);
        toolbar.TryAddChild(_btnParent);

        // Refresh
        toolbar.TryAddChild(MakeIconButton(EditorIcons.RefreshCw, "Refresh", () => Refresh()));

        // ── Separator ──
        toolbar.TryAddChild(new MGSeparator(_window, Orientation.Vertical)
        {
            Margin = new Thickness(4, 0, 4, 0),
            PreferredHeight = 20,
        });

        // ── Breadcrumb ──
        _breadcrumbBar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toolbar.TryAddChild(_breadcrumbBar);

        // ── Spacer (push search to the right) ──
        toolbar.TryAddChild(new MGTextBlock(_window, string.Empty)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 20,
        });

        // ── Search box ──
        _searchBox = new MGTextBox(_window, CharacterLimit: 200)
        {
            PlaceholderText = "Search...",
            MinWidth = 140,
            MaxWidth = 250,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _searchBox.TextChanged += OnSearchTextChanged;

        // Search icon + box
        if (EditorIcons.Search != null)
        {
            toolbar.TryAddChild(new MGImage(_window, EditorIcons.Search, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16, PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        toolbar.TryAddChild(_searchBox);

        return toolbar;
    }

    private MGButton MakeIconButton(Texture2D? icon, string tooltip, Action action)
    {
        var btn = new MGButton(_window, _ => action())
        {
            Padding = new Thickness(2, 2, 2, 2),
        };

        if (icon != null)
        {
            btn.SetContent(new MGImage(_window, icon, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 18,
                PreferredHeight = 18,
            });
        }
        else
        {
            btn.SetContent(new MGTextBlock(_window, tooltip));
        }

        return btn;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Breadcrumb
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateBreadcrumb()
    {
        _breadcrumbBar.TryRemoveAll();

        if (_currentFolder == null || _rootItem == null)
            return;

        // Build path segments from root → current
        var segments = new List<ContentItem>();
        var node = _currentFolder;
        while (node != null)
        {
            segments.Add(node);
            node = node.Parent;
        }
        segments.Reverse();

        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];

            if (i > 0)
            {
                _breadcrumbBar.TryAddChild(new MGTextBlock(_window, ">")
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                });
            }

            var breadcrumbBtn = new MGButton(_window, _ => NavigateTo(seg))
            {
                Padding = new Thickness(4, 1, 4, 1),
            };
            breadcrumbBtn.SetContent(new MGTextBlock(_window, seg.Name)
            {
                VerticalAlignment = VerticalAlignment.Center,
            });
            _breadcrumbBar.TryAddChild(breadcrumbBtn);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Navigation
    // ─────────────────────────────────────────────────────────────────────────

    private void GoBack()
    {
        if (_backHistory.Count == 0) return;
        if (_currentFolder != null)
            _forwardHistory.Push(_currentFolder);
        _currentFolder = _backHistory.Pop();
        UpdateBreadcrumb();
        RefreshAssetList();
        UpdateNavButtons();
        SelectTreeNode(_currentFolder);
    }

    private void GoForward()
    {
        if (_forwardHistory.Count == 0) return;
        if (_currentFolder != null)
            _backHistory.Push(_currentFolder);
        _currentFolder = _forwardHistory.Pop();
        UpdateBreadcrumb();
        RefreshAssetList();
        UpdateNavButtons();
        SelectTreeNode(_currentFolder);
    }

    private void GoUp()
    {
        if (_currentFolder?.Parent == null) return;
        NavigateTo(_currentFolder.Parent);
    }

    private void UpdateNavButtons()
    {
        _btnBack.IsEnabled = _backHistory.Count > 0;
        _btnForward.IsEnabled = _forwardHistory.Count > 0;
        _btnParent.IsEnabled = _currentFolder?.Parent != null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  File item template
    // ─────────────────────────────────────────────────────────────────────────

    private MGElement BuildFileItemTemplate(ContentItem item)
    {
        var iconTex = GetIconForType(item.Type);

        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Padding = new Thickness(4, 2, 4, 2),
            Spacing = 6,
        };

        if (iconTex != null)
        {
            row.TryAddChild(new MGImage(_window, iconTex, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        row.TryAddChild(new MGTextBlock(_window, item.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        return row;
    }

    /// <summary>Returns the best icon <see cref="Texture2D"/> for the given type.</summary>
    private static Texture2D? GetIconForType(ContentItemType type) => type switch
    {
        ContentItemType.Folder    => EditorIcons.Folder,
        ContentItemType.Texture   => EditorIcons.Image,
        ContentItemType.Model     => EditorIcons.Box,
        ContentItemType.Sound     => EditorIcons.Volume,
        ContentItemType.Script    => EditorIcons.FileCode,
        ContentItemType.Scene     => EditorIcons.Clapperboard,
        ContentItemType.Shader    => EditorIcons.Settings,
        ContentItemType.Font      => EditorIcons.Square,
        ContentItemType.Material  => EditorIcons.Palette,
        ContentItemType.Prefab    => EditorIcons.Package,
        ContentItemType.Animation => EditorIcons.Clapperboard,
        ContentItemType.World     => EditorIcons.Layers,
        _                         => EditorIcons.FilePlus,
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  Context menus
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTreeViewRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var menu = new MGContextMenu(_window);
        menu.AddButton("Open",       _ => OnOpenFolderRequested());
        menu.AddButton("New Folder", _ => OnNewFolderRequested());
        menu.AddButton("Rename",     _ => OnRenameFolderRequested());
        menu.AddSeparator();
        menu.AddButton("Copy Path",  _ => OnCopyPathRequested(_currentFolder));
        menu.AddSeparator();
        menu.AddButton("Delete",     _ => OnDeleteFolderRequested());
        _treeView.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    private void OnAssetListRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var selected = _assetList.SelectedValue;

        var menu = new MGContextMenu(_window);

        if (selected != null)
        {
            if (selected.IsDirectory)
            {
                menu.AddButton("Open",       _ => NavigateTo(selected));
            }
            else
            {
                menu.AddButton("Open",       _ => FileOpened?.Invoke(selected));
            }
            menu.AddButton("Rename",     _ => OnRenameItemRequested(selected));
            menu.AddButton("Duplicate",  _ => OnDuplicateRequested(selected));
            menu.AddSeparator();
            menu.AddButton("Copy Path",  _ => OnCopyPathRequested(selected));
            menu.AddButton("Show in Explorer", _ => OnShowInExplorer(selected));
            menu.AddSeparator();
            menu.AddButton("Delete",     _ => OnDeleteItemRequested(selected));
        }
        else
        {
            // Background right-click (no item selected)
            menu.AddButton("New Folder", _ => OnNewFolderRequested());
            menu.AddSeparator();
            menu.AddButton("Refresh",    _ => Refresh());
        }

        _assetList.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Action handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnOpenFolderRequested()
    {
        if (_currentFolder == null) return;
        // Already navigated via tree selection — nothing extra to do
    }

    private void OnNewFolderRequested()
    {
        if (_currentFolder == null) return;
        var newPath = Path.Combine(_currentFolder.FullPath, "New Folder");
        var suffix = 1;
        while (Directory.Exists(newPath))
        {
            newPath = Path.Combine(_currentFolder.FullPath, $"New Folder ({suffix++})");
        }
        try
        {
            Directory.CreateDirectory(newPath);
            FileSystemScanner.Refresh(_currentFolder);
            RefreshTreeView();
            RefreshAssetList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentBrowser] Create folder failed: {ex.Message}");
        }
    }

    private void OnRenameFolderRequested()
    {
        // Rename the currently-selected folder (tree selection)
        var selectedItem = _treeView?.SelectedItem;
        if (selectedItem != null && _itemToFolder.TryGetValue(selectedItem, out var folder))
        {
            OnRenameItemRequested(folder);
        }
    }

    private void OnDeleteFolderRequested()
    {
        var selectedItem = _treeView?.SelectedItem;
        if (selectedItem != null && _itemToFolder.TryGetValue(selectedItem, out var folder))
        {
            OnDeleteItemRequested(folder);
        }
    }

    private void OnRenameItemRequested(ContentItem item)
    {
        // TODO: implement inline rename overlay (Task 9)
        // For now, just log
        Debug.WriteLine($"[ContentBrowser] Rename requested: {item.FullPath}");
    }

    private void OnDeleteItemRequested(ContentItem item)
    {
        try
        {
            if (item.IsDirectory)
            {
                if (Directory.Exists(item.FullPath))
                    Directory.Delete(item.FullPath, recursive: true);
            }
            else
            {
                if (File.Exists(item.FullPath))
                    File.Delete(item.FullPath);
            }

            // If we just deleted the current folder, go up
            if (_currentFolder == item)
            {
                _currentFolder = item.Parent ?? _rootItem;
            }

            FileDeleted?.Invoke(item);
            RebuildTree();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentBrowser] Delete failed: {ex.Message}");
        }
    }

    private void OnDuplicateRequested(ContentItem item)
    {
        if (item.IsDirectory || !File.Exists(item.FullPath)) return;
        try
        {
            var dir = Path.GetDirectoryName(item.FullPath)!;
            var nameNoExt = Path.GetFileNameWithoutExtension(item.Name);
            var ext = item.Extension;
            var copyPath = Path.Combine(dir, $"{nameNoExt}_copy{ext}");
            var suffix = 2;
            while (File.Exists(copyPath))
            {
                copyPath = Path.Combine(dir, $"{nameNoExt}_copy{suffix++}{ext}");
            }
            File.Copy(item.FullPath, copyPath);

            if (_currentFolder != null)
            {
                FileSystemScanner.Refresh(_currentFolder);
                RefreshAssetList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentBrowser] Duplicate failed: {ex.Message}");
        }
    }

    private void OnCopyPathRequested(ContentItem? item)
    {
        if (item == null) return;
        // Clipboard isn't easily available in MonoGame — log for now
        Debug.WriteLine($"[ContentBrowser] Copy path: {item.FullPath}");
    }

    private void OnShowInExplorer(ContentItem item)
    {
        try
        {
            var dir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath);
            if (dir != null && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentBrowser] Show in explorer failed: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Search
    // ─────────────────────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> e)
    {
        _searchFilter = e.NewValue?.Trim() ?? string.Empty;
        RefreshAssetList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Asset list events
    // ─────────────────────────────────────────────────────────────────────────

    private void OnAssetSelectionChanged(object? sender, ReadOnlyCollection<MGListBoxItem<ContentItem>> items)
    {
        var selected = _assetList.SelectedValue;

        // Detect double-click via rapid re-selection of same item
        if (selected != null)
        {
            var now = DateTime.UtcNow;
            if (_lastClickedItem == selected && (now - _lastClickTime).TotalMilliseconds < DoubleClickMs)
            {
                // Double-click detected
                _lastClickedItem = null;
                if (selected.IsDirectory)
                    NavigateTo(selected);
                else
                    FileOpened?.Invoke(selected);
                return;
            }
            _lastClickedItem = selected;
            _lastClickTime = now;
        }

        if (selected != null && !selected.IsDirectory)
        {
            FileSelected?.Invoke(selected);
        }

        var selectedList = selected != null
            ? new List<ContentItem> { selected }
            : new List<ContentItem>();
        SelectionChanged?.Invoke(selectedList);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Catalog / project event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnProjectLoaded(object? sender, EventArgs e) => RebuildTree();
    private void OnAssetAdded(object? sender, AssetInfo e) => RebuildTree();
    private void OnAssetRemoved(object? sender, AssetInfo e) => RebuildTree();
    private void OnAssetRenamed(object? sender, EventArgs<AssetInfo, string> a) => RebuildTree();
    private void OnAssetCleared(object? sender, EventArgs e) => RebuildTree();

    // ─────────────────────────────────────────────────────────────────────────
    //  Tree building
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rescans the project directory from disk and rebuilds the full UI.
    /// </summary>
    private void RebuildTree()
    {
        _itemToFolder.Clear();

        var rootPath = EngineEnvironment.ProjectPath;
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            _rootItem = null;
            _currentFolder = null;
            _treeView?.ClearItems();
            _assetList?.SetItemsSource(new List<ContentItem>());
            return;
        }

        _rootItem = FileSystemScanner.ScanDirectory(rootPath);

        // Keep current folder if still valid, otherwise reset to root
        if (_currentFolder == null || !Directory.Exists(_currentFolder.FullPath))
        {
            _currentFolder = _rootItem;
        }
        else
        {
            // Re-find the matching folder in the new tree
            _currentFolder = FindFolder(_rootItem, _currentFolder.FullPath) ?? _rootItem;
        }

        RefreshTreeView();
        RefreshAssetList();
        UpdateBreadcrumb();
        UpdateNavButtons();
    }

    /// <summary>Rebuilds the <see cref="_treeView"/> from <see cref="_rootItem"/>.</summary>
    private void RefreshTreeView()
    {
        _treeView.ClearItems();
        _itemToFolder.Clear();

        if (_rootItem == null) return;

        var rootTvi = BuildTreeItem(_rootItem);
        rootTvi.IsExpanded = true;
        _treeView.AddItem(rootTvi);
    }

    private MGTreeViewItem BuildTreeItem(ContentItem folder)
    {
        var item = new MGTreeViewItem(_window) { IsExpanded = false };

        // Header: icon + name
        var header = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 4 };
        var folderIcon = EditorIcons.Folder;
        if (folderIcon != null)
        {
            header.TryAddChild(new MGImage(_window, folderIcon, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16,
                PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        header.TryAddChild(new MGTextBlock(_window, folder.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        item.Header = header;

        _itemToFolder[item] = folder;

        foreach (var child in folder.SubFolders)
            item.AddItem(BuildTreeItem(child));

        return item;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tree selection → asset list
    // ─────────────────────────────────────────────────────────────────────────

    private void OnFolderSelectionChanged(object? sender, MGTreeViewItem? tvi)
    {
        if (tvi != null && _itemToFolder.TryGetValue(tvi, out var folder))
        {
            if (_currentFolder != null && _currentFolder != folder)
            {
                _backHistory.Push(_currentFolder);
                _forwardHistory.Clear();
            }

            _currentFolder = folder;
            UpdateBreadcrumb();
            UpdateNavButtons();
        }

        RefreshAssetList();
    }

    private void RefreshAssetList()
    {
        var displayFolder = _currentFolder ?? _rootItem;
        if (displayFolder == null)
        {
            _assetList?.SetItemsSource(new List<ContentItem>());
            return;
        }

        IEnumerable<ContentItem> items = displayFolder.Children;

        // Apply search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            items = CollectAllDescendants(displayFolder)
                .Where(c => c.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        _assetList.SetItemsSource(items.OrderByDescending(c => c.IsDirectory).ThenBy(c => c.Name).ToList());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all descendants (files and folders recursively).</summary>
    private static IEnumerable<ContentItem> CollectAllDescendants(ContentItem folder)
    {
        foreach (var c in folder.Children)
        {
            yield return c;
            if (c.IsDirectory)
            {
                foreach (var d in CollectAllDescendants(c))
                    yield return d;
            }
        }
    }

    /// <summary>Finds a folder in the tree by path.</summary>
    private static ContentItem? FindFolder(ContentItem root, string fullPath)
    {
        if (string.Equals(root.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var sub in root.SubFolders)
        {
            var found = FindFolder(sub, fullPath);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Selects the tree node corresponding to the given folder.</summary>
    private void SelectTreeNode(ContentItem? folder)
    {
        if (folder == null) return;

        foreach (var kvp in _itemToFolder)
        {
            if (string.Equals(kvp.Value.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                _treeView.SelectedItem = kvp.Key;
                return;
            }
        }
    }
}
