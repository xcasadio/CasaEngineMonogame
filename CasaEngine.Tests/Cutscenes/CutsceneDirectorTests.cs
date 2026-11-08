using CasaEngine.Core.Time;
using CasaEngine.Framework.AI.Navigation;
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
    public void Play_NavigateToActionCompletesViaNavigationAgent()
    {
        var world = new World();
        Entity entity = CreateNavigableEntity(out CharacterControllerComponent controller, out NavigationAgentComponent navigationAgent, out _);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "NavigateHero",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
                TimeoutSeconds = 3f,
            }
        };

        world.CutsceneDirector.Play(asset);
        for (int frameIndex = 1; frameIndex <= 20; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
            if (!world.CutsceneDirector.IsPlaying)
            {
                break;
            }
        }

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Completed, snapshot.State);
        Assert.Equal(CutsceneActionTypes.NavigateTo, snapshot.ActiveActionType);
        Assert.Equal("ReachedDestination", snapshot.ActiveActionStopReason);
        Assert.True(navigationAgent.ReachedDestination);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.InRange(entity.RootComponent!.Position.X, 2.45f, 2.55f);
    }

    [Fact]
    public void Play_NavigateToActionFailsWhenNavigationMapIsMissing()
    {
        var world = new World();
        Entity entity = CreateNavigableEntity(out _, out NavigationAgentComponent navigationAgent, out _);
        navigationAgent.NavigationMap = null;
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "NavigateHeroWithoutMap",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.False(world.CutsceneDirector.IsPlaying);
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("NavigationMap", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void Stop_CancelsNavigateToAndRestoresControlMode()
    {
        var world = new World();
        Entity entity = CreateNavigableEntity(out CharacterControllerComponent controller, out NavigationAgentComponent navigationAgent, out CharacterControllerNavigationDriverComponent driver);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "StopNavigateHero",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);

        Assert.True(navigationAgent.HasDestination);
        Assert.True(driver.IsMoving);
        Assert.Equal(CharacterControlMode.AI, controller.ControlMode);

        world.CutsceneDirector.Stop();

        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.False(navigationAgent.HasDestination);
        Assert.False(driver.IsMoving);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(CutsceneRuntimeState.Stopped, snapshot.State);
        Assert.Equal("Cancelled", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void WorldClear_CancelsActiveNavigateTo()
    {
        var world = new World();
        Entity entity = CreateNavigableEntity(out CharacterControllerComponent controller, out NavigationAgentComponent navigationAgent, out CharacterControllerNavigationDriverComponent driver);
        AddEntityToWorld(world, entity);
        var asset = new CutsceneAsset
        {
            Name = "ClearNavigateHero",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            }
        };

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);

        world.Clear();

        Assert.False(navigationAgent.HasDestination);
        Assert.False(driver.IsMoving);
        Assert.Equal(CharacterControlMode.Player, controller.ControlMode);
        Assert.Equal(CutsceneRuntimeState.Stopped, world.CutsceneDirector.GetDebugSnapshot().State);
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

    [Fact]
    public void Ended_RaisesOnceWithCompletedAtNormalEnd()
    {
        var world = new World();
        CutsceneAsset asset = CreateSequenceAsset();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(asset);
        AdvanceUntilNotPlaying(world, 10);
        world.CoroutineManager.Update(Context(11));

        CutsceneEndedEventArgs single = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Completed, single.State);
        Assert.Same(asset, single.Asset);
    }

    [Fact]
    public void Ended_RaisesOnceWithStoppedOnStop()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));
        world.CutsceneDirector.Stop();
        world.CutsceneDirector.Stop();
        world.CoroutineManager.Update(Context(2));

        CutsceneEndedEventArgs single = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Stopped, single.State);
        Assert.Same(asset, single.Asset);
    }

    [Fact]
    public void Ended_RaisesStoppedOnRuntimeSystemsClear()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelAsset(10f, 10f);
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(asset);
        world.CoroutineManager.Update(Context(1));
        world.RuntimeSystems.Clear();

        CutsceneEndedEventArgs single = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Stopped, single.State);
    }

    [Fact]
    public void Ended_RaisesOnceWithFailedWhenNavigateToEntityIsMissing()
    {
        var world = new World();
        var asset = new CutsceneAsset
        {
            Name = "NavigateMissingEntity",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Nobody",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            }
        };
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(asset);
        AdvanceWorld(world, 1);
        AdvanceWorld(world, 2);
        world.CutsceneDirector.Stop();

        CutsceneEndedEventArgs single = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Failed, single.State);
        Assert.Same(asset, single.Asset);
        Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Ended_PlayWhilePlayingRaisesStoppedThenCompleted()
    {
        var world = new World();
        CutsceneAsset first = CreateParallelAsset(10f, 10f);
        CutsceneAsset second = CreateSequenceAsset();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(first);
        world.CoroutineManager.Update(Context(1));
        world.CutsceneDirector.Play(second);

        CutsceneEndedEventArgs afterReplace = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Stopped, afterReplace.State);
        Assert.Same(first, afterReplace.Asset);

        AdvanceUntilNotPlaying(world, 10);

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(second, ended[1].Asset);
    }

    [Fact]
    public void Ended_InvalidAssetRaisesNothing()
    {
        var world = new World();
        var asset = new CutsceneAsset
        {
            Name = "Invalid",
            RootAction = new WaitCutsceneActionData { Seconds = -1f }
        };
        int endedCount = 0;
        world.CutsceneDirector.Ended += (_, _) => endedCount++;

        world.CutsceneDirector.Play(asset);
        world.CutsceneDirector.Stop();

        Assert.Equal(0, endedCount);
    }

    [Fact]
    public void Ended_StopAfterEndRaisesNothingMore()
    {
        var world = new World();
        int endedCount = 0;
        world.CutsceneDirector.Ended += (_, _) => endedCount++;

        world.CutsceneDirector.Play(CreateSequenceAsset());
        AdvanceUntilNotPlaying(world, 10);
        world.CutsceneDirector.Stop();
        world.CoroutineManager.Update(Context(11));

        Assert.Equal(1, endedCount);
    }

    [Fact]
    public void Ended_HandlersSeeDebugSnapshotStateEqualToEventState()
    {
        var world = new World();
        var observed = new List<(CutsceneRuntimeState Event, CutsceneRuntimeState Snapshot)>();
        world.CutsceneDirector.Ended += (_, args) =>
            observed.Add((args.State, world.CutsceneDirector.GetDebugSnapshot().State));

        world.CutsceneDirector.Play(CreateSequenceAsset());
        AdvanceUntilNotPlaying(world, 10);
        world.CutsceneDirector.Play(CreateParallelAsset(10f, 10f));
        world.CoroutineManager.Update(Context(20));
        world.CutsceneDirector.Stop();

        Assert.Equal(2, observed.Count);
        Assert.All(observed, pair => Assert.Equal(pair.Event, pair.Snapshot));
        Assert.Equal(CutsceneRuntimeState.Completed, observed[0].Event);
        Assert.Equal(CutsceneRuntimeState.Stopped, observed[1].Event);
    }

    [Fact]
    public void Ended_PlayFromFailedHandlerKeepsTheNewPlayInControl()
    {
        var world = new World();
        CutsceneAsset next = CreateSequenceAsset();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) =>
        {
            ended.Add(args);
            if (args.State == CutsceneRuntimeState.Failed)
            {
                world.CutsceneDirector.Play(next);
            }
        };

        world.CutsceneDirector.Play(CreateMissingEntityNavigateAsset());
        for (int frameIndex = 1; frameIndex <= 12; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Failed, ended[0].State);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(next, ended[1].Asset);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Completed, snapshot.State);
        Assert.Equal(next.Name, snapshot.AssetName);
    }

    [Fact]
    public void Ended_PlayFromStoppedHandlerDuringReplacementKeepsTheNewestPlay()
    {
        var world = new World();
        CutsceneAsset first = CreateParallelAsset(10f, 10f);
        CutsceneAsset second = CreateParallelAsset(10f, 10f);
        CutsceneAsset third = CreateSequenceAsset();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) =>
        {
            ended.Add(args);
            if (args.State == CutsceneRuntimeState.Stopped && ReferenceEquals(args.Asset, first))
            {
                world.CutsceneDirector.Play(third);
            }
        };

        world.CutsceneDirector.Play(first);
        world.CoroutineManager.Update(Context(1));
        world.CutsceneDirector.Play(second);
        AdvanceUntilNotPlaying(world, 10);

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Stopped, ended[0].State);
        Assert.Same(first, ended[0].Asset);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(third, ended[1].Asset);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Ended_ParallelBranchFailureStoppedBeforeUnwindingRaisesFailedOnce()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelFailureAsset(failingBranchFirst: false);
        var observed = new List<(CutsceneRuntimeState Event, CutsceneRuntimeState Snapshot)>();
        world.CutsceneDirector.Ended += (_, args) =>
            observed.Add((args.State, world.CutsceneDirector.GetDebugSnapshot().State));

        world.CutsceneDirector.Play(asset);
        for (int frameIndex = 1; frameIndex <= 3; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        // The failure is reported at frame 2, but the play waits on its 5 s branch: it has not unwound.
        Assert.Empty(observed);
        Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.NotEmpty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);

        world.CutsceneDirector.Stop();

        (CutsceneRuntimeState Event, CutsceneRuntimeState Snapshot) single = Assert.Single(observed);
        Assert.Equal(CutsceneRuntimeState.Failed, single.Event);
        Assert.Equal(CutsceneRuntimeState.Failed, single.Snapshot);
        Assert.Empty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);

        world.Clear();

        Assert.Single(observed);
    }

    [Fact]
    public void Ended_ParallelBranchFailureClearedBeforeUnwindingRaisesFailedOnce()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelFailureAsset(failingBranchFirst: false);
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(asset);
        for (int frameIndex = 1; frameIndex <= 3; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Empty(ended);

        world.RuntimeSystems.Clear();

        CutsceneEndedEventArgs single = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Failed, single.State);
        Assert.Same(asset, single.Asset);
    }

    [Fact]
    public void Ended_ParallelBranchFailureReplacedBeforeUnwindingRaisesFailedThenCompleted()
    {
        var world = new World();
        CutsceneAsset failing = CreateParallelFailureAsset(failingBranchFirst: false);
        CutsceneAsset healthy = CreateSequenceAsset();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        world.CutsceneDirector.Play(failing);
        for (int frameIndex = 1; frameIndex <= 3; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Empty(ended);

        world.CutsceneDirector.Play(healthy);

        CutsceneEndedEventArgs afterReplace = Assert.Single(ended);
        Assert.Equal(CutsceneRuntimeState.Failed, afterReplace.State);
        Assert.Same(failing, afterReplace.Asset);

        for (int frameIndex = 4; frameIndex <= 12; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(healthy, ended[1].Asset);
        Assert.Empty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);
    }

    [Fact]
    public void Ended_FailedAtTheEndOfThePlay_StopsTheLaterParallelBranchesFirst()
    {
        var world = new World();
        CutsceneAsset asset = CreateParallelFailureAsset(failingBranchFirst: true);
        var observed = new List<(CutsceneRuntimeState Event, CutsceneRuntimeState Snapshot, int ActiveCoroutines)>();
        world.CutsceneDirector.Ended += (_, args) =>
        {
            CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
            observed.Add((args.State, snapshot.State, snapshot.ActiveCoroutines.Count));
        };

        world.CutsceneDirector.Play(asset);
        for (int frameIndex = 1; frameIndex <= 3; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        // Frame 2 fails the first branch, frame 3 unwinds the play to its end while the 5 s branch still waits.
        (CutsceneRuntimeState Event, CutsceneRuntimeState Snapshot, int ActiveCoroutines) single = Assert.Single(observed);
        Assert.Equal(CutsceneRuntimeState.Failed, single.Event);
        Assert.Equal(CutsceneRuntimeState.Failed, single.Snapshot);
        Assert.Equal(0, single.ActiveCoroutines);

        for (int frameIndex = 4; frameIndex <= 8; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Empty(world.CutsceneDirector.GetDebugSnapshot().ActiveCoroutines);
        world.CutsceneDirector.Stop();
        Assert.Single(observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ended_PlayFromFailedHandlerStopsTheRemainingActionsOfTheFailedPlay(bool trailingWait)
    {
        var world = new World();
        CutsceneAsset healthy = CreateSequenceAsset();
        CutsceneActionData trailing = trailingWait
            ? new WaitCutsceneActionData { Seconds = 5f }
            : new NavigateToCutsceneActionData
            {
                EntityName = "Nobody2",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            };
        var failing = new CutsceneAsset
        {
            Name = "FailingThenMore",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new NavigateToCutsceneActionData
                    {
                        EntityName = "Nobody",
                        Destination = new Vector3(2.5f, 0f, 0.5f),
                        StoppingDistance = 0.05f,
                    },
                    trailing,
                }
            }
        };
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) =>
        {
            ended.Add(args);
            if (args.State == CutsceneRuntimeState.Failed)
            {
                world.CutsceneDirector.Play(healthy);
            }
        };

        world.CutsceneDirector.Play(failing);
        for (int frameIndex = 1; frameIndex <= 12; frameIndex++)
        {
            AdvanceWorld(world, frameIndex);
        }

        Assert.Equal(2, ended.Count);
        Assert.Equal(CutsceneRuntimeState.Failed, ended[0].State);
        Assert.Same(failing, ended[0].Asset);
        Assert.Equal(CutsceneRuntimeState.Completed, ended[1].State);
        Assert.Same(healthy, ended[1].Asset);
        CutsceneDebugSnapshot snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Completed, snapshot.State);
        Assert.Equal(healthy.Name, snapshot.AssetName);
        Assert.Empty(snapshot.ActiveCoroutines);
    }

    private static CutsceneAsset CreateParallelFailureAsset(bool failingBranchFirst)
    {
        CutsceneActionData failingBranch = new NavigateToCutsceneActionData
        {
            EntityName = "Nobody",
            Destination = new Vector3(2.5f, 0f, 0.5f),
            StoppingDistance = 0.05f,
        };
        CutsceneActionData waitingBranch = new WaitCutsceneActionData { Seconds = 5f };

        var parallel = new ParallelCutsceneActionData();
        parallel.Actions.Add(failingBranchFirst ? failingBranch : waitingBranch);
        parallel.Actions.Add(failingBranchFirst ? waitingBranch : failingBranch);

        return new CutsceneAsset
        {
            Name = "ParallelFailure",
            RootAction = parallel
        };
    }

    private static CutsceneAsset CreateMissingEntityNavigateAsset()
    {
        return new CutsceneAsset
        {
            Name = "NavigateMissingEntity",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Nobody",
                Destination = new Vector3(2.5f, 0f, 0.5f),
                StoppingDistance = 0.05f,
            }
        };
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

    private static Entity CreateNavigableEntity(
        out CharacterControllerComponent controller,
        out NavigationAgentComponent navigationAgent,
        out CharacterControllerNavigationDriverComponent navigationDriver)
    {
        Entity entity = CreateControlledEntity(out controller);
        entity.RootComponent!.Position = new Vector3(0.5f, 0f, 0.5f);
        navigationAgent = new NavigationAgentComponent
        {
            NavigationMap = CreateGroundGrid(3, 1),
            Query = new NavigationQuery { LayerMask = NavigationLayerMask.Ground },
            StoppingDistance = 0.05f,
        };
        navigationDriver = new CharacterControllerNavigationDriverComponent();
        entity.AddComponent(navigationAgent);
        entity.AddComponent(navigationDriver);
        return entity;
    }

    private static NavigationGrid2D CreateGroundGrid(int width, int height)
    {
        var grid = new NavigationGrid2D(width, height, 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid.SetCell(x, y, new NavigationGridCell(true, 1f, NavigationLayerMask.Ground));
            }
        }

        return grid;
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}