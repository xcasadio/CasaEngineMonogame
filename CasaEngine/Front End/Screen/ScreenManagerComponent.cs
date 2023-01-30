//-----------------------------------------------------------------------------
// ScreenManagerComponent.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Helper;


namespace CasaEngine.FrontEnd.Screen
{
    public class ScreenManagerComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
    {
        readonly List<Screen> screens = new List<Screen>();
        readonly List<Screen> screensToUpdate = new List<Screen>();

        readonly InputState input = new InputState();

        //SpriteBatch spriteBatch;
        //SpriteFont font;
        Texture2D blankTexture;

        bool isInitialized;

        bool traceEnabled;

        Renderer2DComponent m_Renderer2DComponent = null;



        public bool CanSetVisible => true;

        public bool CanSetEnable => true;

        public bool TraceEnabled
        {
            get => traceEnabled;
            set => traceEnabled = value;
        }



        public ScreenManagerComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            if (game == null)
            {
                throw new ArgumentException("ScreenManagerComponent : Game is null");
            }

            game.Components.Add(this);

            UpdateOrder = (int)ComponentUpdateOrder.ScreenManagerComponent;
            DrawOrder = (int)ComponentDrawOrder.ScreenManagerComponent;
        }



        public override void Initialize()
        {
            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);

            if (m_Renderer2DComponent == null)
            {
                throw new NullReferenceException("Renderer2DComponent is null");
            }

            isInitialized = true;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Load content belonging to the screen manager.
            ContentManager content = Game.Content;

            //#if !EDITOR
            blankTexture = new Texture2D(GraphicsDevice, 1, 1);
            Color[] whitePixels = new Color[] { new Color(0, 0, 0, 0) };
            blankTexture.SetData<Color>(whitePixels);
            //#endif

            // Tell each of the screens to load their content.
            foreach (Screen screen in screens)
            {
                screen.LoadContent(m_Renderer2DComponent);
            }
        }

        protected override void UnloadContent()
        {
            // Tell each of the screens to unload their content.
            foreach (Screen screen in screens)
            {
                screen.UnloadContent();
            }

            if (blankTexture != null)
            {
                blankTexture.Dispose();
                blankTexture = null;
            }
        }



        public override void Update(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            // Make a copy of the master screen list, to avoid confusion if
            // the process of updating one screen adds or removes others.
            screensToUpdate.Clear();

            foreach (Screen screen in screens)
                screensToUpdate.Add(screen);

            bool otherScreenHasFocus = !Game.IsActive;
            bool coveredByOtherScreen = false;

            // Loop as long as there are screens waiting to be updated.
            while (screensToUpdate.Count > 0)
            {
                // Pop the topmost screen off the waiting list.
                Screen screen = screensToUpdate[screensToUpdate.Count - 1];

                screensToUpdate.RemoveAt(screensToUpdate.Count - 1);

                // Update the screen.
                //GameHelper.GetGameComponent<Gameplay.Gameplay>(GameInfo.Instance.Game).OnScreenUpdate(screen, elpasedTime, otherScreenHasFocus, coveredByOtherScreen);
                screen.Update(elpasedTime, otherScreenHasFocus, coveredByOtherScreen);

                if (screen.ScreenState == ScreenState.TransitionOn ||
                    screen.ScreenState == ScreenState.Active)
                {
                    // If this is the first active screen we came across,
                    // give it a chance to handle input.
                    if (!otherScreenHasFocus)
                    {
                        screen.HandleInput(input);

                        otherScreenHasFocus = true;
                    }

                    // If this is an active non-popup, inform any subsequent
                    // screens that they are covered by it.
                    if (!screen.IsPopup)
                        coveredByOtherScreen = true;
                }
            }

            // Print debug trace?
            if (traceEnabled)
                TraceScreens();
        }

        void TraceScreens()
        {
            List<string> screenNames = new List<string>();

            foreach (Screen screen in screens)
                screenNames.Add(screen.GetType().Name);

            Trace.WriteLine(string.Join(", ", screenNames.ToArray()));
        }

        public override void Draw(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            foreach (Screen screen in screens)
            {
                if (screen.ScreenState == ScreenState.Hidden)
                    continue;

                //GameHelper.GetGameComponent<Gameplay.Gameplay>(GameInfo.Instance.Game).OnScreenDraw(screen);
                screen.Draw(elpasedTime);
            }
        }



#if EDITOR
        /*public void LoadStaticAsset(AssetManager assetManager_)
		{
			if (blankTexture != null)
			{
				return;
				//if (blankTexture.IsDisposed == false)
				//{
				//	throw new InvalidOperationException("ScreenManagerComponent.LoadStaticAsset() : Please unload content before call this function");
				//}
			}

			//blankTexture = assetManager_.LoadImportAsset<Texture2D>("Content/Texture/blank");
		}*/
#endif

        public void AddScreen(Screen screen, PlayerIndex? controllingPlayer)
        {
            screen.ControllingPlayer = controllingPlayer;
            screen.ScreenManagerComponent = this;
            screen.IsExiting = false;

            // If we have a graphics device, tell the screen to load content.
            if (isInitialized)
            {
                screen.LoadContent(m_Renderer2DComponent);
            }

            //GameHelper.GetGameComponent<Gameplay.Gameplay>(GameInfo.Instance.Game).OnScreenInitialized(screen);
            screens.Add(screen);
        }

        public void RemoveScreen(Screen screen)
        {
            // If we have a graphics device, tell the screen to unload content.
            if (isInitialized)
            {
                screen.UnloadContent();
            }

            screens.Remove(screen);
            screensToUpdate.Remove(screen);
        }

#if EDITOR
        public void ClearScreen()
        {
            foreach (Screen screen in screens)
            {
                screen.UnloadContent();
            }

            screens.Clear();
            screensToUpdate.Clear();
        }
#endif

        public Screen[] GetScreens()
        {
            return screens.ToArray();
        }

        public void FadeBackBufferToBlack(int alpha)
        {
            Viewport viewport = this.GraphicsDevice.Viewport;

            m_Renderer2DComponent.AddSprite2D(blankTexture,
                                new Rectangle(0, 0, viewport.Width, viewport.Height),
                                Point.Zero, Vector2.Zero, 0.0f, Vector2.One,
                                new Color(0, 0, 0, alpha), 0.99f, SpriteEffects.None);
        }

    }
}
