using CasaEngine.Editor.Log;
using CasaEngine.Editor.Remote;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>Editor stand-in for the remote command tests: settable state, recorded calls.</summary>
internal sealed class RemoteTestHost : IEditorRemoteHost
{
    public string EditorVersion { get; set; } = "9.9.9";

    public string ProjectFilePath { get; set; }

    public string CurrentWorldName => CurrentWorld?.Name;

    public bool IsPlaySessionActive { get; set; }

    public bool HasUnsavedWorldChanges { get; set; }

    public World CurrentWorld { get; set; }

    public Entity SelectedEntity { get; set; }

    public EntityComponent SelectedComponent { get; set; }

    public LoggerEditor Logger { get; } = new() { MaxEntries = 10 };

    public string ProjectDirectory { get; set; }

    public int RefreshCount { get; private set; }

    public bool SaveSucceeds { get; set; } = true;

    public int SaveCount { get; private set; }

    public List<(string Name, string FileName)> CreatedWorlds { get; } = new();

    public List<string> OpenedWorlds { get; } = new();

    public bool OpenSucceeds { get; set; } = true;

    public void Select(Entity entity, EntityComponent component)
    {
        SelectedEntity = entity;
        SelectedComponent = component;
    }

    public void RefreshWorldViews() => RefreshCount++;

    public bool SaveProject(out string error)
    {
        SaveCount++;
        error = SaveSucceeds ? null : "The world still has unsaved changes after the save.";
        return SaveSucceeds;
    }

    public bool CreateWorld(string worldName, string fileName, out string error)
    {
        CreatedWorlds.Add((worldName, fileName));
        error = null;
        return true;
    }

    public bool BeginOpenWorld(string fullPath)
    {
        OpenedWorlds.Add(fullPath);
        return OpenSucceeds;
    }

    public string PlayState => IsPlaySessionActive ? "playing" : "editing";

    public bool PlayStarts { get; set; } = true;

    public int StartPlayCount { get; private set; }

    public void StartPlay()
    {
        StartPlayCount++;
        IsPlaySessionActive = PlayStarts;
    }

    public void StopPlay() => IsPlaySessionActive = false;

    public List<(string Kind, string Path)> CaptureRequests { get; } = new();

    public Dictionary<string, RemoteCaptureResult> CaptureResults { get; } = new();

    public bool RequestCapture(string kind, string path, out string error)
    {
        CaptureRequests.Add((kind, path));
        error = null;
        return true;
    }

    public bool TryTakeCaptureResult(string path, out RemoteCaptureResult result)
        => CaptureResults.Remove(path, out result);
}
