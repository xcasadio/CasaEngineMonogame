using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// The game's save-game API (ADR-0044, modeled on Unreal's <c>UGameplayStatics</c> save functions): named slots in
/// a per-user folder, each holding one <see cref="ISaveGameData"/> object written whole. Use the engine instance,
/// <see cref="CasaEngine.Framework.Application.GameSettings.SaveGames"/>.
/// </summary>
/// <remarks>
/// <para>
/// Folder: <c>LocalApplicationData/&lt;ProjectName&gt;/SaveGames</c>, one <c>&lt;slot&gt;.sav</c> file per slot,
/// resolved at the first call that needs it, never at construction. Slot names are 1 to 32 characters among
/// <c>a</c>-<c>z</c>, <c>0</c>-<c>9</c>, <c>_</c> and <c>-</c>, and not a Windows reserved device name.
/// </para>
/// <para>
/// Errors: every problem with a file or the file system (missing, too large, corrupted, refused content, locked,
/// access denied, disk full) is a result, logged with the slot file path and its cause, never an exception.
/// Developer misuse throws: an invalid slot name (<see cref="ArgumentException"/>), a null object
/// (<see cref="ArgumentNullException"/>), an undefined <see cref="SaveGameFormat"/> or a negative
/// <see cref="ISaveGameData.LatestDataVersion"/> (<see cref="ArgumentOutOfRangeException"/>), a project without a
/// valid <c>ProjectName</c> (<see cref="InvalidOperationException"/>), a field name written twice in one JSON object
/// (<see cref="ArgumentException"/>), unbalanced <see cref="SaveGameArchive.BeginObject"/> /
/// <see cref="SaveGameArchive.EndObject"/> calls (<see cref="InvalidOperationException"/>). Any exception the game's
/// own <see cref="ISaveGameData.Serialize"/> or constructor throws also propagates.
/// </para>
/// <para>
/// Save files are not authenticated: after <see cref="TryLoad{T}"/> returns <see cref="SaveGameLoadStatus.Loaded"/>,
/// the game validates what every value means before touching its live state (see <see cref="ISaveGameData"/>).
/// </para>
/// <para>
/// Synchronous and not thread-safe: the game calls it from its own thread, off the hot paths.
/// </para>
/// </remarks>
public sealed class SaveGameService
{
    private readonly SaveGameFileStorage _storage;

    /// <summary>The default per-user folder, resolved at the first I/O call. Touches no disk.</summary>
    internal SaveGameService()
        : this(new SaveGameFileStorage())
    {
    }

    /// <summary>An explicit slot folder (tests, tools). Nothing is created until the first save.</summary>
    internal SaveGameService(string folderPath)
        : this(new SaveGameFileStorage(folderPath))
    {
    }

    internal SaveGameService(SaveGameFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage;
    }

    /// <summary>The largest slot file, in bytes, a save may produce and a load accepts (1 MiB).</summary>
    public static int MaxSlotSizeBytes => SaveGameFileStorage.MaxSlotSizeBytes;

    /// <summary>
    /// Serializes <paramref name="data"/> in memory, then writes it to <paramref name="slot"/> by replacement: the
    /// slot holds either its previous content or the new one, as far as the file system guarantees.
    /// </summary>
    /// <param name="slot">The slot name.</param>
    /// <param name="data">The object to save; its <see cref="ISaveGameData.Serialize"/> runs once, saving.</param>
    /// <param name="format">The file format.</param>
    /// <param name="metadata">
    /// Optional plain-text entries read back by <see cref="ListSlots"/> without loading the data (a save date, a
    /// location name). Null writes none; a null value is refused as <see cref="SaveGameSaveStatus.InvalidData"/>.
    /// </param>
    /// <returns>
    /// <see cref="SaveGameSaveStatus.Saved"/>; <see cref="SaveGameSaveStatus.InvalidData"/> when the object or the
    /// metadata writes a refused value; <see cref="SaveGameSaveStatus.TooLarge"/> above
    /// <see cref="MaxSlotSizeBytes"/>; <see cref="SaveGameSaveStatus.IoError"/>. Nothing is written unless the result
    /// is <see cref="SaveGameSaveStatus.Saved"/>.
    /// </returns>
    public SaveGameSaveResult Save(string slot, ISaveGameData data, SaveGameFormat format, IReadOnlyDictionary<string, string> metadata = null)
    {
        string slotPath = _storage.GetSlotPath(slot);
        ArgumentNullException.ThrowIfNull(data);
        SaveGameEnvelopeFormat envelopeFormat = SaveGameFormatMapping.ToEnvelopeFormat(format);

        SaveGameEnvelopeResult writeResult = SaveGameEnvelope.TryWrite(envelopeFormat, data, metadata, out byte[] fileBytes);
        if (!writeResult.IsSuccess)
        {
            string message = DescribeSlot(slotPath, "save refused", writeResult.Message);
            Logs.WriteError(message);
            return new SaveGameSaveResult(SaveGameSaveStatus.InvalidData, message);
        }

        // Checked before writing, so that no slot is ever written that a load would refuse as too large.
        if (fileBytes.Length > SaveGameFileStorage.MaxSlotSizeBytes)
        {
            string message = DescribeSlot(
                slotPath,
                "save refused",
                $"the serialized save is {fileBytes.Length} bytes, above the {SaveGameFileStorage.MaxSlotSizeBytes}-byte slot limit; nothing was written.");
            Logs.WriteError(message);
            return new SaveGameSaveResult(SaveGameSaveStatus.TooLarge, message);
        }

        SaveGameStorageResult storageResult = _storage.Write(slot, fileBytes);
        switch (storageResult.Status)
        {
            case SaveGameStorageStatus.Success:
                return SaveGameSaveResult.Saved;
            case SaveGameStorageStatus.IoError:
                // Already logged by the storage, with the operation and the path.
                return new SaveGameSaveResult(SaveGameSaveStatus.IoError, storageResult.Message);
            default:
                throw UnexpectedStatus(storageResult.Status);
        }
    }

    /// <summary>
    /// Loads <paramref name="slot"/> into a new <typeparamref name="T"/>, detecting the file's format. On any result
    /// other than <see cref="SaveGameLoadStatus.Loaded"/>, <paramref name="data"/> is <c>default</c>: the game never
    /// receives a partly filled object.
    /// </summary>
    /// <typeparam name="T">The game's save object; the file never names a type.</typeparam>
    /// <param name="slot">The slot name.</param>
    /// <param name="data">The loaded object, or <c>default</c>.</param>
    /// <returns>
    /// <see cref="SaveGameLoadStatus.Loaded"/>, or <see cref="SaveGameLoadStatus.NotFound"/>,
    /// <see cref="SaveGameLoadStatus.TooLarge"/>, <see cref="SaveGameLoadStatus.Corrupted"/>,
    /// <see cref="SaveGameLoadStatus.UnsupportedContainer"/>, <see cref="SaveGameLoadStatus.NewerDataVersion"/>,
    /// <see cref="SaveGameLoadStatus.InvalidData"/> (naming the field), <see cref="SaveGameLoadStatus.IoError"/>.
    /// </returns>
    /// <remarks>
    /// A file with an older data version is loaded: <see cref="SaveGameArchive.DataVersion"/> then tells
    /// <see cref="ISaveGameData.Serialize"/> which layout to read. The loaded values have the right shape and C#
    /// range only; the game validates their meaning before applying them.
    /// </remarks>
    public SaveGameLoadResult TryLoad<T>(string slot, out T data)
        where T : ISaveGameData, new()
    {
        data = default;
        string slotPath = _storage.GetSlotPath(slot);

        // Created first so that a developer error in the object (a negative latest version) fails early, whatever
        // the slot holds.
        var target = new T();
        int latestDataVersion = target.LatestDataVersion;
        if (latestDataVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ISaveGameData.LatestDataVersion),
                latestDataVersion,
                $"{typeof(T).Name}.LatestDataVersion must be zero or greater.");
        }

        SaveGameStorageResult readResult = _storage.TryRead(slot, out byte[] fileBytes);
        if (!readResult.IsSuccess)
        {
            return FromStorageFailure(readResult, slotPath);
        }

        SaveGameEnvelopeResult openResult = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        if (!openResult.IsSuccess)
        {
            return FromEnvelopeFailure(openResult, slotPath, "load refused");
        }

        if (envelope.DataVersion > latestDataVersion)
        {
            string message = DescribeSlot(
                slotPath,
                "load refused",
                $"the file's data version {envelope.DataVersion} is newer than {typeof(T).Name}.LatestDataVersion {latestDataVersion}.");
            Logs.WriteWarning(message);
            return new SaveGameLoadResult(SaveGameLoadStatus.NewerDataVersion, message);
        }

        SaveGameEnvelopeResult deserializeResult = envelope.TryDeserialize(target);
        if (!deserializeResult.IsSuccess)
        {
            // The target may be partly overwritten: it is dropped, never returned.
            return FromEnvelopeFailure(deserializeResult, slotPath, "load refused");
        }

        data = target;
        return SaveGameLoadResult.Loaded;
    }

    /// <summary>True when <paramref name="slot"/> exists. Never throws for a file-system problem.</summary>
    public bool Exists(string slot)
    {
        return _storage.Exists(slot);
    }

    /// <summary>
    /// Deletes <paramref name="slot"/>: <see cref="SaveGameSaveStatus.Deleted"/>,
    /// <see cref="SaveGameSaveStatus.NotFound"/> when it does not exist, or <see cref="SaveGameSaveStatus.IoError"/>.
    /// </summary>
    public SaveGameSaveResult Delete(string slot)
    {
        string slotPath = _storage.GetSlotPath(slot);

        SaveGameStorageResult storageResult = _storage.Delete(slot);
        switch (storageResult.Status)
        {
            case SaveGameStorageStatus.Success:
                return SaveGameSaveResult.Deleted;
            case SaveGameStorageStatus.NotFound:
                return new SaveGameSaveResult(SaveGameSaveStatus.NotFound, DescribeSlot(slotPath, "delete", "the slot does not exist."));
            case SaveGameStorageStatus.IoError:
                // Already logged by the storage, with the operation and the path.
                return new SaveGameSaveResult(SaveGameSaveStatus.IoError, storageResult.Message);
            default:
                throw UnexpectedStatus(storageResult.Status);
        }
    }

    /// <summary>
    /// Lists the slots in ordinal name order, each with its header read without decoding the data, or flagged
    /// unreadable with its reason. Never throws for a file-system problem or a bad file: an unreadable slot is listed
    /// and logged, a slot deleted meanwhile is skipped, and a folder that cannot be listed gives an empty list
    /// (logged). A missing folder is an empty list.
    /// </summary>
    public IReadOnlyList<SaveGameSlotInfo> ListSlots()
    {
        SaveGameStorageResult enumerateResult = _storage.EnumerateSlots(out IReadOnlyList<string> slotNames);
        if (!enumerateResult.IsSuccess)
        {
            // Already logged by the storage, with the operation and the folder.
            return Array.Empty<SaveGameSlotInfo>();
        }

        var slots = new List<SaveGameSlotInfo>(slotNames.Count);
        for (int i = 0; i < slotNames.Count; i++)
        {
            SaveGameSlotInfo slot = ReadSlotInfo(slotNames[i]);
            if (slot != null)
            {
                slots.Add(slot);
            }
        }

        return slots.AsReadOnly();
    }

    /// <summary>The slot's header, its unreadable state, or null when the slot no longer exists.</summary>
    private SaveGameSlotInfo ReadSlotInfo(string slot)
    {
        string slotPath = _storage.GetSlotPath(slot);

        SaveGameStorageResult timeResult = _storage.TryGetLastWriteTimeUtc(slot, out DateTime lastWriteTime);
        if (timeResult.Status == SaveGameStorageStatus.NotFound)
        {
            return null;
        }

        // A time the file system could not give is logged by the storage; the slot is still listed.
        DateTime? lastWriteTimeUtc = timeResult.IsSuccess ? lastWriteTime : null;

        SaveGameStorageResult readResult = _storage.TryRead(slot, out byte[] fileBytes);
        if (readResult.Status == SaveGameStorageStatus.NotFound)
        {
            return null;
        }

        if (!readResult.IsSuccess)
        {
            SaveGameLoadResult failure = FromStorageFailure(readResult, slotPath);
            return SaveGameSlotInfo.Unreadable(slot, failure.Status, failure.Message, lastWriteTimeUtc);
        }

        SaveGameEnvelopeResult openResult = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        if (!openResult.IsSuccess)
        {
            SaveGameLoadResult failure = FromEnvelopeFailure(openResult, slotPath, "listed as unreadable");
            return SaveGameSlotInfo.Unreadable(slot, failure.Status, failure.Message, lastWriteTimeUtc);
        }

        return SaveGameSlotInfo.Readable(
            slot,
            SaveGameFormatMapping.FromEnvelopeFormat(envelope.Format),
            envelope.DataVersion,
            envelope.Metadata,
            lastWriteTimeUtc);
    }

    /// <summary>Maps a failed storage read; the storage has already logged TooLarge and IoError.</summary>
    private static SaveGameLoadResult FromStorageFailure(SaveGameStorageResult result, string slotPath)
    {
        switch (result.Status)
        {
            case SaveGameStorageStatus.NotFound:
                return new SaveGameLoadResult(SaveGameLoadStatus.NotFound, DescribeSlot(slotPath, "load", "the slot does not exist."));
            case SaveGameStorageStatus.TooLarge:
                return new SaveGameLoadResult(SaveGameLoadStatus.TooLarge, result.Message);
            case SaveGameStorageStatus.IoError:
                return new SaveGameLoadResult(SaveGameLoadStatus.IoError, result.Message);
            default:
                throw UnexpectedStatus(result.Status);
        }
    }

    /// <summary>Maps and logs a refused file; the envelope's message lacks the slot, which is added here.</summary>
    private static SaveGameLoadResult FromEnvelopeFailure(SaveGameEnvelopeResult result, string slotPath, string operation)
    {
        SaveGameLoadStatus status;
        switch (result.Status)
        {
            case SaveGameEnvelopeStatus.Corrupted:
                status = SaveGameLoadStatus.Corrupted;
                break;
            case SaveGameEnvelopeStatus.InvalidData:
                status = SaveGameLoadStatus.InvalidData;
                break;
            case SaveGameEnvelopeStatus.UnsupportedContainer:
                status = SaveGameLoadStatus.UnsupportedContainer;
                break;
            default:
                throw UnexpectedStatus(result.Status);
        }

        string message = DescribeSlot(slotPath, operation, result.Message);
        Logs.WriteWarning(message);
        return new SaveGameLoadResult(status, message);
    }

    private static string DescribeSlot(string slotPath, string operation, string cause)
    {
        return $"Save-game slot '{slotPath}' {operation}: {cause}";
    }

    /// <summary>A lower layer returned a status its contract excludes: an engine bug, not a file problem.</summary>
    private static InvalidOperationException UnexpectedStatus<TStatus>(TStatus status)
        where TStatus : struct, Enum
    {
        return new InvalidOperationException($"Save-game service: unexpected {typeof(TStatus).Name} '{status}'.");
    }
}
