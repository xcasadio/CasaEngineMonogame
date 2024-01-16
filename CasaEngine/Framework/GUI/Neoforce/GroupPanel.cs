using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;

namespace TomShane.Neoforce.Controls;

public class GroupPanel : Container
{
    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        var layer = Skin.Layers["Control"];
        var font = layer.Text != null && layer.Text.Font != null ? layer.Text.Font.Resource : null;
        var col = layer.Text != null ? layer.Text.Colors.Enabled : Color.White;
        var offset = new Point(layer.Text.OffsetX, layer.Text.OffsetY);

        renderer.DrawLayer(this, layer, rect);

        if (font != null && Text != null && Text != "")
        {
            renderer.DrawString(this, layer, Text, new Rectangle(rect.Left, rect.Top + layer.ContentMargins.Top, rect.Width, Skin.ClientMargins.Top - layer.ContentMargins.Horizontal), false, offset.X, offset.Y, false);
        }
    }
}