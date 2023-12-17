using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class SideBar : Panel
{

    public SideBar(Manager manager) : base(manager)
    {
        // CanFocus = true;
    }

    public override void Init()
    {
        base.Init();
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["SideBar"]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);
    }

}