namespace TomShane.Neoforce.Controls;

public class SideBarPanel : Container
{
    public SideBarPanel(Manager manager) : base(manager)
    {
        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;
    }
}