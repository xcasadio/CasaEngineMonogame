using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.ScreenEditor;
using CasaEngine.EditorServices.ScreenEditor.Selection;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

/// <summary>Which corner handle initiated the resize drag.</summary>
public enum ResizeAnchor { TopLeft, TopRight, BottomLeft, BottomRight }

public sealed class UIScreenPreviewPanel
{
    private readonly MGWindow _window;
    private readonly UIScreenXamlParser _xamlParser = new();
    private readonly UIScreenPreviewBuilder _previewBuilder = new();

    private MGDockPanel _root;
    private MGTextBlock _titleText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGTextBlock _coordinateText;   // Q-03: cursor coordinate display
    private MGBorder _previewSurface;
    private IReadOnlyDictionary<DocumentNodeId, MGElement> _nodeMap = new Dictionary<DocumentNodeId, MGElement>();
    private UIScreenSelectionService _selectionService;
    private UIScreenDocument _currentDocument;
    private readonly object _reloadSync = new();
    private string _loadedAssetFilePath;
    private string _loadedSourceXamlPath;
    private bool _reloadRequested;
    private string _reloadReason = string.Empty;
    private FileSystemWatcher _assetWatcher;
    private FileSystemWatcher _sourceXamlWatcher;

    // ── drag-to-move / drag-to-resize state ──────────────────────────────
    private DocumentNodeId? _draggingNodeId;
    private ResizeAnchor? _resizeAnchor;   // null = move drag
    // Anchor captured at LMBPress (before DragStart fires) to avoid re-detection lag
    private DocumentNodeId? _pendingResizeNodeId;
    private ResizeAnchor?   _pendingResizeAnchor;
    private const int ResizeHandleSize = 12;
    private const int MinDragThreshold = 3;

    // ── resolution presets ───────────────────────────────────────────────
    private int _previewWidth = 1280;
    private int _previewHeight = 720;

    // ── zoom ─────────────────────────────────────────────────────────────
    private float _zoomFactor = 1f;
    private const float ZoomStep = 0.25f;
    private const float ZoomMin = 0.25f;
    private const float ZoomMax = 4f;

    private static readonly (string Label, int W, int H)[] ResolutionPresets =
    {
        ("1280×720",  1280, 720),
        ("1920×1080", 1920, 1080),
        ("375×667",   375,  667),
        ("768×1024",  768,  1024),
    };

    /// <summary>Provides selection state for drawing the overlay. Must be set before the panel is drawn.</summary>
    public void SetSelectionService(UIScreenSelectionService service)
        => _selectionService = service;

    /// <summary>When true, a grid overlay is drawn over the preview surface by the editor.</summary>
    public bool ShowGrid { get; private set; }

    /// <summary>When true, drag-to-move snaps to the 32-pixel grid.</summary>
    public bool SnapToGrid { get; private set; }

    /// <summary>Current zoom factor (1 = 100%).</summary>
    public float ZoomFactor => _zoomFactor;

    /// <summary>Returns the screen-space bounds of the preview surface, or null if not yet created.
    /// Uses <see cref="MGElement.ActualLayoutBounds"/> which accounts for clipping by parent containers
    /// (including the ScrollViewer viewport) and the scroll offset.</summary>
    public Rectangle? PreviewSurfaceBounds
        => _previewSurface?.ActualLayoutBounds is { Width: > 0, Height: > 0 } b ? b : (Rectangle?)null;

    public UIScreenPreviewPanel(MGWindow window)
    {
        _window = window;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        var toolbar = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Padding = new Thickness(8),
        };

        _titleText = new MGTextBlock(_window, "[b]Screen Preview[/b]")
        {
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No asset loaded")
        {
            WrapText = true,
            Opacity = 0.8f,
        };

        toolbar.TryAddChild(_titleText);
        toolbar.TryAddChild(_sourceText);

        // ── Resolution presets row ────────────────────────────────────────
        var resolutionRow = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 4 };
        foreach (var (label, w, h) in ResolutionPresets)
        {
            int capturedW = w, capturedH = h;
            var btn = new MGButton(_window, _ => SetPreviewResolution(capturedW, capturedH))
            {
                Padding = new Thickness(4, 2, 4, 2),
            };
            btn.SetContent(new MGTextBlock(_window, label));
            resolutionRow.TryAddChild(btn);
        }

        // ── Grid toggle ───────────────────────────────────────────────────
        var gridToggle = new MGButton(_window, _ => ShowGrid = !ShowGrid)
        {
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(8, 0, 0, 0),
        };
        gridToggle.SetContent(new MGTextBlock(_window, "Grid"));
        resolutionRow.TryAddChild(gridToggle);

        // ── Snap-to-grid toggle ───────────────────────────────────────────
        var snapToggle = new MGButton(_window, _ => SnapToGrid = !SnapToGrid)
        {
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(4, 0, 0, 0),
        };
        snapToggle.SetContent(new MGTextBlock(_window, "Snap"));
        resolutionRow.TryAddChild(snapToggle);

        // ── Zoom controls ─────────────────────────────────────────────────
        var zoomOutBtn = new MGButton(_window, _ => SetZoom(_zoomFactor - ZoomStep))
        {
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(8, 0, 0, 0),
        };
        zoomOutBtn.SetContent(new MGTextBlock(_window, "−"));
        resolutionRow.TryAddChild(zoomOutBtn);

        var zoomResetBtn = new MGButton(_window, _ => SetZoom(1f))
        {
            Padding = new Thickness(4, 2, 4, 2),
        };
        zoomResetBtn.SetContent(new MGTextBlock(_window, "100%"));
        resolutionRow.TryAddChild(zoomResetBtn);

        var zoomInBtn = new MGButton(_window, _ => SetZoom(_zoomFactor + ZoomStep))
        {
            Padding = new Thickness(4, 2, 4, 2),
        };
        zoomInBtn.SetContent(new MGTextBlock(_window, "+"));
        resolutionRow.TryAddChild(zoomInBtn);

        // ── Export PNG ────────────────────────────────────────────────────
        var exportBtn = new MGButton(_window, _ => ExportAsPng())
        {
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(8, 0, 0, 0),
        };
        exportBtn.SetContent(new MGTextBlock(_window, "Export PNG"));
        resolutionRow.TryAddChild(exportBtn);

        toolbar.TryAddChild(resolutionRow);

        // ── Coordinate display (Q-03) ─────────────────────────────────────
        _coordinateText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(4, 0, 4, 0),
            Opacity = 0.7f,
            FontSize = 10,
            WrapText = false,
            MinWidth = 120,
            HasStableTextFootprint = true,
        };
        toolbar.TryAddChild(_coordinateText);

        _statusText = new MGTextBlock(_window, "Open a UIScreen asset from the Content Browser.")
        {
            WrapText = true,
            Margin = new Thickness(8, 4, 8, 8),
        };

        _previewSurface = new MGBorder(_window)
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.OverlayPopupBackground)),
            Padding = new Thickness(16),
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _previewSurface.MouseHandler.LMBPressedInside += OnPreviewClicked;
        _previewSurface.MouseHandler.DragStart += OnPreviewDragStart;
        _previewSurface.MouseHandler.DragEnd += OnPreviewDragEnd;
        _previewSurface.MouseHandler.MovedInside += OnPreviewMouseMoved;
        _previewSurface.OnEndingDraw += OnPreviewSurfaceEndingDraw;

        var previewScrollViewer = new MGScrollViewer(_window);
        previewScrollViewer.SetContent(_previewSurface);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(previewScrollViewer, Dock.Top);
        return _root;
    }

    /// <summary>Fired after the document is successfully parsed, or <c>null</c> when load fails.</summary>
    public event Action<UIScreenDocument> DocumentLoaded;

    /// <summary>The document currently loaded into this panel, or null if nothing is open.</summary>
    public UIScreenDocument CurrentDocument => _currentDocument;

    /// <summary>
    /// Returns the screen-space <see cref="Microsoft.Xna.Framework.Rectangle"/> of
    /// the element mapped to <paramref name="nodeId"/>, or null if not found or not visible.
    /// Uses <see cref="MGElement.ActualLayoutBounds"/> which is clipped to the visible viewport
    /// and accounts for the scroll offset — Width/Height will be 0 when fully scrolled out.
    /// </summary>
    public Rectangle? GetElementBounds(DocumentNodeId nodeId)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var element))
            return null;
        var b = element.ActualLayoutBounds;
        return b.Width > 0 && b.Height > 0 ? b : null;
    }

    /// <summary>Fired when the user clicks a control in the preview. Contains the best-fit <see cref="DocumentNodeId"/>, or null if no match.</summary>
    public event Action<DocumentNodeId?> NodePicked;

    /// <summary>Fired when the user drags a control to a new position. Args: nodeId, deltaX, deltaY.</summary>
    public event Action<DocumentNodeId, int, int> NodeMoveRequested;

    /// <summary>Fired when the user drags a resize handle. Args: nodeId, anchor, deltaX, deltaY.</summary>
    public event Action<DocumentNodeId, ResizeAnchor, int, int> NodeResizeRequested;

    /// <summary>The node that is currently selected in the editor, used to scope drag operations.</summary>
    public DocumentNodeId? SelectedNodeId { get; set; }

    // ─────────────────────────────────────────────────────────────────────
    //  R-01: Incremental property update (avoids full preview rebuild)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to apply a single property change directly to the live <see cref="MGElement"/>
    /// that maps to <paramref name="nodeId"/>, without triggering a full preview rebuild.
    /// <para/>
    /// Returns <c>true</c> when the patch was applied.
    /// Returns <c>false</c> when the element or property is not patchable — callers should
    /// fall back to <see cref="RefreshPreviewOnly"/> or <see cref="LoadDocumentDirectly"/>.
    /// </summary>
    public bool TryApplyPropertyUpdate(DocumentNodeId nodeId, string propertyName, string value)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var element))
        {
            return false;
        }

        return MGElementPropertyApplier.TryApply(element, propertyName, value);
    }

    /// <summary>
    /// Rebuilds the preview from the current document without firing <see cref="DocumentLoaded"/>
    /// (hierarchy and inspector panels are NOT notified).  Used for lightweight refresh after
    /// property edits that fall through the incremental path.
    /// </summary>
    public void RefreshPreviewOnly()
    {
        if (_currentDocument == null)
        {
            return;
        }

        CreateContent();

        try
        {
            var (previewWindow, nodeMap) = _previewBuilder.BuildWithMapping(_window.GetDesktop(), _currentDocument, _previewWidth, _previewHeight);
            _nodeMap = nodeMap;
            previewWindow.IsHitTestVisible = false;
            _previewSurface!.SetContent(previewWindow);
            UIDesignModeContext.EnterDesignTime();
        }
        catch (Exception ex)
        {
            _nodeMap = new Dictionary<DocumentNodeId, MGElement>();
            ShowPreviewError("Preview refresh failed", ex.Message, string.Empty);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Q-06: Zoom
    // ─────────────────────────────────────────────────────────────────────

    private void SetZoom(float factor)
    {
        _zoomFactor = Math.Clamp(factor, ZoomMin, ZoomMax);

        if (_previewSurface != null)
        {
            var scaledW = (int)(_previewWidth  * _zoomFactor);
            var scaledH = (int)(_previewHeight * _zoomFactor);
            _previewSurface.PreferredWidth  = scaledW;
            _previewSurface.PreferredHeight = scaledH;
        }

        if (_statusText != null)
        {
            _statusText.Text = $"Zoom: {_zoomFactor * 100:F0}%";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Q-08: Export as PNG
    // ─────────────────────────────────────────────────────────────────────

    private void ExportAsPng()
    {
        // Request export via event so Game1 can supply a RenderTarget and GraphicsDevice
        ExportPngRequested?.Invoke();
    }

    /// <summary>Fired when the user requests a PNG export.  Game1 handles the actual rendering.</summary>
    public event Action ExportPngRequested;

    // ─────────────────────────────────────────────────────────────────────
    //  Q-03: Cursor coordinate display
    // ─────────────────────────────────────────────────────────────────────

    private void OnPreviewMouseMoved(object sender, BaseMouseMovedEventArgs e)
    {
        if (_coordinateText == null)
        {
            return;
        }

        var pos = e.CurrentPosition;
        var surface = PreviewSurfaceBounds;
        if (surface.HasValue && surface.Value.Contains(pos))
        {
            var relative = pos - new Point(surface.Value.X, surface.Value.Y);
            _coordinateText.SetText($"x:{relative.X}  y:{relative.Y}", MGTextInvalidationMode.ReflowLocal);
        }
        else
        {
            _coordinateText.SetText(string.Empty, MGTextInvalidationMode.ReflowLocal);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Document loading
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the preview from an already-parsed document without re-reading from disk.
    /// Called after in-memory edits (e.g. node deletion). Fires <see cref="DocumentLoaded"/>.
    /// </summary>
    public void LoadDocumentDirectly(UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        CreateContent();

        try
        {
            var (previewWindow, nodeMap) = _previewBuilder.BuildWithMapping(_window.GetDesktop(), document, _previewWidth, _previewHeight);
            _nodeMap = nodeMap;
            _currentDocument = document;
            previewWindow.IsHitTestVisible = false;
            _previewSurface!.SetContent(previewWindow);
            _statusText!.Text = "Preview rebuilt.";
            UIDesignModeContext.EnterDesignTime();
            DocumentLoaded?.Invoke(document);
        }
        catch (Exception ex)
        {
            _nodeMap = new Dictionary<DocumentNodeId, MGElement>();
            _currentDocument = null;
            DocumentLoaded?.Invoke(null);
            ShowPreviewError("Preview rebuild failed", ex.Message, string.Empty);
            _statusText!.Text = "Preview rebuild failed.";
        }
    }

    public void LoadAsset(UIScreenAsset asset, string assetFilePath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        CreateContent();

        _titleText!.Text = $"[b]{EscapeMarkup(asset.Name)}[/b]";
        _sourceText!.Text = EscapeMarkup($"{asset.FileName} -> {asset.SourceXamlFile}");

        try
        {
            var sourceXamlPath = ResolveSourceXamlPath(asset, assetFilePath);
            ConfigureWatchers(assetFilePath, sourceXamlPath);
            _loadedAssetFilePath = assetFilePath;
            _loadedSourceXamlPath = sourceXamlPath;

            var document = _xamlParser.ParseFile(sourceXamlPath);
            _currentDocument = document;
            DocumentLoaded?.Invoke(document);

            var (previewWindow, nodeMap) = _previewBuilder.BuildWithMapping(_window.GetDesktop(), document, _previewWidth, _previewHeight);
            _nodeMap = nodeMap;
            previewWindow.IsHitTestVisible = false;

            _previewSurface!.SetContent(previewWindow);
            _statusText!.Text = $"Loaded {EscapeMarkup(Path.GetFileName(sourceXamlPath))}";
        }
        catch (Exception ex)
        {
            _nodeMap = new Dictionary<DocumentNodeId, MGElement>();
            _currentDocument = null;
            DocumentLoaded?.Invoke(null);
            ShowPreviewError("Preview unavailable", ex.Message, asset.SourceXamlFile);
            _statusText!.Text = "Preview build failed.";
        }
    }

    public void Update()
    {
        string reloadReason = null;

        lock (_reloadSync)
        {
            if (_reloadRequested)
            {
                _reloadRequested = false;
                reloadReason = _reloadReason;
                _reloadReason = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(reloadReason))
        {
            ReloadFromDisk(reloadReason);
        }
    }

    private void ReloadFromDisk(string reloadReason)
    {
        if (string.IsNullOrWhiteSpace(_loadedAssetFilePath))
        {
            return;
        }

        try
        {
            var asset = ReadAssetFromFile(_loadedAssetFilePath);
            LoadAsset(asset, _loadedAssetFilePath);
            Logs.WriteInfo($"Reloaded UI screen preview '{asset.Name}' ({reloadReason}).");
            _statusText!.Text = $"Reloaded ({EscapeMarkup(reloadReason)})";
        }
        catch (Exception ex)
        {
            Logs.WriteException(new Exception($"Failed to reload UI screen preview '{_loadedAssetFilePath}' ({reloadReason}).", ex));
            ShowPreviewError("Preview reload failed", ex.Message, _loadedSourceXamlPath ?? _loadedAssetFilePath ?? string.Empty);
            _statusText!.Text = "Preview reload failed.";
        }
    }

    private void ShowPreviewError(string title, string message, string sourcePath)
    {
        var errorPanel = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            Padding = new Thickness(8),
            PreferredWidth = 520,
        };

        errorPanel.TryAddChild(new MGTextBlock(_window, $"[b][c=Orange]{EscapeMarkup(title)}[/c][/b]")
        {
            WrapText = true,
        });

        errorPanel.TryAddChild(new MGTextBlock(_window, EscapeMarkup(message))
        {
            WrapText = true,
        });

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            errorPanel.TryAddChild(new MGTextBlock(_window, $"Source: {EscapeMarkup(sourcePath)}")
            {
                WrapText = true,
                Opacity = 0.8f,
            });
        }

        errorPanel.TryAddChild(new MGTextBlock(_window, "Save the asset or its XAML source again to trigger a full preview rebuild.")
        {
            WrapText = true,
            Opacity = 0.85f,
        });

        var clipboardText = string.IsNullOrWhiteSpace(sourcePath)
            ? $"{title}\n{message}"
            : $"{title}\n{message}\nSource: {sourcePath}";

        var copyButton = new MGButton(_window, _ => CopyToClipboard(clipboardText))
        {
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        copyButton.SetContent(new MGTextBlock(_window, "Copy error"));
        errorPanel.TryAddChild(copyButton);

        _previewSurface!.SetContent(errorPanel);
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Forms.Clipboard.SetText(text);
        }
        catch
        {
            // clipboard access can fail in some environments; silently ignore
        }
    }

    private void ConfigureWatchers(string assetFilePath, string sourceXamlPath)
    {
        ConfigureWatcher(ref _assetWatcher, assetFilePath);
        ConfigureWatcher(ref _sourceXamlWatcher, sourceXamlPath);
    }

    private void ConfigureWatcher(ref FileSystemWatcher watcher, string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Watcher path must have a directory.");
        string fileName = Path.GetFileName(fullPath);

        if (watcher != null)
        {
            bool sameWatcher = string.Equals(watcher.Path, directory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(watcher.Filter, fileName, StringComparison.OrdinalIgnoreCase);
            if (sameWatcher)
            {
                return;
            }

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnWatchedFileChanged;
            watcher.Created -= OnWatchedFileChanged;
            watcher.Deleted -= OnWatchedFileChanged;
            watcher.Renamed -= OnWatchedFileRenamed;
            watcher.Dispose();
        }

        watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        watcher.Changed += OnWatchedFileChanged;
        watcher.Created += OnWatchedFileChanged;
        watcher.Deleted += OnWatchedFileChanged;
        watcher.Renamed += OnWatchedFileRenamed;
        watcher.EnableRaisingEvents = true;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        RequestReload($"{e.ChangeType}: {Path.GetFileName(e.FullPath)}");
    }

    private void OnWatchedFileRenamed(object sender, RenamedEventArgs e)
    {
        RequestReload($"Renamed: {Path.GetFileName(e.OldFullPath)} -> {Path.GetFileName(e.FullPath)}");
    }

    private void RequestReload(string reason)
    {
        lock (_reloadSync)
        {
            _reloadRequested = true;
            _reloadReason = reason;
        }
    }

    private static UIScreenAsset ReadAssetFromFile(string assetFilePath)
    {
        var document = JObject.Parse(File.ReadAllText(assetFilePath));
        if (document["source_xaml_file"] == null)
        {
            throw new InvalidOperationException("UIScreen asset is missing 'source_xaml_file'.");
        }

        var asset = new UIScreenAsset();
        asset.Load(document);
        asset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, assetFilePath);
        return asset;
    }

    private static string ResolveSourceXamlPath(UIScreenAsset asset, string assetFilePath)
    {
        if (string.IsNullOrWhiteSpace(asset.SourceXamlFile))
        {
            throw new InvalidOperationException("UIScreen asset is missing 'SourceXamlFile'.");
        }

        if (Path.IsPathRooted(asset.SourceXamlFile))
        {
            if (!File.Exists(asset.SourceXamlFile))
            {
                throw new FileNotFoundException("UIScreen XAML file not found.", asset.SourceXamlFile);
            }

            return asset.SourceXamlFile;
        }

        var assetDirectory = Path.GetDirectoryName(assetFilePath);
        if (!string.IsNullOrWhiteSpace(assetDirectory))
        {
            var relativeToAsset = Path.GetFullPath(Path.Combine(assetDirectory, asset.SourceXamlFile));
            if (File.Exists(relativeToAsset))
            {
                return relativeToAsset;
            }
        }

        var relativeToProject = Path.GetFullPath(Path.Combine(EngineEnvironment.ProjectPath, asset.SourceXamlFile));
        if (File.Exists(relativeToProject))
        {
            return relativeToProject;
        }

        throw new FileNotFoundException("UIScreen XAML file not found.", asset.SourceXamlFile);
    }

    private static string EscapeMarkup(string value)
        => value
            .Replace("[", "\\[")
            .Replace("]", "\\]");

    // ─────────────────────────────────────────────────────────────────────
    //  Resolution preset
    // ─────────────────────────────────────────────────────────────────────

    private void SetPreviewResolution(int width, int height)
    {
        if (_previewWidth == width && _previewHeight == height) return;
        _previewWidth = width;
        _previewHeight = height;
        if (_currentDocument != null)
        {
            LoadDocumentDirectly(_currentDocument);
            _statusText!.Text = $"Preview: {width}×{height}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Preview picking
    // ─────────────────────────────────────────────────────────────────────

    private void OnPreviewClicked(object sender, BaseMousePressedEventArgs e)
    {
        var click = e.Position;

        // Reset any pending resize captured from a previous press.
        _pendingResizeNodeId = null;
        _pendingResizeAnchor = null;

        // If the click lands on a resize handle of the currently-selected element:
        // – capture the anchor now (at press time, so DragStart doesn't need to recompute
        //   against possibly-stale bounds or coordinates);
        // – do NOT re-run the picker so the selection stays on the element whose handle
        //   was clicked.
        if (SelectedNodeId.HasValue)
        {
            var selBounds = GetElementBounds(SelectedNodeId.Value);
            if (selBounds.HasValue)
            {
                var anchor = GetResizeAnchor(selBounds.Value, click);
                if (anchor.HasValue)
                {
                    _pendingResizeNodeId = SelectedNodeId;
                    _pendingResizeAnchor = anchor;
                    return; // Don't re-select; DragStart will use the pending values.
                }
            }
        }

        DocumentNodeId? bestId = null;
        var bestArea = int.MaxValue;
        var bestDepth = -1;

        foreach (var (nodeId, element) in _nodeMap)
        {
            // ActualLayoutBounds est en coordonnées écran et tient compte du scroll /
            // clipping du ScrollViewer. Width=0 / Height=0 = hors du viewport.
            var bounds = element.ActualLayoutBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0 || !bounds.Contains(click))
            {
                continue;
            }

            var area  = bounds.Width * bounds.Height;
            var depth = _currentDocument != null ? GetNodeDepth(_currentDocument, nodeId) : 0;

            // Prefer the node that is smaller in area; break ties by picking the deeper tree node
            if (area < bestArea || (area == bestArea && depth > bestDepth))
            {
                bestId    = nodeId;
                bestArea  = area;
                bestDepth = depth;
            }
        }

        NodePicked?.Invoke(bestId);
    }

    private static int GetNodeDepth(UIScreenDocument document, DocumentNodeId id)
    {
        static int Recurse(UIScreenNode node, DocumentNodeId target, int depth)
        {
            if (node.Id == target)
            {
                return depth;
            }

            foreach (var child in node.Children)
            {
                var result = Recurse(child, target, depth + 1);
                if (result >= 0)
                {
                    return result;
                }
            }

            return -1;
        }

        return document.Root != null ? Recurse(document.Root, id, 0) : -1;
    }

    /// <summary>Returns the <see cref="ResizeAnchor"/> whose hit-zone contains <paramref name="pos"/>,
    /// or <c>null</c> if the position is not on any corner handle.</summary>
    private ResizeAnchor? GetResizeAnchor(Rectangle r, Point pos)
    {
        int h = ResizeHandleSize;
        if (new Rectangle(r.Left,       r.Top,        h, h).Contains(pos)) return ResizeAnchor.TopLeft;
        if (new Rectangle(r.Right - h,  r.Top,        h, h).Contains(pos)) return ResizeAnchor.TopRight;
        if (new Rectangle(r.Left,       r.Bottom - h, h, h).Contains(pos)) return ResizeAnchor.BottomLeft;
        if (new Rectangle(r.Right - h,  r.Bottom - h, h, h).Contains(pos)) return ResizeAnchor.BottomRight;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Drag — move and resize
    // ─────────────────────────────────────────────────────────────────────

    private void OnPreviewDragStart(object sender, BaseMouseDragStartEventArgs e)
    {
        if (!e.IsLMB)
        {
            _draggingNodeId = null;
            return;
        }

        // Fast path: anchor was captured at LMBPress time (most reliable — avoids
        // re-comparing a possibly-stale position against updated layout bounds).
        if (_pendingResizeNodeId.HasValue)
        {
            _draggingNodeId  = _pendingResizeNodeId;
            _resizeAnchor    = _pendingResizeAnchor;
            _pendingResizeNodeId = null;
            _pendingResizeAnchor = null;
            return;
        }

        // Slow path: no pre-captured handle — use selected node for a move drag.
        if (!SelectedNodeId.HasValue)
        {
            _draggingNodeId = null;
            return;
        }

        var nodeId = SelectedNodeId.Value;
        if (GetElementBounds(nodeId) == null)
        {
            _draggingNodeId = null;
            return;
        }

        _resizeAnchor   = null;   // move drag
        _draggingNodeId = nodeId;
    }

    private void OnPreviewDragEnd(object sender, BaseMouseDragEndEventArgs e)
    {
        if (!e.IsLMB || _draggingNodeId == null)
        {
            _draggingNodeId = null;
            return;
        }

        var nodeId = _draggingNodeId.Value;
        _draggingNodeId = null;

        var delta = e.PositionDelta;
        if (Math.Abs(delta.X) < MinDragThreshold && Math.Abs(delta.Y) < MinDragThreshold)
        {
            return; // ignore noise
        }

        if (_resizeAnchor.HasValue)
        {
            NodeResizeRequested?.Invoke(nodeId, _resizeAnchor.Value, delta.X, delta.Y);
        }
        else
        {
            NodeMoveRequested?.Invoke(nodeId, delta.X, delta.Y);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Selection / grid overlay — drawn via MGUI's draw pipeline so
    //  visibility is automatically managed by the element's LayoutBounds.
    //  OnEndingDraw fires only when _previewSurface is actually drawn,
    //  so the overlay never appears on background tabs.
    // ─────────────────────────────────────────────────────────────────────

    private void OnPreviewSurfaceEndingDraw(object sender, MGElement.MGElementDrawEventArgs e)
    {
        if (ShowGrid)
            DrawGridOverlay(e.DA);
        if (_selectionService != null)
            DrawSelectionHighlights(e.DA);
    }

    private void DrawGridOverlay(ElementDrawArgs da)
    {
        var clip = PreviewSurfaceBounds;
        if (clip == null) return;

        var r = clip.Value;
        const int gridStep = 32;
        var gridColor = new Color(255, 255, 255, 30);

        for (int x = r.Left; x <= r.Right; x += gridStep)
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(x, r.Top, 1, r.Height), gridColor);
        for (int y = r.Top; y <= r.Bottom; y += gridStep)
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, y, r.Width, 1), gridColor);
    }

    private void DrawSelectionHighlights(ElementDrawArgs da)
    {
        var primaryId = _selectionService!.SelectedNodeId;
        var selection = _selectionService.MultiSelection;

        void DrawBorder(Rectangle r, Color color, bool withHandles)
        {
            const int t = 2;
            const int h = 8;
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, r.Top, r.Width, t), color);
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, r.Bottom - t, r.Width, t), color);
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, r.Top, t, r.Height), color);
            da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Right - t, r.Top, t, r.Height), color);
            if (withHandles)
            {
                // 4 corner handles
                da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, r.Top, h, h), color);
                da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Right - h, r.Top, h, h), color);
                da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Left, r.Bottom - h, h, h), color);
                da.DT.FillRectangle(Vector2.Zero, new RectangleF(r.Right - h, r.Bottom - h, h, h), color);
            }
        }

        foreach (var selId in selection)
        {
            var bounds = GetElementBounds(selId);
            if (!bounds.HasValue) continue;
            bool isPrimary = selId == primaryId;
            var color = isPrimary ? new Color(0, 120, 215, 200) : new Color(0, 180, 100, 160);
            DrawBorder(bounds.Value, color, isPrimary);
        }

        // Fallback: single selection when multi-selection is empty
        if (selection.Count == 0 && primaryId.HasValue)
        {
            var bounds = GetElementBounds(primaryId.Value);
            if (bounds.HasValue)
                DrawBorder(bounds.Value, new Color(0, 120, 215, 200), true);
        }
    }
}