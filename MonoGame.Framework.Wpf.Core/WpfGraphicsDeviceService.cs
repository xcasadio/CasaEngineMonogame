using Microsoft.Xna.Framework.Graphics;
using System.Windows;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// WpfGraphicsDeviceService
    /// </summary>
    /// <seealso cref="Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService" />
    /// <seealso cref="Microsoft.Xna.Framework.IGraphicsDeviceManager" />
    public class WpfGraphicsDeviceService : IGraphicsDeviceService, IGraphicsDeviceManager
    {
        const int MsaaSampleLimit = 32;
        readonly WpfGame _host;

        public WpfGraphicsDeviceService(WpfGame host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            if (host.Services.GetService(typeof(IGraphicsDeviceService)) != null)
            {
                throw new NotSupportedException("A graphics device service is already registered.");
            }

            if (host.GraphicsDevice == null)
            {
                throw new ArgumentException("Provided host graphics device is null.");
            }

            GraphicsDevice = host.GraphicsDevice;
            _host.GraphicsDevice.DeviceReset += (sender, args) => DeviceReset?.Invoke(this, args);
            _host.GraphicsDevice.DeviceResetting += (sender, args) => DeviceResetting?.Invoke(this, args);
            host.Services.AddService(typeof(IGraphicsDeviceService), this);
            host.Services.AddService(typeof(IGraphicsDeviceManager), this);
        }

        /// <inheritdoc />
        public event EventHandler<EventArgs> DeviceCreated;

        /// <inheritdoc />
        public event EventHandler<EventArgs> DeviceDisposing;

        /// <inheritdoc />
        public event EventHandler<EventArgs> DeviceReset;

        /// <inheritdoc />
        public event EventHandler<EventArgs> DeviceResetting;

        public GraphicsDevice GraphicsDevice { get; }

        public bool PreferMultiSampling { get; set; }

        public double DpiScalingFactor
        {
            get => _host.DpiScalingFactor;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(DpiScalingFactor), "value must be positive");
                }

                _host.DpiScalingFactor = value;
            }
        }

        public double SystemDpiScalingFactor => PresentationSource.FromVisual(_host).CompositionTarget.TransformToDevice.M11;

        public int PreferredBackBufferWidth => (int)_host.ActualWidth;

        public int PreferredBackBufferHeight => (int)_host.ActualHeight;

        public bool BeginDraw() => true;

        public void CreateDevice()
        {
            ApplyChanges();
            DeviceCreated?.Invoke(this, EventArgs.Empty);
        }

        public void EndDraw() { }

        public void ApplyChanges()
        {
            var w = Math.Max((int)_host.ActualWidth, 1);
            var h = Math.Max((int)_host.ActualHeight, 1);
            var pp = new PresentationParameters
            {
                // set to windows limit, if gpu doesn't support it, monogame will autom. scale it down to the next supported level
                MultiSampleCount = PreferMultiSampling ? MsaaSampleLimit : 0,
                BackBufferWidth = w,
                BackBufferHeight = h,
                DeviceWindowHandle = IntPtr.Zero
            };
            // would be so easy to just call reset. but for some reason monogame doesn't want the WindowHandle to be null on reset (but it's totally fine to be null on create)
            // GraphicsDevice.Reset(pp);
            DeviceDisposing?.Invoke(this, EventArgs.Empty);
            // manually work around it by telling our base implementation to handle the changes
            _host.RecreateGraphicsDevice(pp);
        }
    }
}
