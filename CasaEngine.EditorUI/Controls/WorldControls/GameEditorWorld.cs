using System.IO;
using System.Text.Json;
using System.Windows;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.WorldControls.ViewModels;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using XNAGizmo;
using Point = System.Windows.Point;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public class GameEditorWorld : GameEditor
{
    private GizmoComponent? _gizmoComponent;

    public GameEditorWorld()
    {
        Drop += OnDrop;
    }

    protected override void InitializeGame()
    {
        var gizmoComponent = new GizmoComponent(Game);
        var gridComponent = new GridComponent(Game);
        var axisComponent = new AxisComponent(Game);
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        //must be open by content browser
        Game.GameManager.SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);
        Game.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;
        _gizmoComponent = Game.GetGameComponent<GizmoComponent>();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var formats = e.Data.GetFormats();

        if (formats.Length > 0)
        {
            if (formats[0] == typeof(AssetInfo).FullName)
            {
                var assetInfo = e.Data.GetData(typeof(AssetInfo)) as AssetInfo;

                var extension = Path.GetExtension(assetInfo.FileName);

                if (extension == Constants.FileNameExtensions.Entity)
                {
                    var entityReference = EntityReference.CreateFromAssetInfo(assetInfo, Game.AssetContentManager);
                    CreateEntity(entityReference.Entity, e.GetPosition(this));
                }
                else
                {
                    Logs.WriteWarning($"The asset with the type {extension} is not supported");
                }

                return;
            }
        }

        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            if (dragAndDropInfo.Action == DragAndDropInfoAction.Create)
            {
                e.Handled = true;

                var entity = new Entity();

                if (dragAndDropInfo.Type == DragAndDropInfoType.Entity)
                {
                    //do nothing : empty entity
                }
                else if (dragAndDropInfo.Type == DragAndDropInfoType.PlayerStart)
                {
                    entity.RootComponent = new PlayerStartComponent();
                }

                CreateEntity(entity, e.GetPosition(this));
            }
            else
            {
                Logs.WriteWarning($"The action {dragAndDropInfo.Action} is not supported");
            }
        }
    }

    private void CreateEntity(Entity entity, Point mousePosition)
    {
        _gizmoComponent.Gizmo.Clear();

        entity.Initialize();
        entity.InitializeWithWorld(Game.GameManager.CurrentWorld);

        var worldEditorViewModel = DataContext as WorldEditorViewModel;
        worldEditorViewModel.EntitiesViewModel.Add(entity);

        _gizmoComponent.Gizmo.SetSelectionPool(Game.GameManager.CurrentWorld.GetSelectableComponents());

        if (entity.RootComponent != null)
        {
            var position = mousePosition;
            var camera = Game?.GameManager.ActiveCamera;
            var ray = RayHelper.CalculateRayFromScreenCoordinate(
                new Vector2((float)position.X, (float)position.Y),
                camera.ProjectionMatrix, camera.ViewMatrix, camera.Viewport);

            //TODO : check intersection with object or the plane XZ
            entity.RootComponent.Coordinates.Position = ray.Position + ray.Direction * 5.0f;

            //TODO : devrait etre selectionné tout seul avec le changement de dataContext
            //_gizmoComponent.Gizmo.Selection.Add(entity.RootComponent);
            //_gizmoComponent.Gizmo.RaiseSelectionChanged();
        }
    }
}