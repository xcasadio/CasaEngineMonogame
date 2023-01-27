
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Implements the basic functionality common to button controls.
    /// </summary>
    public abstract class ButtonBase : Control
    {

        #region Properties

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

        #endregion

        #region Constructor

        protected ButtonBase(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            SetDefaultSize(72, 24);
            DoubleClicks = false;
        } // ButtonBase

        #endregion

        #region On click

        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();
            if (ex.Button == MouseButton.Left)
            {
                base.OnClick(e);
            }
        } // OnClick

        #endregion

    } // ButtonBase
} // XNAFinalEngine.UserInterface