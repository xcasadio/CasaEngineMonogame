#nullable enable

using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dInspectorView
{
    private readonly MGWindow _window;

    private MGStackPanel? _root;
    private Animation2dAssetInspectorPanel? _activeInspectorPanel;

    public Animation2dInspectorView(MGWindow window)
    {
        _window = window;
    }

    public void SetInspectorPanel(Animation2dAssetInspectorPanel? inspectorPanel)
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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        if (_activeInspectorPanel == null)
        {
            _root.TryAddChild(new MGTextBlock(_window, "No Animation2D selected.")
            {
                Margin = new Thickness(8, 6, 8, 4),
                Opacity = 0.75f,
                WrapText = true,
            });
            return;
        }

        _root.TryAddChild(_activeInspectorPanel.CreateInspectorContent());
    }
}