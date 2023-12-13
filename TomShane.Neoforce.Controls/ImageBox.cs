using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls;

public class ImageBox : Control
{
    private Texture2D _image;
    private SizeMode _sizeMode = SizeMode.Normal;
    private Rectangle _sourceRect = Rectangle.Empty;

    public Texture2D Image
    {
        get => _image;
        set
        {
            _image = value;
            _sourceRect = new Rectangle(0, 0, _image.Width, _image.Height);
            Invalidate();
            if (!Suspended)
            {
                OnImageChanged(new EventArgs());
            }
        }
    }

    public Rectangle SourceRect
    {
        get => _sourceRect;
        set
        {
            if (value != null && _image != null)
            {
                var l = value.Left;
                var t = value.Top;
                var w = value.Width;
                var h = value.Height;

                if (l < 0)
                {
                    l = 0;
                }

                if (t < 0)
                {
                    t = 0;
                }

                if (w > _image.Width)
                {
                    w = _image.Width;
                }

                if (h > _image.Height)
                {
                    h = _image.Height;
                }

                if (l + w > _image.Width)
                {
                    w = _image.Width - l;
                }

                if (t + h > _image.Height)
                {
                    h = _image.Height - t;
                }

                _sourceRect = new Rectangle(l, t, w, h);
            }
            else if (_image != null)
            {
                _sourceRect = new Rectangle(0, 0, _image.Width, _image.Height);
            }
            else
            {
                _sourceRect = Rectangle.Empty;
            }
            Invalidate();
        }
    }

    public SizeMode SizeMode
    {
        get => _sizeMode;
        set
        {
            if (value == SizeMode.Auto && _image != null)
            {
                Width = _image.Width;
                Height = _image.Height;
            }
            _sizeMode = value;
            Invalidate();
            if (!Suspended)
            {
                OnSizeModeChanged(new EventArgs());
            }
        }
    }

    public event EventHandler ImageChanged;
    public event EventHandler SizeModeChanged;

    public ImageBox(Manager manager) : base(manager)
    {
    }

    public override void Init()
    {
        base.Init();
        CanFocus = false;
        Color = Color.White;
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        if (_image != null)
        {
            if (_sizeMode == SizeMode.Normal)
            {
                renderer.Draw(_image, rect.X, rect.Y, _sourceRect, Color);
            }
            else if (_sizeMode == SizeMode.Auto)
            {
                renderer.Draw(_image, rect.X, rect.Y, _sourceRect, Color);
            }
            else if (_sizeMode == SizeMode.Stretched)
            {
                renderer.Draw(_image, rect, _sourceRect, Color);
            }
            else if (_sizeMode == SizeMode.Centered)
            {
                var x = rect.Width / 2 - _image.Width / 2;
                var y = rect.Height / 2 - _image.Height / 2;

                renderer.Draw(_image, x, y, _sourceRect, Color);
            }
            else if (_sizeMode == SizeMode.Tiled)
            {
                renderer.DrawTileTexture(_image, rect, Color);
            }
        }
    }

    protected virtual void OnImageChanged(EventArgs e)
    {
        ImageChanged?.Invoke(this, e);
    }

    protected virtual void OnSizeModeChanged(EventArgs e)
    {
        SizeModeChanged?.Invoke(this, e);
    }

}