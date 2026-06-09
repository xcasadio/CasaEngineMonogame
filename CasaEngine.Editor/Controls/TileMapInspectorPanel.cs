using CasaEngine.Editor.Styling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

internal sealed class TileMapInspectorPanel
{
    private readonly MGWindow _window;
    private MGStackPanel _root;
    private TileMapEditorPanel _editorPanel;

    public TileMapInspectorPanel(MGWindow window)
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
            Spacing = 6,
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
            _root.TryAddChild(CreateText("No TileMap layer selected.", 0.75f));
            return;
        }

        _root.TryAddChild(CreateText($"[b]{EscapeMarkup(tileMapData.Name)}[/b]", EditorThemePalette.PrimaryHeaderOpacity));
        AddProperty("Map size", $"{tileMapData.MapSize.Width}x{tileMapData.MapSize.Height}");
        AddProperty("Tilesets", tileMapData.TileSetDataAssetIds.Count.ToString());
        AddProperty("Layers", tileMapData.Layers.Count.ToString());

        var selectedLayer = _editorPanel?.SelectedLayer;
        if (selectedLayer == null || _editorPanel == null)
        {
            _root.TryAddChild(CreateText("Select a layer in Hierarchy.", EditorThemePalette.SecondaryTextOpacity));
            return;
        }

        var layerIndex = _editorPanel.SelectedLayerIndex;
        _root.TryAddChild(CreateText("[b]Selected Layer[/b]", EditorThemePalette.SectionHeaderOpacity));
        AddProperty("Name", string.IsNullOrWhiteSpace(selectedLayer.Name) ? $"Layer {layerIndex}" : selectedLayer.Name!);
        AddProperty("Index", layerIndex.ToString());
        AddProperty("Visible", _editorPanel.IsLayerVisible(layerIndex) ? "True" : "False");
        AddProperty("Z offset", selectedLayer.zOffset.ToString("0.###"));
        AddProperty("Tiles", selectedLayer.tiles.Count.ToString());
        AddProperty("Non-empty", _editorPanel.CountNonEmptyTiles(layerIndex).ToString());
        AddProperty("Collision tiles", _editorPanel.CountCollisionTiles(layerIndex).ToString());
        AddProperty("Tile sources", selectedLayer.HasNonDefaultTileSources() ? "Multi-tileset" : "Default tileset");
        AddProperty("Custom properties", selectedLayer.CustomProperties.Count.ToString());
    }

    private void AddProperty(string label, string value)
    {
        if (_root == null)
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
            PreferredWidth = 110,
            Opacity = EditorThemePalette.SectionLabelOpacity,
            WrapText = true,
        });
        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(value))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            WrapText = true,
        });
        _root.TryAddChild(row);
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