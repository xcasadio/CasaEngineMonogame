using TomShane.Neoforce.Controls.Input;

namespace TomShane.Neoforce.Controls;

public abstract class ButtonBase : Control
{
    public override ControlState ControlState
    {
        get
        {
            /*if (DesignMode)
            {
                return ControlState.Enabled;
            }*/

            if (Suspended && !DesignMode)
            {
                return ControlState.Disabled;
            }

            if (!Enabled && !DesignMode)
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

    protected ButtonBase()
    {
        DoubleClicks = false;
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        SetDefaultSize(72, 24);
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