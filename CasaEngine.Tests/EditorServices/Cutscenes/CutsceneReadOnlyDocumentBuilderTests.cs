using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scripting.Coroutines;
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
}