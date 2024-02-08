using System;
using System.IO;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GUI;
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
            "Place Actors" => PlaceEntitiesControl,
            "Place Entities" => PlaceEntitiesControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenWorld(string fileName)
    {
        GameEditor.Game.GameManager.SetWorldToLoad(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var world = GameEditor.Game.GameManager.CurrentWorld;
        AssetSaver.SaveAsset(world.FileName, world);
        Logs.WriteInfo($"World {world.Name} saved ({world.FileName})");

        //SaveEverything();
    }

    private void SaveEverything()
    {
        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            ObjectBase actor = null;

            switch (Path.GetExtension(assetInfo.FileName))
            {
                case Constants.FileNameExtensions.Entity:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<Entity>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.World:
                    continue;
                    //actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<World>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Texture:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<Texture>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Sprite:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<SpriteData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Animation2d:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<Animation2dData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.TileMap:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<TileMapData>(assetInfo.Id);
                    break;
                case ".tileset":
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<TileSetData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Screen:
                    actor = GameScreenControl.gameEditor.Game.AssetContentManager.Load<ScreenGui>(assetInfo.Id);
                    break;
                default:
                    Logs.WriteWarning($"Object '{assetInfo.FileName}' skipped");
                    continue;
            }

            AssetSaver.SaveAsset(actor.FileName, actor);
        }

        AssetCatalog.Save();
    }
}