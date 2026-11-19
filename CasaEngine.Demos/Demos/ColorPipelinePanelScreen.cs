using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The solid interface panel of <see cref="ColorPipelineDemo"/>: user interface that must keep its colour whatever the
/// colour pipeline. Its tree lives in `Content/Screens/color-pipeline-panel.xaml`; the fill colour is applied here because
/// the demo reads back the colour it posed.
/// </summary>
internal sealed class ColorPipelinePanelScreen : XamlUIScreenBase
{
    private readonly Color _color;

    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    public ColorPipelinePanelScreen(Color color)
        : base(DemoScreenXaml.Source("color-pipeline-panel.xaml"))
    {
        _color = color;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        window.BackgroundBrush.NormalValue = new MGSolidFillBrush(_color);
    }
}
