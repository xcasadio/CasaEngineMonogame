using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasaEngine.Editor.Remote.Protocol;

/// <summary>What a serving editor publishes so that tools can find it (ADR-0092, plan P1).</summary>
public sealed class RemoteDiscoveryRecord
{
    public int ProtocolVersion { get; init; } = RemoteProtocol.Version;

    public int ProcessId { get; init; }

    /// <summary>Start time of the editor process, UTC ticks: a reused process id does not match it.</summary>
    public long ProcessStartUtcTicks { get; init; }

    /// <summary>Pipe name only, never a path or a server name.</summary>
    public string PipeName { get; init; } = string.Empty;

    /// <summary>Random token the editor repeats in <see cref="RemoteProtocol.HelloMethod"/>.</summary>
    public string Token { get; init; } = string.Empty;

    public string? ProjectPath { get; init; }
}

/// <summary>
/// Discovery files of serving editors, <c>%LOCALAPPDATA%/CasaEngine/editor-remote/&lt;pid&gt;.json</c>. Tools do not
/// trust them: a record is kept only when its pipe name has the editor's form and its process is alive with the same
/// start time.
/// </summary>
public static partial class RemoteDiscovery
{
    public const string PipeNamePrefix = "casaengine-editor-";

    /// <summary>Allowed process start time difference, for clocks read twice.</summary>
    private static readonly long StartTimeToleranceTicks = TimeSpan.FromSeconds(1).Ticks;

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CasaEngine",
        "editor-remote");

    [GeneratedRegex("^casaengine-editor-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex PipeNamePattern();

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    /// <summary>A new pipe name: the prefix and a random GUID.</summary>
    public static string CreatePipeName() => PipeNamePrefix + Guid.NewGuid().ToString("N");

    public static string CreateToken() => Guid.NewGuid().ToString("N");

    public static bool IsValidPipeName(string? pipeName) => pipeName != null && PipeNamePattern().IsMatch(pipeName);

    public static string GetFilePath(string directory, int processId) => Path.Combine(directory, processId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");

    /// <summary>Writes the record through a temporary file and a move, so a reader never sees half a file.</summary>
    public static string Write(string directory, RemoteDiscoveryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Directory.CreateDirectory(directory);
        string path = GetFilePath(directory, record.ProcessId);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(record, RemoteProtocol.JsonOptions));
        File.Move(temporary, path, overwrite: true);
        return path;
    }

    public static void Delete(string directory, int processId)
    {
        string path = GetFilePath(directory, processId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The valid records of <paramref name="directory"/>. <paramref name="getProcessStartUtcTicks"/> gives the start time
    /// of a live process, or null when it is not running (default: <see cref="GetProcessStartUtcTicks"/>).
    /// </summary>
    public static IReadOnlyList<RemoteDiscoveryRecord> ReadAll(string directory, Func<int, long?>? getProcessStartUtcTicks = null)
    {
        var records = new List<RemoteDiscoveryRecord>();
        if (!Directory.Exists(directory))
        {
            return records;
        }

        getProcessStartUtcTicks ??= GetProcessStartUtcTicks;
        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            RemoteDiscoveryRecord? record = TryRead(path);
            if (record != null && IsValid(record, Path.GetFileNameWithoutExtension(path), getProcessStartUtcTicks))
            {
                records.Add(record);
            }
        }

        records.Sort((left, right) => left.ProcessId.CompareTo(right.ProcessId));
        return records;
    }

    public static long? GetProcessStartUtcTicks(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static RemoteDiscoveryRecord? TryRead(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 64 * 1024)
            {
                return null;
            }

            return JsonSerializer.Deserialize<RemoteDiscoveryRecord>(File.ReadAllBytes(path), RemoteProtocol.JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static bool IsValid(RemoteDiscoveryRecord record, string fileName, Func<int, long?> getProcessStartUtcTicks)
    {
        if (record.ProtocolVersion != RemoteProtocol.Version
            || !IsValidPipeName(record.PipeName)
            || !TokenPattern().IsMatch(record.Token ?? string.Empty)
            || record.ProcessId <= 0
            || fileName != record.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            return false;
        }

        long? startTicks = getProcessStartUtcTicks(record.ProcessId);
        return startTicks.HasValue && Math.Abs(startTicks.Value - record.ProcessStartUtcTicks) <= StartTimeToleranceTicks;
    }
}
