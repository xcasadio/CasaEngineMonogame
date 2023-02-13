
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.UserInterface.Controls.Auxiliary;
using Microsoft.Xna.Framework;

namespace CasaEngine.UserInterface.Controls.Group
{

    public enum GroupBoxType
    {
        Normal,
        Flat
    } // GroupBoxType

    public class GroupBox : Container
    {


        private GroupBoxType _type = GroupBoxType.Normal;



        public virtual GroupBoxType Type
        {
            get => _type;
            set { _type = value; Invalidate(); }
        } // Type



        public GroupBox(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CheckLayer(SkinInformation, "Control");
            CheckLayer(SkinInformation, "Flat");

            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 64;
            BackgroundColor = Color.Transparent;
        } // GroupBox



        protected override void DrawControl(Rectangle rect)
        {
            var layer = _type == GroupBoxType.Normal ? SkinInformation.Layers["Control"] : SkinInformation.Layers["Flat"];
            var font = layer.Text.Font.Font;
            var offset = new Point(layer.Text.OffsetX, layer.Text.OffsetY);
            var size = font.MeasureString(Text);
            size.Y = font.LineSpacing;
            var r = new Rectangle(rect.Left, rect.Top + (int)(size.Y / 2), rect.Width, rect.Height - (int)(size.Y / 2));

            UserInterfaceManager.Renderer.DrawLayer(this, layer, r);

            if (!string.IsNullOrEmpty(Text))
            {
                var bg = new Rectangle(r.Left + offset.X, (r.Top - (int)(size.Y / 2)) + offset.Y, (int)size.X + layer.ContentMargins.Horizontal, (int)size.Y);
                UserInterfaceManager.Renderer.DrawLayer(UserInterfaceManager.Skin.Controls["Control"].Layers[0], bg, new Color(64, 64, 64), 0);
                UserInterfaceManager.Renderer.DrawString(this, layer, Text, new Rectangle(r.Left, r.Top - (int)(size.Y / 2), (int)(size.X), (int)size.Y), true, 0, 0, false);
            }
        } // DrawControl


    } // GroupBox
} // XNAFinalEngine.UserInterface
