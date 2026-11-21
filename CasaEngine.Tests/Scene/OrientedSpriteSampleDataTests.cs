using System.Drawing;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// Guards the data of Projects/OrientedSpriteSample (ADR-0151, ADR-0152): a tilted, rolled perspective camera posed far from
/// the origin of the world, two sprites that are its child components (a far picture and a near picture with a cut-out), a box
/// and a turned sprite standing in the world between the two pictures. Running it in the Launcher proves the drawing; this
/// proves the data hangs together: the committed images hold the pixels of the generator, the camera is the root component of
/// its entity and does not see the origin of the world, the recorded scale of each picture is the one the formula gives for the
/// field of view and the height of the frame, the box lies between the two pictures and shows through the cut-out, the turned
/// sprite is seen from the front, and every reference resolves in the sample's own catalogue.
/// </summary>
public class OrientedSpriteSampleDataTests
{
    private const string ProjectFolder = "OrientedSpriteSample";

    [Fact]
    public void TheCommittedImages_HoldThePixelsOfTheGenerator()
    {
        AssertPixels("far.png", OrientedSpriteSampleImages.PictureWidth, OrientedSpriteSampleImages.PictureHeight,
            OrientedSpriteSampleImages.FarPixel);
        AssertPixels("near.png", OrientedSpriteSampleImages.PictureWidth, OrientedSpriteSampleImages.PictureHeight,
            OrientedSpriteSampleImages.NearPixel);
        AssertPixels("turned.png", OrientedSpriteSampleImages.TurnedSize, OrientedSpriteSampleImages.TurnedSize,
            OrientedSpriteSampleImages.TurnedPixel);
        AssertPixels("box.png", OrientedSpriteSampleImages.BoxTextureSize, OrientedSpriteSampleImages.BoxTextureSize,
            OrientedSpriteSampleImages.BoxPixel);
    }

    [Fact]
    public void TheNearPicture_IsOpaqueAroundACutOutThatIsFullyTransparent()
    {
        for (var y = 0; y < OrientedSpriteSampleImages.PictureHeight; y++)
        {
            for (var x = 0; x < OrientedSpriteSampleImages.PictureWidth; x++)
            {
                bool inCutout = x >= OrientedSpriteSampleImages.CutoutLeft && x < OrientedSpriteSampleImages.CutoutLeft + OrientedSpriteSampleImages.CutoutWidth
                    && y >= OrientedSpriteSampleImages.CutoutTop && y < OrientedSpriteSampleImages.CutoutTop + OrientedSpriteSampleImages.CutoutHeight;
                Assert.Equal(inCutout ? 0 : 255, OrientedSpriteSampleImages.NearPixel(x, y).A);
            }
        }
    }

    [Fact]
    public void TheCamera_IsTheRootComponentOfItsEntity_AndLoadsAsAPerspectiveCamera()
    {
        var root = (JObject)Entities()["camera"]["root_component"]!;
        var camera = LoadCamera();

        Assert.Equal("PerspectiveCameraComponent", (string?)root["type"]);
        Assert.Equal(PerspectiveCameraComponent.DefaultFieldOfView, camera.FieldOfView, 1e-6f);
        Assert.Equal(Vector2.Zero, camera.PictureOffset);
        Assert.Equal(OrientedSpriteSampleImages.FrameWidth, camera.Viewport.Width);
        Assert.Equal(OrientedSpriteSampleImages.FrameHeight, camera.Viewport.Height);
    }

    [Fact]
    public void TheProject_AsksForTheResolutionTheScalesAreComputedFor()
    {
        var project = Project();

        Assert.Equal(OrientedSpriteSampleImages.FrameWidth, (int)project["DebugWidth"]!);
        Assert.Equal(OrientedSpriteSampleImages.FrameHeight, (int)project["DebugHeight"]!);
    }

    [Fact]
    public void TheCamera_HasYawPitchAndRoll_SoThatNoneOfItsAxesIsAnAxisOfTheWorld()
    {
        var orientation = LoadCamera().LocalOrientation;
        var right = Vector3.Transform(Vector3.Right, orientation);
        var up = Vector3.Transform(Vector3.Up, orientation);
        var forward = Vector3.Transform(Vector3.Forward, orientation);

        foreach (var axis in new[] { right, up, forward })
        {
            Assert.True(MathF.Max(MathF.Abs(axis.X), MathF.Max(MathF.Abs(axis.Y), MathF.Abs(axis.Z))) < 0.95f, axis.ToString());
        }

        Assert.True(MathF.Abs(forward.X) > 0.1f || MathF.Abs(forward.Z) > 0.1f, "yaw");
        Assert.True(MathF.Abs(forward.Y) > 0.1f, "pitch");
        Assert.True(MathF.Abs(right.Y) > 0.1f, "roll");
    }

    [Fact]
    public void TheOriginOfTheWorld_IsOutsideTheFrustumOfTheCamera()
    {
        var camera = LoadCamera();
        var frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);

        Assert.Equal(ContainmentType.Disjoint, frustum.Contains(Vector3.Zero));
        Assert.True(camera.LocalPosition.Length() > 500f, "The camera must stand far from the origin of the world.");
    }

    [Fact]
    public void TheTwoPictures_AreChildComponentsOfTheCamera_AtMinusDOnItsViewingAxis()
    {
        var children = ChildSprites();

        Assert.Equal(2, children.Count);
        foreach (var child in children.Values)
        {
            var position = ReadVector(child["coordinates"]!["position"]!);
            Assert.Equal(0f, position.X);
            Assert.Equal(0f, position.Y);
            Assert.True(position.Z < 0f);
            Assert.Equal(Quaternion.Identity, ReadQuaternion(child["coordinates"]!["rotation"]!));
            Assert.Equal("Opaque", (string?)child["blend_mode"]);
        }

        Assert.True(Distance(children["far-picture"]) > Distance(children["near-picture"]));
        Assert.True(Distance(children["near-picture"]) > 1f, "The near picture must be beyond the near plane.");
        Assert.True(Distance(children["far-picture"]) < LoadCamera().FarPlane);

        // They load as the child components of the camera.
        var camera = LoadCamera();
        Assert.Equal(2, camera.Children.OfType<StaticSpriteComponent>().Count());
    }

    [Fact]
    public void TheScaleOfEachPicture_IsOnePictureOnePixelPerFramePixel()
    {
        var camera = LoadCamera();
        var children = ChildSprites();

        foreach (var (spriteName, child) in children)
        {
            float distance = Distance(child);
            float expected = 2f * distance * MathF.Tan(camera.FieldOfView * 0.5f) / OrientedSpriteSampleImages.FrameHeight;
            var scale = ReadVector(child["coordinates"]!["scale"]!);
            var sprite = LoadSprite(spriteName);

            Assert.True(MathF.Abs(scale.X - expected) <= 1e-4f * expected, $"{spriteName}: scale x {scale.X}, expected {expected}");
            Assert.True(MathF.Abs(scale.Y - expected) <= 1e-4f * expected, $"{spriteName}: scale y {scale.Y}, expected {expected}");
            Assert.Equal(1f, scale.Z);

            // Larger than the frame in both directions, and centred on its origin.
            Assert.True((int)sprite["location"]!["w"]! > OrientedSpriteSampleImages.FrameWidth);
            Assert.True((int)sprite["location"]!["h"]! > OrientedSpriteSampleImages.FrameHeight);
            Assert.Equal((int)sprite["location"]!["w"]! / 2, (int)sprite["hotspot"]!["x"]!);
            Assert.Equal((int)sprite["location"]!["h"]! / 2, (int)sprite["hotspot"]!["y"]!);
        }
    }

    [Fact]
    public void TheBox_LiesBetweenTheTwoPictures_AlongTheViewingAxis_AndShowsThroughTheCutOut()
    {
        var camera = LoadCamera();
        var children = ChildSprites();
        var box = Entities()["box"]["root_component"]!["coordinates"]!;
        var boxPosition = ReadVector(box["position"]!);
        float boxSize = (float)box["scale"]!["x"]!;
        var forward = Vector3.Transform(Vector3.Forward, camera.LocalOrientation);

        float depth = Vector3.Dot(boxPosition - camera.LocalPosition, forward);
        float halfDiagonal = boxSize * 0.5f * MathF.Sqrt(3f);

        Assert.True(depth > Distance(children["near-picture"]));
        Assert.True(depth < Distance(children["far-picture"]));
        // The whole box, not only its centre.
        Assert.True(depth - halfDiagonal > Distance(children["near-picture"]));
        Assert.True(depth + halfDiagonal < Distance(children["far-picture"]));

        AssertShowsThroughTheCutOut(camera, boxPosition, boxSize);
    }

    [Fact]
    public void TheTurnedSprite_IsATurnedRootComponent_SeenFromItsFront_ThroughTheCutOut()
    {
        var camera = LoadCamera();
        var children = ChildSprites();
        var node = Entities()["turned-sprite"]["root_component"]!;
        var position = ReadVector(node["coordinates"]!["position"]!);
        var orientation = ReadQuaternion(node["coordinates"]!["rotation"]!);
        var forward = Vector3.Transform(Vector3.Forward, camera.LocalOrientation);

        Assert.Equal("StaticSpriteComponent", (string?)node["type"]);
        Assert.Empty((JArray)node["children_component"]!);
        Assert.NotEqual(Quaternion.Identity, orientation);
        Assert.True(MathF.Abs(orientation.W) < 0.999f, "The rotation must be a real turn.");

        // The quad of a sprite faces its local +Z; the faces that look away from the camera are culled.
        var normal = Vector3.Transform(Vector3.UnitZ, orientation);
        var towardsCamera = Vector3.Normalize(camera.LocalPosition - position);
        Assert.True(Vector3.Dot(normal, towardsCamera) > 0.2f, "The turned sprite must be seen from its front.");

        float depth = Vector3.Dot(position - camera.LocalPosition, forward);
        Assert.True(depth > Distance(children["near-picture"]));
        Assert.True(depth < Distance(children["far-picture"]));

        float size = OrientedSpriteSampleImages.TurnedSize * (float)node["coordinates"]!["scale"]!["x"]!;
        AssertShowsThroughTheCutOut(camera, position, size);
    }

    [Fact]
    public void EveryReference_ResolvesInTheCatalogue()
    {
        var catalog = Catalog();
        var entities = Entities();

        var spriteNames = ChildSprites().Keys
            .Append((string)entities["turned-sprite"]["root_component"]!["spriteDataName"]!)
            .ToList();
        Assert.Equal(3, spriteNames.Distinct().Count());

        foreach (var spriteName in spriteNames)
        {
            var sprite = LoadCatalogued(catalog, info => (string?)info["name"] == spriteName, "sprite");
            var sheet = LoadCatalogued(catalog, info => (string?)info["id"] == (string?)sprite["sprite_sheet_asset_id"], "texture");
            AssertCatalogued(catalog, (string?)sheet["texture_asset_id"], "png");
        }

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
    public void TheProject_ReusesTheGameplayDllOfThePerspectiveCameraSample_AndCopiesItNextToItself()
    {
        var project = Project();
        var catalog = Catalog();
        var scriptName = (string)Entities()["camera"]["script_class_name"]!;
        var dllName = (string)project["GameplayDllName"]!;
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Projects", "CasaEngine.PerspectiveCameraSample", "PictureOffsetKeys.cs"));
        var projectFile = File.ReadAllText(Path.Combine(root, "Projects", "CasaEngine.PerspectiveCameraSample", "CasaEngine.PerspectiveCameraSample.csproj"));
        var gitignore = File.ReadAllText(Path.Combine(root, ".gitignore"));

        Assert.Equal("CasaEngine.PerspectiveCameraSample.dll", dllName);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Contains(catalog, info => (string?)info["file_name"] == (string?)project["FirstWorldLoaded"] && (string?)info["asset_type"] == "world");
        Assert.Equal(Path.GetFileNameWithoutExtension(dllName) + ".PictureOffsetKeys", scriptName);
        Assert.Contains("namespace CasaEngine.PerspectiveCameraSample;", source);
        Assert.Contains("public class PictureOffsetKeys : GameplayProxy", source);
        Assert.Contains($@"..\{ProjectFolder}\CasaEngine.PerspectiveCameraSample.dll", projectFile);
        Assert.Contains($@"..\{ProjectFolder}\CasaEngine.PerspectiveCameraSample.pdb", projectFile);
        Assert.Contains($"Projects/{ProjectFolder}/*.dll", gitignore);
        Assert.Contains($"Projects/{ProjectFolder}/*.pdb", gitignore);
    }

    // The thing of the given size, seen from the camera, must stand inside the cut-out of the near picture, as the frame
    // shows it (the picture is centred on the frame).
    private static void AssertShowsThroughTheCutOut(PerspectiveCameraComponent camera, Vector3 worldPosition, float worldSize)
    {
        var view = camera.ViewMatrix;
        var inView = Vector3.Transform(worldPosition, view);
        Assert.True(inView.Z < 0f, "The thing must be in front of the camera.");

        var clip = Vector4.Transform(new Vector4(worldPosition, 1f), view * camera.ProjectionMatrix);
        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        float frameX = (ndcX * 0.5f + 0.5f) * OrientedSpriteSampleImages.FrameWidth;
        float frameY = (0.5f - ndcY * 0.5f) * OrientedSpriteSampleImages.FrameHeight;

        // Pixels per world unit at that depth, to size the thing on screen.
        float pixelsPerUnit = OrientedSpriteSampleImages.FrameHeight / (2f * -inView.Z * MathF.Tan(camera.FieldOfView * 0.5f));
        float halfSize = worldSize * 0.5f * pixelsPerUnit;

        // The near picture is centred on the frame.
        float pictureX = frameX + (OrientedSpriteSampleImages.PictureWidth - OrientedSpriteSampleImages.FrameWidth) * 0.5f;
        float pictureY = frameY + (OrientedSpriteSampleImages.PictureHeight - OrientedSpriteSampleImages.FrameHeight) * 0.5f;

        Assert.True(pictureX - halfSize >= OrientedSpriteSampleImages.CutoutLeft, $"left edge {pictureX - halfSize}");
        Assert.True(pictureX + halfSize <= OrientedSpriteSampleImages.CutoutLeft + OrientedSpriteSampleImages.CutoutWidth, $"right edge {pictureX + halfSize}");
        Assert.True(pictureY - halfSize >= OrientedSpriteSampleImages.CutoutTop, $"top edge {pictureY - halfSize}");
        Assert.True(pictureY + halfSize <= OrientedSpriteSampleImages.CutoutTop + OrientedSpriteSampleImages.CutoutHeight, $"bottom edge {pictureY + halfSize}");
    }

    private static PerspectiveCameraComponent LoadCamera()
    {
        var node = (JObject)Entities()["camera"]["root_component"]!;
        return Assert.IsType<PerspectiveCameraComponent>(ElementFactory.LoadComponent<SceneComponent>(node));
    }

    // The two sprites of the children_component of the camera, by sprite name.
    private static Dictionary<string, JToken> ChildSprites()
    {
        var children = (JArray)Entities()["camera"]["root_component"]!["children_component"]!;
        foreach (var child in children)
        {
            Assert.Equal("StaticSpriteComponent", (string?)child["type"]);
        }

        return children.ToDictionary(child => (string)child["spriteDataName"]!, child => child);
    }

    private static float Distance(JToken child) => -ReadVector(child["coordinates"]!["position"]!).Z;

    private static Vector3 ReadVector(JToken node) => new((float)node["x"]!, (float)node["y"]!, (float)node["z"]!);

    private static Quaternion ReadQuaternion(JToken node) => new((float)node["x"]!, (float)node["y"]!, (float)node["z"]!, (float)node["w"]!);

    private static JObject LoadSprite(string spriteName)
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Images", spriteName.Replace("-picture", string.Empty) + ".sprite")));

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

    private static JObject Project()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "OrientedSpriteSample.json")));

    private static Dictionary<string, JToken> Entities()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "OrientedWorld.world")))["entity_references"]!
            .ToDictionary(reference => (string)reference["entity"]!["name"]!, reference => reference["entity"]!);

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", ProjectFolder);

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
