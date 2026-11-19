using Newtonsoft.Json;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>A world of the project the cutscene preview can show.</summary>
/// <param name="WorldId">Id of the world asset.</param>
/// <param name="FileName">File name of the world asset, relative to the project.</param>
/// <param name="StartupCutsceneAssetId">Id of the cutscene the world plays when it begins, or <see cref="Guid.Empty"/>.</param>
public readonly record struct CutscenePreviewWorldCandidate(Guid WorldId, string FileName, Guid StartupCutsceneAssetId);

/// <summary>The ordered worlds offered for a cutscene and the one shown by default.</summary>
/// <param name="Worlds">The candidates, the default first. Empty when the project has no world.</param>
public sealed record CutscenePreviewWorldChoice(IReadOnlyList<CutscenePreviewWorldCandidate> Worlds)
{
    /// <summary>True when the project has at least one world.</summary>
    public bool HasWorld => Worlds.Count > 0;

    /// <summary>The world shown by default. Only valid when <see cref="HasWorld"/> is true.</summary>
    public CutscenePreviewWorldCandidate Default => Worlds[0];
}

/// <summary>
/// Chooses the world a cutscene is previewed in (ADR-0135). The order is: the world remembered for this cutscene, then the
/// worlds whose startup cutscene it is, then the project's first world, then every other world by file name. It reads no
/// file: the caller supplies the data.
/// </summary>
public static class CutscenePreviewWorldResolver
{
    private const string StartupCutsceneKey = "startup_cutscene_asset_id";

    /// <param name="worlds">Every world of the project.</param>
    /// <param name="cutsceneAssetId">Id of the edited cutscene.</param>
    /// <param name="firstWorldLoaded">The project's <c>FirstWorldLoaded</c> file name, possibly empty.</param>
    /// <param name="rememberedWorldId">The world remembered for this cutscene, or <see cref="Guid.Empty"/>. An id that matches no world is ignored.</param>
    public static CutscenePreviewWorldChoice Resolve(
        IReadOnlyList<CutscenePreviewWorldCandidate> worlds,
        Guid cutsceneAssetId,
        string firstWorldLoaded,
        Guid rememberedWorldId)
    {
        ArgumentNullException.ThrowIfNull(worlds);

        var ranked = new List<(int Rank, CutscenePreviewWorldCandidate World)>(worlds.Count);
        foreach (CutscenePreviewWorldCandidate world in worlds)
        {
            ranked.Add((RankOf(world, cutsceneAssetId, firstWorldLoaded, rememberedWorldId), world));
        }

        ranked.Sort(static (a, b) =>
        {
            int byRank = a.Rank.CompareTo(b.Rank);
            return byRank != 0
                ? byRank
                : string.Compare(a.World.FileName, b.World.FileName, StringComparison.OrdinalIgnoreCase);
        });

        var ordered = new List<CutscenePreviewWorldCandidate>(ranked.Count);
        foreach ((int _, CutscenePreviewWorldCandidate world) in ranked)
        {
            ordered.Add(world);
        }

        return new CutscenePreviewWorldChoice(ordered);
    }

    /// <summary>
    /// Reads only the <c>startup_cutscene_asset_id</c> key of a .world, without loading the world. Absent, nested, not a
    /// string or not a guid gives <see cref="Guid.Empty"/>.
    /// </summary>
    public static Guid ReadStartupCutsceneAssetId(TextReader worldJson)
    {
        ArgumentNullException.ThrowIfNull(worldJson);

        using var reader = new JsonTextReader(worldJson) { DateParseHandling = DateParseHandling.None, CloseInput = false };
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName
                && reader.Depth == 1
                && string.Equals((string)reader.Value, StartupCutsceneKey, StringComparison.Ordinal))
            {
                return reader.Read()
                       && reader.TokenType == JsonToken.String
                       && Guid.TryParse((string)reader.Value, out Guid id)
                    ? id
                    : Guid.Empty;
            }
        }

        return Guid.Empty;
    }

    private static int RankOf(CutscenePreviewWorldCandidate world, Guid cutsceneAssetId, string firstWorldLoaded, Guid rememberedWorldId)
    {
        if (rememberedWorldId != Guid.Empty && world.WorldId == rememberedWorldId)
        {
            return 0;
        }

        if (cutsceneAssetId != Guid.Empty && world.StartupCutsceneAssetId == cutsceneAssetId)
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(firstWorldLoaded)
            && string.Equals(NormalizePath(world.FileName), NormalizePath(firstWorldLoaded), StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/');
}
