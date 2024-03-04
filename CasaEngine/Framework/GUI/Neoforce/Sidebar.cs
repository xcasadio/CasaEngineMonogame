using CasaEngine.Framework.GUI.Neoforce.Skins;

namespace CasaEngine.Framework.GUI.Neoforce;

public class SideBar : Panel
{
    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["SideBar"]);
    }
}