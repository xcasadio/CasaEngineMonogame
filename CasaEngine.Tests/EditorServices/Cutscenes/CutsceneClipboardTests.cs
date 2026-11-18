using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneClipboardTests
{
    // root Sequence [ Wait(1), Parallel [ Wait(2), Wait(3) ] ]
    private sealed class Tree
    {
        public readonly SequenceCutsceneActionData Root = new();
        public readonly ParallelCutsceneActionData Parallel = new();
        public readonly WaitCutsceneActionData A = new() { Seconds = 1 };
        public readonly WaitCutsceneActionData B = new() { Seconds = 2 };
        public readonly WaitCutsceneActionData C = new() { Seconds = 3 };
        public readonly CutsceneEditSession Session;

        public Tree()
        {
            Parallel.Actions.AddRange([B, C]);
            Root.Actions.AddRange([A, Parallel]);
            Session = new CutsceneEditSession(new CutsceneAsset { RootAction = Root });
        }
    }

    private static string Serialize(CutsceneActionData action)
    {
        var asset = new CutsceneAsset { RootAction = action };
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, node);
        return node["root_action"]!.ToString();
    }

    // A file whose Wait and Parallel hold values the loader rewrites: each gives a load loss.
    private static CutsceneEditSession LoadWithLosses()
    {
        var file = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test",
            ["type"] = nameof(CutsceneAsset),
            ["version"] = 1,
            ["root_action"] = new JObject
            {
                ["type"] = "Sequence",
                ["actions"] = new JArray
                {
                    new JObject { ["type"] = "Wait", ["seconds"] = 1 },
                    new JObject
                    {
                        ["type"] = "Parallel",
                        ["actions"] = new JArray
                        {
                            new JObject { ["type"] = "Wait", ["seconds"] = "2" },
                            new JObject { ["type"] = "Wait", ["seconds"] = "3" },
                        },
                    },
                },
            },
        };
        return CutsceneEditSession.Load(file);
    }

    [Fact]
    public void ANewClipboardIsEmpty()
    {
        var clipboard = new CutsceneClipboard();

        Assert.False(clipboard.HasContent);
        Assert.Null(clipboard.ContentType);
        Assert.Null(clipboard.CreatePaste());
    }

    [Fact]
    public void CopyingKeepsTheTypeAndANewInstance()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();

        Assert.Null(clipboard.TryCopy(t.Session, t.A));

        Assert.True(clipboard.HasContent);
        Assert.Equal("Wait", clipboard.ContentType);
        var paste = clipboard.CreatePaste();
        Assert.NotNull(paste);
        Assert.NotSame(t.A, paste);
        Assert.Equal(Serialize(t.A), Serialize(paste!));
    }

    [Fact]
    public void TheKeptCopyDoesNotChangeWhenTheOriginalIsEditedAfterwards()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.Parallel));
        string before = Serialize(t.Parallel);

        t.B.Seconds = 99;
        t.Parallel.Actions.Add(new WaitCutsceneActionData { Seconds = 7 });

        var paste = (ParallelCutsceneActionData)clipboard.CreatePaste()!;
        Assert.Equal(before, Serialize(paste));
        Assert.Equal(2, paste.Actions.Count);
        Assert.Equal(2, ((WaitCutsceneActionData)paste.Actions[0]).Seconds);
    }

    [Fact]
    public void TwoPastesGiveDistinctInstancesThatShareNothing()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.Parallel));

        var first = (ParallelCutsceneActionData)clipboard.CreatePaste()!;
        var second = (ParallelCutsceneActionData)clipboard.CreatePaste()!;

        Assert.NotSame(first, second);
        Assert.NotSame(first.Actions, second.Actions);
        Assert.NotSame(first.Actions[0], second.Actions[0]);
        Assert.NotSame(t.Parallel, first);

        first.Actions.Clear();
        Assert.Equal(2, second.Actions.Count);
        Assert.Equal(2, ((ParallelCutsceneActionData)clipboard.CreatePaste()!).Actions.Count);
    }

    [Fact]
    public void ASubtreeIsCopiedWhole()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();

        Assert.Null(clipboard.TryCopy(t.Session, t.Parallel));

        Assert.Equal("Parallel", clipboard.ContentType);
        var paste = (ParallelCutsceneActionData)clipboard.CreatePaste()!;
        Assert.Equal(Serialize(t.Parallel), Serialize(paste));
    }

    [Fact]
    public void TheRootCanBeCopied()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();

        Assert.Null(CutsceneClipboard.GetCopyRefusal(t.Session, t.Root));
        Assert.Null(clipboard.TryCopy(t.Session, t.Root));

        Assert.Equal("Sequence", clipboard.ContentType);
        Assert.Equal(Serialize(t.Root), Serialize(clipboard.CreatePaste()!));
    }

    [Fact]
    public void ACopyReplacesThePreviousContent()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.A));

        Assert.Null(clipboard.TryCopy(t.Session, t.Parallel));

        Assert.Equal("Parallel", clipboard.ContentType);
    }

    [Fact]
    public void ClearEmptiesTheClipboard()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.A));

        clipboard.Clear();

        Assert.False(clipboard.HasContent);
        Assert.Null(clipboard.CreatePaste());
    }

    [Fact]
    public void ClipboardsAreIndependentInstances()
    {
        var t = new Tree();
        var one = new CutsceneClipboard();
        var other = new CutsceneClipboard();

        Assert.Null(one.TryCopy(t.Session, t.A));

        Assert.True(one.HasContent);
        Assert.False(other.HasContent);
    }

    [Fact]
    public void AnActionOutsideTheTreeIsRefused()
    {
        var t = new Tree();
        var clipboard = new CutsceneClipboard();

        Assert.NotNull(CutsceneClipboard.GetCopyRefusal(t.Session, new WaitCutsceneActionData()));
        Assert.NotNull(clipboard.TryCopy(t.Session, new WaitCutsceneActionData()));
        Assert.False(clipboard.HasContent);
    }

    [Fact]
    public void ACopyWithALoadLossIsRefusedAndKeepsTheClipboard()
    {
        var lossy = LoadWithLosses();
        var children = CutsceneActionCatalog.GetChildren(lossy.Asset.RootAction!);
        var cleanLeaf = children[0];
        var lossyParallel = children[1];
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(lossy, cleanLeaf));

        string? refusal = clipboard.TryCopy(lossy, lossyParallel);

        Assert.False(string.IsNullOrEmpty(refusal));
        Assert.Equal("Wait", clipboard.ContentType);
        Assert.NotNull(CutsceneClipboard.GetCopyRefusal(lossy, lossy.Asset.RootAction!));
        Assert.NotNull(CutsceneClipboard.GetCopyRefusal(lossy, CutsceneActionCatalog.GetChildren(lossyParallel)[0]));
    }

    [Fact]
    public void ACopyWithAFormatErrorIsRefusedAndKeepsTheClipboard()
    {
        var t = new Tree();
        var broken = new ShowSpriteCutsceneActionData();
        broken.FormatErrors["x"] = "not a number";
        var holder = new SequenceCutsceneActionData();
        holder.Actions.Add(broken);
        t.Root.Actions.Add(holder);
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.A));

        Assert.NotNull(clipboard.TryCopy(t.Session, broken));
        Assert.NotNull(clipboard.TryCopy(t.Session, holder));

        Assert.Equal("Wait", clipboard.ContentType);
    }

    [Fact]
    public void ACopyWithAnUnknownTypeIsRefusedAndKeepsTheClipboard()
    {
        var t = new Tree();
        var unknown = new UnknownCutsceneActionData("Dance");
        t.Root.Actions.Add(unknown);
        var clipboard = new CutsceneClipboard();
        Assert.Null(clipboard.TryCopy(t.Session, t.A));

        Assert.NotNull(clipboard.TryCopy(t.Session, unknown));
        Assert.NotNull(clipboard.TryCopy(t.Session, t.Root));

        Assert.Equal("Wait", clipboard.ContentType);
    }
}
