using CasaEngine.EditorServices;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundEmitterComponentTests
{
    private static SoundEmitterComponent CreateConfiguredComponent(Guid soundAssetId)
    {
        return new SoundEmitterComponent
        {
            Name = "engine loop",
            SoundAssetId = soundAssetId,
            PlayOnStart = true,
            BusName = AudioBusNames.Voice,
            VolumeOverride = 0.4f,
            PitchOverride = -0.2f,
            IsLoopedOverride = true,
        };
    }

    [Fact]
    public void Defaults_AreNeutral()
    {
        var component = new SoundEmitterComponent();

        Assert.Equal(Guid.Empty, component.SoundAssetId);
        Assert.False(component.PlayOnStart);
        Assert.Equal(string.Empty, component.BusName);
        Assert.Equal(1f, component.VolumeOverride);
        Assert.Equal(0f, component.PitchOverride);
        Assert.Null(component.IsLoopedOverride);
        Assert.False(component.IsPlaying);
    }

    [Theory]
    [InlineData(5f, 1f)]
    [InlineData(-1f, 0f)]
    public void VolumeOverride_IsClamped(float value, float expected)
    {
        Assert.Equal(expected, new SoundEmitterComponent { VolumeOverride = value }.VolumeOverride);
    }

    [Theory]
    [InlineData(5f, 1f)]
    [InlineData(-5f, -1f)]
    public void PitchOverride_IsClamped(float value, float expected)
    {
        Assert.Equal(expected, new SoundEmitterComponent { PitchOverride = value }.PitchOverride);
    }

    [Fact]
    public void NullBusName_BecomesEmpty()
    {
        Assert.Equal(string.Empty, new SoundEmitterComponent { BusName = null }.BusName);
    }

    [Fact]
    public void Clone_CopiesEveryField()
    {
        var soundAssetId = Guid.NewGuid();
        var original = CreateConfiguredComponent(soundAssetId);

        var clone = original.Clone();

        Assert.Equal(soundAssetId, clone.SoundAssetId);
        Assert.True(clone.PlayOnStart);
        Assert.Equal(AudioBusNames.Voice, clone.BusName);
        Assert.Equal(0.4f, clone.VolumeOverride, 4);
        Assert.Equal(-0.2f, clone.PitchOverride, 4);
        Assert.True(clone.IsLoopedOverride);
    }

    [Fact]
    public void SaveThenLoad_KeepsEveryField()
    {
        var soundAssetId = Guid.NewGuid();
        var entity = new Entity { Name = "Radio" };
        entity.AddComponent(CreateConfiguredComponent(soundAssetId));

        var document = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, document);

        var reloaded = new Entity();
        reloaded.Load(document);

        var component = Assert.IsType<SoundEmitterComponent>(
            Assert.Single(reloaded.Components, x => x is SoundEmitterComponent));

        Assert.Equal(soundAssetId, component.SoundAssetId);
        Assert.True(component.PlayOnStart);
        Assert.Equal(AudioBusNames.Voice, component.BusName);
        Assert.Equal(0.4f, component.VolumeOverride, 4);
        Assert.Equal(-0.2f, component.PitchOverride, 4);
        Assert.True(component.IsLoopedOverride);
    }

    [Fact]
    public void SaveThenLoad_KeepsAnUnsetLoopOverride()
    {
        var entity = new Entity { Name = "Radio" };
        entity.AddComponent(new SoundEmitterComponent { SoundAssetId = Guid.NewGuid() });

        var document = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, document);

        var reloaded = new Entity();
        reloaded.Load(document);

        var component = Assert.IsType<SoundEmitterComponent>(
            Assert.Single(reloaded.Components, x => x is SoundEmitterComponent));

        Assert.Null(component.IsLoopedOverride);
    }

    [Fact]
    public void PlayAndStop_AreNoOpsWithoutAWorld()
    {
        var component = new SoundEmitterComponent { SoundAssetId = Guid.NewGuid() };

        // No world means no audio service: this must stay silent, never throw.
        component.Play();
        component.Stop();
        component.StopWithFade(1f);

        Assert.False(component.IsPlaying);
    }

    [Fact]
    public void Clone_CopiesTheTransform()
    {
        var original = CreateConfiguredComponent(Guid.NewGuid());
        original.LocalPosition = new Vector3(1f, 2f, 3f);
        original.LocalScale = new Vector3(2f, 2f, 2f);

        var clone = original.Clone();

        Assert.Equal(new Vector3(1f, 2f, 3f), clone.LocalPosition);
        Assert.Equal(new Vector3(2f, 2f, 2f), clone.LocalScale);
    }

    [Fact]
    public void TheOldForm_WithoutTransformNorChildren_LoadsWithAnIdentityTransform()
    {
        // The form saved before the emitter became a scene component, written by hand.
        var soundAssetId = Guid.NewGuid();
        var oldForm = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "engine loop",
            ["type"] = nameof(SoundEmitterComponent),
            ["sound_asset_id"] = soundAssetId.ToString(),
            ["play_on_start"] = true,
            ["bus_name"] = AudioBusNames.Voice,
            ["volume_override"] = 0.4f,
            ["pitch_override"] = -0.2f,
            ["is_looped_override"] = true,
        };

        var document = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { Name = "Radio" }, document);
        document["components"] = new JArray(oldForm);

        var reloaded = new Entity();
        reloaded.Load(document);

        var component = Assert.IsType<SoundEmitterComponent>(Assert.Single(reloaded.Components));
        Assert.Equal("engine loop", component.Name);
        Assert.Equal(soundAssetId, component.SoundAssetId);
        Assert.True(component.PlayOnStart);
        Assert.Equal(AudioBusNames.Voice, component.BusName);
        Assert.Equal(0.4f, component.VolumeOverride, 4);
        Assert.Equal(-0.2f, component.PitchOverride, 4);
        Assert.True(component.IsLoopedOverride);
        Assert.Equal(Vector3.Zero, component.LocalPosition);
        Assert.Equal(Quaternion.Identity, component.LocalOrientation);
        Assert.Equal(Vector3.One, component.LocalScale);
        Assert.Empty(component.Children);
    }

    [Fact]
    public void AnotherSceneComponent_WithoutATransform_StillThrows()
    {
        var withoutTransform = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "root",
            ["type"] = nameof(TransformComponent),
            ["children_component"] = new JArray(),
        };

        var withoutChildren = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "root",
            ["type"] = nameof(TransformComponent),
            ["local_transform"] = SaveTransform(new TransformComponent()),
        };

        Assert.ThrowsAny<Exception>(() => new TransformComponent().Load(withoutTransform));
        Assert.ThrowsAny<Exception>(() => new TransformComponent().Load(withoutChildren));
    }

    [Fact]
    public void SaveThenLoad_KeepsAnEmitterAsRoot()
    {
        var entity = new Entity { Name = "Radio" };
        entity.RootComponent = CreateConfiguredComponent(Guid.NewGuid());
        entity.RootComponent.LocalPosition = new Vector3(1f, 2f, 3f);

        var reloaded = AudioListenerComponentTests.Reload(entity);

        var component = Assert.IsType<SoundEmitterComponent>(reloaded.RootComponent);
        Assert.Equal(new Vector3(1f, 2f, 3f), component.LocalPosition);
        Assert.Equal(0.4f, component.VolumeOverride, 4);
        Assert.True(component.IsLoopedOverride);
        Assert.Equal(AudioBusNames.Voice, component.BusName);
    }

    [Fact]
    public void SaveThenLoad_KeepsAnEmitterAsAChildOfATransformComponent()
    {
        var entity = new Entity { Name = "Radio" };
        var root = new TransformComponent { LocalPosition = new Vector3(5f, 0f, 0f) };
        var emitter = CreateConfiguredComponent(Guid.NewGuid());
        emitter.LocalPosition = new Vector3(0f, 7f, 0f);
        root.AddChildComponent(emitter);
        entity.RootComponent = root;

        var reloaded = AudioListenerComponentTests.Reload(entity);

        var component = Assert.IsType<SoundEmitterComponent>(Assert.Single(reloaded.RootComponent.Children));
        Assert.Equal(new Vector3(0f, 7f, 0f), component.LocalPosition);
        Assert.Equal(0.4f, component.VolumeOverride, 4);
        Assert.Same(reloaded.RootComponent, component.Parent);
    }

    [Fact]
    public void SaveThenLoad_KeepsTheTransformOfAnEmitterAtEntityLevel()
    {
        var entity = new Entity { Name = "Radio" };
        var emitter = CreateConfiguredComponent(Guid.NewGuid());
        emitter.LocalPosition = new Vector3(0f, 0f, 9f);
        entity.AddComponent(emitter);

        var reloaded = AudioListenerComponentTests.Reload(entity);

        var component = Assert.IsType<SoundEmitterComponent>(Assert.Single(reloaded.Components));
        Assert.Equal(new Vector3(0f, 0f, 9f), component.LocalPosition);
        Assert.Equal(-0.2f, component.PitchOverride, 4);
    }

    private static JObject SaveTransform(SceneComponent component)
    {
        var node = new JObject();
        component.LocalTransform.Save(node);
        return node;
    }
}
