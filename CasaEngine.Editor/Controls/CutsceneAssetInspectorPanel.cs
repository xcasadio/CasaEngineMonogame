#nullable enable

using System;
using System.Globalization;
using System.IO;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

internal sealed class CutsceneAssetInspectorPanel
{
    private readonly MGWindow _window;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _contentStack;

    private CutsceneAsset? _cutsceneAsset;
    private CutsceneReadOnlyDocument? _document;
    private string? _loadedRelativePath;

    public CutsceneAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    public CutsceneAsset? LoadedCutsceneAsset => _cutsceneAsset;

    public string? LoadedRelativePath => _loadedRelativePath;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Cutscene Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No cutscene asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .cutscene asset from the Content Browser.")
        {
            Margin = new Thickness(8, 0, 8, 8),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _contentStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_contentStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public void LoadAsset(CutsceneAsset cutsceneAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(cutsceneAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _cutsceneAsset = cutsceneAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _document = CutsceneReadOnlyDocumentBuilder.Build(cutsceneAsset);
        RefreshInspector();
    }

    public void RefreshRuntimeSnapshot(CutsceneDebugSnapshot? runtimeSnapshot)
    {
        if (_cutsceneAsset == null)
        {
            return;
        }

        _document = CutsceneReadOnlyDocumentBuilder.Build(_cutsceneAsset, runtimeSnapshot);
        RefreshInspector();
    }

    public static bool TryLoadAsset(string fullPath, out CutsceneAsset cutsceneAsset)
    {
        cutsceneAsset = new CutsceneAsset();

        if (!File.Exists(fullPath)
            || !string.Equals(Path.GetExtension(fullPath), Constants.FileNameExtensions.Cutscene, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            cutsceneAsset.Load(document);
            cutsceneAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(cutsceneAsset.FileName)
                            ?? AssetCatalog.GetByFileName(cutsceneAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                cutsceneAsset.Name = assetInfo.Name;
                cutsceneAsset.AssetId = assetInfo.Id;
                cutsceneAsset.FileName = assetInfo.FileName;
            }
            else
            {
                cutsceneAsset.AssetId = cutsceneAsset.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshInspector()
    {
        if (_headerText == null || _sourceText == null || _statusText == null || _contentStack == null)
        {
            return;
        }

        _contentStack.TryRemoveAll();
        if (_document == null)
        {
            _headerText.Text = "[b]Cutscene Inspector[/b]";
            _sourceText.Text = "No cutscene asset loaded.";
            _statusText.Text = "Open a .cutscene asset from the Content Browser.";
            return;
        }

        _headerText.Text = $"[b]{EscapeMarkup(_document.AssetName)}[/b]";
        _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "No source path."
            : EscapeMarkup(_loadedRelativePath);
        _statusText.Text = _document.ValidationMessages.Count == 0
            ? "Read-only V1 cutscene asset. Validation OK."
            : $"Read-only V1 cutscene asset. {_document.ValidationMessages.Count.ToString(CultureInfo.InvariantCulture)} validation issue(s).";

        AddProperty("Editable", _document.CanEdit ? "True" : "False");
        AddProperty("Runtime state", _document.RuntimeState.ToString());
        AddProperty("Active coroutines", _document.ActiveCoroutines.Count.ToString(CultureInfo.InvariantCulture));

        AddSection("Actions");
        if (_document.RootAction == null)
        {
            AddText("No root action.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            AddActionNode(_document.RootAction, 0);
        }

        AddSection("Validation");
        if (_document.ValidationMessages.Count == 0)
        {
            AddText("No validation warnings or errors.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (int index = 0; index < _document.ValidationMessages.Count; index++)
            {
                var message = _document.ValidationMessages[index];
                AddText($"{message.Severity}: {EscapeMarkup(message.Path)} - {EscapeMarkup(message.Message)}", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }

        AddSection("Runtime Coroutines");
        if (_document.ActiveCoroutines.Count == 0)
        {
            AddText("No active cutscene coroutine in the current world snapshot.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (int index = 0; index < _document.ActiveCoroutines.Count; index++)
            {
                var coroutine = _document.ActiveCoroutines[index];
                string remainingTime = coroutine.RemainingTime.HasValue
                    ? coroutine.RemainingTime.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    : "<none>";
                AddText($"#{coroutine.Id.ToString(CultureInfo.InvariantCulture)} {EscapeMarkup(coroutine.Name ?? "<unnamed>")} state={EscapeMarkup(coroutine.State)} paused={coroutine.IsPaused} instruction={EscapeMarkup(coroutine.CurrentInstruction ?? "<none>")} remaining={remainingTime}", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }
    }

    private void AddActionNode(CutsceneReadOnlyActionNode node, int depth)
    {
        string indent = new(' ', depth * 2);
        AddText($"{indent}- {EscapeMarkup(node.Type)}  {EscapeMarkup(node.Path)}", EditorThemePalette.PrimaryHeaderOpacity);

        for (int index = 0; index < node.Properties.Count; index++)
        {
            var property = node.Properties[index];
            AddText($"{indent}  {EscapeMarkup(property.Name)}: {EscapeMarkup(property.Value)}", EditorThemePalette.SecondaryTextOpacity);
        }

        for (int index = 0; index < node.Children.Count; index++)
        {
            AddActionNode(node.Children[index], depth + 1);
        }
    }

    private void AddSection(string title)
    {
        AddText($"[b]{EscapeMarkup(title)}[/b]", EditorThemePalette.SectionHeaderOpacity);
    }

    private void AddProperty(string label, string value)
    {
        if (_contentStack == null)
        {
            return;
        }

        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 130,
            Opacity = EditorThemePalette.SectionLabelOpacity,
            WrapText = true,
        });
        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(value))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            WrapText = true,
        });
        _contentStack.TryAddChild(row);
    }

    private void AddText(string text, float opacity)
    {
        _contentStack?.TryAddChild(new MGTextBlock(_window, text)
        {
            Opacity = opacity,
            WrapText = true,
        });
    }

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}