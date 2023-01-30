//-----------------------------------------------------------------------------
// ScreenManagerComponent.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System;


using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.Math;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Helper;


namespace CasaEngine.FrontEnd.Screen
{
    /// <summary>
    /// The screen manager is a component which manages one or more Screen
    /// instances. It maintains a stack of screens, calls their Update and Draw
    /// methods at the appropriate times, and automatically routes input to the
    /// topmost active screen.
    /// </summary>
    public class ScreenManagerComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
    {

        List<Screen> screens = new List<Screen>();
        List<Screen> screensToUpdate = new List<Screen>();

        InputState input = new InputState();

        //SpriteBatch spriteBatch;
        //SpriteFont font;
        Texture2D blankTexture;

        bool isInitialized;

        bool traceEnabled;

        Renderer2DComponent m_Renderer2DComponent = null;



        /// <summary>
        /// Gets
        /// </summary>
        public bool CanSetVisible
        {
            get { return true; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool CanSetEnable
        {
            get { return true; }
        }

        /// <summary>
        /// If true, the manager prints out a list of all the screens
        /// each time it is updated. This can be useful for making sure
        /// everything is being added and removed at the right times.
        /// </summary>
        public bool TraceEnabled
        {
            get { return traceEnabled; }
            set { traceEnabled = value; }
        }



        /// <summary>
        /// Constructs a new screen manager component.
        /// </summary>
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



        /// <summary>
        /// Initializes the screen manager component.
        /// </summary>
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

        /// <summary>
        /// Load your graphics content.
        /// </summary>
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

        /// <summary>
        /// Unload your graphics content.
        /// </summary>
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



        /// <summary>
        /// Allows each screen to run logic.
        /// </summary>
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

        /// <summary>
        /// Prints a list of all the screens, for debugging.
        /// </summary>
        void TraceScreens()
        {
            List<string> screenNames = new List<string>();

            foreach (Screen screen in screens)
                screenNames.Add(screen.GetType().Name);

            Trace.WriteLine(string.Join(", ", screenNames.ToArray()));
        }

        /// <summary>
        /// Tells each screen to draw itself.
        /// </summary>
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
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Adds a new screen to the screen manager.
        /// </summary>
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

        /// <summary>
        /// Removes a screen from the screen manager. You should normally
        /// use Screen.ExitScreen instead of calling this directly, so
        /// the screen can gradually transition off rather than just being
        /// instantly removed.
        /// </summary>
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
        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// Expose an array holding all the screens. We return a copy rather
        /// than the real master list, because screens should only ever be added
        /// or removed using the AddScreen and RemoveScreen methods.
        /// </summary>
		public Screen[] GetScreens()
        {
            return screens.ToArray();
        }

        /// <summary>
        /// Helper draws a translucent black fullscreen sprite, used for fading
        /// screens in and out, and for darkening the background behind popups.
        /// </summary>
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
