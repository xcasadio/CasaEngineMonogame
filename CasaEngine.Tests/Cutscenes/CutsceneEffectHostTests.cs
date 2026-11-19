using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Tests.Audio;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>The <see cref="ICutsceneEffectHost"/> of a director: the services it supplies and the game requests it refuses.</summary>
public sealed class CutsceneEffectHostTests
{
    private static readonly Guid SoundAssetId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static TheoryData<string> GameRequestActions => new()
    {
        { "change_world" },
        { "open_screen" },
        { "play_video" },
        { "exit_on_input" },
    };

    [Theory]
    [MemberData(nameof(GameRequestActions))]
    public void ARefusedGameRequest_IsSkipped_AndThePlayCompletes(string kind)
    {
        var (world, manager) = NewWorldWithGameManager();
        var host = new TestHost { Allow = false };
        world.CutsceneDirector.EffectHost = host;
        var resolverCalls = 0;
        world.CutsceneDirector.ExitOnInputResolver = _ =>
        {
            resolverCalls++;
            return (null, "must not be asked");
        };
        CutsceneActionData action = NewGameRequest(kind);

        PlayUntilNotPlaying(world, new SequenceCutsceneActionData
        {
            Actions = { action, new WaitFramesCutsceneActionData { Frames = 1 } },
        });

        Assert.Same(action, Assert.Single(host.Requests));
        Assert.Null(manager.PendingWorldToLoad);
        Assert.Equal(0, resolverCalls);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ARefusedGameRequest_WithoutARunningGame_IsARefusalNotAnError()
    {
        var world = new World();
        var host = new TestHost { Allow = false };
        world.CutsceneDirector.EffectHost = host;

        PlayUntilNotPlaying(world, new ChangeWorldCutsceneActionData { Target = "second.world" });

        Assert.Single(host.Requests);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AnAllowedGameRequest_ReachesTheGameAsWithoutAHost()
    {
        var world = new World();
        var host = new TestHost { Allow = true };
        world.CutsceneDirector.EffectHost = host;

        PlayUntilNotPlaying(world, new ChangeWorldCutsceneActionData { Target = "second.world" });

        Assert.Single(host.Requests);
        Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void ARefusedGameRequest_InsideRepeatAndParallel_IsRefusedToo()
    {
        var world = new World();
        var host = new TestHost { Allow = false };
        world.CutsceneDirector.EffectHost = host;
        var repeat = new RepeatCutsceneActionData
        {
            Count = 2,
            Actions = { new ChangeWorldCutsceneActionData { Target = "a.world" }, new WaitFramesCutsceneActionData { Frames = 1 } },
        };
        var parallel = new ParallelCutsceneActionData
        {
            Actions = { new OpenScreenCutsceneActionData { Target = "Intro" }, new WaitFramesCutsceneActionData { Frames = 1 } },
        };

        PlayUntilNotPlaying(world, new SequenceCutsceneActionData { Actions = { repeat, parallel } });

        Assert.Equal(3, host.Requests.Count);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void FadeScreen_UsesTheScreenEffectServiceOfTheHost_AndLastsItsDuration()
    {
        var world = new World();
        var service = new ScreenEffectService();
        world.CutsceneDirector.EffectHost = new TestHost { ScreenEffects = service };

        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Fade",
            RootAction = new FadeScreenCutsceneActionData { R = 255, G = 0, B = 0, DurationSeconds = 0.5f },
        });

        int frames = 0;
        bool sawFade = false;
        while (world.CutsceneDirector.IsPlaying && frames < 20)
        {
            frames++;
            world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, frames));
            service.Update(0.1f);
            sawFade |= service.IsFading;
        }

        Assert.True(sawFade);

        Assert.InRange(frames, 5, 6);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
        Assert.Equal(255, service.R);
    }

    [Fact]
    public void AHostWithoutAudio_PlaysNothing()
    {
        AssetContentManager assets = NewAssetManager(out SoundLoader loader);
        World world = NewWorldWithAssets(assets);
        world.CutsceneDirector.EffectHost = new TestHost { Audio = null };

        PlayUntilNotPlaying(world, new SequenceCutsceneActionData
        {
            Actions =
            {
                new PlaySoundCutsceneActionData { SoundAssetId = SoundAssetId },
                new PlayMusicCutsceneActionData { SoundAssetId = SoundAssetId },
                new StopMusicCutsceneActionData(),
                new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 1f },
            },
        });

        Assert.Equal(0, loader.Loads);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void AHostWithAnAudioService_ReceivesTheSoundActions()
    {
        AssetContentManager assets = NewAssetManager(out SoundLoader loader);
        World world = NewWorldWithAssets(assets);
        using var audio = new AudioService(new FakeAudioBackend());
        world.CutsceneDirector.EffectHost = new TestHost { Audio = audio };

        PlayUntilNotPlaying(world, new PlaySoundCutsceneActionData { SoundAssetId = SoundAssetId });

        Assert.Equal(1, loader.Loads);
    }

    private static CutsceneActionData NewGameRequest(string kind)
    {
        switch (kind)
        {
            case "change_world":
                return new ChangeWorldCutsceneActionData { Target = "second.world" };
            case "open_screen":
                return new OpenScreenCutsceneActionData { Target = "Intro" };
            case "play_video":
                return new PlayVideoCutsceneActionData { Target = "Movie" };
            default:
                var exitOnInput = new ExitOnInputCutsceneActionData { Mapping = "mapping", NextCutscene = "next" };
                exitOnInput.Inputs.Add("exit");
                return exitOnInput;
        }
    }

    private static void PlayUntilNotPlaying(World world, CutsceneActionData root)
    {
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Host", RootAction = root });
        Assert.True(world.CutsceneDirector.IsPlaying);

        for (int frame = 1; frame <= 30 && world.CutsceneDirector.IsPlaying; frame++)
        {
            world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, frame));
        }

        Assert.False(world.CutsceneDirector.IsPlaying);
    }

    private static (World World, GameManager Manager) NewWorldWithGameManager()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var manager = new GameManager(null);
        SetBackingField(game, nameof(CasaEngineGame.GameManager), manager);
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return (world, manager);
    }

    private static World NewWorldWithAssets(AssetContentManager assets)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assets);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return world;
    }

    private static AssetContentManager NewAssetManager(out SoundLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SoundAssetId] = new AssetInfo(SoundAssetId) { Name = "hit", FileName = "hit.sound" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new SoundLoader();
        manager.RegisterAssetLoader(typeof(SoundAsset), loader);
        return manager;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        FieldInfo field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private sealed class SoundLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new SoundAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class TestHost : ICutsceneEffectHost
    {
        public AudioService Audio { get; set; }

        public ScreenEffectService ScreenEffects { get; set; }

        public bool Allow { get; set; }

        public List<CutsceneActionData> Requests { get; } = [];

        public bool AllowGameRequest(CutsceneActionData action)
        {
            Requests.Add(action);
            return Allow;
        }
    }
}
