namespace TomShane.Neoforce.Controls;

public class Dialog : Window
{
    public Panel TopPanel { get; }

    public Panel BottomPanel { get; }

    public Label Caption { get; }

    public Label Description { get; }

    public Dialog(Manager manager) : base(manager)
    {
        TopPanel = new Panel(manager);
        TopPanel.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;
        TopPanel.Init();
        TopPanel.Parent = this;
        TopPanel.Width = ClientWidth;
        TopPanel.Height = 64;
        TopPanel.BevelBorder = BevelBorder.Bottom;

        Caption = new Label(manager);
        Caption.Init();
        Caption.Parent = TopPanel;
        Caption.Width = Caption.Parent.ClientWidth - 16;
        Caption.Text = "Caption";
        Caption.Left = 8;
        Caption.Top = 8;
        Caption.Alignment = Alignment.TopLeft;
        Caption.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

        Description = new Label(manager);
        Description.Init();
        Description.Parent = TopPanel;
        Description.Width = Description.Parent.ClientWidth - 16;
        Description.Left = 8;
        Description.Text = "Description text.";
        Description.Alignment = Alignment.TopLeft;
        Description.Anchor = Anchors.Left | Anchors.Top | Anchors.Right;

        BottomPanel = new Panel(manager);
        BottomPanel.Init();
        BottomPanel.Parent = this;
        BottomPanel.Width = ClientWidth;
        BottomPanel.Height = 24 + 16;
        BottomPanel.Top = ClientHeight - BottomPanel.Height;
        BottomPanel.BevelBorder = BevelBorder.Top;
        BottomPanel.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;

    }

    public override void Init()
    {
        base.Init();

        var lc = new SkinLayer(Caption.Skin.Layers[0]);
        lc.Text.Font.Resource = Manager.Skin.Fonts[Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Resource;
        lc.Text.Colors.Enabled = Utilities.ParseColor(Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFontColor"].Value);

        var ld = new SkinLayer(Description.Skin.Layers[0]);
        ld.Text.Font.Resource = Manager.Skin.Fonts[Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Resource;
        ld.Text.Colors.Enabled = Utilities.ParseColor(Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFontColor"].Value);

        TopPanel.Color = Utilities.ParseColor(Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["Color"].Value);
        TopPanel.BevelMargin = int.Parse(Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelMargin"].Value);
        TopPanel.BevelStyle = Utilities.ParseBevelStyle(Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["BevelStyle"].Value);

        Caption.Skin = new SkinControl(Caption.Skin);
        Caption.Skin.Layers[0] = lc;
        Caption.Height = Manager.Skin.Fonts[Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["CaptFont"].Value].Height;

        Description.Skin = new SkinControl(Description.Skin);
        Description.Skin.Layers[0] = ld;
        Description.Height = Manager.Skin.Fonts[Manager.Skin.Controls["Dialog"].Layers["TopPanel"].Attributes["DescFont"].Value].Height;
        Description.Top = Caption.Top + Caption.Height + 4;
        Description.Height = Description.Parent.ClientHeight - Description.Top - 8;

        BottomPanel.Color = Utilities.ParseColor(Manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["Color"].Value);
        BottomPanel.BevelMargin = int.Parse(Manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelMargin"].Value);
        BottomPanel.BevelStyle = Utilities.ParseBevelStyle(Manager.Skin.Controls["Dialog"].Layers["BottomPanel"].Attributes["BevelStyle"].Value);
    }
}