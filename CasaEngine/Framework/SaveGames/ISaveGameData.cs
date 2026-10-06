namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// A game's save object (ADR-0044, modeled on Unreal's <c>USaveGame</c> and <c>Serialize(FArchive&amp;)</c>):
/// the game fills it, and the save-game service writes it to a slot or loads it back.
/// </summary>
/// <remarks>
/// <para>
/// One <see cref="Serialize"/> method runs in both directions, so reading and writing cannot diverge: each
/// <see cref="SaveGameArchive.Value(string, ref int)"/> call writes the field when saving and overwrites it
/// when loading. The calls must be the same, in the same order, in both directions: the binary format is
/// positional and only the JSON format finds fields by name. Field names must be unique inside one object.
/// </para>
/// <para>
/// Migration: when loading, <see cref="SaveGameArchive.DataVersion"/> is the version the file was written with,
/// never greater than <see cref="LatestDataVersion"/>; branch on it to read the fields of an older layout.
/// When saving, it is <see cref="LatestDataVersion"/>.
/// </para>
/// <para>
/// Save files are not authenticated. The archive only guarantees each value's shape and the range of its C#
/// type: after a successful load, and before touching any live state, the game validates what every value
/// means (ids, indices, counters, coordinates), then applies the object as a whole or not at all.
/// </para>
/// </remarks>
public interface ISaveGameData
{
    /// <summary>
    /// The data version this build writes, zero or greater. Increase it whenever the fields or their order change.
    /// </summary>
    int LatestDataVersion { get; }

    /// <summary>
    /// Writes or reads every field through <paramref name="archive"/>, depending on
    /// <see cref="SaveGameArchive.IsLoading"/>. Invalid data raises an exception that the save-game service turns
    /// into a result; let it propagate. Any exception this method throws on its own also propagates to the caller.
    /// </summary>
    void Serialize(SaveGameArchive archive);
}
