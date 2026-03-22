using System;
using System.IO;
using CasaEngine.Core.Log;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Engine;
using CasaEngine.Framework.GUI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
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
    private readonly object _reloadSync = new();
    private string? _loadedAssetFilePath;
    private string? _loadedSourceXamlPath;
    private bool _reloadRequested;
    private string _reloadReason = string.Empty;
    private FileSystemWatcher? _assetWatcher;
    private FileSystemWatcher? _sourceXamlWatcher;

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

        var previewScrollViewer = new MGScrollViewer(_window);
        previewScrollViewer.SetContent(_previewSurface);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(previewScrollViewer, Dock.Top);
        return _root;
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
            var previewWindow = _previewBuilder.Build(_window.GetDesktop(), document);
            previewWindow.IsHitTestVisible = false;

            _previewSurface!.SetContent(previewWindow);
            _statusText!.Text = $"Loaded {EscapeMarkup(Path.GetFileName(sourceXamlPath))}";
        }
        catch (Exception ex)
        {
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
}