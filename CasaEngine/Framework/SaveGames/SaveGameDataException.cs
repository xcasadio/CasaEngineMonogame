namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// A save-game value that has the wrong shape or is refused: a missing or mistyped field, an integer outside
/// its C# type, a non-finite float, an array whose length differs from the caller's, a malformed string
/// (ADR-0044). Thrown while writing as well as while reading, by <see cref="SaveGameArchive"/> and by the
/// format implementations. It is the only exception type the save-game service turns into an
/// <c>InvalidData</c> result; any other exception, notably one thrown by a game's
/// <see cref="ISaveGameData.Serialize"/>, propagates.
/// </summary>
internal sealed class SaveGameDataException : Exception
{
    public SaveGameDataException(string fieldPath, string reason)
        : this(fieldPath, reason, null)
    {
    }

    public SaveGameDataException(string fieldPath, string reason, Exception innerException)
        : base($"Invalid save-game data at '{fieldPath}': {reason}", innerException)
    {
        FieldPath = fieldPath;
        Reason = reason;
    }

    /// <summary>
    /// The offending field, with its nesting path: object names joined by '.', an array element as
    /// <c>name[index]</c>, for example <c>player.inventory[3]</c>.
    /// </summary>
    public string FieldPath { get; }

    /// <summary>Why the value was refused, without the field path.</summary>
    public string Reason { get; }
}
