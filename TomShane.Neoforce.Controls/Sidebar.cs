using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class SideBar : Panel
{
    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["SideBar"]);
    }
}