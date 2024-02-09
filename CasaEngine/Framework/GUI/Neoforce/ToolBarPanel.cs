using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ToolBarPanel : Control
{
    public override void Initialize(Manager manager)
    {
        Width = 64;
        Height = 25;

        base.Initialize(manager);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ToolBarPanel"]);
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        AlignBars();
    }

    private void AlignBars()
    {
        var rx = new int[8];
        var h = 0;
        var rm = -1;

        foreach (var c in Controls)
        {
            if (c is ToolBar bar)
            {
                if (bar.FullRow)
                {
                    bar.Width = Width;
                }

                bar.Left = rx[bar.Row];
                bar.Top = bar.Row * bar.Height + (bar.Row > 0 ? 1 : 0);
                rx[bar.Row] += bar.Width + 1;

                if (bar.Row > rm)
                {
                    rm = bar.Row;
                    h = bar.Top + bar.Height + 1;
                }
            }
        }

        Height = h;
    }
}