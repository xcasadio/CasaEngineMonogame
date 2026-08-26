using CasaEngine.EditorServices;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
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
}
