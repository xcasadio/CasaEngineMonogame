using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.Audio.Spatial;
using Microsoft.Xna.Framework;
using Xunit;
using NumericsVector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// The spatial behaviour of <see cref="SoundEmitterComponent"/> as a scene component (plan T9.8, decisions D13, P38, P40, P41),
/// bound to a service on the fallback backend through <c>BindServicesForTests</c>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class SoundEmitterComponentSpatialTests
{
    private static SoundEmitterComponent CreateBoundEmitter(SpatialRig rig, SoundAsset asset, Vector3 position, out Entity entity)
    {
        var emitter = new SoundEmitterComponent { LocalPosition = position };
        entity = new Entity { RootComponent = emitter };
        emitter.BindServicesForTests(rig.Service, asset);
        return emitter;
    }

    [Fact]
    public void ASpatialSound_IsCreatedWithTheDistanceGain_OnceAListenerExists()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);
        var emitter = CreateBoundEmitter(rig, rig.CreateAsset(), new Vector3(0f, 0f, -2f), out _);

        emitter.Play();
        rig.Tick(2);

        Assert.True(emitter.IsPlaying);
        rig.Verify(emitter.VoiceForTests, 0.5f, 0f);
    }

    [Fact]
    public void ThePoseUnderARotatedParent_IsTheOneTheVoiceIsPlacedAt()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);

        var parent = new TransformComponent
        {
            LocalPosition = new Vector3(0f, 0f, -2f),
            LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2),
        };
        var emitter = new SoundEmitterComponent { LocalPosition = new Vector3(1f, 0f, 0f) };
        parent.AddChildComponent(emitter);
        var entity = new Entity { RootComponent = parent };
        emitter.BindServicesForTests(rig.Service, rig.CreateAsset());

        emitter.Play();
        rig.Tick(2);

        // World position (0, 0, -3): three units ahead, gain 1/3, centred (the sum of the local positions would be (1, 0, -2)).
        rig.Verify(emitter.VoiceForTests, 1f / 3f, 0f);
        GC.KeepAlive(entity);
    }

    [Fact]
    public void TheVoiceFollowsTheEntityWhenItMoves()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);
        var emitter = CreateBoundEmitter(rig, rig.CreateAsset(), new Vector3(0f, 0f, -2f), out var entity);
        emitter.Play();
        rig.Tick(2);
        rig.Verify(emitter.VoiceForTests, 0.5f, 0f);

        emitter.LocalPosition = new Vector3(0f, 0f, -4f);
        entity.Update(0.01f);
        rig.Tick(2);

        rig.Verify(emitter.VoiceForTests, 0.25f, 0f);
    }

    [Fact]
    public void AStillEmitter_SendsNothingToTheBackend()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);
        var emitter = CreateBoundEmitter(rig, rig.CreateAsset(), new Vector3(0f, 0f, -2f), out var entity);
        emitter.Play();
        rig.Tick(2);

        var setParameters = rig.Fake.SetParametersCount;
        var setVolume = rig.Fake.SetVolumeCount;

        for (var i = 0; i < 5; i++)
        {
            entity.Update(0.01f);
            rig.Tick();
        }

        Assert.Equal(setParameters, rig.Fake.SetParametersCount);
        Assert.Equal(setVolume, rig.Fake.SetVolumeCount);
    }

    [Fact]
    public void ANeutralSpatialVoice_IsSpatializedWhenTheListenerComesLater()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var emitter = CreateBoundEmitter(rig, rig.CreateAsset(), new Vector3(0f, 0f, -4f), out _);

        emitter.Play();
        rig.Tick(2);
        rig.Verify(emitter.VoiceForTests, 1f, float.NaN);

        rig.SetListener(NumericsVector3.Zero);
        rig.Tick(2);
        rig.Verify(emitter.VoiceForTests, 0.25f, 0f);
    }

    [Fact]
    public void Detach_StopsTheVoice()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);
        var emitter = CreateBoundEmitter(rig, rig.CreateAsset(), new Vector3(0f, 0f, -2f), out var entity);
        emitter.Play();
        rig.Tick(2);
        var voice = emitter.VoiceForTests;
        Assert.True(rig.Service.IsAlive(voice));

        entity.RootComponent = null;
        rig.Tick();

        Assert.False(rig.Service.IsAlive(voice));
        Assert.False(emitter.IsPlaying);
    }

    [Fact]
    public void AnAssetWithoutASpatialMode_UsesThePlainPath_WhateverThePositionOrTheListener()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(NumericsVector3.Zero);
        var asset = rig.CreateAsset(AudioSpatialMode.None);
        var emitter = CreateBoundEmitter(rig, asset, new Vector3(0f, 0f, -50f), out var entity);

        emitter.Play();
        rig.Tick(2);

        // Full gain: the sound is not attenuated by the distance of its emitter.
        rig.Verify(emitter.VoiceForTests, 1f, float.NaN);

        emitter.LocalPosition = new Vector3(0f, 0f, -80f);
        entity.Update(0.01f);
        rig.Tick(2);

        rig.Verify(emitter.VoiceForTests, 1f, float.NaN);
    }
}
