using System.Windows;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class GameEditorEntity : GameEditor
{
    private Entity _cameraEntity;
    private ArcBallCameraComponent _camera;

    public GameEditorEntity()
    {
        //Drop += OnDrop;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext != null)
        {
            var entityViewModel = DataContext as EntityViewModel;
            entityViewModel.Entity.Initialize(Game);
            Game.GameManager.ActiveCamera = _camera;
            Game.GameManager.CurrentWorld.ClearEntities();
            Game.GameManager.CurrentWorld.AddEntity(_cameraEntity);
            Game.GameManager.CurrentWorld.AddEntity(entityViewModel.Entity);
        }
    }

    protected override void LoadContent()
    {
        new AxisComponent(Game);

        base.LoadContent();

        Game.GameManager.CurrentWorld = new World();
        Game.GameManager.CurrentWorld.Initialize(Game);

        _cameraEntity = new Entity();
        _camera = new ArcBallCameraComponent();
        _cameraEntity.ComponentManager.Add(_camera);
        var gamePlayComponent = new GamePlayComponent();
        _cameraEntity.ComponentManager.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptArcBallCamera();
        _cameraEntity.Initialize(Game);

        _camera.SetCamera(new Vector3(0, 5f, -10f), Vector3.Zero, Vector3.Up);
        Game.GameManager.ActiveCamera = _camera;
        Game.GameManager.CurrentWorld.AddEntity(_cameraEntity);

        Game.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        //if (e.Data.GetDataPresent(DataFormats.StringFormat))
        //{
        //    string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
        //
        //    var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);
        //
        //    e.Handled = true;
        //    var position = e.GetPosition(this);
        //    var camera = Game?.GameManager.ActiveCamera;
        //    var ray = RayHelper.CalculateRayFromScreenCoordinate(
        //        new Vector2((float)position.X, (float)position.Y),
        //        camera.ProjectionMatrix, camera.ViewMatrix, camera.Viewport);
        //    //tester si le ray intersect un model sinon ray.Position
        //
        //    //create element type from dataString
        //    //add element at ray.Position => 
        //
        //    if (dragAndDropInfo.Action == DragAndDropInfoAction.Create)
        //    {
        //        if (dragAndDropInfo.Type == DragAndDropInfoType.Actor)
        //        {
        //            var entity = new Entity
        //            {
        //                Name = "Entity " + new Random().NextInt64()
        //            };
        //            entity.Coordinates.LocalPosition = ray.Position + ray.Direction * 15.0f;//entity.BoundingBox.;
        //            entity.Initialize(Game);
        //            Game?.GameManager.CurrentWorld.AddEntityImmediately(entity);
        //
        //            //select this entity
        //            var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
        //            gizmoComponent.Gizmo.Clear(); // TODO
        //            gizmoComponent.Gizmo.Selection.Add(entity);
        //            gizmoComponent.Gizmo.RaiseSelectionChanged();
        //        }
        //    }
        //}
    }
}