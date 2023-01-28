
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, Jos� Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Implements the basic functionality common to button controls.
    /// </summary>
    public abstract class ButtonBase : Control
    {


        /// <summary>
        /// Gets a value indicating current state of the control.
        /// </summary>
        public override ControlState ControlState
        {
            get
            {
                if (DesignMode) return ControlState.Enabled;
                if (Suspended)  return ControlState.Disabled;
                if (!Enabled)   return ControlState.Disabled;

                if ((Pressed[(int)MouseButton.Left] && Inside) || Pressed[(int)MouseButton.None]) 
                    return ControlState.Pressed;
                if (Hovered && Inside)
                    return ControlState.Hovered;
                if ((Focused && !Inside) || (Hovered && !Inside) || (Focused && !Hovered && Inside))
                    return ControlState.Focused;
                return ControlState.Enabled;
            }
        } // ControlState



        protected ButtonBase(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            SetDefaultSize(72, 24);
            DoubleClicks = false;
        } // ButtonBase



        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();
            if (ex.Button == MouseButton.Left)
            {
                base.OnClick(e);
            }
        } // OnClick


    } // ButtonBase
} // XNAFinalEngine.UserInterface