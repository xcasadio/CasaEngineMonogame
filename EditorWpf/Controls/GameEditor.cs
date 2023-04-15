using System;
using System.Text.Json;
using System.Windows;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using EditorWpf.Datas;
using EditorWpf.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace EditorWpf.Controls;

public class GameEditor : WpfGame
{
    public static event EventHandler? GameStarted;

    public static CasaEngineGame? Game { get; private set; }

    public GameEditor()
    {
        Drop += OnDrop;
    }

    protected override bool CanRender => !string.IsNullOrEmpty(Game.GameManager.ProjectSettings.FirstWorldLoaded);

    protected override void Initialize()
    {
        var graphicsDeviceService = new WpfGraphicsDeviceService(this);
        Game = new CasaEngineGame(graphicsDeviceService);
        Game.GameManager.Initialize();

        Game.GameManager.SetInputProvider(new KeyboardStateProvider(new WpfKeyboard(this)), new MouseStateProvider(new WpfMouse(this)));

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
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        //in editor the camera is an element of the world
        GameInfo.Instance.ActiveCamera?.ScreenResized((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
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
                    entity.Initialize(Game);
                    GameInfo.Instance.CurrentWorld.AddObjectImmediately(entity);

                    //select this entity
                    var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Clear(); // TODO
                    gizmoComponent.Gizmo.Selection.Add(entity);
                    gizmoComponent.Gizmo.RaiseSelectionChanged();
                }
            }
        }
    }
}