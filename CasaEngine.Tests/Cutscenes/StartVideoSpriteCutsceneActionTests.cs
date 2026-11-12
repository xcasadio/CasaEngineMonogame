using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The StartVideoSprite cutscene action (ADR-0093) without a graphics device: JSON, validation, editor builder and
/// failures; and the failure messages of PlayVideoSprite, which keep their exact text.
/// </summary>
public sealed class StartVideoSpriteCutsceneActionTests
{
    [Fact]
    public void SaveLoad_RoundTripsTheAction()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions = { new StartVideoSpriteCutsceneActionData { EntityName = "Film" } },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Film", Assert.IsType<StartVideoSpriteCutsceneActionData>(root.Actions[0]).EntityName);
        var saved = (JObject)node["root_action"]!["actions"]![0]!;
        Assert.Equal("StartVideoSprite", saved["type"]!.ToString());
        Assert.Equal("Film", saved["entity"]!.ToString());
        Assert.True(loaded.Validate().IsValid);
    }

    [Fact]
    public void Validate_OfAMissingEntity_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"StartVideoSprite\"}")));

        Assert.Equal("StartVideoSprite.entity is required.", error.Message);
    }

    [Theory]
    [InlineData("\"entity\":3")]
    [InlineData("\"entity\":\"\"")]
    [InlineData("\"entity\":\"  \"")]
    [InlineData("\"entity\":[]")]
    public void Validate_OfAMalformedEntity_NamesTheField(string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"StartVideoSprite\",{field}}}")));

        Assert.StartsWith("StartVideoSprite.entity must be a non-empty string", error.Message);
    }

    [Fact]
    public void TheReadOnlyBuilder_ShowsTheAction()
    {
        var asset = new CutsceneAsset
        {
            Name = "Film",
            RootAction = new StartVideoSpriteCutsceneActionData { EntityName = "Film" },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        Assert.Equal(CutsceneActionTypes.StartVideoSprite, document.RootAction.Type);
        Assert.Equal("Film", document.RootAction.Properties.Single(property => property.Name == "entity").Value);
    }

    [Fact]
    public void AnUnknownEntity_FailsThePlayNamingIt()
    {
        var world = new World();

        PlayAndStep(world, new StartVideoSpriteCutsceneActionData { EntityName = "Ghost" });

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Equal("StartVideoSprite: entity 'Ghost' was not found in the world.", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnEntityWithoutAVideoRoot_FailsThePlayNamingIt()
    {
        var world = new World();
        world.Entities.Add(new Entity { Name = "Plain", RootComponent = new StaticSpriteComponent() });

        PlayAndStep(world, new StartVideoSpriteCutsceneActionData { EntityName = "Plain" });

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Equal("StartVideoSprite: the root component of entity 'Plain' is not a VideoSpriteComponent.", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnUninitializedVideoComponent_FailsThePlayNamingTheEntity_AndShowsNothing()
    {
        var world = new World();
        var entity = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = "film" } };
        entity.IsVisible = false;
        world.Entities.Add(entity);

        PlayAndStep(world, new StartVideoSpriteCutsceneActionData { EntityName = "Film" });

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Equal("StartVideoSprite: the video of entity 'Film' is not loaded (the component was not initialized in a world).", snapshot.ActiveActionStopReason);
        Assert.False(entity.IsVisible);
    }

    [Fact]
    public void TheFailureMessagesOfPlayVideoSprite_KeepTheirText()
    {
        var unknown = new World();
        PlayAndStep(unknown, new PlayVideoSpriteCutsceneActionData { EntityName = "Ghost" });

        var plain = new World();
        plain.Entities.Add(new Entity { Name = "Plain", RootComponent = new StaticSpriteComponent() });
        PlayAndStep(plain, new PlayVideoSpriteCutsceneActionData { EntityName = "Plain" });

        var uninitialized = new World();
        uninitialized.Entities.Add(new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = "film" } });
        PlayAndStep(uninitialized, new PlayVideoSpriteCutsceneActionData { EntityName = "Film" });

        Assert.Equal("PlayVideoSprite: entity 'Ghost' was not found in the world.", unknown.CutsceneDirector.GetDebugSnapshot().ActiveActionStopReason);
        Assert.Equal("PlayVideoSprite: the root component of entity 'Plain' is not a VideoSpriteComponent.", plain.CutsceneDirector.GetDebugSnapshot().ActiveActionStopReason);
        Assert.Equal("PlayVideoSprite: the video of entity 'Film' is not loaded (the component was not initialized in a world).", uninitialized.CutsceneDirector.GetDebugSnapshot().ActiveActionStopReason);
    }

    private static void PlayAndStep(World world, CutsceneActionData action)
    {
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Film", RootAction = action });
        world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, 1));
    }

    private static IReadOnlyList<CutsceneValidationMessage> ValidateAction(JObject action)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = action };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);
        return asset.Validate().Messages;
    }
}
