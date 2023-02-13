
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.UserInterface.Controls.Buttons
{


    public enum RadioButtonMode
    {
        Auto,
        Manual
    } // RadioButtonMode


    public class RadioButton : CheckBox
    {


        private RadioButtonMode _mode = RadioButtonMode.Auto;



        public RadioButtonMode Mode
        {
            get => _mode;
            set => _mode = value;
        } // Mode



        public RadioButton(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {

        }



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["RadioButton"]);
        } // InitSkin



        protected override void OnClick(EventArgs e)
        {
            var ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left && _mode == RadioButtonMode.Auto)
            {
                if (Parent != null)
                {
                    foreach (var control in Parent.ChildrenControls) // Check for brothers.
                    {
                        if (control is RadioButton)
                        {
                            ((RadioButton)control).Checked = false;
                        }
                    }
                }
                else // If the parent is the manager.
                {
                    foreach (var control in UserInterfaceManager.RootControls)
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
