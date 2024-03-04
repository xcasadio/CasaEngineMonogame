using CasaEngine.Framework.GUI.Neoforce.Graphics;
using CasaEngine.Framework.GUI.Neoforce.Skins;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.GUI.Neoforce;

public class Label : Control
{
    public Alignment Alignment { get; set; } = Alignment.MiddleLeft;

    public bool Ellipsis { get; set; } = true;

    public override void Initialize(Manager manager)
    {
        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 16;

        base.Initialize(manager);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        //base.DrawControl(renderer, rect, gameTime);

        var s = new SkinLayer(Skin.Layers[0]);
        s.Text.Alignment = Alignment;
        renderer.DrawString(this, s, Text, rect, true, 0, 0, Ellipsis);
    }
}