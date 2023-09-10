namespace TomShane.Neoforce.Controls;

public class ExitDialog : Dialog
{
    public readonly Button BtnYes;
    public readonly Button BtnNo;
    private Label _lblMessage;
    private ImageBox _imgIcon;

    public ExitDialog(Manager manager) : base(manager)
    {
        var msg = "Do you really want to exit " + Manager.Game.Window.Title + "?";
        ClientWidth = (int)Manager.Skin.Controls["Label"].Layers[0].Text.Font.Resource.MeasureString(msg).X + 48 + 16 + 16 + 16;
        ClientHeight = 120;
        TopPanel.Visible = false;
        IconVisible = true;
        Resizable = false;
        Text = Manager.Game.Window.Title;
        Center();

        _imgIcon = new ImageBox(Manager);
        _imgIcon.Init();
        _imgIcon.Image = Manager.Skin.Images["Icon.Question"].Resource;
        _imgIcon.Left = 16;
        _imgIcon.Top = 16;
        _imgIcon.Width = 48;
        _imgIcon.Height = 48;
        _imgIcon.SizeMode = SizeMode.Stretched;

        _lblMessage = new Label(Manager);
        _lblMessage.Init();

        _lblMessage.Left = 80;
        _lblMessage.Top = 16;
        _lblMessage.Width = ClientWidth - _lblMessage.Left;
        _lblMessage.Height = 48;
        _lblMessage.Alignment = Alignment.TopLeft;
        _lblMessage.Text = msg;

        BtnYes = new Button(Manager);
        BtnYes.Init();
        BtnYes.Left = BottomPanel.ClientWidth / 2 - BtnYes.Width - 4;
        BtnYes.Top = 8;
        BtnYes.Text = "Yes";
        BtnYes.ModalResult = ModalResult.Yes;

        BtnNo = new Button(Manager);
        BtnNo.Init();
        BtnNo.Left = BottomPanel.ClientWidth / 2 + 4;
        BtnNo.Top = 8;
        BtnNo.Text = "No";
        BtnNo.ModalResult = ModalResult.No;

        Add(_imgIcon);
        Add(_lblMessage);
        BottomPanel.Add(BtnYes);
        BottomPanel.Add(BtnNo);

        DefaultControl = BtnNo;
    }

    public override void Init()
    {
        base.Init();
    }

}