namespace TomShane.Neoforce.Controls;

///  <include file='Documents/ButtonBase.xml' path='ButtonBase/Class[@name="ButtonBase"]/*' />          
public abstract class ButtonBase : Control
{
    public override ControlState ControlState
    {
        get
        {
            if (DesignMode)
            {
                return ControlState.Enabled;
            }

            if (Suspended)
            {
                return ControlState.Disabled;
            }
            if (!Enabled)
            {
                return ControlState.Disabled;
            }

            if ((Pressed[(int)MouseButton.Left] && Inside) || (Focused && (Pressed[(int)GamePadActions.Press] || Pressed[(int)MouseButton.None])))
            {
                return ControlState.Pressed;
            }

            if (Hovered && Inside)
            {
                return ControlState.Hovered;
            }
            if ((Focused && !Inside) || (Hovered && !Inside) || (Focused && !Hovered && Inside))
            {
                return ControlState.Focused;
            }
            return ControlState.Enabled;
        }
    }

    protected ButtonBase(Manager manager)
        : base(manager)
    {
        SetDefaultSize(72, 24);
        DoubleClicks = false;
    }

    protected override void OnClick(EventArgs e)
    {
        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();
        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            base.OnClick(e);
        }
    }

}