using System;
using System.Windows;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
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

    public event EventHandler GameStarted;

    public GameEditorEntity GameEditorEntity => GameEditorEntityControl.gameEntityEditor;

    public EntityEditorControl()
    {
        InitializeComponent();

        GameEditorEntityControl.gameEntityEditor.GameStarted += OnGameStarted;
        EntityControl.InitializeFromGameEditor(GameEditorEntityControl.gameEntityEditor);
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        GameStarted?.Invoke(this, e);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Details" => EntityControl,
            "Entity Editor" => GameEditorEntityControl,
            "Game ScreenGui" => GameEditorEntityControl, // TODO : remove
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };

        // TODO : remove
        if (e.Model.Title == "Game ScreenGui")
        {
            e.Model.Title = "Entity Editor";
        }
    }

    public void LoadEntity(string fileName)
    {
        var game = GameEditorEntityControl.gameEntityEditor.Game;
        game.GameManager.CurrentWorld.ClearEntities();

        var assetInfo = AssetCatalog.GetByFileName(fileName);
        var entity = game.AssetContentManager.Load<AActor>(assetInfo.Id);
        game.GameManager.CurrentWorld.AddEntity(entity);
        SelectedItem = new EntityViewModel(entity);
    }
}