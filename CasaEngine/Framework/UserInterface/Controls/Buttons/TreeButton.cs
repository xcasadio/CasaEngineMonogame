/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Framework.UserInterface.Controls.Buttons
{

    public class TreeButton : ButtonBase
    {


        private bool _isChecked;



        public virtual bool Checked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                Invalidate();
                if (!Suspended)
                {
                    OnCheckedChanged(new EventArgs());
                }
            }
        } // Checked



        public event EventHandler CheckedChanged;



        public TreeButton(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CheckLayer(SkinInformation, "Control");

            Width = 64;
            Height = 16;
        } // TreeButton



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TreeButton"]);
        } // InitSkin



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            CheckedChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            var layer = SkinInformation.Layers["Checked"];

            if (!_isChecked)
            {
                layer = SkinInformation.Layers["Control"];
            }

            rect.Width = layer.Width;
            rect.Height = layer.Height;
            var rc = new Rectangle(rect.Left + rect.Width + 4, rect.Y, Width - (layer.Width + 4), rect.Height);

            UserInterfaceManager.Renderer.DrawLayer(this, layer, rect);
            UserInterfaceManager.Renderer.DrawString(this, layer, Text, rc, false, 0, 0);
        } // DrawControl



        protected override void OnClick(EventArgs e)
        {
            var ex = e is MouseEventArgs ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
            {
                Checked = !Checked;
            }
            base.OnClick(e);
        } // OnClick



        protected virtual void OnCheckedChanged(EventArgs e)
        {
            if (CheckedChanged != null)
            {
                CheckedChanged.Invoke(this, e);
            }
        } // OnCheckedChanged


    } // TreeButton
} // XNAFinalEngine.UserInterface
