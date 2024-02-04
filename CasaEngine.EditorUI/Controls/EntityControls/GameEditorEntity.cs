using System.Windows;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class GameEditorEntity : GameEditor
{
    private bool _isEntityMustBeLoaded;

    public GameEditorEntity()
    {
        //Drop += OnDrop;
        DataContextChanged += OnDataContextChanged;
    }

    protected override void InitializeGame()
    {
        var gizmoComponent = new GizmoComponent(Game);
        var gridComponent = new GridComponent(Game);
        new AxisComponent(Game);
        Game.GameManager.WorldChanged += OnWorldChanged;
    }

    private void OnWorldChanged(object? sender, System.EventArgs e)
    {
        if (_isEntityMustBeLoaded)
        {
            LoadEntityFromDataContext();
            _isEntityMustBeLoaded = false;
        }
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        Game.GameManager.SetWorldToLoad(new World());
        Game.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext == null)
        {
            return;
        }

        if (Game.GameManager.CurrentWorld?.Game != null)
        {
            LoadEntityFromDataContext();
        }
        else
        {
            _isEntityMustBeLoaded = true;
        }
    }

    private void LoadEntityFromDataContext()
    {
        var entityViewModel = DataContext as EntityViewModel;
        entityViewModel.Entity.Initialize();
        var world = Game.GameManager.CurrentWorld;
        entityViewModel.Entity.InitializeWithWorld(world);
        world.ClearEntities();
        world.AddEntityWithEditor(entityViewModel.Entity);
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
        //            entity.LoadContent(Game);
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