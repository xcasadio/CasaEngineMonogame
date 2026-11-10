using System.Drawing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// Guards the data of Projects/SpriteDrawOrderSample, a project with no gameplay DLL whose world holds three overlapping
/// sprites of the same Z, whose stacking is chosen by their draw_order (ADR-0092). Running it in the Launcher proves the
/// drawing; this proves the data hangs together: the committed image holds the pixels the generator produces, the draw orders
/// are the reverse of the order of the entities in the world, and every reference resolves in the sample's own catalogue.
/// </summary>
public class SpriteDrawOrderSampleDataTests
{
    private static readonly string[] Discs = ["red", "green", "blue"];

    [Fact]
    public void TheCommittedSheet_HoldsThePixelsOfTheGenerator()
    {
        using var bitmap = new Bitmap(Path.Combine(ProjectPath(), "Images", "discs.png"));

        Assert.Equal(SpriteDrawOrderSampleImages.SheetWidth, bitmap.Width);
        Assert.Equal(SpriteDrawOrderSampleImages.DiscSize, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var actual = bitmap.GetPixel(x, y);
                var color = SpriteDrawOrderSampleImages.SheetPixel(x, y);
                Assert.True(
                    actual.A == color.A && actual.R == color.R && actual.G == color.G && actual.B == color.B,
                    $"discs.png differs at ({x}, {y}): committed ({actual.R},{actual.G},{actual.B},{actual.A}), generated ({color.R},{color.G},{color.B},{color.A}).");
            }
        }
    }

    [Fact]
    public void TheGeneratedPng_DecodesWithTheExpectedSizeAndTransparency()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sprite-draw-order-sample-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, SpriteDrawOrderSampleImages.BuildSheetPng());
            using var bitmap = new Bitmap(path);

            Assert.Equal(SpriteDrawOrderSampleImages.SheetWidth, bitmap.Width);
            Assert.Equal(SpriteDrawOrderSampleImages.DiscSize, bitmap.Height);
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
            Assert.Equal(SpriteDrawOrderSampleImages.DiscAlpha, bitmap.GetPixel(SpriteDrawOrderSampleImages.DiscSize / 2, SpriteDrawOrderSampleImages.DiscSize / 2).A);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheThreeDiscs_HaveDistinctOpaqueEnoughColours()
    {
        var colors = Enumerable.Range(0, SpriteDrawOrderSampleImages.DiscCount).Select(SpriteDrawOrderSampleImages.DiscColor).ToList();

        Assert.Equal(3, colors.Distinct().Count());
        Assert.All(colors, color => Assert.InRange((int)color.A, 1, 254));
    }

    [Fact]
    public void TheWorld_HoldsThreeOverlappingAlphaBlendSpritesAtTheSameZ()
    {
        var sprites = SpriteEntities();

        Assert.Equal(3, sprites.Count);
        foreach (var root in sprites)
        {
            Assert.Equal("StaticSpriteComponent", (string?)root["type"]);
            Assert.Equal("AlphaBlend", (string?)root["blend_mode"]);
        }

        Assert.Single(sprites.Select(root => (double)root["coordinates"]!["position"]!["z"]!).Distinct());

        var positions = sprites.Select(root => (double)root["coordinates"]!["position"]!["x"]!).Order().ToList();
        for (var index = 1; index < positions.Count; index++)
        {
            Assert.True(positions[index] - positions[index - 1] < SpriteDrawOrderSampleImages.DiscSize, "neighbouring sprites must overlap");
            Assert.True(positions[index] - positions[index - 1] > 0, "sprites must not share a position");
        }
    }

    [Fact]
    public void TheDrawOrders_AreTheReverseOfTheOrderOfTheEntitiesInTheWorld()
    {
        var sprites = SpriteEntities();

        var orders = sprites.Select(root => (int)root["draw_order"]!).ToList();

        Assert.Equal(Discs.Length, orders.Count);
        Assert.Equal(orders.OrderByDescending(order => order), orders);
        Assert.Equal(orders.Count, orders.Distinct().Count());
        Assert.Equal(Discs.Select(color => "disc-" + color), SpriteNames(sprites));
    }

    [Fact]
    public void TheWorld_HasA2dCameraNoDepthSortComponentAndALightClear()
    {
        var text = File.ReadAllText(Path.Combine(ProjectPath(), "DrawOrderWorld.world"));
        var world = JObject.Parse(text);

        Assert.Equal("Camera2dComponent", (string?)Entities(world)["camera"]["root_component"]!["type"]);
        Assert.DoesNotContain("DepthSortable2DComponent", text);
        Assert.Equal("SolidColor", (string?)world["environment"]!["background_mode"]);
        var color = world["environment"]!["background_color"]!;
        Assert.True((int)color["r"]! >= 128 && (int)color["g"]! >= 128 && (int)color["b"]! >= 128);
    }

    [Fact]
    public void EverySpriteTheWorldNames_ResolvesToACataloguedSpriteOnACataloguedImage()
    {
        var catalog = Catalog();

        foreach (var root in SpriteEntities())
        {
            var name = (string)root["spriteDataName"]!;
            var spriteInfo = Assert.Single(catalog, info => (string?)info["name"] == name);
            Assert.Equal("sprite", (string?)spriteInfo["asset_type"]);
            var sprite = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)spriteInfo["file_name"]!)));
            var sheetInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)sprite["sprite_sheet_asset_id"]);
            Assert.Equal("texture", (string?)sheetInfo["asset_type"]);
            var texture = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)sheetInfo["file_name"]!)));
            var imageInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)texture["texture_asset_id"]);
            Assert.Equal("png", (string?)imageInfo["asset_type"]);

            using var bitmap = new Bitmap(Path.Combine(ProjectPath(), (string)imageInfo["file_name"]!));
            var location = sprite["location"]!;
            Assert.True((int)location["x"]! + (int)location["w"]! <= bitmap.Width, name);
            Assert.True((int)location["y"]! + (int)location["h"]! <= bitmap.Height, name);
        }
    }

    [Fact]
    public void EveryCataloguedFile_ExistsAndEveryIdAndNameIsUnique()
    {
        var catalog = Catalog();

        foreach (var info in catalog)
        {
            Assert.True(File.Exists(Path.Combine(ProjectPath(), (string)info["file_name"]!)), (string?)info["file_name"]);
        }

        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["id"]).Distinct().Count());
        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["name"]).Distinct().Count());
    }

    [Fact]
    public void TheProject_HasNoDllNoFirstScreenAndStartsOnTheWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "SpriteDrawOrderSample.json")));
        var catalog = Catalog();

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Contains(catalog, info => (string?)info["file_name"] == (string?)project["FirstWorldLoaded"] && (string?)info["asset_type"] == "world");
    }

    private static List<string> SpriteNames(List<JToken> sprites) => sprites.Select(root => (string)root["spriteDataName"]!).ToList();

    /// <summary>The root components of the sprite entities, in the order of the world file.</summary>
    private static List<JToken> SpriteEntities()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "DrawOrderWorld.world")))["entity_references"]!
            .Select(reference => reference["entity"]!["root_component"]!)
            .Where(root => (string?)root["type"] == "StaticSpriteComponent")
            .ToList();

    private static Dictionary<string, JToken> Entities(JObject world)
        => world["entity_references"]!.ToDictionary(reference => (string)reference["entity"]!["name"]!, reference => reference["entity"]!);

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "SpriteDrawOrderSample");

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

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
