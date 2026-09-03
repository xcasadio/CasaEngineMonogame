using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;

namespace CasaEngine.Tests.Assets;

/// <summary>
/// The asset manager caches tile maps by id and nothing ever unloads them, so every world showing the
/// same map used to receive the very same instance - and a running game writes into it.
///
/// Reported in play: after several map changes the ship deck lost most of its tiles. The Alundra port
/// moves wall and floor tiles out of the base layers so they can be depth-sorted against sprites, which
/// removes them from the layer. On the second visit they were already gone, 774 wall and 582 floor
/// placements no longer matched the live map, and nobody drew those tiles.
/// </summary>
public class TileMapWorldWorkingCopyTests
{
    private static TileMapData BuildTemplate()
    {
        var data = new TileMapData { MapSize = new Size(2, 1) };
        data.CustomProperties["MapId"] = "389";

        var layer = new TileMapLayerData { Name = "ground" };
        layer.tiles.AddRange(new[] { 10, 11 });
        layer.tileSources.AddRange(new[] { 0, 0 });
        layer.tileFlags.AddRange(new[] { TileCellFlags.None, TileCellFlags.None });
        data.Layers.Add(layer);

        return data;
    }

    [Fact]
    public void AWorldWritingItsCopy_LeavesTheTemplateUntouched()
    {
        var template = BuildTemplate();

        var firstWorld = template.CreateWorldWorkingCopy();
        // What the depth interleave does: take the tile out of the base layer.
        firstWorld.Layers[0].tiles[0] = TileMapData.EmptyTileId;

        Assert.Equal(10, template.Layers[0].tiles[0]);
    }

    [Fact]
    public void TheNextWorldGetsThePristineTiles_NotWhatThePreviousWorldRemoved()
    {
        var template = BuildTemplate();

        var firstWorld = template.CreateWorldWorkingCopy();
        firstWorld.Layers[0].tiles[0] = TileMapData.EmptyTileId;

        var secondWorld = template.CreateWorldWorkingCopy();

        // The whole point: a second visit must still find the tile the first visit moved out.
        Assert.Equal(10, secondWorld.Layers[0].tiles[0]);
        Assert.NotSame(firstWorld.Layers[0].tiles, secondWorld.Layers[0].tiles);
    }

    [Fact]
    public void TheCopySharesWhatIsNeverWritten_AndDuplicatesOnlyThePerTileLists()
    {
        var template = BuildTemplate();

        var copy = template.CreateWorldWorkingCopy();

        // Duplicated: the three per-tile lists are the whole of the mutable state.
        Assert.NotSame(template.Layers[0].tiles, copy.Layers[0].tiles);
        Assert.NotSame(template.Layers[0].tileSources, copy.Layers[0].tileSources);
        Assert.NotSame(template.Layers[0].tileFlags, copy.Layers[0].tileFlags);

        // Carried across unchanged, so a map change costs a few integer lists, not a re-parse.
        Assert.Equal(template.MapSize, copy.MapSize);
        Assert.Equal("389", copy.CustomProperties["MapId"]);
        Assert.Equal("ground", copy.Layers[0].Name);
        Assert.Equal(template.Layers[0].zOffset, copy.Layers[0].zOffset);
    }

    [Fact]
    public void TheTileMapComponentTakesItsOwnWorkingCopy()
    {
        // Source guard, same shape as MonoGameBasicEffectUsageTests: the behaviour above is only worth
        // anything if the one production loader actually asks for a copy, and a component-level montage
        // would need a live Game just to reach that line.
        string repositoryRoot = FindRepositoryRoot();
        string componentPath = Path.Combine(
            repositoryRoot, "CasaEngine", "Framework", "Scene", "Entities", "Components", "TileMapComponent.cs");

        Assert.True(File.Exists(componentPath), $"expected the tile map component at '{componentPath}'.");

        string source = File.ReadAllText(componentPath);
        int loadIndex = source.IndexOf("Load<TileMapData>(TileMapDataAssetId)", StringComparison.Ordinal);

        Assert.True(loadIndex >= 0, "the component must still be the one place that loads its tile map data.");
        Assert.Contains(
            "CreateWorldWorkingCopy()",
            source.Substring(loadIndex, Math.Min(200, source.Length - loadIndex)),
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CasaEngine", "Framework")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("repository root not found from " + AppContext.BaseDirectory);
    }
}
