
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


        private int itemIndex = -1;
        private readonly List<MenuItem> items = new List<MenuItem>();



        protected internal int ItemIndex
        {
            get => itemIndex;
            set => itemIndex = value;
        }

        protected internal MenuBase ChildMenu { get; set; }

        protected internal MenuBase RootMenu { get; set; }

        protected internal MenuBase ParentMenu { get; set; }

        public List<MenuItem> Items => items;


        protected MenuBase(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            RootMenu = this;
        } // MenuBase


    } // MenuBase
} // XNAFinalEngine.UserInterface