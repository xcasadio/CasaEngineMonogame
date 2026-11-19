using System.Drawing;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// Guards the data of Projects/PerspectiveCameraSample: a world with a posed perspective camera (ADR-0147), a backdrop wider
/// than the picture and a box standing on it, and a small gameplay DLL that scrolls the picture with the arrow keys. Running it
/// in the Launcher proves the drawing; this proves the data hangs together: the committed images hold the pixels of the
/// generator, the camera loads as the class the world names and frames the backdrop one unit per pixel, the box stands on the
/// mark drawn in the backdrop, and every reference resolves in the sample's own catalogue.
/// </summary>
public class PerspectiveCameraSampleDataTests
{
    [Fact]
    public void TheCommittedImages_HoldThePixelsOfTheGenerator()
    {
        AssertPixels("backdrop.png", PerspectiveCameraSampleImages.BackdropWidth, PerspectiveCameraSampleImages.BackdropHeight,
            PerspectiveCameraSampleImages.BackdropPixel);
        AssertPixels("box.png", PerspectiveCameraSampleImages.BoxTextureSize, PerspectiveCameraSampleImages.BoxTextureSize,
            PerspectiveCameraSampleImages.BoxPixel);
    }

    [Fact]
    public void TheCamera_LoadsAsAPerspectiveCamera_WithNoOffsetAndNoRotation()
    {
        var camera = LoadCamera();

        Assert.Equal(PerspectiveCameraComponent.DefaultFieldOfView, camera.FieldOfView, 1e-6f);
        Assert.Equal(Vector2.Zero, camera.PictureOffset);
        Assert.Equal(Quaternion.Identity, camera.LocalOrientation);
        Assert.Equal(0f, camera.LocalPosition.X);
        Assert.Equal(0f, camera.LocalPosition.Y);
    }

    [Fact]
    public void TheCamera_FramesTheHeightOfTheBackdrop_OneUnitPerPixel()
    {
        var camera = LoadCamera();
        var backdrop = Entities()["backdrop"]["root_component"]!;
        float distance = camera.LocalPosition.Z - (float)backdrop["coordinates"]!["position"]!["z"]!;

        float visibleHeight = 2f * distance * MathF.Tan(camera.FieldOfView * 0.5f);

        Assert.Equal(PerspectiveCameraSampleImages.BackdropHeight, visibleHeight, 0.01f);
    }

    [Fact]
    public void TheBox_StandsOnTheMarkOfTheBackdrop_AndTheSceneFitsTheDepthRange()
    {
        var entities = Entities();
        var box = entities["box"]["root_component"]!["coordinates"]!;
        float boxX = (float)box["position"]!["x"]!;
        float boxY = (float)box["position"]!["y"]!;
        float boxZ = (float)box["position"]!["z"]!;
        float boxSize = (float)box["scale"]!["x"]!;
        var sprite = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Images", "backdrop.sprite")));

        // The sprite is drawn around its hotspot, y up: backdrop pixel (px, py) is at world (px - hotspot x, hotspot y - py).
        float markCentreX = PerspectiveCameraSampleImages.MarkLeft + PerspectiveCameraSampleImages.MarkSize * 0.5f - (float)sprite["hotspot"]!["x"]!;
        float markCentreY = (float)sprite["hotspot"]!["y"]! - (PerspectiveCameraSampleImages.MarkTop + PerspectiveCameraSampleImages.MarkSize * 0.5f);

        Assert.Equal(markCentreX, boxX);
        Assert.Equal(markCentreY, boxY);
        // The unit box is centred on its origin: its back face lies on the backdrop plane.
        Assert.Equal(boxSize * 0.5f, boxZ);

        // The world resets every camera to depths 1 to 1000 when it initialises.
        var camera = LoadCamera();
        Assert.InRange(camera.LocalPosition.Z - boxSize, 1f, 1000f);
        Assert.InRange(camera.LocalPosition.Z, 1f, 1000f);
    }

    [Fact]
    public void TheBackdrop_IsTwiceAsWideAsAFourByThreePicture()
    {
        var sprite = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Images", "backdrop.sprite")));

        Assert.Equal(PerspectiveCameraSampleImages.BackdropWidth, (int)sprite["location"]!["w"]!);
        Assert.Equal(PerspectiveCameraSampleImages.BackdropHeight, (int)sprite["location"]!["h"]!);
        Assert.Equal(2 * PerspectiveCameraSampleImages.BackdropHeight * 4 / 3, PerspectiveCameraSampleImages.BackdropWidth);
    }

    [Fact]
    public void EveryReference_ResolvesInTheCatalogue()
    {
        var catalog = Catalog();
        var entities = Entities();

        var spriteName = (string)entities["backdrop"]["root_component"]!["spriteDataName"]!;
        var sprite = LoadCatalogued(catalog, info => (string?)info["name"] == spriteName, "sprite");
        var sheet = LoadCatalogued(catalog, info => (string?)info["id"] == (string?)sprite["sprite_sheet_asset_id"], "texture");
        AssertCatalogued(catalog, (string?)sheet["texture_asset_id"], "png");

        var boxRoot = entities["box"]["root_component"]!;
        AssertCatalogued(catalog, (string?)boxRoot["static_model_asset_id"], "staticmodel");
        var materialId = (string?)boxRoot["material_slot_overrides"]![0]!["material_asset_id"];
        var material = LoadCatalogued(catalog, info => (string?)info["id"] == materialId, "material");
        Assert.Equal("unlit-texture", (string?)material["definition_id"]);
        var texture = LoadCatalogued(catalog, info => (string?)info["id"] == (string?)material["properties"]!["base_color_texture"], "texture");
        AssertCatalogued(catalog, (string?)texture["texture_asset_id"], "png");
    }

    [Fact]
    public void EveryCataloguedFile_ExistsAndEveryIdAndNameIsUnique()
    {
        var catalog = Catalog();

        foreach (var info in catalog)
        {
            Assert.True(File.Exists(Path.Combine(ProjectPath(), (string)info["file_name"]!)), (string?)info["file_name"]);
            if ((string?)info["asset_type"] != "png")
            {
                var file = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)info["file_name"]!)));
                Assert.Equal((string?)info["id"], (string?)file["id"]);
                Assert.Equal((string?)info["name"], (string?)file["name"]);
            }
        }

        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["id"]).Distinct().Count());
        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["name"]).Distinct().Count());
    }

    [Fact]
    public void TheProject_NamesItsDll_AndTheCameraNamesAScriptOfThatDll()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "PerspectiveCameraSample.json")));
        var catalog = Catalog();
        var scriptName = (string)Entities()["camera"]["script_class_name"]!;
        var dllName = (string)project["GameplayDllName"]!;
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Projects", "CasaEngine.PerspectiveCameraSample", "PictureOffsetKeys.cs"));

        Assert.Equal("CasaEngine.PerspectiveCameraSample.dll", dllName);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Contains(catalog, info => (string?)info["file_name"] == (string?)project["FirstWorldLoaded"] && (string?)info["asset_type"] == "world");
        Assert.Equal(Path.GetFileNameWithoutExtension(dllName) + ".PictureOffsetKeys", scriptName);
        Assert.Contains("namespace CasaEngine.PerspectiveCameraSample;", source);
        Assert.Contains("public class PictureOffsetKeys : GameplayProxy", source);
    }

    private static PerspectiveCameraComponent LoadCamera()
    {
        var node = (JObject)Entities()["camera"]["root_component"]!;
        return Assert.IsType<PerspectiveCameraComponent>(ElementFactory.LoadComponent<SceneComponent>(node));
    }

    private static void AssertPixels(string fileName, int width, int height, Func<int, int, Microsoft.Xna.Framework.Color> pixel)
    {
        using var bitmap = new Bitmap(Path.Combine(ProjectPath(), "Images", fileName));

        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var actual = bitmap.GetPixel(x, y);
                var color = pixel(x, y);
                Assert.True(
                    actual.A == color.A && actual.R == color.R && actual.G == color.G && actual.B == color.B,
                    $"{fileName} differs at ({x}, {y}): committed ({actual.R},{actual.G},{actual.B},{actual.A}), generated ({color.R},{color.G},{color.B},{color.A}).");
            }
        }
    }

    private static JObject LoadCatalogued(JToken catalog, Predicate<JToken> match, string assetType)
    {
        var info = Assert.Single(catalog, match);
        Assert.Equal(assetType, (string?)info["asset_type"]);
        return JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)info["file_name"]!)));
    }

    private static void AssertCatalogued(JToken catalog, string? id, string assetType)
    {
        var info = Assert.Single(catalog, candidate => (string?)candidate["id"] == id);
        Assert.Equal(assetType, (string?)info["asset_type"]);
    }

    private static Dictionary<string, JToken> Entities()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "CameraWorld.world")))["entity_references"]!
            .ToDictionary(reference => (string)reference["entity"]!["name"]!, reference => reference["entity"]!);

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "PerspectiveCameraSample");

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
