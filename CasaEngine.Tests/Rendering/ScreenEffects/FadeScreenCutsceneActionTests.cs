using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering.Depth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScreenEffects;

/// <summary>
/// Covers the <c>FadeScreen</c> cutscene action added alongside the screen effect service (plan
/// item E10.a/6) - the exact layer of the four audio cutscene actions
/// (<c>CasaEngine.Tests.Audio.CutsceneAudioActionsTests</c>): serializer save/load round-trip and
/// validator coverage.
/// </summary>
public class FadeScreenCutsceneActionTests
{
    private static CutsceneAsset RoundTrip(CutsceneActionData rootAction)
    {
        var asset = new CutsceneAsset { Name = "screen effect cutscene", RootAction = rootAction };
        var document = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, document);

        var reloaded = new CutsceneAsset();
        reloaded.Load(document);
        return reloaded;
    }

    [Fact]
    public void FadeScreen_SurvivesARoundTrip()
    {
        var reloaded = RoundTrip(new FadeScreenCutsceneActionData
        {
            R = 10,
            G = 20,
            B = 30,
            DurationSeconds = 0.32f,
            BlendMode = SpriteBlendMode.Subtractive,
        });

        // The mandatory mutation: removing the serializer's FadeScreen case makes the loaded action
        // fall through to UnknownCutsceneActionData, silently dropping every field - this must fail.
        var action = Assert.IsType<FadeScreenCutsceneActionData>(reloaded.RootAction);
        Assert.Equal(10, action.R);
        Assert.Equal(20, action.G);
        Assert.Equal(30, action.B);
        Assert.Equal(0.32f, action.DurationSeconds, 4);
        Assert.Equal(SpriteBlendMode.Subtractive, action.BlendMode);
    }

    [Fact]
    public void FadeScreen_DefaultsToAdditiveWhenTheBlendModeFieldIsAbsent()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "c",
            ["root_action"] = new JObject
            {
                ["type"] = CutsceneActionTypes.FadeScreen,
                ["r"] = 1,
                ["g"] = 2,
                ["b"] = 3,
                ["duration_seconds"] = 1f,
            },
        };

        var asset = new CutsceneAsset();
        asset.Load(document);

        var action = Assert.IsType<FadeScreenCutsceneActionData>(asset.RootAction);
        Assert.Equal(SpriteBlendMode.Additive, action.BlendMode);
    }

    [Fact]
    public void Validate_AcceptsAWellFormedFadeScreen()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new FadeScreenCutsceneActionData { DurationSeconds = 1f },
        };

        Assert.True(CutsceneValidator.Validate(asset).IsValid);
    }

    [Fact]
    public void Validate_RejectsANegativeDuration()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new FadeScreenCutsceneActionData { DurationSeconds = -1f },
        };

        var result = CutsceneValidator.Validate(asset);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Message.Contains("FadeScreen.duration_seconds"));
    }
}
