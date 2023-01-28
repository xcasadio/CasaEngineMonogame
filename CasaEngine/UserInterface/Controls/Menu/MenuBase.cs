
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System.Collections.Generic;

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Base class for menus.
    /// </summary>
    public abstract class MenuBase : Control
    {

              
        private int itemIndex = -1;
        private readonly List<MenuItem> items = new List<MenuItem>();



        /// <summary>
        /// Selected item.
        /// </summary>
        protected internal int ItemIndex { get { return itemIndex; } set { itemIndex = value; } }
        
        protected internal MenuBase ChildMenu { get; set; }

        /// <summary>
        /// The root of this menu.
        /// </summary>
        protected internal MenuBase RootMenu { get; set; }

        /// <summary>
        /// The father of this menu.
        /// </summary>
        protected internal MenuBase ParentMenu { get; set; }

        /// <summary>
        /// The items of the menu.
        /// </summary>
        public List<MenuItem> Items { get { return items; } }



        protected MenuBase(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            RootMenu = this;
        } // MenuBase


    } // MenuBase
} // XNAFinalEngine.UserInterface