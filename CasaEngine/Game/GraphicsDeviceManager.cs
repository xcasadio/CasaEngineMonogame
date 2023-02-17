using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Game
{
    /*public class GraphicsDeviceManager : IGraphicsDeviceManager, IDisposable, IGraphicsDeviceService
    {
        public static readonly int DefaultBackBufferWidth = 800;
        public static readonly int DefaultBackBufferHeight = 480;
        private const string RuntimeProfileResourceName = "Microsoft.Xna.Framework.RuntimeProfile";

        private readonly CustomGame? _game;
        private GraphicsDevice? _graphicsDevice;
        private int _backBufferWidth = DefaultBackBufferWidth;
        private int _backBufferHeight = DefaultBackBufferHeight;
        private SurfaceFormat _backBufferFormat = SurfaceFormat.Color;
        private DepthFormat _depthStencilFormat = DepthFormat.Depth24;
        //private GraphicsProfile graphicsProfile;
        private GraphicsDeviceInformation? _currentGraphicsDeviceInformation;
        private bool _synchronizeWithVerticalRetrace = true;


        private bool _needReset = false;


        public event EventHandler<EventArgs> Disposed;
        public event EventHandler<EventArgs> DeviceCreated;
        public event EventHandler<EventArgs> DeviceDisposing;
        public event EventHandler<EventArgs> DeviceReset;
        public event EventHandler<EventArgs> DeviceResetting;
        public event EventHandler<PreparingDeviceSettingsEventArgs> PreparingDeviceSettings;

        public GraphicsDevice GraphicsDevice => _graphicsDevice;

        public GraphicsProfile GraphicsProfile
        {
            get => _currentGraphicsDeviceInformation.GraphicsProfile;
            set => throw new NotImplementedException();
        }

        public DepthFormat PreferredDepthStencilFormat
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.DepthStencilFormat;
            set => _depthStencilFormat = value;
        }

        public SurfaceFormat PreferredBackBufferFormat
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.BackBufferFormat;
            set => _backBufferFormat = value;
        }

        public int PreferredBackBufferWidth
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.BackBufferWidth;
            set => _backBufferWidth = value;
        }

        public int PreferredBackBufferHeight
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.BackBufferHeight;
            set => _backBufferHeight = value;
        }

        public bool IsFullScreen
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.IsFullScreen;
            set => throw new NotImplementedException();
        }

        public bool SynchronizeWithVerticalRetrace
        {
            get => _synchronizeWithVerticalRetrace;
            set
            {
                _synchronizeWithVerticalRetrace = value;
                throw new NotImplementedException();
            }
        }

        public bool PreferMultiSampling
        {
            get => _currentGraphicsDeviceInformation.PresentationParameters.MultiSampleCount > 0;
            set => throw new NotImplementedException();
        }

        public DisplayOrientation SupportedOrientations
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public GraphicsDeviceManager(CustomGame game)
        {
            if (game == null)
            {
                throw new ArgumentNullException("game");
            }

            _game = game;

            if (game.Services.GetService(typeof(IGraphicsDeviceManager)) != null)
            {
                throw new ArgumentException("The GraphicsDeviceManager was already registered to the game class");
            }

            game.Services.AddService(typeof(IGraphicsDeviceManager), this);

            if (game.Services.GetService(typeof(IGraphicsDeviceService)) != null)
            {
                throw new ArgumentException("The GraphicsDeviceService was already registered to the game class");
            }

            game.Services.AddService(typeof(IGraphicsDeviceService), this);

            //game.Control.ClientSizeChanged += Window_ClientSizeChanged;
            //game.Control.ScreenDeviceNameChanged += Window_ScreenDeviceNameChanged;
            //game.Control.OrientationChanged += Window_OrientationChanged;
        }

        public bool BeginDraw()
        {
            return true;
        }

        public void CreateDevice()
        {
            ApplyChanges();
        }

        private void CreateDevice(GraphicsDeviceInformation deviceInformation)
        {
            if (_graphicsDevice != null)
            {
                System.Diagnostics.Debugger.Break(); // Test this!!!
                _graphicsDevice.Dispose();
                _graphicsDevice = null;
            }

            //TODO: validate graphics device

            _graphicsDevice = new GraphicsDevice(deviceInformation.Adapter, deviceInformation.GraphicsProfile, deviceInformation.PresentationParameters);
            //GraphicsResourceTracker.Instance.UpdateGraphicsDeviceReference(this.graphicsDevice);

            //TODO: hookup events
            _graphicsDevice.DeviceResetting += OnDeviceResetting;
            _graphicsDevice.DeviceReset += OnDeviceReset;

            // Update the vsync value in case it was set before creation of the device
            //SynchronizeWithVerticalRetrace = synchronizeWithVerticalRetrace;

            OnDeviceCreated(this, EventArgs.Empty);
        }

        public void EndDraw()
        {
            _graphicsDevice.Present();
        }

        public void ApplyChanges()
        {
            var graphicsDeviceInformation = FindBestDevice(true);
            OnPreparingDeviceSettings(this, new PreparingDeviceSettingsEventArgs(graphicsDeviceInformation));

            if (_graphicsDevice != null)
            {
                if (CanResetDevice(graphicsDeviceInformation))
                {
                    OnDeviceResetting(this, EventArgs.Empty);

                    _graphicsDevice.Reset(graphicsDeviceInformation.PresentationParameters);

                    OnDeviceReset(this, EventArgs.Empty);
                }
                else
                {
                    //graphicsDevice.Dispose();
                    //graphicsDevice = null;
                    // Dispose could not be used here because all references to graphicsDevice get dirty!
                    _graphicsDevice.Reset(graphicsDeviceInformation.PresentationParameters);
                    //graphicsDevice.Recreate(graphicsDeviceInformation.PresentationParameters);
                }
            }

            if (_graphicsDevice == null)
            {
                CreateDevice(graphicsDeviceInformation);
            }

            _currentGraphicsDeviceInformation = graphicsDeviceInformation;
        }

        public void ToggleFullScreen()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_game != null && _game.Services.GetService(typeof(IGraphicsDeviceService)) == this)
                {
                    _game.Services.RemoveService(typeof(IGraphicsDeviceService));
                }

                if (_graphicsDevice != null)
                {
                    _graphicsDevice.Dispose();
                    _graphicsDevice = null;
                }

                if (Disposed != null)
                {
                    Disposed(this, EventArgs.Empty);
                }
            }
        }

        protected GraphicsDeviceInformation FindBestDevice(bool anySuitableDevice)
        {
            return new GraphicsDeviceInformation
            {
                Adapter = GraphicsAdapter.DefaultAdapter,//new GraphicsAdapter(),
                GraphicsProfile = GraphicsProfile.HiDef,
                PresentationParameters = new PresentationParameters
                {
                    BackBufferWidth = _backBufferWidth,
                    BackBufferHeight = _backBufferHeight,
                    BackBufferFormat = _backBufferFormat,
                    DepthStencilFormat = _depthStencilFormat,
                    DeviceWindowHandle = _game.ControlHandle,
                    IsFullScreen = false,
                    DisplayOrientation = DisplayOrientation.Default,
                    MultiSampleCount = 0,
                    PresentationInterval = PresentInterval.Two,
                    RenderTargetUsage = RenderTargetUsage.DiscardContents
                }
            };
        }

        protected virtual bool CanResetDevice(GraphicsDeviceInformation newDeviceInfo)
        {
            return GraphicsDevice.GraphicsProfile == newDeviceInfo.GraphicsProfile;
        }

        protected virtual void RankDevices(List<GraphicsDeviceInformation> foundDevices)
        {
            throw new NotImplementedException();
        }


    protected virtual void OnDeviceCreated(object sender, EventArgs args)
    {
        RaiseEventIfNotNull(DeviceCreated, sender, args);
    }

    protected virtual void OnDeviceDisposing(object sender, EventArgs args)
    {
        RaiseEventIfNotNull(DeviceDisposing, sender, args);
    }

    protected virtual void OnDeviceReset(object sender, EventArgs args)
    {
        RaiseEventIfNotNull(DeviceReset, sender, args);
    }

    protected virtual void OnDeviceResetting(object sender, EventArgs args)
    {
        RaiseEventIfNotNull(DeviceResetting, sender, args);
    }

    protected virtual void OnPreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs args)
    {
        RaiseEventIfNotNull(PreparingDeviceSettings, sender, args);
    }

    private void RaiseEventIfNotNull<T>(EventHandler<T> handler, object sender, T args) where T : EventArgs
    {
        if (handler != null)
        {
            handler(sender, args);
        }
    }
}
*/
}
