
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Framework.UserInterface.Controls.Auxiliary;

namespace CasaEngine.Framework.UserInterface.Controls.Windows;

public class Dialog : Window
{


    public Panel.Panel TopPanel { get; private set; }

    public Panel.Panel BottomPanel { get; private set; }

    public Label Caption { get; private set; }

    public Label Description { get; private set; }



    public Dialog(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        TopPanel = new Panel.Panel(UserInterfaceManager)
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

        BottomPanel = new Panel.Panel(UserInterfaceManager)
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

        var lc = new SkinLayer(Caption.SkinInformation.Layers[0]);
        var skinColor = lc.Text.Colors;
        lc.Text.Font.Font = UserInterfaceManager.Skin.Fonts[UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Font;
        skinColor.Enabled = Utilities.ParseColor(UserInterfaceManager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFontColor"].Value);
        lc.Text.Colors = skinColor;

        var ld = new SkinLayer(Description.SkinInformation.Layers[0]);
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
  // XNAFinalEngine.UserInterface