using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Editor.Remote;

/// <summary>What the remote commands read from the editor. Implemented by <see cref="GameEditor"/>; faked in tests.</summary>
internal interface IEditorRemoteHost
{
    string EditorVersion { get; }

    string ProjectFilePath { get; }

    string CurrentWorldName { get; }

    bool IsPlaySessionActive { get; }

    /// <summary>The world history context has changes not saved yet.</summary>
    bool HasUnsavedWorldChanges { get; }

    World CurrentWorld { get; }

    Entity SelectedEntity { get; }

    EntityComponent SelectedComponent { get; }

    /// <summary>The editor log already registered with <c>Logs</c>; null when there is none.</summary>
    LoggerEditor Logger { get; }

    /// <summary>Folder of the open project; null when none is open.</summary>
    string ProjectDirectory { get; }

    /// <summary>Selects in the editor's panels, as a click in the hierarchy or the inspector would.</summary>
    void Select(Entity entity, EntityComponent component);

    /// <summary>Refreshes the hierarchy, inspector and viewport after a world change made outside the panels.</summary>
    void RefreshWorldViews();

    /// <summary>Saves the project and the world; false, with a reason, when something was not written.</summary>
    bool SaveProject(out string error);

    /// <summary>Writes a new empty world file and adds it to the asset catalog.</summary>
    bool CreateWorld(string worldName, string fileName, out string error);

    /// <summary>Starts loading a catalogued world without any dialog; false when it cannot.</summary>
    bool BeginOpenWorld(string fullPath);

    /// <summary>Play mode state in lower case: editing, starting, playing, paused or stopping.</summary>
    string PlayState { get; }

    /// <summary>Starts play mode as the toolbar button does (scripts built first); failures are logged.</summary>
    void StartPlay();

    void StopPlay();

    /// <summary>Asks for a capture (<c>window</c> or <c>viewport</c>) written to <paramref name="path"/> at the end of the next frame.</summary>
    bool RequestCapture(string kind, string path, out string error);

    /// <summary>The result of the capture of <paramref name="path"/>, once taken.</summary>
    bool TryTakeCaptureResult(string path, out RemoteCaptureResult result);
}

/// <summary>
/// Remote control of the editor (ADR-0092): the command registry, the game-thread dispatcher and, while started,
/// the pipe server and its discovery file. Off until <see cref="Start"/> (plan P2).
/// </summary>
internal sealed class EditorRemoteControl : IDisposable
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(5);

    private readonly IEditorRemoteHost _host;
    private readonly string _discoveryDirectory;
    private readonly EditorRemoteLogReader _logReader;
    private EditorRemoteServer _server;
    private string _token;

    public EditorRemoteControl(IEditorRemoteHost host, string discoveryDirectory = null, Func<double> clockSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _discoveryDirectory = discoveryDirectory ?? RemoteDiscovery.DefaultDirectory;
        Registry = new EditorRemoteCommandRegistry();
        Dispatcher = new EditorRemoteDispatcher(Registry, clockSeconds);
        _logReader = host.Logger != null ? new EditorRemoteLogReader(host.Logger) : null;
        RegisterBuiltInCommands();
        EditorRemoteReadCommands.Register(Registry, host, () => _logReader);
        EditorRemoteEditCommands.Register(Registry, host);
        EditorRemoteViewCommands.Register(Registry, host, NewCapturePath);
    }

    /// <summary><c>%TEMP%/CasaEngine/editor-remote</c>: captures, under names the client cannot choose (plan P8).</summary>
    public static string DefaultCaptureDirectory => Path.Combine(Path.GetTempPath(), "CasaEngine", "editor-remote");

    public string CaptureDirectory { get; init; } = DefaultCaptureDirectory;

    private readonly List<string> _capturePaths = new();

    private string NewCapturePath()
    {
        string path = Path.Combine(CaptureDirectory, "capture-" + Guid.NewGuid().ToString("N") + ".png");
        _capturePaths.Add(path);
        return path;
    }

    /// <summary>Deletes the captures of this editor session (plan P8: cleaned when the editor closes).</summary>
    private void DeleteCaptures()
    {
        for (int i = 0; i < _capturePaths.Count; i++)
        {
            try
            {
                if (File.Exists(_capturePaths[i]))
                {
                    File.Delete(_capturePaths[i]);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logs.WriteWarning($"[Remote] Could not delete the capture '{_capturePaths[i]}': {ex.Message}");
            }
        }

        _capturePaths.Clear();
    }

    public EditorRemoteCommandRegistry Registry { get; }

    public EditorRemoteDispatcher Dispatcher { get; }

    public bool IsRunning => _server != null;

    public string PipeName => _server?.PipeName;

    /// <summary>Raised after <see cref="Start"/> or <see cref="Stop"/> changed <see cref="IsRunning"/>.</summary>
    public event Action RunningChanged;

    /// <summary>True when the current process runs elevated; the server then refuses to start (security review S3).</summary>
    public static bool IsProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Starts the pipe server and writes the discovery file. Returns false, with a warning, when it cannot.</summary>
    public bool Start(bool refuseWhenElevated = true)
    {
        if (_server != null)
        {
            return true;
        }

        if (refuseWhenElevated && IsProcessElevated())
        {
            Logs.WriteWarning("[Remote] Remote control is refused while the editor runs as administrator.");
            return false;
        }

        string pipeName = RemoteDiscovery.CreatePipeName();
        var server = new EditorRemoteServer(Dispatcher, pipeName, IdleTimeout, AnswerTimeout);
        try
        {
            server.Start();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            server.Dispose();
            Logs.WriteWarning($"[Remote] Remote control could not open its pipe: {ex.Message}");
            return false;
        }

        _token = RemoteDiscovery.CreateToken();
        try
        {
            using Process process = Process.GetCurrentProcess();
            RemoteDiscovery.Write(_discoveryDirectory, new RemoteDiscoveryRecord
            {
                ProcessId = Environment.ProcessId,
                ProcessStartUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                PipeName = pipeName,
                Token = _token,
                ProjectPath = _host.ProjectFilePath,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            server.Dispose();
            _token = null;
            Logs.WriteWarning($"[Remote] Remote control could not write its discovery file: {ex.Message}");
            return false;
        }

        _server = server;
        Logs.WriteInfo($"[Remote] Remote control started on pipe '{pipeName}'.");
        RunningChanged?.Invoke();
        return true;
    }

    public void Stop()
    {
        if (_server == null)
        {
            return;
        }

        _server.Dispose();
        _server = null;
        _token = null;
        Dispatcher.CancelAll("The editor stopped its remote control.");

        try
        {
            RemoteDiscovery.Delete(_discoveryDirectory, Environment.ProcessId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logs.WriteWarning($"[Remote] Remote control could not delete its discovery file: {ex.Message}");
        }

        Logs.WriteInfo("[Remote] Remote control stopped.");
        RunningChanged?.Invoke();
    }

    /// <summary>Game thread, once per frame.</summary>
    public void Pump() => Dispatcher.Pump();

    public void Dispose()
    {
        Stop();
        DeleteCaptures();
        _logReader?.Dispose();
    }

    private void RegisterBuiltInCommands()
    {
        Registry.Register(new EditorRemoteCommand(
            RemoteProtocol.HelloMethod,
            "Handshake: protocol version, editor version, process id and the token of the discovery file.",
            null,
            readOnly: true,
            destructive: false,
            call => call.Succeed(new RemoteHelloResult
            {
                ProtocolVersion = RemoteProtocol.Version,
                EditorVersion = _host.EditorVersion,
                ProcessId = Environment.ProcessId,
                Token = _token ?? string.Empty,
            })));

        Registry.Register(new EditorRemoteCommand(
            RemoteProtocol.ListCommandsMethod,
            "Lists the editor commands with their argument schemas.",
            null,
            readOnly: true,
            destructive: false,
            call => call.Succeed(Registry.Describe())));

        Registry.Register(new EditorRemoteCommand(
            "editor.status",
            "Project file, current world, play mode and unsaved world changes.",
            null,
            readOnly: true,
            destructive: false,
            call => call.Succeed(new EditorRemoteStatus
            {
                ProjectPath = _host.ProjectFilePath,
                WorldName = _host.CurrentWorldName,
                PlayActive = _host.IsPlaySessionActive,
                UnsavedChanges = _host.HasUnsavedWorldChanges,
            })));
    }
}

internal sealed class EditorRemoteStatus
{
    public string ProjectPath { get; init; }

    public string WorldName { get; init; }

    public bool PlayActive { get; init; }

    public bool UnsavedChanges { get; init; }
}
