using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// WpfDrawableGameComponent
    /// </summary>
    /// <seealso cref="Microsoft.Xna.Framework.WpfGameComponent" />
    /// <seealso cref="Microsoft.Xna.Framework.IDrawable" />
    public class WpfDrawableGameComponent : WpfGameComponent, IDrawable
    {
        readonly WpfGame _game;
        bool _visible = true;
        int _drawOrder;
        bool _initialized;

        public WpfDrawableGameComponent(WpfGame game) : base(game) => _game = game;

        public event EventHandler<EventArgs> DrawOrderChanged;

        public event EventHandler<EventArgs> VisibleChanged;

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;

        public int DrawOrder
        {
            get => _drawOrder;
            set
            {
                if (_drawOrder == value)
                {
                    return;
                }

                _drawOrder = value;
                var ev = DrawOrderChanged;
                ev?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible = value;
                var ev = VisibleChanged;
                ev?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (!_initialized)
            {
                _initialized = true;
                LoadContent();
            }
        }

        public virtual void Draw(GameTime gameTime) { }

        protected virtual void LoadContent() { }
    }
}