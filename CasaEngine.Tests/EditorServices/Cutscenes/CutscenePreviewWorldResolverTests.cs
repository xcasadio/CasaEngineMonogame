using CasaEngine.EditorServices.Cutscenes;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutscenePreviewWorldResolverTests : IDisposable
{
    private static readonly Guid CutsceneId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid WorldA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid WorldB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid WorldC = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid WorldD = Guid.Parse("00000000-0000-0000-0000-00000000000d");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-preview-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static List<CutscenePreviewWorldCandidate> Worlds() =>
    [
        new(WorldD, "d.world", Guid.Empty),
        new(WorldC, "c.world", CutsceneId),
        new(WorldB, "b.world", Guid.Empty),
        new(WorldA, "a.world", Guid.Empty),
    ];

    private static Guid[] Ids(CutscenePreviewWorldChoice choice) => choice.Worlds.Select(w => w.WorldId).ToArray();

    [Fact]
    public void ARememberedWorld_ComesFirst()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve(Worlds(), CutsceneId, "b.world", WorldD);

        Assert.Equal([WorldD, WorldC, WorldB, WorldA], Ids(choice));
        Assert.Equal(WorldD, choice.Default.WorldId);
    }

    [Fact]
    public void WithoutARememberedWorld_TheStartupWorldOfTheCutsceneComesFirst()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve(Worlds(), CutsceneId, "b.world", Guid.Empty);

        Assert.Equal([WorldC, WorldB, WorldA, WorldD], Ids(choice));
    }

    [Fact]
    public void WithoutARememberedOrStartupWorld_FirstWorldLoadedComesFirst()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve(Worlds(), Guid.NewGuid(), "B.WORLD", Guid.Empty);

        Assert.Equal([WorldB, WorldA, WorldC, WorldD], Ids(choice));
    }

    [Fact]
    public void WithNothingElse_TheFirstWorldByFileNameIsTheDefault()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve(Worlds(), Guid.NewGuid(), "", Guid.Empty);

        Assert.Equal([WorldA, WorldB, WorldC, WorldD], Ids(choice));
    }

    [Fact]
    public void ARememberedWorldThatMatchesNoWorld_IsIgnored()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve(Worlds(), CutsceneId, "b.world", Guid.NewGuid());

        Assert.Equal(WorldC, choice.Default.WorldId);
        Assert.Equal(4, choice.Worlds.Count);
    }

    [Fact]
    public void AProjectWithoutWorld_HasNoChoice()
    {
        CutscenePreviewWorldChoice choice = CutscenePreviewWorldResolver.Resolve([], CutsceneId, "a.world", WorldA);

        Assert.False(choice.HasWorld);
        Assert.Empty(choice.Worlds);
    }

    [Theory]
    [InlineData("{\"name\":\"w\",\"startup_cutscene_asset_id\":\"18086f73-c97d-5066-a58f-34a8253ead66\"}", "18086f73-c97d-5066-a58f-34a8253ead66")]
    [InlineData("{\"name\":\"w\"}", "00000000-0000-0000-0000-000000000000")]
    [InlineData("{\"nested\":{\"startup_cutscene_asset_id\":\"18086f73-c97d-5066-a58f-34a8253ead66\"}}", "00000000-0000-0000-0000-000000000000")]
    [InlineData("{\"startup_cutscene_asset_id\":\"not a guid\"}", "00000000-0000-0000-0000-000000000000")]
    [InlineData("{\"startup_cutscene_asset_id\":12}", "00000000-0000-0000-0000-000000000000")]
    public void TheStartupCutsceneIsReadFromTheTopLevelKeyOnly(string json, string expected)
    {
        Guid id = CutscenePreviewWorldResolver.ReadStartupCutsceneAssetId(new StringReader(json));

        Assert.Equal(Guid.Parse(expected), id);
    }

    [Fact]
    public void ASettingsFile_RoundTrips()
    {
        var settings = new CutscenePreviewSettings();
        settings.SetWorldId(CutsceneId, WorldB);
        settings.Save(_directory);

        Assert.True(File.Exists(CutscenePreviewSettings.GetPath(_directory)));
        CutscenePreviewSettings loaded = CutscenePreviewSettings.Load(_directory);
        Assert.Equal(WorldB, loaded.GetWorldId(CutsceneId));
        Assert.Equal(Guid.Empty, loaded.GetWorldId(Guid.NewGuid()));

        loaded.SetWorldId(CutsceneId, Guid.Empty);
        loaded.Save(_directory);
        Assert.Equal(Guid.Empty, CutscenePreviewSettings.Load(_directory).GetWorldId(CutsceneId));
    }

    [Fact]
    public void AMissingSettingsFile_GivesEmptySettings()
    {
        Assert.Equal(Guid.Empty, CutscenePreviewSettings.Load(_directory).GetWorldId(CutsceneId));
    }

    [Fact]
    public void AnUnreadableSettingsFile_GivesEmptySettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CutscenePreviewSettings.GetPath(_directory))!);
        File.WriteAllText(CutscenePreviewSettings.GetPath(_directory), "{ not json");

        Assert.Equal(Guid.Empty, CutscenePreviewSettings.Load(_directory).GetWorldId(CutsceneId));
    }
}
