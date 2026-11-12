using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework.Input;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Video;

/// <summary>
/// The StartVideoSprite cutscene action (ADR-0093) over the real player. It shows the entity, starts the film and yields
/// no frame; the component hides the entity in the update where the film ends; no end of the play (ExitOnInput, a
/// replacing play, Stop, a failure) touches the film; a Repeat restarts it; it lives with PlayVideoSprite through the
/// play generation. A frame of the game runs the coroutines, then the entity, then the keyboard, as the game does.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public sealed class StartVideoSpriteCutsceneActionGpuTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d5000000-0000-4000-8000-000000000001");
    private const string VideoName = "start-film";
    private const float Step = 0.1f;

    private readonly List<string> _files = new();

    public StartVideoSpriteCutsceneActionGpuTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "start-film.mdecvideo", AssetType = "mdecvideo" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
        foreach (var file in _files)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Left to the system.
            }
        }
    }

    // ---- No frame, and the end of the film (case 2) ----

    [GpuFact]
    public void TheAction_YieldsNoFrame_TheNextActionRunsInTheSameUpdate()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            var marker = rig.AddMarker();
            rig.Play(Sequence(Start(), new HideSpriteCutsceneActionData { EntityName = "Marker" }, Wait(10f)));

            rig.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 1));

            Assert.True(rig.Film.IsVisible);
            Assert.True(rig.Video.IsPlaying);
            Assert.True(rig.Video.HidesWhenEnded);
            Assert.False(marker.IsVisible);
        });
    }

    [GpuFact]
    public void AFilmThatEndsDuringThePlay_IsHiddenInTheUpdateWhereItEnds_OneUpdateBeforePlayVideoSprite()
    {
        var path = NewVideo(3);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var started = new Rig(path);
            started.Play(Sequence(Start(), Wait(10f)));
            var startedHidden = started.RunUntilHidden(20, out var startedVisibleFrames);

            var played = new Rig(path);
            played.Play(new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            var playedHidden = played.RunUntilHidden(20, out var playedVisibleFrames);

            // A film of 3 pictures at 10 frames per second, started in update 1, ends in update 3.
            Assert.Equal(3, startedHidden);
            Assert.Equal(2, startedVisibleFrames);
            Assert.True(started.Video.IsEnded);
            Assert.False(started.Video.IsPlaying);
            Assert.Equal(CutsceneRuntimeState.Playing, started.World.CutsceneDirector.GetDebugSnapshot().State);

            // PlayVideoSprite sees the end at the next update (ADR-0091): the extra frame of D3.
            Assert.Equal(4, playedHidden);
            Assert.Equal(3, playedVisibleFrames);
        });
    }

    // ---- The play stops: the film runs to its end (case 1) ----

    [GpuTheory]
    [InlineData("exit")]
    [InlineData("replace")]
    [InlineData("stop")]
    [InlineData("fail")]
    public void AStoppedPlay_LeavesTheFilmRunningToItsEnd_ThenTheEntityIsHidden(string how)
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            var next = new CutsceneAsset { Name = "next", RootAction = Wait(10f) };
            rig.ExitTo(next);

            switch (how)
            {
                case "exit":
                    rig.Play(Sequence(rig.ArmExit(), Start(), Wait(10f)));
                    break;
                case "fail":
                    rig.Play(Sequence(Start(), Wait(0.15f), new HideSpriteCutsceneActionData { EntityName = "Ghost" }));
                    break;
                default:
                    rig.Play(Sequence(Start(), Wait(10f)));
                    break;
            }

            rig.RunFrame();
            rig.RunFrame();
            Assert.True(rig.Video.IsPlaying);

            switch (how)
            {
                case "exit":
                    rig.KeyDown = true;
                    while (rig.World.CutsceneDirector.GetDebugSnapshot().AssetName != "next" && rig.Frame < 10)
                    {
                        rig.RunFrame();
                    }

                    Assert.Equal("next", rig.World.CutsceneDirector.GetDebugSnapshot().AssetName);
                    break;
                case "replace":
                    rig.World.CutsceneDirector.Play(next);
                    break;
                case "stop":
                    rig.World.CutsceneDirector.Stop();
                    Assert.Equal(CutsceneRuntimeState.Stopped, rig.World.CutsceneDirector.GetDebugSnapshot().State);
                    break;
                case "fail":
                    while (rig.World.CutsceneDirector.GetDebugSnapshot().State != CutsceneRuntimeState.Failed && rig.Frame < 10)
                    {
                        rig.RunFrame();
                    }

                    Assert.Equal(CutsceneRuntimeState.Failed, rig.World.CutsceneDirector.GetDebugSnapshot().State);
                    break;
            }

            // Two updates after the stop, the cleanup of any stopped coroutine has run: the film still plays and advances.
            var picture = rig.Video.CurrentFrameIndex;
            for (var i = 0; i < 2; i++)
            {
                rig.RunFrame();
                Assert.True(rig.Video.IsPlaying);
                Assert.True(rig.Film.IsVisible);
                Assert.True(rig.Video.CurrentFrameIndex > picture);
                picture = rig.Video.CurrentFrameIndex;
            }

            var hidden = rig.RunUntilHidden(60, out _);

            // A film of 30 pictures, started in update 1, ends in update 30, whatever stopped the play.
            Assert.Equal(30, hidden);
            Assert.True(rig.Video.IsEnded);
        });
    }

    // ---- Repeat (case 3) ----

    [GpuFact]
    public void ARepeat_RestartsTheFilm_AtEachLap_AndTheEntityIsHiddenAtEachEnd()
    {
        var path = NewVideo(3);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            var generation = rig.Video.PlayGeneration;
            rig.Play(Repeat(2, Start(), Wait(0.55f)));

            var visible = new List<bool>();
            while (rig.World.CutsceneDirector.GetDebugSnapshot().State == CutsceneRuntimeState.Playing && rig.Frame < 40)
            {
                rig.RunFrame();
                visible.Add(rig.Film.IsVisible);
            }

            Assert.Equal(CutsceneRuntimeState.Completed, rig.World.CutsceneDirector.GetDebugSnapshot().State);
            Assert.Equal(generation + 2, rig.Video.PlayGeneration);
            Assert.Equal([2, 2], VisibleRuns(visible));
            Assert.False(rig.Film.IsVisible);
            Assert.True(rig.Video.IsEnded);
        });
    }

    [GpuFact]
    public void ALapShorterThanTheFilm_RestartsItFromItsFirstPicture_AndTheEntityStaysVisibleUntilTheLastEnd()
    {
        var path = NewVideo(5);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            var generation = rig.Video.PlayGeneration;
            rig.Play(Repeat(2, Start(), Wait(0.15f)));

            var visible = new List<bool>();
            var restarts = 0;
            var picture = -1;
            while (!rig.Video.IsEnded && rig.Frame < 40)
            {
                rig.RunFrame();
                visible.Add(rig.Film.IsVisible);
                if (rig.Video.CurrentFrameIndex < picture)
                {
                    restarts++;
                }

                picture = rig.Video.CurrentFrameIndex;
            }

            Assert.Equal(generation + 2, rig.Video.PlayGeneration);
            Assert.Equal(1, restarts);
            Assert.Single(VisibleRuns(visible));
            Assert.False(rig.Film.IsVisible);
        });
    }

    [GpuFact]
    public void AnEndlessRepeatOfTheActionAlone_FailsBecauseNoFramePasses()
    {
        var path = NewVideo(3);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            rig.Play(Repeat(0, Start()));

            rig.RunFrame();

            var snapshot = rig.World.CutsceneDirector.GetDebugSnapshot();
            Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
            Assert.Contains("without a frame passing", snapshot.ActiveActionStopReason);
        });
    }

    // ---- Living with PlayVideoSprite (C4) ----

    [GpuFact]
    public void TheActionOnAFilmThatPlayVideoSpriteWaitsFor_TakesItOver_AndTheOlderActionLeavesItAlone()
    {
        var path = NewVideo(10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            var parallel = new ParallelCutsceneActionData();
            parallel.Actions.Add(new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });
            parallel.Actions.Add(Sequence(Wait(0.15f), Start()));
            rig.Play(parallel);

            while (!rig.Video.HidesWhenEnded && rig.Frame < 10)
            {
                rig.RunFrame();
            }

            Assert.True(rig.Video.HidesWhenEnded);
            var startFrame = rig.Frame;

            // The PlayVideoSprite branch sees another play and ends without touching the film; the Parallel completes.
            rig.RunFrame();
            rig.RunFrame();
            Assert.Equal(CutsceneRuntimeState.Completed, rig.World.CutsceneDirector.GetDebugSnapshot().State);
            Assert.True(rig.Video.IsPlaying);
            Assert.True(rig.Film.IsVisible);

            var hidden = rig.RunUntilHidden(60, out _);

            // Started again in update startFrame, a film of 10 pictures ends 9 updates later.
            Assert.Equal(startFrame + 9, hidden);
            Assert.True(rig.Video.IsEnded);
        });
    }

    [GpuFact]
    public void PlayVideoSpriteOnAFilmStartedByTheAction_OwnsIt_AndItsCleanupApplies()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            rig.Play(Sequence(Start(), Wait(0.15f), new PlayVideoSpriteCutsceneActionData { EntityName = "Film" }));

            var generation = rig.Video.PlayGeneration;
            rig.RunFrame();
            while (rig.Video.PlayGeneration == generation + 1 && rig.Frame < 10)
            {
                rig.RunFrame();
            }

            Assert.Equal(generation + 2, rig.Video.PlayGeneration);
            Assert.False(rig.Video.HidesWhenEnded);

            rig.World.CutsceneDirector.Stop();
            rig.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, rig.Frame + 1));

            Assert.False(rig.Video.IsPlaying);
            Assert.False(rig.Film.IsVisible);
        });
    }

    // ---- World cleared ----

    [GpuFact]
    public void AClearedWorld_ReleasesTheFilm_WithoutAnError()
    {
        var path = NewVideo(30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var rig = new Rig(path);
            rig.Play(Sequence(Start(), Wait(10f)));
            rig.RunFrame();
            Assert.True(rig.Video.IsPlaying);

            // What World.Clear does to the coroutines, then to the entities (the physics part needs a full game).
            rig.World.RuntimeSystems.Clear();
            rig.World.ClearEntities(true);
            rig.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, 10));
            rig.Video.Update(Step);

            Assert.False(rig.Video.HasPlayer);
            Assert.False(rig.Video.IsPlaying);
        });
    }

    // ---- Helpers ----

    private string NewVideo(int frameCount)
    {
        var path = WorldVideoTestFiles.Write(frameCount, 10, 1);
        _files.Add(path);
        return path;
    }

    private static StartVideoSpriteCutsceneActionData Start() => new() { EntityName = "Film" };

    private static WaitCutsceneActionData Wait(float seconds) => new() { Seconds = seconds };

    private static SequenceCutsceneActionData Sequence(params CutsceneActionData[] actions)
    {
        var sequence = new SequenceCutsceneActionData();
        sequence.Actions.AddRange(actions);
        return sequence;
    }

    private static RepeatCutsceneActionData Repeat(int count, params CutsceneActionData[] actions)
    {
        var repeat = new RepeatCutsceneActionData { Count = count };
        repeat.Actions.AddRange(actions);
        return repeat;
    }

    /// <summary>The lengths of the runs of consecutive visible frames.</summary>
    private static List<int> VisibleRuns(List<bool> visible)
    {
        var runs = new List<int>();
        var length = 0;
        foreach (var shown in visible)
        {
            if (shown)
            {
                length++;
            }
            else if (length > 0)
            {
                runs.Add(length);
                length = 0;
            }
        }

        if (length > 0)
        {
            runs.Add(length);
        }

        return runs;
    }

    /// <summary>A world with the film entity, a keyboard, and frames in the order of the game.</summary>
    private sealed class Rig
    {
        private readonly InputMapping _exit;
        private CutsceneAsset _next;

        public Rig(string path)
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            World = harness.World;
            Film = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = VideoName } };
            harness.World.Entities.Add(Film);
            harness.AddToWorld(Film);

            _exit = new InputMapping
            {
                Name = "exit",
                ButtonBehavior = ButtonBehaviors.DigitalInput,
                KeyButton = new KeyButton(Keys.Space),
                AlternativeKeyButton = new KeyButton(),
            };
            World.CutsceneDirector.ExitOnInputResolver = _ =>
                (new CutsceneInputWatch(World.CutsceneDirector, [_exit], Keyboard, new MouseManager(), new GamePadManager(), _next), null);
        }

        public World World { get; }

        public Entity Film { get; }

        public VideoSpriteComponent Video => (VideoSpriteComponent)Film.RootComponent!;

        public KeyboardManager Keyboard { get; } = new();

        public bool KeyDown { get; set; }

        public long Frame { get; private set; }

        public void ExitTo(CutsceneAsset next) => _next = next;

        public ExitOnInputCutsceneActionData ArmExit()
        {
            var action = new ExitOnInputCutsceneActionData { Mapping = "test-mapping", NextCutscene = "next" };
            action.Inputs.Add("exit");
            return action;
        }

        public Entity AddMarker()
        {
            var marker = new Entity { Name = "Marker", RootComponent = new StaticSpriteComponent() };
            World.Entities.Add(marker);
            return marker;
        }

        public void Play(CutsceneActionData root) => World.CutsceneDirector.Play(new CutsceneAsset { Name = "Film", RootAction = root });

        /// <summary>One frame of the game: the coroutines, then the entity, then the keyboard.</summary>
        public void RunFrame()
        {
            Frame++;
            World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, Frame));
            Film.Update(Step);
            Keyboard.Update(KeyDown ? new KeyboardState(Keys.Space) : new KeyboardState());
        }

        /// <summary>Runs frames until the entity is hidden; returns that frame, and how many frames it was visible after.</summary>
        public long RunUntilHidden(int limit, out int visibleFrames)
        {
            visibleFrames = 0;
            while (Frame < limit)
            {
                RunFrame();
                if (!Film.IsVisible)
                {
                    return Frame;
                }

                visibleFrames++;
            }

            return -1;
        }
    }
}
