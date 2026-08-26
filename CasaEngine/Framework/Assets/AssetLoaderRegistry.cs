using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.UI.MGUI;

using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Assets;

public static class AssetLoaderRegistry
{
    public static void RegisterLoaders(AssetContentManager assetContentManager)
    {
        assetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        assetContentManager.RegisterAssetLoader(typeof(XnaTextureCube), new TextureCubeLoader());
        assetContentManager.RegisterAssetLoader(typeof(Effect), new EffectLoader());
        assetContentManager.RegisterAssetLoader(typeof(IAudioClip), new SoundEffectLoader());
        assetContentManager.RegisterAssetLoader(typeof(RiggedModel), new ModelLoader());
        assetContentManager.RegisterAssetLoader(typeof(SkeletonDefinition), new SkeletonDefinitionLoader());
        assetContentManager.RegisterAssetLoader(typeof(AnimationClip), new AnimationClipLoader());
        assetContentManager.RegisterAssetLoader(typeof(RetargetProfile), new RetargetProfileLoader());
        //assetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());

        assetContentManager.RegisterAssetLoader(typeof(ObjectBase), new AssetLoader<ObjectBase>());
        assetContentManager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        assetContentManager.RegisterAssetLoader(typeof(SkinnedMesh), new AssetLoader<SkinnedMesh>());
        assetContentManager.RegisterAssetLoader(typeof(StaticModel), new AssetLoader<StaticModel>());
        assetContentManager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        assetContentManager.RegisterAssetLoader(typeof(SpriteData), new AssetLoader<SpriteData>());
        assetContentManager.RegisterAssetLoader(typeof(Texture), new AssetLoader<Texture>());
        assetContentManager.RegisterAssetLoader(typeof(TileMapData), new AssetLoader<TileMapData>());
        assetContentManager.RegisterAssetLoader(typeof(TileSetData), new AssetLoader<TileSetData>());
        assetContentManager.RegisterAssetLoader(typeof(UIScreenAsset), new AssetLoader<UIScreenAsset>());
        assetContentManager.RegisterAssetLoader(typeof(Scene.World.World), new AssetLoader<Scene.World.World>());
        assetContentManager.RegisterAssetLoader(typeof(PlayerStartupSettings), new AssetLoader<PlayerStartupSettings>());
        assetContentManager.RegisterAssetLoader(typeof(GameplayModeAsset), new AssetLoader<GameplayModeAsset>());
        assetContentManager.RegisterAssetLoader(typeof(ButtonsMapping), new AssetLoader<ButtonsMapping>());
        assetContentManager.RegisterAssetLoader(typeof(EnvironmentAsset), new EnvironmentAssetLoader());
        assetContentManager.RegisterAssetLoader(typeof(MaterialAsset), new MaterialAssetLoader());
        assetContentManager.RegisterAssetLoader(typeof(ParticleEffectAsset), new ParticleEffectAssetLoader());
        assetContentManager.RegisterAssetLoader(typeof(CutsceneAsset), new CutsceneAssetLoader());
        assetContentManager.RegisterAssetLoader(typeof(DialogueAsset), new DialogueAssetLoader());
    }
}