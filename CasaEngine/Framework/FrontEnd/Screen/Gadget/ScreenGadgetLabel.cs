using System.Xml;
using CasaEngine.Core.Design;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget;

public class ScreenGadgetLabel
    : ScreenGadget
{
    public ScreenGadgetLabel(XmlElement el, SaveOption opt)
        : base(el, opt)
    {

    }

#if EDITOR
    public
#else
    protected
#endif
    override void DrawGadget(float elapsedTime)
    {
        Renderer2dComponent.DrawRectangle(Location.X, Location.Y, Scale.X, Scale.Y, BackgroundColor, 0.0001f);

        Renderer2dComponent.DrawText(
            Font,
            Text,
            Location + Vector2.One * 5,
            0.0f,
            Scale,
            FontColor,
            0.0f);
    }

#if EDITOR
    public static int Num;

    public ScreenGadgetLabel()
        : base("Label" + (Num++))
    {
        Width = 100;
        Height = 80;
    }
#endif
}