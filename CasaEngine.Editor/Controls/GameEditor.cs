using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CasaEngine.Framework.Game;
using CasaEngine.Editor.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;

namespace CasaEngine.Editor.Controls;

public class GameEditor : WpfGame
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

        Game.GameManager.UseGui = UseGui;

        Game.GameManager.Initialize();

        if (UseGui)
        {
            Game.GameManager.UiManager.DefaultRenderTarget = RenderTargetBackBuffer;
        }

        Game.GameManager.SetInputProvider(
            new KeyboardStateProvider(new WpfKeyboard(this)),
            new MouseStateProvider(new WpfMouse(this)));

        //In editor mode the game is in idle mode so we don't update physics
        Game.GameManager.PhysicsEngineComponent.Enabled = false;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        Game.GameManager.BeginLoadContent();
        base.LoadContent();
        Game.GameManager.EndLoadContent();

        foreach (var component in Game.Components)
        {
            component.Initialize();
        }

        GameStarted?.Invoke(Game, EventArgs.Empty);
        _isInitialized = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        var newSizeWidth = (int)sizeInfo.NewSize.Width;
        var newSizeHeight = (int)sizeInfo.NewSize.Height;

        //in editor the camera is an element of the world
        Game?.GameManager.ActiveCamera?.ScreenResized(newSizeWidth, newSizeHeight);
        Game?.GameManager.OnScreenResized(newSizeWidth, newSizeHeight);
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void Update(GameTime gameTime)
    {
        Game.GameManager.BeginUpdate(gameTime);

        var sortedGameComponents = new List<GameComponent>(Game.Components.Count);
        sortedGameComponents.AddRange(Game.Components
            .Where(x => x is IUpdateable { Enabled: true })
            .Cast<GameComponent>()
            .OrderBy(x => x.UpdateOrder));

        foreach (var component in sortedGameComponents)
        {
            component.Update(gameTime);
        }

        base.Update(gameTime);
        Game.GameManager.EndUpdate(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        Game.GameManager.BeginDraw(gameTime);

        var sortedGameComponents = new List<IDrawable>(Game.Components.Count);
        sortedGameComponents.AddRange(Game.Components
            .Where(x => x is IDrawable { Visible: true })
            .Cast<IDrawable>()
            .OrderBy(x => x.DrawOrder));

        foreach (var component in sortedGameComponents)
        {
            component.Draw(gameTime);
        }

        base.Draw(gameTime);
        Game.GameManager.EndDraw(gameTime);
    }

    protected override void CreateGraphicsDeviceDependentResources(PresentationParameters pp)
    {
        base.CreateGraphicsDeviceDependentResources(pp);

        if (UseGui && Game?.GameManager?.UiManager != null)
        {
            Game.GameManager.UiManager.DefaultRenderTarget = RenderTargetBackBuffer;
        }
    }
}