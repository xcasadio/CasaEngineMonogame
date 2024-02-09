namespace TomShane.Neoforce.Controls;

public abstract class MenuBase : Control
{
    protected internal int ItemIndex { get; set; } = -1;

    protected internal MenuBase ChildMenu { get; set; }

    protected internal MenuBase RootMenu { get; set; }

    protected internal MenuBase ParentMenu { get; set; }

    public List<MenuItem> Items { get; } = new();

    protected MenuBase()
    {
        RootMenu = this;
    }

}