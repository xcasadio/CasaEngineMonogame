using System.IO;
using System.Text.Json;
using System.Windows;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public class GameEditorWorld : GameEditor
{
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
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var formats = e.Data.GetFormats();

        if (formats.Length > 0)
        {
            if (formats[0] == typeof(AssetInfo).FullName)
            {
                var assetInfo = e.Data.GetData(typeof(AssetInfo)) as AssetInfo;

                var extension = Path.GetExtension(assetInfo.Name);

                if (extension == Constants.FileNameExtensions.Entity)
                {
                    var entityReference = EntityReference.CreateFromAssetInfo(assetInfo, Game.AssetContentManager);
                    entityReference.Entity.Initialize();
                    entityReference.Entity.InitializeWithWorld(Game.GameManager.CurrentWorld);
                    Game.GameManager.CurrentWorld.AddEntityReference(entityReference);

                    var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
                    gizmoComponent.Gizmo.Clear();
                    if (entityReference.Entity.RootComponent != null)
                    {
                        gizmoComponent.Gizmo.Selection.Add(entityReference.Entity.RootComponent);
                    }
                    gizmoComponent.Gizmo.RaiseSelectionChanged();
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

                var position = e.GetPosition(this);
                var camera = Game?.GameManager.ActiveCamera;
                var ray = RayHelper.CalculateRayFromScreenCoordinate(
                    new Vector2((float)position.X, (float)position.Y),
                    camera.ProjectionMatrix, camera.ViewMatrix, camera.Viewport);

                var entity = new AActor();

                if (dragAndDropInfo.Type == DragAndDropInfoType.Entity)
                {
                    //do nothing : empty entity
                }
                else if (dragAndDropInfo.Type == DragAndDropInfoType.PlayerStart)
                {
                    entity.RootComponent = new PlayerStartComponent();
                }

                entity.RootComponent.Coordinates.Position = ray.Position + ray.Direction * 15.0f;

                entity.Initialize();
                entity.InitializeWithWorld(Game.GameManager.CurrentWorld);

                Game?.GameManager.CurrentWorld.AddEntityWithEditor(entity);

                //select this entity
                var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
                //TODO : do it in one function
                gizmoComponent.Gizmo.Clear();
                if (entity.RootComponent != null)
                {
                    gizmoComponent.Gizmo.Selection.Add(entity.RootComponent);
                }
                gizmoComponent.Gizmo.RaiseSelectionChanged();
            }
        }
    }
}