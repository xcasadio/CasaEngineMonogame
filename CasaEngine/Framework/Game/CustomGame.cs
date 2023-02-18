namespace CasaEngine.Framework.Game
{
    /*public class CustomGame : IDisposable
    {
        private static bool _isAssetContentManagerInitialized;

        private IGraphicsDeviceManager? _graphicsDeviceManager;
        private IGraphicsDeviceService? _graphicsDeviceService;
        private readonly GameServiceContainer _gameServices;

        private bool _firstUpdateDone;
        private bool _firstDrawDone;
        private bool _drawingSlow;
        //private bool inRun;
        private bool _isInitialized;

        private volatile bool _shouldExit;

        private readonly WindowsGameTimer _gameTimer;
        private TimeSpan _gameTimeAccu;
        private GameTime _gameTime;
        private TimeSpan _totalGameTime;
        private long _updatesSinceRunningSlowly1;
        private long _updatesSinceRunningSlowly2;
        private bool _suppressDraw;

        //private GameTime gameUpdateTime;

        private ContentManager _content;

        private readonly List<IGameComponent> _drawableGameComponents;

        private bool IsResized { get; set; }
        private DateTime _needResizeLastTime = DateTime.MinValue;

        public event EventHandler<EventArgs> Activated;
        public event EventHandler<EventArgs> Deactivated;
        public event EventHandler<EventArgs> Disposed;
        public event EventHandler<EventArgs> Exiting;

        private int NewHeight { get; set; }
        private int NewWidth { get; set; }

        public IntPtr GameWindowHandle
        {
            get;
            private set;
        }

        public CustomGame(IntPtr handle)
        {
            ControlHandle = handle;

            _gameServices = new GameServiceContainer();

            FrameworkDispatcher.Update();

            _content = new CustomContentManager(_gameServices);

            _gameTimer = new WindowsGameTimer();
            IsFixedTimeStep = true;
            //this.gameUpdateTime = new GameTime();
            InactiveSleepTime = TimeSpan.FromMilliseconds(20.0);
            TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60L);  // default is 1/60s 

            //TODO: implement draw- and update-order handling of GameComponents
            Components = new GameComponentCollection();
            Components.ComponentAdded += ComponentAdded;
            Components.ComponentRemoved += ComponentRemoved;
            _drawableGameComponents = new List<IGameComponent>();

            IsActive = true;

            if (_isAssetContentManagerInitialized == false)
            {
                CasaEngine.Game.Engine.Instance.AssetContentManager = new AssetContentManager();
                CasaEngine.Game.Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
                CasaEngine.Game.Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
                _isAssetContentManagerInitialized = true;
            }
        }

        public void Resize(int width, int height)
        {
            NewWidth = width;
            NewHeight = height;
            IsResized = true;
        }

        ~CustomGame()
        {
            Components.ComponentAdded -= ComponentAdded;
            Components.ComponentRemoved -= ComponentRemoved;

            Dispose(false);
        }

        protected virtual void Initialize()
        {
            if (!_isInitialized)
            {
                foreach (GameComponent component in Components)
                {
                    component.Initialize();
                }

                _isInitialized = true;

                LoadContent();
            }
        }

        protected virtual void Update(GameTime gameTime)
        {
            foreach (IUpdateable updateable in Components)
            {
                if (updateable.Enabled)
                {
                    updateable.Update(gameTime);
                }
            }

            FrameworkDispatcher.Update();
        }

        protected virtual void Draw(GameTime gameTime)
        {
            foreach (IDrawable drawable in _drawableGameComponents)
            {
                if (drawable.Visible)
                {
                    drawable.Draw(gameTime);
                }
            }
        }

        protected virtual void LoadContent()
        {
        }

        protected virtual void UnloadContent()
        {
        }

        protected virtual void BeginRun()
        {

        }

        protected virtual void EndRun()
        {

        }

        public void RunOneFrame()
        {
            throw new NotImplementedException();
        }

        public void SuppressDraw()
        {
            _suppressDraw = true;
        }

        public void ResetElapsedTime()
        {
            throw new NotImplementedException();
        }

        protected virtual bool BeginDraw()
        {
            if (_graphicsDeviceManager != null && !_graphicsDeviceManager.BeginDraw())
            {
                return false;
            }
            //Logger.BeginLogEvent(LoggingEvent.Draw, "");
            return true;
        }

        protected virtual void EndDraw()
        {
            _graphicsDeviceManager?.EndDraw();
        }

        public void Exit()
        {
            _shouldExit = true;
        }

        public void Run()
        {
            try
            {
                _graphicsDeviceManager = Services.GetService(typeof(IGraphicsDeviceManager)) as IGraphicsDeviceManager;
                _graphicsDeviceManager?.CreateDevice();

                Initialize();
                LoadContent();

                //this.inRun = true;
                BeginRun();

                _gameTime = new GameTime(_totalGameTime, TimeSpan.Zero, false);

                Update(_gameTime);
                _firstUpdateDone = true;

                while (_shouldExit == false)
                {
                    if (IsResized)
                    {
                        var dateTimeNow = DateTime.Now;
                        var span = dateTimeNow.Subtract(_needResizeLastTime);

                        if (span.TotalSeconds >= 1.0)
                        {
                            if (NewWidth != GraphicsDevice.PresentationParameters.BackBufferWidth
                                || NewHeight != GraphicsDevice.PresentationParameters.BackBufferHeight)
                            {
                                _needResizeLastTime = dateTimeNow;
                                var pp = GraphicsDevice.PresentationParameters;
                                pp.BackBufferWidth = Control.Width;
                                pp.BackBufferHeight = Control.Height;
                                GraphicsDevice.Reset(pp);
                                //System.Diagnostics.Debug.WriteLine(Environment.TickCount.ToString() + " - GraphicsDevice.Reset()");
                                LoadContent();
                            }
                            IsResized = false;
                        }
                    }

                    Tick();
                }

                EndRun();

                Dispose(true);
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Tick()
        {
            if (_shouldExit)
            {
                return;
            }

            // Throttle speed when the game is not active
            if (!IsActive)
            {
                Thread.Sleep(InactiveSleepTime);
            }

            _gameTimer.Update();

            var skipDraw = IsFixedTimeStep ? DoFixedTimeStep(_gameTimer.ElapsedTime) : DoTimeStep(_gameTimer.ElapsedTime);
            _suppressDraw = false;
            if (skipDraw == false)
            {
                DrawFrame();
            }
        }

        private bool DoFixedTimeStep(TimeSpan time)
        {
            var skipDraw = false;

            if (Math.Abs(time.Ticks - TargetElapsedTime.Ticks) < TargetElapsedTime.Ticks >> 6)
            {
                time = TargetElapsedTime;
            }

            _gameTimeAccu += time;
            var updateCount = _gameTimeAccu.Ticks / TargetElapsedTime.Ticks;

            if (updateCount <= 0)
            {
                return false;
            }

            if (updateCount > 1)
            {
                _updatesSinceRunningSlowly2 = _updatesSinceRunningSlowly1;
                _updatesSinceRunningSlowly1 = 0;
            }
            else
            {
                _updatesSinceRunningSlowly1++;
                _updatesSinceRunningSlowly2++;
            }

            _drawingSlow = (_updatesSinceRunningSlowly2 < 20);

            while (updateCount > 0)
            {
                if (_shouldExit)
                {
                    break;
                }

                updateCount -= 1L;

                _gameTime = new GameTime(_totalGameTime, TargetElapsedTime, _drawingSlow);
                Update(_gameTime);
                skipDraw &= _suppressDraw;
                _suppressDraw = false;

                _gameTimeAccu -= TargetElapsedTime;
                _totalGameTime += TargetElapsedTime;
            }

            return skipDraw;
        }

        private bool DoTimeStep(TimeSpan time)
        {
            _gameTime = new GameTime(_totalGameTime, TargetElapsedTime, false);
            Update(_gameTime);
            _totalGameTime += time;
            return _suppressDraw;
        }

        private void RunGame()
        {
            _graphicsDeviceManager = Services.GetService(typeof(IGraphicsDeviceManager)) as IGraphicsDeviceManager;
            _graphicsDeviceManager?.CreateDevice();

            Initialize();
            //this.inRun = true;
            BeginRun();

            _gameTime = new GameTime(_totalGameTime, TimeSpan.Zero, false);

            Update(_gameTime);
            _firstUpdateDone = true;
            EndRun();
        }

        private void DrawFrame()
        {
            if (!_shouldExit)
            {
                if (_firstUpdateDone)
                {
                    if (IsVisible(Control))
                    {
                        if (BeginDraw())
                        {
                            Draw(_gameTime);
                            EndDraw();

                            if (!_firstDrawDone)
                            {
                                _firstDrawDone = true;
                            }
                        }
                    }
                }
            }
        }

        public static bool IsVisible(Control ctl)
        {
            // Returns true if the control would be visible if container is visible
            var mi = ctl.GetType().GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi == null)
            {
                return ctl.Visible;
            }

            return (bool)(mi.Invoke(ctl, new object[] { 2 }));
        }

        public GameServiceContainer Services => _gameServices;

        public ContentManager Content
        {
            get => _content;
            set => _content = value;
        }

        public GraphicsDevice GraphicsDevice
        {
            get
            {
                //TODO: GraphicsDevice property is heavily used. Maybe it is better to hook an event to the services container and
                //      cache the reference to the GraphicsDeviceService to prevent accessing the dictionary of the services container

                var graphicsDeviceService = _graphicsDeviceService;
                if (graphicsDeviceService == null)
                {
                    _graphicsDeviceService = graphicsDeviceService = Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;

                    //TODO: exception if null
                }

                return graphicsDeviceService.GraphicsDevice;
            }
        }

        public Control Control
        {
            get;
            private set;
        }

        public IntPtr ControlHandle // to avoid inter thread call
        {
            get;
            private set;
        }

        public bool IsFixedTimeStep
        {
            get;
            set;
        }

        public TimeSpan TargetElapsedTime
        {
            get;
            set;
        }

        public TimeSpan InactiveSleepTime
        {
            get;
            set;
        }

        public bool IsActive
        {
            get;
            internal set;
        }

        public bool IsMouseVisible
        {
            get;
            set;
        }

        public GameComponentCollection Components
        {
            get;
            private set;
        }

        internal bool IsActiveIgnoringGuide => throw new NotImplementedException();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                IDisposable disposable;
                var array = new IGameComponent[Components.Count];
                Components.CopyTo(array, 0);
                for (var i = 0; i < array.Length; i++)
                {
                    disposable = (IDisposable)array[i];
                    disposable?.Dispose();
                }

                disposable = (IDisposable)_graphicsDeviceManager;
                disposable?.Dispose();

                Disposed?.Invoke(this, EventArgs.Empty);
            }
        }

        protected virtual bool ShowMissingRequirementMessage(Exception exception)
        {
            throw new NotImplementedException();
        }

        protected virtual void OnActivated(object sender, EventArgs args)
        {
            RaiseIfNotNull(Activated, sender, args);
        }

        protected virtual void OnDeactivated(object sender, EventArgs args)
        {
            RaiseIfNotNull(Deactivated, sender, args);
        }

        protected virtual void OnExiting(object sender, EventArgs args)
        {
            RaiseIfNotNull(Exiting, sender, args);
        }

        private void RaiseIfNotNull(EventHandler<EventArgs> eventDelegate, object sender, EventArgs args)
        {
            eventDelegate?.Invoke(sender, args);
        }

        private void ComponentRemoved(object? sender, GameComponentCollectionEventArgs e)
        {
            if (e.GameComponent is IDrawable)
            {
                _drawableGameComponents.Remove(e.GameComponent);
            }
        }

        private void ComponentAdded(object? sender, GameComponentCollectionEventArgs e)
        {
            if (e.GameComponent is IDrawable)
            {
                _drawableGameComponents.Add(e.GameComponent);
            }

            if (_isInitialized)
            {
                e.GameComponent.Initialize();
            }
        }

    }*/
}
