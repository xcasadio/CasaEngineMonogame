
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{


    public enum ButtonMode
    {
        Normal,
        PushButton
    } // ButtonMode


    public class Button : ButtonBase
    {


        private Glyph glyph;

        private ModalResult modalResult = ModalResult.None;

        private ButtonMode mode = ButtonMode.Normal;

        private bool pushed;



        public Glyph Glyph
        {
            get => glyph;
            set
            {
                glyph = value;
                if (!Suspended) OnGlyphChanged(new EventArgs());
            }
        } // Glyph

        public ModalResult ModalResult
        {
            get => modalResult;
            set => modalResult = value;
        } // ModalResult

        public ButtonMode Mode
        {
            get => mode;
            set => mode = value;
        } // Mode

        public bool Pushed
        {
            get => pushed;
            set
            {
                pushed = value;
                Invalidate();
            }
        } // Pushed



        public event EventHandler GlyphChanged;



        public Button(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            SetDefaultSize(72, 24);
        } // Button



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Button"]);
        } // InitSkin



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            GlyphChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            if (mode == ButtonMode.PushButton && pushed)
            {
                SkinLayer l = SkinInformation.Layers["Control"];
                UserInterfaceManager.Renderer.DrawLayer(l, rect, l.States.Pressed.Color, l.States.Pressed.Index);
                if (l.States.Pressed.Overlay)
                {
                    UserInterfaceManager.Renderer.DrawLayer(l, rect, l.Overlays.Pressed.Color, l.Overlays.Pressed.Index);
                }
            }
            else
            {
                base.DrawControl(rect);
            }

            SkinLayer layer = SkinInformation.Layers["Control"];
            int ox = 0; int oy = 0;

            if (ControlState == ControlState.Pressed)
            {
                ox = 1; oy = 1;
            }
            if (glyph != null)
            {
                Margins cont = layer.ContentMargins;
                Rectangle r = new Rectangle(rect.Left + cont.Left, rect.Top + cont.Top, rect.Width - cont.Horizontal, rect.Height - cont.Vertical);
                UserInterfaceManager.Renderer.DrawGlyph(glyph, r);
            }
            else
            {
                UserInterfaceManager.Renderer.DrawString(this, layer, Text, rect, true, ox, oy);
            }
        } // DrawControl



        private void OnGlyphChanged(EventArgs e)
        {
            if (GlyphChanged != null) GlyphChanged.Invoke(this, e);
        } // OnGlyphChanged



        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left)
            {
                pushed = !pushed;
            }

            base.OnClick(e);

            // If the button close the window
            if (ex.Button == MouseButton.Left && Root != null && Root is Window && ModalResult != ModalResult.None)
            {
                ((Window)Root).Close(ModalResult);
            }
        } // OnClick


    } // Button
} // XNAFinalEngine.UserInterface