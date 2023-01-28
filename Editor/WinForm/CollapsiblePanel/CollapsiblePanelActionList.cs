
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;

namespace Editor.WinForm.CollapsiblePanel.CollapsiblePanel
{
    public class CollapsiblePanelActionList : DesignerActionList
    {
        public CollapsiblePanelActionList(IComponent component)
            : base(component)
        {

        }

        public string Title
        {
            get
            {
                return ((CollapsiblePanel)this.Component).HeaderText;
            }
            set
            {
                PropertyDescriptor property =  TypeDescriptor.GetProperties(this.Component)["HeaderText"];
                property.SetValue(this.Component, value);

            }
        }

        public bool UseAnimation
        {
            get
            {
                return ((CollapsiblePanel)this.Component).UseAnimation;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["UseAnimation"];
                property.SetValue(this.Component, value);

            }
        }

        public bool Collapsed
        {
            get
            {
                return ((CollapsiblePanel)this.Component).Collapse;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["Collapse"];
                property.SetValue(this.Component, value);

            }
        }


        public bool ShowSeparator
        {
            get
            {
                return ((CollapsiblePanel)this.Component).ShowHeaderSeparator;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["ShowHeaderSeparator"];
                property.SetValue(this.Component, value);
            }
        }

        public bool UseRoundedCorner
        {
            get
            {
                return ((CollapsiblePanel)this.Component).RoundedCorners;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["RoundedCorners"];
                property.SetValue(this.Component, value);
            }
        }

        public int HeaderCornersRadius
        {
            get
            {
                return ((CollapsiblePanel)this.Component).HeaderCornersRadius;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["HeaderCornersRadius"];
                property.SetValue(this.Component, value);
            }
        }



        public Image HeaderImage
        {
            get
            {
                return ((CollapsiblePanel)this.Component).HeaderImage;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(this.Component)["HeaderImage"];
                property.SetValue(this.Component, value);
            }
        }



        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Header Parameters"));
            items.Add(new DesignerActionPropertyItem("Title", "Panel's header text"));
            items.Add(new DesignerActionPropertyItem("HeaderImage", "Image"));
            items.Add(new DesignerActionPropertyItem("UseAnimation", "Animated panel"));
            items.Add(new DesignerActionPropertyItem("Collapsed", "Collapse"));
            items.Add(new DesignerActionPropertyItem("ShowSeparator", "Show borders"));
            items.Add(new DesignerActionPropertyItem("UseRoundedCorner","Rounded corners"));
            items.Add(new DesignerActionPropertyItem("HeaderCornersRadius", "Corner's radius [5,10]"));

            return items;


        }
    }
}
