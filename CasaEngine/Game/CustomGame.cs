using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using CasaEngineCommon.Logger;
using CasaEngine.Asset;
using CasaEngine.Asset.Fonts;
using CasaEngine.Project;
using System.IO;
using CasaEngineCommon.Helper;

namespace CasaEngine.Game
{
    /// <summary>
    /// 
    /// </summary>
    public class CustomGame
        : IDisposable
    {
        static private bool m_IsAssetContentManagerInitialized = false;


        private IGraphicsDeviceManager graphicsDeviceManager;
        private IGraphicsDeviceService graphicsDeviceService;
        private GameServiceContainer gameServices;

        private bool firstUpdateDone;
        private bool firstDrawDone;
        private bool drawingSlow;
        //private bool inRun;
        private bool isInitialized;

        private volatile bool ShouldExit;

        private WindowsGameTimer gameTimer;
        private TimeSpan gameTimeAccu;
        private GameTime gameTime;
        private TimeSpan totalGameTime;
        private long updatesSinceRunningSlowly1;
        private long updatesSinceRunningSlowly2;
        private bool suppressDraw;

        //private GameTime gameUpdateTime;

        private ContentManager content;

        private List<IGameComponent> drawableGameComponents;

        private volatile bool m_NeedResize = false;
        private DateTime m_NeedResizeLastTime = DateTime.MinValue;


        public event EventHandler<EventArgs> Activated;
        public event EventHandler<EventArgs> Deactivated;
        public event EventHandler<EventArgs> Disposed;
        public event EventHandler<EventArgs> Exiting;



        /// <summary>
        /// 
        /// </summary>
        public Form GameWindow
        {
            get;
            private set;
        }

        public IntPtr GameWindowHandle
        {
            get;
            private set;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public CustomGame(Control control_)
        {
            Control = control_;
            Control.Resize += new EventHandler(OnControlResize);
            ControlHandle = control_.Handle;

            GameWindow = Control.FindForm();
            GameWindowHandle = GameWindow.Handle;

            this.gameServices = new GameServiceContainer();

            FrameworkDispatcher.Update();

            this.content = new CustomContentManager(this.gameServices);

            this.gameTimer = new WindowsGameTimer();
            this.IsFixedTimeStep = true;
            //this.gameUpdateTime = new GameTime();
            this.InactiveSleepTime = TimeSpan.FromMilliseconds(20.0);
            this.TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60L);  // default is 1/60s 

            //TODO: implement draw- and update-order handling of GameComponents
            this.Components = new GameComponentCollection();
            this.Components.ComponentAdded += components_ComponentAdded;
            this.Components.ComponentRemoved += components_ComponentRemoved;
            this.drawableGameComponents = new List<IGameComponent>();

            this.IsActive = true;

            if (m_IsAssetContentManagerInitialized == false)
            {
                Engine.Instance.AssetContentManager = new AssetContentManager();
                Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
                Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(System.Windows.Forms.Cursor), new CursorLoader());
                m_IsAssetContentManagerInitialized = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnControlResize(object sender, EventArgs e)
        {
            m_NeedResize = true;
        }

        /// <summary>
        /// 
        /// </summary>
        ~CustomGame()
        {
            this.Components.ComponentAdded -= components_ComponentAdded;
            this.Components.ComponentRemoved -= components_ComponentRemoved;

            Dispose(false);
        }

        protected virtual void Initialize()
        {
            if (!this.isInitialized)
            {
                foreach (GameComponent component in this.Components)
                {
                    component.Initialize();
                }

                this.isInitialized = true;

                this.LoadContent();
            }
        }

        protected virtual void Update(GameTime gameTime)
        {
            foreach (IUpdateable updateable in this.Components)
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
            foreach (IDrawable drawable in this.drawableGameComponents)
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
            this.suppressDraw = true;
        }

        public void ResetElapsedTime()
        {
            throw new NotImplementedException();
        }

        protected virtual bool BeginDraw()
        {
            if ((this.graphicsDeviceManager != null) && !this.graphicsDeviceManager.BeginDraw())
            {
                return false;
            }
            //Logger.BeginLogEvent(LoggingEvent.Draw, "");
            return true;
        }

        protected virtual void EndDraw()
        {
            if (this.graphicsDeviceManager != null)
            {
                this.graphicsDeviceManager.EndDraw();
            }
        }

        public void Exit()
        {
            this.ShouldExit = true;
        }

        public void Run()
        {
            try
            {
                this.graphicsDeviceManager = this.Services.GetService(typeof(IGraphicsDeviceManager)) as IGraphicsDeviceManager;
                if (this.graphicsDeviceManager != null)
                    this.graphicsDeviceManager.CreateDevice();

                this.Initialize();
                LoadContent();

                //this.inRun = true;
                this.BeginRun();

                gameTime = new GameTime(totalGameTime, TimeSpan.Zero, false);

                this.Update(this.gameTime);
                this.firstUpdateDone = true;

                while (ShouldExit == false)
                {
                    if (m_NeedResize)
                    {
                        DateTime dateTimeNow = DateTime.Now;
                        TimeSpan span = dateTimeNow.Subtract(m_NeedResizeLastTime);

                        if (span.TotalSeconds >= 1.0)
                        {
                            if (Control.Width > 0
                                && Control.Height > 0
                                && (Control.Width != GraphicsDevice.PresentationParameters.BackBufferWidth
                                || Control.Height != GraphicsDevice.PresentationParameters.BackBufferHeight))
                            {
                                m_NeedResizeLastTime = dateTimeNow;
                                PresentationParameters pp = GraphicsDevice.PresentationParameters;
                                pp.BackBufferWidth = Control.Width;
                                pp.BackBufferHeight = Control.Height;
                                GraphicsDevice.Reset(pp);
                                //System.Diagnostics.Debug.WriteLine(Environment.TickCount.ToString() + " - GraphicsDevice.Reset()");
                                LoadContent();
                            }
                            m_NeedResize = false;
                        }
                    }

                    Tick();
                }

                this.EndRun();

                Dispose(true);
            }
            catch (System.Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Tick()
        {
            if (this.ShouldExit)
            {
                return;
            }

            // Throttle speed when the game is not active
            if (!this.IsActive)
            {
                Thread.Sleep(InactiveSleepTime);
            }

            gameTimer.Update();

            bool skipDraw = IsFixedTimeStep ? DoFixedTimeStep(gameTimer.ElapsedTime) : DoTimeStep(gameTimer.ElapsedTime);
            this.suppressDraw = false;
            if (skipDraw == false)
            {
                DrawFrame();
            }
        }

        private bool DoFixedTimeStep(TimeSpan time)
        {
            bool skipDraw = false;

            if (System.Math.Abs(time.Ticks - this.TargetElapsedTime.Ticks) < this.TargetElapsedTime.Ticks >> 6)
            {
                time = this.TargetElapsedTime;
            }

            this.gameTimeAccu += time;
            long updateCount = this.gameTimeAccu.Ticks / this.TargetElapsedTime.Ticks;

            if (updateCount <= 0)
            {
                return false;
            }

            if (updateCount > 1)
            {
                this.updatesSinceRunningSlowly2 = this.updatesSinceRunningSlowly1;
                this.updatesSinceRunningSlowly1 = 0;
            }
            else
            {
                this.updatesSinceRunningSlowly1++;
                this.updatesSinceRunningSlowly2++;
            }

            this.drawingSlow = (this.updatesSinceRunningSlowly2 < 20);

            while (updateCount > 0)
            {
                if (this.ShouldExit)
                {
                    break;
                }

                updateCount -= 1L;

                this.gameTime = new GameTime(totalGameTime, TargetElapsedTime, drawingSlow);
                this.Update(this.gameTime);
                skipDraw &= this.suppressDraw;
                this.suppressDraw = false;

                this.gameTimeAccu -= this.TargetElapsedTime;
                this.totalGameTime += this.TargetElapsedTime;
            }

            return skipDraw;
        }

        private bool DoTimeStep(TimeSpan time)
        {
            this.gameTime = new GameTime(totalGameTime, TargetElapsedTime, false);
            this.Update(this.gameTime);
            this.totalGameTime += time;
            return suppressDraw;
        }

        private void RunGame()
        {
            this.graphicsDeviceManager = this.Services.GetService(typeof(IGraphicsDeviceManager)) as IGraphicsDeviceManager;
            if (this.graphicsDeviceManager != null)
                this.graphicsDeviceManager.CreateDevice();

            this.Initialize();
            //this.inRun = true;
            this.BeginRun();

            gameTime = new GameTime(totalGameTime, TimeSpan.Zero, false);

            this.Update(this.gameTime);
            this.firstUpdateDone = true;
            this.EndRun();
        }

        private void DrawFrame()
        {
            if (!this.ShouldExit)
            {
                if (this.firstUpdateDone)
                {
                    if (IsVisible(this.Control) == true)
                    {
                        if (this.BeginDraw())
                        {
                            this.Draw(this.gameTime);
                            this.EndDraw();

                            if (!this.firstDrawDone)
                            {
                                this.firstDrawDone = true;
                            }
                        }
                    }
                }
            }
        }

        public static bool IsVisible(Control ctl)
        {
            // Returns true if the control would be visible if container is visible
            MethodInfo mi = ctl.GetType().GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi == null) return ctl.Visible;
            return (bool)(mi.Invoke(ctl, new object[] { 2 }));
        }

        public GameServiceContainer Services
        {
            get
            {
                return this.gameServices;
            }
        }

        public ContentManager Content
        {
            get
            {
                return this.content;
            }
            set
            {
                this.content = value;
            }
        }

        public GraphicsDevice GraphicsDevice
        {
            get
            {
                //TODO: GraphicsDevice property is heavily used. Maybe it is better to hook an event to the services container and
                //      cache the reference to the GraphicsDeviceService to prevent accessing the dictionary of the services container

                IGraphicsDeviceService graphicsDeviceService = this.graphicsDeviceService;
                if (graphicsDeviceService == null)
                {
                    this.graphicsDeviceService = graphicsDeviceService = this.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;

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

        /// <summary>
        /// Gets
        /// </summary>
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


        internal bool IsActiveIgnoringGuide
        {
            get
            {
                throw new NotImplementedException();
            }
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
                IDisposable disposable;
                var array = new IGameComponent[Components.Count];
                Components.CopyTo(array, 0);
                for (int i = 0; i < array.Length; i++)
                {
                    disposable = (IDisposable)array[i];
                    if (disposable != null)
                        disposable.Dispose();
                }

                disposable = (IDisposable)graphicsDeviceManager;
                if (disposable != null)
                    disposable.Dispose();

                if (Disposed != null)
                    Disposed(this, EventArgs.Empty);
            }
        }

        protected virtual bool ShowMissingRequirementMessage(Exception exception)
        {
            throw new NotImplementedException();
        }

        protected virtual void OnActivated(Object sender, EventArgs args)
        {
            RaiseIfNotNull(this.Activated, sender, args);
        }

        protected virtual void OnDeactivated(Object sender, EventArgs args)
        {
            RaiseIfNotNull(this.Deactivated, sender, args);
        }

        protected virtual void OnExiting(Object sender, EventArgs args)
        {
            RaiseIfNotNull(this.Exiting, sender, args);
        }

        private void RaiseIfNotNull(EventHandler<EventArgs> eventDelegate, Object sender, EventArgs args)
        {
            if (eventDelegate != null)
            {
                eventDelegate(sender, args);
            }
        }

        private void components_ComponentRemoved(object sender, GameComponentCollectionEventArgs e)
        {
            if (e.GameComponent is IDrawable)
            {
                drawableGameComponents.Remove(e.GameComponent);
            }
        }

        private void components_ComponentAdded(object sender, GameComponentCollectionEventArgs e)
        {
            if (e.GameComponent is IDrawable)
            {
                drawableGameComponents.Add(e.GameComponent);
            }

            if (isInitialized)
            {
                e.GameComponent.Initialize();
            }
        }

    }
}
