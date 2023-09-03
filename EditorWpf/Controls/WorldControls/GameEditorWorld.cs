using System;
using System.Text.Json;
using System.Windows;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using EditorWpf.DragAndDrop;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.WorldControls;

public class GameEditorWorld : GameEditor
{
    public GameEditorWorld()
    {
        Drop += OnDrop;
    }

    protected override void LoadContent()
    {
        var gizmoComponent = new GizmoComponent(Game);
        var gridComponent = new GridComponent(Game);
        new AxisComponent(Game);

        base.LoadContent();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
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
                entity.Name = "Entity " + entity.AssetInfo.Id;
                entity.Coordinates.LocalPosition = ray.Position + ray.Direction * 15.0f;
                entity.Initialize(Game);

                if (dragAndDropInfo.Type == DragAndDropInfoType.Entity)
                {
                    //do nothing : empty entity
                }
                else if (dragAndDropInfo.Type == DragAndDropInfoType.PlayerStart)
                {
                    entity.ComponentManager.Add(new PlayerStartComponent(entity));
                }

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