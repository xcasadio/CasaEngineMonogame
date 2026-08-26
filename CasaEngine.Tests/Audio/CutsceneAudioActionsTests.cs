using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class CutsceneAudioActionsTests
{
    private static CutsceneAsset RoundTrip(CutsceneActionData rootAction)
    {
        var asset = new CutsceneAsset { Name = "audio cutscene", RootAction = rootAction };
        var document = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, document);

        var reloaded = new CutsceneAsset();
        reloaded.Load(document);
        return reloaded;
    }

    [Fact]
    public void PlaySound_SurvivesARoundTrip()
    {
        var soundAssetId = Guid.NewGuid();

        var reloaded = RoundTrip(new PlaySoundCutsceneActionData
        {
            SoundAssetId = soundAssetId,
            Volume = 0.5f,
            BusName = "Voice",
        });

        var action = Assert.IsType<PlaySoundCutsceneActionData>(reloaded.RootAction);
        Assert.Equal(soundAssetId, action.SoundAssetId);
        Assert.Equal(0.5f, action.Volume, 4);
        Assert.Equal("Voice", action.BusName);
    }

    [Fact]
    public void PlayMusic_SurvivesARoundTrip()
    {
        var soundAssetId = Guid.NewGuid();

        var reloaded = RoundTrip(new PlayMusicCutsceneActionData
        {
            SoundAssetId = soundAssetId,
            FadeInSeconds = 2.5f,
            Crossfade = false,
        });

        var action = Assert.IsType<PlayMusicCutsceneActionData>(reloaded.RootAction);
        Assert.Equal(soundAssetId, action.SoundAssetId);
        Assert.Equal(2.5f, action.FadeInSeconds, 4);
        Assert.False(action.Crossfade);
    }

    [Fact]
    public void StopMusic_SurvivesARoundTrip()
    {
        var reloaded = RoundTrip(new StopMusicCutsceneActionData { FadeOutSeconds = 1.5f });

        var action = Assert.IsType<StopMusicCutsceneActionData>(reloaded.RootAction);
        Assert.Equal(1.5f, action.FadeOutSeconds, 4);
    }

    [Fact]
    public void FadeMusic_SurvivesARoundTrip()
    {
        var reloaded = RoundTrip(new FadeMusicCutsceneActionData { TargetVolume = 0.25f, DurationSeconds = 3f });

        var action = Assert.IsType<FadeMusicCutsceneActionData>(reloaded.RootAction);
        Assert.Equal(0.25f, action.TargetVolume, 4);
        Assert.Equal(3f, action.DurationSeconds, 4);
    }

    [Fact]
    public void PlayMusic_DefaultsToCrossfadingWhenTheFlagIsAbsent()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "c",
            ["root_action"] = new JObject
            {
                ["type"] = CutsceneActionTypes.PlayMusic,
                ["sound_asset_id"] = Guid.NewGuid().ToString(),
            },
        };

        var asset = new CutsceneAsset();
        asset.Load(document);

        Assert.True(Assert.IsType<PlayMusicCutsceneActionData>(asset.RootAction).Crossfade);
    }

    [Fact]
    public void Validate_AcceptsWellFormedAudioActions()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new PlaySoundCutsceneActionData { SoundAssetId = Guid.NewGuid(), Volume = 1f },
                    new PlayMusicCutsceneActionData { SoundAssetId = Guid.NewGuid(), FadeInSeconds = 1f },
                    new FadeMusicCutsceneActionData { TargetVolume = 0.5f, DurationSeconds = 2f },
                    new StopMusicCutsceneActionData { FadeOutSeconds = 1f },
                },
            },
        };

        Assert.True(CutsceneValidator.Validate(asset).IsValid);
    }

    [Fact]
    public void Validate_RejectsAPlaySoundWithoutAsset()
    {
        var asset = new CutsceneAsset { RootAction = new PlaySoundCutsceneActionData() };

        var result = CutsceneValidator.Validate(asset);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, x => x.Message.Contains("PlaySound.sound_asset_id"));
    }

    [Fact]
    public void Validate_RejectsAPlayMusicWithoutAsset()
    {
        var asset = new CutsceneAsset { RootAction = new PlayMusicCutsceneActionData() };

        Assert.False(CutsceneValidator.Validate(asset).IsValid);
    }

    [Fact]
    public void Validate_RejectsNegativeDurations()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new PlayMusicCutsceneActionData { SoundAssetId = Guid.NewGuid(), FadeInSeconds = -1f },
                    new StopMusicCutsceneActionData { FadeOutSeconds = -1f },
                    new FadeMusicCutsceneActionData { DurationSeconds = -1f },
                },
            },
        };

        var result = CutsceneValidator.Validate(asset);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Messages.Count(x => x.Message.Contains("greater than or equal to zero")));
    }

    [Fact]
    public void Validate_RejectsOutOfRangeVolumes()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new PlaySoundCutsceneActionData { SoundAssetId = Guid.NewGuid(), Volume = 2f },
                    new FadeMusicCutsceneActionData { TargetVolume = -0.5f },
                },
            },
        };

        var result = CutsceneValidator.Validate(asset);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Messages.Count(x => x.Message.Contains("between zero and one")));
    }
}
