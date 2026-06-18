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
            string tileSetPath = Path.Combine(tempDirectory, "ImportedLevel.tileset");
            string texturePath = Path.Combine(tempDirectory, "tiles.texture");
            string assetCatalogPath = Path.Combine(tempDirectory, "AssetInfos.json");

            Assert.True(File.Exists(tileMapPath));
            Assert.True(File.Exists(tileSetPath));
            Assert.True(File.Exists(texturePath));
            Assert.True(File.Exists(assetCatalogPath));
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "ImportedLevel_Imported")));
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "Textures")));

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
            Assert.True(File.Exists(Path.Combine(tempDirectory, "EmbeddedLevel.tileset")));
            Assert.True(File.Exists(Path.Combine(tempDirectory, "tiles with space.texture")));
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "EmbeddedLevel_Imported")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_TiledTmxSupportsMultipleTilesets()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            string groundImagePath = Path.Combine(tempDirectory, "ground.png");
            string decorImagePath = Path.Combine(tempDirectory, "decor.png");
            File.WriteAllBytes(groundImagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            File.WriteAllBytes(decorImagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string groundTsxPath = Path.Combine(tempDirectory, "ground.tsx");
            File.WriteAllText(groundTsxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.10.2" name="ground" tilewidth="16" tileheight="16" tilecount="2" columns="2">
                 <image source="ground.png" width="32" height="16"/>
                </tileset>
                """);

            string decorTsxPath = Path.Combine(tempDirectory, "decor.tsx");
            File.WriteAllText(decorTsxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.10.2" name="decor" tilewidth="16" tileheight="16" tilecount="2" columns="2">
                 <image source="decor.png" width="32" height="16"/>
                </tileset>
                """);

            string tmxPath = Path.Combine(tempDirectory, "multi.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="4" height="1" tilewidth="16" tileheight="16" infinite="0">
                 <tileset firstgid="1" source="ground.tsx"/>
                 <tileset firstgid="3" source="decor.tsx"/>
                 <layer id="1" name="Mixed" width="4" height="1">
                  <data encoding="csv">1,3,4,0</data>
                 </layer>
                </map>
                """);

            bool imported = EditorAssetImportService.ImportFile(tmxPath, Path.Combine(tempDirectory, "MultiLevel.tmx"));

            Assert.True(imported);
            string tileMapPath = Path.Combine(tempDirectory, "MultiLevel.tileMap");
            string groundTileSetPath = Path.Combine(tempDirectory, "ground.tileset");
            string decorTileSetPath = Path.Combine(tempDirectory, "decor.tileset");

            Assert.True(File.Exists(tileMapPath));
            Assert.True(File.Exists(groundTileSetPath));
            Assert.True(File.Exists(decorTileSetPath));
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "MultiLevel_Imported")));

            var tileMapDocument = JObject.Parse(File.ReadAllText(tileMapPath));
            var tileSetIds = Assert.IsType<JArray>(tileMapDocument["tile_set_asset_ids"]);
            Assert.Equal(2, tileSetIds.Count);
            Assert.Equal((string?)tileSetIds[0], (string?)tileMapDocument["tile_set_asset_id"]);

            var layers = Assert.IsType<JArray>(tileMapDocument["layers"]);
            var mixedLayer = Assert.IsType<JObject>(layers[0]);
            Assert.Equal(new[] { 0, 0, 1, -1 }, mixedLayer["tiles"]!.Values<int>().ToArray());
            Assert.Equal(new[] { 0, 1, 1, 0 }, mixedLayer["tile_sources"]!.Values<int>().ToArray());

            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult!.CreatedAssetFileNames, fileName => fileName.EndsWith("ground.tileset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(EditorAssetImportService.LastTiledMapImportResult.CreatedAssetFileNames, fileName => fileName.EndsWith("decor.tileset", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_TiledTmxImportsTileAnimations()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            string imagePath = Path.Combine(tempDirectory, "animated.png");
            File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string tsxPath = Path.Combine(tempDirectory, "animated.tsx");
            File.WriteAllText(tsxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.10.2" name="animated" tilewidth="16" tileheight="16" tilecount="2" columns="2">
                 <image source="animated.png" width="32" height="16"/>
                 <tile id="0">
                  <animation>
                   <frame tileid="0" duration="120"/>
                   <frame tileid="1" duration="80"/>
                  </animation>
                 </tile>
                </tileset>
                """);

            string tmxPath = Path.Combine(tempDirectory, "animated.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="1" height="1" tilewidth="16" tileheight="16" infinite="0">
                 <tileset firstgid="1" source="animated.tsx"/>
                 <layer id="1" name="Ground" width="1" height="1">
                  <data encoding="csv">1</data>
                 </layer>
                </map>
                """);

            bool imported = EditorAssetImportService.ImportFile(tmxPath, Path.Combine(tempDirectory, "AnimatedLevel.tmx"));

            Assert.True(imported);
            string tileSetPath = Path.Combine(tempDirectory, "AnimatedLevel.tileset");
            Assert.True(File.Exists(tileSetPath));

            var tileSetDocument = JObject.Parse(File.ReadAllText(tileSetPath));
            var tiles = Assert.IsType<JArray>(tileSetDocument["tiles"]);
            var animatedTile = Assert.IsType<JObject>(tiles[0]);
            Assert.Equal("Animated", (string?)animatedTile["type"]);

            var location = Assert.IsType<JObject>(animatedTile["location"]);
            Assert.Equal(0, location["x"]!.Value<int>());
            Assert.Equal(0, location["y"]!.Value<int>());
            Assert.Equal(16, location["w"]!.Value<int>());
            Assert.Equal(16, location["h"]!.Value<int>());

            var frames = Assert.IsType<JArray>(animatedTile["animation_frames"]);
            Assert.Equal(2, frames.Count);
            var firstFrame = Assert.IsType<JObject>(frames[0]);
            Assert.Equal(0, firstFrame["tile_id"]!.Value<int>());
            Assert.Equal(120, firstFrame["duration_ms"]!.Value<int>());
            var secondFrame = Assert.IsType<JObject>(frames[1]);
            Assert.Equal(1, secondFrame["tile_id"]!.Value<int>());
            Assert.Equal(80, secondFrame["duration_ms"]!.Value<int>());
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
                        string tileSetPath = Path.Combine(tempDirectory, "JsonLevel.tileset");
                        string texturePath = Path.Combine(tempDirectory, "tiles.texture");

                        Assert.True(File.Exists(tileMapPath));
                        Assert.True(File.Exists(tileSetPath));
                        Assert.True(File.Exists(texturePath));
                        Assert.False(Directory.Exists(Path.Combine(tempDirectory, "JsonLevel_Imported")));
                        Assert.False(Directory.Exists(Path.Combine(tempDirectory, "Textures")));

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
        public void ImportFile_TiledJsonImportsTileAnimations()
        {
                string tempDirectory = CreateTempDirectory();
                string? previousProjectPath = EngineEnvironment.ProjectPath;

                try
                {
                        EngineEnvironment.ProjectPath = tempDirectory;
                        EditorAssetCatalogService.Clear();

                        string imagePath = Path.Combine(tempDirectory, "animated.png");
                        File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

                        string tsjPath = Path.Combine(tempDirectory, "animated.tsj");
                        File.WriteAllText(tsjPath,
                                """
                                {
                                    "type": "tileset",
                                    "name": "animated",
                                    "tilewidth": 16,
                                    "tileheight": 16,
                                    "tilecount": 2,
                                    "columns": 2,
                                    "image": "animated.png",
                                    "imagewidth": 32,
                                    "imageheight": 16,
                                    "tiles": [
                                        {
                                            "id": 0,
                                            "animation": [
                                                { "tileid": 0, "duration": 120 },
                                                { "tileid": 1, "duration": 80 }
                                            ]
                                        }
                                    ]
                                }
                                """);

                        string tmjPath = Path.Combine(tempDirectory, "animated.tmj");
                        File.WriteAllText(tmjPath,
                                """
                                {
                                    "type": "map",
                                    "orientation": "orthogonal",
                                    "infinite": false,
                                    "width": 1,
                                    "height": 1,
                                    "tilewidth": 16,
                                    "tileheight": 16,
                                    "tilesets": [
                                        { "firstgid": 1, "source": "animated.tsj" }
                                    ],
                                    "layers": [
                                        {
                                            "type": "tilelayer",
                                            "name": "Ground",
                                            "width": 1,
                                            "height": 1,
                                            "data": [1]
                                        }
                                    ]
                                }
                                """);

                        bool imported = EditorAssetImportService.ImportFile(tmjPath, Path.Combine(tempDirectory, "AnimatedJsonLevel.tmj"));

                        Assert.True(imported);
                        string tileSetPath = Path.Combine(tempDirectory, "AnimatedJsonLevel.tileset");
                        var tileSetDocument = JObject.Parse(File.ReadAllText(tileSetPath));
                        var tiles = Assert.IsType<JArray>(tileSetDocument["tiles"]);
                        var animatedTile = Assert.IsType<JObject>(tiles[0]);
                        Assert.Equal("Animated", (string?)animatedTile["type"]);
                        var frames = Assert.IsType<JArray>(animatedTile["animation_frames"]);
                        Assert.Equal(new[] { 0, 1 }, frames.Select(frame => frame!["tile_id"]!.Value<int>()).ToArray());
                        Assert.Equal(new[] { 120, 80 }, frames.Select(frame => frame!["duration_ms"]!.Value<int>()).ToArray());
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
}