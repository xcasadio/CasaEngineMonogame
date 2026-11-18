using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scripting.Coroutines;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePreviewEffectHostTests
{
    private const float Step = 0.1f;

    [Fact]
    public void TheHostPlaysNoSound_AndOwnsItsScreenEffectService()
    {
        var host = new CutscenePreviewEffectHost();

        Assert.Null(host.Audio);
        Assert.NotNull(host.ScreenEffects);
    }

    [Fact]
    public void TheExitCutsceneOfTheSample_RequestsNoWorld_AndRaisesTheEndFlag()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        CutsceneAsset asset = CutsceneEditSession.Load(JObject.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "Projects", "SpriteCutsceneSample", "Cutscenes", "exit.cutscene")))).Asset;

        PlayToTheEnd(world, asset, out _);

        CutscenePreviewRequest request = Assert.Single(host.Requests);
        Assert.Equal(CutsceneActionTypes.ChangeWorld, request.Type);
        Assert.Equal("EndWorld.world", request.Target);
        Assert.True(host.EndRequested);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ExitOnInputThenWait_Completes_AndTheActionIsRecordedOnce()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        var exitOnInput = new ExitOnInputCutsceneActionData { Mapping = "mapping", NextCutscene = "next" };
        exitOnInput.Inputs.Add("exit");
        var asset = new CutsceneAsset
        {
            Name = "Exit",
            RootAction = new SequenceCutsceneActionData
            {
                Actions = { exitOnInput, new WaitCutsceneActionData { Seconds = 0.3f } },
            },
        };

        PlayToTheEnd(world, asset, out _);

        CutscenePreviewRequest request = Assert.Single(host.Requests);
        Assert.Equal(CutsceneActionTypes.ExitOnInput, request.Type);
        Assert.Equal("next", request.Target);
        Assert.False(host.EndRequested);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnActionRefusedAtEveryTurnOfARepeat_IsListedOnce()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        var asset = new CutsceneAsset
        {
            Name = "Repeat",
            RootAction = new RepeatCutsceneActionData
            {
                Count = 3,
                Actions = { new OpenScreenCutsceneActionData { Target = "Intro" }, new WaitFramesCutsceneActionData { Frames = 1 } },
            },
        };

        PlayToTheEnd(world, asset, out _);

        Assert.Single(host.Requests);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AFadeScreenOfHalfASecond_AdvancesTheServiceOfTheHost()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        var asset = new CutsceneAsset
        {
            Name = "Fade",
            RootAction = new FadeScreenCutsceneActionData { R = 255, G = 0, B = 0, DurationSeconds = 0.5f },
        };

        PlayToTheEnd(world, asset, out bool sawFade, host);

        Assert.True(sawFade);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(255, host.ScreenEffects.R);
    }

    [Fact]
    public void Reset_ForgetsTheRequestsTheEndFlagAndTheFade()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        PlayToTheEnd(world, new CutsceneAsset { Name = "Change", RootAction = new ChangeWorldCutsceneActionData { Target = "x.world" } }, out _);
        Assert.NotEmpty(host.Requests);
        host.ScreenEffects.SetOverlay(1, 2, 3, CasaEngine.Framework.Rendering.Depth.SpriteBlendMode.AlphaBlend);

        host.Reset();

        Assert.Empty(host.Requests);
        Assert.False(host.EndRequested);
        Assert.False(host.ScreenEffects.Active);
    }

    private static (World World, CutscenePreviewEffectHost Host) NewWorldWithHost()
    {
        var world = new World();
        var host = new CutscenePreviewEffectHost();
        world.CutsceneDirector.EffectHost = host;
        return (world, host);
    }

    private static void PlayToTheEnd(World world, CutsceneAsset asset, out bool sawFade, CutscenePreviewEffectHost host = null)
    {
        sawFade = false;
        world.CutsceneDirector.Play(asset);
        Assert.True(world.CutsceneDirector.IsPlaying);

        for (int frame = 1; frame <= 60 && world.CutsceneDirector.IsPlaying; frame++)
        {
            world.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
            host?.Update(Step);
            sawFade |= host?.ScreenEffects.IsFading ?? false;
        }

        Assert.False(world.CutsceneDirector.IsPlaying);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("CasaEngine.MonoGame.sln was not found above the test binaries.");
    }
}
