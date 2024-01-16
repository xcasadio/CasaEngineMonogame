using System.Text.Json;
using System.Windows;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Editor.DragAndDrop;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.WorldControls;

public class GameEditorWorld : GameEditor
{
    public GameEditorWorld() : base(false)
    {
        Drop += OnDrop;
    }

    protected override void LoadContent()
    {
        var gizmoComponent = new GizmoComponent(Game);
        var gridComponent = new GridComponent(Game);
        var axisComponent = new AxisComponent(Game);

        base.LoadContent();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var formats = e.Data.GetFormats();

        if (formats.Length > 0)
        {
            if (formats[0] == typeof(AssetInfo).FullName)
            {
                var assetInfo = e.Data.GetData(typeof(AssetInfo)) as AssetInfo;
                var entityReference = EntityReference.CreateFromAssetInfo(assetInfo, Game.GameManager.AssetContentManager);
                entityReference.Entity.Initialize(Game);
                Game.GameManager.CurrentWorld.AddEntityReference(entityReference);

                var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
                gizmoComponent.Gizmo.Clear();
                gizmoComponent.Gizmo.Selection.Add(entityReference.Entity);
                gizmoComponent.Gizmo.RaiseSelectionChanged();

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

                var entity = new Entity();
                entity.Coordinates.LocalPosition = ray.Position + ray.Direction * 15.0f;

                if (dragAndDropInfo.Type == DragAndDropInfoType.Entity)
                {
                    //do nothing : empty entity
                }
                else if (dragAndDropInfo.Type == DragAndDropInfoType.PlayerStart)
                {
                    entity.ComponentManager.Add(new PlayerStartComponent());
                }

                entity.Initialize(Game);

                Game?.GameManager.CurrentWorld.AddEntityEditorMode(entity);

                //select this entity
                var gizmoComponent = Game.GetGameComponent<GizmoComponent>();
                gizmoComponent.Gizmo.Clear();
                gizmoComponent.Gizmo.Selection.Add(entity);
                gizmoComponent.Gizmo.RaiseSelectionChanged();
            }
        }
    }
}