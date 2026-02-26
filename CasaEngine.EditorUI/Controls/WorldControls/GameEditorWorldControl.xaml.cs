using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.WorldControls.ViewModels;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Point = System.Windows.Point;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class GameEditorWorldControl : UserControl
{
    private WorldEditorViewModel? ScreenControlViewModel => DataContext as WorldEditorViewModel;
    private ViewId _viewId = ViewId.Empty;

    public ViewId WorldViewId => _viewId;

    /// <summary>Fires after the view has been registered with the EngineHost, carrying the assigned ViewId.</summary>
    public event EventHandler<ViewId>? ViewRegistered;

    public GameEditorWorldControl()
    {
        InitializeComponent();
        Drop += OnDrop;
        if (EngineHost.Instance?.IsStarted == true)
            OnEngineHostStarted(EngineHost.Instance, EventArgs.Empty);
        else
            EngineHost.InstanceStarted += OnEngineHostStarted;
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        if (host == null) return;

        _viewId = host.RegisterEditorView(new EditorViewDefinition
        {
            Name = "World",
            ViewType = EditorViewType.World,
            ShowGizmo = true,
            ShowGrid = true,
            ShowAxis = true,
            InitialWidth = Math.Max((int)gameViewport.ActualWidth, 1),
            InitialHeight = Math.Max((int)gameViewport.ActualHeight, 1),
        });

        gameViewport.Attach(host, _viewId);
        DataContext = new WorldEditorViewModel(host, _viewId);

        var physicsDebugView = host.Game.GetGameComponent<PhysicsDebugViewRendererComponent>();
        if (physicsDebugView != null) physicsDebugView.DisplayPhysics = true;
        host.Game.GameManager.WorldChanged += OnWorldChanged;

        if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
            host.Game.GameManager.SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);

        ViewRegistered?.Invoke(this, _viewId);
    }

    private void OnWorldChanged(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        var ctx = host?.GetViewContext(_viewId);
        var currentWorld = host?.Game?.GameManager.CurrentWorld;
        if (ctx?.RenderView != null && currentWorld != null)
        {
            ctx.RenderView.World = currentWorld;
            ctx.World = currentWorld;
        }
    }

    private void ButtonLaunchGame_Click(object sender, RoutedEventArgs e)
    {
        var game = EngineHost.Instance?.Game;
        if (game == null) return;
        game.IsRunningInGameEditorMode = !game.IsRunningInGameEditorMode;
        game.PhysicsEngineComponent.Enabled = game.IsRunningInGameEditorMode;
        buttonLaunch.Content = game.IsRunningInGameEditorMode ? "Running" : "Launch";
    }

    private void ButtonTranslate_Click(object sender, RoutedEventArgs e)
        => ScreenControlViewModel!.IsTranslationMode = true;

    private void ButtonRotate_Click(object sender, RoutedEventArgs e)
        => ScreenControlViewModel!.IsRotationMode = true;

    private void ButtonScale_Click(object sender, RoutedEventArgs e)
        => ScreenControlViewModel!.IsScaleMode = true;

    private void ButtonLocalSpace_Click(object sender, RoutedEventArgs e)
        => ScreenControlViewModel!.IsTransformSpaceLocal = true;

    private void ButtonWorldSpace_Click(object sender, RoutedEventArgs e)
        => ScreenControlViewModel!.IsTransformSpaceWorld = true;

    private void OnDrop(object sender, DragEventArgs e)
    {
        var formats = e.Data.GetFormats();

        if (formats.Length > 0)
        {
            if (formats[0] == typeof(AssetInfo).FullName)
            {
                var assetInfo = e.Data.GetData(typeof(AssetInfo)) as AssetInfo;
                var game = EngineHost.Instance?.Game;
                if (assetInfo != null && game != null)
                {
                    var handler = AssetDropHandlerRegistry.Instance.FindHandler(assetInfo);
                    if (handler != null)
                    {
                        CreateEntity(handler.CreateEntity(assetInfo, game), e.GetPosition(gameViewport));
                    }
                    else
                    {
                        var extension = Path.GetExtension(assetInfo.FileName);
                        Logs.WriteWarning($"The asset with the type {extension} is not supported");
                    }
                }
                return;
            }
        }

        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            if (dragAndDropInfo!.Action == DragAndDropInfoAction.Create)
            {
                e.Handled = true;
                var entity = new Entity();

                if (dragAndDropInfo.Type == DragAndDropInfoType.Entity)
                {
                    // empty entity — no root component needed
                }
                else if (dragAndDropInfo.Type == DragAndDropInfoType.PlayerStart)
                {
                    entity.RootComponent = new PlayerStartComponent();
                }

                CreateEntity(entity, e.GetPosition(gameViewport));
            }
            else
            {
                Logs.WriteWarning($"The action {dragAndDropInfo.Action} is not supported");
            }
        }
    }

    private void CreateEntity(Entity entity, Point mousePosition)
    {
        var host = EngineHost.Instance;
        if (host == null) return;

        var ctx = host.GetViewContext(_viewId);
        var gizmoComponent = ctx?.Gizmo;
        gizmoComponent?.Gizmo.Clear();

        entity.Initialize();
        entity.InitializeWithWorld(host.Game.GameManager.CurrentWorld);

        (DataContext as WorldEditorViewModel)?.EntitiesViewModel.Add(entity);

        gizmoComponent?.Gizmo.SetSelectionPool(host.Game.GameManager.CurrentWorld.GetSelectableComponents());

        if (entity.RootComponent != null && ctx?.Camera != null)
        {
            var ray = RayHelper.CalculateRayFromScreenCoordinate(
                new Vector2((float)mousePosition.X, (float)mousePosition.Y),
                ctx.Camera.ProjectionMatrix, ctx.Camera.ViewMatrix, ctx.Camera.Viewport);
            entity.RootComponent.Coordinates.Position = ray.Position + ray.Direction * 5.0f;
        }
    }
}