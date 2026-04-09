
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

public sealed class MaterialHierarchyPanel
{
    private readonly MGWindow _window;

    private MGStackPanel? _root;
    private MaterialAssetInspectorPanel? _activeInspectorPanel;

    public MaterialHierarchyPanel(MGWindow window)
    {
        _window = window;
    }

    public void SetInspectorPanel(MaterialAssetInspectorPanel? inspectorPanel)
    {
        _activeInspectorPanel = inspectorPanel;
        RefreshContent();
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
        };

        RefreshContent();
        return _root;
    }

    private void RefreshContent()
    {
        if (_root == null)
        {
            return;
        }

        _root.TryRemoveAll();

        MaterialAsset? materialAsset = _activeInspectorPanel?.LoadedMaterialAsset;
        if (materialAsset == null)
        {
            _root.TryAddChild(new MGTextBlock(_window, "No material selected.")
            {
                Opacity = 0.75f,
                WrapText = true,
            });
            return;
        }

        _root.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(materialAsset.Name)}[/b]")
        {
            WrapText = true,
        });

        if (!string.IsNullOrWhiteSpace(_activeInspectorPanel?.LoadedRelativePath))
        {
            _root.TryAddChild(new MGTextBlock(_window, $"Asset: {EscapeMarkup(_activeInspectorPanel.LoadedRelativePath!)}")
            {
                Opacity = 0.8f,
                WrapText = true,
            });
        }

        _root.TryAddChild(new MGTextBlock(_window, "Materials do not expose a hierarchy tree. Use Inspector to edit the active material.")
        {
            Opacity = 0.75f,
            WrapText = true,
        });
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}