using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>The ShowSprite and HideSprite cutscene actions: JSON, validation, execution and failures.</summary>
public sealed class ShowHideSpriteCutsceneActionsTests
{
    // ---- JSON ----

    [Fact]
    public void SaveLoad_RoundTripsShowAndHide()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new ShowSpriteCutsceneActionData
                    {
                        EntityName = "Star",
                        Sprite = "star-sprite",
                        X = 12.5f,
                        Y = -4f,
                        Color = new Color(1, 2, 3, 4),
                        BlendMode = SpriteBlendMode.Additive,
                    },
                    new HideSpriteCutsceneActionData { EntityName = "Star" },
                },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        var show = Assert.IsType<ShowSpriteCutsceneActionData>(root.Actions[0]);
        Assert.Equal("Star", show.EntityName);
        Assert.Equal("star-sprite", show.Sprite);
        Assert.Equal(12.5f, show.X);
        Assert.Equal(-4f, show.Y);
        Assert.Equal(new Color(1, 2, 3, 4), show.Color);
        Assert.Equal(SpriteBlendMode.Additive, show.BlendMode);
        Assert.Equal("Star", Assert.IsType<HideSpriteCutsceneActionData>(root.Actions[1]).EntityName);
        Assert.True(loaded.Validate().IsValid);
        Assert.Equal("ShowSprite", node["root_action"]!["actions"]![0]!["type"]!.ToString());
        Assert.Equal(4, ((JArray)node["root_action"]!["actions"]![0]!["color"]!).Count);
    }

    [Fact]
    public void Load_OfAThreeEntryColor_ReadsAlpha255()
    {
        var show = Assert.IsType<ShowSpriteCutsceneActionData>(
            LoadAction("{\"type\":\"ShowSprite\",\"entity\":\"A\",\"color\":[10,20,30]}"));

        Assert.Equal(new Color(10, 20, 30, 255), show.Color);
    }

    [Fact]
    public void Load_OfOnlyTheEntity_LeavesOptionalFieldsNull()
    {
        var show = Assert.IsType<ShowSpriteCutsceneActionData>(LoadAction("{\"type\":\"ShowSprite\",\"entity\":\"A\"}"));

        Assert.Null(show.Sprite);
        Assert.Null(show.X);
        Assert.Null(show.Y);
        Assert.Null(show.Color);
        Assert.Null(show.BlendMode);
        Assert.Empty(ValidateAction(JObject.Parse("{\"type\":\"ShowSprite\",\"entity\":\"A\"}")));
    }

    [Fact]
    public void Load_OfIntegerAndDecimalPositions_ReadsFloats()
    {
        var show = Assert.IsType<ShowSpriteCutsceneActionData>(
            LoadAction("{\"type\":\"ShowSprite\",\"entity\":\"A\",\"x\":3,\"y\":-2.5}"));

        Assert.Equal(3f, show.X);
        Assert.Equal(-2.5f, show.Y);
    }

    // ---- Validator ----

    [Theory]
    [InlineData("ShowSprite")]
    [InlineData("HideSprite")]
    public void Validate_OfAMissingEntity_IsRefused(string type)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"{type}\"}}")));

        Assert.Equal($"{type}.entity is required.", error.Message);
    }

    [Theory]
    [InlineData("ShowSprite", "\"entity\":3")]
    [InlineData("ShowSprite", "\"entity\":\"\"")]
    [InlineData("HideSprite", "\"entity\":[]")]
    [InlineData("HideSprite", "\"entity\":\"  \"")]
    public void Validate_OfAMalformedEntity_NamesTheField(string type, string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"{type}\",{field}}}")));

        Assert.StartsWith($"{type}.entity must be a non-empty string", error.Message);
    }

    [Theory]
    [InlineData("\"color\":{\"r\":1,\"g\":2,\"b\":3,\"a\":4}")]
    [InlineData("\"color\":[1,2]")]
    [InlineData("\"color\":[1,2,3,4,5]")]
    [InlineData("\"color\":[1,2,256]")]
    [InlineData("\"color\":[-1,2,3]")]
    [InlineData("\"color\":[1,2,3.5]")]
    public void Validate_OfAMalformedColor_IsRefused(string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"ShowSprite\",\"entity\":\"A\",{field}}}")));

        Assert.StartsWith("ShowSprite.color must be [r, g, b] or [r, g, b, a], each an integer from 0 to 255", error.Message);
    }

    [Fact]
    public void Validate_OfAnUnknownBlendMode_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"ShowSprite\",\"entity\":\"A\",\"blend_mode\":\"Nope\"}")));

        Assert.StartsWith("ShowSprite.blend_mode must be one of", error.Message);
    }

    [Theory]
    [InlineData("\"x\":1")]
    [InlineData("\"y\":1")]
    public void Validate_OfXWithoutYOrTheReverse_IsRefused(string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"ShowSprite\",\"entity\":\"A\",{field}}}")));

        Assert.Equal("ShowSprite.x and ShowSprite.y must be given together.", error.Message);
    }

    [Fact]
    public void Validate_OfANonNumericPositionOrSprite_IsRefused()
    {
        var errors = ValidateAction(JObject.Parse("{\"type\":\"ShowSprite\",\"entity\":\"A\",\"x\":\"1\",\"y\":true,\"sprite\":4}"));

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, error => error.Message.StartsWith("ShowSprite.x must be a number"));
        Assert.Contains(errors, error => error.Message.StartsWith("ShowSprite.y must be a number"));
        Assert.Contains(errors, error => error.Message.StartsWith("ShowSprite.sprite must be a non-empty string"));
    }

    // ---- Execution ----

    [Fact]
    public void Show_MakesTheEntityVisibleAndAppliesItsFields_InTheSameUpdate()
    {
        var (world, entity, sprite) = CreateWorldWithSprite("Star");
        entity.IsVisible = false;
        sprite.Position = new Vector3(0, 0, 7);
        sprite.StartMove(new Vector2(100, 0), 10f);
        sprite.StartTint(Color.Red, 5f);

        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Show",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new ShowSpriteCutsceneActionData
                    {
                        EntityName = "Star",
                        X = 10f,
                        Y = 20f,
                        Color = new Color(9, 8, 7, 6),
                        BlendMode = SpriteBlendMode.Subtractive,
                    },
                },
            },
        });
        Step(world, 1);

        Assert.True(entity.IsVisible);
        Assert.Equal(new Vector3(10f, 20f, 7f), sprite.Position);
        Assert.Equal(new Color(9, 8, 7, 6), sprite.Color);
        Assert.Equal(SpriteBlendMode.Subtractive, sprite.BlendMode);
        Assert.False(sprite.IsMoving);
        Assert.False(sprite.IsTinting);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    [Fact]
    public void Show_OfOnlyTheEntity_KeepsTheOtherFields()
    {
        var (world, entity, sprite) = CreateWorldWithSprite("Star");
        entity.IsVisible = false;
        sprite.Position = new Vector3(3, 4, 5);
        sprite.Color = new Color(1, 2, 3, 4);
        sprite.BlendMode = SpriteBlendMode.Additive;

        PlayAndStep(world, new ShowSpriteCutsceneActionData { EntityName = "Star" });

        Assert.True(entity.IsVisible);
        Assert.Equal(new Vector3(3, 4, 5), sprite.Position);
        Assert.Equal(new Color(1, 2, 3, 4), sprite.Color);
        Assert.Equal(SpriteBlendMode.Additive, sprite.BlendMode);
    }

    [Fact]
    public void Hide_MakesTheEntityInvisibleAndStopsMotionAndTint()
    {
        var (world, entity, sprite) = CreateWorldWithSprite("Star");
        sprite.StartMove(new Vector2(100, 0), 10f);
        sprite.StartTint(Color.Red, 5f);

        PlayAndStep(world, new HideSpriteCutsceneActionData { EntityName = "Star" });

        Assert.False(entity.IsVisible);
        Assert.False(sprite.IsMoving);
        Assert.False(sprite.IsTinting);
        Assert.Equal(CutsceneRuntimeState.Completed, world.CutsceneDirector.GetDebugSnapshot().State);
    }

    // ---- Failures ----

    [Theory]
    [InlineData("Show")]
    [InlineData("Hide")]
    public void AnUnknownEntity_FailsThePlayNamingIt(string kind)
    {
        var (world, _, _) = CreateWorldWithSprite("Star");

        PlayAndStep(world, Action(kind, "Ghost"));

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Ghost'", snapshot.ActiveActionStopReason);
    }

    [Theory]
    [InlineData("Show")]
    [InlineData("Hide")]
    public void AnEntityWithoutASpriteRoot_FailsThePlayNamingIt(string kind)
    {
        var world = new World();
        world.Entities.Add(new Entity { Name = "Plain", RootComponent = new TestSceneComponent() });

        PlayAndStep(world, Action(kind, "Plain"));

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Plain'", snapshot.ActiveActionStopReason);
        Assert.Contains("StaticSpriteComponent", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnUnknownSprite_FailsThePlayAndChangesNothing()
    {
        var (world, entity, sprite) = CreateWorldWithSprite("Star");
        entity.IsVisible = false;

        PlayAndStep(world, new ShowSpriteCutsceneActionData { EntityName = "Star", Sprite = "missing-sprite", X = 1, Y = 2 });

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'missing-sprite'", snapshot.ActiveActionStopReason);
        Assert.False(entity.IsVisible);
        Assert.Equal(Vector3.Zero, sprite.Position);
    }

    // ---- Editor ----

    [Fact]
    public void TheReadOnlyBuilder_ShowsBothActionsAndTheirFields()
    {
        var asset = new CutsceneAsset
        {
            Name = "Sprites",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new ShowSpriteCutsceneActionData { EntityName = "Star", X = 1.5f, Y = 2f, Color = new Color(1, 2, 3, 4), BlendMode = SpriteBlendMode.Additive },
                    new HideSpriteCutsceneActionData { EntityName = "Star" },
                },
            },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        var show = document.RootAction.Children[0];
        Assert.Equal(CutsceneActionTypes.ShowSprite, show.Type);
        Assert.Equal("Star", Property(show, "entity"));
        Assert.Equal("1.5", Property(show, "x"));
        Assert.Equal("1, 2, 3, 4", Property(show, "color"));
        Assert.Equal("Additive", Property(show, "blend_mode"));
        var hide = document.RootAction.Children[1];
        Assert.Equal(CutsceneActionTypes.HideSprite, hide.Type);
        Assert.Equal("Star", Property(hide, "entity"));
    }

    // ---- Helpers ----

    private static string Property(CutsceneReadOnlyActionNode node, string name)
        => node.Properties.Single(property => property.Name == name).Value;

    private static CutsceneActionData Action(string kind, string entity)
        => kind == "Show"
            ? new ShowSpriteCutsceneActionData { EntityName = entity }
            : new HideSpriteCutsceneActionData { EntityName = entity };

    private static (World World, Entity Entity, StaticSpriteComponent Sprite) CreateWorldWithSprite(string name)
    {
        var world = new World();
        var sprite = new StaticSpriteComponent();
        var entity = new Entity { Name = name, RootComponent = sprite };
        world.Entities.Add(entity);
        return (world, entity, sprite);
    }

    private static void PlayAndStep(World world, CutsceneActionData root)
    {
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Sprites", RootAction = root });
        Step(world, 1);
    }

    private static void Step(World world, long update)
        => world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, update));

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

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
