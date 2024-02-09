using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TomShane.Neoforce.Controls.Graphics;

namespace TomShane.Neoforce.Controls;

public class ToolTip : Control
{
    public override bool Visible
    {
        set
        {
            if (value && !string.IsNullOrEmpty(Text) && Skin?.Layers[0] != null)
            {
                var size = Skin.Layers[0].Text.Font.Resource.MeasureString(Text);
                Width = (int)size.X + Skin.Layers[0].ContentMargins.Horizontal;
                Height = (int)size.Y + Skin.Layers[0].ContentMargins.Vertical;
                Left = Mouse.GetState().X;
                Top = Mouse.GetState().Y + 24;
                base.Visible = value;
            }
            else
            {
                base.Visible = false;
            }
        }
    }

    public override void Initialize(Manager manager)
    {
        Text = "";
        CanFocus = false;
        Passive = true;

        base.Initialize(manager);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = Manager.Skin.Controls["ToolTip"];
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        renderer.DrawLayer(this, Skin.Layers[0], rect);
        renderer.DrawString(this, Skin.Layers[0], Text, rect, true);
    }

}