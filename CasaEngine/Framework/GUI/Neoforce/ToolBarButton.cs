using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ToolBarButton : Button
{
    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        CanFocus = false;
        Text = "";
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ToolBarButton"]);
    }
}