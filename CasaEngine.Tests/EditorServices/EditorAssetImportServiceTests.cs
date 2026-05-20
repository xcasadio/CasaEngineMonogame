using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Common;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.TileMap;

using CasaEngine.Tests;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

[Collection(ProjectEnvironmentCollection.Name)]
public class EditorAssetImportServiceTests
{
    [Fact]
    public void ImportFile_StaticModelAuthorsMaterialAssets()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceFilePath = Path.Combine(repositoryRoot, "Projects", "SampleProject", "Car.x");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Car.x");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(sourceFilePath, destinationFilePath);

            Assert.True(catalogChanged);

            string importedMaterialsDirectory = Path.Combine(tempDirectory, "Car_Imported", "Materials");
            Assert.True(Directory.Exists(importedMaterialsDirectory));

            string[] materialFiles = Directory.GetFiles(importedMaterialsDirectory, "*" + Constants.FileNameExtensions.Material);
            Assert.NotEmpty(materialFiles);

            foreach (string materialFile in materialFiles)
            {
                var materialDocument = JObject.Parse(File.ReadAllText(materialFile));
                Assert.Equal("lit-diffuse", (string?)materialDocument["definition_id"]);
                Assert.NotNull(materialDocument["properties"]);
                Assert.Null(materialDocument["type"]);
            }

            string staticModelPath = Path.Combine(tempDirectory, "Car.staticModel");
            Assert.True(File.Exists(staticModelPath));

            var staticModelDocument = JObject.Parse(File.ReadAllText(staticModelPath));
            var meshesNode = Assert.IsType<JArray>(staticModelDocument["meshes"]);
            bool hasMaterialBinding = false;

            foreach (var meshToken in meshesNode)
            {
                var meshNode = Assert.IsType<JObject>(meshToken);
                string? materialAssetIdText = (string?)meshNode["material_asset_id"];
                if (string.IsNullOrWhiteSpace(materialAssetIdText)
                    || !Guid.TryParse(materialAssetIdText, out var materialAssetId)
                    || materialAssetId == Guid.Empty)
                {
                    continue;
                }

                hasMaterialBinding = true;
                var assetInfo = AssetCatalog.Get(materialAssetId);
                Assert.NotNull(assetInfo);
                Assert.Equal(Constants.FileNameExtensions.Material, Path.GetExtension(assetInfo!.FileName));
            }

            Assert.True(hasMaterialBinding);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_ReflectionCubemapResourceAlone_DoesNotPersistReflectionProperty()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(sourceFilePath, destinationFilePath);

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));

            Assert.False(TryReadReflectionTextureId(material, out _));
            Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
            Assert.True(ambientValue.TryGetVector3(out var ambientColor));
            Assert.True(ambientColor.X > 0.3f);
            Assert.Equal(RenderQueue.Opaque, material.Queue);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_OptionalReflectionHint_PersistsReflectionProperty()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.ReflectiveLit,
                    LegacyMaterialImportHint.Reflection)));

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));
            Assert.True(TryReadReflectionTextureId(material, out var reflectionTextureId));
            Assert.NotEqual(Guid.Empty, reflectionTextureId);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_AlphaCutoutLegacyModel_UsesOptionalLegacyImportProfileInterpretation()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "AlphaPalm.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "AlphaPalm.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.AlphaCutoutLit,
                    LegacyMaterialImportHint.AlphaCutout)));

            Assert.True(catalogChanged);

            var materials = LoadImportedMaterials(tempDirectory, "AlphaPalm_Imported");
            MaterialAsset[] alphaCutoutMaterials = materials.Where(candidate => candidate.Queue == RenderQueue.AlphaTest).ToArray();

            Assert.NotEmpty(alphaCutoutMaterials);
            Assert.All(alphaCutoutMaterials, material =>
            {
                Assert.Equal("CullNone", material.RasterizerStateName);
                Assert.True(material.TryGetPropertyValue("alpha_cutoff", out var alphaCutoffValue));
                Assert.True(alphaCutoffValue.TryGetFloat(out var alphaCutoff));
                Assert.Equal(0.35f, alphaCutoff);
            });
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_BrightAmbientHint_ComesFromProfileInsteadOfAssetNaming()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "AlphaPalm.X");
        Assert.True(File.Exists(sourceFilePath));

        string neutralProjectDirectory = CreateTempDirectory();
        string hintedProjectDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EditorAssetCatalogService.Clear();

            EngineEnvironment.ProjectPath = neutralProjectDirectory;
            bool neutralCatalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                Path.Combine(neutralProjectDirectory, "AlphaPalm.X"));

            EditorAssetCatalogService.Clear();

            EngineEnvironment.ProjectPath = hintedProjectDirectory;
            bool hintedCatalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                Path.Combine(hintedProjectDirectory, "AlphaPalm.X"),
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.OpaqueLit,
                    LegacyMaterialImportHint.BrightAmbient)));

            Assert.True(neutralCatalogChanged);
            Assert.True(hintedCatalogChanged);

            var neutralMaterials = LoadImportedMaterials(neutralProjectDirectory, "AlphaPalm_Imported");
            var hintedMaterials = LoadImportedMaterials(hintedProjectDirectory, "AlphaPalm_Imported");

            Assert.Equal(neutralMaterials.Count, hintedMaterials.Count);
            Assert.Contains(neutralMaterials, material => ReadAmbientColor(material).X < 128f / 255f);
            Assert.All(hintedMaterials, material =>
            {
                Vector3 ambientColor = ReadAmbientColor(material);
                Assert.InRange(ambientColor.X, 128f / 255f, 1.0f);
                Assert.InRange(ambientColor.Y, 128f / 255f, 1.0f);
                Assert.InRange(ambientColor.Z, 128f / 255f, 1.0f);
                Assert.Equal(RenderQueue.Opaque, material.Queue);
            });
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(neutralProjectDirectory, recursive: true);
            Directory.Delete(hintedProjectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_PassesOptionalLegacyImportProfileToImporter()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.AlphaCutoutLit,
                    LegacyMaterialImportHint.AlphaCutout)));

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));
            Assert.Equal(RenderQueue.AlphaTest, material.Queue);
            Assert.Equal("CullNone", material.RasterizerStateName);
            Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
            Assert.True(ambientValue.TryGetVector3(out var ambientColor));
            Assert.InRange(ambientColor.X, 0.30f, 0.35f);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_SkinnedModelAuthorsSeparatedAnimationAssets()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceFilePath = Path.Combine(repositoryRoot, "Projects", "SampleProject", "Skinned", "kid_idle.FBX");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "kid_idle.FBX");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(sourceFilePath, destinationFilePath);

            Assert.True(catalogChanged);

            string modelPath = Path.Combine(tempDirectory, "kid_idle.model");
            Assert.True(File.Exists(modelPath));

            var modelDocument = JObject.Parse(File.ReadAllText(modelPath));
            Guid geometryAssetId = Guid.Parse(modelDocument["geometry_asset_id"]!.Value<string>()!);
            Guid skeletonAssetId = Guid.Parse(modelDocument["skeleton_asset_id"]!.Value<string>()!);
            Guid defaultAnimationClipAssetId = Guid.Parse(modelDocument["default_animation_clip_asset_id"]!.Value<string>()!);
            var animationClipIds = Assert.IsType<JArray>(modelDocument["animation_clip_asset_ids"]);

            Assert.NotEqual(Guid.Empty, geometryAssetId);
            Assert.NotEqual(Guid.Empty, skeletonAssetId);
            Assert.NotEqual(Guid.Empty, defaultAnimationClipAssetId);
            Assert.NotEmpty(animationClipIds);

            var geometryAssetInfo = AssetCatalog.Get(geometryAssetId);
            var skeletonAssetInfo = AssetCatalog.Get(skeletonAssetId);
            var defaultAnimationClipAssetInfo = AssetCatalog.Get(defaultAnimationClipAssetId);

            Assert.NotNull(geometryAssetInfo);
            Assert.NotNull(skeletonAssetInfo);
            Assert.NotNull(defaultAnimationClipAssetInfo);
            Assert.Equal("kid_idle.FBX", geometryAssetInfo!.FileName);
            Assert.Equal(Constants.FileNameExtensions.Skeleton, Path.GetExtension(skeletonAssetInfo!.FileName));
            Assert.Equal(Constants.FileNameExtensions.SkeletonAnimation, Path.GetExtension(defaultAnimationClipAssetInfo!.FileName));

            string skeletonPath = Path.Combine(tempDirectory, skeletonAssetInfo.FileName);
            Assert.True(File.Exists(skeletonPath));

            var skeletonDocument = JObject.Parse(File.ReadAllText(skeletonPath));
            var joints = Assert.IsType<JArray>(skeletonDocument["joints"]);
            Assert.NotEmpty(joints);

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            var skeletonDefinition = assetContentManager.Load<SkeletonDefinition>(skeletonAssetId);
            var animationClip = assetContentManager.Load<AnimationClip>(defaultAnimationClipAssetId);

            Assert.True(skeletonDefinition.Count > 0);
            Assert.True(ReferenceEquals(animationClip.Skeleton, skeletonDefinition));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_TiledTmxAuthorsTileMapTilesetAndTextureAssets()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            string imagePath = Path.Combine(tempDirectory, "tiles.png");
            File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string tsxPath = Path.Combine(tempDirectory, "tiles.tsx");
            File.WriteAllText(tsxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.10.2" name="tiles" tilewidth="16" tileheight="16" tilecount="4" columns="2">
                 <image source="tiles.png" width="32" height="32"/>
                 <tile id="2">
                  <properties>
                   <property name="damage" type="int" value="7"/>
                  </properties>
                  <objectgroup>
                   <object id="1" x="2" y="3" width="10" height="11"/>
                  </objectgroup>
                 </tile>
                </tileset>
                """);

            string tmxPath = Path.Combine(tempDirectory, "level.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="3" height="2" tilewidth="16" tileheight="16" infinite="0">
                                 <properties>
                                    <property name="weather" value="rain"/>
                                 </properties>
                 <tileset firstgid="1" source="tiles.tsx"/>
                 <layer id="1" name="Ground" width="3" height="2">
                                    <properties>
                                     <property name="walkable" type="bool" value="false"/>
                                    </properties>
                  <data encoding="csv">
                1,0,2,
                2147483651,4,0
                  </data>
                 </layer>
                 <layer id="2" name="Decor" width="3" height="2">
                  <data encoding="csv">
                0,0,0,
                0,0,1
                  </data>
                 </layer>
                                 <objectgroup id="3" name="Objects">
                                    <object id="1" name="PlayerStart" type="spawn" x="16" y="32" width="4" height="5">
                                     <properties>
                                        <property name="team" value="blue"/>
                                     </properties>
                                    </object>
                                 </objectgroup>
                </map>
                """);

            bool imported = EditorAssetImportService.ImportFile(tmxPath, Path.Combine(tempDirectory, "ImportedLevel.tmx"));

            Assert.True(imported);
            Assert.NotNull(EditorAssetImportService.LastTiledMapImportResult);
            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult!.Warnings, warning => warning.Contains("flip", StringComparison.OrdinalIgnoreCase));

            string tileMapPath = Path.Combine(tempDirectory, "ImportedLevel.tileMap");
            string tileSetPath = Path.Combine(tempDirectory, "ImportedLevel_Imported", "ImportedLevel.tileset");
            string texturePath = Path.Combine(tempDirectory, "ImportedLevel_Imported", "Textures", "tiles.texture");
            string assetCatalogPath = Path.Combine(tempDirectory, "AssetInfos.json");

            Assert.True(File.Exists(tileMapPath));
            Assert.True(File.Exists(tileSetPath));
            Assert.True(File.Exists(texturePath));
            Assert.True(File.Exists(assetCatalogPath));

            var tileMapDocument = JObject.Parse(File.ReadAllText(tileMapPath));
            Assert.Equal("ImportedLevel", (string?)tileMapDocument["name"]);
            Assert.NotEqual(Guid.Empty, Guid.Parse(tileMapDocument["tile_set_asset_id"]!.Value<string>()!));
            var tileMapProperties = Assert.IsType<JObject>(tileMapDocument["custom_properties"]);
            Assert.Equal("rain", (string?)tileMapProperties["weather"]);

            var layers = Assert.IsType<JArray>(tileMapDocument["layers"]);
            Assert.Equal(2, layers.Count);

            var groundLayer = Assert.IsType<JObject>(layers[0]);
            Assert.Equal("Ground", (string?)groundLayer["name"]);
            Assert.Equal(new[] { 0, -1, 1, 2, 3, -1 }, groundLayer["tiles"]!.Values<int>().ToArray());
            Assert.Equal(new[] { 0, 0, 0, (int)TileCellFlags.FlipHorizontal, 0, 0 }, groundLayer["tile_flags"]!.Values<int>().ToArray());
            var groundLayerProperties = Assert.IsType<JObject>(groundLayer["custom_properties"]);
            Assert.Equal("false", (string?)groundLayerProperties["walkable"]);

            var decorLayer = Assert.IsType<JObject>(layers[1]);
            Assert.Equal("Decor", (string?)decorLayer["name"]);
            Assert.Equal(0.1f, decorLayer["z_offset"]!.Value<float>());
            Assert.Equal(new[] { -1, -1, -1, -1, -1, 0 }, decorLayer["tiles"]!.Values<int>().ToArray());

            var objectLayers = Assert.IsType<JArray>(tileMapDocument["object_layers"]);
            var objectLayer = Assert.IsType<JObject>(objectLayers[0]);
            Assert.Equal("Objects", (string?)objectLayer["name"]);
            var objects = Assert.IsType<JArray>(objectLayer["objects"]);
            var playerStart = Assert.IsType<JObject>(objects[0]);
            Assert.Equal(1, playerStart["id"]!.Value<int>());
            Assert.Equal("PlayerStart", (string?)playerStart["name"]);
            Assert.Equal("spawn", (string?)playerStart["type"]);
            Assert.Equal(16f, playerStart["x"]!.Value<float>());
            Assert.Equal(32f, playerStart["y"]!.Value<float>());
            Assert.Equal(4f, playerStart["width"]!.Value<float>());
            Assert.Equal(5f, playerStart["height"]!.Value<float>());
            var playerStartProperties = Assert.IsType<JObject>(playerStart["custom_properties"]);
            Assert.Equal("blue", (string?)playerStartProperties["team"]);

            var tileSetDocument = JObject.Parse(File.ReadAllText(tileSetPath));
            var tiles = Assert.IsType<JArray>(tileSetDocument["tiles"]);
            Assert.Equal(4, tiles.Count);

            var thirdTile = Assert.IsType<JObject>(tiles[2]);
            Assert.Equal(2, thirdTile["id"]!.Value<int>());
            Assert.Equal("Blocked", (string?)thirdTile["collision_type"]);
            var thirdTileProperties = Assert.IsType<JObject>(thirdTile["custom_properties"]);
            Assert.Equal("7", (string?)thirdTileProperties["damage"]);
            var thirdTileLocation = Assert.IsType<JObject>(thirdTile["location"]);
            Assert.Equal(0, thirdTileLocation["x"]!.Value<int>());
            Assert.Equal(16, thirdTileLocation["y"]!.Value<int>());
            Assert.Equal(16, thirdTileLocation["w"]!.Value<int>());
            Assert.Equal(16, thirdTileLocation["h"]!.Value<int>());

            var collision = Assert.IsType<JObject>(thirdTile["collision"]);
            Assert.Equal("Defense", (string?)collision["collision_type"]);
            Assert.Equal("Rectangle", (string?)collision["shape_type"]);
            Assert.Equal(10f, collision["w"]!.Value<float>());
            Assert.Equal(11f, collision["h"]!.Value<float>());
            var collisionLocation = Assert.IsType<JObject>(collision["location"]);
            Assert.Equal(2f, collisionLocation["x"]!.Value<float>());
            Assert.Equal(3f, collisionLocation["y"]!.Value<float>());

            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult.CreatedAssetFileNames, fileName => fileName.EndsWith("ImportedLevel.tileMap", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult.CreatedAssetFileNames, fileName => fileName.EndsWith("ImportedLevel.tileset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult.CreatedAssetFileNames, fileName => fileName.EndsWith("tiles.texture", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_TiledTmxSupportsEmbeddedTilesetsAndPathsWithSpaces()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            string imagePath = Path.Combine(tempDirectory, "tiles with space.png");
            File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string tmxPath = Path.Combine(tempDirectory, "embedded tileset.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="orthogonal" width="1" height="1" tilewidth="16" tileheight="16" infinite="0">
                 <tileset firstgid="1" name="embedded" tilewidth="16" tileheight="16" tilecount="1" columns="1">
                  <image source="tiles with space.png" width="16" height="16"/>
                 </tileset>
                 <layer id="1" name="Ground" width="1" height="1">
                  <data encoding="csv">1</data>
                 </layer>
                </map>
                """);

            bool imported = EditorAssetImportService.ImportFile(tmxPath, Path.Combine(tempDirectory, "EmbeddedLevel.tmx"));

            Assert.True(imported);
            Assert.True(File.Exists(Path.Combine(tempDirectory, "EmbeddedLevel.tileMap")));
            Assert.True(File.Exists(Path.Combine(tempDirectory, "EmbeddedLevel_Imported", "EmbeddedLevel.tileset")));
            Assert.True(File.Exists(Path.Combine(tempDirectory, "EmbeddedLevel_Imported", "Textures", "tiles with space.texture")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

        [Fact]
        public void ImportFile_TiledJsonAuthorsTileMapTilesetAndTextureAssets()
        {
                string tempDirectory = CreateTempDirectory();
                string? previousProjectPath = EngineEnvironment.ProjectPath;

                try
                {
                        EngineEnvironment.ProjectPath = tempDirectory;
                        EditorAssetCatalogService.Clear();

                        string imagePath = Path.Combine(tempDirectory, "tiles.png");
                        File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                        string tsjPath = Path.Combine(tempDirectory, "tiles.tsj");
                        File.WriteAllText(tsjPath,
                                """
                                {
                                    "type": "tileset",
                                    "name": "tiles",
                                    "tilewidth": 16,
                                    "tileheight": 16,
                                    "tilecount": 1,
                                    "columns": 1,
                                    "image": "tiles.png",
                                    "imagewidth": 16,
                                    "imageheight": 16
                                }
                                """);

                        string tmjPath = Path.Combine(tempDirectory, "level.tmj");
                        File.WriteAllText(tmjPath,
                                """
                                {
                                    "type": "map",
                                    "orientation": "orthogonal",
                                    "infinite": false,
                                    "width": 2,
                                    "height": 1,
                                    "tilewidth": 16,
                                    "tileheight": 16,
                                    "tilesets": [
                                        {
                                            "firstgid": 1,
                                            "source": "tiles.tsj"
                                        }
                                    ],
                                    "layers": [
                                        {
                                            "type": "tilelayer",
                                            "name": "Ground",
                                            "width": 2,
                                            "height": 1,
                                            "data": [1, 0]
                                        }
                                    ]
                                }
                                """);

                        bool imported = EditorAssetImportService.ImportFile(tmjPath, Path.Combine(tempDirectory, "JsonLevel.tmj"));

                        Assert.True(imported);
                        string tileMapPath = Path.Combine(tempDirectory, "JsonLevel.tileMap");
                        string tileSetPath = Path.Combine(tempDirectory, "JsonLevel_Imported", "JsonLevel.tileset");
                        string texturePath = Path.Combine(tempDirectory, "JsonLevel_Imported", "Textures", "tiles.texture");

                        Assert.True(File.Exists(tileMapPath));
                        Assert.True(File.Exists(tileSetPath));
                        Assert.True(File.Exists(texturePath));

                        var tileMapDocument = JObject.Parse(File.ReadAllText(tileMapPath));
                        var layers = Assert.IsType<JArray>(tileMapDocument["layers"]);
                        var groundLayer = Assert.IsType<JObject>(layers[0]);
                        Assert.Equal("Ground", (string?)groundLayer["name"]);
                        Assert.Equal(new[] { 0, -1 }, groundLayer["tiles"]!.Values<int>().ToArray());
                }
                finally
                {
                        EditorAssetCatalogService.Clear();
                        EngineEnvironment.ProjectPath = previousProjectPath;
                        Directory.Delete(tempDirectory, recursive: true);
                }
        }

    [Fact]
    public void ImportFile_TiledTmxRejectsUnsupportedOrientation()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            string tmxPath = Path.Combine(tempDirectory, "isometric.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="isometric" width="1" height="1" tilewidth="16" tileheight="16" infinite="0">
                </map>
                """);

            var exception = Assert.Throws<NotSupportedException>(() =>
                EditorAssetImportService.ImportFile(tmxPath, Path.Combine(tempDirectory, "isometric.tmx")));

            Assert.Contains("orientation", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }

    private static string FindWorkspaceRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string? workspaceRoot = Directory.GetParent(repositoryRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new DirectoryNotFoundException("Unable to locate the workspace root from the repository root.");
        }

        return workspaceRoot;
    }

    private static IReadOnlyList<MaterialAsset> LoadImportedMaterials(string projectDirectory, string importedFolderName)
    {
        string importedMaterialsDirectory = Path.Combine(projectDirectory, importedFolderName, "Materials");
        Assert.True(Directory.Exists(importedMaterialsDirectory));

        return Directory
            .GetFiles(importedMaterialsDirectory, "*" + Constants.FileNameExtensions.Material)
            .Select(materialFile =>
            {
                var materialDocument = JObject.Parse(File.ReadAllText(materialFile));
                var material = new MaterialAsset();
                material.Load(materialDocument);
                return material;
            })
            .ToArray();
    }

    private static Vector3 ReadAmbientColor(MaterialAsset material)
    {
        Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
        Assert.True(ambientValue.TryGetVector3(out var ambientColor));
        return ambientColor;
    }

    private static bool TryReadReflectionTextureId(MaterialAsset material, out Guid reflectionTextureId)
    {
        reflectionTextureId = Guid.Empty;
        return material.TryGetPropertyValue("reflection_texture", out var reflectionTextureValue)
            && reflectionTextureValue.TryGetTextureId(out reflectionTextureId)
            && reflectionTextureId != Guid.Empty;
    }

    private sealed class StubLegacyImportProfile : ILegacyMaterialImportProfile
    {
        private readonly LegacyMaterialImportInterpretation _interpretation;

        public StubLegacyImportProfile(LegacyMaterialImportInterpretation interpretation)
        {
            _interpretation = interpretation;
        }

        public LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context)
            => _interpretation;
    }
}