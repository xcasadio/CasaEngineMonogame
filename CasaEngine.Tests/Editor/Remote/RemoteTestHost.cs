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

    /// <summary>Mirrors the editor's rule: only the five asset kinds (entity included), never a modified one.</summary>
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

    /// <summary>
    /// For an entity asset registered in <see cref="EntityAssets"/> (found by the catalog entry of the file), its panel id;
    /// null when <see cref="EntityAssetOpenFails"/>; otherwise the test panel id of the type, as before.
    /// </summary>
    public string OpenAsset(string fullPath, string type)
    {
        OpenedAssets.Add((fullPath, type));
        if (type == EditorRemoteAssetTypes.Entity)
        {
            if (EntityAssetOpenFails)
            {
                return null;
            }

            if (FindEntityAsset(fullPath) is { } panel)
            {
                return panel.PanelId;
            }
        }

        return "panel_" + type + "_test";
    }

    /// <summary>The registered entity asset's context first, then <see cref="AssetContexts"/>.</summary>
    public bool TryGetAssetHistoryContext(Guid assetId, out CasaEngine.Editor.History.EditorHistoryContext context)
    {
        if (EntityAssets.TryGetValue(assetId, out EntityAssetPanel panel))
        {
            context = panel.Context;
            return true;
        }

        return AssetContexts.TryGetValue(assetId, out context);
    }

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

    /// <summary>When set, <see cref="TrySaveAsset"/> fails with this error.</summary>
    public string SaveAssetError { get; set; }

    /// <summary>Records the call; for a registered entity asset, marks its context saved in the real dirty state, as the editor does.</summary>
    public bool TrySaveAsset(Guid assetId, out string error)
    {
        SavedAssets.Add(assetId);
        if (SaveAssetError != null)
        {
            error = SaveAssetError;
            return false;
        }

        if (EntityAssets.TryGetValue(assetId, out EntityAssetPanel panel))
        {
            CasaEngine.Editor.History.EditorDirtyStateService.Current.MarkSaved(panel.Context);
        }

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

    /// <summary>An entity asset "open in its panel": a real entity, the file the panel loaded and the panel's history context.</summary>
    public sealed class EntityAssetPanel
    {
        public Entity Entity { get; init; }

        public string LoadedFile { get; set; }

        public string PanelId { get; init; }

        public CasaEngine.Editor.History.EditorHistoryContext Context { get; init; }
    }

    /// <summary>The open entity asset panels, by asset id.</summary>
    public Dictionary<Guid, EntityAssetPanel> EntityAssets { get; } = new();

    public bool EntityAssetOpenFails { get; set; }

    /// <summary>Calls of the entity asset members below (the open, the save and the history context are recorded elsewhere).</summary>
    public int EntityAssetCalls { get; private set; }

    public List<(Guid AssetId, EntityComponent Component)> SelectedEntityAssetComponents { get; } = new();

    /// <summary>Registers an open entity asset panel with the editor's panel id and an <c>Entity</c> history context.</summary>
    public EntityAssetPanel AddEntityAsset(Guid assetId, Entity entity, string loadedFile)
    {
        string panelId = CasaEngine.Editor.Workspaces.EditorPanelIds.EntityAssetDocumentPrefix + assetId.ToString("N");
        var panel = new EntityAssetPanel
        {
            Entity = entity,
            LoadedFile = loadedFile,
            PanelId = panelId,
            Context = new CasaEngine.Editor.History.EditorHistoryContext(CasaEngine.Editor.History.EditorHistoryContextKind.Entity, panelId),
        };
        EntityAssets[assetId] = panel;
        return panel;
    }

    public Entity GetEntityAsset(Guid assetId)
    {
        EntityAssetCalls++;
        return EntityAssets.TryGetValue(assetId, out EntityAssetPanel panel) ? panel.Entity : null;
    }

    public string GetEntityAssetLoadedFile(Guid assetId)
    {
        EntityAssetCalls++;
        return EntityAssets.TryGetValue(assetId, out EntityAssetPanel panel) ? panel.LoadedFile : null;
    }

    /// <summary>The real dirty state of the panel's context, as the panel's <c>IsDirty</c> reads it.</summary>
    public bool IsEntityAssetModified(Guid assetId)
    {
        EntityAssetCalls++;
        return EntityAssets.TryGetValue(assetId, out EntityAssetPanel panel)
            && CasaEngine.Editor.History.EditorDirtyStateService.Current.IsDirty(panel.Context);
    }

    public void SelectEntityAssetComponent(Guid assetId, EntityComponent component)
    {
        EntityAssetCalls++;
        SelectedEntityAssetComponents.Add((assetId, component));
    }

    private EntityAssetPanel FindEntityAsset(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(ProjectDirectory))
        {
            return null;
        }

        string relative = Path.GetRelativePath(ProjectDirectory, fullPath);
        CasaEngine.Framework.Assets.AssetInfo info = CasaEngine.Framework.Assets.AssetCatalog.GetByFileName(relative)
            ?? CasaEngine.Framework.Assets.AssetCatalog.GetByFileName(relative.Replace('\\', '/'));
        return info != null && EntityAssets.TryGetValue(info.Id, out EntityAssetPanel panel) ? panel : null;
    }
}
