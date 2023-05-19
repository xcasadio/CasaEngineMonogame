using System;
using System.Windows;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
using EditorWpf.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace EditorWpf.Controls;

public class GameEditor : WpfGame
{
    private bool _isInitialized;
    public event EventHandler? GameStarted;

    public CasaEngineGame? Game { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }

    protected override bool CanRender => _isInitialized;

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        Game = new CasaEngineGame(null, graphicsDeviceService);
        Game.GameManager.Initialize();
        Game.GameManager.SetInputProvider(new KeyboardStateProvider(new WpfKeyboard(this)), new MouseStateProvider(new WpfMouse(this)));

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
        //in editor the camera is an element of the world
        Game?.GameManager.ActiveCamera?.ScreenResized((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
        Game?.GameManager.OnScreenResized((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void Update(GameTime gameTime)
    {
        Game.GameManager.BeginUpdate(gameTime);

        foreach (var component in Game.Components)
        {
            if (component is IUpdateable { Enabled: true } updateable)
            {
                updateable.Update(gameTime);
            }
        }

        base.Update(gameTime);
        Game.GameManager.EndUpdate(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        Game.GameManager.BeginDraw(gameTime);

        foreach (var component in Game.Components)
        {
            if (component is IDrawable { Visible: true } drawable)
            {
                drawable.Draw(gameTime);
            }
        }

        base.Draw(gameTime);
        Game.GameManager.EndDraw(gameTime);
    }

}