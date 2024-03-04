namespace CasaEngine.Framework.GUI.Neoforce;

public class ExitDialog : Dialog
{
    public Button BtnYes;
    public Button BtnNo;
    private Label _lblMessage;
    private ImageBox _imgIcon;

    public override void Initialize(Manager manager)
    {
        var msg = "Do you really want to exit " + Manager.Game.Window.Title + "?";
        ClientWidth = (int)Manager.Skin.Controls["Label"].Layers[0].Text.Font.Resource.MeasureString(msg).X + 48 + 16 + 16 + 16;
        Text = Manager.Game.Window.Title;

        ClientHeight = 120;
        TopPanel.Visible = false;
        IconVisible = true;
        Resizable = false;

        base.Initialize(manager);

        _imgIcon = new ImageBox();
        _imgIcon.Initialize(Manager);
        _imgIcon.Image = Manager.Skin.Images["Icon.Question"].Resource;
        _imgIcon.Left = 16;
        _imgIcon.Top = 16;
        _imgIcon.Width = 48;
        _imgIcon.Height = 48;
        _imgIcon.SizeMode = SizeMode.Stretched;

        _lblMessage = new Label();
        _lblMessage.Initialize(Manager);

        _lblMessage.Left = 80;
        _lblMessage.Top = 16;
        _lblMessage.Width = ClientWidth - _lblMessage.Left;
        _lblMessage.Height = 48;
        _lblMessage.Alignment = Alignment.TopLeft;
        _lblMessage.Text = msg;

        BtnYes = new Button();
        BtnYes.Initialize(Manager);
        BtnYes.Left = BottomPanel.ClientWidth / 2 - BtnYes.Width - 4;
        BtnYes.Top = 8;
        BtnYes.Text = "Yes";
        BtnYes.ModalResult = ModalResult.Yes;

        BtnNo = new Button();
        BtnNo.Initialize(Manager);
        BtnNo.Left = BottomPanel.ClientWidth / 2 + 4;
        BtnNo.Top = 8;
        BtnNo.Text = "No";
        BtnNo.ModalResult = ModalResult.No;

        Add(_imgIcon);
        Add(_lblMessage);
        BottomPanel.Add(BtnYes);
        BottomPanel.Add(BtnNo);

        DefaultControl = BtnNo;

        Center();
    }

}