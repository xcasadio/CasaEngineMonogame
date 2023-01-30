
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Game;

namespace XNAFinalEngine.UserInterface
{

    public class ExitDialog : Dialog
    {

              
        private readonly Button buttonYes;
        private readonly Button buttonNo;
        private readonly Label labelMessage;
        private readonly ImageBox imageIcon;


    
        public ExitDialog(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            string msg = "Do you really want to exit?";
            ClientWidth = (int)UserInterfaceManager.Skin.Controls["Label"].Layers[0].Text.Font.Font.MeasureString(msg).X + 48 + 16 + 16 + 16;
            ClientHeight = 120;
            TopPanel.Visible = false;
            IconVisible = true;
            Resizable = false;
            StayOnTop = true;
            Text = Engine.Instance.Game.Window.Title;
            CenterWindow();

            imageIcon = new ImageBox(UserInterfaceManager)
            {
                Texture = UserInterfaceManager.Skin.Images["Icon.Question"].Texture,
                Left = 16,
                Top = 16,
                Width = 48,
                Height = 48,
                SizeMode = SizeMode.Stretched
            };

            labelMessage = new Label(UserInterfaceManager)
                {
                    Left = 80,
                    Top = 16,
                    Height = 48,
                    Alignment = Alignment.TopLeft,
                    Text = msg
                };
            labelMessage.Width = ClientWidth - labelMessage.Left;

            buttonYes = new Button(UserInterfaceManager)
            {
                Top = 8, 
                Text = "Yes",
                ModalResult = ModalResult.Yes
            };
            buttonYes.Left = (BottomPanel.ClientWidth / 2) - buttonYes.Width - 4;

            buttonNo = new Button(UserInterfaceManager)
            {
                Left = (BottomPanel.ClientWidth/2) + 4,
                Top = 8,
                Text = "No",
                ModalResult = ModalResult.No
            };

            Add(imageIcon);
            Add(labelMessage);
            BottomPanel.Add(buttonYes);
            BottomPanel.Add(buttonNo);

            DefaultControl = buttonNo;
        } // ExitDialog


    } // ExitDialog
} // XNAFinalEngine.UserInterface