
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.UserInterface.Controls.Menu
{

    public class MenuItem
    {


        // Default values.
        private bool _enabled = true;

        // Children Items.
        private List<MenuItem> _items = new();



        public string Text { get; set; }

        public string RightSideText { get; set; }

        public Texture2D Icon { get; set; }

        public bool SeparationLine { get; set; }

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public List<MenuItem> Items
        {
            get => _items;
            set => _items = value;
        }



        public event EventHandler Click;

        public event EventHandler Selected;



        public MenuItem(string text = "Menu Item", bool separated = false)
        {
            Text = text;
            SeparationLine = separated;
        } // MenuItem



        internal void OnClick(EventArgs e)
        {
            if (Click != null)
            {
                Click.Invoke(this, e);
            }
        } // OnClick

        internal void OnSelected(EventArgs e)
        {
            if (Selected != null)
            {
                Selected.Invoke(this, e);
            }
        } // OnSelected


    } // MenuItem
} // XNAFinalEngine.UserInterface