using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using EditorWpf.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace EditorWpf.Controls;

public class GameEditor : WpfGame
{
    private CasaEngineGame _game;

    public GameEditor()
    {
        SizeChanged += OnSizeChanged;
    }

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        _game = new CasaEngineGame(graphicsDeviceService);
        _game.GameManager.Initialize();

        _game.GameManager.SetInputProvider(new KeyboardStateProvider(new WpfKeyboard(this)),
            new MouseStateProvider(new WpfMouse(this)));

        base.Initialize();
    }

    protected override void LoadContent()
    {
        var world = new World();
        GameInfo.Instance.CurrentWorld = world;

        var entity = new Entity();
        entity.Name = "Entity camera";
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        GameInfo.Instance.ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        world.AddObjectImmediately(entity);

        entity = new Entity();
        entity.Name = "Entity box";
        //entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
        var meshComponent = new MeshComponent(entity);
        entity.ComponentManager.Components.Add(meshComponent);
        meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        world.AddObjectImmediately(entity);

        _game.GameManager.BeginLoadContent();
        base.LoadContent();
        _game.GameManager.EndLoadContent();

        foreach (var component in _game.Components)
        {
            component.Initialize();
        }

        GameInfo.Instance.InvokeReadyToStart(_game);
    }

    private void OnSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (_game != null)
        {
            _game.GameManager.OnScreenResized((int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _game.GameManager.BeginUpdate(gameTime);

        foreach (var component in _game.Components)
        {
            if (component is IUpdateable updateable && updateable.Enabled)
            {
                updateable.Update(gameTime);
            }
        }

        base.Update(gameTime);
        _game.GameManager.EndUpdate(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _game.GameManager.BeginDraw(gameTime);

        foreach (var component in _game.Components)
        {
            if (component is IDrawable drawable && drawable.Visible)
            {
                drawable.Draw(gameTime);
            }
        }

        base.Draw(gameTime);
        _game.GameManager.EndDraw(gameTime);
    }
}