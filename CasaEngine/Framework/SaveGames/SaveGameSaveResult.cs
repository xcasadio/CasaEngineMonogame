namespace CasaEngine.Framework.SaveGames;

/// <summary>Outcome of <see cref="SaveGameService.Save"/> or <see cref="SaveGameService.Delete"/>.</summary>
public enum SaveGameSaveStatus
{
    /// <summary><see cref="SaveGameService.Save"/> wrote the slot.</summary>
    Saved,

    /// <summary><see cref="SaveGameService.Delete"/> removed the slot.</summary>
    Deleted,

    /// <summary><see cref="SaveGameService.Delete"/> found no such slot. Not logged: nothing went wrong.</summary>
    NotFound,

    /// <summary>
    /// The serialized save exceeds the slot size limit (1 MiB). Nothing was written: the previous slot content,
    /// if any, is unchanged.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The object or the metadata wrote a refused value (a non-finite float, a null or malformed string, a null
    /// metadata value). Nothing was written.
    /// </summary>
    InvalidData,

    /// <summary>
    /// The file system refused the operation (locked file, access denied, disk full). The previous slot content, if
    /// any, is unchanged.
    /// </summary>
    IoError,
}

/// <summary>
/// Result of a save-game write: <see cref="SaveGameService.Save"/> or <see cref="SaveGameService.Delete"/>.
/// Every failure is already logged with its context by the service; <see cref="Message"/> repeats that context
/// (slot file path and cause) for the game's own diagnostics.
/// </summary>
public sealed class SaveGameSaveResult
{
    internal static readonly SaveGameSaveResult Saved = new(SaveGameSaveStatus.Saved, string.Empty);
    internal static readonly SaveGameSaveResult Deleted = new(SaveGameSaveStatus.Deleted, string.Empty);

    internal SaveGameSaveResult(SaveGameSaveStatus status, string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public SaveGameSaveStatus Status { get; }

    /// <summary>The context of a failure or of <see cref="SaveGameSaveStatus.NotFound"/>; empty on success.</summary>
    public string Message { get; }

    /// <summary>True for <see cref="SaveGameSaveStatus.Saved"/> and <see cref="SaveGameSaveStatus.Deleted"/>.</summary>
    public bool IsSuccess => Status == SaveGameSaveStatus.Saved || Status == SaveGameSaveStatus.Deleted;

    public override string ToString()
    {
        return Message.Length == 0 ? Status.ToString() : Status + ": " + Message;
    }
}
