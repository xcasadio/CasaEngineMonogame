
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

namespace XNAFinalEngine.UserInterface
{

    #region Enumerators
          
    public enum RadioButtonMode
    {
        Auto,
        Manual
    } // RadioButtonMode

    #endregion

    /// <summary>
    /// Radio Button.
    /// Default mode: auto. That is the bothers are modified by event in this control.
    /// </summary>
    public class RadioButton : CheckBox
    {
        
        #region Variables

        /// <summary>
        /// Radio Button Mode (auto, manual).
        /// In manual mode the brothers aren't chagend for clicks in this radio button.
        /// </summary>
        private RadioButtonMode mode = RadioButtonMode.Auto;

        #endregion

        #region Properties

        /// <summary>
        /// Radio Button Mode (auto, manual).
        /// In manual mode the brothers aren't chagend for clicks in this radio button.
        /// </summary>
        public RadioButtonMode Mode
        {
            get { return mode; }
            set { mode = value; }
        } // Mode

        #endregion

        #region Constructor
   
        /// <summary>
        /// CheckBox.
        /// </summary>
        public RadioButton(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            
        }

        #endregion

        #region Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["RadioButton"]);
        } // InitSkin

        #endregion

        #region On Click

        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left && mode == RadioButtonMode.Auto)
            {
                if (Parent != null) 
                {
                    foreach (Control control in Parent.ChildrenControls) // Check for brothers.
                    {
                        if (control is RadioButton)
                        {
                            ((RadioButton) control).Checked = false;
                        }
                    }
                }
                else // If the parent is the manager.
                {
                    foreach (Control control in UserInterfaceManager.RootControls)
                    {
                        if (control is RadioButton)
                        {
                            ((RadioButton) control).Checked = false;
                        }
                    }
                }
            }
            base.OnClick(e);
        } // OnClick

        #endregion

    } // RadioButton
} // XNAFinalEngine.UserInterface
