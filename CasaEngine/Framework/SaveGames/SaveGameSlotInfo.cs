using System.Collections.ObjectModel;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// One slot of <see cref="SaveGameService.ListSlots"/>: its header (format, data version, metadata) read without
/// decoding the data, or the reason it cannot be read.
/// </summary>
/// <remarks>
/// A readable slot may still fail <see cref="SaveGameService.TryLoad{T}"/>: with
/// <see cref="SaveGameLoadStatus.NewerDataVersion"/>, or with <see cref="SaveGameLoadStatus.InvalidData"/> when its
/// data does not match the game's object. <see cref="Metadata"/> is untrusted plain text from the file: bound its
/// length and disable MGUI's inline formatting before displaying it.
/// </remarks>
public sealed class SaveGameSlotInfo
{
    private SaveGameSlotInfo(
        string name,
        SaveGameLoadStatus status,
        string message,
        SaveGameFormat format,
        int dataVersion,
        IReadOnlyDictionary<string, string> metadata,
        DateTime? lastWriteTimeUtc)
    {
        Name = name;
        Status = status;
        Message = message ?? string.Empty;
        Format = format;
        DataVersion = dataVersion;
        Metadata = metadata;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    /// <summary>The slot name, as given to <see cref="SaveGameService.Save"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// <see cref="SaveGameLoadStatus.Loaded"/> when the header was read; otherwise why the slot is unreadable:
    /// <see cref="SaveGameLoadStatus.TooLarge"/>, <see cref="SaveGameLoadStatus.Corrupted"/>,
    /// <see cref="SaveGameLoadStatus.UnsupportedContainer"/>, <see cref="SaveGameLoadStatus.InvalidData"/> or
    /// <see cref="SaveGameLoadStatus.IoError"/>.
    /// </summary>
    public SaveGameLoadStatus Status { get; }

    /// <summary>True when the header was read: <see cref="Format"/>, <see cref="DataVersion"/> and <see cref="Metadata"/> are the file's.</summary>
    public bool IsReadable => Status == SaveGameLoadStatus.Loaded;

    /// <summary>Why the slot is unreadable (slot file path and cause); empty when readable.</summary>
    public string Message { get; }

    /// <summary>The file's format; <see cref="SaveGameFormat.Json"/> (the default value) when unreadable.</summary>
    public SaveGameFormat Format { get; }

    /// <summary>The data version the file was written with; zero when unreadable.</summary>
    public int DataVersion { get; }

    /// <summary>The file's metadata, read-only, ordinal keys; empty when unreadable.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// The slot file's last-write time in UTC, readable or not; null when the file system could not give it (the
    /// failure is logged).
    /// </summary>
    public DateTime? LastWriteTimeUtc { get; }

    internal static SaveGameSlotInfo Readable(
        string name,
        SaveGameFormat format,
        int dataVersion,
        IReadOnlyDictionary<string, string> metadata,
        DateTime? lastWriteTimeUtc)
    {
        var metadataCopy = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        return new SaveGameSlotInfo(
            name,
            SaveGameLoadStatus.Loaded,
            string.Empty,
            format,
            dataVersion,
            new ReadOnlyDictionary<string, string>(metadataCopy),
            lastWriteTimeUtc);
    }

    internal static SaveGameSlotInfo Unreadable(string name, SaveGameLoadStatus status, string message, DateTime? lastWriteTimeUtc)
    {
        if (status == SaveGameLoadStatus.Loaded)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "An unreadable slot needs a failure status.");
        }

        return new SaveGameSlotInfo(
            name,
            status,
            message,
            default,
            0,
            ReadOnlyDictionary<string, string>.Empty,
            lastWriteTimeUtc);
    }

    public override string ToString()
    {
        return IsReadable ? $"{Name} ({Format}, data version {DataVersion})" : $"{Name} (unreadable: {Status})";
    }
}
