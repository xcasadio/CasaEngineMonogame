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

    public List<RemotePanelInfo> Panels { get; } = new();

    public List<string> ClosedPanels { get; } = new();

    public List<(string FullPath, string Type)> OpenedAssets { get; } = new();

    public Dictionary<Guid, CasaEngine.Editor.History.EditorHistoryContext> AssetContexts { get; } = new();

    public List<CasaEngine.Editor.History.EditorHistoryContext> RefreshedContexts { get; } = new();

    public IReadOnlyList<RemotePanelInfo> ListPanels() => Panels;

    public bool ActivatePanel(string panelId) => Panels.Exists(p => p.Id == panelId);

    /// <summary>Mirrors the editor's rule: only the four asset kinds, never a modified one.</summary>
    public RemotePanelCloseResult ClosePanel(string panelId)
    {
        RemotePanelInfo panel = Panels.Find(p => p.Id == panelId);
        if (panel == null)
        {
            return RemotePanelCloseResult.NotFound;
        }

        if (panel.Kind == "other")
        {
            return RemotePanelCloseResult.NotClosable;
        }

        if (panel.Modified)
        {
            return RemotePanelCloseResult.Modified;
        }

        Panels.Remove(panel);
        ClosedPanels.Add(panelId);
        return RemotePanelCloseResult.Closed;
    }

    public string OpenAsset(string fullPath, string type)
    {
        OpenedAssets.Add((fullPath, type));
        return "panel_" + type + "_test";
    }

    public bool TryGetAssetHistoryContext(Guid assetId, out CasaEngine.Editor.History.EditorHistoryContext context)
        => AssetContexts.TryGetValue(assetId, out context);

    public void RefreshAfterHistory(CasaEngine.Editor.History.EditorHistoryContext context) => RefreshedContexts.Add(context);

    public List<(Guid AssetId, string Property, string RawValue)> MaterialSets { get; } = new();

    /// <summary>What the material panel would answer for a set: null to accept (after the validator), otherwise the error.</summary>
    public Func<CasaEngine.Framework.Materials.Definitions.MaterialPropertyDefinition, CasaEngine.Framework.Materials.Runtime.MaterialValue, string> LastMaterialValidator { get; private set; }

    public string MaterialSetError { get; set; }

    public List<Guid> SavedAssets { get; } = new();

    public IReadOnlyList<CasaEngine.Editor.Controls.MaterialAssetInspectorPanel.PropertySnapshot> GetMaterialProperties(Guid assetId)
        => Array.Empty<CasaEngine.Editor.Controls.MaterialAssetInspectorPanel.PropertySnapshot>();

    public bool TrySetMaterialProperty(
        Guid assetId,
        string property,
        string rawValue,
        Func<CasaEngine.Framework.Materials.Definitions.MaterialPropertyDefinition, CasaEngine.Framework.Materials.Runtime.MaterialValue, string> validate,
        out string error)
    {
        LastMaterialValidator = validate;
        MaterialSets.Add((assetId, property, rawValue));
        error = MaterialSetError;
        return error == null;
    }

    public bool TrySaveAsset(Guid assetId, out string error)
    {
        SavedAssets.Add(assetId);
        error = null;
        return true;
    }

    public List<(string RelativeFile, string Preset)> CreatedParticles { get; } = new();

    public CasaEngine.Framework.Particles.Authoring.ParticleEffectAsset ParticleAsset { get; set; }

    public List<(Guid AssetId, string Property, string RawValue)> ParticleSets { get; } = new();

    public Func<string, string, string> LastParticleValidator { get; private set; }

    public int CatalogSaves { get; private set; }

    public bool CreateParticleAsset(RemoteNewAssetFile file, string preset, out Guid assetId, out string error)
    {
        CreatedParticles.Add((file.RelativeFile, preset));
        assetId = Guid.NewGuid();
        error = null;
        return true;
    }

    public CasaEngine.Framework.Particles.Authoring.ParticleEffectAsset GetParticleAsset(Guid assetId) => ParticleAsset;

    public bool TrySetParticleProperty(Guid assetId, string property, string rawValue, Func<string, string, string> validate, out string error)
    {
        LastParticleValidator = validate;
        ParticleSets.Add((assetId, property, rawValue));
        error = null;
        return true;
    }

    public void SaveAssetCatalog() => CatalogSaves++;

    public CasaEngine.EditorServices.ScreenEditor.DocumentModel.UIScreenDocument ScreenDocument { get; set; }

    /// <summary>The command stack screen sets run on, as the editor's screen panel does; bound to the history context of the screen.</summary>
    public CasaEngine.EditorServices.ScreenEditor.Commands.UICommandStack ScreenStack { get; set; }

    public CasaEngine.EditorServices.ScreenEditor.DocumentModel.UIScreenDocument GetScreenDocument(Guid assetId) => ScreenDocument;

    public bool TrySetScreenProperty(Guid assetId, CasaEngine.EditorServices.ScreenEditor.DocumentModel.UIScreenNode node, string property, string value, out string error)
    {
        ScreenStack.Execute(new CasaEngine.EditorServices.ScreenEditor.Commands.SetPropertyCommand(node, property, value));
        error = null;
        return true;
    }

    public List<string> CreatedSounds { get; } = new();

    public CasaEngine.Framework.Audio.SoundAsset SoundAsset { get; set; }

    public bool SoundModified { get; set; }

    public bool CreateSoundAsset(RemoteNewAssetFile file, out Guid assetId, out string error)
    {
        CreatedSounds.Add(file.RelativeFile);
        assetId = Guid.NewGuid();
        error = null;
        return true;
    }

    public CasaEngine.Framework.Audio.SoundAsset GetSoundAsset(Guid assetId) => SoundAsset;

    public bool IsSoundModified(Guid assetId) => SoundModified;

    /// <summary>Mirrors the sound panel: the change applies to the loaded sound and marks it modified.</summary>
    public bool TrySetSoundProperty(Guid assetId, Action<CasaEngine.Framework.Audio.SoundAsset> change, out string error)
    {
        change(SoundAsset);
        SoundModified = true;
        error = null;
        return true;
    }
}
