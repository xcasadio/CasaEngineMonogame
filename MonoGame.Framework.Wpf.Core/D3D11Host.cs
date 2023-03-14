using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Internals;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// D3D11Host
    /// </summary>
    /// <seealso cref="System.Windows.Controls.Image" />
    /// <seealso cref="System.IDisposable" />
    public abstract class D3D11Host : Image, IDisposable
    {
        static readonly object GraphicsDeviceLock = new object();
        bool _isRendering;

        static GraphicsDevice _staticGraphicsDevice;
        GraphicsDevice _graphicsDevice;
        static bool? _isInDesignMode;
        static int _referenceCount;

        D3D11Image _d3D11Image;
        bool _disposed;
        TimeSpan _lastRenderingTime;
        bool _loaded;
        bool GraphicsDeviceInitialized;

        RenderTarget2D _sharedRenderTarget;
        RenderTarget2D _cachedRenderTarget;
        bool _resetBackBuffer;
        bool _dpiChanged;
        bool _isActive;
        SpriteBatch _spriteBatch;
        double _dpiScalingFactor = 1;
        static bool _useASingleSharedGraphicsDevice = false;
        readonly List<IDisposable> _toBeDisposedNextFrame = new List<IDisposable>();

        protected D3D11Host()
        {
            // defaulting to fill as that's what's needed in most cases
            Stretch = Stretch.Fill;
            StretchDirection = StretchDirection.Both;
            Loaded += OnLoaded;
        }

        #region Dispose

        public void Dispose()
        {
            // each of those has its own check for disposed
            StopRendering();
            UnitializeImageSource();
            DisposeRenderTargetsFromPreviousFrames();
            if (GraphicsDeviceInitialized)
            {
                UninitializeGraphicsDevice();
                GraphicsDeviceInitialized = false;
            }

            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Activated = null;
            Deactivated = null;
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }
            Dispose(true);
        }

        protected abstract void Dispose(bool disposing);

        #endregion

        public static bool UseASingleSharedGraphicsDevice
        {
            get => _useASingleSharedGraphicsDevice;
            set
            {
                if (_referenceCount > 0)
                {
                    throw new InvalidOperationException($"{nameof(UseASingleSharedGraphicsDevice)} must be set before the first instance is created and cannot be changed during runtime.");
                }

                _useASingleSharedGraphicsDevice = value;
            }
        }

        public bool IsFixedTimeStep => false;

        public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromTicks(160000); // 60 fps

        public static bool IsInDesignMode
        {
            get
            {
                if (!_isInDesignMode.HasValue)
                {
                    _isInDesignMode = (bool)DependencyPropertyDescriptor.FromProperty(DesignerProperties.IsInDesignModeProperty, typeof(FrameworkElement)).Metadata.DefaultValue;
                }

                return _isInDesignMode.Value;
            }
        }

        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive == value)
                {
                    return;
                }

                _isActive = value;
                if (!_disposed)
                {
                    if (IsActive)
                    {
                        Activated?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        Deactivated?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public double DpiScalingFactor
        {
            get => _dpiScalingFactor;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(DpiScalingFactor), "value must be positive");
                }

                if (_dpiScalingFactor == value)
                {
                    return;
                }

                _dpiScalingFactor = value;
                _dpiChanged = true;
            }
        }

        public GraphicsDevice GraphicsDevice => UseASingleSharedGraphicsDevice ? _staticGraphicsDevice : _graphicsDevice;

        public GameServiceContainer Services { get; } = new GameServiceContainer();

        public event EventHandler<EventArgs> Activated;

        public event EventHandler<EventArgs> Deactivated;

        protected virtual void Initialize()
        {
            _disposed = false;
            var graphicsDeviceManager = (IGraphicsDeviceManager)Services.GetService(typeof(IGraphicsDeviceManager));
            if (graphicsDeviceManager == null)
            {
                throw new NotSupportedException($"Services must contain a {nameof(IGraphicsDeviceManager)} instance");
            }

            graphicsDeviceManager.CreateDevice();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            _resetBackBuffer = true;
            base.OnRenderSizeChanged(sizeInfo);
        }

        protected virtual void Render(GameTime time) { }

        void InitializeGraphicsDevice()
        {
            lock (GraphicsDeviceLock)
            {
                _referenceCount++;
                if (_referenceCount == 1 || !UseASingleSharedGraphicsDevice)
                {
                    // Create Direct3D 11 device.
                    var gd = CreateSharedGraphicsDevice(new PresentationParameters
                    {
                        // Do not associate graphics device with window.
                        DeviceWindowHandle = IntPtr.Zero
                    });
                    if (UseASingleSharedGraphicsDevice)
                    {
                        _staticGraphicsDevice = gd;
                    }
                    else
                    {
                        _graphicsDevice = gd;
                    }
                }
            }
        }

        // TODO: could allow user to chose which adapter to use
        static GraphicsDevice CreateSharedGraphicsDevice(PresentationParameters presentationParameters) =>
            new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, presentationParameters);

        public void RecreateGraphicsDevice(PresentationParameters pp)
        {
            lock (GraphicsDeviceLock)
            {
                if (GraphicsDevice == null)
                {
                    throw new NotSupportedException("Can only recreate graphics device when one already exists. Initialize one first!");
                }

                CreateGraphicsDeviceDependentResources(pp);
            }
        }

        void UninitializeGraphicsDevice()
        {
            lock (GraphicsDeviceLock)
            {
                _referenceCount--;
                if (_referenceCount == 0 || !UseASingleSharedGraphicsDevice)
                {
                    GraphicsDevice.PresentationParameters.DeviceWindowHandle = new IntPtr(1);
                    GraphicsDevice.Reset();
                    GraphicsDevice.Dispose();
                    if (UseASingleSharedGraphicsDevice)
                    {
                        _staticGraphicsDevice = null;
                    }
                    else
                    {
                        _graphicsDevice = null;
                    }
                }
            }
        }

        void CreateBackBuffer()
        {
            _d3D11Image.SetBackBuffer(null);
            if (_sharedRenderTarget != null)
            {
                _toBeDisposedNextFrame.Add(_sharedRenderTarget);
            }

            var sampleCount = 0;
            if (_cachedRenderTarget != null)
            {
                sampleCount = _cachedRenderTarget.MultiSampleCount;
                _toBeDisposedNextFrame.Add(_cachedRenderTarget);
            }
            CreateGraphicsDeviceDependentResources(new PresentationParameters
            {
                BackBufferWidth = Math.Max((int)ActualWidth, 1),
                BackBufferHeight = Math.Max((int)ActualHeight, 1),
                MultiSampleCount = sampleCount
            });
        }

        void CreateGraphicsDeviceDependentResources(PresentationParameters pp)
        {
            var width = pp.BackBufferWidth;
            var height = pp.BackBufferHeight;
            var ms = pp.MultiSampleCount;
            _sharedRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Bgr32, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents, true);
            _d3D11Image.SetBackBuffer(_sharedRenderTarget);
            _cachedRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Bgr32, DepthFormat.Depth24Stencil8, ms, RenderTargetUsage.PreserveContents, false);
        }

        void InitializeImageSource()
        {
            _d3D11Image = new D3D11Image();
            _d3D11Image.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;
            CreateBackBuffer();
            Source = _d3D11Image;
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (_d3D11Image.IsFrontBufferAvailable)
            {
                StartRendering();
                _resetBackBuffer = true;
            }
            else
            {
                StopRendering();
            }
        }

        void OnLoaded(object sender, RoutedEventArgs eventArgs)
        {
            if (IsInDesignMode || _loaded)
            {
                return;
            }

            _loaded = true;

            // get the current window and register Activate/Deactivate
            var window = this.FindParent<Window>();
            if (window == null)
            {
                throw new NotSupportedException("The game control does not have a parent window, this is currently not supported");
            }

            window.Activated += OnWindowActivated;
            window.Deactivated += OnWindowDeactivated;
            window.Closed += OnWindowClosed;
            var tabControl = this.FindParent<TabControl>();
            // if there is any parent that is a tabitem, we must be inside a TabControl

            var windowIsInTab = tabControl != null;
            if (windowIsInTab)
            {
                tabControl.SelectionChanged += TabChanged;
            }

            // check whether current window is indeed the active one (usually it will be) but there are some edge cases (programmatically show a tab with this game control after a timer) -> window doesn't have to be active
            var active = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            if (active == window && IsVisible)
            {
                IsActive = true;
            }

            if (!GraphicsDeviceInitialized)
            {
                InitializeGraphicsDevice();
                GraphicsDeviceInitialized = true;
            }
            InitializeImageSource();
            try
            {
                Initialize();
                StartRendering();
            }
            catch (Exception ex)
            {
                if (Environment.Is64BitOperatingSystem || Environment.Is64BitProcess)
                {
                    // catch and rethrow because WPF just swallows it silently on x64..
                    var deCancerifyWpf = new BackgroundWorker();
                    deCancerifyWpf.DoWork += (e, arg) => { arg.Result = arg.Argument; };
                    deCancerifyWpf.RunWorkerCompleted += (e, arg) => throw new Exception("Initialize failed, see inner exception for details.", (Exception)arg.Result);
                    deCancerifyWpf.RunWorkerAsync(ex);
                }
            }
        }

        void TabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                // are we being added or removed?
                var gameParent = this.FindParent<TabItem>();
                if (e.AddedItems.Contains(gameParent))
                {
                    IsActive = true;
                }
                else if (e.RemovedItems.Contains(gameParent))
                {
                    IsActive = false;
                }
            }
        }

        void OnWindowClosed(object sender, EventArgs e) => Dispose();

        void OnWindowActivated(object sender, EventArgs e)
        {
            var tabControl = this.FindParent<TabControl>();
            // if there is any parent that is a tabitem, we must be inside a TabControl
            var windowIsInTab = tabControl != null;
            if (windowIsInTab)
            {
                // inside a tab, check whether the tab is active
                var tabItem = this.FindParent<TabItem>();
                // only set to true for the currently active tab
                IsActive = tabControl.SelectedItem == tabItem;
            }
            else
            // regular control on the window
            {
                IsActive = true;
            }
        }

        void OnWindowDeactivated(object sender, EventArgs e) => IsActive = false;

        void OnRendering(object sender, EventArgs eventArgs)
        {
            if (!_isRendering)
            {
                return;
            }

            if (_toBeDisposedNextFrame.Count > 0)
            {
                DisposeRenderTargetsFromPreviousFrames();
            }

            // Recreate back buffer if necessary.
            if (_resetBackBuffer || _dpiChanged)
            {
                CreateBackBuffer();
                _dpiChanged = false;
            }

            // CompositionTarget.Rendering event may be raised multiple times per frame (e.g. during window resizing).
            // this will be apparent when the last rendering time equals the new argument
            var renderingEventArgs = (RenderingEventArgs)eventArgs;
            if (_lastRenderingTime != renderingEventArgs.RenderingTime)
            {
                // get time since last actual rendering
                var deltaTicks = renderingEventArgs.RenderingTime.Ticks - _lastRenderingTime.Ticks;
                // accumulate until time is greater than target time between frames
                if (deltaTicks >= TargetElapsedTime.Ticks)
                {
                    // enough time has passed to draw a single frame draw into cache
                    GraphicsDevice.SetRenderTarget(_cachedRenderTarget);
                    Render(new GameTime(renderingEventArgs.RenderingTime, TimeSpan.FromTicks(deltaTicks)));
                    GraphicsDevice.Flush();
                    _lastRenderingTime = renderingEventArgs.RenderingTime;
                }
            }
            else if (_resetBackBuffer)
            {
                // always force render when backbuffer is reset (happens during resize due to size change) if we don't always render it will remain black until next frame is drawn draw into cache
                GraphicsDevice.SetRenderTarget(_cachedRenderTarget);
                Render(new GameTime(renderingEventArgs.RenderingTime, TimeSpan.Zero));
                GraphicsDevice.Flush();
            }

            _d3D11Image.Lock();
            // poor man's swap chain implementation now draw from cache to backbuffer
            GraphicsDevice.SetRenderTarget(_sharedRenderTarget);
            _spriteBatch.Begin();
            _spriteBatch.Draw(_cachedRenderTarget, GraphicsDevice.Viewport.Bounds, Color.White);
            _spriteBatch.End();
            GraphicsDevice.Flush();

            _d3D11Image.Unlock();
            GraphicsDevice.SetRenderTarget(_cachedRenderTarget);
            _d3D11Image.Invalidate(); // Always invalidate D3DImage to reduce flickering during window resizing.

            _resetBackBuffer = false;
        }

        void DisposeRenderTargetsFromPreviousFrames()
        {
            for (var i = 0; i < _toBeDisposedNextFrame.Count; i++)
            {
                _toBeDisposedNextFrame[i]?.Dispose();
                _toBeDisposedNextFrame.RemoveAt(i--);
            }
        }

        void StartRendering()
        {
            if (_isRendering)
            {
                return;
            }

            CompositionTarget.Rendering += OnRendering;
            _isRendering = true;
        }

        void StopRendering()
        {
            if (!_isRendering)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            _isRendering = false;
        }

        void UnitializeImageSource()
        {
            Source = null;
            if (_d3D11Image != null)
            {
                _d3D11Image.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
                _d3D11Image.Dispose();
                _d3D11Image = null;
            }
            if (_sharedRenderTarget != null)
            {
                _sharedRenderTarget.Dispose();
                _sharedRenderTarget = null;
            }
            if (_cachedRenderTarget != null)
            {
                if (_cachedRenderTarget.MultiSampleCount > 0 && UseASingleSharedGraphicsDevice)
                {
                    // TODO: this is a memoryleak, the code is intentional because Dispose actually crashes Monogame when using a shared graphicsdevice and disposing MSAA enabled rendertargets
                    // at the very latest this will happen on window close, for SPA this is fine as the process will shut down
                    // but if your editor has multiple windows (or tabs) that can be created/closed multiple times this will slowly increase memory usage..
                }
                else
                {
                    _cachedRenderTarget.Dispose();
                }

                _cachedRenderTarget = null;
            }
        }
    }
}
