using System;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class WorldEditorControl : EditorControlBase
{
    protected override string LayoutFileName => "worldEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerWorld;

    public event EventHandler GameStarted;

    public GameEditor GameEditor => GameScreenControl.gameEditor;

    public WorldEditorControl()
    {
        InitializeComponent();

        GameScreenControl.gameEditor.GameStarted += OnGameStarted;
        EntitiesControl.InitializeFromGameEditor(GameScreenControl.gameEditor);
        EntityControl.InitializeFromGameEditor(GameScreenControl.gameEditor);
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        GameStarted?.Invoke(this, e);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Entities" => EntitiesControl,
            "Details" => EntityControl,
            "Game ScreenGui" => GameScreenControl,
            "Place Actors" => PlaceActorsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenWorld(string fileName)
    {
        //GameEditor.Game.GameManager.CurrentWorld = 
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var world = GameEditor.Game.GameManager.CurrentWorld;
        AssetSaver.SaveAsset(world.FileName, world);
        Logs.WriteInfo($"World {world.Name} saved ({world.FileName})");
    }
}