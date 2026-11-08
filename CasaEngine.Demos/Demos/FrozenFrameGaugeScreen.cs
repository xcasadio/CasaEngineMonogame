using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The solid window that plays the life gauge in <see cref="FrozenFrameDemo"/> (E19.TR-4a): user interface drawn in the
/// image the engine keeps. Its tree lives in `Content/Screens/frozen-frame-gauge.xaml`; the fill colour, one per scene, is
/// applied here because the demo reads back the colour it posed.
/// </summary>
internal sealed class FrozenFrameGaugeScreen : XamlUIScreenBase
{
    private MGWindow? _window;
    private Color _color = Color.Black;

    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    public FrozenFrameGaugeScreen()
        : base(DemoScreenXaml.Source("frozen-frame-gauge.xaml"))
    {
    }

    public void SetColor(Color color)
    {
        _color = color;
        Apply();
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        _window = window;
        Apply();
    }

    private void Apply()
    {
        if (_window != null)
        {
            _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(_color);
        }
    }
}
