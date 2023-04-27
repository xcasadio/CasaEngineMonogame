using System;
using System.Text.Json;
using System.Windows;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using EditorWpf.Datas;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls;

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

        base.LoadContent();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);

            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            e.Handled = true;
            var position = e.GetPosition(this);
            var camera = Game?.GameManager.ActiveCamera;
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
                    Game?.GameManager.CurrentWorld.AddEntityImmediately(entity);

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