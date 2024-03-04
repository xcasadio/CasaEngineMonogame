using CasaEngine.Framework.GUI.Neoforce.Skins;

namespace CasaEngine.Framework.GUI.Neoforce;

public class ToolBarButton : Button
{
    public override void Initialize(Manager manager)
    {
        CanFocus = false;
        Text = "";

        base.Initialize(manager);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ToolBarButton"]);
    }
}