
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class Label : Control
    {


        private Alignment _alignment = Alignment.MiddleLeft;

        private bool _ellipsis = true;



        public virtual Alignment Alignment
        {
            get => _alignment;
            set => _alignment = value;
        } // Alignment

        public virtual bool Ellipsis
        {
            get => _ellipsis;
            set => _ellipsis = value;
        } // Ellipsis



        public Label(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 16;
        } // Label



        protected override void DrawControl(Rectangle rect)
        {
            SkinLayer skinLayer = SkinInformation.Layers[0];
            UserInterfaceManager.Renderer.DrawString(this, skinLayer, Text, rect, true, 0, 0, _ellipsis);
        } // DrawControl


    } // Label
} // XNAFinalEngine.UserInterface
