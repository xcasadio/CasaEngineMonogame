
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Asset;


namespace XNAFinalEngine.UserInterface
{

    public class ImageBox : Control
    {


        private Texture texture;

        private SizeMode sizeMode = SizeMode.Normal;

        private Rectangle sourceRectangle = Rectangle.Empty;



        public Texture Texture
        {
            get => texture;
            set
            {
                texture = value;
                sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
                Invalidate();
                if (!Suspended) OnImageChanged(new EventArgs());
            }
        } // Texture

        public Rectangle SourceRectangle
        {
            get => sourceRectangle;
            set
            {
                if (texture != null)
                {
                    int left = value.Left;
                    int top = value.Top;
                    int width = value.Width;
                    int height = value.Height;

                    if (left < 0) left = 0;
                    if (top < 0) top = 0;
                    if (width > texture.Width) width = texture.Width;
                    if (height > texture.Height) height = texture.Height;
                    if (left + width > texture.Width) width = (texture.Width - left);
                    if (top + height > texture.Height) height = (texture.Height - top);

                    sourceRectangle = new Rectangle(left, top, width, height);
                }
                else
                {
                    sourceRectangle = Rectangle.Empty;
                }
                Invalidate();
            }
        } // SourceRectangle

        public SizeMode SizeMode
        {
            get => sizeMode;
            set
            {
                if (value == SizeMode.Auto && texture != null)
                {
                    Width = texture.Width;
                    Height = texture.Height;
                }
                sizeMode = value;
                Invalidate();
                if (!Suspended) OnSizeModeChanged(new EventArgs());
            }
        } // SizeMode



        public event EventHandler ImageChanged;
        public event EventHandler SizeModeChanged;



        public ImageBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Color = Color.White;
            SetDefaultSize(50, 50);
        } // ImageBox



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            ImageChanged = null;
            SizeModeChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



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
                        int x = (rect.Width / 2) - (texture.Width / 2);
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



        protected virtual void OnImageChanged(EventArgs e)
        {
            if (ImageChanged != null) ImageChanged.Invoke(this, e);
        } // OnImageChanged

        protected virtual void OnSizeModeChanged(EventArgs e)
        {
            if (SizeModeChanged != null) SizeModeChanged.Invoke(this, e);
        } // OnSizeModeChanged


    } // ImageBox
} // XNAFinalEngine.UserInterface
