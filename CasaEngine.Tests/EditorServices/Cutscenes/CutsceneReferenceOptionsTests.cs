using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneReferenceOptionsTests
{
    private static readonly Guid SoundId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ScreenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WorldId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static AssetInfo Asset(Guid id, string name, string fileName, string type)
        => new(id) { Name = name, FileName = fileName, AssetType = type };

    private static List<AssetInfo> Catalogue() =>
    [
        Asset(SoundId, "theme", "Sounds/theme.sound", "sound"),
        Asset(Guid.NewGuid(), "alarm", "Sounds/alarm.sound", "sound"),
        Asset(Guid.NewGuid(), "orb-left", "Sprites/orb-left.sprite", "sprite"),
        Asset(ScreenId, "Title", "Screens/title.uiscreen", "uiscreen"),
        Asset(WorldId, "Cave", "Worlds/cave.world", "world"),
        Asset(Guid.NewGuid(), "exit", "Cutscenes/exit.cutscene", "cutscene"),
        Asset(Guid.NewGuid(), "menu", "Mappings/menu.buttonsmapping", "buttonsmapping"),
    ];

    [Theory]
    [InlineData(CutsceneActionPropertySemantic.Sound, "sound", 2)]
    [InlineData(CutsceneActionPropertySemantic.Sprite, "sprite", 1)]
    [InlineData(CutsceneActionPropertySemantic.Screen, "uiscreen", 1)]
    [InlineData(CutsceneActionPropertySemantic.World, "world", 1)]
    [InlineData(CutsceneActionPropertySemantic.Cutscene, "cutscene", 1)]
    [InlineData(CutsceneActionPropertySemantic.ButtonsMapping, "buttonsmapping", 1)]
    public void EachAssetSense_ListsOnlyAssetsOfItsType(CutsceneActionPropertySemantic semantic, string assetType, int count)
    {
        var catalogue = Catalogue();
        var options = CutsceneReferenceOptions.Build(semantic, catalogue);

        Assert.Equal(count, options.Count);
        Assert.Equal(assetType, CutsceneReferenceOptions.GetAssetType(semantic));
        Assert.True(CutsceneReferenceOptions.IsAssetReference(semantic));
        foreach (var option in options)
        {
            Assert.Equal(assetType, catalogue.Single(asset => asset.Id == option.AssetId).AssetType);
        }
    }

    [Theory]
    [InlineData(CutsceneActionPropertySemantic.Plain)]
    [InlineData(CutsceneActionPropertySemantic.Entity)]
    [InlineData(CutsceneActionPropertySemantic.InputList)]
    public void ASenseThatIsNotAnAsset_HasNoOption(CutsceneActionPropertySemantic semantic)
    {
        Assert.False(CutsceneReferenceOptions.IsAssetReference(semantic));
        Assert.Empty(CutsceneReferenceOptions.Build(semantic, Catalogue()));
    }

    [Fact]
    public void TheOptionsAreSortedByLabelWithoutCase()
    {
        var assets = new List<AssetInfo>
        {
            Asset(Guid.NewGuid(), "beta", "b.sprite", "sprite"),
            Asset(Guid.NewGuid(), "Alpha", "a.sprite", "sprite"),
            Asset(Guid.NewGuid(), "charlie", "c.sprite", "sprite"),
        };

        var options = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sprite, assets);

        Assert.Equal(["Alpha", "beta", "charlie"], options.Select(option => option.Label));
    }

    [Fact]
    public void TheWrittenValue_IsTheFormTheRuntimeReads()
    {
        var sound = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sound, Catalogue()).Single(option => option.AssetId == SoundId);
        var world = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.World, Catalogue()).Single();
        var sprite = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sprite, Catalogue()).Single();
        var screen = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Screen, Catalogue()).Single();

        Assert.Equal(SoundId.ToString(), sound.Value);
        Assert.Equal("theme", sound.Label);
        Assert.Equal("Worlds/cave.world", world.Value);
        Assert.Equal("Cave", world.Label);
        Assert.Equal("orb-left", sprite.Value);
        Assert.Equal("Title", screen.Value);
    }

    [Fact]
    public void AnUnnamedAsset_IsLabelledByItsFileName()
    {
        var options = CutsceneReferenceOptions.Build(
            CutsceneActionPropertySemantic.World,
            [Asset(Guid.NewGuid(), string.Empty, "Worlds/x.world", "world")]);

        Assert.Equal("Worlds/x.world", options.Single().Label);
    }

    [Fact]
    public void Find_MatchesTheWrittenValueOfEachSense()
    {
        var catalogue = Catalogue();
        foreach (var semantic in new[]
                 {
                     CutsceneActionPropertySemantic.Sound, CutsceneActionPropertySemantic.Sprite, CutsceneActionPropertySemantic.Screen,
                     CutsceneActionPropertySemantic.World, CutsceneActionPropertySemantic.Cutscene, CutsceneActionPropertySemantic.ButtonsMapping,
                 })
        {
            var options = CutsceneReferenceOptions.Build(semantic, catalogue);
            foreach (var option in options)
            {
                Assert.Same(option, CutsceneReferenceOptions.Find(semantic, options, option.Value));
            }
        }
    }

    [Fact]
    public void Find_AcceptsAGuidWhereTheRuntimeAcceptsANameOrAGuid()
    {
        var screens = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Screen, Catalogue());

        Assert.Equal("Title", CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Screen, screens, ScreenId.ToString())!.Value);
        Assert.Equal("Title", CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Screen, screens, ScreenId.ToString("N"))!.Value);
    }

    [Fact]
    public void Find_DoesNotReadAGuidAsAWorldFileNameOrASpriteName()
    {
        var worlds = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.World, Catalogue());
        var sprites = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sprite, Catalogue());

        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.World, worlds, WorldId.ToString()));
        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sprite, sprites, sprites[0].AssetId.ToString()));
    }

    [Fact]
    public void Find_ReadsASoundByItsGuidOnly()
    {
        var sounds = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sound, Catalogue());

        Assert.Equal(SoundId, CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sound, sounds, SoundId.ToString("B"))!.AssetId);
        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sound, sounds, "theme"));
    }

    [Fact]
    public void Find_ReturnsNullForAMissingAssetAndForAnEmptyValue()
    {
        var sprites = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sprite, Catalogue());
        var sounds = CutsceneReferenceOptions.Build(CutsceneActionPropertySemantic.Sound, Catalogue());

        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sprite, sprites, "gone"));
        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sound, sounds, Guid.NewGuid().ToString()));
        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sprite, sprites, string.Empty));
        Assert.Null(CutsceneReferenceOptions.Find(CutsceneActionPropertySemantic.Sprite, sprites, null));
    }
}
