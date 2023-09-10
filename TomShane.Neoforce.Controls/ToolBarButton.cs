namespace TomShane.Neoforce.Controls;

public class ToolBarButton : Button
{
    public ToolBarButton(Manager manager) : base(manager)
    {
        CanFocus = false;
        Text = "";
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ToolBarButton"]);
    }
}