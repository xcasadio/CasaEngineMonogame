using CasaEngine.Editor.Styling;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

internal sealed class TileMapHierarchyPanel
{
    private readonly MGWindow _window;
    private MGStackPanel _root;
    private TileMapEditorPanel _editorPanel;

    public TileMapHierarchyPanel(MGWindow window)
    {
        _window = window;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _root = new MGStackPanel(_window, Orientation.Vertical)
        {
            Margin = new Thickness(8),
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        RefreshContent();
        return _root;
    }

    public void SetEditorPanel(TileMapEditorPanel editorPanel)
    {
        if (ReferenceEquals(_editorPanel, editorPanel))
        {
            RefreshContent();
            return;
        }

        if (_editorPanel != null)
        {
            _editorPanel.LayersChanged -= OnLayersChanged;
            _editorPanel.SelectedLayerChanged -= OnSelectedLayerChanged;
        }

        _editorPanel = editorPanel;
        if (_editorPanel != null)
        {
            _editorPanel.LayersChanged += OnLayersChanged;
            _editorPanel.SelectedLayerChanged += OnSelectedLayerChanged;
        }

        RefreshContent();
    }

    private void OnLayersChanged()
    {
        RefreshContent();
    }

    private void OnSelectedLayerChanged(int layerIndex)
    {
        RefreshContent();
    }

    private void RefreshContent()
    {
        if (_root == null)
        {
            return;
        }

        _root.TryRemoveAll();
        var tileMapData = _editorPanel?.LoadedTileMap;
        if (tileMapData == null)
        {
            _root.TryAddChild(CreateText("No TileMap selected.", 0.75f));
            return;
        }

        _root.TryAddChild(CreateText($"[b]{EscapeMarkup(tileMapData.Name)}[/b]", EditorThemePalette.PrimaryHeaderOpacity));
        _root.TryAddChild(CreateText($"{tileMapData.MapSize.Width}x{tileMapData.MapSize.Height} - {tileMapData.Layers.Count} layer(s)", EditorThemePalette.SecondaryTextOpacity));

        for (var layerIndex = 0; layerIndex < tileMapData.Layers.Count; layerIndex++)
        {
            _root.TryAddChild(CreateLayerRow(layerIndex));
        }
    }

    private MGElement CreateLayerRow(int layerIndex)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var visibilityCheckBox = new MGCheckBox(_window)
        {
            IsChecked = _editorPanel?.IsLayerVisible(layerIndex) == true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        visibilityCheckBox.OnCheckStateChanged += (_, args) => _editorPanel?.SetLayerVisible(layerIndex, args.NewValue == true);
        row.TryAddChild(visibilityCheckBox);

        var isSelected = _editorPanel?.SelectedLayerIndex == layerIndex;
        var button = new MGButton(_window, _ => _editorPanel?.SelectLayer(layerIndex))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        button.SetContent(new MGTextBlock(_window, $"{(isSelected ? "> " : string.Empty)}{EscapeMarkup(_editorPanel?.GetLayerDisplayName(layerIndex) ?? $"Layer {layerIndex}")}")
        {
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = true,
        });

        var border = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(isSelected ? EditorThemePalette.AccentSelection : EditorThemePalette.PanelBorder)))
        {
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        border.SetContent(button);
        row.TryAddChild(border);
        return row;
    }

    private MGTextBlock CreateText(string text, float opacity)
    {
        return new MGTextBlock(_window, text)
        {
            Opacity = opacity,
            WrapText = true,
        };
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}