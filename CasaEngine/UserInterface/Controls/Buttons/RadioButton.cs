
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{


    public enum RadioButtonMode
    {
        Auto,
        Manual
    } // RadioButtonMode


    public class RadioButton : CheckBox
    {


        private RadioButtonMode mode = RadioButtonMode.Auto;



        public RadioButtonMode Mode
        {
            get => mode;
            set => mode = value;
        } // Mode



        public RadioButton(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {

        }



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["RadioButton"]);
        } // InitSkin



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
                            ((RadioButton)control).Checked = false;
                        }
                    }
                }
                else // If the parent is the manager.
                {
                    foreach (Control control in UserInterfaceManager.RootControls)
                    {
                        if (control is RadioButton)
                        {
                            ((RadioButton)control).Checked = false;
                        }
                    }
                }
            }
            base.OnClick(e);
        } // OnClick


    } // RadioButton
} // XNAFinalEngine.UserInterface
