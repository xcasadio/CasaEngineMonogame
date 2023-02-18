
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Framework.UserInterface.Controls.Auxiliary;

namespace CasaEngine.Framework.UserInterface.Controls.Group
{

    public class GroupPanel : Container
    {


        public GroupPanel(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 64;
        } // GroupPanel



        protected override void DrawControl(Rectangle rect)
        {
            var layer = SkinInformation.Layers["Control"];
            var font = layer.Text != null && layer.Text.Font != null ? layer.Text.Font.Font : null;
            var offset = new Point(layer.Text.OffsetX, layer.Text.OffsetY);

            UserInterfaceManager.Renderer.DrawLayer(this, layer, rect);

            if (font != null && !string.IsNullOrEmpty(Text))
            {
                UserInterfaceManager.Renderer.DrawString(this, layer, Text, new Rectangle(rect.Left, rect.Top + layer.ContentMargins.Top, rect.Width, SkinInformation.ClientMargins.Top - layer.ContentMargins.Horizontal), false, offset.X, offset.Y, false);
            }
        } // DrawControl


    } // GroupPanel
} // XNAFinalEngine.UserInterface