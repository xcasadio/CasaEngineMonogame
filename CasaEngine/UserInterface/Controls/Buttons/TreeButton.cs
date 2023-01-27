
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
using Microsoft.Xna.Framework;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Tree Button
    /// </summary>
    public class TreeButton : ButtonBase
    {

        #region Variables

        /// <summary>
        /// Cheked?
        /// </summary>
        private bool isChecked;

        #endregion

        #region Properties

        /// <summary>
        /// Cheked?
        /// </summary>
        public virtual bool Checked
        {
            get { return isChecked; }
            set
            {
                isChecked = value;
                Invalidate();
                if (!Suspended) 
                    OnCheckedChanged(new EventArgs());
            }
        } // Checked

        #endregion

        #region Events
                
        public event EventHandler CheckedChanged;

        #endregion

        #region Constructor
   
        /// <summary>
        /// Tree Button.
        /// </summary>
        public TreeButton(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CheckLayer(SkinInformation, "Control");

            Width = 64;
            Height = 16;
        } // TreeButton

        #endregion

        #region Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TreeButton"]);
        } // InitSkin

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            CheckedChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion

        #region Draw

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            SkinLayer layer = SkinInformation.Layers["Checked"];

            if (!isChecked)
            {
                layer = SkinInformation.Layers["Control"];
            }

            rect.Width = layer.Width;
            rect.Height = layer.Height;
            Rectangle rc = new Rectangle(rect.Left + rect.Width + 4, rect.Y, Width - (layer.Width + 4), rect.Height);

            UserInterfaceManager.Renderer.DrawLayer(this, layer, rect);
            UserInterfaceManager.Renderer.DrawString(this, layer, Text, rc, false, 0, 0);
        } // DrawControl

        #endregion

        #region On Click

        protected override void OnClick(EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
            {
                Checked = !Checked;
            }
            base.OnClick(e);
        } // OnClick

        #endregion

        #region On Checked Changed

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            if (CheckedChanged != null) CheckedChanged.Invoke(this, e);
        } // OnCheckedChanged

        #endregion

    } // TreeButton
} // XNAFinalEngine.UserInterface
