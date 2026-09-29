namespace CasaEngine.Framework.SaveGames;

/// <summary>The file format a save game is written in (ADR-0044). Loading detects the format by itself.</summary>
public enum SaveGameFormat
{
    /// <summary>Readable JSON document, fields found by name, no checksum: meant for debugging.</summary>
    Json,

    /// <summary>Compact positional binary payload with a final CRC-32: meant for the shipped game.</summary>
    Binary,
}

/// <summary>Maps the public <see cref="SaveGameFormat"/> to and from the internal <see cref="SaveGameEnvelopeFormat"/>.</summary>
internal static class SaveGameFormatMapping
{
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined value.</exception>
    public static SaveGameEnvelopeFormat ToEnvelopeFormat(SaveGameFormat format)
    {
        switch (format)
        {
            case SaveGameFormat.Json:
                return SaveGameEnvelopeFormat.Json;
            case SaveGameFormat.Binary:
                return SaveGameEnvelopeFormat.Binary;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown save-game format.");
        }
    }

    public static SaveGameFormat FromEnvelopeFormat(SaveGameEnvelopeFormat format)
    {
        switch (format)
        {
            case SaveGameEnvelopeFormat.Json:
                return SaveGameFormat.Json;
            case SaveGameEnvelopeFormat.Binary:
                return SaveGameFormat.Binary;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown save-game envelope format.");
        }
    }
}
