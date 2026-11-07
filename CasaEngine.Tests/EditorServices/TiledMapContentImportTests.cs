using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Tiled;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The content entry of the Tiled JSON import (<see cref="EditorAssetImportService.ImportTiledMapContent"/>) must write the
/// same files as the path entry (<see cref="EditorAssetImportService.ImportTiledMap"/>) for the same map, tilesets and images.
/// Asset ids come from Guid.NewGuid, so the second run is seeded with the catalog entries of the first one.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class TiledMapContentImportTests
{
    private sealed record Scenario(string MapJson, Dictionary<string, string> TilesetTexts, string[] ImagePaths);

    public static TheoryData<string> ScenarioNames => new()
    {
        "external",
        "embedded",
        "mixed",
        "animations",
        "collisionsAndProperties",
        "flips",
    };

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void ContentImport_WritesTheSameFilesAsThePathImport(string scenarioName)
    {
        RunEquivalence(CreateScenario(scenarioName), sourceInProject: false, handBuiltResolver: false);
    }

    [Theory]
    [InlineData("external")]
    [InlineData("mixed")]
    public void ContentImport_WithSourcesInsideTheProject_WritesTheSameFilesAsThePathImport(string scenarioName)
    {
        RunEquivalence(CreateScenario(scenarioName), sourceInProject: true, handBuiltResolver: false);
    }

    [Fact]
    public void ContentImport_WithAHandBuiltTilesetReference_WritesTheSameFilesAsThePathImport()
    {
        RunEquivalence(CreateScenario("external"), sourceInProject: false, handBuiltResolver: true);
    }

    [Fact]
    public void ContentImport_RejectsAResolvedTilesetWithAWrongTileSizeLikeThePathImport()
    {
        var scenario = CreateScenario("external");
        scenario.TilesetTexts["sets/ground.tsj"] = scenario.TilesetTexts["sets/ground.tsj"].Replace("\"tilewidth\": 16", "\"tilewidth\": 32");

        string root = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            string sourceDirectory = Path.Combine(root, "src");
            string projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(projectDirectory);
            WriteSources(sourceDirectory, scenario);
            string mapPath = Path.Combine(sourceDirectory, "level.tmj");

            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();
            var pathException = Assert.Throws<NotSupportedException>(
                () => EditorAssetImportService.ImportTiledMap(mapPath, Path.Combine(projectDirectory, "Out.tmj")));

            var handBuilt = CreateExternalReference(sourceDirectory, tileWidth: 32);
            var contentException = Assert.Throws<NotSupportedException>(
                () => EditorAssetImportService.ImportTiledMapContent(
                    scenario.MapJson,
                    mapPath,
                    _ => handBuilt,
                    _ => new byte[] { 1 },
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Equal("Tileset tile size 32x16 does not match map tile size 16x16.", pathException.Message);
            Assert.Equal(pathException.Message, contentException.Message);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentImport_ThrowsFileNotFoundWhenReadImageReturnsNull()
    {
        var scenario = CreateScenario("external");

        string root = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            string projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(projectDirectory);
            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();

            var exception = Assert.Throws<FileNotFoundException>(
                () => EditorAssetImportService.ImportTiledMapContent(
                    scenario.MapJson,
                    Path.Combine(root, "src", "level.tmj"),
                    request => ResolveFromTexts(scenario, request),
                    _ => null,
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Equal("Unable to import Tiled tileset image.", exception.Message);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PathImport_ThrowsFileNotFoundWhenTheExternalTilesetIsMissing()
    {
        var scenario = CreateScenario("external");

        string root = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            string sourceDirectory = Path.Combine(root, "src");
            string projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(projectDirectory);
            WriteSources(sourceDirectory, scenario);
            File.Delete(Path.Combine(sourceDirectory, "sets", "ground.tsj"));

            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();

            var exception = Assert.Throws<FileNotFoundException>(
                () => EditorAssetImportService.ImportTiledMap(
                    Path.Combine(sourceDirectory, "level.tmj"),
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Contains("ground.tsj", exception.FileName);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentImport_WhenTheLogicalImagePathIsTheDestinationTexture_WritesTheReturnedBytesWithNoFileThere()
    {
        RunInEmptyProject(projectDirectory =>
        {
            string imagePath = Path.Combine(projectDirectory, "ground.png");
            byte[] returned = CreateImageBytes(5);

            ImportWithImageAt(projectDirectory, imagePath, returned);

            Assert.Equal(returned, File.ReadAllBytes(imagePath));
        });
    }

    [Fact]
    public void ContentImport_WhenTheLogicalImagePathIsTheDestinationTexture_OverwritesAStaleFile()
    {
        RunInEmptyProject(projectDirectory =>
        {
            string imagePath = Path.Combine(projectDirectory, "ground.png");
            File.WriteAllBytes(imagePath, new byte[] { 9, 9, 9 });
            byte[] returned = CreateImageBytes(6);

            ImportWithImageAt(projectDirectory, imagePath, returned);

            Assert.Equal(returned, File.ReadAllBytes(imagePath));
            Assert.Equal(new[] { "ground.png" }, Directory.EnumerateFiles(projectDirectory, "ground*.png").Select(Path.GetFileName).ToArray());
        });
    }

    [Fact]
    public void ContentImport_ThrowsInvalidOperationWhenTheResolverReturnsNull()
    {
        var scenario = CreateScenario("external");
        RunInEmptyProject(projectDirectory =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EditorAssetImportService.ImportTiledMapContent(
                    scenario.MapJson,
                    Path.Combine(projectDirectory, "src", "level.tmj"),
                    _ => null!,
                    _ => new byte[] { 1 },
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Equal("The resolver returned no tileset for 'sets/ground.tsj'.", exception.Message);
        });
    }

    [Fact]
    public void ContentImport_LetsAnExceptionOfTheResolverReachTheCaller()
    {
        var scenario = CreateScenario("external");
        RunInEmptyProject(projectDirectory =>
        {
            var thrown = new FileNotFoundException("Missing tileset.", "sets/ground.tsj");

            var exception = Assert.Throws<FileNotFoundException>(
                () => EditorAssetImportService.ImportTiledMapContent(
                    scenario.MapJson,
                    Path.Combine(projectDirectory, "src", "level.tmj"),
                    _ => throw thrown,
                    _ => new byte[] { 1 },
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Same(thrown, exception);
        });
    }

    [Fact]
    public void ContentImport_ThrowsInvalidOperationWhenTheResolvedTilesetHasAWrongFirstGid()
    {
        var scenario = CreateScenario("external");
        RunInEmptyProject(projectDirectory =>
        {
            string sourceDirectory = Path.Combine(projectDirectory, "src");
            var handBuilt = CreateExternalReference(sourceDirectory, tileWidth: 16, firstGid: 7);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EditorAssetImportService.ImportTiledMapContent(
                    scenario.MapJson,
                    Path.Combine(sourceDirectory, "level.tmj"),
                    _ => handBuilt,
                    _ => new byte[] { 1 },
                    Path.Combine(projectDirectory, "Out.tmj")));

            Assert.Equal("The resolver returned a tileset for 'sets/ground.tsj' with FirstGid 7, expected 1.", exception.Message);
        });
    }

    private static void ImportWithImageAt(string projectDirectory, string imagePath, byte[] returned)
    {
        var scenario = CreateScenario("external");
        string sourceDirectory = Path.Combine(projectDirectory, "src");
        var handBuilt = CreateExternalReference(sourceDirectory, tileWidth: 16, imageFilePath: imagePath);

        EditorAssetImportService.ImportTiledMapContent(
            scenario.MapJson,
            Path.Combine(sourceDirectory, "level.tmj"),
            _ => handBuilt,
            path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(imagePath), StringComparison.OrdinalIgnoreCase) ? returned : null,
            Path.Combine(projectDirectory, "Out.tmj"));
    }

    private static void RunInEmptyProject(Action<string> body)
    {
        string root = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            string projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(projectDirectory);
            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();
            body(projectDirectory);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(root, recursive: true);
        }
    }

    private static void RunEquivalence(Scenario scenario, bool sourceInProject, bool handBuiltResolver)
    {
        string root = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            // Project A: the path import, from sources written on disc. Project B: the content import, from memory only: its
            // source folder is never written (a regression that reads the source on disc would fail on a missing file).
            string projectA = Path.Combine(root, "projectA");
            string projectB = Path.Combine(root, "projectB");
            string sourceA = sourceInProject ? projectA : Path.Combine(root, "srcA");
            string sourceB = sourceInProject ? projectB : Path.Combine(root, "srcB");
            WriteSources(sourceA, scenario);
            Directory.CreateDirectory(projectA);
            Directory.CreateDirectory(projectB);

            var sourceFilesA = ListFiles(sourceA);
            Assert.False(sourceInProject ? ListFiles(projectB).Count > 0 : Directory.Exists(sourceB), "The content import's source folder must not exist on disc.");

            var tilesetTextsByPath = scenario.TilesetTexts.ToDictionary(
                pair => Path.GetFullPath(Path.Combine(sourceB, pair.Key.Replace('/', Path.DirectorySeparatorChar))),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var imagesByPath = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < scenario.ImagePaths.Length; index++)
            {
                string imagePath = Path.GetFullPath(Path.Combine(sourceB, scenario.ImagePaths[index].Replace('/', Path.DirectorySeparatorChar)));
                imagesByPath[imagePath] = CreateImageBytes(index);
            }

            EngineEnvironment.ProjectPath = projectA;
            EditorAssetCatalogService.Clear();
            var resultA = EditorAssetImportService.ImportTiledMap(
                Path.Combine(sourceA, "level.tmj"),
                Path.Combine(projectA, "Out.tmj"));

            // Same asset ids in the second project: seed its catalog with the entries the first run created.
            var seededEntries = AssetCatalog.AssetInfos.ToList();
            EngineEnvironment.ProjectPath = projectB;
            EditorAssetCatalogService.Clear();
            foreach (var entry in seededEntries)
            {
                EditorAssetCatalogService.Add(entry);
            }

            Func<TiledExternalTilesetRequest, TiledTilesetReference> resolver = handBuiltResolver
                ? _ => CreateExternalReference(sourceB, tileWidth: 16)
                : request => TiledMapImporter.ReadTilesetJson(
                    tilesetTextsByPath[Path.GetFullPath(request.FilePath)],
                    request.FilePath,
                    request.FirstGid,
                    request.MapTileWidth,
                    request.MapTileHeight);

            var resultB = EditorAssetImportService.ImportTiledMapContent(
                scenario.MapJson,
                Path.Combine(sourceB, "level.tmj"),
                resolver,
                path => imagesByPath.TryGetValue(Path.GetFullPath(path), out var bytes) ? bytes : null,
                Path.Combine(projectB, "Out.tmj"));

            Assert.Equal(resultA.CreatedAssetFileNames, resultB.CreatedAssetFileNames);
            Assert.Equal(resultA.Warnings, resultB.Warnings);

            // Still nothing of the source on disc after the import.
            Assert.False(sourceInProject ? File.Exists(Path.Combine(projectB, "level.tmj")) : Directory.Exists(sourceB));

            // Project A holds the sources too when they are inside the project: they are not outputs.
            var filesA = ListFiles(projectA).Where(file => !sourceInProject || !sourceFilesA.Contains(file)).ToList();
            var filesB = ListFiles(projectB);
            Assert.Equal(filesA, filesB);
            Assert.Contains("AssetInfos.json", filesA);
            Assert.Contains(filesA, file => file.EndsWith(".tileMap", StringComparison.Ordinal));
            Assert.Contains(filesA, file => file.EndsWith(".tileset", StringComparison.Ordinal));
            Assert.Contains(filesA, file => file.EndsWith(".texture", StringComparison.Ordinal));
            Assert.Contains(filesA, file => file.EndsWith(".png", StringComparison.Ordinal));

            foreach (var file in filesA)
            {
                Assert.True(
                    File.ReadAllBytes(Path.Combine(projectA, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(projectB, file))),
                    $"'{file}' differs between the path import and the content import.");
            }
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(root, recursive: true);
        }
    }

    private static List<string> ListFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(directory, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    private static TiledTilesetReference ResolveFromTexts(Scenario scenario, TiledExternalTilesetRequest request)
    {
        return TiledMapImporter.ReadTilesetJson(
            scenario.TilesetTexts[request.Source],
            request.FilePath,
            request.FirstGid,
            request.MapTileWidth,
            request.MapTileHeight);
    }

    // What ReadTilesetJson produces for the "external" scenario's ground.tsj, built without any text.
    private static TiledTilesetReference CreateExternalReference(string sourceDirectory, int tileWidth, int firstGid = 1, string? imageFilePath = null)
    {
        return new TiledTilesetReference(
            firstGid,
            "ground",
            imageFilePath ?? Path.GetFullPath(Path.Combine(sourceDirectory, "img", "ground.png")),
            tileWidth,
            16,
            2,
            4,
            32,
            32,
            new Dictionary<int, TiledTileCollision>(),
            new Dictionary<int, Dictionary<string, string>>(),
            new Dictionary<int, List<TiledTileAnimationFrame>>(),
            new List<string>());
    }

    private static void WriteSources(string sourceDirectory, Scenario scenario)
    {
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "level.tmj"), scenario.MapJson);

        foreach (var (relativePath, text) in scenario.TilesetTexts)
        {
            string fullPath = Path.Combine(sourceDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, text);
        }

        for (var index = 0; index < scenario.ImagePaths.Length; index++)
        {
            string fullPath = Path.Combine(sourceDirectory, scenario.ImagePaths[index].Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, CreateImageBytes(index));
        }
    }

    // Distinct, non-trivial bytes per image so that any mix-up or alteration shows in the comparison.
    private static byte[] CreateImageBytes(int index)
    {
        return new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, (byte)(index + 1), 0, 255, (byte)(index * 7 + 3) };
    }

    private static string MapJson(string tilesets, string data, string extra = "")
    {
        return $$"""
            {
                "type": "map",
                "orientation": "orthogonal",
                "infinite": false,
                "width": 2,
                "height": 2,
                "tilewidth": 16,
                "tileheight": 16,
                {{extra}}
                "tilesets": [{{tilesets}}],
                "layers": [
                    {
                        "type": "tilelayer",
                        "name": "Ground",
                        "width": 2,
                        "height": 2,
                        "properties": [ { "name": "layerKey", "type": "string", "value": "layerValue" } ],
                        "data": {{data}}
                    },
                    {
                        "type": "tilelayer",
                        "name": "Top",
                        "width": 2,
                        "height": 2,
                        "data": [0, 3, 0, 4]
                    }
                ]
            }
            """;
    }

    private static string GroundTsj(string tilesJson = "")
    {
        string tiles = tilesJson.Length == 0 ? string.Empty : $", \"tiles\": {tilesJson}";
        return $$"""
            {
                "type": "tileset",
                "name": "ground",
                "tilewidth": 16,
                "tileheight": 16,
                "image": "../img/ground.png",
                "imagewidth": 32,
                "imageheight": 32
                {{tiles}}
            }
            """;
    }

    private static Scenario CreateScenario(string name)
    {
        const string ExternalGround = "{ \"firstgid\": 1, \"source\": \"sets/ground.tsj\" }";
        const string EmbeddedDecor = """
            {
                "firstgid": 5,
                "name": "decor",
                "tilewidth": 16,
                "tileheight": 16,
                "tilecount": 2,
                "columns": 2,
                "image": "img/decor.png",
                "imagewidth": 32,
                "imageheight": 16
            }
            """;

        switch (name)
        {
            case "external":
                return new Scenario(
                    MapJson(ExternalGround, "[1, 2, 3, 4]"),
                    new Dictionary<string, string> { ["sets/ground.tsj"] = GroundTsj() },
                    new[] { "img/ground.png" });

            case "embedded":
                return new Scenario(
                    MapJson(
                        EmbeddedDecor
                            .Replace("\"firstgid\": 5", "\"firstgid\": 1")
                            .Replace("\"tilecount\": 2", "\"tilecount\": 4")
                            .Replace("\"imageheight\": 16", "\"imageheight\": 32"),
                        "[1, 2, 2, 1]"),
                    new Dictionary<string, string>(),
                    new[] { "img/decor.png" });

            case "mixed":
                return new Scenario(
                    MapJson(ExternalGround + ", " + EmbeddedDecor, "[1, 5, 2, 6]"),
                    new Dictionary<string, string> { ["sets/ground.tsj"] = GroundTsj() },
                    new[] { "img/ground.png", "img/decor.png" });

            case "animations":
                return new Scenario(
                    MapJson(ExternalGround, "[1, 2, 3, 4]"),
                    new Dictionary<string, string>
                    {
                        ["sets/ground.tsj"] = GroundTsj(
                            """
                            [
                                { "id": 0, "animation": [ { "tileid": 0, "duration": 120 }, { "tileid": 1, "duration": 80 }, { "tileid": 2, "duration": 0 } ] },
                                { "id": 3, "animation": [ { "tileid": 3, "duration": 50 }, { "tileid": 2, "duration": 70 } ] }
                            ]
                            """),
                    },
                    new[] { "img/ground.png" });

            case "collisionsAndProperties":
                return new Scenario(
                    MapJson(
                        ExternalGround,
                        "[1, 2, 3, 4]",
                        "\"properties\": [ { \"name\": \"mapKey\", \"type\": \"string\", \"value\": \"mapValue\" } ],"),
                    new Dictionary<string, string>
                    {
                        ["sets/ground.tsj"] = GroundTsj(
                            """
                            [
                                {
                                    "id": 1,
                                    "properties": [ { "name": "kind", "type": "string", "value": "wall" }, { "name": "hp", "type": "int", "value": 3 } ],
                                    "objectgroup": { "objects": [ { "x": 2, "y": 4, "width": 12, "height": 8 }, { "x": 0, "y": 0, "width": 1, "height": 1 } ] }
                                },
                                {
                                    "id": 2,
                                    "objectgroup": { "objects": [ { "x": 0, "y": 0, "width": 8, "height": 8, "polygon": [ { "x": 0, "y": 0 }, { "x": 8, "y": 0 }, { "x": 4, "y": 8 } ] } ] }
                                }
                            ]
                            """),
                    },
                    new[] { "img/ground.png" });

            case "flips":
                return new Scenario(
                    MapJson(ExternalGround, "[2147483649, 1073741826, 536870915, 3221225476]"),
                    new Dictionary<string, string> { ["sets/ground.tsj"] = GroundTsj() },
                    new[] { "img/ground.png" });

            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
