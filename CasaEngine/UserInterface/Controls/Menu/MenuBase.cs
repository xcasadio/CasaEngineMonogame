
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using System.Collections.Generic;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Base class for menus.
    /// </summary>
    public abstract class MenuBase : Control
    {

        #region Variables
              
        private int itemIndex = -1;
        private readonly List<MenuItem> items = new List<MenuItem>();

        #endregion

        #region Properties

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

        #endregion

        #region Constructor

        protected MenuBase(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            RootMenu = this;
        } // MenuBase

        #endregion

    } // MenuBase
} // XNAFinalEngine.UserInterface