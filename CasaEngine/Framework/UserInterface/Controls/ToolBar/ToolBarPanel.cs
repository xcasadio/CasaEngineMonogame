/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Framework.UserInterface.Controls.ToolBar;

public class ToolBarPanel : Control
{


    public ToolBarPanel(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
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



    protected internal override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);
        AlignBars();
    } // Update

    private void AlignBars()
    {
        var rowOffset = new int[8];
        var height = 0;
        var rowMax = -1;

        foreach (var childControl in ChildrenControls)
        {
            if (childControl is ToolBar)
            {
                var toolBar = childControl as ToolBar;
                if (toolBar.FullRow)
                {
                    toolBar.Width = Width;
                }

                toolBar.Left = rowOffset[toolBar.Row];
                toolBar.Top = toolBar.Row * toolBar.Height + (toolBar.Row > 0 ? 1 : 0);
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
  // XNAFinalEngine.UserInterface