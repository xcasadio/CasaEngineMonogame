using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Game
{
    public class DrawableGameComponent
        : GameComponent, IDrawable
    {
        private bool _isInitialized;
        private IGraphicsDeviceService _device;
        private int _drawOrder;
        private bool _visible = true;


        public event EventHandler<EventArgs> DrawOrderChanged;
        public event EventHandler<EventArgs> VisibleChanged;


        public DrawableGameComponent(CustomGame game)
            : base(game)
        {

        }

        public GraphicsDevice GraphicsDevice
        {
            get
            {
                if (this._device == null)
                {
                    throw new InvalidOperationException("Component is not initialized");
                }
                return this._device.GraphicsDevice;
            }
        }

        public int DrawOrder
        {
            get => _drawOrder;
            set
            {
                if (_drawOrder != value)
                {
                    _drawOrder = value;
                    OnDrawOrderChanged(this, EventArgs.Empty);
                }
            }
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnVisibleChanged(this, EventArgs.Empty);
                }
            }
        }

        protected virtual void OnDrawOrderChanged(object sender, EventArgs arg)
        {
            if (DrawOrderChanged != null)
            {
                DrawOrderChanged(sender, arg);
            }
        }

        protected virtual void OnVisibleChanged(object sender, EventArgs arg)
        {
            if (VisibleChanged != null)
            {
                VisibleChanged(sender, arg);
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (!_isInitialized)
            {
                this._device = base.Game.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
                if (this._device == null)
                {
                    throw new InvalidOperationException("Service not found: IGraphicsDeviceService");
                }
                this._device.DeviceCreated += OnDeviceCreated;
                this._device.DeviceReset += new EventHandler<EventArgs>(OnDeviceReset);
                this._device.DeviceDisposing += OnDeviceDisposing;

                if (this._device.GraphicsDevice != null)
                {
                    LoadContent();
                }
            }
            _isInitialized = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.UnloadContent();
                if (this._device != null)
                {
                    this._device.DeviceCreated -= OnDeviceCreated;
                    this._device.DeviceDisposing -= OnDeviceDisposing;
                }
            }
            base.Dispose(disposing);
        }

        protected virtual void LoadContent()
        {

        }

        protected virtual void UnloadContent()
        {
        }

        public virtual void Draw(GameTime gameTime)
        {

        }

        private void OnDeviceCreated(object sender, EventArgs arg)
        {
            this.LoadContent();
        }

        private void OnDeviceReset(object sender, EventArgs e)
        {
            this.LoadContent();
        }

        private void OnDeviceDisposing(object sender, EventArgs arg)
        {
            this.UnloadContent();
        }
    }
}
