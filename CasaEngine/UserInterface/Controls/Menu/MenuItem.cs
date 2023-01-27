
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
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Menu Item.
    /// This items are placed in the menu control.
    /// </summary>
    public class MenuItem
    {

        #region Variables

        // Default values.
        private bool enabled = true;

        // Children Items.
        private List<MenuItem> items = new List<MenuItem>();

        #endregion

        #region Properties

        /// <summary>
        /// Showing text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Normally this is used to indicate the keyboard shortcut.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string RightSideText { get; set; }

        /// <summary>
        /// Icon that can be placed to the left of the text.
        /// </summary>
        public Texture2D Icon { get; set; }

        /// <summary>
        /// A separeted item has a line over it.
        /// </summary>
        public bool SeparationLine { get; set; }

        /// <summary>
        /// Enabled?
        /// </summary>
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        /// <summary>
        /// Children Items.
        /// </summary>
        public List<MenuItem> Items { get { return items; } set { items = value; } }

        #endregion

        #region Events

        /// <summary>
        ///  Was the menu item clicked.
        /// </summary>
        public event EventHandler Click;

        /// <summary>
        /// Was the menu item selected. I.e. is the mouse over the item.
        /// </summary>
        public event EventHandler Selected;

        #endregion

        #region Constructors

        /// <summary>
        /// Menu Item.
        /// This items are placed in the menu control.
        /// </summary>
        /// <param name="text">Item name.</param>
        /// <param name="separated">A separeted item has a line over it.</param>
        public MenuItem(string text = "Menu Item", bool separated = false)
        {
            Text = text;
            SeparationLine = separated;
        } // MenuItem

        #endregion

        #region Raise Events
        
        internal void OnClick(EventArgs e)
        {
            if (Click != null) 
                Click.Invoke(this, e);
        } // OnClick

        internal void OnSelected(EventArgs e)
        {
            if (Selected != null) 
                Selected.Invoke(this, e);
        } // OnSelected

        #endregion

    } // MenuItem
} // XNAFinalEngine.UserInterface