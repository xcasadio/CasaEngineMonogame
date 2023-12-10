using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class Label : Control
{
    public Alignment Alignment { get; set; } = Alignment.MiddleLeft;

    public bool Ellipsis { get; set; } = true;

    public Label(Manager manager) : base(manager)
    {
        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 16;
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        //base.DrawControl(renderer, rect, gameTime);

        var s = new SkinLayer(Skin.Layers[0]);
        s.Text.Alignment = Alignment;
        renderer.DrawString(this, s, Text, rect, true, 0, 0, Ellipsis);
    }
}