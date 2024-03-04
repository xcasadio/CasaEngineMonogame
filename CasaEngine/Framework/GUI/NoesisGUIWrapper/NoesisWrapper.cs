using System.Diagnostics;
using CasaEngine.Framework.GUI.NoesisGUIWrapper.Config;
using CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Noesis;
using Size = Noesis.Size;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper
{
    //using Input;
    using Control = System.Windows.Forms.Control;
    using EventArgs = System.EventArgs;
    using Rectangle = Microsoft.Xna.Framework.Rectangle;

    /// <summary>
    /// Wrapper usage:
    /// 1. at game LoadContent() create wrapper instance
    /// 2. at game Update() invoke:
    /// - 2.1. wrapper.UpdateInput(gameTime)
    /// - 2.2. your game update (game logic)
    /// - 2.3. wrapper.Update(gameTime)
    /// 3. at game Draw() invoke:
    /// - 3.1. wrapper.PreRender(gameTime)
    /// - 3.2. clear graphics device (including stencil buffer)
    /// - 3.3. your game drawing code
    /// - 3.4. wrapper.Render()
    /// 4. at game UnloadContent() call wrapper.Dispose() method.
    /// Please be sure you have IsMouseVisible=true at the MonoGame Game class instance.
    /// </summary>
    public class NoesisWrapper : IDisposable
    {
        private static bool isGuiInitialized;

        private readonly NoesisConfig config;

        private readonly GameWindow gameWindow;

        private readonly GraphicsDevice graphicsDevice;

        //private InputManager input;

        private Size lastSize;

        private Rectangle lastViewportBounds;

        private NoesisProviderManager providerManager;

        private NoesisViewWrapper view;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoesisWrapper" /> class.
        /// </summary>
        public NoesisWrapper(NoesisConfig config)
        {
            config.Validate();
            this.config = config;
            gameWindow = config.GameWindow;

            // setup Noesis Debug callbacks
            Log.SetLogCallback(NoesisLogCallbackHandler);
            Error.SetUnhandledCallback(NoesisUnhandledExceptionHandler);

            Noesis.GUI.SetSoftwareKeyboardCallback(SoftwareKeyboardCallbackHandler);

            graphicsDevice = config.GraphicsDevice;
            providerManager = config.NoesisProviderManager;
            var provider = providerManager;
            Noesis.GUI.SetFontProvider(provider.FontProvider);
            Noesis.GUI.SetTextureProvider(provider.TextureProvider);
            Noesis.GUI.SetXamlProvider(provider.XamlProvider);

            // setup theme
            if (config.ThemeXamlFilePath is not null)
            {
                // similar to GUI.LoadApplicationResources(config.ThemeXamlFilePath)
                // but retain the ResourceDictionary to expose it as a Theme property
                // (useful to get application resources)
                var themeResourceDictionary = new ResourceDictionary();
                Noesis.GUI.SetApplicationResources(themeResourceDictionary);
                Noesis.GUI.LoadComponent(themeResourceDictionary, config.ThemeXamlFilePath);
                Theme = themeResourceDictionary;
            }

            // create and prepare view
            var controlTreeRoot = (FrameworkElement)Noesis.GUI.LoadXaml(config.RootXamlFilePath);
            ControlTreeRoot = controlTreeRoot
                                   ?? throw new Exception(
                                       $"UI file \"{config.RootXamlFilePath}\" is not found - cannot initialize UI");

            view = new NoesisViewWrapper(
                controlTreeRoot,
                graphicsDevice,
                this.config.CurrentTotalGameTime);
            RefreshSize(forceRefresh: true);

            var form = (Form)Control.FromHandle(gameWindow.Handle);
            //input = view.CreateInputManager(config, form);

            // subscribe to MonoGame events
            EventsSubscribe();
        }

        /// <summary>
        /// Gets root element.
        /// </summary>
        public FrameworkElement ControlTreeRoot { get; private set; }

        /// <summary>
        /// Gets the input manager.
        /// </summary>
        //public InputManager Input => input;

        /// <summary>
        /// Gets resource dictionary of theme.
        /// </summary>
        public ResourceDictionary Theme { get; private set; }

        public NoesisViewWrapper View => view;

        public static void Init(string licenseName, string licenseKey)
        {
            if (isGuiInitialized)
            {
                return;
            }

            // init NoesisGUI (called only once during the game lifetime)
            isGuiInitialized = true;
            Noesis.GUI.Init();
            Noesis.GUI.SetLicense("xav", "ciab2xRrm9t4HQHSUn+aa23Pv2SNkzISdVb6D7BWxwNhM38V");
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void PreRender()
        {
            view.PreRender();
        }

        public void Render()
        {
            view.Render();
        }

        /// <summary>
        /// Updates NoesisGUI.
        /// </summary>
        /// <param name="gameTime">Current game time.</param>
        public void Update(GameTime gameTime)
        {
            RefreshSize();
            view.Update(gameTime);
        }

        /// <summary>
        /// Updates NoesisGUI input.
        /// </summary>
        /// <param name="gameTime">Current game time.</param>
        /// <param name="isWindowActive">Is game focused?</param>
        public void UpdateInput(GameTime gameTime, bool isWindowActive)
        {
            //if (this.lastIsWindowActive != isWindowActive)
            //{
            //    // workaround for the issue when after switching from a GPU-heavy application NoesisGUI renders incorrectly
            //    this.lastIsWindowActive = isWindowActive;
            //    this.RefreshSize(forceRefresh: true);
            //}

            //input.Update(gameTime, isWindowActive);
        }

        private void DestroyRoot()
        {
            if (view == null)
            {
                // already destroyed
                return;
            }

            EventsUnsubscribe();

            view.Shutdown();
            view = null;
            var viewWeakRef = new WeakReference(view);
            view = null;
            //input?.Dispose();
            //input = null;
            ControlTreeRoot = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();

            // ensure the view is GC'ollected
            Debug.Assert(viewWeakRef.Target == null);
        }

        private void DeviceLostHandler(object sender, EventArgs eventArgs)
        {
            // TODO: restore this? not sure where it went in NoesisGUI 2.0
            //Noesis.GUI.DeviceLost();
        }

        private void DeviceResetHandler(object sender, EventArgs e)
        {
            // TODO: restore this? not sure where it went in NoesisGUI 2.0
            //Noesis.GUI.DeviceReset();
            RefreshSize(forceRefresh: true);
        }

        private void EventsSubscribe()
        {
            graphicsDevice.DeviceReset += DeviceResetHandler;
            graphicsDevice.DeviceLost += DeviceLostHandler;
            gameWindow.TextInput += GameWindowTextInputHandler;
        }

        private void EventsUnsubscribe()
        {
            graphicsDevice.DeviceReset -= DeviceResetHandler;
            graphicsDevice.DeviceLost -= DeviceLostHandler;
            gameWindow.TextInput -= GameWindowTextInputHandler;
        }

        private void GameWindowTextInputHandler(object sender, TextInputEventArgs e)
        {
            //input.OnMonoGameChar(e.Character, e.Key);
        }

        private void NoesisLogCallbackHandler(LogLevel level, string channel, string message)
        {
            if (level == LogLevel.Error)
            {
                config.OnErrorMessageReceived?.Invoke(message);
            }
            else if (level >= LogLevel.Warning)
            {
                config.OnDevLogMessageReceived?.Invoke(message);
            }
        }

        private void NoesisUnhandledExceptionHandler(Exception exception)
        {
            config.OnUnhandledException?.Invoke(exception);
        }

        private void RefreshSize(bool forceRefresh = false)
        {
            var viewport = config.CallbackGetViewport();
            if (viewport.Bounds != lastViewportBounds)
            {
                lastViewportBounds = viewport.Bounds;
                forceRefresh = true;
            }

            var size = new Size(viewport.Width, viewport.Height);
            if (!forceRefresh
                && lastSize == size)
            {
                return;
            }

            if (forceRefresh
                && lastSize == size)
            {
                // force refresh size
                view.SetSize(1601, 901);
            }

            lastSize = size;
            view.SetSize((ushort)viewport.Width, (ushort)viewport.Height);
        }

        private void Shutdown()
        {
            DestroyRoot();
            Theme = null;
            providerManager.Dispose();
            providerManager = null;
            Noesis.GUI.UnregisterNativeTypes();
        }

        private void SoftwareKeyboardCallbackHandler(UIElement focused, bool open)
        {
            //input?.SoftwareKeyboardCallbackHandler(focused, open);
        }
    }
}