using System.ComponentModel;
using System.ComponentModel.Design;

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
                return ((CollapsiblePanel)Component).HeaderText;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["HeaderText"];
                property.SetValue(Component, value);

            }
        }

        public bool UseAnimation
        {
            get
            {
                return ((CollapsiblePanel)Component).UseAnimation;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["UseAnimation"];
                property.SetValue(Component, value);

            }
        }

        public bool Collapsed
        {
            get
            {
                return ((CollapsiblePanel)Component).Collapse;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["Collapse"];
                property.SetValue(Component, value);

            }
        }


        public bool ShowSeparator
        {
            get
            {
                return ((CollapsiblePanel)Component).ShowHeaderSeparator;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["ShowHeaderSeparator"];
                property.SetValue(Component, value);
            }
        }

        public bool UseRoundedCorner
        {
            get
            {
                return ((CollapsiblePanel)Component).RoundedCorners;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["RoundedCorners"];
                property.SetValue(Component, value);
            }
        }

        public int HeaderCornersRadius
        {
            get
            {
                return ((CollapsiblePanel)Component).HeaderCornersRadius;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["HeaderCornersRadius"];
                property.SetValue(Component, value);
            }
        }



        public Image HeaderImage
        {
            get
            {
                return ((CollapsiblePanel)Component).HeaderImage;
            }
            set
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(Component)["HeaderImage"];
                property.SetValue(Component, value);
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
            items.Add(new DesignerActionPropertyItem("UseRoundedCorner", "Rounded corners"));
            items.Add(new DesignerActionPropertyItem("HeaderCornersRadius", "Corner's radius [5,10]"));

            return items;


        }
    }
}
