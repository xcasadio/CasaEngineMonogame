using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneActionTreePathTests
{
    // root Sequence [ Wait, Parallel [ Wait, Repeat [ HideSprite ] ], MoveSprite ]
    private static SequenceCutsceneActionData BuildTree(out RepeatCutsceneActionData repeat)
    {
        var root = new SequenceCutsceneActionData();
        var parallel = new ParallelCutsceneActionData();
        repeat = new RepeatCutsceneActionData { Count = 2 };
        repeat.Actions.Add(new HideSpriteCutsceneActionData { EntityName = "orb" });
        parallel.Actions.Add(new WaitCutsceneActionData { Seconds = 1 });
        parallel.Actions.Add(repeat);
        root.Actions.Add(new WaitCutsceneActionData { Seconds = 2 });
        root.Actions.Add(parallel);
        root.Actions.Add(new MoveSpriteCutsceneActionData { EntityName = "orb", X = 1, Y = 2, Speed = 3 });
        return root;
    }

    [Fact]
    public void TryResolvePath_IsTheInverseOfGetPath_ForEveryAction()
    {
        var root = BuildTree(out var repeat);

        Assert.True(CutsceneActionTree.TryResolvePath(root, "root_action", out var found));
        Assert.Same(root, found);

        Assert.True(CutsceneActionTree.TryResolvePath(root, "root_action.actions[1].actions[1]", out found));
        Assert.Same(repeat, found);
        Assert.Equal("root_action.actions[1].actions[1]", CutsceneActionTree.GetPath(root, found));

        Assert.True(CutsceneActionTree.TryResolvePath(root, "root_action.actions[2]", out found));
        Assert.IsType<MoveSpriteCutsceneActionData>(found);
    }

    [Theory]
    [InlineData("")]
    [InlineData("actions[0]")]
    [InlineData("root_action.actions[9]")]
    [InlineData("root_action.actions[x]")]
    [InlineData("root_action.actions[0")]
    [InlineData("root_action.actions[0].actions[0]")]
    [InlineData("root_action.children[0]")]
    [InlineData("root_action.actions[-1]")]
    public void TryResolvePath_RefusesAMalformedPathOrOneOutsideTheTree(string path)
    {
        var root = BuildTree(out _);

        Assert.False(CutsceneActionTree.TryResolvePath(root, path, out _));
    }

    [Fact]
    public void CountActions_CountsTheRootAndEveryDescendant()
    {
        var root = BuildTree(out _);

        Assert.Equal(7, CutsceneActionTree.CountActions(root));
    }
}
