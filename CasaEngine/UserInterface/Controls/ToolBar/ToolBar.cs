
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class ToolBar : Control
    {


        private int _row;



        public virtual int Row
        {
            get => _row;
            set
            {
                _row = value;
                if (_row < 0) _row = 0;
                if (_row > 7) _row = 7;
            }
        } // Row

        public virtual bool FullRow { get; set; }



        public ToolBar(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            FullRow = false;
            Left = 0;
            Top = 0;
            Width = 64;
            Height = 24;
            CanFocus = false;
        } // ToolBar



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ToolBar"]);
        } // InitSkin


    } // ToolBar
} // XNAFinalEngine.UserInterface