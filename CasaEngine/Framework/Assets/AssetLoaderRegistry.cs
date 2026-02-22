using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework.Graphics;
using Cursor = CasaEngine.Framework.GUI.Neoforce.Cursor;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Assets;

public static class AssetLoaderRegistry
{
    public static void RegisterLoaders(AssetContentManager assetContentManager)
    {
        assetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        assetContentManager.RegisterAssetLoader(typeof(Effect), new EffectLoader());
        assetContentManager.RegisterAssetLoader(typeof(RiggedModel), new ModelLoader());
        //assetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
        assetContentManager.RegisterAssetLoader(typeof(Cursor), new NeoForceCursorLoader());

        assetContentManager.RegisterAssetLoader(typeof(ObjectBase), new AssetLoader<ObjectBase>());
        assetContentManager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        assetContentManager.RegisterAssetLoader(typeof(Pawn), new AssetLoader<Pawn>());
        assetContentManager.RegisterAssetLoader(typeof(SkinnedMesh), new AssetLoader<SkinnedMesh>());
        assetContentManager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        assetContentManager.RegisterAssetLoader(typeof(SpriteData), new AssetLoader<SpriteData>());
        assetContentManager.RegisterAssetLoader(typeof(Texture), new AssetLoader<Texture>());
        assetContentManager.RegisterAssetLoader(typeof(TileMapData), new AssetLoader<TileMapData>());
        assetContentManager.RegisterAssetLoader(typeof(TileSetData), new AssetLoader<TileSetData>());
        assetContentManager.RegisterAssetLoader(typeof(ScreenGui), new AssetLoader<ScreenGui>());
        assetContentManager.RegisterAssetLoader(typeof(World.World), new AssetLoader<World.World>());
        assetContentManager.RegisterAssetLoader(typeof(GameMode), new AssetLoader<GameMode>());
    }
}