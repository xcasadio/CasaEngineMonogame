//-----------------------------------------------------------------------------
// ScreenManagerComponent.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Diagnostics;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class ScreenManagerComponent : DrawableGameComponent
{
    private readonly List<Screen> _screens = new();
    private readonly List<Screen> _screensToUpdate = new();

    private readonly InputState _input;

    //SpriteBatch spriteBatch;
    //SpriteFont font;
    private Texture2D _blankTexture;

    private bool _isInitialized;

    private bool _traceEnabled;

    private Renderer2dComponent _renderer2dComponent;

    public bool CanSetVisible => true;

    public bool CanSetEnable => true;

    public bool TraceEnabled
    {
        get => _traceEnabled;
        set => _traceEnabled = value;
    }

    public ScreenManagerComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        _input = new InputState(game);

        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.ScreenManagerComponent;
        DrawOrder = (int)ComponentDrawOrder.ScreenManagerComponent;
    }

    public override void Initialize()
    {
        _renderer2dComponent = Game.GetGameComponent<Renderer2dComponent>();

        if (_renderer2dComponent == null)
        {
            throw new NullReferenceException("Renderer2dComponent is null");
        }

        _isInitialized = true;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // Load content belonging to the screen manager.
        var content = Game.Content;

        //#if !EDITOR
        _blankTexture = new Texture2D(GraphicsDevice, 1, 1);
        var whitePixels = new Color[] { new(0, 0, 0, 0) };
        _blankTexture.SetData(whitePixels);
        //#endif

        // Tell each of the screens to load their content.
        foreach (var screen in _screens)
        {
            screen.LoadContent(Game);
        }
    }

    protected override void UnloadContent()
    {
        // Tell each of the screens to unload their content.
        foreach (var screen in _screens)
        {
            screen.UnloadContent();
        }

        if (_blankTexture != null)
        {
            _blankTexture.Dispose();
            _blankTexture = null;
        }
    }

    public override void Update(GameTime gameTime)
    {
        var elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

        // Make a copy of the master screen list, to avoid confusion if
        // the process of updating one screen adds or removes others.
        _screensToUpdate.Clear();

        foreach (var screen in _screens)
        {
            _screensToUpdate.Add(screen);
        }

        var otherScreenHasFocus = !Game.IsActive;
        var coveredByOtherScreen = false;

        // Loop as long as there are screens waiting to be updated.
        while (_screensToUpdate.Count > 0)
        {
            // Pop the topmost screen off the waiting list.
            var screen = _screensToUpdate[_screensToUpdate.Count - 1];

            _screensToUpdate.RemoveAt(_screensToUpdate.Count - 1);

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
                    screen.HandleInput(_input);

                    otherScreenHasFocus = true;
                }

                // If this is an active non-popup, inform any subsequent
                // screens that they are covered by it.
                if (!screen.IsPopup)
                {
                    coveredByOtherScreen = true;
                }
            }
        }

        // Print debug trace?
        if (_traceEnabled)
        {
            TraceScreens();
        }
    }

    private void TraceScreens()
    {
        var screenNames = new List<string>();

        foreach (var screen in _screens)
        {
            screenNames.Add(screen.GetType().Name);
        }

        Trace.WriteLine(string.Join(", ", screenNames.ToArray()));
    }

    public override void Draw(GameTime gameTime)
    {
        var elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

        foreach (var screen in _screens)
        {
            if (screen.ScreenState == ScreenState.Hidden)
            {
                continue;
            }

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
        if (_isInitialized)
        {
            screen.LoadContent(Game);
        }

        //GameHelper.GetGameComponent<Gameplay.Gameplay>(GameInfo.Instance.Game).OnScreenInitialized(screen);
        _screens.Add(screen);
    }

    public void RemoveScreen(Screen screen)
    {
        // If we have a graphics device, tell the screen to unload content.
        if (_isInitialized)
        {
            screen.UnloadContent();
        }

        _screens.Remove(screen);
        _screensToUpdate.Remove(screen);
    }

#if EDITOR
    public void ClearScreen()
    {
        foreach (var screen in _screens)
        {
            screen.UnloadContent();
        }

        _screens.Clear();
        _screensToUpdate.Clear();
    }
#endif

    public Screen[] GetScreens()
    {
        return _screens.ToArray();
    }

    public void FadeBackBufferToBlack(int alpha)
    {
        var viewport = GraphicsDevice.Viewport;

        _renderer2dComponent.DrawRectangle(0, 0, viewport.Width, viewport.Height, new Color(0, 0, 0, alpha), 0.99f);
    }

}