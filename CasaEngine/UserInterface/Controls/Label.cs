
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


        private Alignment alignment = Alignment.MiddleLeft;

        private bool ellipsis = true;



        public virtual Alignment Alignment
        {
            get => alignment;
            set => alignment = value;
        } // Alignment

        public virtual bool Ellipsis
        {
            get => ellipsis;
            set => ellipsis = value;
        } // Ellipsis



        public Label(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 16;
        } // Label



        protected override void DrawControl(Rectangle rect)
        {
            SkinLayer skinLayer = SkinInformation.Layers[0];
            UserInterfaceManager.Renderer.DrawString(this, skinLayer, Text, rect, true, 0, 0, ellipsis);
        } // DrawControl


    } // Label
} // XNAFinalEngine.UserInterface
