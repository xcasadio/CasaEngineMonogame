using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneSaveTests
{
    private const int RepositoryCutsceneCount = 14;

    public static IEnumerable<object[]> RepositoryFiles()
    {
        foreach (string path in FindRepositoryCutsceneFiles())
        {
            yield return [Path.GetRelativePath(FindRepositoryRoot(), path).Replace('\\', '/')];
        }
    }

    [Fact]
    public void TheSaveSourceOfTheCutsceneEditorIsTheNextValue()
    {
        Assert.Equal(9, (int)EditorAssetSaveSource.CutsceneEditorPanel);
    }

    [Fact]
    public void TheRepositoryHoldsTheFourteenCutsceneFiles()
    {
        var files = FindRepositoryCutsceneFiles();

        Assert.True(files.Count >= RepositoryCutsceneCount, $"Found {files.Count} .cutscene files, expected {RepositoryCutsceneCount}.");
        Assert.Equal(RepositoryCutsceneCount, files.Count);
    }

    [Theory]
    [MemberData(nameof(RepositoryFiles))]
    public void ARepositoryFile_SavesAndReloadsWithoutLoss_AndSavesTheSameTextTwice(string relativePath)
    {
        var source = JObject.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath)));

        var first = CutsceneEditSession.Load(source);
        Assert.True(EditorAssetJsonSerializer.TrySerialize(first.Asset, out var savedFirst));
        string firstText = savedFirst.ToString(Formatting.Indented);

        var second = CutsceneEditSession.Load(JObject.Parse(firstText));
        Assert.True(EditorAssetJsonSerializer.TrySerialize(second.Asset, out var savedSecond));
        string secondText = savedSecond.ToString(Formatting.Indented);

        Assert.Null(FindDifference(first.Asset, second.Asset));
        Assert.Equal(firstText, secondText);
    }

    [Theory]
    [MemberData(nameof(RepositoryFiles))]
    public void ARepositoryFile_HasNoLoadLoss_AndTheGuardAcceptsIt(string relativePath)
    {
        var source = JObject.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath)));

        var session = CutsceneEditSession.Load(source);

        Assert.Empty(session.LoadLosses);
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out string reason), reason);
    }

    [Fact]
    public void TheRepositoryFilesAreNotWritten()
    {
        var files = FindRepositoryCutsceneFiles();
        var before = files.Select(File.ReadAllBytes).ToList();

        foreach (string file in files)
        {
            var session = CutsceneEditSession.Load(JObject.Parse(File.ReadAllText(file)));
            Assert.True(EditorAssetJsonSerializer.TrySerialize(session.Asset, out _));
            _ = CutsceneSaveGuard.TryGetBlockingReason(session, out _);
        }

        for (int index = 0; index < files.Count; index++)
        {
            Assert.Equal(before[index], File.ReadAllBytes(files[index]));
        }
    }

    [Fact]
    public void TheComparisonOfTheRoundTripDetectsALoss()
    {
        var source = Root(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray(
                new JObject { ["type"] = "FadeScreen", ["r"] = 1, ["g"] = 2, ["b"] = 3, ["duration_seconds"] = 1.0, ["blend_mode"] = "Additive" },
                new JObject { ["type"] = "Wait", ["seconds"] = 0.1 }),
        });
        var first = CutsceneEditSession.Load(source);
        Assert.True(EditorAssetJsonSerializer.TrySerialize(first.Asset, out var saved));
        var second = CutsceneEditSession.Load(JObject.Parse(saved.ToString(Formatting.Indented)));
        Assert.Null(FindDifference(first.Asset, second.Asset));

        var sequence = (SequenceCutsceneActionData)first.Asset.RootAction!;
        var fade = (FadeScreenCutsceneActionData)sequence.Actions[0];
        var wait = (WaitCutsceneActionData)sequence.Actions[1];

        fade.BlendMode = Framework.Rendering.Depth.SpriteBlendMode.Opaque;
        Assert.NotNull(FindDifference(first.Asset, second.Asset));

        fade.BlendMode = Framework.Rendering.Depth.SpriteBlendMode.Additive;
        Assert.Null(FindDifference(first.Asset, second.Asset));

        wait.Seconds = 0.2f;
        Assert.NotNull(FindDifference(first.Asset, second.Asset));
    }

    [Fact]
    public void TrySerializeOfACutscene_WritesWhatTheSerializerWrites()
    {
        var session = CutsceneEditSession.Load(Root(new JObject { ["type"] = "Wait", ["seconds"] = 1.5 }));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(session.Asset, out var saved));

        Assert.Equal("CutsceneAsset", saved["type"]!.Value<string>());
        Assert.Equal(1.5f, saved["root_action"]!["seconds"]!.Value<float>());
        Assert.Equal(session.Asset.Id.ToString(), saved["id"]!.Value<string>());
    }

    [Fact]
    public void AHealthyTree_IsAcceptedByTheGuard()
    {
        var session = CutsceneEditSession.Load(Root(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray(
                new JObject { ["type"] = "Wait", ["seconds"] = 1 },
                new JObject { ["type"] = "ShowSprite", ["entity"] = "orb", ["color"] = new JArray(1, 2, 3), ["unknown_key"] = true }),
        }));

        Assert.Empty(session.LoadLosses);
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out _));
    }

    public static IEnumerable<object[]> MalformedFixtures()
    {
        yield return ["unknown action type", UnknownType(), "root_action.actions[1]", "type"];
        yield return ["format error", FormatError(), "root_action", "x"];
        yield return ["target that is not a string", TargetNotAString(), "root_action.actions[1]", "target"];
        yield return ["invalid blend_mode", InvalidBlendMode(), "root_action", "blend_mode"];
        yield return ["actions entry that is not an object", ActionsEntryNotAnObject(), "root_action", "actions"];
        yield return ["actions that is not an array", ActionsNotAnArray(), "root_action", "actions"];
        yield return ["root_action that is not an object", RootNotAnObject(), "root_action", "root_action"];
    }

    [Theory]
    [MemberData(nameof(MalformedFixtures))]
    public void AMalformedFile_IsRefusedByTheGuard_WithThePathAndTheKey(string name, JObject source, string path, string key)
    {
        var session = CutsceneEditSession.Load(source);

        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reason), name);
        Assert.Contains(path, reason);
        Assert.Contains(key, reason);
    }

    [Fact]
    public void ALoadLoss_CarriesTheActionTheKeyAndThePath()
    {
        var session = CutsceneEditSession.Load(TargetNotAString());

        var loss = Assert.Single(session.LoadLosses);
        var sequence = (SequenceCutsceneActionData)session.Asset.RootAction!;
        Assert.Same(sequence.Actions[1], loss.Action);
        Assert.Equal("target", loss.Key);
        Assert.Equal("root_action.actions[1]", loss.Path);
    }

    [Fact]
    public void AnEntryThatIsNotAnObject_IsOneLossOnTheListOfItsParent()
    {
        var session = CutsceneEditSession.Load(ActionsEntryNotAnObject());

        var loss = Assert.Single(session.LoadLosses);
        Assert.Same(session.Asset.RootAction, loss.Action);
        Assert.Equal("actions", loss.Key);
        Assert.Equal("root_action", loss.Path);
    }

    [Fact]
    public void TheEntriesOfAListThatFollowASkippedEntry_AreStillCompared()
    {
        var session = CutsceneEditSession.Load(Root(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray(5, new JObject { ["type"] = "OpenScreen", ["target"] = 7 }),
        }));

        Assert.Equal(["actions", "target"], session.LoadLosses.Select(loss => loss.Key).ToArray());
        Assert.Equal("root_action.actions[0]", session.LoadLosses[1].Path);
    }

    [Fact]
    public void AKnownKeyThatIsNullInTheFile_IsALoss()
    {
        var session = CutsceneEditSession.Load(Root(new JObject { ["type"] = "OpenScreen", ["target"] = JValue.CreateNull() }));

        var loss = Assert.Single(session.LoadLosses);
        Assert.Equal("target", loss.Key);
    }

    [Fact]
    public void FloatsAreComparedAsFloats_AndStringsWithoutCase()
    {
        var session = CutsceneEditSession.Load(Root(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray(
                new JObject { ["type"] = "Wait", ["seconds"] = 0.1 },
                new JObject { ["type"] = "FadeScreen", ["r"] = 1, ["g"] = 2, ["b"] = 3, ["duration_seconds"] = 1234.5678, ["blend_mode"] = "Additive" },
                new JObject { ["type"] = "MoveTo", ["entity"] = "hero", ["destination"] = new JObject { ["x"] = 1, ["y"] = 2, ["z"] = 3 } },
                new JObject { ["type"] = "PlaySound", ["sound_asset_id"] = Guid.NewGuid().ToString("N").ToUpperInvariant().Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-") }),
        }));

        Assert.Empty(session.LoadLosses);
    }

    [Fact]
    public void AFormatErrorField_IsAcceptedAfterAnEdit_AndRefusedAgainAfterUndo()
    {
        AssertEditableFixture(FormatError(), "x", 1.5f);
    }

    [Fact]
    public void ATargetThatIsNotAString_IsAcceptedAfterAnEdit_AndRefusedAgainAfterUndo()
    {
        AssertEditableFixture(TargetNotAString(), "target", "title", actionIndex: 1);
    }

    [Fact]
    public void AnInvalidBlendMode_IsAcceptedAfterAnEdit_AndRefusedAgainAfterUndo()
    {
        AssertEditableFixture(InvalidBlendMode(), "blend_mode", Framework.Rendering.Depth.SpriteBlendMode.Opaque);
    }

    [Fact]
    public void TheTwoFixturesOnActions_HaveNoEditableFieldAndStayRefused()
    {
        foreach (var source in new[] { ActionsEntryNotAnObject(), ActionsNotAnArray() })
        {
            var session = CutsceneEditSession.Load(source);

            Assert.Empty(CutsceneActionPropertyEditor.GetProperties(session.Asset.RootAction!));
            Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out _));
        }
    }

    [Fact]
    public void AnEmptyEntity_IsRefusedByTheReloadCheck_AndAcceptedAgainAfterUndo()
    {
        var session = CutsceneEditSession.Load(Root(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb" }),
        }));
        var action = ((SequenceCutsceneActionData)session.Asset.RootAction!).Actions[0];
        var history = new EditorHistoryStack();
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out _));

        history.Execute(new SetCutsceneActionPropertyCommand(session, action, CutsceneActionPropertyEditor.FindProperty(action, "entity")!, string.Empty));

        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out string reason));
        Assert.Contains("root_action.actions[0]", reason);
        Assert.Contains("entity", reason);

        history.Undo();

        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out reason), reason);
    }

    private static void AssertEditableFixture(JObject source, string key, object value, int actionIndex = -1)
    {
        var session = CutsceneEditSession.Load(source);
        var action = actionIndex < 0 ? session.Asset.RootAction! : ((SequenceCutsceneActionData)session.Asset.RootAction!).Actions[actionIndex];
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out _));

        var history = new EditorHistoryStack();
        history.Execute(new SetCutsceneActionPropertyCommand(session, action, CutsceneActionPropertyEditor.FindProperty(action, key)!, value));
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out string reason), reason);

        history.Undo();
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(session, out _));

        history.Redo();
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(session, out reason), reason);
    }

    private static JObject UnknownType() => Root(new JObject
    {
        ["type"] = "Sequence",
        ["actions"] = new JArray(new JObject { ["type"] = "Wait", ["seconds"] = 1 }, new JObject { ["type"] = "Frobnicate" }),
    });

    private static JObject FormatError() => Root(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb", ["x"] = "abc", ["y"] = 2 });

    private static JObject TargetNotAString() => Root(new JObject
    {
        ["type"] = "Sequence",
        ["actions"] = new JArray(new JObject { ["type"] = "Wait", ["seconds"] = 1 }, new JObject { ["type"] = "OpenScreen", ["target"] = 5 }),
    });

    private static JObject InvalidBlendMode() => Root(new JObject
    {
        ["type"] = "FadeScreen",
        ["r"] = 1,
        ["g"] = 2,
        ["b"] = 3,
        ["duration_seconds"] = 1,
        ["blend_mode"] = "Nope",
    });

    private static JObject ActionsEntryNotAnObject() => Root(new JObject
    {
        ["type"] = "Sequence",
        ["actions"] = new JArray(new JObject { ["type"] = "Wait", ["seconds"] = 1 }, 5),
    });

    private static JObject ActionsNotAnArray() => Root(new JObject { ["type"] = "Sequence", ["actions"] = new JObject() });

    private static JObject RootNotAnObject()
    {
        var source = Root(new JObject());
        source["root_action"] = "Wait";
        return source;
    }

    private static JObject Root(JObject rootAction) => new()
    {
        ["id"] = Guid.NewGuid().ToString(),
        ["name"] = "Fixture",
        ["type"] = "CutsceneAsset",
        ["version"] = 1,
        ["root_action"] = rootAction,
    };

    /// <summary>The first difference between two trees: types, paths, the text of every property, validation messages.</summary>
    private static string? FindDifference(CutsceneAsset left, CutsceneAsset right)
    {
        string? difference = FindDifference(left.RootAction, right.RootAction, "root_action");
        if (difference != null)
        {
            return difference;
        }

        string leftMessages = string.Join("\n", left.Validate().Messages.Select(message => $"{message.Severity} {message.Path} {message.Message}"));
        string rightMessages = string.Join("\n", right.Validate().Messages.Select(message => $"{message.Severity} {message.Path} {message.Message}"));
        return leftMessages == rightMessages ? null : "validation messages differ";
    }

    private static string? FindDifference(CutsceneActionData? left, CutsceneActionData? right, string path)
    {
        if (left == null || right == null)
        {
            return left == right ? null : $"{path}: one action is missing";
        }

        if (left.GetType() != right.GetType() || left.Type != right.Type)
        {
            return $"{path}: {left.Type} against {right.Type}";
        }

        foreach (var property in CutsceneActionPropertyEditor.GetProperties(left))
        {
            if (property.GetText(left) != property.GetText(right))
            {
                return $"{path}.{property.Key}: '{property.GetText(left)}' against '{property.GetText(right)}'";
            }
        }

        var leftChildren = CutsceneLoadFidelityChildren(left);
        var rightChildren = CutsceneLoadFidelityChildren(right);
        if (leftChildren.Count != rightChildren.Count)
        {
            return $"{path}: {leftChildren.Count} children against {rightChildren.Count}";
        }

        for (int index = 0; index < leftChildren.Count; index++)
        {
            string? difference = FindDifference(leftChildren[index], rightChildren[index], $"{path}.actions[{index}]");
            if (difference != null)
            {
                return difference;
            }
        }

        return null;
    }

    private static IReadOnlyList<CutsceneActionData> CutsceneLoadFidelityChildren(CutsceneActionData action)
        => action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            ParallelCutsceneActionData parallel => parallel.Actions,
            RepeatCutsceneActionData repeat => repeat.Actions,
            _ => [],
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine.MonoGame.sln was not found above the test assembly.");
    }

    /// <summary>The cutscenes of the sources: Projects/*/Cutscenes and CasaEngine.Demos/Content/Cutscenes, never a bin or obj copy.</summary>
    private static List<string> FindRepositoryCutsceneFiles()
    {
        string root = FindRepositoryRoot();
        var files = new List<string>();

        foreach (string project in Directory.GetDirectories(Path.Combine(root, "Projects")))
        {
            string folder = Path.Combine(project, "Cutscenes");
            if (Directory.Exists(folder))
            {
                files.AddRange(Directory.GetFiles(folder, "*.cutscene", SearchOption.TopDirectoryOnly));
            }
        }

        string demos = Path.Combine(root, "CasaEngine.Demos", "Content", "Cutscenes");
        if (Directory.Exists(demos))
        {
            files.AddRange(Directory.GetFiles(demos, "*.cutscene", SearchOption.TopDirectoryOnly));
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }
}
