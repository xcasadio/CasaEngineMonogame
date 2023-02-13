using CasaEngine.Core_Systems.Game;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Front_End.Screen
{
    public class LoadingScreen
        : Screen
    {
        readonly bool _loadingIsSlow;
        bool _otherScreensAreGone;

        readonly Screen[] _screensToLoad;

        readonly Renderer2DComponent _renderer2DComponent = null;



        private LoadingScreen(ScreenManagerComponent screenManager, bool loadingIsSlow,
                              Screen[] screensToLoad)
            : base("LoadingScreen")
        {
            _loadingIsSlow = loadingIsSlow;
            _screensToLoad = screensToLoad;

            TransitionOnTime = TimeSpan.FromSeconds(0.5);

            _renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }

        public static void Load(ScreenManagerComponent screenManager, bool loadingIsSlow,
                                PlayerIndex? controllingPlayer,
                                params Screen[] screensToLoad)
        {
            // Tell all the current screens to transition off.
            foreach (var screen in screenManager.GetScreens())
                screen.ExitScreen();

            // Create and activate the loading screen.
            var loadingScreen = new LoadingScreen(screenManager,
                                                            loadingIsSlow,
                                                            screensToLoad);

            screenManager.AddScreen(loadingScreen, controllingPlayer);
        }



        public override void Update(float elapsedTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

            // If all the previous screens have finished transitioning
            // off, it is time to actually perform the load.
            if (_otherScreensAreGone)
            {
                ScreenManagerComponent.RemoveScreen(this);

                foreach (var screen in _screensToLoad)
                {
                    if (screen != null)
                    {
                        ScreenManagerComponent.AddScreen(screen, ControllingPlayer);
                    }
                }

                // Once the load has finished, we use ResetElapsedTime to tell
                // the  game timing mechanism that we have just finished a very
                // long frame, and that it should not try to catch up.
                Engine.Instance.Game.ResetElapsedTime();
            }
        }

        public override void Draw(float elapsedTime)
        {
            // If we are the only active screen, that means all the previous screens
            // must have finished transitioning off. We check for this in the Draw
            // method, rather than in Update, because it isn't enough just for the
            // screens to be gone: in order for the transition to look good we must
            // have actually drawn a frame without them before we perform the load.
            if ((ScreenState == ScreenState.Active) &&
                (ScreenManagerComponent.GetScreens().Length == 1))
            {
                _otherScreensAreGone = true;
            }

            // The gameplay screen takes a while to load, so we display a loading
            // message while that is going on, but the menus load very quickly, and
            // it would look silly if we flashed this up for just a fraction of a
            // second while returning from the game to the menus. This parameter
            // tells us how long the loading is going to take, so we know whether
            // to bother drawing the message.
            if (_loadingIsSlow)
            {
                const string message = "Loading...";

                // Center the text in the viewport.
                var viewport = Engine.Instance.GraphicsDeviceManager.GraphicsDevice.Viewport;
                var viewportSize = new Vector2(viewport.Width, viewport.Height);
                var textSize = Engine.Instance.DefaultSpriteFont.MeasureString(message);
                var textPosition = (viewportSize - textSize) / 2;

                var color = new Color((byte)255, (byte)255, (byte)255, TransitionAlpha);

                // Draw the text.
                /*spriteBatch.Begin();
                spriteBatch.DrawString(font, message, textPosition, color);
                spriteBatch.End();*/
                _renderer2DComponent.AddText2D(Engine.Instance.DefaultSpriteFont, message, textPosition, 0.0f, Vector2.One, color, 0.99f);
            }
        }

    }
}
