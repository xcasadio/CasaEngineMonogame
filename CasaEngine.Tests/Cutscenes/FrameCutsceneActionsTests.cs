using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The frame-counted cutscene actions: JSON, validation, and the frame by frame timing contract (a fade of N
/// frames reaches its target at update N, the next action runs at update N + 1 counting from the update that
/// ran the fade).
/// </summary>
public sealed class FrameCutsceneActionsTests
{
    private const byte WitnessMarker = 9;

    // ---- JSON ----

    [Fact]
    public void SaveLoad_RoundTripsBothActions()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new FadeScreenFramesCutsceneActionData
                    {
                        From = new Color(1, 2, 3, 4),
                        To = new Color(5, 6, 7, 255),
                        Frames = 12,
                        Layer = ScreenEffectLayer.AboveUI,
                        BlendMode = SpriteBlendMode.AlphaBlend,
                    },
                    new WaitFramesCutsceneActionData { Frames = 7 },
                },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        var fade = Assert.IsType<FadeScreenFramesCutsceneActionData>(root.Actions[0]);
        Assert.Equal(new Color(1, 2, 3, 4), fade.From);
        Assert.Equal(new Color(5, 6, 7, 255), fade.To);
        Assert.Equal(12, fade.Frames);
        Assert.Equal(ScreenEffectLayer.AboveUI, fade.Layer);
        Assert.Equal(SpriteBlendMode.AlphaBlend, fade.BlendMode);
        Assert.Empty(fade.FormatErrors);
        Assert.Equal(7, Assert.IsType<WaitFramesCutsceneActionData>(root.Actions[1]).Frames);
        Assert.True(loaded.Validate().IsValid);
    }

    [Fact]
    public void Load_ReadsTheDocumentedShapes_AndAnOpaqueColourWithThreeChannels()
    {
        var action = LoadAction("{\"type\":\"FadeScreenFrames\",\"from\":[10,20,30],\"to\":[0,0,0,0],\"frames\":4,\"layer\":\"AboveUI\",\"blend_mode\":\"AlphaBlend\"}");

        var fade = Assert.IsType<FadeScreenFramesCutsceneActionData>(action);
        Assert.Equal(new Color(10, 20, 30, 255), fade.From);
        Assert.Equal(new Color(0, 0, 0, 0), fade.To);
    }

    [Fact]
    public void Load_OfAnAbsentField_LeavesItNull()
    {
        var wait = Assert.IsType<WaitFramesCutsceneActionData>(LoadAction("{\"type\":\"WaitFrames\"}"));
        var fade = Assert.IsType<FadeScreenFramesCutsceneActionData>(LoadAction("{\"type\":\"FadeScreenFrames\"}"));

        Assert.Null(wait.Frames);
        Assert.Null(fade.From);
        Assert.Null(fade.To);
        Assert.Null(fade.Frames);
        Assert.Null(fade.Layer);
        Assert.Null(fade.BlendMode);
    }

    // ---- Validator ----

    [Theory]
    [InlineData("frames")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("layer")]
    [InlineData("blend_mode")]
    public void Validate_OfAFadeWithAFieldLeftOut_NamesTheActionAndTheField(string missing)
    {
        var node = ValidFade();
        node.Remove(missing);

        var error = Assert.Single(ValidateAction(node));
        Assert.Equal($"FadeScreenFrames.{missing} is required.", error.Message);
    }

    [Fact]
    public void Validate_OfAWaitWithoutFrames_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"WaitFrames\"}")));

        Assert.Equal("WaitFrames.frames is required.", error.Message);
    }

    [Theory]
    [InlineData("\"from\"", "[1,2]")]
    [InlineData("\"from\"", "[1,2,3,4,5]")]
    [InlineData("\"from\"", "[1,2,256]")]
    [InlineData("\"from\"", "[1,2,-1]")]
    [InlineData("\"from\"", "[1,2,1.5]")]
    [InlineData("\"from\"", "[1,2,\"3\"]")]
    [InlineData("\"to\"", "\"red\"")]
    [InlineData("\"layer\"", "\"Above\"")]
    [InlineData("\"layer\"", "1")]
    [InlineData("\"blend_mode\"", "\"alphablend\"")]
    [InlineData("\"frames\"", "2.5")]
    [InlineData("\"frames\"", "\"4\"")]
    [InlineData("\"frames\"", "3000000000")]
    public void Validate_OfAMalformedField_IsRefused(string key, string value)
    {
        var node = ValidFade();
        node[key.Trim('"')] = JToken.Parse(value);

        var error = Assert.Single(ValidateAction(node));
        Assert.StartsWith($"FadeScreenFrames.{key.Trim('"')} must be", error.Message);
    }

    [Fact]
    public void Validate_OfAMalformedWaitFrames_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"WaitFrames\",\"frames\":1.5}")));

        Assert.StartsWith("WaitFrames.frames must be an integer", error.Message);
    }

    [Fact]
    public void Validate_OfNegativeFrames_IsRefused()
    {
        var wait = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"WaitFrames\",\"frames\":-1}")));
        var fadeNode = ValidFade();
        fadeNode["frames"] = -3;
        var fade = Assert.Single(ValidateAction(fadeNode));

        Assert.Equal("WaitFrames.frames must be greater than or equal to zero.", wait.Message);
        Assert.Equal("FadeScreenFrames.frames must be greater than or equal to zero.", fade.Message);
    }

    [Fact]
    public void Validate_OfAZeroCount_IsAccepted()
    {
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"WaitFrames\",\"frames\":0}")));
    }

    // ---- Timing (contract C2) ----

    [Fact]
    public void FadeThenWaitThenWitness_FollowsTheFrameContract_UpdateByUpdate()
    {
        var harness = new Harness();
        harness.Play(new SequenceCutsceneActionData
        {
            Actions =
            {
                Fade(from: new Color(0, 0, 0, 255), to: new Color(0, 0, 0, 0), frames: 4, ScreenEffectLayer.AboveUI),
                new WaitFramesCutsceneActionData { Frames = 3 },
                Witness(),
            },
        });

        Assert.Equal(ScreenEffectLayer.BelowUI, harness.Service.Layer);

        for (int update = 1; update <= 9; update++)
        {
            harness.CoroutineStep(update);

            if (update == 1)
            {
                // The layer is set before the first value is drawn, which is the ramp's first step.
                Assert.Equal(ScreenEffectLayer.AboveUI, harness.Service.Layer);
                Assert.Equal(255, harness.Service.A);
            }

            harness.Service.Update(0.1f);

            int k = Math.Min(update, 4);
            byte expectedAlpha = (byte)MathF.Round(255f + ((0f - 255f) * (k / 4f)));
            Assert.Equal(expectedAlpha, harness.Service.A);
            Assert.Equal(update < 4, harness.Service.IsFading);

            // The witness runs at update 8: fade at 1, target at 4, wait starts at 5 and the next action 3 updates later.
            Assert.Equal(update >= 8 ? WitnessMarker : 0, harness.Service.R);
        }

        Assert.Equal(CutsceneRuntimeState.Completed, harness.World.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Fade_ReachesItsTargetExactlyAtUpdateN_AndTheNextActionRunsAtUpdateNPlusOne()
    {
        var harness = new Harness();
        harness.Play(new SequenceCutsceneActionData
        {
            Actions =
            {
                Fade(from: new Color(0, 0, 0, 255), to: new Color(80, 0, 0, 0), frames: 4, ScreenEffectLayer.BelowUI),
                Witness(),
            },
        });

        for (int update = 1; update <= 5; update++)
        {
            harness.CoroutineStep(update);
            harness.Service.Update(0.1f);

            if (update < 4)
            {
                Assert.True(harness.Service.IsFading);
                Assert.NotEqual(0, harness.Service.A);
            }

            // The red channel climbs to 80 at update 4; the witness then replaces it with its marker at update 5.
            if (update == 4)
            {
                Assert.Equal(80, harness.Service.R);
                Assert.Equal(0, harness.Service.A);
            }

            if (update == 5)
            {
                Assert.Equal(WitnessMarker, harness.Service.R);
            }
        }
    }

    [Fact]
    public void WaitFramesOfZero_RunsTheNextActionInTheSameUpdate()
    {
        var harness = new Harness();
        harness.Play(new SequenceCutsceneActionData
        {
            Actions = { new WaitFramesCutsceneActionData { Frames = 0 }, Witness() },
        });

        harness.CoroutineStep(1);

        Assert.Equal(WitnessMarker, harness.Service.R);
    }

    [Fact]
    public void WaitFramesOfOne_RunsTheNextActionAtTheFollowingUpdate()
    {
        var harness = new Harness();
        harness.Play(new SequenceCutsceneActionData
        {
            Actions = { new WaitFramesCutsceneActionData { Frames = 1 }, Witness() },
        });

        harness.CoroutineStep(1);
        Assert.Equal(0, harness.Service.R);
        harness.CoroutineStep(2);
        Assert.Equal(WitnessMarker, harness.Service.R);
    }

    [Fact]
    public void FadeOfZeroFrames_SetsTheTargetAtOnce_AndTheNextActionRunsInTheSameUpdate()
    {
        var harness = new Harness();
        harness.Play(new SequenceCutsceneActionData
        {
            Actions =
            {
                Fade(from: new Color(0, 0, 0, 255), to: new Color(0, 0, 0, 77), frames: 0, ScreenEffectLayer.AboveUI),
                Witness(),
            },
        });

        harness.CoroutineStep(1);

        Assert.Equal(WitnessMarker, harness.Service.R);
        Assert.Equal(ScreenEffectLayer.AboveUI, harness.Service.Layer);
    }

    [Fact]
    public void FadeIgnoresTheElapsedTime()
    {
        var harness = new Harness();
        harness.Play(Fade(from: new Color(0, 0, 0, 200), to: new Color(0, 0, 0, 0), frames: 2, ScreenEffectLayer.BelowUI));

        harness.CoroutineStep(1);
        harness.Service.Update(100f);
        Assert.Equal(100, harness.Service.A);
        harness.CoroutineStep(2);
        harness.Service.Update(0f);
        Assert.Equal(0, harness.Service.A);
    }

    [Fact]
    public void WithoutAScreenEffectService_BothActionsEndAtOnce()
    {
        var world = new World();
        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "NoService",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    Fade(from: new Color(0, 0, 0, 255), to: new Color(0, 0, 0, 0), frames: 10, ScreenEffectLayer.AboveUI),
                    new WaitFramesCutsceneActionData { Frames = 0 },
                },
            },
        });

        world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, 1));
        world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, 2));

        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    // ---- Helpers ----

    private static FadeScreenFramesCutsceneActionData Fade(Color from, Color to, int frames, ScreenEffectLayer layer)
        => new()
        {
            From = from,
            To = to,
            Frames = frames,
            Layer = layer,
            BlendMode = SpriteBlendMode.AlphaBlend,
        };

    /// <summary>An instant fade that paints the red channel with a marker: it shows the update it ran in.</summary>
    private static FadeScreenFramesCutsceneActionData Witness()
        => Fade(new Color((int)WitnessMarker, 0, 0, 0), new Color((int)WitnessMarker, 0, 0, 0), 0, ScreenEffectLayer.AboveUI);

    private static JObject ValidFade()
        => JObject.Parse("{\"type\":\"FadeScreenFrames\",\"from\":[0,0,0,255],\"to\":[0,0,0,0],\"frames\":4,\"layer\":\"AboveUI\",\"blend_mode\":\"AlphaBlend\"}");

    private static CutsceneActionData LoadAction(string json)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = JObject.Parse(json) };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.RootAction;
    }

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = action };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.Validate().Messages;
    }

    private sealed class Harness
    {
        public Harness()
        {
            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(componentsField);
            componentsField!.SetValue(game, new GameComponentCollection());

            var component = new ScreenEffectComponent(game);
            SetBackingField(game, nameof(CasaEngineGame.ScreenEffectComponent), component);

            World = new World();
            SetBackingField(World, nameof(World.Game), game);
            Service = component.Service;
        }

        public World World { get; }

        public ScreenEffectService Service { get; }

        public void Play(CutsceneActionData root)
        {
            World.CutsceneDirector.Play(new CutsceneAsset { Name = "Frames", RootAction = root });
            Assert.True(World.CutsceneDirector.IsPlaying);
        }

        /// <summary>The world's coroutine update of one game update. The caller then steps the service.</summary>
        public void CoroutineStep(long update)
            => World.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, update));

        private static void SetBackingField(object instance, string propertyName, object value)
        {
            var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(instance, value);
        }
    }
}
