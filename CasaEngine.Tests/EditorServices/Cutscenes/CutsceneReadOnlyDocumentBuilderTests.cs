using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneReadOnlyDocumentBuilderTests
{
    [Fact]
    public void Build_CreatesReadOnlyActionTreeAndValidationMessages()
    {
        var asset = new CutsceneAsset
        {
            Name = "Intro",
            FileName = "Cutscenes/intro.cutscene",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 1.25f },
                    new ParallelCutsceneActionData(),
                },
            },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        Assert.False(document.CanEdit);
        Assert.Equal("Intro", document.AssetName);
        Assert.Equal("Cutscenes/intro.cutscene", document.AssetFileName);
        Assert.NotNull(document.RootAction);
        Assert.Equal(CutsceneActionTypes.Sequence, document.RootAction.Type);
        Assert.Equal(2, document.RootAction.Children.Count);
        Assert.Equal(CutsceneActionTypes.Wait, document.RootAction.Children[0].Type);
        Assert.Equal("seconds", document.RootAction.Children[0].Properties[0].Name);
        Assert.Equal("1.25", document.RootAction.Children[0].Properties[0].Value);
        Assert.Single(document.ValidationMessages);
        Assert.Equal(CutsceneValidationSeverity.Warning, document.ValidationMessages[0].Severity);
    }

    [Fact]
    public void Build_CopiesRuntimeCoroutineSnapshot()
    {
        var asset = new CutsceneAsset
        {
            Name = "Runtime",
            RootAction = new WaitCutsceneActionData { Seconds = 0.5f },
        };
        var runtimeSnapshot = new CutsceneDebugSnapshot(
            CutsceneRuntimeState.Playing,
            asset.AssetId,
            asset.Name,
            asset.FileName,
            CoroutineHandle.Invalid,
            Array.Empty<CutsceneValidationMessage>(),
            new[]
            {
                new CoroutineDebugInfo
                {
                    Id = 7,
                    Name = "Cutscene:Runtime",
                    OwnerName = "CutsceneDirector",
                    CurrentInstruction = "WaitForSeconds",
                    State = "Running",
                    RemainingTime = 0.25f,
                },
            });

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset, runtimeSnapshot);

        Assert.Equal(CutsceneRuntimeState.Playing, document.RuntimeState);
        Assert.Single(document.ActiveCoroutines);
        Assert.Equal(7, document.ActiveCoroutines[0].Id);
        Assert.Equal("WaitForSeconds", document.ActiveCoroutines[0].CurrentInstruction);
    }

    [Fact]
    public void Build_IncludesNavigateToActionAndRuntimeActionState()
    {
        var asset = new CutsceneAsset
        {
            Name = "Navigate",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2f, 0f, 4f),
                StoppingDistance = 0.2f,
                TimeoutSeconds = 6f,
            },
        };
        var runtimeSnapshot = new CutsceneDebugSnapshot(
            CutsceneRuntimeState.Playing,
            asset.AssetId,
            asset.Name,
            asset.FileName,
            CoroutineHandle.Invalid,
            Array.Empty<CutsceneValidationMessage>(),
            Array.Empty<CoroutineDebugInfo>(),
            CutsceneActionTypes.NavigateTo,
            "Hero",
            new Vector3(2f, 0f, 4f),
            "Moving",
            string.Empty);

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset, runtimeSnapshot);

        Assert.Equal(CutsceneActionTypes.NavigateTo, document.RootAction!.Type);
        Assert.Contains(document.RootAction.Properties, property => property.Name == "destination" && property.Value == "2, 0, 4");
        Assert.Equal(CutsceneActionTypes.NavigateTo, document.ActiveActionType);
        Assert.Equal("Hero", document.ActiveActionEntityName);
        Assert.Equal(new Vector3(2f, 0f, 4f), document.ActiveActionDestination);
        Assert.Equal("Moving", document.ActiveActionState);
    }

    [Fact]
    public void Build_ShowsTheFieldsOfTheFrameActions()
    {
        var asset = new CutsceneAsset
        {
            Name = "Frames",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitFramesCutsceneActionData { Frames = 6 },
                    new FadeScreenFramesCutsceneActionData
                    {
                        From = new Color(1, 2, 3, 4),
                        To = new Color(5, 6, 7, 8),
                        Frames = 9,
                        Layer = CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectLayer.AboveUI,
                        BlendMode = CasaEngine.Framework.Rendering.Depth.SpriteBlendMode.AlphaBlend,
                    },
                    new FadeScreenFramesCutsceneActionData(),
                },
            },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        var wait = document.RootAction.Children[0];
        Assert.Equal(CutsceneActionTypes.WaitFrames, wait.Type);
        Assert.Equal("frames", wait.Properties[0].Name);
        Assert.Equal("6", wait.Properties[0].Value);

        var fade = document.RootAction.Children[1];
        Assert.Equal(CutsceneActionTypes.FadeScreenFrames, fade.Type);
        Assert.Equal(
            new[] { "from=1, 2, 3, 4", "to=5, 6, 7, 8", "frames=9", "layer=AboveUI", "blend_mode=AlphaBlend" },
            fade.Properties.Select(property => $"{property.Name}={property.Value}").ToArray());

        var empty = document.RootAction.Children[2];
        Assert.All(empty.Properties, property => Assert.Equal(string.Empty, property.Value));
        Assert.Equal(5, document.ValidationMessages.Count(message => message.Severity == CutsceneValidationSeverity.Error));
    }

    [Fact]
    public void Build_ShowsTheTargetOfTheOpenScreenAndChangeWorldActions()
    {
        var asset = new CutsceneAsset
        {
            Name = "Chain",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new OpenScreenCutsceneActionData { Target = "Intro" },
                    new ChangeWorldCutsceneActionData { Target = "second.world" },
                    new ChangeWorldCutsceneActionData(),
                },
            },
        };

        var document = CutsceneReadOnlyDocumentBuilder.Build(asset);

        var openScreen = document.RootAction.Children[0];
        Assert.Equal(CutsceneActionTypes.OpenScreen, openScreen.Type);
        Assert.Equal("target=Intro", $"{openScreen.Properties[0].Name}={openScreen.Properties[0].Value}");
        var changeWorld = document.RootAction.Children[1];
        Assert.Equal(CutsceneActionTypes.ChangeWorld, changeWorld.Type);
        Assert.Equal("target=second.world", $"{changeWorld.Properties[0].Name}={changeWorld.Properties[0].Value}");
        Assert.Equal(string.Empty, document.RootAction.Children[2].Properties[0].Value);
        Assert.Single(document.ValidationMessages, message => message.Severity == CutsceneValidationSeverity.Error);
    }
}
