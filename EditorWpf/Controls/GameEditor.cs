using System;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.World;
using EditorWpf.Datas;
using EditorWpf.Inputs;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace EditorWpf.Controls;

public class GameEditor : WpfGame
{
    private CasaEngineGame? _game;

    public GameEditor()
    {
        Drop += OnDrop;
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
        //var world = new World();
        //GameInfo.Instance.CurrentWorld = world;
        //
        //var entity = new Entity();
        //entity.Name = "Entity camera";
        //var camera = new ArcBallCameraComponent(entity);
        //entity.ComponentManager.Components.Add(camera);
        //GameInfo.Instance.ActiveCamera = camera;
        //camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        //world.AddObjectImmediately(entity);
        //
        //entity = new Entity();
        //entity.Name = "Entity box";
        ////entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
        //var meshComponent = new StaticMeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        //world.AddObjectImmediately(entity);
        //
        //entity = new Entity();
        //entity.Name = "Entity box 2";
        //entity.Coordinates.LocalPosition += Vector3.UnitX * 2.0f;
        //meshComponent = new MeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        //world.AddObjectImmediately(entity);

        _game.GameManager.BeginLoadContent();
        base.LoadContent();
        _game.GameManager.EndLoadContent();

        foreach (var component in _game.Components)
        {
            component.Initialize();
        }

        GameInfo.Instance.InvokeReadyToStart(_game);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        _game?.GameManager.OnScreenResized((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
        base.OnRenderSizeChanged(sizeInfo);
    }

    protected override void Update(GameTime gameTime)
    {
        _game.GameManager.BeginUpdate(gameTime);

        foreach (var component in _game.Components)
        {
            if (component is IUpdateable { Enabled: true } updateable)
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
            if (component is IDrawable { Visible: true } drawable)
            {
                drawable.Draw(gameTime);
            }
        }

        base.Draw(gameTime);
        _game.GameManager.EndDraw(gameTime);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);

            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            e.Handled = true;
            var position = e.GetPosition(this);
            var camera = GameInfo.Instance.ActiveCamera;
            var ray = CasaEngine.Core.Helper.MathHelper.CalculateRayFromScreenCoordinate(
                new Vector2((float)position.X, (float)position.Y),
                camera.ProjectionMatrix, camera.ViewMatrix, camera.Viewport);
            //tester si le ray intersect un model sinon ray.Position

            //create element type from dataString
            //add element at ray.Position => 

            if (dragAndDropInfo.Action == DragAndDropInfoAction.Create)
            {
                if (dragAndDropInfo.Type == DragAndDropInfoType.Actor)
                {
                    var entity = new Entity
                    {
                        Name = "Entity " + new Random().NextInt64()
                    };
                    entity.Coordinates.LocalPosition = ray.Position + ray.Direction * 15.0f;//entity.BoundingBox.;
                    entity.Initialize();
                    GameInfo.Instance.CurrentWorld.AddObjectImmediately(entity);

                    //select this entity
                    var gizmoComponent = Engine.Instance.Game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Clear(); // TODO
                    gizmoComponent.Gizmo.Selection.Add(entity);
                    gizmoComponent.Gizmo.RaiseSelectionChanged();
                }
            }
        }
    }
}