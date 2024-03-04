using CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Config
{
    //using Input.SystemSettings;

    public class NoesisConfig
    {
        private bool isInputSet;

        /// <param name="gameWindow">The MonoGame GameWindow instance.</param>
        /// <param name="graphics">Graphics device manager of the game instance.</param>
        /// <param name="noesisProviderManager">NoesisGUI Provider Manager (create it before calling this method).</param>
        /// <param name="rootXamlFilePath">Local XAML file path - will be used as the UI root element.</param>
        /// <param name="themeXamlFilePath">
        /// (can be null) Local XAML file path - will be used as global ResourceDictionary (UI
        /// style).
        /// </param>
        /// <param name="currentTotalGameTime">Current game time (needed to do proper Update() calls).</param>
        /// <param name="onErrorMessageReceived">Callback to invoke when error message received from NoesisGUI.</param>
        /// <param name="onUnhandledException">
        /// Callback to invoke when an unhandled exception thrown from NoesisGUI context (can be in event handler, etc).
        /// </param>
        /// <param name="isEnableDirectionalNavigation">
        /// Is directional (arrow) keys navigation should be enabled? If it's disabled (by default)
        /// arrow key presses will be not passed to NoesisGUI unless it's focused on a textbox.
        /// </param>
        /// <param name="isProcessMouseMiddleButton">
        /// Enable processing of the middle (scrollwheel) mouse button (disabled by
        /// default).
        /// </param>
        public NoesisConfig(
            GameWindow gameWindow,
            GraphicsDevice graphicsDevice,
            NoesisProviderManager noesisProviderManager,
            string rootXamlFilePath,
            string themeXamlFilePath,
            TimeSpan currentTotalGameTime,
            Func<Viewport> callbackGetViewport,
            Action<string> onErrorMessageReceived = null,
            Action<string> onDevLogMessageReceived = null,
            Action<Exception> onUnhandledException = null,
            bool isEnableDirectionalNavigation = false,
            bool isProcessMouseMiddleButton = false)
        {
            if (string.IsNullOrEmpty(rootXamlFilePath))
            {
                throw new ArgumentNullException(
                    nameof(rootXamlFilePath),
                    "File path to the root xaml element cannot be null");
            }

            GameWindow = gameWindow ?? throw new ArgumentNullException(nameof(gameWindow));
            GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

            RootXamlFilePath = rootXamlFilePath.Replace('/', '\\');
            ThemeXamlFilePath = themeXamlFilePath?.Replace('/', '\\');

            OnErrorMessageReceived = onErrorMessageReceived;
            OnDevLogMessageReceived = onDevLogMessageReceived;
            OnUnhandledException = onUnhandledException;
            CurrentTotalGameTime = currentTotalGameTime;
            CallbackGetViewport = callbackGetViewport;
            IsProcessMouseMiddleButton = isProcessMouseMiddleButton;
            NoesisProviderManager = noesisProviderManager;
            IsEnableDirectionalNavigation = isEnableDirectionalNavigation;
        }

        public Func<Viewport> CallbackGetViewport { get; }

        public bool IsEnableDirectionalNavigation { get; }

        internal TimeSpan CurrentTotalGameTime { get; }

        internal GameWindow GameWindow { get; }

        internal GraphicsDevice GraphicsDevice { get; }

        internal double InputKeyRepeatDelaySeconds { get; private set; }

        internal double InputKeyRepeatIntervalSeconds { get; private set; }

        internal double InputMouseDoubleClickIntervalSeconds { get; private set; }

        internal bool IsProcessMouseMiddleButton { get; }

        internal NoesisProviderManager NoesisProviderManager { get; }

        internal Action<string> OnDevLogMessageReceived { get; }

        internal Action<string> OnErrorMessageReceived { get; }

        internal Action<Exception> OnUnhandledException { get; }

        internal string RootXamlFilePath { get; }

        internal string ThemeXamlFilePath { get; }
        /*
        public void SetupInput(
            double keyRepeatDelaySeconds = 0.5,
            double keyRepeatIntervalSeconds = 0.033333333333333333,
            double mouseDoubleClickIntervalSeconds = 0.5)
        {
            InputKeyRepeatDelaySeconds = keyRepeatDelaySeconds;
            InputKeyRepeatIntervalSeconds = keyRepeatIntervalSeconds;
            InputMouseDoubleClickIntervalSeconds = mouseDoubleClickIntervalSeconds;
            isInputSet = true;
        }

        public void SetupInputFromWindows()
        {
            InputSettingsHelper.GetPlatform()
                               .GetSystemInputSettings(
                                   out var keyRepeatDelaySeconds,
                                   out var keyRepeatIntervalSeconds,
                                   out var mouseDoubleClickIntervalSeconds);

            SetupInput(
                keyRepeatDelaySeconds,
                keyRepeatIntervalSeconds,
                mouseDoubleClickIntervalSeconds);
        }
        */
        internal void Validate()
        {
            if (NoesisProviderManager == null)
            {
                throw new Exception(
                    "No NoesisGUI Provider specified. Please call according method of the "
                    + nameof(NoesisConfig));
            }

            //if (!isInputSet)
            //{
            //    throw new Exception(
            //        "The input is not setup. Please call according method of the " + nameof(NoesisConfig));
            //}
        }
    }
}