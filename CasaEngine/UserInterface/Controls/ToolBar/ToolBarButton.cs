
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Tool bar button.
    /// </summary>
    public class ToolBarButton : Button
    {


        /// <summary>
        /// Tool Bar Button.
        /// </summary>
        public ToolBarButton(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Text = "";
        }



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ToolBarButton"]);
        } // InitSkin


    } // ToolBarButton
} // XNAFinalEngine.UserInterface
