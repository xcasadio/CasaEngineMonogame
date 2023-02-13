
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.UserInterface.Controls.Auxiliary;
using CasaEngine.UserInterface.Controls.Windows;

namespace CasaEngine.UserInterface.Controls.Buttons
{


    public enum ButtonMode
    {
        Normal,
        PushButton
    } // ButtonMode


    public class Button : ButtonBase
    {


        private Glyph _glyph;

        private ModalResult _modalResult = ModalResult.None;

        private ButtonMode _mode = ButtonMode.Normal;

        private bool _pushed;



        public Glyph Glyph
        {
            get => _glyph;
            set
            {
                _glyph = value;
                if (!Suspended)
                {
                    OnGlyphChanged(new EventArgs());
                }
            }
        } // Glyph

        public ModalResult ModalResult
        {
            get => _modalResult;
            set => _modalResult = value;
        } // ModalResult

        public ButtonMode Mode
        {
            get => _mode;
            set => _mode = value;
        } // Mode

        public bool Pushed
        {
            get => _pushed;
            set
            {
                _pushed = value;
                Invalidate();
            }
        } // Pushed



        public event EventHandler GlyphChanged;



        public Button(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
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
            if (_mode == ButtonMode.PushButton && _pushed)
            {
                var l = SkinInformation.Layers["Control"];
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

            var layer = SkinInformation.Layers["Control"];
            var ox = 0; var oy = 0;

            if (ControlState == ControlState.Pressed)
            {
                ox = 1; oy = 1;
            }
            if (_glyph != null)
            {
                var cont = layer.ContentMargins;
                var r = new Rectangle(rect.Left + cont.Left, rect.Top + cont.Top, rect.Width - cont.Horizontal, rect.Height - cont.Vertical);
                UserInterfaceManager.Renderer.DrawGlyph(_glyph, r);
            }
            else
            {
                UserInterfaceManager.Renderer.DrawString(this, layer, Text, rect, true, ox, oy);
            }
        } // DrawControl



        private void OnGlyphChanged(EventArgs e)
        {
            if (GlyphChanged != null)
            {
                GlyphChanged.Invoke(this, e);
            }
        } // OnGlyphChanged



        protected override void OnClick(EventArgs e)
        {
            var ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left)
            {
                _pushed = !_pushed;
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