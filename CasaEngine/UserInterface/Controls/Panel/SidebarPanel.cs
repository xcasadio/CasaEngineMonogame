
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

    public class SideBarPanel : Container
    {

        public SideBarPanel(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 64;
        } // SideBarPanel

    } // SideBarPanel
} // XNAFinalEngine.UserInterface
