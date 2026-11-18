using CasaEngine.Core.Time;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>A MoveTo that cannot run, or runs out of time, fails the play instead of throwing.</summary>
public sealed class MoveToFailureCutsceneActionTests
{
    [Fact]
    public void MoveToAnEntityAbsentFromTheWorld_FailsThePlay()
    {
        var world = new World();

        List<CutsceneEndedEventArgs> ended = PlayUntilEnded(world, new MoveToCutsceneActionData { EntityName = "Nobody", Destination = new Vector3(1f, 0f, 0f) });

        Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("Nobody", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void MoveToAnEntityWithoutCharacterController_FailsThePlay()
    {
        var world = new World();
        world.AddEntity(new Entity { Name = "Rock", RootComponent = new TestSceneComponent() });
        world.Update(FrameTime.FromElapsedTime(0f));

        List<CutsceneEndedEventArgs> ended = PlayUntilEnded(world, new MoveToCutsceneActionData { EntityName = "Rock", Destination = new Vector3(1f, 0f, 0f) });

        Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("CharacterControllerComponent", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void MoveToThatRunsOutOfTime_FailsThePlay()
    {
        var world = new World();
        var entity = new Entity { Name = "Hero", RootComponent = new TestSceneComponent() };
        entity.AddComponent(new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 1f,
                Acceleration = 100f,
                Deceleration = 100f,
                Gravity = 0f,
            }
        });
        world.AddEntity(entity);
        world.Update(FrameTime.FromElapsedTime(0f));

        List<CutsceneEndedEventArgs> ended = PlayUntilEnded(world, new MoveToCutsceneActionData
        {
            EntityName = "Hero",
            Destination = new Vector3(1000f, 0f, 0f),
            StoppingDistance = 0.05f,
            TimeoutSeconds = 0.3f,
        });

        Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("timed out", snapshot.ActiveActionStopReason);
    }

    private static List<CutsceneEndedEventArgs> PlayUntilEnded(World world, CutsceneActionData root)
    {
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "MoveFailure", RootAction = root });

        for (int frameIndex = 1; frameIndex <= 30 && ended.Count == 0; frameIndex++)
        {
            world.Update(FrameTime.FromElapsedTime(0.1f, frameIndex));
        }

        return ended;
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}
