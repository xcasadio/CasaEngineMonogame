using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Log;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Engine;
using CasaEngine.Framework.GUI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class UIScreenPreviewPanel
{
    private readonly MGWindow _window;
    private readonly UIScreenXamlParser _xamlParser = new();
    private readonly UIScreenPreviewBuilder _previewBuilder = new();

    private MGDockPanel? _root;
    private MGTextBlock? _titleText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGBorder? _previewSurface;
    private IReadOnlyDictionary<DocumentNodeId, MGElement> _nodeMap = new Dictionary<DocumentNodeId, MGElement>();
    private UIScreenDocument? _currentDocument;
    private readonly object _reloadSync = new();
    private string? _loadedAssetFilePath;
    private string? _loadedSourceXamlPath;
    private bool _reloadRequested;
    private string _reloadReason = string.Empty;
    private FileSystemWatcher? _assetWatcher;
    private FileSystemWatcher? _sourceXamlWatcher;

    // ── drag-to-move / drag-to-resize state ──────────────────────────────
    private DocumentNodeId? _draggingNodeId;
    private bool _isDraggingResize;
    private const int ResizeHandleSize = 12;
    private const int MinDragThreshold = 3;

    // ── resolution presets ───────────────────────────────────────────────
    private int _previewWidth = 1280;
    private int _previewHeight = 720;

    private static readonly (string Label, int W, int H)[] ResolutionPresets =
    {
        ("1280×720",  1280, 720),
        ("1920×1080", 1920, 1080),
        ("375×667",   375,  667),
        ("768×1024",  768,  1024),
    };

    /// <summary>When true, a grid overlay is drawn over the preview surface by the editor.</summary>
    public bool ShowGrid { get; private set; }

    /// <summary>Returns the screen-space bounds of the preview surface, or null if not yet created.</summary>
    public Microsoft.Xna.Framework.Rectangle? PreviewSurfaceBounds
        => _previewSurface?.LayoutBounds;

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

        toolbar.TryAddChild(resolutionRow);

        _statusText = new MGTextBlock(_window, "Open a UIScreen asset from the Content Browser.")
        {
            WrapText = true,
            Margin = new Thickness(8, 4, 8, 8),
        };

        _previewSurface = new MGBorder(_window)
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(new Color(24, 28, 36))),
            Padding = new Thickness(16),
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _previewSurface.MouseHandler.LMBPressedInside += OnPreviewClicked;
        _previewSurface.MouseHandler.DragStart += OnPreviewDragStart;
        _previewSurface.MouseHandler.DragEnd += OnPreviewDragEnd;

        var previewScrollViewer = new MGScrollViewer(_window);
        previewScrollViewer.SetContent(_previewSurface);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(previewScrollViewer, Dock.Top);
        return _root;
    }

    /// <summary>Fired after the document is successfully parsed, or <c>null</c> when load fails.</summary>
    public event Action<UIScreenDocument?>? DocumentLoaded;

    /// <summary>The document currently loaded into this panel, or null if nothing is open.</summary>
    public UIScreenDocument? CurrentDocument => _currentDocument;

    /// <summary>
    /// Returns the screen-space <see cref="Microsoft.Xna.Framework.Rectangle"/> of
    /// the element mapped to <paramref name="nodeId"/>, or null if not found.
    /// </summary>
    public Microsoft.Xna.Framework.Rectangle? GetElementBounds(DocumentNodeId nodeId)
        => _nodeMap.TryGetValue(nodeId, out var element) ? element.LayoutBounds : null;

    /// <summary>Fired when the user clicks a control in the preview. Contains the best-fit <see cref="DocumentNodeId"/>, or null if no match.</summary>
    public event Action<DocumentNodeId?>? NodePicked;

    /// <summary>Fired when the user drags a control to a new position. Args: nodeId, deltaX, deltaY.</summary>
    public event Action<DocumentNodeId, int, int>? NodeMoveRequested;

    /// <summary>Fired when the user drags the resize handle. Args: nodeId, deltaWidth, deltaHeight.</summary>
    public event Action<DocumentNodeId, int, int>? NodeResizeRequested;

    /// <summary>The node that is currently selected in the editor, used to scope drag operations.</summary>
    public DocumentNodeId? SelectedNodeId { get; set; }

    /// <summary>
    /// Rebuilds the preview from an already-parsed document without re-reading from disk.
    /// Called after in-memory edits (e.g. node deletion).
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
        string? reloadReason = null;

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

    private void ConfigureWatcher(ref FileSystemWatcher? watcher, string filePath)
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

    private void OnPreviewClicked(object? sender, BaseMousePressedEventArgs e)
    {
        var click = e.Position;

        DocumentNodeId? bestId = null;
        var bestArea = int.MaxValue;
        var bestDepth = -1;

        foreach (var (nodeId, element) in _nodeMap)
        {
            var bounds = element.LayoutBounds;
            if (!bounds.Contains(click))
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

    // ─────────────────────────────────────────────────────────────────────
    //  Drag — move and resize
    // ─────────────────────────────────────────────────────────────────────

    private void OnPreviewDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        if (!e.IsLMB || !SelectedNodeId.HasValue)
        {
            _draggingNodeId = null;
            return;
        }

        var nodeId = SelectedNodeId.Value;
        var bounds = GetElementBounds(nodeId);
        if (bounds == null)
        {
            _draggingNodeId = null;
            return;
        }

        var r = bounds.Value;
        var handleZone = new Microsoft.Xna.Framework.Rectangle(
            r.Right - ResizeHandleSize, r.Bottom - ResizeHandleSize,
            ResizeHandleSize, ResizeHandleSize);

        _isDraggingResize = handleZone.Contains(e.Position);
        _draggingNodeId = nodeId;
    }

    private void OnPreviewDragEnd(object? sender, BaseMouseDragEndEventArgs e)
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

        if (_isDraggingResize)
        {
            NodeResizeRequested?.Invoke(nodeId, delta.X, delta.Y);
        }
        else
        {
            NodeMoveRequested?.Invoke(nodeId, delta.X, delta.Y);
        }
    }
}