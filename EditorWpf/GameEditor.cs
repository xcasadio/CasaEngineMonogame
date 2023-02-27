using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EditorWpf;

public class GameEditor : WpfGame
{
    private GameManager _gameManager;
    private CasaEngineGame _game;

    public GameEditor()
    {
        SizeChanged += OnSizeChanged;
    }

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        _game = new CasaEngineGame();
        _gameManager = new GameManager(_game, graphicsDeviceService);
        _gameManager.Initialize();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        var world = new World();
        GameInfo.Instance.CurrentWorld = world;

        var entity = new Entity();
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        GameInfo.Instance.ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        world.AddObjectImmediately(entity);

        entity = new Entity();
        //entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
        var meshComponent = new MeshComponent(entity);
        entity.ComponentManager.Components.Add(meshComponent);
        meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        world.AddObjectImmediately(entity);

        _gameManager.BeginLoadContent();
        base.LoadContent();
        _gameManager.EndLoadContent();

        foreach (var component in _game.Components)
        {
            component.Initialize();
        }
    }

    private void OnSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (_game != null)
        {
            _gameManager.OnScreenResized((int)e.NewSize.Width, (int)e.NewSize.Height);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _gameManager.BeginUpdate(gameTime);

        foreach (var component in _game.Components)
        {
            if (component is IUpdateable updateable && updateable.Enabled)
            {
                updateable.Update(gameTime);
            }
        }

        base.Update(gameTime);
        _gameManager.EndUpdate(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _gameManager.BeginDraw(gameTime);

        foreach (var component in _game.Components)
        {
            if (component is IDrawable drawable && drawable.Visible)
            {
                drawable.Draw(gameTime);
            }
        }

        base.Draw(gameTime);
        _gameManager.EndDraw(gameTime);
    }
}