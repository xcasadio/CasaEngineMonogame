using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class StatusBar : Control
{
    public override void Initialize(Manager manager)
    {
        Left = 0;
        Top = 0;
        Width = 64;
        Height = 24;
        CanFocus = false;

        base.Initialize(manager);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["StatusBar"]);
    }
}