using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;

namespace CasaEngine.Framework.SaveGames;

internal enum SaveGameStorageStatus
{
    Success,
    NotFound,
    TooLarge,
    IoError,
}

/// <summary>
/// Outcome of a <see cref="SaveGameFileStorage"/> operation. <see cref="Message"/> carries the logged context
/// (operation, path, cause) for <see cref="SaveGameStorageStatus.TooLarge"/> and
/// <see cref="SaveGameStorageStatus.IoError"/>; it is empty otherwise.
/// </summary>
internal readonly struct SaveGameStorageResult
{
    private SaveGameStorageResult(SaveGameStorageStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public SaveGameStorageStatus Status { get; }
    public string Message { get; }
    public bool IsSuccess => Status == SaveGameStorageStatus.Success;

    public static SaveGameStorageResult Success { get; } = new(SaveGameStorageStatus.Success, string.Empty);
    public static SaveGameStorageResult NotFound { get; } = new(SaveGameStorageStatus.NotFound, string.Empty);

    public static SaveGameStorageResult TooLarge(string message) => new(SaveGameStorageStatus.TooLarge, message);
    public static SaveGameStorageResult IoError(string message) => new(SaveGameStorageStatus.IoError, message);
}

/// <summary>
/// Stores save-game slots as <c>&lt;slot&gt;.sav</c> files in one folder (ADR-0044). Slot names follow
/// <see cref="SaveGameNames"/>; a refused name throws <see cref="ArgumentException"/>. File-system failures
/// (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>) never escape: they become an
/// <see cref="SaveGameStorageStatus.IoError"/> result, logged with the operation and the path.
/// Not thread-safe: the game calls it synchronously, off the hot paths.
/// </summary>
internal sealed class SaveGameFileStorage
{
    /// <summary>Largest slot file <see cref="TryRead"/> accepts, checked before any allocation.</summary>
    public const int MaxSlotSizeBytes = 1024 * 1024;

    public const string DefaultFolderName = "SaveGames";

    // ProjectSettings.ProjectName's default value (ProjectSettings.cs): a project that never set its name must
    // not share a save folder with every other such project.
    private const string UndefinedProjectName = "Project name undefined";

    private readonly Func<string> _localApplicationDataProvider;
    private readonly Func<string> _projectNameProvider;
    private string _folderPath;

    /// <summary>
    /// Default folder: <c>LocalApplicationData/&lt;ProjectName&gt;/SaveGames</c>, resolved at the first I/O
    /// call (the project settings may not be loaded yet at construction), then kept for the storage's lifetime.
    /// </summary>
    public SaveGameFileStorage()
        : this(
            () => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            () => GameSettings.ProjectSettings.ProjectName)
    {
    }

    /// <summary>An explicit folder (tests, tools). Nothing is created until the first write.</summary>
    public SaveGameFileStorage(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("The save-game folder path must not be empty.", nameof(folderPath));
        }

        _folderPath = Path.GetFullPath(folderPath);
    }

    /// <summary>
    /// Default-folder resolution with injectable sources: lets tests exercise it under a temporary root
    /// instead of the real LocalApplicationData.
    /// </summary>
    internal SaveGameFileStorage(Func<string> localApplicationDataProvider, Func<string> projectNameProvider)
    {
        ArgumentNullException.ThrowIfNull(localApplicationDataProvider);
        ArgumentNullException.ThrowIfNull(projectNameProvider);

        _localApplicationDataProvider = localApplicationDataProvider;
        _projectNameProvider = projectNameProvider;
    }

    /// <summary>
    /// The folder holding the slots, resolving the default one on first use. Touches no disk.
    /// </summary>
    /// <exception cref="InvalidOperationException">The default folder cannot be resolved safely.</exception>
    internal string ResolveFolderPath()
    {
        if (_folderPath == null)
        {
            _folderPath = ResolveDefaultFolderPath(_localApplicationDataProvider(), _projectNameProvider());
        }

        return _folderPath;
    }

    public bool Exists(string slotName)
    {
        return File.Exists(GetSlotPath(slotName));
    }

    /// <summary>
    /// Reads a whole slot. The file length is compared to <see cref="MaxSlotSizeBytes"/> before anything is
    /// allocated, and at most that length plus one byte is ever read. <paramref name="data"/> is null unless
    /// the result is <see cref="SaveGameStorageStatus.Success"/>.
    /// </summary>
    public SaveGameStorageResult TryRead(string slotName, out byte[] data)
    {
        data = null;
        string slotPath = GetSlotPath(slotName);

        try
        {
            using var stream = new FileStream(slotPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            long length = stream.Length;
            if (length > MaxSlotSizeBytes)
            {
                string message = $"Save-game read refused for '{slotPath}': {length} bytes exceeds the {MaxSlotSizeBytes}-byte slot limit.";
                Logs.WriteWarning(message);
                return SaveGameStorageResult.TooLarge(message);
            }

            var buffer = new byte[(int)length];
            int totalRead = ReadUpTo(stream, buffer);

            // The length was checked above; a file that shrank or grew since then is not a consistent snapshot.
            // Probing one byte keeps the total read at most MaxSlotSizeBytes + 1.
            if (totalRead != buffer.Length || stream.ReadByte() != -1)
            {
                string message = $"Save-game read failed for '{slotPath}': the file changed size while it was being read.";
                Logs.WriteError(message);
                return SaveGameStorageResult.IoError(message);
            }

            data = buffer;
            return SaveGameStorageResult.Success;
        }
        catch (FileNotFoundException)
        {
            return SaveGameStorageResult.NotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return SaveGameStorageResult.NotFound;
        }
        catch (IOException exception)
        {
            return LogIoError("read", slotPath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return LogIoError("read", slotPath, exception);
        }
    }

    public SaveGameStorageResult Write(string slotName, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Write(slotName, stream => stream.Write(data, 0, data.Length));
    }

    /// <summary>
    /// Writes a slot by replacement: <paramref name="writeContent"/> fills a new temporary file
    /// <c>&lt;slot&gt;.&lt;guid&gt;.tmp</c> in the same folder, which is flushed to disk then moved over the slot.
    /// On any failure the temporary file is deleted and the previous slot content is left as it was.
    /// An I/O failure becomes an <see cref="SaveGameStorageStatus.IoError"/> result; any other exception thrown by
    /// <paramref name="writeContent"/> propagates to the caller after the cleanup.
    /// </summary>
    public SaveGameStorageResult Write(string slotName, Action<Stream> writeContent)
    {
        ArgumentNullException.ThrowIfNull(writeContent);

        string slotPath = GetSlotPath(slotName);
        string folderPath = ResolveFolderPath();
        string tempPath = Path.Combine(folderPath, slotName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool tempCreated = false;
        bool moved = false;

        try
        {
            Directory.CreateDirectory(folderPath);

            // CreateNew fails if anything already exists at the temporary path, so an existing file or link is
            // never followed or truncated; FileShare.None keeps other handles out while it is written.
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                tempCreated = true;
                writeContent(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, slotPath, overwrite: true);
            moved = true;
            return SaveGameStorageResult.Success;
        }
        catch (IOException exception)
        {
            return LogIoError("write", slotPath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return LogIoError("write", slotPath, exception);
        }
        finally
        {
            if (tempCreated && !moved)
            {
                DeleteTemporaryFile(tempPath);
            }
        }
    }

    /// <summary>
    /// Deletes a slot: <see cref="SaveGameStorageStatus.NotFound"/> when it does not exist.
    /// </summary>
    public SaveGameStorageResult Delete(string slotName)
    {
        string slotPath = GetSlotPath(slotName);

        try
        {
            if (!File.Exists(slotPath))
            {
                return SaveGameStorageResult.NotFound;
            }

            File.Delete(slotPath);
            return SaveGameStorageResult.Success;
        }
        catch (IOException exception)
        {
            return LogIoError("delete", slotPath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return LogIoError("delete", slotPath, exception);
        }
    }

    /// <summary>
    /// Lists the slots: only files whose extension is exactly ".sav" (ordinal) and whose name passes the slot rule,
    /// in ordinal order. A missing folder is an empty list, and is not created.
    /// </summary>
    public SaveGameStorageResult EnumerateSlots(out IReadOnlyList<string> slotNames)
    {
        slotNames = Array.Empty<string>();
        string folderPath = ResolveFolderPath();

        try
        {
            if (!Directory.Exists(folderPath))
            {
                return SaveGameStorageResult.Success;
            }

            string[] filePaths = Directory.GetFiles(folderPath);
            var names = new List<string>(filePaths.Length);
            for (int i = 0; i < filePaths.Length; i++)
            {
                string fileName = Path.GetFileName(filePaths[i]);
                if (!string.Equals(Path.GetExtension(fileName), SaveGameNames.SlotFileExtension, StringComparison.Ordinal))
                {
                    continue;
                }

                string slotName = Path.GetFileNameWithoutExtension(fileName);
                if (SaveGameNames.IsValidSlotName(slotName))
                {
                    names.Add(slotName);
                }
            }

            // File names in one folder are unique, so the ordinal order is total and the result is stable.
            names.Sort(StringComparer.Ordinal);
            slotNames = names;
            return SaveGameStorageResult.Success;
        }
        catch (IOException exception)
        {
            return LogIoError("enumerate", folderPath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return LogIoError("enumerate", folderPath, exception);
        }
    }

    internal static string ResolveDefaultFolderPath(string localApplicationDataPath, string projectName)
    {
        if (string.IsNullOrEmpty(localApplicationDataPath))
        {
            throw new InvalidOperationException(
                "Save games: the LocalApplicationData folder is not available on this system (Environment.GetFolderPath returned an empty path).");
        }

        if (!Path.IsPathFullyQualified(localApplicationDataPath))
        {
            throw new InvalidOperationException(
                "Save games: the LocalApplicationData folder path is not fully qualified.");
        }

        if (string.IsNullOrEmpty(projectName) || string.Equals(projectName, UndefinedProjectName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Save games: ProjectSettings.ProjectName is not set; the project name names the save-game folder.");
        }

        if (!SaveGameNames.IsValidProjectFolderName(projectName))
        {
            throw new InvalidOperationException(
                $"Save games: ProjectSettings.ProjectName is not a valid folder name: expected 1 to {SaveGameNames.MaxProjectFolderNameLength} ASCII letters, digits, spaces, '_' and '-', starting with a letter or a digit, not ending with a space, and not a Windows reserved device name. It is not sanitized, so that two projects never share a folder.");
        }

        // A drive root keeps its separator after trimming ("C:\"), so the prefix only adds one when missing.
        string rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localApplicationDataPath));
        string rootPrefix = Path.EndsInDirectorySeparator(rootPath) ? rootPath : rootPath + Path.DirectorySeparatorChar;
        string folderPath = Path.GetFullPath(Path.Combine(rootPath, projectName, DefaultFolderName));
        if (!folderPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Save games: the save-game folder '{folderPath}' is not inside LocalApplicationData '{rootPath}'.");
        }

        return folderPath;
    }

    private string GetSlotPath(string slotName)
    {
        SaveGameNames.ThrowIfInvalidSlotName(slotName, nameof(slotName));

        return Path.Combine(ResolveFolderPath(), SaveGameNames.GetSlotFileName(slotName));
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static void DeleteTemporaryFile(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException exception)
        {
            Logs.WriteWarning($"Save-game cleanup could not delete the temporary file '{tempPath}': {exception.GetType().Name}: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Logs.WriteWarning($"Save-game cleanup could not delete the temporary file '{tempPath}': {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static SaveGameStorageResult LogIoError(string operation, string path, Exception exception)
    {
        string message = $"Save-game {operation} failed for '{path}': {exception.GetType().Name}: {exception.Message}";
        Logs.WriteError(message);
        return SaveGameStorageResult.IoError(message);
    }
}
