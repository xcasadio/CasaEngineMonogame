using CasaEngine.Core.Time;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

public sealed class CutsceneDirectorTests
{
    [Fact]
    public void Play_CompletesSequenceActionsThroughWorldCoroutineManager()
    {
        var world = new World();
        CutsceneAsset asset = CreateSequenceAsset();

        world.CutsceneDirector.Play(asset);

        Assert.True(world.CutsceneDirector.IsPlaying);

        int completedFrame = AdvanceUntilNotPlaying(world, 10);

        Assert.InRange(completedFrame, 1, 10);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Completed, snapshot.State);
        Assert.False(world.CutsceneDirector.IsPlaying);
    }

    [Fact]
    public void Play_CompletesParallelActionsThroughWorldCoroutineManager()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset();

        world.CutsceneDirector.Play(asset);
        int completedFrame = AdvanceUntilNotPlaying(world, 12);

        Assert.InRange(completedFrame, 1, 12);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Stop_StopsActiveCutsceneAndOwnedParallelChildren()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));
        world.CoroutineManager.Update(Context(2));

        Assert.True(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines.Count > 0);

        world.CutsceneDirector.Stop();
        world.CoroutineManager.Update(Context(3));

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Stopped, snapshot.State);
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Empty(snapshot.ActiveCoroutines);
    }

    [Fact]
    public void Stop_CancelsCharacterMotionMoveToAndRestoresControlMode()
    {
        var world = new World();
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "MoveHero",
            RootAction = new MoveToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(10f, 0f, 0f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);

        Assert.Null(entity.GetComponent<CharacterControllerMoveToDriverComponent>());
        Assert.Equal(CharacterControlMode.Cutscene, controller.ControlMode);

        world.CutsceneDirector.Stop();

        Assert.Null(entity.GetComponent<CharacterControllerMoveToDriverComponent>());
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Play_InvalidAssetDoesNotStartCoroutine()
    {
        var world = new World();
        var asset = new CutsceneAsset
        {
            Name = "Invalid",
            RootAction = new WaitCutsceneActionData { Seconds = -1f }
        };

        world.CutsceneDirector.Play(asset);

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Invalid, snapshot.State);
        Assert.Contains(snapshot.ValidationMessages, message => message.Severity == CutsceneValidationSeverity.Error);
    }

    [Fact]
    public void Play_MoveToActionTakesAndRestoresCharacterControl()
    {
        var world = new World();
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "MoveHero",
            RootAction = new MoveToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(1f, 0f, 0f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);

        Assert.Null(entity.GetComponent<CharacterControllerMoveToDriverComponent>());
        Assert.Equal(CharacterControlMode.Cutscene, controller.ControlMode);

        for (int frameIndex = 2; frameIndex <= 10; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);

            if (!world.CutsceneDirector.IsPlaying)
            {
                break;
            }
        }

        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
    }

    [Fact]
    public void Play_MoveToActionAdvancesPositionInRuntimeUpdateOrder()
    {
        var world = new World();
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "MoveHeroRuntimeOrder",
            RootAction = new MoveToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(3f, 0f, 0f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);

        float startX = entity.RootComponent!.Position.X;
        float firstFrameX = startX;
        float furthestX = startX;

        for (int frameIndex = 1; frameIndex <= 20; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);

            if (frameIndex == 1)
            {
                firstFrameX = entity.RootComponent.Position.X;
            }

            furthestX = Math.Max(furthestX, entity.RootComponent.Position.X);

            if (!world.CutsceneDirector.IsPlaying)
            {
                break;
            }
        }

        Assert.True(firstFrameX > startX, $"Expected entity to start moving during the same runtime frame, but first-frame X stayed at {firstFrameX:F3}.");
        Assert.True(furthestX > startX + 0.5f, $"Expected entity to move forward in runtime order, but X stayed at {furthestX:F3}.");
        Assert.InRange(entity.RootComponent.Position.X, 2.95f, 3.05f);
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
    }

    [Fact]
    public void Play_MoveToActionCanReplayAfterCompletion()
    {
        var world = new World();
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "ReplayMoveHero",
            RootAction = new MoveToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2f, 0f, 0f),
                StoppingDistance = 0.05f,
            }
        };

        PlayUntilStopped(world, entity, asset, maxFrameCount: 20);
        Assert.InRange(entity.RootComponent!.Position.X, 1.95f, 2.05f);

        entity.RootComponent.Position = Vector3.Zero;
        controller.SetControlMode(CharacterControlMode.Player);
        controller.Stop();

        PlayUntilStopped(world, entity, asset, maxFrameCount: 20);

        Assert.InRange(entity.RootComponent.Position.X, 1.95f, 2.05f);
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
    }

    [Fact]
    public void Play_MoveToActionCanRestartWhilePreviousMoveIsStillActive()
    {
        var world = new World();
        Entity entity = CreateControlledEntity(out CharacterControllerComponent controller);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "RestartActiveMoveHero",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 0.15f },
                    new MoveToCutsceneActionData
                    {
                        EntityName = "Hero",
                        Destination = new Vector3(5f, 0f, 0f),
                        StoppingDistance = 0.05f,
                    }
                }
            }
        };

        world.CutsceneDirector.Play(asset);

        for (int frameIndex = 1; frameIndex <= 8; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.True(entity.RootComponent!.Position.X > 0.5f, $"Expected the first playthrough to be in motion before restart, but X was {entity.RootComponent.Position.X:F3}.");

        world.CutsceneDirector.Stop();
        entity.RootComponent.Position = Vector3.Zero;
        controller.SetControlMode(CharacterControlMode.Player);
        controller.Stop();
        world.CutsceneDirector.Play(asset);

        float furthestReplayX = entity.RootComponent.Position.X;
        for (int frameIndex = 9; frameIndex <= 30; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
            furthestReplayX = Math.Max(furthestReplayX, entity.RootComponent.Position.X);

            if (!world.CutsceneDirector.IsPlaying)
            {
                break;
            }
        }

        Assert.True(furthestReplayX > 1f, $"Expected restarted MoveTo to advance after an active restart, but replay X stayed at {furthestReplayX:F3}.");
        Assert.InRange(entity.RootComponent.Position.X, 4.95f, 5.05f);
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
    }

    [Fact]
    public void WorldClearStopsCutsceneDirector()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));

        world.Clear();

        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    private static int AdvanceUntilNotPlaying(World world, int maxFrameCount)
    {
        for (int frameIndex = 1; frameIndex <= maxFrameCount; frameIndex++)
        {
            world.CoroutineManager.Update(Context(frameIndex));
            if (!world.CutsceneDirector.IsPlaying)
            {
                return frameIndex;
            }
        }

        return maxFrameCount;
    }

    private static CoroutineUpdateContext Context(long frameIndex)
    {
        return new CoroutineUpdateContext(0.1f, 0.1f, 1f, frameIndex);
    }

    private static void AddEntityToWorld(World world, Entity entity)
    {
        world.AddEntity(entity);
        world.Update(FrameTime.FromElapsedTime(0f));
    }

    private static void AdvanceWorld(World world, long frameIndex)
    {
        world.Update(FrameTime.FromElapsedTime(0.1f, frameIndex));
    }

    private static void PlayUntilStopped(World world, Entity entity, CutsceneAsset asset, int maxFrameCount)
    {
        world.CutsceneDirector.Play(asset);

        for (int frameIndex = 1; frameIndex <= maxFrameCount; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);

            if (!world.CutsceneDirector.IsPlaying)
            {
                return;
            }
        }

        Assert.False(world.CutsceneDirector.IsPlaying, $"Expected cutscene '{asset.Name}' to complete within {maxFrameCount} frames.");
    }

    private static CutsceneAsset CreateSequenceAsset()
    {
        return new CutsceneAsset
        {
            Name = "Sequence",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 0.2f },
                    new WaitCutsceneActionData { Seconds = 0.1f }
                }
            }
        };
    }

    private static CutsceneAsset CreateParallelAsset(float firstWait = 0.2f, float secondWait = 0.3f)
    {
        return new CutsceneAsset
        {
            Name = "Parallel",
            RootAction = new ParallelCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = firstWait },
                    new WaitCutsceneActionData { Seconds = secondWait }
                }
            }
        };
    }

    private static Entity CreateControlledEntity(out CharacterControllerComponent controller)
    {
        var entity = new Entity
        {
            Name = "Hero",
            RootComponent = new TestSceneComponent(),
        };

        controller = new CharacterControllerComponent
        {
            Settings = new CharacterControllerSettings
            {
                MaxHorizontalSpeed = 10f,
                Acceleration = 100f,
                Deceleration = 100f,
                Gravity = 0f,
            }
        };
        entity.AddComponent(controller);
        return entity;
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}