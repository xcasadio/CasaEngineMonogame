
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.Framework.UserInterface.Controls.ToolBar;

public class ToolBarButton : Button
{


    public ToolBarButton(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
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
// XNAFinalEngine.UserInterface
