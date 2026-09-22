using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// ADR-0037 (T3.3): a cutscene's PlaySound/PlayMusic actions acquire their <see cref="SoundAsset"/> only
/// for the duration of the action - <c>CutsceneActionCoroutineFactory.WithSoundAsset</c>, private, exists
/// solely for this - and give the handle back once the synchronous call that reads it (starting the
/// voice or the track) returns.
/// </summary>
public class CutsceneActionCoroutineFactorySoundAssetHandleTests
{
    private static readonly Guid SoundAssetId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private sealed class SoundAssetLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new SoundAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager(out SoundAssetLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SoundAssetId] = new AssetInfo(SoundAssetId) { Name = "hit", FileName = "hit.sound" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new SoundAssetLoader();
        manager.RegisterAssetLoader(typeof(SoundAsset), loader);
        return manager;
    }

    private static World NewWorldWithGame(AssetContentManager assetContentManager)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokeWithSoundAsset(World world, Guid soundAssetId, Action<SoundAsset> useAsset)
    {
        var method = typeof(CutsceneActionCoroutineFactory).GetMethod(
            "WithSoundAsset", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method!.Invoke(null, new object[] { world, soundAssetId, useAsset });
    }

    [Fact]
    public void WithSoundAsset_HoldsTheHandleOnlyForTheCallback_ThenReleasesIt()
    {
        var manager = NewManager(out var loader);
        var world = NewWorldWithGame(manager);
        var callbackCount = 0;

        InvokeWithSoundAsset(world, SoundAssetId, asset =>
        {
            callbackCount++;
            Assert.NotNull(asset);
            // Still held while the callback runs: nothing to free yet.
            Assert.Equal(0, manager.CollectUnreferenced());
        });

        Assert.Equal(1, callbackCount);
        Assert.Equal(1, loader.Loads);
        // Given back once WithSoundAsset returns: freeable at once.
        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void WithSoundAsset_WithEmptyAssetId_NeverInvokesTheCallback()
    {
        var manager = NewManager(out var loader);
        var world = NewWorldWithGame(manager);
        var callbackCount = 0;

        InvokeWithSoundAsset(world, Guid.Empty, _ => callbackCount++);

        Assert.Equal(0, callbackCount);
        Assert.Equal(0, loader.Loads);
    }
}
