using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Editor.History;
using CasaEngine.Editor.ContentBrowser;
using CasaEngine.Editor.ContentBrowser.Controls;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using CasaEngine.Editor.ContentBrowser.Views;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.DragDrop;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;
using FormsClipboard = System.Windows.Forms.Clipboard;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsMessageBox = System.Windows.Forms.MessageBox;
using FormsMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using FormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A two-pane content browser panel.
/// Left pane  : folder tree  (<see cref="MGTreeView"/>).
/// Right pane : content view (<see cref="IContentView"/>).
/// The data model is <see cref="ContentItem"/> (file-system based) and the
/// tree is populated by <see cref="FileSystemScanner"/>.
/// </summary>
public class ContentBrowserPanel
{
    private static readonly Color ToolbarBackgroundColor = new(26, 30, 38);
    private static readonly Color TreeBackgroundColor = new(22, 25, 31);
    private static readonly Color ContentBackgroundColor = new(18, 21, 28);
    private static readonly Color PanelBorderColor = new(62, 72, 88);
    private static readonly Color AccentSelectionColor = new(58, 110, 182, 185);

    private sealed class ContextMenuExtension
    {
        public string Label { get; }
        public Action<ContentItem> Action { get; }

        public ContextMenuExtension(string label, Action<ContentItem> action)
        {
            Label = label;
            Action = action;
        }
    }

    private sealed class ContentBrowserViewState
    {
        public ContentBrowserViewState(string? folderPath, IReadOnlyList<string> selectionPaths)
        {
            FolderPath = folderPath;
            SelectionPaths = selectionPaths;
        }

        public string? FolderPath { get; }

        public IReadOnlyList<string> SelectionPaths { get; }
    }

    private sealed class ExecutedContentBrowserCommand : IEditorCommand
    {
        private readonly FileOperationService _fileOperationService;
        private readonly ReversibleFileOperation _operation;
        private readonly Action _applyExecuteViewState;
        private readonly Action _applyUndoViewState;
        private bool _isInitialized;

        public ExecutedContentBrowserCommand(
            string description,
            FileOperationService fileOperationService,
            ReversibleFileOperation operation,
            Action applyExecuteViewState,
            Action applyUndoViewState)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentNullException.ThrowIfNull(fileOperationService);
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(applyExecuteViewState);
            ArgumentNullException.ThrowIfNull(applyUndoViewState);

            Description = description;
            _fileOperationService = fileOperationService;
            _operation = operation;
            _applyExecuteViewState = applyExecuteViewState;
            _applyUndoViewState = applyUndoViewState;
        }

        public string Description { get; }

        public void Execute()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _applyExecuteViewState();
                return;
            }

            if (_operation.Redo(_fileOperationService))
            {
                _applyExecuteViewState();
            }
        }

        public void Undo()
        {
            if (_operation.Undo(_fileOperationService))
            {
                _applyUndoViewState();
            }
        }
    }

    private readonly FileOperationService _fileOperationService = new();
    private readonly ThumbnailCache _thumbnailCache;
    private readonly Dictionary<string, List<MGImage>> _tooltipPreviewImages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MGTextBlock>> _tooltipDimensionTexts = new(StringComparer.OrdinalIgnoreCase);
    private string _pendingOperationError = string.Empty;
    private readonly ContentContextMenu _contextMenu;
    private readonly InlineRenameOverlay _inlineRenameOverlay;
    private string? _pendingCurrentFolderPath;
    private List<string>? _pendingSelectionPaths;
    private readonly List<string> _clipboardPaths = new();
    private bool _clipboardMoveOperation;
    private readonly Dictionary<ContentItemType, List<ContextMenuExtension>> _contextMenuExtensions = new();

    /// <summary>Raised when a file is selected (single click).</summary>
    public event Action<ContentItem>? FileSelected;
    /// <summary>Raised when a file is opened (double-click).</summary>
    public event Action<ContentItem>? FileOpened;
    /// <summary>Raised when a file or folder is deleted.</summary>
    public event Action<ContentItem>? FileDeleted;
    /// <summary>Raised when a file or folder is renamed (item, old name).</summary>
    public event Action<ContentItem, string>? FileRenamed;
    /// <summary>Raised when a file or folder is moved (item, old parent).</summary>
    public event Action<ContentItem, ContentItem>? FileMoved;
    /// <summary>Raised when the selection set changes.</summary>
    public event Action<IReadOnlyList<ContentItem>>? SelectionChanged;

    private readonly MGWindow _window;

    public ContentBrowserConfig Config { get; }

    public ContentBrowserEvents Events { get; } = new();

    // UI controls
    private MGTreeView _treeView = null!;
    private MGContentPresenter _contentViewHost = null!;
    private MGComboBox<string> _viewModeComboBox = null!;
    private MGStackPanel _breadcrumbBar = null!;
    private MGTextBox _searchBox = null!;
    private MGButton _btnBack = null!;
    private MGButton _btnForward = null!;
    private MGButton _btnParent = null!;
    private GridView _gridView = null!;
    private DetailView _detailView = null!;
    private IContentView _activeContentView = null!;

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

    private static readonly MGSolidFillBrush DropHighlightBrush = new(new Color(70, 130, 180, 96));


    public ContentBrowserPanel(MGWindow window)
        : this(window, null)
    {
    }

    public ContentBrowserPanel(MGWindow window, ContentBrowserConfig? config)
    {
        _window = window;
        Config = config ?? new ContentBrowserConfig();
        if (window.Desktop.Runtime is not CasaDesktopRuntime runtime)
        {
            throw new InvalidOperationException($"{nameof(ContentBrowserPanel)} requires the CasaEngine MGUI backend runtime.");
        }

        _thumbnailCache = new ThumbnailCache(runtime.GraphicsDevice, Config.ThumbnailSize);
        _thumbnailCache.ThumbnailReady += OnThumbnailReady;
        _contextMenu = new ContentContextMenu(window);
        _inlineRenameOverlay = new InlineRenameOverlay(window);
        _fileOperationService.ErrorOccurred += OnFileOperationError;
    }

    /// <summary>
    /// Builds and returns the root MGUI element for this panel.
    /// Intended to be used as a <c>DockPanelNode.ContentFactory</c> result.
    /// </summary>
    public MGElement CreateContent()
    {
        // Toolbar row
        var toolbar = WrapPanelSurface(BuildToolbar(), ToolbarBackgroundColor, new Thickness(6, 4, 6, 4));

        // Tree view (left pane)
        _treeView = new MGTreeView(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(TreeBackgroundColor)),
            SelectionBackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(AccentSelectionColor)),
        };
        _treeView.SelectionChanged += OnFolderSelectionChanged;
        _treeView.KeyboardHandler.Pressed += OnTreeViewKeyPressed;
        _treeView.MouseHandler.RMBReleasedInside += OnTreeViewRightClick;
        _window.WindowKeyboardHandler.Pressed += OnGlobalKeyPressed;

        var treeScroll = new MGScrollViewer(_window);
        treeScroll.SetContent(_treeView);
        var treePane = WrapPanelSurface(treeScroll, TreeBackgroundColor);

        // Content views (right pane)
        _gridView = new GridView(_window, Config.ThumbnailSize, GetGridItemPreviewTexture, ConfigureGridItemElement);
        _detailView = new DetailView(_window, GetIconForType, ConfigureDetailItemElement);

        BindContentViewEvents(_gridView);
        BindContentViewEvents(_detailView);
        ConfigureContentViewInteractions(_gridView.RootElement);
        ConfigureContentViewInteractions(_detailView.ListView);

        _activeContentView = Config.DefaultViewMode == ContentViewMode.Detail ? _detailView : _gridView;
        _contentViewHost = new MGContentPresenter(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _contentViewHost.SetContent(_activeContentView.RootElement);
        var contentPane = WrapPanelSurface(_contentViewHost, ContentBackgroundColor);

        // Grid: [tree | splitter | list]
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

        contentGrid.TryAddChild(0, 0, treePane);
        contentGrid.TryAddChild(0, 1, splitter);
        contentGrid.TryAddChild(0, 2, contentPane);

        // Outer dock: toolbar on top, content fills
        var outerPanel = new MGDockPanel(_window);
        outerPanel.TryAddChild(toolbar, Dock.Top);
        outerPanel.TryAddChild(contentGrid, Dock.Top); // last child fills

        EditorAssetCatalogService.AssetAdded += OnAssetAdded;
        EditorAssetCatalogService.AssetRemoved += OnAssetRemoved;
        EditorAssetCatalogService.AssetRenamed += OnAssetRenamed;
        EditorAssetCatalogService.AssetCleared += OnAssetCleared;
        EditorProjectAuthoringService.ProjectLoaded += OnProjectLoaded;

        // Initial population
        RebuildTree();

        return outerPanel;
    }

    /// <summary>Forces a complete rescan from disk.</summary>
    public void Refresh()
    {
        RebuildTree();
    }

    public void RegisterContextMenuExtension(ContentItemType type, string label, Action<ContentItem> action)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A context menu extension label is required.", nameof(label));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (!_contextMenuExtensions.TryGetValue(type, out var extensions))
        {
            extensions = new List<ContextMenuExtension>();
            _contextMenuExtensions[type] = extensions;
        }

        extensions.Add(new ContextMenuExtension(label, action));
    }

    public void Update()
    {
        _thumbnailCache.Update();

        if (_fileOperationService.ConsumePendingExternalChanges())
        {
            _thumbnailCache.InvalidateAll();
            RebuildTree();
        }

        if (!string.IsNullOrEmpty(_pendingOperationError))
        {
            FormsMessageBox.Show(_pendingOperationError, "Content Browser", FormsMessageBoxButtons.OK, FormsMessageBoxIcon.Error);
            _pendingOperationError = string.Empty;
        }
    }

    /// <summary>Navigates to the given folder, updating history.</summary>
    public void NavigateTo(ContentItem folder)
    {
        if (folder == null || !folder.IsDirectory)
        {
            return;
        }

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
            toolbar.TryAddChild(new MGImage(_window, EditorIcons.AsImage(EditorIcons.Search)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 16, PreferredHeight = 16,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        toolbar.TryAddChild(_searchBox);

        toolbar.TryAddChild(new MGSeparator(_window, Orientation.Vertical)
        {
            Margin = new Thickness(4, 0, 4, 0),
            PreferredHeight = 20,
        });

        _viewModeComboBox = new MGComboBox<string>(_window)
        {
            MinWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _viewModeComboBox.SetItemsSource(new[] { "Grid", "Details" });
        _viewModeComboBox.SelectedItem = Config.DefaultViewMode == ContentViewMode.Detail ? "Details" : "Grid";
        _viewModeComboBox.SelectedItemChanged += OnViewModeChanged;
        toolbar.TryAddChild(_viewModeComboBox);

        return toolbar;
    }

    private void BindContentViewEvents(IContentView view)
    {
        view.SelectionChanged += items => OnContentViewSelectionChanged(view, items);
        view.FileDoubleClicked += item =>
        {
            if (view == _activeContentView)
            {
                FileOpened?.Invoke(item);
                Events.RaiseFileOpened(item);
            }
        };
        view.DirectoryDoubleClicked += item =>
        {
            if (view == _activeContentView)
            {
                NavigateTo(item);
            }
        };
    }

    private void ConfigureContentViewInteractions(MGElement element)
    {
        element.AllowDrop = true;
        element.DragEnter += OnAssetListDragEnter;
        element.DragOver += OnAssetListDragOver;
        element.DragLeave += OnAssetListDragLeave;
        element.Drop += OnAssetListDrop;
        element.KeyboardHandler.Pressed += OnAssetListKeyPressed;
        element.MouseHandler.DragStart += OnAssetListDragStart;
        element.MouseHandler.RMBReleasedInside += OnAssetListRightClick;
    }

    private void OnViewModeChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> e)
    {
        if (string.Equals(e.NewValue, "Details", StringComparison.Ordinal))
        {
            SetActiveContentView(_detailView);
            return;
        }

        SetActiveContentView(_gridView);
    }

    private void SetActiveContentView(IContentView view)
    {
        if (_contentViewHost == null || _activeContentView == view)
        {
            return;
        }

        var previousSelection = GetSelectedItems();
        _activeContentView = view;
        _contentViewHost.SetContent(view.RootElement);
        view.RestoreSelection(previousSelection);
    }

    private void OnContentViewSelectionChanged(IContentView view, IReadOnlyList<ContentItem> selectedItems)
    {
        if (view != _activeContentView)
        {
            return;
        }

        var selected = selectedItems.Count > 0 ? selectedItems[0] : null;
        if (selected != null && !selected.IsDirectory)
        {
            FileSelected?.Invoke(selected);
            Events.RaiseFileSelected(selected);
        }

        SelectionChanged?.Invoke(selectedItems);
        Events.RaiseSelectionChanged(selectedItems);
    }

    private MGButton MakeIconButton(Texture2D? icon, string tooltip, Action action)
    {
        var btn = new MGButton(_window, _ => action())
        {
            Padding = new Thickness(2, 2, 2, 2),
        };

        if (icon != null)
        {
            btn.SetContent(new MGImage(_window, EditorIcons.AsImage(icon)!, Stretch: Stretch.Uniform)
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

    private void UpdateBreadcrumb()
    {
        _breadcrumbBar.TryRemoveAll();

        if (_currentFolder == null || _rootItem == null)
        {
            return;
        }

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

    private void GoBack()
    {
        if (_backHistory.Count == 0)
        {
            return;
        }

        if (_currentFolder != null)
        {
            _forwardHistory.Push(_currentFolder);
        }

        _currentFolder = _backHistory.Pop();
        UpdateBreadcrumb();
        RefreshAssetList();
        UpdateNavButtons();
        SelectTreeNode(_currentFolder);
    }

    private void GoForward()
    {
        if (_forwardHistory.Count == 0)
        {
            return;
        }

        if (_currentFolder != null)
        {
            _backHistory.Push(_currentFolder);
        }

        _currentFolder = _forwardHistory.Pop();
        UpdateBreadcrumb();
        RefreshAssetList();
        UpdateNavButtons();
        SelectTreeNode(_currentFolder);
    }

    private void GoUp()
    {
        if (_currentFolder?.Parent == null)
        {
            return;
        }

        NavigateTo(_currentFolder.Parent);
    }

    private void UpdateNavButtons()
    {
        _btnBack.IsEnabled = _backHistory.Count > 0;
        _btnForward.IsEnabled = _forwardHistory.Count > 0;
        _btnParent.IsEnabled = _currentFolder?.Parent != null;
    }
    
    private void ConfigureGridItemElement(ContentItem item, MGElement element)
    {
        ConfigureItemToolTip(item, element);

        if (item.IsDirectory)
        {
            element.AllowDrop = true;
            element.DragEnter += (_, e) => OnFolderDropTargetDragEnter(element, item, e);
            element.DragOver += (_, e) => OnFolderDropTargetDragOver(element, item, e);
            element.DragLeave += (_, e) => OnFolderDropTargetDragLeave(element, item, e);
            element.Drop += (_, e) => OnFolderDropTargetDrop(element, item, e);
        }
    }

    private void ConfigureDetailItemElement(ContentItem item, MGElement element)
    {
        ConfigureItemToolTip(item, element);
    }

    private Texture2D? GetGridItemPreviewTexture(ContentItem item)
    {
        var placeholder = GetIconForType(item.Type);
        var cached = _thumbnailCache.GetOrRequest(item, placeholder);
        if (cached.IsLoaded && cached.Texture != null)
        {
            item.Thumbnail = cached.Texture;
            return cached.Texture;
        }

        return item.Thumbnail ?? placeholder;
    }

    /// <summary>Returns the best icon <see cref="Texture2D"/> for the given type.</summary>
    private Texture2D? GetIconForType(ContentItemType type) => ContentItemDisplay.GetIcon(Config, type);
    
    private void OnTreeViewRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var menu = _contextMenu.CreateTreeMenu(
            _currentFolder,
            HasClipboardItems,
            OnOpenFolderRequested,
            OnNewFolderRequested,
            OnRenameFolderRequested,
            () => OnCopyRequested(_currentFolder),
            () => OnCutRequested(_currentFolder),
            () => OnCopyPathRequested(_currentFolder),
            OnPasteRequested,
            OnDeleteFolderRequested);
        _treeView.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }

    private void OnAssetListRightClick(object? sender, BaseMouseReleasedEventArgs e)
    {
        var selected = GetSelectedItem();

        var menu = _contextMenu.CreateContentMenu(
            selected,
            HasClipboardItems,
            OnNewFolderRequested,
            OnImportRequested,
            Refresh,
            OnPasteRequested,
            selected == null ? null : () => OnOpenItemRequested(selected),
            selected == null ? null : () => OnRenameItemRequested(selected),
            selected == null ? null : () => OnDuplicateRequested(selected),
            selected == null ? null : () => OnCopyRequested(selected),
            selected == null ? null : () => OnCutRequested(selected),
            selected == null ? null : () => OnCopyPathRequested(selected),
            selected == null ? null : () => OnShowInExplorer(selected),
            selected == null ? null : () => OnPropertiesRequested(selected),
            selected == null ? null : () => OnDeleteItemRequested(selected));

        if (selected != null)
        {
            AppendContextMenuExtensions(menu, selected);
        }

        var target = sender as MGElement ?? _contentViewHost;
        target.GetDesktop().TryOpenContextMenu(menu, e.Position);
    }
    
    private void OnOpenFolderRequested()
    {
        if (_currentFolder == null)
        {
            return;
        }
        // Already navigated via tree selection — nothing extra to do
    }

    private void OnOpenItemRequested(ContentItem item)
    {
        if (item.IsDirectory)
        {
            NavigateTo(item);
            return;
        }

        FileOpened?.Invoke(item);
        Events.RaiseFileOpened(item);
    }

    private void OnNewFolderRequested()
    {
        if (_currentFolder == null)
        {
            return;
        }

        string folderName = "New Folder";
        string candidatePath = Path.Combine(_currentFolder.FullPath, folderName);
        int suffix = 1;
        while (Directory.Exists(candidatePath))
        {
            folderName = $"New Folder ({suffix++})";
            candidatePath = Path.Combine(_currentFolder.FullPath, folderName);
        }

        var undoViewState = CaptureViewState();
        if (_fileOperationService.TryCreateDirectoryOperation(_currentFolder.FullPath, folderName, out var operation))
        {
            var executeViewState = CreateViewState(_currentFolder.FullPath, operation.SelectionAfterExecute);
            ExecuteHistoryOperation("Create Folder", operation, executeViewState, undoViewState);
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
        if (item.Parent == null)
        {
            return;
        }

        if (!TryGetRenameAnchorBounds(item, out var anchorBounds))
        {
            Debug.WriteLine($"[ContentBrowser] Cannot start rename without a valid anchor: {item.FullPath}");
            return;
        }

        _inlineRenameOverlay.Show(item, anchorBounds, TryCommitInlineRename);
    }

    private void OnDeleteItemRequested(ContentItem item)
    {
        if (FormsMessageBox.Show($"Delete '{item.Name}'?", "Content Browser", FormsMessageBoxButtons.YesNo, FormsMessageBoxIcon.Warning) != FormsDialogResult.Yes)
        {
            return;
        }

        InvalidateThumbnailForItem(item);
        var undoViewState = CaptureViewState();
        if (_fileOperationService.TryDeleteOperation(new[] { item.FullPath }, out var operation))
        {
            var executeViewState = CreateViewState(GetFolderPathAfterDelete(new[] { item }), operation.SelectionAfterExecute);
            ExecuteHistoryOperation("Delete", operation, executeViewState, undoViewState);
            FileDeleted?.Invoke(item);
            Events.RaiseFileDeleted(item);
        }
    }

    private void OnDuplicateRequested(ContentItem item)
    {
        if (item.Parent == null)
        {
            return;
        }

        var undoViewState = CaptureViewState();
        if (_fileOperationService.TryCopyOperation(new[] { item.FullPath }, item.Parent.FullPath, out var operation))
        {
            var executeViewState = CreateViewState(_currentFolder?.FullPath, operation.SelectionAfterExecute);
            ExecuteHistoryOperation("Duplicate", operation, executeViewState, undoViewState);
        }
    }

    private void OnCopyRequested(ContentItem? item)
    {
        SetClipboardItems(GetContextItems(item), false);
    }

    private void OnCutRequested(ContentItem? item)
    {
        SetClipboardItems(GetContextItems(item), true);
    }

    private void OnPasteRequested()
    {
        if (_currentFolder == null)
        {
            return;
        }

        var clipboardItems = ResolveClipboardItems();
        if (!CanPasteIntoFolder(_currentFolder, clipboardItems, _clipboardMoveOperation))
        {
            return;
        }

        if (TransferItems(_currentFolder, clipboardItems, !_clipboardMoveOperation))
        {
            if (_clipboardMoveOperation)
            {
                ClearClipboardItems();
            }
        }
    }

    private void OnImportRequested()
    {
        if (_currentFolder == null)
        {
            return;
        }

        using var dialog = new FormsOpenFileDialog
        {
            Title = "Import files into the Content Browser",
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog() != FormsDialogResult.OK || dialog.FileNames.Length == 0)
        {
            return;
        }

        var undoViewState = CaptureViewState();
        if (_fileOperationService.TryImportOperation(dialog.FileNames, _currentFolder.FullPath, out var operation))
        {
            var executeViewState = CreateViewState(_currentFolder.FullPath, operation.SelectionAfterExecute);
            ExecuteHistoryOperation("Import", operation, executeViewState, undoViewState);
        }
    }

    private void OnPropertiesRequested(ContentItem item)
    {
        FormsMessageBox.Show(BuildPropertiesText(item), $"Properties - {item.Name}", FormsMessageBoxButtons.OK, FormsMessageBoxIcon.Information);
    }

    private void OnCopyPathRequested(ContentItem? item)
    {
        if (item == null)
        {
            return;
        }

        try
        {
            FormsClipboard.SetText(item.FullPath);
        }
        catch (Exception ex)
        {
            OnFileOperationError($"Cannot copy '{item.FullPath}' to the clipboard.\n{ex.Message}");
        }
    }

    private void OnShowInExplorer(ContentItem item)
    {
        try
        {
            var dir = item.IsDirectory ? item.FullPath : Path.GetDirectoryName(item.FullPath);
            if (dir != null && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentBrowser] Show in explorer failed: {ex.Message}");
        }
    }

    private void OnSearchTextChanged(object? sender, MGUI.Shared.Helpers.EventArgs<string> e)
    {
        _searchFilter = e.NewValue?.Trim() ?? string.Empty;
        RefreshAssetList();
    }

    private void OnTreeViewKeyPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (e.IsHandled || _treeView.SelectedItem == null)
        {
            return;
        }

        if (!_itemToFolder.TryGetValue(_treeView.SelectedItem, out var folder))
        {
            return;
        }

        switch (e.Key)
        {
            case Keys.Enter:
                NavigateTo(folder);
                e.SetHandledBy(_treeView, true);
                break;
            case Keys.F2:
                OnRenameItemRequested(folder);
                e.SetHandledBy(_treeView, true);
                break;
            case Keys.Delete:
                OnDeleteItemRequested(folder);
                e.SetHandledBy(_treeView, true);
                break;
            case Keys.Back:
                GoUp();
                e.SetHandledBy(_treeView, true);
                break;
        }
    }

    private void OnAssetListKeyPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (e.IsHandled)
        {
            return;
        }

        var selected = GetSelectedItem();
        if (selected == null)
        {
            return;
        }

        switch (e.Key)
        {
            case Keys.Enter:
                if (selected.IsDirectory)
                {
                    NavigateTo(selected);
                }
                else
                {
                    FileOpened?.Invoke(selected);
                }

                e.SetHandledBy(sender as MGElement ?? _contentViewHost, true);
                break;
            case Keys.F2:
                OnRenameItemRequested(selected);
                e.SetHandledBy(sender as MGElement ?? _contentViewHost, true);
                break;
            case Keys.Delete:
                OnDeleteSelectedItemsRequested();
                e.SetHandledBy(sender as MGElement ?? _contentViewHost, true);
                break;
            case Keys.Back:
                GoUp();
                e.SetHandledBy(sender as MGElement ?? _contentViewHost, true);
                break;
        }
    }
    
    private void OnAssetItemDragStart(MGElement element, ContentItem item, BaseMouseDragStartEventArgs e)
    {
        var draggedItems = GetDraggedItems(item);
        if (draggedItems.Count == 0)
        {
            return;
        }

        element.DoDragDrop(new DragDropData(draggedItems, DragDropEffect.Copy | DragDropEffect.Move));
        e.SetHandledBy(element, true);
    }

    private void OnAssetListDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        if (!e.IsLMB)
        {
            return;
        }

        ContentItem? draggedItem = null;
        if (sender == _gridView.RootElement)
        {
            draggedItem = _gridView.PressedItem;
        }

        draggedItem ??= GetSelectedItem();
        if (draggedItem == null)
        {
            return;
        }

        OnAssetItemDragStart(sender as MGElement ?? _contentViewHost, draggedItem, e);
    }

    private void OnAssetListDragEnter(object? sender, DragEnterEventArgs e)
    {
        if (sender is not MGElement element)
        {
            return;
        }

        if (CanDropIntoFolder(_currentFolder, e.Data.GetData<List<ContentItem>>()))
        {
            element.OverlayBrush = DropHighlightBrush;
        }
    }

    private void OnAssetListDragOver(object? sender, DragOverEventArgs e)
    {
        if (sender is not MGElement element)
        {
            return;
        }

        e.Data.DropEffect = GetCurrentDropEffect();
        element.OverlayBrush = CanDropIntoFolder(_currentFolder, e.Data.GetData<List<ContentItem>>())
            ? DropHighlightBrush
            : null;
    }

    private void OnAssetListDragLeave(object? sender, DragLeaveEventArgs e)
    {
        if (sender is MGElement element)
        {
            element.OverlayBrush = null;
        }
    }

    private void OnAssetListDrop(object? sender, DropEventArgs e)
    {
        if (sender is MGElement element)
        {
            element.OverlayBrush = null;
        }

        if (_currentFolder == null)
        {
            return;
        }

        PerformDrop(_currentFolder, e.Data.GetData<List<ContentItem>>(), e.Data.DropEffect);
    }

    private void OnFolderDropTargetDragEnter(MGElement targetElement, ContentItem targetFolder, DragEnterEventArgs e)
    {
        if (CanDropIntoFolder(targetFolder, e.Data.GetData<List<ContentItem>>()))
        {
            targetElement.OverlayBrush = DropHighlightBrush;
        }
    }

    private void OnFolderDropTargetDragOver(MGElement targetElement, ContentItem targetFolder, DragOverEventArgs e)
    {
        e.Data.DropEffect = GetCurrentDropEffect();
        targetElement.OverlayBrush = CanDropIntoFolder(targetFolder, e.Data.GetData<List<ContentItem>>())
            ? DropHighlightBrush
            : null;
    }

    private void OnFolderDropTargetDragLeave(MGElement targetElement, ContentItem targetFolder, DragLeaveEventArgs e)
    {
        targetElement.OverlayBrush = null;
    }

    private void OnFolderDropTargetDrop(MGElement targetElement, ContentItem targetFolder, DropEventArgs e)
    {
        targetElement.OverlayBrush = null;
        PerformDrop(targetFolder, e.Data.GetData<List<ContentItem>>(), e.Data.DropEffect);
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
        if (_inlineRenameOverlay.IsOpen)
        {
            _inlineRenameOverlay.Cancel();
        }

        _itemToFolder.Clear();

        var rootPath = GetConfiguredRootPath();
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            _rootItem = null;
            _currentFolder = null;
            _fileOperationService.ClearRoot();
            _treeView?.ClearItems();
            _gridView?.SetItems(Array.Empty<ContentItem>());
            _detailView?.SetItems(Array.Empty<ContentItem>());
            return;
        }

        _rootItem = FileSystemScanner.ScanDirectory(rootPath);
        _fileOperationService.SetRoot(_rootItem);

        var targetFolderPath = _pendingCurrentFolderPath ?? _currentFolder?.FullPath;
        _pendingCurrentFolderPath = null;
        _currentFolder = string.IsNullOrWhiteSpace(targetFolderPath)
            ? _rootItem
            : FindFolder(_rootItem, targetFolderPath) ?? _rootItem;

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

        if (_rootItem == null)
        {
            return;
        }

        var rootTvi = BuildTreeItem(_rootItem);
        rootTvi.IsExpanded = true;
        _treeView.AddItem(rootTvi);
    }

    private MGTreeViewItem BuildTreeItem(ContentItem folder)
    {
        var item = new MGTreeViewItem(_window) { IsExpanded = false };

        // Header: icon + name
        var header = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 4 };
        var folderIcon = GetIconForType(ContentItemType.Folder);
        if (folderIcon != null)
        {
            header.TryAddChild(new MGImage(_window, EditorIcons.AsImage(folderIcon)!, Stretch: Stretch.Uniform)
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
        item.AllowDrop = true;
        item.DragEnter += (_, e) => OnFolderDropTargetDragEnter(header, folder, e);
        item.DragOver += (_, e) => OnFolderDropTargetDragOver(header, folder, e);
        item.DragLeave += (_, e) => OnFolderDropTargetDragLeave(header, folder, e);
        item.Drop += (_, e) => OnFolderDropTargetDrop(header, folder, e);

        _itemToFolder[item] = folder;

        foreach (var child in folder.SubFolders)
        {
            if (ShouldIncludeItem(child))
            {
                item.AddItem(BuildTreeItem(child));
            }
        }

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
        var previousSelection = GetSelectedItems();
        var pendingSelectionPaths = _pendingSelectionPaths;
        _pendingSelectionPaths = null;
        ClearTooltipRegistrations();
        if (displayFolder == null)
        {
            _gridView?.SetItems(Array.Empty<ContentItem>());
            _detailView?.SetItems(Array.Empty<ContentItem>());
            return;
        }

        var orderedItems = ContentBrowserItemQuery.GetVisibleItems(displayFolder, _searchFilter, ShouldIncludeItem);
        _gridView.SetItems(orderedItems);
        _detailView.SetItems(orderedItems);

        if (pendingSelectionPaths != null && pendingSelectionPaths.Count > 0)
        {
            var restoredSelection = new List<ContentItem>();
            for (int selectionIndex = 0; selectionIndex < pendingSelectionPaths.Count; selectionIndex++)
            {
                string selectionPath = pendingSelectionPaths[selectionIndex];
                for (int itemIndex = 0; itemIndex < orderedItems.Count; itemIndex++)
                {
                    if (!string.Equals(orderedItems[itemIndex].FullPath, selectionPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    restoredSelection.Add(orderedItems[itemIndex]);
                    break;
                }
            }

            if (restoredSelection.Count > 0)
            {
                _activeContentView.RestoreSelection(restoredSelection);
                return;
            }
        }

        _activeContentView.RestoreSelection(previousSelection);
    }

    private List<ContentItem> GetDraggedItems(ContentItem primaryItem)
    {
        var selectedItems = GetSelectedItems();
        if (selectedItems.Count == 0 || !selectedItems.Contains(primaryItem))
        {
            return new List<ContentItem> { primaryItem };
        }

        return selectedItems;
    }

    private List<ContentItem> GetSelectedItems()
    {
        var selectedItems = new List<ContentItem>();
        if (_activeContentView == null)
        {
            return selectedItems;
        }

        foreach (var item in _activeContentView.SelectedItems)
        {
            selectedItems.Add(item);
        }

        return selectedItems;
    }

    private ContentItem? GetSelectedItem()
    {
        var selectedItems = GetSelectedItems();
        return selectedItems.Count > 0 ? selectedItems[0] : null;
    }

    private static DragDropEffect GetCurrentDropEffect()
    {
        var keyboardState = Keyboard.GetState();
        return keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)
            ? DragDropEffect.Copy
            : DragDropEffect.Move;
    }

    private static bool CanDropIntoFolder(ContentItem? targetFolder, IReadOnlyList<ContentItem>? draggedItems)
    {
        if (targetFolder == null || !targetFolder.IsDirectory || draggedItems == null || draggedItems.Count == 0)
        {
            return false;
        }

        foreach (var draggedItem in draggedItems)
        {
            if (draggedItem == null)
            {
                return false;
            }

            if (string.Equals(draggedItem.FullPath, targetFolder.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var targetPath = Path.Combine(targetFolder.FullPath, draggedItem.Name);
            if (string.Equals(draggedItem.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (draggedItem.IsDirectory && IsChildPath(targetFolder.FullPath, draggedItem.FullPath))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanPasteIntoFolder(ContentItem? targetFolder, IReadOnlyList<ContentItem>? clipboardItems, bool isMoveOperation)
    {
        if (targetFolder == null || !targetFolder.IsDirectory || clipboardItems == null || clipboardItems.Count == 0)
        {
            return false;
        }

        foreach (var clipboardItem in clipboardItems)
        {
            if (clipboardItem == null)
            {
                return false;
            }

            if (clipboardItem.IsDirectory && string.Equals(clipboardItem.FullPath, targetFolder.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (clipboardItem.IsDirectory && IsChildPath(targetFolder.FullPath, clipboardItem.FullPath))
            {
                return false;
            }

            if (isMoveOperation)
            {
                var targetPath = Path.Combine(targetFolder.FullPath, clipboardItem.Name);
                if (string.Equals(clipboardItem.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void PerformDrop(ContentItem targetFolder, IReadOnlyList<ContentItem>? draggedItems, DragDropEffect effect)
    {
        if (!CanDropIntoFolder(targetFolder, draggedItems))
        {
            return;
        }

        var actualEffect = effect == DragDropEffect.None ? GetCurrentDropEffect() : effect;
        var copied = actualEffect.HasFlag(DragDropEffect.Copy) && !actualEffect.HasFlag(DragDropEffect.Move);
        _ = TransferItems(targetFolder, draggedItems!, copied);
    }

    private static bool IsChildPath(string candidateChildPath, string parentPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedChild = Path.GetFullPath(candidateChildPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFileOperationError(string message)
    {
        _pendingOperationError = message;
    }

    private void OnGlobalKeyPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (e.IsHandled || _inlineRenameOverlay.IsOpen)
        {
            return;
        }

        if (e.Tracker.IsControlDown && e.Key == Keys.F)
        {
            FocusSearchBox();
            e.SetHandledBy(_window, true);
            return;
        }

        if (e.Tracker.IsAltDown)
        {
            switch (e.Key)
            {
                case Keys.Left:
                    GoBack();
                    e.SetHandledBy(_window, true);
                    return;

                case Keys.Right:
                    GoForward();
                    e.SetHandledBy(_window, true);
                    return;
            }
        }

        switch (e.Key)
        {
            case Keys.F5:
                Refresh();
                e.SetHandledBy(_window, true);
                return;
        }

        if (_window.Desktop.FocusedKeyboardHandler is MGTextBox)
        {
            return;
        }

        if (e.Key == Keys.Back)
        {
            GoUp();
            e.SetHandledBy(_window, true);
        }
    }

    private void FocusSearchBox()
    {
        if (_searchBox == null)
        {
            return;
        }

        _searchBox.RequestFocus();
        _searchBox.SelectAll();
    }

    private void OnDeleteSelectedItemsRequested()
    {
        var selectedItems = GetSelectedItems();
        if (selectedItems.Count == 0)
        {
            return;
        }

        if (selectedItems.Count == 1)
        {
            OnDeleteItemRequested(selectedItems[0]);
            return;
        }

        if (FormsMessageBox.Show($"Delete {selectedItems.Count} items?", "Content Browser", FormsMessageBoxButtons.YesNo, FormsMessageBoxIcon.Warning) != FormsDialogResult.Yes)
        {
            return;
        }

        var itemsToDelete = selectedItems.OrderByDescending(item => item.FullPath.Length).ToList();
        for (int i = 0; i < itemsToDelete.Count; i++)
        {
            InvalidateThumbnailForItem(itemsToDelete[i]);
        }

        var sourcePaths = new List<string>(itemsToDelete.Count);
        for (int i = 0; i < itemsToDelete.Count; i++)
        {
            sourcePaths.Add(itemsToDelete[i].FullPath);
        }

        var undoViewState = CaptureViewState();
        if (_fileOperationService.TryDeleteOperation(sourcePaths, out var operation))
        {
            var executeViewState = CreateViewState(GetFolderPathAfterDelete(itemsToDelete), operation.SelectionAfterExecute);
            ExecuteHistoryOperation("Delete Items", operation, executeViewState, undoViewState);
            for (int i = 0; i < itemsToDelete.Count; i++)
            {
                FileDeleted?.Invoke(itemsToDelete[i]);
                Events.RaiseFileDeleted(itemsToDelete[i]);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Finds a folder in the tree by path.</summary>
    private static ContentItem? FindFolder(ContentItem root, string fullPath)
    {
        if (string.Equals(root.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var sub in root.SubFolders)
        {
            var found = FindFolder(sub, fullPath);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Selects the tree node corresponding to the given folder.</summary>
    private void SelectTreeNode(ContentItem? folder)
    {
        if (folder == null)
        {
            return;
        }

        foreach (var kvp in _itemToFolder)
        {
            if (string.Equals(kvp.Value.FullPath, folder.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                _treeView.SelectedItem = kvp.Key;
                return;
            }
        }
    }

    private bool TransferItems(ContentItem targetFolder, IReadOnlyList<ContentItem> items, bool copied)
    {
        var sourcePaths = new List<string>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            sourcePaths.Add(items[i].FullPath);
        }

        var undoViewState = CaptureViewState();
        ReversibleFileOperation operation;
        bool succeeded = copied
            ? _fileOperationService.TryCopyOperation(sourcePaths, targetFolder.FullPath, out operation)
            : _fileOperationService.TryMoveOperation(sourcePaths, targetFolder.FullPath, out operation);
        if (!succeeded)
        {
            return false;
        }

        string? executeFolderPath = _currentFolder?.FullPath;
        if (!copied)
        {
            executeFolderPath = TranslateFolderPath(_currentFolder?.FullPath, sourcePaths, operation.SelectionAfterExecute);
        }

        var executeViewState = CreateViewState(executeFolderPath, operation.SelectionAfterExecute);
        ExecuteHistoryOperation(copied ? "Copy Items" : "Move Items", operation, executeViewState, undoViewState);

        if (!copied)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Parent == null)
                {
                    continue;
                }

                Events.RaiseFileMoved(items[i], items[i].Parent);
            }
        }

        return true;
    }

    private static string PredictDestinationPath(ContentItem targetFolder, ContentItem item)
    {
        var baseName = item.IsDirectory ? item.Name : Path.GetFileNameWithoutExtension(item.Name);
        var extension = item.IsDirectory ? string.Empty : Path.GetExtension(item.Name);
        var candidatePath = Path.Combine(targetFolder.FullPath, item.Name);
        var suffix = 1;

        while (Directory.Exists(candidatePath) || File.Exists(candidatePath))
        {
            var uniqueName = item.IsDirectory
                ? $"{baseName} ({suffix++})"
                : $"{baseName} ({suffix++}){extension}";
            candidatePath = Path.Combine(targetFolder.FullPath, uniqueName);
        }

        return candidatePath;
    }

    private bool HasClipboardItems => _clipboardPaths.Count > 0;

    private void SetClipboardItems(IReadOnlyList<ContentItem> items, bool moveOperation)
    {
        _clipboardPaths.Clear();
        foreach (var item in items)
        {
            _clipboardPaths.Add(item.FullPath);
        }

        _clipboardMoveOperation = moveOperation;
    }

    private void ClearClipboardItems()
    {
        _clipboardPaths.Clear();
        _clipboardMoveOperation = false;
    }

    private IReadOnlyList<ContentItem> ResolveClipboardItems()
    {
        var items = new List<ContentItem>();
        if (_rootItem == null)
        {
            return items;
        }

        foreach (var path in _clipboardPaths)
        {
            var item = FindItemByPath(_rootItem, path);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private IReadOnlyList<ContentItem> GetContextItems(ContentItem? item)
    {
        if (item == null)
        {
            return Array.Empty<ContentItem>();
        }

        var selectedItems = GetSelectedItems();
        if (selectedItems.Count == 0)
        {
            return new[] { item };
        }

        foreach (var selectedItem in selectedItems)
        {
            if (string.Equals(selectedItem.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return selectedItems;
            }
        }

        return new[] { item };
    }

    private ContentBrowserViewState CaptureViewState()
    {
        var selectionPaths = new List<string>();
        var selectedItems = GetSelectedItems();
        for (int i = 0; i < selectedItems.Count; i++)
        {
            selectionPaths.Add(selectedItems[i].FullPath);
        }

        return new ContentBrowserViewState(_currentFolder?.FullPath, selectionPaths);
    }

    private ContentBrowserViewState CreateViewState(string? folderPath, IReadOnlyList<string> selectionPaths)
        => new(folderPath, selectionPaths);

    private void RestoreViewState(ContentBrowserViewState viewState)
    {
        _pendingCurrentFolderPath = viewState.FolderPath;
        SetPendingSelectionPaths(viewState.SelectionPaths);
        RebuildTree();
    }

    private void SetPendingSelectionPaths(IReadOnlyList<string> selectionPaths)
    {
        if (selectionPaths == null || selectionPaths.Count == 0)
        {
            _pendingSelectionPaths = null;
            return;
        }

        _pendingSelectionPaths = new List<string>(selectionPaths.Count);
        for (int i = 0; i < selectionPaths.Count; i++)
        {
            _pendingSelectionPaths.Add(selectionPaths[i]);
        }
    }

    private void ExecuteHistoryOperation(
        string description,
        ReversibleFileOperation operation,
        ContentBrowserViewState executeViewState,
        ContentBrowserViewState undoViewState)
    {
        EditorHistoryService.Current.Execute(
            EditorHistoryContext.ContentBrowser,
            new ExecutedContentBrowserCommand(
                description,
                _fileOperationService,
                operation,
                () => RestoreViewState(executeViewState),
                () => RestoreViewState(undoViewState)));
    }

    private string? GetFolderPathAfterDelete(IReadOnlyList<ContentItem> removedItems)
    {
        if (_currentFolder == null)
        {
            return null;
        }

        string currentFolderPath = _currentFolder.FullPath;
        for (int i = 0; i < removedItems.Count; i++)
        {
            if (IsSamePathOrDescendant(currentFolderPath, removedItems[i].FullPath))
            {
                return removedItems[i].Parent?.FullPath ?? _rootItem?.FullPath;
            }
        }

        return currentFolderPath;
    }

    private static string? TranslateFolderPath(string? folderPath, string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !IsSamePathOrDescendant(folderPath, sourcePath))
        {
            return folderPath;
        }

        if (string.Equals(folderPath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return destinationPath;
        }

        string relativeSuffix = folderPath[sourcePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativeSuffix)
            ? destinationPath
            : Path.Combine(destinationPath, relativeSuffix);
    }

    private static string? TranslateFolderPath(string? folderPath, IReadOnlyList<string> sourcePaths, IReadOnlyList<string> destinationPaths)
    {
        string? translatedPath = folderPath;
        int count = Math.Min(sourcePaths.Count, destinationPaths.Count);
        for (int i = 0; i < count; i++)
        {
            translatedPath = TranslateFolderPath(translatedPath, sourcePaths[i], destinationPaths[i]);
        }

        return translatedPath;
    }

    private static bool IsSamePathOrDescendant(string path, string rootPath)
    {
        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedRootPath = rootPath.EndsWith(Path.DirectorySeparatorChar) || rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildPropertiesText(ContentItem item)
    {
        var relativePath = item.FullPath;
        var rootPath = GetConfiguredRootPath();
        if (!string.IsNullOrWhiteSpace(rootPath)
            && relativePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = Path.GetRelativePath(rootPath, item.FullPath);
        }

        return string.Join(Environment.NewLine, new[]
        {
            $"Name: {item.Name}",
            $"Type: {ContentItemDisplay.GetTypeLabel(item)}",
            $"Path: {relativePath}",
            $"Full path: {item.FullPath}",
            $"Size: {(item.IsDirectory ? "-" : ContentItemDisplay.FormatSize(item.Size))}",
            $"Modified: {(item.LastModified == default ? "-" : item.LastModified.ToString("yyyy-MM-dd HH:mm"))}",
        });
    }

    private void AppendContextMenuExtensions(MGContextMenu menu, ContentItem item)
    {
        if (!_contextMenuExtensions.TryGetValue(item.Type, out var extensions) || extensions.Count == 0)
        {
            return;
        }

        menu.AddSeparator();
        foreach (var extension in extensions)
        {
            menu.AddButton(extension.Label, _ => extension.Action(item));
        }
    }

    private string GetConfiguredRootPath()
    {
        var configuredRoot = string.IsNullOrWhiteSpace(Config.RootDirectory)
            ? EngineEnvironment.ProjectPath
            : Config.RootDirectory;

        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(configuredRoot) || string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return configuredRoot;
        }

        return Path.GetFullPath(Path.Combine(EngineEnvironment.ProjectPath, configuredRoot));
    }

    private bool ShouldIncludeItem(ContentItem item)
    {
        if (!Config.ShowHiddenFiles && item.Name.StartsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        if (item.IsDirectory)
        {
            foreach (var excludedDirectory in Config.ExcludedDirectories)
            {
                if (string.Equals(item.Name, excludedDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var excludedExtension in Config.ExcludedExtensions)
        {
            if (string.Equals(item.Extension, NormalizeExtension(excludedExtension), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}";
    }

    private bool TryCommitInlineRename(ContentItem item, string newName)
    {
        var parentDirectory = Path.GetDirectoryName(item.FullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return false;
        }

        var newFullPath = Path.Combine(parentDirectory, newName);
        InvalidateThumbnailForItem(item);
        var oldName = item.Name;
        var undoViewState = CaptureViewState();
        if (!_fileOperationService.TryRenameOperation(item.FullPath, newName, out var operation))
        {
            return false;
        }

        var executeFolderPath = TranslateFolderPath(_currentFolder?.FullPath, item.FullPath, newFullPath);
        var executeViewState = CreateViewState(executeFolderPath, operation.SelectionAfterExecute);
        ExecuteHistoryOperation("Rename", operation, executeViewState, undoViewState);

        FileRenamed?.Invoke(item, oldName);
        Events.RaiseFileRenamed(item, oldName);
        return true;
    }

    private bool TryGetRenameAnchorBounds(ContentItem item, out Rectangle anchorBounds)
    {
        var selectedFromView = GetSelectedItem();
        if (selectedFromView != null && string.Equals(selectedFromView.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase)
            && _activeContentView.TryGetPrimarySelectionBounds(out anchorBounds))
        {
            return true;
        }

        if (_treeView.SelectedItem != null
            && _itemToFolder.TryGetValue(_treeView.SelectedItem, out var selectedFolder)
            && string.Equals(selectedFolder.FullPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            var headerBounds = _treeView.SelectedItem.HeaderContent?.ActualLayoutBounds ?? Rectangle.Empty;
            if (!headerBounds.IsEmpty)
            {
                anchorBounds = headerBounds;
                return true;
            }
        }

        anchorBounds = Rectangle.Empty;
        return false;
    }

    private static ContentItem? FindItemByPath(ContentItem root, string fullPath)
    {
        if (string.Equals(root.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var found = FindItemByPath(child, fullPath);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnThumbnailReady(string path, Texture2D texture, Point sourceSize)
    {
        if (_rootItem == null)
        {
            return;
        }

        var item = FindItemByPath(_rootItem, path);
        if (item == null)
        {
            return;
        }

        item.Thumbnail = texture;
        _gridView.RefreshItemPresentation(item);

        if (_tooltipPreviewImages.TryGetValue(path, out var previewImages))
        {
            foreach (var previewImage in previewImages)
            {
                previewImage.Source = new MGTextureData(EditorIcons.AsImage(texture)!);
            }
        }

        if (_tooltipDimensionTexts.TryGetValue(path, out var dimensionLabels))
        {
            foreach (var dimensionLabel in dimensionLabels)
            {
                dimensionLabel.Text = $"Dimensions: {sourceSize.X} x {sourceSize.Y}";
            }
        }
    }

    private void InvalidateThumbnailForItem(ContentItem item)
    {
        if (item == null)
        {
            return;
        }

        _thumbnailCache.Invalidate(item.FullPath);
        item.Thumbnail = null;

        foreach (var child in item.Children)
        {
            InvalidateThumbnailForItem(child);
        }
    }

    private void ConfigureItemToolTip(ContentItem item, MGElement host)
    {
        var tooltip = new MGToolTip(_window, host, item.Type == ContentItemType.Texture ? 280 : 260, item.Type == ContentItemType.Texture ? 340 : 190)
        {
            ShowDelayOverride = TimeSpan.FromMilliseconds(180),
        };

        var panel = new MGStackPanel(_window, Orientation.Vertical)
        {
            Padding = new Thickness(10),
            Spacing = 6,
        };

        var previewResult = _thumbnailCache.GetOrRequest(item, GetIconForType(item.Type));
        if (previewResult.Texture != null)
        {
            var preview = new MGImage(_window, EditorIcons.AsImage(previewResult.Texture)!, Stretch: Stretch.Uniform)
            {
                PreferredWidth = 200,
                PreferredHeight = 200,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            panel.TryAddChild(preview);
            RegisterTooltipPreview(item.FullPath, preview);
        }

        panel.TryAddChild(new MGTextBlock(_window, item.Name) { IsBold = true, WrapText = true });
        panel.TryAddChild(new MGTextBlock(_window, $"Type: {ContentItemDisplay.GetTypeLabel(item)}"));
        panel.TryAddChild(new MGTextBlock(_window, $"Path: {ContentItemDisplay.GetRelativePath(GetConfiguredRootPath(), item)}") { WrapText = true });
        panel.TryAddChild(new MGTextBlock(_window, $"Size: {(item.IsDirectory ? "-" : ContentItemDisplay.FormatSize(item.Size))}"));

        if (item.Type == ContentItemType.Texture)
        {
            var dimensions = previewResult.SourceSize.HasValue
                ? $"Dimensions: {previewResult.SourceSize.Value.X} x {previewResult.SourceSize.Value.Y}"
                : "Dimensions: loading...";
            var dimensionsText = new MGTextBlock(_window, dimensions);
            panel.TryAddChild(dimensionsText);
            RegisterTooltipDimensions(item.FullPath, dimensionsText);
        }

        panel.TryAddChild(new MGTextBlock(_window, $"Modified: {(item.LastModified == default ? "-" : item.LastModified.ToString("yyyy-MM-dd HH:mm"))}"));
        tooltip.SetContent(panel);
        host.ToolTip = tooltip;
    }

    private void RegisterTooltipPreview(string path, MGImage preview)
    {
        if (!_tooltipPreviewImages.TryGetValue(path, out var previews))
        {
            previews = new List<MGImage>();
            _tooltipPreviewImages[path] = previews;
        }

        previews.Add(preview);
    }

    private void RegisterTooltipDimensions(string path, MGTextBlock dimensionsLabel)
    {
        if (!_tooltipDimensionTexts.TryGetValue(path, out var labels))
        {
            labels = new List<MGTextBlock>();
            _tooltipDimensionTexts[path] = labels;
        }

        labels.Add(dimensionsLabel);
    }

    private void ClearTooltipRegistrations()
    {
        _tooltipPreviewImages.Clear();
        _tooltipDimensionTexts.Clear();
    }

    private MGBorder WrapPanelSurface(MGElement content, Color backgroundColor, Thickness? padding = null)
    {
        var border = new MGBorder(_window, new Thickness(1), new MGUniformBorderBrush(new MGSolidFillBrush(PanelBorderColor)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(backgroundColor)),
            Padding = padding ?? new Thickness(4),
            CornerRadius = new MGCornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        border.SetContent(content);
        return border;
    }
}
