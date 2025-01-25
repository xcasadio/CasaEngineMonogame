using System;
using System.Windows;
using CasaEngine.EditorUI.Inputs;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Controls;

public abstract class GameEditor : WpfGame
{
    public event EventHandler? GameStarted;

    public CasaEngineGame? Game { get; private set; }
    public bool UseGui { get; protected init; }
    protected bool IsGameInitialized { get; private set; }

    protected override bool CanRender => IsGameInitialized && IsVisible;

    protected GameEditor(bool useGui = false)
    {
        UseGui = useGui;
    }

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        Game = new CasaEngineGame(null, graphicsDeviceService);
        Game.IsRunningInGameEditorMode = true;
        Game.GameManager.WorldChanged += OnWorldChanged;
        InitializeGame();
        Game.InitializeWithEditor();
        Game.UserInterfaceComponent.Enabled = UseGui;

        if (UseGui)
        {
            Game.UserInterfaceComponent.UINeoForceManager.DefaultRenderTarget = RenderTargetBackBuffer;
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

        GameStarted?.Invoke(Game, EventArgs.Empty);
        IsGameInitialized = true;
    }

    private void OnWorldChanged(object? sender, EventArgs e)
    {
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            OnScreenResized((int)ActualWidth, (int)ActualHeight);
        }
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

        if (UseGui && Game?.UserInterfaceComponent.UINeoForceManager != null)
        {
            Game.UserInterfaceComponent.UINeoForceManager.DefaultRenderTarget = RenderTargetBackBuffer;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        var newSizeWidth = (int)sizeInfo.NewSize.Width;
        var newSizeHeight = (int)sizeInfo.NewSize.Height;

        OnScreenResized(newSizeWidth, newSizeHeight);
    }

    private void OnScreenResized(int width, int height)
    {
        //in editor the camera is not an element of the world
        Game?.GameManager.ActiveCamera?.OnScreenResized(width, height);
        Game?.OnScreenResized(width, height);
    }
}