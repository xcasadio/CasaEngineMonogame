using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class StatusBar : Control
{
    public StatusBar(Manager manager) : base(manager)
    {
        Left = 0;
        Top = 0;
        Width = 64;
        Height = 24;
        CanFocus = false;
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["StatusBar"]);
    }
}