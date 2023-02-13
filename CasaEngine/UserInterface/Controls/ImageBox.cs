
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Assets.Textures;

namespace CasaEngine.UserInterface.Controls
{

    public class ImageBox : Control
    {


        private Texture _texture;

        private SizeMode _sizeMode = SizeMode.Normal;

        private Rectangle _sourceRectangle = Rectangle.Empty;



        public Texture Texture
        {
            get => _texture;
            set
            {
                _texture = value;
                _sourceRectangle = new Rectangle(0, 0, _texture.Width, _texture.Height);
                Invalidate();
                if (!Suspended)
                {
                    OnImageChanged(new EventArgs());
                }
            }
        } // Texture

        public Rectangle SourceRectangle
        {
            get => _sourceRectangle;
            set
            {
                if (_texture != null)
                {
                    var left = value.Left;
                    var top = value.Top;
                    var width = value.Width;
                    var height = value.Height;

                    if (left < 0)
                    {
                        left = 0;
                    }

                    if (top < 0)
                    {
                        top = 0;
                    }

                    if (width > _texture.Width)
                    {
                        width = _texture.Width;
                    }

                    if (height > _texture.Height)
                    {
                        height = _texture.Height;
                    }

                    if (left + width > _texture.Width)
                    {
                        width = (_texture.Width - left);
                    }

                    if (top + height > _texture.Height)
                    {
                        height = (_texture.Height - top);
                    }

                    _sourceRectangle = new Rectangle(left, top, width, height);
                }
                else
                {
                    _sourceRectangle = Rectangle.Empty;
                }
                Invalidate();
            }
        } // SourceRectangle

        public SizeMode SizeMode
        {
            get => _sizeMode;
            set
            {
                if (value == SizeMode.Auto && _texture != null)
                {
                    Width = _texture.Width;
                    Height = _texture.Height;
                }
                _sizeMode = value;
                Invalidate();
                if (!Suspended)
                {
                    OnSizeModeChanged(new EventArgs());
                }
            }
        } // SizeMode



        public event EventHandler ImageChanged;
        public event EventHandler SizeModeChanged;



        public ImageBox(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
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
            if (_texture != null)
            {
                switch (_sizeMode)
                {
                    case SizeMode.Normal:
                    case SizeMode.Auto:
                        UserInterfaceManager.Renderer.Draw(_texture.Resource, rect.X, rect.Y, _sourceRectangle, Color);
                        break;
                    case SizeMode.Stretched:
                        UserInterfaceManager.Renderer.Draw(_texture.Resource, rect, _sourceRectangle, Color);
                        break;
                    case SizeMode.Centered:
                        var x = (rect.Width / 2) - (_texture.Width / 2);
                        var y = (rect.Height / 2) - (_texture.Height / 2);
                        UserInterfaceManager.Renderer.Draw(_texture.Resource, x, y, _sourceRectangle, Color);
                        break;
                    case SizeMode.Fit:
                        var aspectRatiorectangle = rect;
                        if (_texture.Width / _texture.Height > rect.Width / rect.Height)
                        {
                            aspectRatiorectangle.Height = (int)(rect.Height * ((float)rect.Width / rect.Height) / ((float)_texture.Width / _texture.Height));
                            aspectRatiorectangle.Y = rect.Y + (rect.Height - aspectRatiorectangle.Height) / 2;
                        }
                        else
                        {
                            aspectRatiorectangle.Width = (int)(rect.Width * ((float)_texture.Width / _texture.Height) / ((float)rect.Width / rect.Height));
                            aspectRatiorectangle.X = rect.X + (rect.Width - aspectRatiorectangle.Width) / 2;
                        }

                        UserInterfaceManager.Renderer.Draw(_texture.Resource, aspectRatiorectangle, _sourceRectangle, Color);
                        break;
                }
            }
        } // DrawControl



        protected virtual void OnImageChanged(EventArgs e)
        {
            if (ImageChanged != null)
            {
                ImageChanged.Invoke(this, e);
            }
        } // OnImageChanged

        protected virtual void OnSizeModeChanged(EventArgs e)
        {
            if (SizeModeChanged != null)
            {
                SizeModeChanged.Invoke(this, e);
            }
        } // OnSizeModeChanged


    } // ImageBox
} // XNAFinalEngine.UserInterface
