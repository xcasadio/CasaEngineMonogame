using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Component = CasaEngine.Framework.Scene.Entities.Components.SoundEmitterComponent;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// The variation draw of <see cref="AudioService.PlaySound(SoundAsset, in SoundPlaybackOverrides, object)"/>:
/// it applies on top of the overrides. Process-global state (asset manager, game fields) is touched by the
/// real emitter and cutscene paths, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceVariationTests
{
    private static readonly Guid SoundAssetId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static AudioService CreateService(
        out FakeAudioBackend backend,
        out FakeAudioClipProvider provider,
        int voiceCapacity = 8)
    {
        backend = new FakeAudioBackend(voiceCapacity);
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateAsset(FakeAudioClipProvider provider, string name = "step")
    {
        return new SoundAsset
        {
            Name = name,
            AudioFileAssetId = provider.Register(new FakeAudioClip(name)),
        };
    }

    [Fact]
    public void AssetWithoutVariation_KeepsTheParameters_AndNeverCallsTheRandom()
    {
        var service = CreateService(out var backend, out var provider);
        var random = new SoundVariationTests.ScriptedRandom();
        service.VariationRandom = random;
        var asset = CreateAsset(provider);
        asset.Volume = 0.6f;
        asset.Pitch = 0.2f;

        var voice = service.PlaySound(asset);

        var applied = backend.GetParameters(voice);
        Assert.Equal(0.6f, applied.Volume, 5);
        Assert.Equal(0.2f, applied.Pitch, 5);
        Assert.Empty(random.Calls);
    }

    [Fact]
    public void DegenerateVolumeFactor_ScalesTheAssetVolume()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.8f;
        asset.VariationVolumeMin = 0.5f;
        asset.VariationVolumeMax = 0.5f;

        var voice = service.PlaySound(asset);

        Assert.Equal(0.4f, backend.GetParameters(voice).Volume, 5);
    }

    [Fact]
    public void DegenerateVolumeFactor_AppliesOnTopOfTheOverride()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.8f;
        asset.VariationVolumeMin = 0.5f;
        asset.VariationVolumeMax = 0.5f;

        var voice = service.PlaySound(asset, new SoundPlaybackOverrides(volume: 0.5f));

        Assert.Equal(0.25f, backend.GetParameters(voice).Volume, 5);
    }

    [Fact]
    public void DegeneratePitchOffset_AppliesOnTopOfTheOverriddenPitch()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.VariationPitchMin = 0.25f;
        asset.VariationPitchMax = 0.25f;

        var voice = service.PlaySound(asset, new SoundPlaybackOverrides(pitch: 0.5f));

        Assert.Equal(0.75f, backend.GetParameters(voice).Pitch, 5);
    }

    [Fact]
    public void ScriptedIndex_PicksTheMatchingClip()
    {
        var service = CreateService(out var backend, out var provider);
        var clips = new[] { new FakeAudioClip("a"), new FakeAudioClip("b"), new FakeAudioClip("c") };
        var asset = new SoundAsset
        {
            Name = "multi",
            AudioFileAssetId = provider.Register(clips[0]),
        };
        asset.VariationAudioFileAssetIds.Add(provider.Register(clips[1]));
        asset.VariationAudioFileAssetIds.Add(provider.Register(clips[2]));
        service.VariationRandom = new SoundVariationTests.ScriptedRandom(indices: new[] { 2 });

        var voice = service.PlaySound(asset);

        Assert.Same(clips[2], backend.GetClip(voice));
    }

    [Fact]
    public void DrawnFileNotFound_ReturnsNone_WithoutFallback()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.VariationAudioFileAssetIds.Add(Guid.NewGuid());
        service.VariationRandom = new SoundVariationTests.ScriptedRandom(indices: new[] { 1 });

        var voice = service.PlaySound(asset);

        Assert.False(voice.IsValid);
        Assert.Equal(0, backend.PlayCount);
    }

    [Fact]
    public void StreamingAsset_IsStillRefused()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        asset.IsStreaming = true;
        asset.VariationAudioFileAssetIds.Add(provider.Register(new FakeAudioClip("b")));
        var random = new SoundVariationTests.ScriptedRandom();
        service.VariationRandom = random;

        var voice = service.PlaySound(asset);

        Assert.False(voice.IsValid);
        Assert.Empty(random.Calls);
    }

    [Fact]
    public void NullVariationRandom_FallsBackToTheDefault()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        asset.VariationAudioFileAssetIds.Add(provider.Register(new FakeAudioClip("b")));
        asset.VariationVolumeMin = 0.5f;

        service.VariationRandom = null;

        Assert.Same(Random.Shared, service.VariationRandom);
        Assert.True(service.PlaySound(asset).IsValid);
    }

    // ---- real paths -------------------------------------------------------------------------

    private sealed class ConfiguredSoundAssetLoader : IAssetLoader
    {
        private readonly SoundAsset _asset;

        public ConfiguredSoundAssetLoader(SoundAsset asset) => _asset = asset;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => _asset;

        public bool IsFileSupported(string fileName) => true;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static World CreateWorld(SoundAsset asset, AudioService service)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SoundAssetId] = new AssetInfo(SoundAssetId) { Name = "step", FileName = "step.sound" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        manager.RegisterAssetLoader(typeof(SoundAsset), new ConfiguredSoundAssetLoader(asset));

        var audioSystem = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        SetBackingField(audioSystem, nameof(AudioSystemComponent.Service), service);

        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);
        SetBackingField(game, nameof(CasaEngineGame.AudioSystemComponent), audioSystem);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    [Fact]
    public void EmitterPath_ComposesTheOverridesWithTheDraw()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.8f;
        asset.Pitch = 0.1f;
        asset.VariationVolumeMin = 0.5f;
        asset.VariationVolumeMax = 0.5f;
        asset.VariationPitchMin = 0.25f;
        asset.VariationPitchMax = 0.25f;
        var world = CreateWorld(asset, service);

        var entity = new Entity();
        var component = new Component { SoundAssetId = SoundAssetId, VolumeOverride = 0.5f, PitchOverride = 0.25f };
        entity.AddComponent(component);
        entity.InitializeWithWorld(world);

        component.Play();

        var voiceField = typeof(Component).GetField("_voice", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(voiceField);
        var voice = (AudioVoiceHandle)voiceField!.GetValue(component)!;
        Assert.True(voice.IsValid);

        var applied = backend.GetParameters(voice);
        Assert.Equal(asset.Volume * 0.5f * 0.5f, applied.Volume, 5);
        Assert.Equal(asset.Pitch + 0.25f + 0.25f, applied.Pitch, 5);

        component.Detach();
    }

    [Fact]
    public void CutscenePath_ComposesTheActionVolumeWithTheDraw()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.8f;
        asset.VariationVolumeMin = 0.5f;
        asset.VariationVolumeMax = 0.5f;
        var world = CreateWorld(asset, service);

        var method = typeof(CutsceneActionCoroutineFactory).GetMethod(
            "PlaySound", BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(PlaySoundCutsceneActionData), typeof(World) });
        Assert.NotNull(method);

        method!.Invoke(null, new object[] { new PlaySoundCutsceneActionData { SoundAssetId = SoundAssetId, Volume = 0.5f }, world });

        Assert.Equal(1, backend.PlayCount);
        var applied = backend.GetParameters(new AudioVoiceHandle(0, 0));
        Assert.Equal(asset.Volume * 0.5f * 0.5f, applied.Volume, 5);
    }

    // ---- allocations ------------------------------------------------------------------------

    private static long MeasureAllocation(AudioService service, SoundAsset asset, int iterations)
    {
        for (var i = 0; i < 20; i++)
        {
            service.Stop(service.PlaySound(asset));
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < iterations; i++)
        {
            var voice = service.PlaySound(asset);
            service.Stop(voice);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void PlaySound_WithVariations_DoesNotAllocate()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.VariationAudioFileAssetIds.Add(provider.Register(new FakeAudioClip("b")));
        asset.VariationAudioFileAssetIds.Add(provider.Register(new FakeAudioClip("c")));
        asset.VariationVolumeMin = 0.5f;
        asset.VariationPitchMin = -0.2f;
        asset.VariationPitchMax = 0.2f;
        service.VariationRandom = new Random(1234);

        var allocated = MeasureAllocation(service, asset, 1000);

        Assert.Equal(1020, backend.PlayCount);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void PlaySound_WithoutVariation_AllocationBaseline()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);

        var allocated = MeasureAllocation(service, asset, 1000);

        Assert.Equal(1020, backend.PlayCount);
        Assert.Equal(0, allocated);
    }
}
