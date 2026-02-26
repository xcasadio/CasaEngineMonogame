using System;
using System.Windows;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityEditorControl : EditorControlBase
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(EntityEditorControl));

    public EntityViewModel SelectedItem
    {
        get => (EntityViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    protected override string LayoutFileName => "entityEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerWorld;

    public EntityEditorControl()
    {
        InitializeComponent();

        // Wire up EntityControl to the entity preview viewport once it is registered.
        GameEditorEntityControl.ViewIdReady += OnEntityViewRegistered;
    }

    private void OnEntityViewRegistered(object? sender, ViewId viewId)
    {
        var host = EngineHost.Instance;
        if (host == null) return;
        EntityControl.InitializeFromEngineHost(host, viewId);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Details" => EntityControl,
            "Entity Editor" => GameEditorEntityControl,
            "Game ScreenGui" => GameEditorEntityControl, // TODO: remove
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };

        // TODO: remove
        if (e.Model.Title == "Game ScreenGui")
            e.Model.Title = "Entity Editor";
    }

    public void OpenEntity(string fileName)
    {
        var game = EngineHost.Instance?.Game;
        if (game == null) return;

        var assetInfo = AssetCatalog.GetByFileName(fileName);
        var entity = game.AssetContentManager.Load<Entity>(assetInfo.Id);
        entity.ReActivate();

        GameEditorEntityControl.LoadEntity(entity);
        SelectedItem = new EntityViewModel(entity);
    }
}