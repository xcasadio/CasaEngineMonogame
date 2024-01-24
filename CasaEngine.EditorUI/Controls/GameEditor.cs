using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CasaEngine.EditorUI.Inputs;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Controls;

public abstract class GameEditor : WpfGame
{
    private bool _isInitialized;
    public event EventHandler? GameStarted;

    public CasaEngineGame? Game { get; private set; }
    public bool UseGui { get; protected init; }

    protected override bool CanRender => _isInitialized && IsVisible;

    protected GameEditor(bool useGui = false)
    {
        UseGui = useGui;
    }

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        Game = new CasaEngineGame(null, graphicsDeviceService);
        Game.IsRunningInGameEditorMode = true;
        Game.UseGui = UseGui;
        InitializeGame();

        Game.InitializeWithEditor();

        if (UseGui)
        {
            Game.UiManager.DefaultRenderTarget = RenderTargetBackBuffer;
        }

        //In editor mode the game is in idle mode so we don't update physics
        Game.PhysicsEngineComponent.Enabled = false;

        Game.SetInputProvider(
            new KeyboardStateProvider(new WpfKeyboard(this)),
            new MouseStateProvider(new WpfMouse(this)));

        base.Initialize();
    }

    protected abstract void InitializeGame();

    protected override void LoadContent()
    {
        Game.LoadContentWithEditor();
        Game.GameManager.EndLoadContent();

        GameStarted?.Invoke(Game, EventArgs.Empty);
        _isInitialized = true;
    }

    protected override void Update(GameTime gameTime)
    {
        Game.UpdateWithEditor(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        Game.DrawWithEditor(gameTime);
    }

    protected override void CreateGraphicsDeviceDependentResources(PresentationParameters pp)
    {
        base.CreateGraphicsDeviceDependentResources(pp);

        if (UseGui && Game?.UiManager != null)
        {
            Game.UiManager.DefaultRenderTarget = RenderTargetBackBuffer;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        var newSizeWidth = (int)sizeInfo.NewSize.Width;
        var newSizeHeight = (int)sizeInfo.NewSize.Height;

        //in editor the camera is an element of the world
        Game?.GameManager.ActiveCamera?.ScreenResized(newSizeWidth, newSizeHeight);
        Game?.OnScreenResized(newSizeWidth, newSizeHeight);
        base.OnRenderSizeChanged(sizeInfo);
    }
}