
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class ClipBox : Control
    {

        public ClipBox(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Color = Color.Transparent;
            BackgroundColor = Color.Transparent;
            CanFocus = false;
            Passive = true;
        } // ClipBox

    } // ClipBox
} // XNAFinalEngine.UserInterface