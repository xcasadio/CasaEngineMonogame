namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// Outcome of <see cref="SaveGameService.TryLoad{T}"/>, and the readability of a slot in
/// <see cref="SaveGameService.ListSlots"/>.
/// </summary>
public enum SaveGameLoadStatus
{
    /// <summary>The slot was read and decoded.</summary>
    Loaded,

    /// <summary>The slot does not exist. Not logged: a game without a save yet is normal.</summary>
    NotFound,

    /// <summary>The slot file exceeds the slot size limit (1 MiB); it was not read.</summary>
    TooLarge,

    /// <summary>The binary file is truncated or fails its CRC-32: accidental corruption.</summary>
    Corrupted,

    /// <summary>The file's container version is unknown to this engine (zero, negative or newer).</summary>
    UnsupportedContainer,

    /// <summary>
    /// The file was written with a data version newer than <see cref="ISaveGameData.LatestDataVersion"/>: an older
    /// build never loads data a newer build wrote.
    /// </summary>
    NewerDataVersion,

    /// <summary>
    /// The file's content is refused: unknown format, malformed document, a missing field or a value with the wrong
    /// shape or outside its C# type. <see cref="SaveGameLoadResult.Message"/> names the field.
    /// </summary>
    InvalidData,

    /// <summary>The file system refused the read (locked file, access denied).</summary>
    IoError,
}

/// <summary>
/// Result of <see cref="SaveGameService.TryLoad{T}"/>. Every failure is already logged with its context by the
/// service; <see cref="Message"/> repeats that context (slot file path and cause) for the game's own diagnostics.
/// </summary>
public sealed class SaveGameLoadResult
{
    internal static readonly SaveGameLoadResult Loaded = new(SaveGameLoadStatus.Loaded, string.Empty);

    internal SaveGameLoadResult(SaveGameLoadStatus status, string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public SaveGameLoadStatus Status { get; }

    /// <summary>The context of a failure or of <see cref="SaveGameLoadStatus.NotFound"/>; empty when loaded.</summary>
    public string Message { get; }

    /// <summary>True only for <see cref="SaveGameLoadStatus.Loaded"/>.</summary>
    public bool IsLoaded => Status == SaveGameLoadStatus.Loaded;

    public override string ToString()
    {
        return Message.Length == 0 ? Status.ToString() : Status + ": " + Message;
    }
}
