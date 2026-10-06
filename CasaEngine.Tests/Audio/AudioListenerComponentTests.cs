using CasaEngine.EditorServices;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.Audio.Spatial;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using NumericsVector3 = System.Numerics.Vector3;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// <see cref="AudioListenerComponent"/> and the pose reading of <see cref="AudioScenePose"/> (plan T9.8, decisions D8, P35, P40),
/// on the fallback backend: what the listener did shows in the gain the voices of a spatial sound take.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioListenerComponentTests
{
    private static readonly NumericsVector3 Ahead4 = new(0f, 0f, -4f);

    // ---- pose (a) -----------------------------------------------------------

    [Fact]
    public void ThePoseUnderARotatedParent_ComposesTheRotation_WhereSceneComponentPositionDoesNot()
    {
        var parent = new TransformComponent
        {
            LocalPosition = new Vector3(10f, 0f, 0f),
            LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2),
        };
        var child = new TransformComponent { LocalPosition = new Vector3(1f, 0f, 0f) };
        parent.AddChildComponent(child);

        AudioScenePose.GetWorldPose(child, out var position, out var forward, out var up);

        // The child, one unit along the local X of a parent turned by 90 degrees around Y, is one unit along -Z.
        Assert.Equal(10f, position.X, 4);
        Assert.Equal(0f, position.Y, 4);
        Assert.Equal(-1f, position.Z, 4);
        Assert.Equal(-1f, forward.X, 4);
        Assert.Equal(0f, forward.Y, 4);
        Assert.Equal(0f, forward.Z, 4);
        Assert.Equal(0f, up.X, 4);
        Assert.Equal(1f, up.Y, 4);
        Assert.Equal(0f, up.Z, 4);

        // SceneComponent.Position adds the local positions without composing the rotation: (11, 0, 0), a different point.
        Assert.Equal(new Vector3(11f, 0f, 0f), child.Position);
    }

    [Fact]
    public void ANonUniformScaleOfTheParent_DoesNotChangeThePose()
    {
        var parent = new TransformComponent
        {
            LocalPosition = new Vector3(10f, 0f, 0f),
            LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2),
            LocalScale = new Vector3(2f, 3f, 4f),
        };
        var child = new TransformComponent { LocalPosition = new Vector3(1f, 0f, 0f), LocalScale = new Vector3(5f, 1f, 7f) };
        parent.AddChildComponent(child);

        AudioScenePose.GetWorldPose(child, out var position, out var forward, out _);

        Assert.Equal(10f, position.X, 4);
        Assert.Equal(-1f, position.Z, 4);
        Assert.Equal(-1f, forward.X, 4);
        Assert.Equal(1f, forward.Length(), 4);
    }

    [Fact]
    public void AComponentAtEntityLevel_KeepsItsOwnMatrix_WhateverTheEntityRoot()
    {
        var entity = new Entity { RootComponent = new TransformComponent { LocalPosition = new Vector3(100f, 0f, 0f) } };
        var component = new TransformComponent { LocalPosition = new Vector3(1f, 2f, 3f) };
        entity.AddComponent(component);

        Assert.Equal(new NumericsVector3(1f, 2f, 3f), AudioScenePose.GetWorldPosition(component));
    }

    [Fact]
    public void AComponentInAChildEntity_GetsTheRootOfTheParentEntityTwiceWhenItHasAParent_AsTheEngineComputesIt()
    {
        // Documented existing behaviour of SceneComponent.WorldMatrix* (plan O33), inherited by the audio and not corrected.
        var parentEntity = new Entity { RootComponent = new TransformComponent { LocalPosition = new Vector3(10f, 0f, 0f) } };
        var childEntity = new Entity { RootComponent = new TransformComponent() };
        var withParent = new TransformComponent { LocalPosition = new Vector3(1f, 0f, 0f) };
        childEntity.RootComponent.AddChildComponent(withParent);
        parentEntity.AddChild(childEntity);

        // The root of the child entity itself: the parent root once.
        Assert.Equal(new NumericsVector3(10f, 0f, 0f), AudioScenePose.GetWorldPosition(childEntity.RootComponent));

        // A component with a parent in the child entity: 1 + 10 (through its parent) + 10 (again, from its own owner).
        Assert.Equal(new NumericsVector3(21f, 0f, 0f), AudioScenePose.GetWorldPosition(withParent));
    }

    // ---- registration (f) ---------------------------------------------------

    [Fact]
    public void AttachedAndBound_RegistersItsPoseAtOnce()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent { LocalPosition = new Vector3(0f, 0f, -2f) };
        var entity = new Entity { RootComponent = listener };

        Assert.False(rig.Service.HasListener);

        listener.BindServiceForTests(rig.Service);

        Assert.True(rig.Service.HasListener);
        Assert.True(listener.IsRegistered);

        // The listener is two units ahead of the origin: a sound at (0, 0, -4) is two units away, half the gain.
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead4);
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f);
        GC.KeepAlive(entity);
    }

    [Fact]
    public void ThePoseOfAListenerBoundBeforeBeingAttached_IsRegisteredWhenTheWorldIsEntered()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent();
        listener.BindServiceForTests(rig.Service);

        Assert.False(rig.Service.HasListener);

        var entity = new Entity { RootComponent = listener };
        entity.InitializeWithWorld(new World());

        Assert.True(rig.Service.HasListener);
    }

    [Fact]
    public void Update_PushesThePoseOnlyWhenItChanged()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent { LocalPosition = new Vector3(0f, 0f, 0f) };
        var entity = new Entity { RootComponent = listener };
        listener.BindServiceForTests(rig.Service);

        var voice = rig.PlayAt(rig.CreateAsset(), Ahead4);
        rig.Tick(2);
        rig.Verify(voice, 0.25f, 0f);

        // A sentinel pose written behind the back of the component: a still listener must not overwrite it.
        rig.Service.SetListener(listener, AudioListenerPose.Create(new NumericsVector3(0f, 0f, -2f), new NumericsVector3(0f, 0f, -1f), NumericsVector3.UnitY));
        entity.Update(0.01f);
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f);

        // Once it moves, the pose of the component replaces the sentinel.
        listener.LocalPosition = new Vector3(0f, 0f, -3f);
        entity.Update(0.01f);
        rig.Tick(2);
        rig.Verify(voice, 1f, 0f);
    }

    [Fact]
    public void ThePoseFollowsTheParentRotation()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var root = new TransformComponent { LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -MathHelper.PiOver2) };
        var listener = new AudioListenerComponent();
        root.AddChildComponent(listener);
        var entity = new Entity { RootComponent = root };
        listener.BindServiceForTests(rig.Service);

        // Turned by -90 degrees around Y, the listener looks along +X and its right is +Z: a sound at +X is dead ahead.
        var voice = rig.PlayAt(rig.CreateAsset(), new NumericsVector3(2f, 0f, 0f));
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f);

        rig.Service.SetVoicePosition(voice, new NumericsVector3(0f, 0f, 2f));
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 1f);
        GC.KeepAlive(entity);
    }

    [Fact]
    public void Detach_RemovesTheListener()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent();
        var entity = new Entity { RootComponent = listener };
        listener.BindServiceForTests(rig.Service);

        entity.RootComponent = null;

        Assert.False(rig.Service.HasListener);
        Assert.False(listener.IsRegistered);
    }

    [Fact]
    public void DisablingTheEntity_RemovesTheListener_AndEnablingItRegistersItAgain()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent();
        var entity = new Entity { RootComponent = listener };
        listener.BindServiceForTests(rig.Service);

        entity.IsEnabled = false;

        Assert.False(rig.Service.HasListener);

        // Entity.IsEnabled does not gate Entity.Update: a disabled listener must not come back from there.
        entity.Update(0.01f);
        Assert.False(rig.Service.HasListener);

        entity.IsEnabled = true;

        Assert.True(rig.Service.HasListener);
    }

    [Fact]
    public void DisablingTheParentEntity_RemovesTheListenerOfTheChildEntity()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var listener = new AudioListenerComponent();
        var parentEntity = new Entity();
        var childEntity = new Entity { RootComponent = listener };
        parentEntity.AddChild(childEntity);
        listener.BindServiceForTests(rig.Service);

        Assert.True(rig.Service.HasListener);

        parentEntity.IsEnabled = false;

        Assert.False(rig.Service.HasListener);

        parentEntity.IsEnabled = true;

        Assert.True(rig.Service.HasListener);
    }

    [Fact]
    public void TwoListeners_TheLastRegisteredWins_AndTheRemovalOfItReactivatesThePrevious()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var first = new AudioListenerComponent { LocalPosition = new Vector3(0f, 0f, -2f) };
        var second = new AudioListenerComponent { LocalPosition = new Vector3(0f, 0f, -3f) };
        var firstEntity = new Entity { RootComponent = first };
        var secondEntity = new Entity { RootComponent = second };
        first.BindServiceForTests(rig.Service);
        second.BindServiceForTests(rig.Service);

        var voice = rig.PlayAt(rig.CreateAsset(), Ahead4);
        rig.Tick(2);
        rig.Verify(voice, 1f, 0f);

        secondEntity.IsEnabled = false;
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f);
        GC.KeepAlive(firstEntity);
    }

    [Fact]
    public void AnEmitterInitializedBeforeAnyListener_IsSpatializedFromTheFirstUpdateAfterTheListenerRegisters()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var world = new World();

        var emitter = new SoundEmitterComponent { PlayOnStart = true, LocalPosition = new Vector3(0f, 0f, -4f) };
        emitter.BindServicesForTests(rig.Service, rig.CreateAsset());
        var emitterEntity = new Entity { RootComponent = emitter };
        emitterEntity.InitializeWithWorld(world);

        Assert.True(emitter.IsPlaying);
        var voice = emitter.VoiceForTests;

        // No listener yet: neutral.
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN);

        var listener = new AudioListenerComponent();
        var listenerEntity = new Entity { RootComponent = listener };
        listenerEntity.InitializeWithWorld(world);
        listener.BindServiceForTests(rig.Service);

        rig.Tick(2);
        rig.Verify(voice, 0.25f, 0f);
    }

    [Fact]
    public void AListenerEntityWithAStaticModel_StillTicks()
    {
        var plain = new Entity { RootComponent = new StaticModelComponent() };
        Assert.False(plain.GetResolvedPolicies().ShouldUpdateThisFrame);

        var withListener = new Entity { RootComponent = new StaticModelComponent() };
        withListener.AddComponent(new AudioListenerComponent());

        var resolved = withListener.GetResolvedPolicies();

        Assert.True(resolved.ShouldUpdateThisFrame);
        Assert.Equal(TickPolicy.EveryFrame, withListener.GetEffectivePolicySet().TickPolicy);
    }

    // ---- bounding box (g) -----------------------------------------------------

    [Fact]
    public void TheBoundingBoxOfAnEntityWithoutAnAudioComponent_IsTheOneOfItsRoot()
    {
        var entity = new Entity { RootComponent = new TransformComponent { LocalPosition = new Vector3(3f, 0f, 0f) } };

        var box = entity.GetBoundingBox();

        Assert.Equal(new Vector3(2.5f, -0.5f, -0.5f), box.Min);
        Assert.Equal(new Vector3(3.5f, 0.5f, 0.5f), box.Max);
    }

    [Fact]
    public void AnEntityLevelAudioComponent_AddsOnlyASmallBoxAroundItsOwnPose()
    {
        var entity = new Entity { RootComponent = new TransformComponent { LocalPosition = new Vector3(3f, 0f, 0f) } };
        var listener = new AudioListenerComponent { LocalPosition = new Vector3(3f, 0f, 0f) };
        entity.AddComponent(listener);

        var box = entity.GetBoundingBox();

        // Inside the box of the root: nothing stretched.
        Assert.Equal(new Vector3(2.5f, -0.5f, -0.5f), box.Min);
        Assert.Equal(new Vector3(3.5f, 0.5f, 0.5f), box.Max);

        var own = listener.GetBoundingBox();
        Assert.Equal(new Vector3(2.9f, -0.1f, -0.1f), own.Min);
        Assert.Equal(new Vector3(3.1f, 0.1f, 0.1f), own.Max);
    }

    [Fact]
    public void AFarChildEmitter_IsMeasuredWithItsSmallBox()
    {
        var root = new TransformComponent();
        var emitter = new SoundEmitterComponent { LocalPosition = new Vector3(100f, 0f, 0f) };
        root.AddChildComponent(emitter);
        var entity = new Entity { RootComponent = root };

        var box = entity.GetBoundingBox();

        // The box of the transform component is the union of its drawable descendants: the emitter, small, at x = 100.
        Assert.Equal(99.9f, box.Min.X, 4);
        Assert.Equal(100.1f, box.Max.X, 4);
        Assert.Equal(0.1f, box.Max.Y, 4);
    }

    // ---- clone, save, load (c, d) --------------------------------------------

    [Fact]
    public void Clone_CopiesTheTransform()
    {
        var original = new AudioListenerComponent { LocalPosition = new Vector3(1f, 2f, 3f) };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(new Vector3(1f, 2f, 3f), clone.LocalPosition);
        Assert.False(clone.IsRegistered);
    }

    [Fact]
    public void SaveThenLoad_KeepsAListenerAsRoot()
    {
        var entity = new Entity { Name = "Ears" };
        entity.RootComponent = new AudioListenerComponent { LocalPosition = new Vector3(4f, 5f, 6f) };

        var reloaded = Reload(entity);

        var listener = Assert.IsType<AudioListenerComponent>(reloaded.RootComponent);
        Assert.Equal(new Vector3(4f, 5f, 6f), listener.LocalPosition);
    }

    [Fact]
    public void SaveThenLoad_KeepsAListenerAtEntityLevelAndAsAChild()
    {
        var entity = new Entity { Name = "Ears" };
        var root = new TransformComponent();
        root.AddChildComponent(new AudioListenerComponent { LocalPosition = new Vector3(1f, 0f, 0f) });
        entity.RootComponent = root;
        entity.AddComponent(new AudioListenerComponent { LocalPosition = new Vector3(0f, 2f, 0f) });

        var reloaded = Reload(entity);

        var child = Assert.IsType<AudioListenerComponent>(Assert.Single(reloaded.RootComponent.Children));
        Assert.Equal(new Vector3(1f, 0f, 0f), child.LocalPosition);

        var atEntityLevel = Assert.IsType<AudioListenerComponent>(Assert.Single(reloaded.Components));
        Assert.Equal(new Vector3(0f, 2f, 0f), atEntityLevel.LocalPosition);
    }

    internal static Entity Reload(Entity entity)
    {
        var document = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, document);

        var reloaded = new Entity();
        reloaded.Load(document);
        return reloaded;
    }
}
