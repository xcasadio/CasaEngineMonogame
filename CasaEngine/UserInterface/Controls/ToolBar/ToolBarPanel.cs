
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
    /// Tool Bar Panel.
    /// </summary>
    public class ToolBarPanel : Control
    {

   
        /// <summary>
        /// Tool Bar Panel.
        /// </summary>
        public ToolBarPanel(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Width = 64;
            Height = 25;
        } // ToolBarPanel


                      
        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ToolBarPanel"]);
        } // InitSkin



        protected internal override void Update(float elapsedTime_)
        {
            base.Update(elapsedTime_);
            AlignBars();
        } // Update

        private void AlignBars()
        {
            int[] rowOffset = new int[8];
            int height = 0;
            int rowMax = -1;

            foreach (Control childControl in ChildrenControls)
            {
                if (childControl is ToolBar)
                {
                    ToolBar toolBar = childControl as ToolBar;
                    if (toolBar.FullRow)
                        toolBar.Width = Width;

                    toolBar.Left = rowOffset[toolBar.Row];
                    toolBar.Top = (toolBar.Row * toolBar.Height) + (toolBar.Row > 0 ? 1 : 0);
                    rowOffset[toolBar.Row] += toolBar.Width + 1;

                    if (toolBar.Row > rowMax)
                    {
                        rowMax = toolBar.Row;
                        height = toolBar.Top + toolBar.Height + 1;
                    }
                }
            }
            Height = height;
        } // AlignBars


    } // ToolBarPanel
} // XNAFinalEngine.UserInterface