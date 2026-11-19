using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Tests.Audio;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePreviewEffectHostTests
{
    private const float Step = 0.1f;

    [Fact]
    public void TheHostAlwaysHasAnAudioService_AndOwnsItsScreenEffectService()
    {
        using var host = new CutscenePreviewEffectHost();

        Assert.NotNull(host.Audio);
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

    private static ExitOnInputCutsceneActionData NewExitOnInput(string next, params string[] inputs)
    {
        var action = new ExitOnInputCutsceneActionData { Mapping = "mapping", NextCutscene = next };
        action.Inputs.AddRange(inputs);
        return action;
    }

    [Fact]
    public void AnExitOnInputThatWasPassed_ArmsItsInputs()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        Assert.Null(host.ArmedInput);
        Assert.Empty(host.ArmedInputNames);
        ExitOnInputCutsceneActionData exitOnInput = NewExitOnInput("next", "exit", "cancel");

        PlayToTheEnd(world, new CutsceneAsset { Name = "Arm", RootAction = exitOnInput }, out _);

        Assert.Same(exitOnInput, host.ArmedInput);
        Assert.Equal(["exit", "cancel"], host.ArmedInputNames);
        Assert.Equal("next", host.ArmedNextCutscene);
        Assert.Equal(1, host.ArmedVersion);
    }

    [Fact]
    public void ASecondExitOnInput_ReplacesTheFirst()
    {
        using var host = new CutscenePreviewEffectHost();
        ExitOnInputCutsceneActionData first = NewExitOnInput("one", "a");
        ExitOnInputCutsceneActionData second = NewExitOnInput("two", "b");

        host.AllowGameRequest(first);
        int versionAfterFirst = host.ArmedVersion;
        host.AllowGameRequest(second);

        Assert.Same(second, host.ArmedInput);
        Assert.Equal(["b"], host.ArmedInputNames);
        Assert.Equal("two", host.ArmedNextCutscene);
        Assert.NotEqual(versionAfterFirst, host.ArmedVersion);
        Assert.Equal(2, host.Requests.Count);
    }

    [Fact]
    public void TheSameExitOnInputPassedAgain_StaysArmed_KeepsItsVersion_AndIsNotRecordedTwice()
    {
        using var host = new CutscenePreviewEffectHost();
        ExitOnInputCutsceneActionData action = NewExitOnInput("next", "exit");

        host.AllowGameRequest(action);
        int version = host.ArmedVersion;
        host.AllowGameRequest(action);
        host.AllowGameRequest(action);

        Assert.Same(action, host.ArmedInput);
        Assert.Equal(version, host.ArmedVersion);
        Assert.Single(host.Requests);
    }

    [Fact]
    public void ARepeatedExitOnInput_StaysArmedAtEveryTurn()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        ExitOnInputCutsceneActionData exitOnInput = NewExitOnInput("next", "exit");
        var asset = new CutsceneAsset
        {
            Name = "Loop",
            RootAction = new RepeatCutsceneActionData
            {
                Count = 3,
                Actions = { exitOnInput, new WaitFramesCutsceneActionData { Frames = 1 } },
            },
        };

        PlayToTheEnd(world, asset, out _);

        Assert.Same(exitOnInput, host.ArmedInput);
        Assert.Equal(1, host.ArmedVersion);
        Assert.Single(host.Requests);
    }

    [Fact]
    public void Disarm_DropsTheArmedAction_AndChangesTheVersionOnce()
    {
        using var host = new CutscenePreviewEffectHost();
        host.AllowGameRequest(NewExitOnInput("next", "exit"));
        int armedVersion = host.ArmedVersion;

        host.Disarm();
        int disarmedVersion = host.ArmedVersion;
        host.Disarm();

        Assert.Null(host.ArmedInput);
        Assert.Empty(host.ArmedInputNames);
        Assert.Equal(string.Empty, host.ArmedNextCutscene);
        Assert.NotEqual(armedVersion, disarmedVersion);
        Assert.Equal(disarmedVersion, host.ArmedVersion);
    }

    [Fact]
    public void Reset_DisarmsTheHost()
    {
        using var host = new CutscenePreviewEffectHost();
        host.AllowGameRequest(NewExitOnInput("next", "exit"));

        host.Reset();

        Assert.Null(host.ArmedInput);
        Assert.False(host.TrySimulateInput("exit", out _));
    }

    [Fact]
    public void TrySimulateInput_AnswersForAnArmedName_AndRefusesTheOthers()
    {
        using var host = new CutscenePreviewEffectHost();

        Assert.False(host.TrySimulateInput("exit", out string withoutArm));
        Assert.Null(withoutArm);

        host.AllowGameRequest(NewExitOnInput("next", "exit", "cancel"));

        Assert.True(host.TrySimulateInput("cancel", out string next));
        Assert.Equal("next", next);
        Assert.False(host.TrySimulateInput("jump", out string unknown));
        Assert.Null(unknown);
        Assert.False(host.TrySimulateInput(null, out _));
        Assert.NotNull(host.ArmedInput);
    }

    [Fact]
    public void TheScreenEffectAfterReset_IsInItsFirstState_OnTheSameInstance()
    {
        (World world, CutscenePreviewEffectHost host) = NewWorldWithHost();
        ScreenEffectService effects = host.ScreenEffects;
        var white = new CutsceneAsset
        {
            Name = "White",
            RootAction = new FadeScreenCutsceneActionData { R = 255, G = 255, B = 255, DurationSeconds = 0.5f },
        };
        PlayToTheEnd(world, white, out _, host);
        Assert.Equal(255, host.ScreenEffects.R);

        host.Reset();

        Assert.Same(effects, host.ScreenEffects);
        Assert.False(effects.Active);
        Assert.Equal(0, effects.R);
        Assert.Equal(0, effects.G);
        Assert.Equal(0, effects.B);
        Assert.Equal(255, effects.A);
        Assert.Equal(SpriteBlendMode.Opaque, effects.Blend);
        Assert.Equal(ScreenEffectLayer.BelowUI, effects.Layer);

        // The same fade played again goes from black to the half of the way, not from the white of the previous play.
        world.CutsceneDirector.Play(white);
        for (int frame = 1; frame <= 3; frame++)
        {
            world.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
            host.Update(Step);
        }

        Assert.InRange(effects.R, 100, 160);
    }

    public static TheoryData<bool, bool> FlagCombinations => new()
    {
        { false, false },
        { true, false },
        { false, true },
        { true, true },
    };

    [Theory]
    [MemberData(nameof(FlagCombinations))]
    public void Audio_IsTheEditorServiceOnlyWhenTheSoundIsOnAndTheStepIsANormalPlayStep(bool soundOn, bool normalStep)
    {
        using var editorAudio = new AudioService(new FakeAudioBackend());
        using var host = new CutscenePreviewEffectHost(editorAudio) { IsSoundOn = soundOn, IsNormalPlayStep = normalStep };

        AudioService audio = host.Audio;

        Assert.NotNull(audio);
        if (soundOn && normalStep)
        {
            Assert.Same(editorAudio, audio);
            Assert.True(host.EditorAudioLent);
        }
        else
        {
            Assert.NotSame(editorAudio, audio);
            Assert.False(host.EditorAudioLent);
        }
    }

    [Fact]
    public void Audio_WithoutAnEditorService_IsAMutedService()
    {
        using var host = new CutscenePreviewEffectHost { IsSoundOn = true, IsNormalPlayStep = true };

        AudioService audio = host.Audio;

        Assert.NotNull(audio);
        Assert.False(audio.Backend.IsAvailable);
        Assert.False(host.EditorAudioLent);
    }

    [Fact]
    public void Reset_GivesTheHostANewMutedService_AndKeepsTheSoundFlags()
    {
        using var editorAudio = new AudioService(new FakeAudioBackend());
        using var host = new CutscenePreviewEffectHost(editorAudio) { IsSoundOn = true };
        AudioService before = host.Audio;

        host.Reset();

        Assert.NotSame(before, host.Audio);
        Assert.False(host.Audio.Backend.IsAvailable);
        Assert.True(host.IsSoundOn);
        Assert.Same(editorAudio, host.EditorAudio);
    }

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var host = new CutscenePreviewEffectHost();

        host.Dispose();
        host.Dispose();
        host.Reset();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ASoundAction_ReachesTheEditorDevice_OnlyWhenTheSoundIsOnAndTheStepIsANormalPlayStep(bool soundOn, bool normalStep)
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = soundOn;
        rig.Host.IsNormalPlayStep = normalStep;

        rig.Play(new SequenceCutsceneActionData
        {
            Actions =
            {
                new PlaySoundCutsceneActionData { SoundAssetId = AudioRig.SoundAssetId },
                new PlayMusicCutsceneActionData { SoundAssetId = AudioRig.MusicAssetId },
            },
        });

        int expected = soundOn && normalStep ? 1 : 0;
        Assert.Equal(expected, rig.Backend.PlayCount);
        Assert.Equal(expected, rig.EditorAudio.Music.ActiveTrackCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AFadeMusicOfOneSecond_LastsOneSecond_WhateverTheSoundAndTheStep(bool soundOn, bool normalStep)
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = soundOn;
        rig.Host.IsNormalPlayStep = normalStep;

        int frames = rig.PlayUntilTheMarker(new SequenceCutsceneActionData
        {
            Actions =
            {
                new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 1f },
                new FadeScreenCutsceneActionData { DurationSeconds = 0f },
            },
        });

        Assert.InRange(frames, 10, 12);
    }

    [Fact]
    public void AFadeMusic_OnTheMutedService_LeavesTheMixerOfTheEditorAlone()
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = false;
        rig.Host.IsNormalPlayStep = true;

        rig.PlayUntilTheMarker(new SequenceCutsceneActionData
        {
            Actions =
            {
                new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 1f },
                new FadeScreenCutsceneActionData { DurationSeconds = 0f },
            },
        });

        Assert.Equal(1f, rig.MusicBus.Volume, 4);
        Assert.False(rig.Host.EditorAudioLent);
    }

    [Fact]
    public void RestoreEditorAudio_PutsTheMusicBusBack_StopsTheVoicesOfTheWorld_AndTheMusics()
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = true;
        rig.Host.IsNormalPlayStep = true;
        rig.Host.TakeMixerSnapshot();

        rig.PlayUntilTheMarker(new SequenceCutsceneActionData
        {
            Actions =
            {
                new PlaySoundCutsceneActionData { SoundAssetId = AudioRig.SoundAssetId },
                new PlayMusicCutsceneActionData { SoundAssetId = AudioRig.MusicAssetId },
                new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 0.5f },
                new FadeScreenCutsceneActionData { DurationSeconds = 0f },
            },
        });
        Assert.Equal(0f, rig.MusicBus.Volume, 4);
        Assert.Equal(1, rig.EditorAudio.Music.ActiveTrackCount);
        Assert.True(rig.Backend.ActiveVoiceCount >= 2);

        rig.Host.RestoreEditorAudio(rig.World);

        Assert.Equal(1f, rig.MusicBus.Volume, 4);
        Assert.Equal(0, rig.EditorAudio.Music.ActiveTrackCount);
        Assert.Equal(0, rig.Backend.ActiveVoiceCount);
    }

    [Fact]
    public void RestoreEditorAudio_AfterAnExtinctionAndALaterSwitchOn_StillPutsTheSameStateBack()
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = true;
        rig.Host.IsNormalPlayStep = true;
        rig.Host.TakeMixerSnapshot();
        Assert.Equal(1f, rig.MusicBus.Volume, 4);

        rig.World.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Fade",
            RootAction = new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 1f },
        });

        for (int frame = 1; frame <= 30 && rig.World.CutsceneDirector.IsPlaying; frame++)
        {
            // Off from 0.3 s, on again from 0.6 s, with no new snapshot.
            rig.Host.IsSoundOn = frame is < 3 or >= 6;
            rig.World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
            rig.Host.Update(Step);
        }

        Assert.False(rig.World.CutsceneDirector.IsPlaying);
        Assert.Equal(0f, rig.MusicBus.Volume, 4);

        rig.Host.RestoreEditorAudio(rig.World);

        Assert.Equal(1f, rig.MusicBus.Volume, 4);
    }

    [Fact]
    public void ABusLeftAtZeroByAPreviousPlay_IsCleanedBeforeTheNextSnapshot()
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = true;
        rig.Host.IsNormalPlayStep = true;
        rig.Host.TakeMixerSnapshot();
        rig.PlayUntilTheMarker(new SequenceCutsceneActionData
        {
            Actions =
            {
                new FadeMusicCutsceneActionData { TargetVolume = 0f, DurationSeconds = 0.3f },
                new FadeScreenCutsceneActionData { DurationSeconds = 0f },
            },
        });
        Assert.Equal(0f, rig.MusicBus.Volume, 4);

        // What a launch does: clean up the previous play, then take the snapshot of the new one.
        rig.Host.RestoreEditorAudio(rig.World);
        rig.Host.TakeMixerSnapshot();
        Assert.Equal(1f, rig.MusicBus.Volume, 4);

        // The snapshot holds 1.0: a loan that changes the bus is put back to 1.0.
        rig.MusicBus.Volume = 0.4f;
        Assert.Same(rig.EditorAudio, rig.Host.Audio);
        rig.Host.RestoreEditorAudio(rig.World);

        Assert.Equal(1f, rig.MusicBus.Volume, 4);
    }

    [Fact]
    public void RestoreEditorAudio_DoesNothingWhenTheEditorServiceWasNotLent_OrDoesNotExist()
    {
        using var rig = new AudioRig();
        rig.Host.IsSoundOn = false;
        rig.Host.TakeMixerSnapshot();
        rig.MusicBus.Volume = 0.4f;

        rig.Host.RestoreEditorAudio(rig.World);

        Assert.Equal(0.4f, rig.MusicBus.Volume, 4);

        using var withoutService = new CutscenePreviewEffectHost();
        withoutService.TakeMixerSnapshot();
        withoutService.RestoreEditorAudio(rig.World);
    }

    [Fact]
    public void TheStepCounts_FollowTheNormalPlayFlag_AndAreZeroedByTheSnapshotOnly()
    {
        using var host = new CutscenePreviewEffectHost();
        Assert.Equal(0, host.NormalPlayStepCount);
        Assert.Equal(0, host.OtherStepCount);
        Assert.False(host.LastStepWasNormalPlay);

        host.IsNormalPlayStep = false;
        host.Update(Step);
        host.Update(Step);
        host.Update(Step);
        host.IsNormalPlayStep = true;
        host.Update(Step);
        host.Update(Step);

        Assert.Equal(3, host.OtherStepCount);
        Assert.Equal(2, host.NormalPlayStepCount);
        Assert.True(host.LastStepWasNormalPlay);

        host.Reset();

        Assert.Equal(3, host.OtherStepCount);
        Assert.Equal(2, host.NormalPlayStepCount);
        Assert.True(host.LastStepWasNormalPlay);

        host.TakeMixerSnapshot();

        Assert.Equal(0, host.OtherStepCount);
        Assert.Equal(0, host.NormalPlayStepCount);
        Assert.False(host.LastStepWasNormalPlay);
    }

    /// <summary>A world whose sounds load with a file and a clip provider, a fake device for the editor service, and a host over it.</summary>
    private sealed class AudioRig : IDisposable
    {
        public static readonly Guid SoundAssetId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public static readonly Guid MusicAssetId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        public AudioRig()
        {
            Backend = new FakeAudioBackend();
            var provider = new FakeAudioClipProvider();
            EditorAudio = new AudioService(Backend) { ClipProvider = provider };
            Host = new CutscenePreviewEffectHost(EditorAudio);

            var infos = new Dictionary<Guid, AssetInfo>
            {
                [SoundAssetId] = new AssetInfo(SoundAssetId) { Name = "hit", FileName = "hit.sound" },
                [MusicAssetId] = new AssetInfo(MusicAssetId) { Name = "theme", FileName = "theme.sound" },
            };

            var sound = new SoundAsset { Name = "hit", AudioFileAssetId = provider.Register(new FakeAudioClip("hit")) };
            var music = new SoundAsset
            {
                Name = "theme",
                AudioFileAssetId = provider.RegisterStream(WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 106496, formatChunkSize: 18)),
                BusName = AudioBusNames.Music,
                IsStreaming = true,
            };

            var assets = new AssetContentManager
            {
                RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
            };
            assets.RegisterAssetLoader(typeof(SoundAsset), new SoundLoader(new Dictionary<string, SoundAsset>
            {
                ["hit.sound"] = sound,
                ["theme.sound"] = music,
            }));

            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assets);
            World = new World();
            SetBackingField(World, nameof(World.Game), game);
            World.CutsceneDirector.EffectHost = Host;
        }

        public FakeAudioBackend Backend { get; }

        public AudioService EditorAudio { get; }

        public CutscenePreviewEffectHost Host { get; }

        public World World { get; }

        public AudioBus MusicBus => EditorAudio.Mixer.GetBus(AudioBusNames.Music);

        public void Play(CutsceneActionData root)
        {
            World.CutsceneDirector.Play(new CutsceneAsset { Name = "Rig", RootAction = root });
            for (int frame = 1; frame <= 60 && World.CutsceneDirector.IsPlaying; frame++)
            {
                World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
                Host.Update(Step);
            }

            Assert.False(World.CutsceneDirector.IsPlaying);
        }

        /// <summary>Plays until the sequence ends and returns the frame at which the marker (the last action, a zero fade) started.</summary>
        public int PlayUntilTheMarker(CutsceneActionData root)
        {
            World.CutsceneDirector.Play(new CutsceneAsset { Name = "Rig", RootAction = root });
            int markerFrame = 0;
            for (int frame = 1; frame <= 60 && World.CutsceneDirector.IsPlaying; frame++)
            {
                World.CoroutineManager.Update(new CoroutineUpdateContext(Step, Step, 1f, frame));
                Host.Update(Step);
                if (markerFrame == 0 && Host.ScreenEffects.Active)
                {
                    markerFrame = frame;
                }
            }

            Assert.False(World.CutsceneDirector.IsPlaying);
            Assert.True(markerFrame > 0);
            return markerFrame;
        }

        public void Dispose()
        {
            Host.Dispose();
            EditorAudio.Dispose();
        }

        private sealed class SoundLoader(Dictionary<string, SoundAsset> assets) : IAssetLoader
        {
            public object LoadAsset(string fileName, AssetContentManager assetContentManager) => assets[Path.GetFileName(fileName)];

            public bool IsFileSupported(string fileName) => true;
        }
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        FieldInfo field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
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
