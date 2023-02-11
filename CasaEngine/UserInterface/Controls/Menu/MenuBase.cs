
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public abstract class MenuBase : Control
    {


        private int _itemIndex = -1;
        private readonly List<MenuItem> _items = new();



        protected internal int ItemIndex
        {
            get => _itemIndex;
            set => _itemIndex = value;
        }

        protected internal MenuBase ChildMenu { get; set; }

        protected internal MenuBase RootMenu { get; set; }

        protected internal MenuBase ParentMenu { get; set; }

        public List<MenuItem> Items => _items;


        protected MenuBase(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            RootMenu = this;
        } // MenuBase


    } // MenuBase
} // XNAFinalEngine.UserInterface