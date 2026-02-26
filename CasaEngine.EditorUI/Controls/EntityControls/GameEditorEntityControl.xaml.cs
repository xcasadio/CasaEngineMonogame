using System;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class GameEditorEntityControl : UserControl
{
    private EntityViewModel? EntityControlViewModel => DataContext as EntityViewModel;
    private ViewId _viewId = ViewId.Empty;

    public ViewId EntityViewId => _viewId;

    /// <summary>Fires after the view has been registered with the EngineHost, carrying the assigned ViewId.</summary>
    public event EventHandler<ViewId>? ViewIdReady;

    public GameEditorEntityControl()
    {
        InitializeComponent();

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
            Name = "Entity",
            ViewType = EditorViewType.Entity,
            ShowGizmo = true,
            ShowGrid = true,
            ShowAxis = true,
            InitialWidth = Math.Max((int)gameEntityEditor.ActualWidth, 1),
            InitialHeight = Math.Max((int)gameEntityEditor.ActualHeight, 1),
        });

        gameEntityEditor.Attach(host, _viewId);
        ViewIdReady?.Invoke(this, _viewId);
    }

    /// <summary>
    /// Loads <paramref name="entity"/> into this view's dedicated preview world,
    /// replacing any previously loaded entity.
    /// </summary>
    public void LoadEntity(Entity entity)
    {
        var host = EngineHost.Instance;
        var ctx = host?.GetViewContext(_viewId);
        var world = ctx?.World;
        if (world == null) return;

        entity.Initialize();
        world.ClearEntities();
        entity.InitializeWithWorld(world);
        world.AddEntityWithEditor(entity);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (EntityControlViewModel == null) return;

        var fileName = EntityControlViewModel.Entity.FileName;
        AssetSaver.SaveAsset(fileName, EntityControlViewModel.Entity);
        Logs.WriteInfo($"Entity {EntityControlViewModel.Entity.Name} saved ({fileName})");
    }
}