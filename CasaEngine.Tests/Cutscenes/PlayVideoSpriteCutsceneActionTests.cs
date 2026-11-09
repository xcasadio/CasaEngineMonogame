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

/// <summary>The PlayVideoSprite cutscene action without a graphics device: JSON, validation, editor builder and failures.</summary>
public sealed class PlayVideoSpriteCutsceneActionTests
{
    [Fact]
    public void SaveLoad_RoundTripsTheAction()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions = { new PlayVideoSpriteCutsceneActionData { EntityName = "Film" } },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Film", Assert.IsType<PlayVideoSpriteCutsceneActionData>(root.Actions[0]).EntityName);
        Assert.Equal("PlayVideoSprite", node["root_action"]!["actions"]![0]!["type"]!.ToString());
        Assert.True(loaded.Validate().IsValid);
    }

    [Fact]
    public void Validate_OfAMissingEntity_IsRefused()
    {
        var error = Assert.Single(ValidateAction(JObject.Parse("{\"type\":\"PlayVideoSprite\"}")));

        Assert.Equal("PlayVideoSprite.entity is required.", error.Message);
    }

    [Theory]
    [InlineData("\"entity\":3")]
    [InlineData("\"entity\":\"\"")]
    [InlineData("\"entity\":\"  \"")]
    [InlineData("\"entity\":[]")]
    public void Validate_OfAMalformedEntity_NamesTheField(string field)
    {
        var error = Assert.Single(ValidateAction(JObject.Parse($"{{\"type\":\"PlayVideoSprite\",{field}}}")));

        Assert.StartsWith("PlayVideoSprite.entity must be a non-empty string", error.Message);
    }

    [Fact]
    public void TheReadOnlyBuilder_ShowsTheAction()
    {
        var asset = new CutsceneAsset
        {
            Name = "Film",
            RootAction = new PlayVideoSpriteCutsceneActionData { EntityName = "Film" },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        Assert.Equal(CutsceneActionTypes.PlayVideoSprite, document.RootAction.Type);
        Assert.Equal("Film", document.RootAction.Properties.Single(property => property.Name == "entity").Value);
    }

    [Fact]
    public void AnUnknownEntity_FailsThePlayNamingIt()
    {
        var world = new World();

        PlayAndStep(world, "Ghost");

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Ghost'", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnEntityWithoutAVideoRoot_FailsThePlayNamingIt()
    {
        var world = new World();
        world.Entities.Add(new Entity { Name = "Plain", RootComponent = new StaticSpriteComponent() });

        PlayAndStep(world, "Plain");

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Plain'", snapshot.ActiveActionStopReason);
        Assert.Contains("VideoSpriteComponent", snapshot.ActiveActionStopReason);
    }

    [Fact]
    public void AnUninitializedVideoComponent_FailsThePlayNamingTheEntity_AndShowsNothing()
    {
        var world = new World();
        var entity = new Entity { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = "film" } };
        entity.IsVisible = false;
        world.Entities.Add(entity);

        PlayAndStep(world, "Film");

        var snapshot = world.CutsceneDirector.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("'Film'", snapshot.ActiveActionStopReason);
        Assert.False(entity.IsVisible);
    }

    private static void PlayAndStep(World world, string entity)
    {
        world.CutsceneDirector.Play(new CutsceneAsset
        {
            Name = "Film",
            RootAction = new PlayVideoSpriteCutsceneActionData { EntityName = entity },
        });
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
