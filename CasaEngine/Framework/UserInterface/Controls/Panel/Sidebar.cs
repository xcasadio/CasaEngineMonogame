/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Framework.UserInterface.Controls.Panel;

public class SideBar : Panel
{
    public SideBar(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {

    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["SideBar"]);
    } // InitSkin

} // SideBar
// XNAFinalEngine.UserInterface
