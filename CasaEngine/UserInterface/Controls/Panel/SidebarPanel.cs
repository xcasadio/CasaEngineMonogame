
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.UserInterface.Controls.Auxiliary;

namespace CasaEngine.UserInterface.Controls.Panel
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
