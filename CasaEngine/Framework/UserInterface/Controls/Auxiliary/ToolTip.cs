
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.UserInterface.Controls.Auxiliary;

public class ToolTip : Control
{


    public override bool Visible
    {
        set
        {
            if (value && !string.IsNullOrEmpty(Text) && SkinInformation != null && SkinInformation.Layers[0] != null)
            {
                var size = SkinInformation.Layers[0].Text.Font.Font.MeasureString(Text);
                Width = (int)size.X + SkinInformation.Layers[0].ContentMargins.Horizontal;
                Height = (int)size.Y + SkinInformation.Layers[0].ContentMargins.Vertical;
                Left = Mouse.GetState().X;
                Top = Mouse.GetState().Y + 24;
                // If it is outside the screen...
                if (Left + Width > UserInterfaceManager.Screen.Width)
                {
                    Left = UserInterfaceManager.Screen.Width - Width;
                }

                if (Top + Height > UserInterfaceManager.Screen.Height)
                {
                    Top = UserInterfaceManager.Screen.Height - Height;
                }

                base.Visible = true;
            }
            else
            {
                base.Visible = false;
            }
        }
    } // Visible



    public ToolTip(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
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



    protected override void DrawControl(Rectangle rect)
    {
        UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers[0], rect);
        UserInterfaceManager.Renderer.DrawString(this, SkinInformation.Layers[0], Text, rect, true);
    } // DrawControl


} // ToolTip
  // XNAFinalEngine.UserInterface