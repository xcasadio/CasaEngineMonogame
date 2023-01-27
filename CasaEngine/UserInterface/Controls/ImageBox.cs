
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
using CasaEngine.Asset;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Image Box.
    /// </summary>
    public class ImageBox : Control
    {

        #region Variables

        /// <summary>
        /// Texture.
        /// </summary>
        private Texture texture;

        /// <summary>
        /// Size Mode (Normal, Streched, Centered and Auto).
        /// Auto mode changes the control's width and height to the texture's dimentions.
        /// </summary>
        private SizeMode sizeMode = SizeMode.Normal;

        /// <summary>
        /// Allows to cut the texture.
        /// </summary>
        private Rectangle sourceRectangle = Rectangle.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// Texture.
        /// </summary>
        public Texture Texture
        {
            get { return texture; }
            set
            {
                texture = value;
                sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
                Invalidate();
                if (!Suspended) OnImageChanged(new EventArgs());
            }
        } // Texture

        /// <summary>
        /// Allows to cut the texture.
        /// </summary>
        public Rectangle SourceRectangle
        {
            get { return sourceRectangle; }
            set
            {
                if (texture != null)
                {
                    int left   = value.Left;
                    int top    = value.Top;
                    int width  = value.Width;
                    int height = value.Height;

                    if (left < 0) left = 0;
                    if (top < 0)  top = 0;
                    if (width  > texture.Width)  width = texture.Width;
                    if (height > texture.Height) height = texture.Height;
                    if (left + width  > texture.Width)  width  = (texture.Width  - left);
                    if (top  + height > texture.Height) height = (texture.Height - top);

                    sourceRectangle = new Rectangle(left, top, width, height);
                }
                else
                {
                    sourceRectangle = Rectangle.Empty;
                }
                Invalidate();
            }
        } // SourceRectangle

        /// <summary>
        /// Size Mode (Normal, Streched, Fit, Centered and Auto).
        /// </summary>
        /// <remarks>
        /// Normal: It preserves the pixel ratio to 1. If the destination rectangle is smaller than the source rectangle the lower-right part of the texture will be cut.
        /// Auto: It changes the control's width and height to the texture's dimentions.
        /// Centered: Same as normal, but the center of the texture is in the same place as the center of the control.
        /// Stretched: It stretches the texture onto the control.
        /// Fit: It stretches the texture onto the control but maintaining the texture's aspect ratio.
        /// </remarks>
        public SizeMode SizeMode
        {
            get { return sizeMode; }
            set
            {
                if (value == SizeMode.Auto && texture != null)
                {
                    Width  = texture.Width;
                    Height = texture.Height;
                }
                sizeMode = value;
                Invalidate();
                if (!Suspended) OnSizeModeChanged(new EventArgs());
            }
        } // SizeMode

        #endregion

        #region Events

        public event EventHandler ImageChanged;
        public event EventHandler SizeModeChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Image Box.
        /// </summary>
        public ImageBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Color = Color.White;
            SetDefaultSize(50, 50);
        } // ImageBox

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            ImageChanged = null;
            SizeModeChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion

        #region Draw

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            if (texture != null)
            {
                switch (sizeMode)
                {
                    case SizeMode.Normal:
                    case SizeMode.Auto:
                        UserInterfaceManager.Renderer.Draw(texture.Resource, rect.X, rect.Y, sourceRectangle, Color);
                        break;
                    case SizeMode.Stretched:
                        UserInterfaceManager.Renderer.Draw(texture.Resource, rect, sourceRectangle, Color);
                        break;
                    case SizeMode.Centered:
                        int x = (rect.Width  / 2) - (texture.Width  / 2);
                        int y = (rect.Height / 2) - (texture.Height / 2);
                        UserInterfaceManager.Renderer.Draw(texture.Resource, x, y, sourceRectangle, Color);
                        break;
                    case SizeMode.Fit:
                        Rectangle aspectRatiorectangle = rect;
                        if (texture.Width / texture.Height > rect.Width / rect.Height)
                        {
                            aspectRatiorectangle.Height = (int)(rect.Height * ((float)rect.Width / rect.Height) / ((float)texture.Width / texture.Height));
                            aspectRatiorectangle.Y = rect.Y + (rect.Height - aspectRatiorectangle.Height) / 2;
                        }
                        else
                        {
                            aspectRatiorectangle.Width = (int)(rect.Width * ((float)texture.Width / texture.Height) / ((float)rect.Width / rect.Height));
                            aspectRatiorectangle.X = rect.X + (rect.Width - aspectRatiorectangle.Width) / 2;
                        }

                        UserInterfaceManager.Renderer.Draw(texture.Resource, aspectRatiorectangle, sourceRectangle, Color);
                        break;
                }
            }
        } // DrawControl

        #endregion

        #region On Image Changed, On Size Mode Changed

        protected virtual void OnImageChanged(EventArgs e)
        {
            if (ImageChanged != null) ImageChanged.Invoke(this, e);
        } // OnImageChanged

        protected virtual void OnSizeModeChanged(EventArgs e)
        {
            if (SizeModeChanged != null) SizeModeChanged.Invoke(this, e);
        } // OnSizeModeChanged

        #endregion

    } // ImageBox
} // XNAFinalEngine.UserInterface
