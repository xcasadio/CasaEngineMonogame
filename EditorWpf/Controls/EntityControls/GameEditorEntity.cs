﻿using System;
using System.Text.Json;
using System.Windows;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using EditorWpf.Controls.SpriteControls;
using EditorWpf.Datas;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.EntityControls;

public class GameEditorEntity : GameEditor
{
    private Entity _cameraEntity;
    private ArcBallCameraComponent _camera;

    public GameEditorEntity()
    {
        //Drop += OnDrop;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext != null)
        {
            var entityDataViewModel = DataContext as EntityDataViewModel;
            entityDataViewModel.Entity.Initialize(Game);
            Game.GameManager.ActiveCamera = _camera;
            Game.GameManager.CurrentWorld.ClearEntities();
            Game.GameManager.CurrentWorld.AddEntityImmediately(_cameraEntity);
            Game.GameManager.CurrentWorld.AddEntityImmediately(entityDataViewModel.Entity);
            //StaticSpriteComponent.TryLoadSpriteData(spriteData?.Name);
        }
    }

    protected override void LoadContent()
    {
        //var gizmoComponent = new GizmoComponent(Game);
        //var gridComponent = new GridComponent(Game);
        new AxisComponent(Game);

        base.LoadContent();

        Game.GameManager.CurrentWorld = new World();

        _cameraEntity = new Entity();
        _camera = new ArcBallCameraComponent(_cameraEntity);
        _cameraEntity.ComponentManager.Components.Add(_camera);
        _camera.SetCamera(new Vector3(0, 5f, -10f), Vector3.Zero, Vector3.Up);
        var gamePlayComponent = new GamePlayComponent(_cameraEntity);
        _cameraEntity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptArcBallCamera(_cameraEntity);
        _cameraEntity.Initialize(Game);
        Game.GameManager.ActiveCamera = _camera;
        Game.GameManager.CurrentWorld.AddEntityImmediately(_cameraEntity);

        //_inputComponent = Game.GetGameComponent<InputComponent>();
        //Game.Components.Remove(Game.GetGameComponent<GridComponent>());

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