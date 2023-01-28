
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System;


using Microsoft.Xna.Framework;

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Dialog window. It has a top panel (with description and caption) and a bottom panel.
    /// </summary>
    public class Dialog : Window
    {


        /// <summary>
        /// Top panel. 
        /// It also stores the caption and description.
        /// </summary>
        public Panel TopPanel { get; private set; }

        /// <summary>
        /// Bottom panel, good for buttons.
        /// </summary>
        public Panel BottomPanel { get; private set; }

        /// <summary>
        /// Caption. It shows in the top panel.
        /// </summary>
        public Label Caption { get; private set; }

        /// <summary>
        /// Description. It shows in the top panel.
        /// </summary>
        public Label Description { get; private set; }



        /// <summary>
        /// Dialog window. It has a top panel (with description and caption) and a bottom panel.
        /// </summary>
        public Dialog(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            TopPanel = new Panel(UserInterfaceManager)
            {
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Height = 64,
                BevelBorder = BevelBorder.Bottom,
                Parent = this,
                Width = ClientWidth
            };
            Caption = new Label(UserInterfaceManager)
            {
                Parent = TopPanel,
                Text = "Caption",
                Left = 8,
                Top = 8,
                Alignment = Alignment.TopLeft,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right
            };
            Caption.Width = Caption.Parent.ClientWidth - 16;

            Description = new Label(UserInterfaceManager)
            {
                Parent = TopPanel,
                Left = 8,
                Text = "Description text.",
                Alignment = Alignment.TopLeft,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right
            };
            Description.Width = Description.Parent.ClientWidth - 16;

            BottomPanel = new Panel(UserInterfaceManager)
            {
                Parent = this,
                Width = ClientWidth,
                Height = 24 + 16,
                BevelBorder = BevelBorder.Top,
                Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right
            };
            BottomPanel.Top = ClientHeight - BottomPanel.Height;
        } // Dialog



        protected internal override void Init()
        {
            base.Init();

            SkinLayer lc = new SkinLayer(Caption.SkinInformation.Layers[0]);
            SkinStates<Color> skinColor = lc.Text.Colors;
            lc.Text.Font.Font = UserInterfaceManager.Skin.Fonts[UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Font;
            skinColor.Enabled = Utilities.ParseColor(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFontColor"].Value);
            lc.Text.Colors = skinColor;

            SkinLayer ld = new SkinLayer(Description.SkinInformation.Layers[0]);
            ld.Text.Font.Font = UserInterfaceManager.Skin.Fonts[UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Font;
            skinColor = ld.Text.Colors;
            skinColor.Enabled = Utilities.ParseColor(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFontColor"].Value);
            ld.Text.Colors = skinColor;

            TopPanel.Color = Utilities.ParseColor(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["Color"].Value);
            TopPanel.BevelMargin = int.Parse(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelMargin"].Value);
            TopPanel.BevelStyle = ParseBevelStyle(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelStyle"].Value);

            Caption.SkinInformation = new SkinControlInformation(Caption.SkinInformation);
            Caption.SkinInformation.Layers[0] = lc;
            Caption.Height = UserInterfaceManager.Skin.Fonts[UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Height;

            Description.SkinInformation = new SkinControlInformation(Description.SkinInformation);
            Description.SkinInformation.Layers[0] = ld;
            Description.Height = UserInterfaceManager.Skin.Fonts[UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Height;
            Description.Top = Caption.Top + Caption.Height + 4;
            Description.Height = Description.Parent.ClientHeight - Description.Top - 8;

            BottomPanel.Color = Utilities.ParseColor(UserInterfaceManager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["Color"].Value);
            BottomPanel.BevelMargin = int.Parse(UserInterfaceManager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelMargin"].Value);
            BottomPanel.BevelStyle = ParseBevelStyle(UserInterfaceManager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelStyle"].Value);
        } // Init

        private static BevelStyle ParseBevelStyle(string str)
        {
            return (BevelStyle)Enum.Parse(typeof(BevelStyle), str, true);
        } // ParseBevelStyle


    } // Dialog
} // XNAFinalEngine.UserInterface