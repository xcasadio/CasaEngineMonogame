
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Asset.Fonts;




namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Group Panel. Group controls that will be enclosed by a bevel and it will be a title in the top title bar.
    /// </summary>
    public class GroupPanel : Container
    {


        /// <summary>
        /// Group Panel. Group controls with and title a bar.
        /// </summary>
        public GroupPanel(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 64;
        } // GroupPanel



        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            SkinLayer layer = SkinInformation.Layers["Control"];
            Font font = (layer.Text != null && layer.Text.Font != null) ? layer.Text.Font.Font : null;
            Point offset = new Point(layer.Text.OffsetX, layer.Text.OffsetY);

            UserInterfaceManager.Renderer.DrawLayer(this, layer, rect);

            if (font != null && !string.IsNullOrEmpty(Text))
            {
                UserInterfaceManager.Renderer.DrawString(this, layer, Text, new Rectangle(rect.Left, rect.Top + layer.ContentMargins.Top, rect.Width, SkinInformation.ClientMargins.Top - layer.ContentMargins.Horizontal), false, offset.X, offset.Y, false);
            }
        } // DrawControl


    } // GroupPanel
} // XNAFinalEngine.UserInterface