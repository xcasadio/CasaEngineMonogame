
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.CoreSystems;



namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Tool Tip
    /// </summary>
    public class ToolTip : Control
    {


        /// <summary>
        /// Gets or sets a value that indicates whether the control is rendered.
        /// </summary>
        public override bool Visible
        {
            set
            {
                if (value && !string.IsNullOrEmpty(Text) && SkinInformation != null && SkinInformation.Layers[0] != null)
                {
                    Vector2 size = SkinInformation.Layers[0].Text.Font.Font.MeasureString(Text);
                    Width = (int)size.X + SkinInformation.Layers[0].ContentMargins.Horizontal;
                    Height = (int)size.Y + SkinInformation.Layers[0].ContentMargins.Vertical;
                    Left = Mouse.GetState().X;
                    Top = Mouse.GetState().Y + 24;
                    // If it is outside the screen...
                    if (Left + Width > UserInterfaceManager.Screen.Width)
                        Left = UserInterfaceManager.Screen.Width - Width;
                    if (Top + Height > UserInterfaceManager.Screen.Height)
                        Top = UserInterfaceManager.Screen.Height - Height;
                    base.Visible = true;
                }
                else
                {
                    base.Visible = false;
                }
            }
        } // Visible



        /// <summary>
        /// Tool Tip
        /// </summary>
        public ToolTip(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Text = "";
            CanFocus = false;
            Passive = true;
        } // ToolTip



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = UserInterfaceManager.Skin.Controls["ToolTip"];
        } // InitSkin



        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers[0], rect);
            UserInterfaceManager.Renderer.DrawString(this, SkinInformation.Layers[0], Text, rect, true);
        } // DrawControl


    } // ToolTip
} // XNAFinalEngine.UserInterface