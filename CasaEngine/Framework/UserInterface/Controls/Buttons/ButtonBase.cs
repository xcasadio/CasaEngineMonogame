/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Framework.UserInterface.Controls.Buttons;

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

            if (Pressed[(int)MouseButton.Left] && Inside || Pressed[(int)MouseButton.None])
            {
                return ControlState.Pressed;
            }

            if (Hovered && Inside)
            {
                return ControlState.Hovered;
            }

            if (Focused && !Inside || Hovered && !Inside || Focused && !Hovered && Inside)
            {
                return ControlState.Focused;
            }

            return ControlState.Enabled;
        }
    } // ControlState



    protected ButtonBase(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        SetDefaultSize(72, 24);
        DoubleClicks = false;
    } // ButtonBase



    protected override void OnClick(EventArgs e)
    {
        var ex = e is MouseEventArgs ? (MouseEventArgs)e : new MouseEventArgs();
        if (ex.Button == MouseButton.Left)
        {
            base.OnClick(e);
        }
    } // OnClick


} // ButtonBase
  // XNAFinalEngine.UserInterface