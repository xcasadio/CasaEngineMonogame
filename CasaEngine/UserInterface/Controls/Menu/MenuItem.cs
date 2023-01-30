
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Graphics;

namespace XNAFinalEngine.UserInterface
{

    public class MenuItem
    {


        // Default values.
        private bool enabled = true;

        // Children Items.
        private List<MenuItem> items = new List<MenuItem>();



        public string Text { get; set; }

        public string RightSideText { get; set; }

        public Texture2D Icon { get; set; }

        public bool SeparationLine { get; set; }

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public List<MenuItem> Items
        {
            get => items;
            set => items = value;
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
                Click.Invoke(this, e);
        } // OnClick

        internal void OnSelected(EventArgs e)
        {
            if (Selected != null)
                Selected.Invoke(this, e);
        } // OnSelected


    } // MenuItem
} // XNAFinalEngine.UserInterface