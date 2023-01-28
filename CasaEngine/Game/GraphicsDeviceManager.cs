using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Game
{
    public class GraphicsDeviceManager : IGraphicsDeviceManager, IDisposable, IGraphicsDeviceService
    {
        public static readonly int DefaultBackBufferWidth = 800;
        public static readonly int DefaultBackBufferHeight = 480;
        private const string RuntimeProfileResourceName = "Microsoft.Xna.Framework.RuntimeProfile";

        private CustomGame game;
        private GraphicsDevice graphicsDevice;
        private int backBufferWidth = DefaultBackBufferWidth;
        private int backBufferHeight = DefaultBackBufferHeight;
        private SurfaceFormat backBufferFormat = SurfaceFormat.Color;
        private DepthFormat depthStencilFormat = DepthFormat.Depth24;
        //private GraphicsProfile graphicsProfile;
        private GraphicsDeviceInformation currentGraphicsDeviceInformation;
        private bool synchronizeWithVerticalRetrace = true;


        private bool needReset = false;


        public event EventHandler<EventArgs> Disposed;
        public event EventHandler<EventArgs> DeviceCreated;
        public event EventHandler<EventArgs> DeviceDisposing;
        public event EventHandler<EventArgs> DeviceReset;
        public event EventHandler<EventArgs> DeviceResetting;
        public event EventHandler<PreparingDeviceSettingsEventArgs> PreparingDeviceSettings;

        public GraphicsDevice GraphicsDevice
        {
            get { return this.graphicsDevice; }
        }

        public GraphicsProfile GraphicsProfile
        {
            get
            {
                return this.currentGraphicsDeviceInformation.GraphicsProfile;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public DepthFormat PreferredDepthStencilFormat
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.DepthStencilFormat;
            }
            set
            {
                this.depthStencilFormat = value;
            }
        }

        public SurfaceFormat PreferredBackBufferFormat
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.BackBufferFormat;
            }
            set
            {
                this.backBufferFormat = value;
            }
        }

        public int PreferredBackBufferWidth
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.BackBufferWidth;
            }
            set
            {
                this.backBufferWidth = value;
            }
        }

        public int PreferredBackBufferHeight
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.BackBufferHeight;
            }
            set
            {
                this.backBufferHeight = value;
            }
        }

        public bool IsFullScreen
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.IsFullScreen;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool SynchronizeWithVerticalRetrace
        {
            get
            {
                return synchronizeWithVerticalRetrace;
            }
            set
            {
                synchronizeWithVerticalRetrace = value;
                throw new NotImplementedException();
            }
        }

        public bool PreferMultiSampling
        {
            get
            {
                return this.currentGraphicsDeviceInformation.PresentationParameters.MultiSampleCount > 0;
            }
            set { throw new NotImplementedException(); }
        }

        public DisplayOrientation SupportedOrientations
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public GraphicsDeviceManager(CustomGame game)
        {
            if (game == null)
                throw new ArgumentNullException("game");
            this.game = game;

            if (game.Services.GetService(typeof(IGraphicsDeviceManager)) != null)
                throw new ArgumentException("The GraphicsDeviceManager was already registered to the game class");
            game.Services.AddService(typeof(IGraphicsDeviceManager), this);

            if (game.Services.GetService(typeof(IGraphicsDeviceService)) != null)
                throw new ArgumentException("The GraphicsDeviceService was already registered to the game class");
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
            if (this.graphicsDevice != null)
            {
                System.Diagnostics.Debugger.Break(); // Test this!!!
                this.graphicsDevice.Dispose();
                this.graphicsDevice = null;
            }

            //TODO: validate graphics device

            this.graphicsDevice = new GraphicsDevice(deviceInformation.Adapter, deviceInformation.GraphicsProfile, deviceInformation.PresentationParameters);
            //GraphicsResourceTracker.Instance.UpdateGraphicsDeviceReference(this.graphicsDevice);

            //TODO: hookup events
            this.graphicsDevice.DeviceResetting += OnDeviceResetting;
            this.graphicsDevice.DeviceReset += OnDeviceReset;

            // Update the vsync value in case it was set before creation of the device
            //SynchronizeWithVerticalRetrace = synchronizeWithVerticalRetrace;

            OnDeviceCreated(this, EventArgs.Empty);
        }

        public void EndDraw()
        {
            this.graphicsDevice.Present();
        }

        public void ApplyChanges()
        {
            GraphicsDeviceInformation graphicsDeviceInformation = FindBestDevice(true);
            OnPreparingDeviceSettings(this, new PreparingDeviceSettingsEventArgs(graphicsDeviceInformation));

            if (graphicsDevice != null)
            {
                if (this.CanResetDevice(graphicsDeviceInformation))
                {
                    OnDeviceResetting(this, EventArgs.Empty);

                    this.graphicsDevice.Reset(graphicsDeviceInformation.PresentationParameters,
                        graphicsDeviceInformation.Adapter);

                    OnDeviceReset(this, EventArgs.Empty);
                }
                else
                {
                    //graphicsDevice.Dispose();
                    //graphicsDevice = null;
                    // Dispose could not be used here because all references to graphicsDevice get dirty!
                    graphicsDevice.Reset(graphicsDeviceInformation.PresentationParameters);
                    //graphicsDevice.Recreate(graphicsDeviceInformation.PresentationParameters);
                }
            }

            if (graphicsDevice == null)
                CreateDevice(graphicsDeviceInformation);

            this.currentGraphicsDeviceInformation = graphicsDeviceInformation;
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
                if (game != null && game.Services.GetService(typeof(IGraphicsDeviceService)) == this)
                    game.Services.RemoveService(typeof(IGraphicsDeviceService));

                if (graphicsDevice != null)
                {
                    graphicsDevice.Dispose();
                    graphicsDevice = null;
                }

                if (Disposed != null)
                    Disposed(this, EventArgs.Empty);
            }
        }

        protected GraphicsDeviceInformation FindBestDevice(bool anySuitableDevice)
        {
            //TODO: implement FindBestDevice

            GraphicsDeviceInformation deviceInformation = new GraphicsDeviceInformation();

            deviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            if (deviceInformation.PresentationParameters != null)
            {
                deviceInformation.PresentationParameters.BackBufferWidth = backBufferWidth;
                deviceInformation.PresentationParameters.DeviceWindowHandle = game.ControlHandle;
                deviceInformation.PresentationParameters.BackBufferFormat = backBufferFormat;
                deviceInformation.PresentationParameters.BackBufferWidth = backBufferWidth;
                deviceInformation.PresentationParameters.BackBufferHeight = backBufferHeight;
                deviceInformation.PresentationParameters.DepthStencilFormat = depthStencilFormat;
                deviceInformation.PresentationParameters.IsFullScreen = false;
                deviceInformation.PresentationParameters.DepthStencilFormat = DepthFormat.Depth24;//DepthFormat.Depth24Stencil8;
                deviceInformation.PresentationParameters.DisplayOrientation = DisplayOrientation.Default;
                deviceInformation.PresentationParameters.MultiSampleCount = 0;
                deviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Two;
                deviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            }

            return deviceInformation;
        }

        protected virtual bool CanResetDevice(GraphicsDeviceInformation newDeviceInfo)
        {
            return GraphicsDevice.GraphicsProfile == newDeviceInfo.GraphicsProfile;
        }

        protected virtual void RankDevices(List<GraphicsDeviceInformation> foundDevices)
        {
            throw new NotImplementedException();
        }

        /*private void Window_OrientationChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Window_ScreenDeviceNameChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }*/

        /*private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            needReset = true;
            // TODO: validate
            if (graphicsDevice != null)
            {

                PresentationParameters parameters = graphicsDevice.PresentationParameters;
                parameters.BackBufferWidth = game.Control.Width; //Bounds.Width;
                parameters.BackBufferHeight = game.Control.Height; //Bounds.Height;
                graphicsDevice.Reset(parameters);
            }          
        }*/

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
                handler(sender, args);
        }
    }
}
