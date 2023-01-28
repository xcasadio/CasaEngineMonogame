
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Tool Bar
    /// </summary>
    public class ToolBar : Control
    {

             
        private int row;



        public virtual int Row
        {
            get { return row; }
            set
            {
                row = value;
                if (row < 0) row = 0;
                if (row > 7) row = 7;
            }
        } // Row

        public virtual bool FullRow { get; set; }



        public ToolBar(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
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