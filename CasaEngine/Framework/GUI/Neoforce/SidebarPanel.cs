namespace TomShane.Neoforce.Controls;

public class SideBarPanel : Container
{
    public override void Initialize(Manager manager)
    {
        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;

        base.Initialize(manager);
    }
}