using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class Dialog : Window
{
    public Panel TopPanel { get; private set; }

    public Panel BottomPanel { get; private set; }

    public Label Caption { get; private set; }

    public Label Description { get; private set; }

    public override void Initialize(Manager manager)
    {
        TopPanel = new Panel();
        TopPanel.Initialize(manager);

        Caption = new Label();
        Caption.Initialize(manager);

        Description = new Label();
        Description.Initialize(manager);

        BottomPanel = new Panel();
        BottomPanel.Initialize(manager);

        base.Initialize(manager);

        TopPanel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;
        TopPanel.Width = ClientWidth;
        TopPanel.Height = 64;
        TopPanel.BevelBorder = BevelBorder.Bottom;
        TopPanel.Parent = this;

        Caption.Parent = TopPanel;
        Caption.Width = TopPanel.ClientWidth - 16;
        Caption.Text = "Caption";
        Caption.Left = 8;
        Caption.Top = 8;
        Caption.Alignment = Alignment.TopLeft;
        Caption.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

        Description.Parent = TopPanel;
        Description.Width = TopPanel.ClientWidth - 16;
        Description.Left = 8;
        Description.Text = "Description text.";
        Description.Alignment = Alignment.TopLeft;
        Description.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

        BottomPanel.Width = ClientWidth;
        BottomPanel.Height = 24 + 16;
        BottomPanel.Top = ClientHeight - BottomPanel.Height;
        BottomPanel.BevelBorder = BevelBorder.Top;
        BottomPanel.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;
        BottomPanel.Parent = this;

        var lc = new SkinLayer(Caption.Skin.Layers[0]);
        lc.Text.Font.Resource = manager.Skin.Fonts[manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Resource;
        lc.Text.Colors.Enabled = Utilities.ParseColor(manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFontColor"].Value);

        var ld = new SkinLayer(Description.Skin.Layers[0]);
        ld.Text.Font.Resource = manager.Skin.Fonts[manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Resource;
        ld.Text.Colors.Enabled = Utilities.ParseColor(manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFontColor"].Value);

        TopPanel.Color = Utilities.ParseColor(manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["Color"].Value);
        TopPanel.BevelMargin = int.Parse(manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelMargin"].Value);
        TopPanel.BevelStyle = Utilities.ParseBevelStyle(manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelStyle"].Value);

        Caption.Skin = new SkinControl(Caption.Skin);
        Caption.Skin.Layers[0] = lc;
        Caption.Height = manager.Skin.Fonts[manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Height;

        Description.Skin = new SkinControl(Description.Skin);
        Description.Skin.Layers[0] = ld;
        Description.Height = manager.Skin.Fonts[manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Height;
        Description.Top = Caption.Top + Caption.Height + 4;
        Description.Height = TopPanel.ClientHeight - Description.Top - 8;

        BottomPanel.Color = Utilities.ParseColor(manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["Color"].Value);
        BottomPanel.BevelMargin = int.Parse(manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelMargin"].Value);
        BottomPanel.BevelStyle = Utilities.ParseBevelStyle(manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelStyle"].Value);
    }
}