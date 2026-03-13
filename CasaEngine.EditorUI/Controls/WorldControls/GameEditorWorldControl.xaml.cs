using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.WorldControls.ViewModels;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using System;
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
        gameViewport.Drop += OnDrop;
        gameViewport.DragOver += OnDragOver;
        if (EngineHost.Instance?.IsStarted == true)
        {
            OnEngineHostStarted(EngineHost.Instance, EventArgs.Empty);
        }
        else
        {
            EngineHost.InstanceStarted += OnEngineHostStarted;
        }
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        if (host == null)
        {
            return;
        }

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
        if (physicsDebugView != null)
        {
            physicsDebugView.DisplayPhysics = true;
        }

        host.Game.GameManager.WorldChanged += OnWorldChanged;

        if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
        {
            host.Game.GameManager.SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);
        }

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
        if (game == null)
        {
            return;
        }

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

    private void OnDragOver(object sender, DragEventArgs e)
        => AssetDropHelper.HandleDragOver(e);

    private void OnDrop(object sender, DragEventArgs e)
    {
        var game = EngineHost.Instance?.Game;
        if (game != null)
        {
            var entity = AssetDropHelper.HandleDrop(e, game);
            if (entity != null)
            {
                CreateEntity(entity, e.GetPosition(gameViewport));
                return;
            }
        }

        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            if (dragAndDropInfo!.Action == DragAndDropInfoAction.Create)
            {
                var handler = ToolboxDropHandlerRegistry.Instance.FindHandler(dragAndDropInfo);
                if (handler != null)
                {
                    e.Handled = true;
                    CreateEntity(handler.CreateEntity(dragAndDropInfo), e.GetPosition(gameViewport));
                }
                else
                {
                    Logs.WriteWarning($"The toolbox type {dragAndDropInfo.Type} is not supported");
                }
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
        if (host == null)
        {
            return;
        }

        var ctx = host.GetViewContext(_viewId);
        var gizmoComponent = ctx?.Gizmo;
        gizmoComponent?.ClearSelection();

        entity.Initialize();
        entity.InitializeWithWorld(host.Game.GameManager.CurrentWorld);

        (DataContext as WorldEditorViewModel)?.EntitiesViewModel.Add(entity);

        gizmoComponent?.SetSelectionPool(host.Game.GameManager.CurrentWorld.GetSelectableComponents());

        if (entity.RootComponent != null && ctx?.Camera != null)
        {
            var ray = RayHelper.CalculateRayFromScreenCoordinate(
                new Vector2((float)mousePosition.X, (float)mousePosition.Y),
                ctx.Camera.ProjectionMatrix, ctx.Camera.ViewMatrix, ctx.Camera.Viewport);
            entity.RootComponent.Coordinates.Position = ray.Position + ray.Direction * 5.0f;
        }
    }
}