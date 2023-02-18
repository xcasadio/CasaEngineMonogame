
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, Jos� Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Framework.UserInterface.Controls.Auxiliary;

namespace CasaEngine.Framework.UserInterface.Controls.Panel
{

    public class SideBarPanel : Container
    {

        public SideBarPanel(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 64;
        } // SideBarPanel

    } // SideBarPanel
} // XNAFinalEngine.UserInterface
