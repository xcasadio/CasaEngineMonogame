
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Framework.Game;
using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.Framework.UserInterface.Controls.Windows;

public class ExitDialog : Dialog
{


    private readonly Button _buttonYes;
    private readonly Button _buttonNo;
    private readonly Label _labelMessage;
    private readonly ImageBox _imageIcon;



    public ExitDialog(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        var msg = "Do you really want to exit?";
        ClientWidth = (int)UserInterfaceManager.Skin.Controls["Label"].Layers[0].Text.Font.Font.MeasureString(msg).X + 48 + 16 + 16 + 16;
        ClientHeight = 120;
        TopPanel.Visible = false;
        IconVisible = true;
        Resizable = false;
        StayOnTop = true;
        Text = userInterfaceManager.Game.Window.Title;
        CenterWindow();

        _imageIcon = new ImageBox(UserInterfaceManager)
        {
            Texture = UserInterfaceManager.Skin.Images["Icon.Question"].Texture,
            Left = 16,
            Top = 16,
            Width = 48,
            Height = 48,
            SizeMode = SizeMode.Stretched
        };

        _labelMessage = new Label(UserInterfaceManager)
        {
            Left = 80,
            Top = 16,
            Height = 48,
            Alignment = Alignment.TopLeft,
            Text = msg
        };
        _labelMessage.Width = ClientWidth - _labelMessage.Left;

        _buttonYes = new Button(UserInterfaceManager)
        {
            Top = 8,
            Text = "Yes",
            ModalResult = ModalResult.Yes
        };
        _buttonYes.Left = BottomPanel.ClientWidth / 2 - _buttonYes.Width - 4;

        _buttonNo = new Button(UserInterfaceManager)
        {
            Left = BottomPanel.ClientWidth / 2 + 4,
            Top = 8,
            Text = "No",
            ModalResult = ModalResult.No
        };

        Add(_imageIcon);
        Add(_labelMessage);
        BottomPanel.Add(_buttonYes);
        BottomPanel.Add(_buttonNo);

        DefaultControl = _buttonNo;
    } // ExitDialog


} // ExitDialog
  // XNAFinalEngine.UserInterface