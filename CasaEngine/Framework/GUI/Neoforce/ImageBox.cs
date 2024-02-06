using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Serialization;

namespace TomShane.Neoforce.Controls;

public class ImageBox : Control
{
    private Texture2D? _image;
    private Guid _spriteAssetId = Guid.Empty;
    private SizeMode _sizeMode = SizeMode.Normal;
    private Rectangle _sourceRect = Rectangle.Empty;

    public Guid SpriteAssetId
    {
        get => _spriteAssetId;
        set
        {
            _spriteAssetId = value;
            LoadImage();
        }
    }

    public Texture2D? Image
    {
        get => _image;
        set
        {
            _image = value;
            _sourceRect = new Rectangle(0, 0, _image?.Width ?? 0, _image?.Height ?? 0);
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

    public event EventHandler? ImageChanged;
    public event EventHandler? SizeModeChanged;

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);
        CanFocus = false;
        Color = Color.White;

        LoadImage();
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        if (_image == null)
        {
            return;
        }

        switch (_sizeMode)
        {
            case SizeMode.Normal:
            case SizeMode.Auto:
                renderer.Draw(_image, rect.X, rect.Y, _sourceRect, Color);
                break;
            case SizeMode.Stretched:
                renderer.Draw(_image, rect, _sourceRect, Color);
                break;
            case SizeMode.Centered:
                {
                    var x = rect.Width / 2 - _image.Width / 2;
                    var y = rect.Height / 2 - _image.Height / 2;

                    renderer.Draw(_image, x, y, _sourceRect, Color);
                    break;
                }
            case SizeMode.Tiled:
                renderer.DrawTileTexture(_image, rect, Color);
                break;
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

    public override void Load(JsonElement element)
    {
        base.Load(element);

        //TODO remove
        if (element.GetProperty("sprite_id").ValueKind == JsonValueKind.Number)
        {
            _spriteAssetId = AssetInfo.GuidsById[element.GetProperty("sprite_id").GetInt32()];
        }
        else
        {
            _spriteAssetId = element.GetProperty("sprite_id").GetGuid();
        }

        SizeMode = element.GetProperty("size_mode").GetEnum<SizeMode>();
    }

    private void LoadImage()
    {
        if (_spriteAssetId != Guid.Empty && Manager != null)
        {
            var spriteData = Manager.CasaEngineGame.AssetContentManager.Load<SpriteData>(_spriteAssetId);
            var sprite = Sprite.Create(spriteData, Manager.CasaEngineGame.AssetContentManager);
            Image = sprite.Texture.Resource;
            SourceRect = sprite.SpriteData.PositionInTexture;
        }
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);

        node.Add("sprite_id", SpriteAssetId);
        node.Add("size_mode", SizeMode);
    }
#endif
}