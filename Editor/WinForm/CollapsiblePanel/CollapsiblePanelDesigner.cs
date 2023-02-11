
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms.Design;
using System.ComponentModel.Design;
using System.Diagnostics;

namespace Editor.WinForm.CollapsiblePanel.CollapsiblePanel
{
    public class CollapsiblePanelDesigner : ParentControlDesigner
    {

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                DesignerActionListCollection collection = new DesignerActionListCollection();
                if (Control != null && Control is CollapsiblePanel)
                {
                    CollapsiblePanel panel = (CollapsiblePanel)Control;
                    if (!string.IsNullOrEmpty(panel.Name))
                    {
                        if (string.IsNullOrEmpty(panel.HeaderText))
                        {
                            panel.HeaderText = panel.Name;
                        }
                    }
                }

                collection.Add(new CollapsiblePanelActionList(Control));

                return collection;
            }
        }





    }
}
