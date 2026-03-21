using System;
using System.IO;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Engine;
using CasaEngine.Framework.GUI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

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
            var document = _xamlParser.ParseFile(sourceXamlPath);
            var previewWindow = _previewBuilder.Build(_window.GetDesktop(), document);
            previewWindow.IsHitTestVisible = false;

            _previewSurface!.SetContent(previewWindow);
            _statusText!.Text = $"Loaded {EscapeMarkup(Path.GetFileName(sourceXamlPath))}";
        }
        catch (Exception ex)
        {
            _previewSurface!.SetContent(new MGTextBlock(_window, $"[c=Orange]Preview unavailable[/c]\n{EscapeMarkup(ex.Message)}")
            {
                WrapText = true,
            });

            _statusText!.Text = "Preview build failed.";
        }
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