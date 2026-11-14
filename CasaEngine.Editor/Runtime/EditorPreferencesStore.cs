using System;
using System.Diagnostics;
using System.IO;
using CasaEngine.Core.Logging;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// The editor's single holder of <see cref="EditorPreferences"/> (ADR-0091). A change is clamped, applied at once
/// through <see cref="Changed"/>, and written to the file later: <see cref="FlushIfDue"/>, called once per editor
/// frame, writes once after <see cref="WriteDelaySeconds"/> without a new change. It allocates nothing while idle.
/// </summary>
internal sealed class EditorPreferencesStore
{
    public const double WriteDelaySeconds = 1.0;

    private readonly string _filePath;
    private readonly Func<double> _clockSeconds;
    private readonly Action<string> _warn;
    private bool _dirty;
    private double _lastChangeSeconds;

    public EditorPreferencesStore(string filePath, Func<double> clockSeconds = null, Action<string> warn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _clockSeconds = clockSeconds ?? StopwatchSeconds;
        _warn = warn ?? Logs.WriteWarning;
    }

    /// <summary><c>%APPDATA%/CasaEngine/editor.preferences.json</c>.</summary>
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CasaEngine",
        "editor.preferences.json");

    public string FilePath => _filePath;

    public EditorPreferences Current { get; private set; } = EditorPreferences.Default;

    /// <summary>Raised after every accepted change, with the new preferences.</summary>
    public event Action<EditorPreferences> Changed;

    /// <summary>Number of handlers of <see cref="Changed"/>; lets tests check that disposed viewports unsubscribe.</summary>
    public int ChangedSubscriberCount => Changed?.GetInvocationList().Length ?? 0;

    /// <summary>Number of successful writes since creation (tests).</summary>
    public int WriteCount { get; private set; }

    public bool HasPendingWrite => _dirty;

    /// <summary>
    /// Reads the file. A missing file gives the defaults; an unreadable one gives the defaults with a warning and is
    /// left as it is until a preference changes.
    /// </summary>
    public void Load()
    {
        EditorPreferences loaded = EditorPreferences.Default;
        if (File.Exists(_filePath))
        {
            try
            {
                EditorPreferencesSerializer.TryRead(File.ReadAllText(_filePath), WarnWithPath, out loaded);
            }
            // Conversion errors are a last resort: the serializer already reports bad values per key. A broken file
            // must never stop the editor from opening a project.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidCastException or OverflowException or FormatException)
            {
                loaded = EditorPreferences.Default;
                WarnWithPath($"cannot be read, defaults are used: {ex.Message}");
            }
        }

        _dirty = false;
        Current = loaded;
        Changed?.Invoke(Current);
    }

    /// <summary>Clamps <paramref name="preferences"/>, applies it at once and schedules a write. Equal values do nothing.</summary>
    public void Update(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EditorPreferences clamped = preferences.Clamp();
        if (clamped == Current)
        {
            return;
        }

        Current = clamped;
        _dirty = true;
        _lastChangeSeconds = _clockSeconds();
        Changed?.Invoke(Current);
    }

    /// <summary>Writes once the last change is <see cref="WriteDelaySeconds"/> old. Cheap when nothing is pending.</summary>
    public bool FlushIfDue()
    {
        if (!_dirty || _clockSeconds() - _lastChangeSeconds < WriteDelaySeconds)
        {
            return false;
        }

        return Flush();
    }

    /// <summary>Writes now if a change is pending (editor exit). A write error is logged, not thrown.</summary>
    public bool Flush()
    {
        if (!_dirty)
        {
            return false;
        }

        _dirty = false;
        try
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, EditorPreferencesSerializer.Write(Current));
            WriteCount++;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WarnWithPath($"cannot be saved: {ex.Message}");
            return false;
        }
    }

    private void WarnWithPath(string message)
    {
        _warn($"Editor preferences '{_filePath}': {message}");
    }

    private static double StopwatchSeconds()
    {
        return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }
}
