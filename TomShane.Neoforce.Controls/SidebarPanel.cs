namespace TomShane.Neoforce.Controls;

public class SideBarPanel : Container
{
    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;
    }
}