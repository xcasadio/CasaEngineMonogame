using System;
using System.IO;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class WorldEditorControl : EditorControlBase
{
    protected override string LayoutFileName => "worldEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerWorld;

    public WorldEditorControl()
    {
        InitializeComponent();

        // Wire up sub-controls once the world viewport's view is registered.
        GameScreenControl.ViewRegistered += OnWorldViewRegistered;
    }

    private void OnWorldViewRegistered(object? sender, ViewId viewId)
    {
        var host = EngineHost.Instance;
        if (host == null) return;

        EntitiesControl.InitializeFromEngineHost(host, viewId);
        EntityControl.InitializeFromEngineHost(host, viewId);
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
        EngineHost.Instance?.Game?.GameManager.SetWorldToLoad(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var world = EngineHost.Instance?.Game?.GameManager.CurrentWorld;
        if (world == null) return;
        AssetSaver.SaveAsset(world.FileName, world);
        Logs.WriteInfo($"World {world.Name} saved ({world.FileName})");
    }

    private void SaveEverything()
    {
        var game = EngineHost.Instance?.Game;
        if (game == null) return;

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            ObjectBase? actor = null;

            switch (Path.GetExtension(assetInfo.FileName))
            {
                case Constants.FileNameExtensions.Entity:
                    actor = game.AssetContentManager.Load<Entity>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.World:
                    continue;
                case Constants.FileNameExtensions.Texture:
                    actor = game.AssetContentManager.Load<Texture>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Sprite:
                    actor = game.AssetContentManager.Load<SpriteData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Animation2d:
                    actor = game.AssetContentManager.Load<Animation2dData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.TileMap:
                    actor = game.AssetContentManager.Load<TileMapData>(assetInfo.Id);
                    break;
                case ".tileset":
                    actor = game.AssetContentManager.Load<TileSetData>(assetInfo.Id);
                    break;
                case Constants.FileNameExtensions.Screen:
                    throw new NotSupportedException("Constants.FileNameExtensions.Screen");
                default:
                    Logs.WriteWarning($"Object '{assetInfo.FileName}' skipped");
                    continue;
            }

            if (actor != null)
                AssetSaver.SaveAsset(actor.FileName, actor);
        }

        AssetCatalog.Save();
    }
}