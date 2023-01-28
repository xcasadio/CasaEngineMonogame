
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;

namespace XNAFinalEngine.UserInterface
{

    public class ClipBox : Control
    {

        /// <summary>
        /// Clip Box.
        /// </summary>
        public ClipBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Color = Color.Transparent;
            BackgroundColor = Color.Transparent;
            CanFocus = false;
            Passive = true;
        } // ClipBox

    } // ClipBox
} // XNAFinalEngine.UserInterface