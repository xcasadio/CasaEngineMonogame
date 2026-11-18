#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Keyboard;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The cutscene document: every property of every action of the tree is a text field (ADR-0095). A field commits on Enter
/// or when it loses the focus, as a <see cref="SetCutsceneActionPropertyCommand"/> in the history of the document; undo and
/// redo, global or not, rebuild the fields from the asset, which stays the only source of truth.
/// </summary>
internal sealed class CutsceneAssetInspectorPanel : IDisposable
{
    private const int FieldLabelWidth = 150;
    private const int FieldIndentPerDepth = 12;
    private const int PropertiesColumnWidth = 420;
    private const string PreviewLimitText = "Sprite animations, physics and scripts are frozen in the preview.";

    private readonly MGWindow _window;
    private readonly EditorDirtyStateService _dirtyState = EditorDirtyStateService.Current;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _contentStack;

    private readonly GraphicsDevice? _graphicsDevice;
    private readonly HostedEditorGameAdapter? _editorRuntime;
    private readonly IWindowInputSource? _windowInputSource;
    private readonly CutscenePreviewSession? _preview;
    private WorldViewportPanel? _previewViewport;
    private MGDockPanel? _documentRoot;
    private MGComboBox<string>? _worldCombo;
    private MGTextBlock? _previewStatusText;
    private CutscenePreviewSettings? _previewSettings;
    private readonly List<CutscenePreviewWorldCandidate> _previewWorlds = [];
    private readonly List<string> _previewWorldLabels = [];
    private PreviewStatusKey _lastPreviewStatusKey;

    private CutsceneEditSession? _session;
    private CutsceneReadOnlyDocument? _document;
    private CutsceneDebugSnapshot? _runtimeSnapshot;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private string _message = string.Empty;
    private bool _lastDirty;
    private bool _suppressControlCallbacks;
    private bool _disposed;

    public CutsceneAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    /// <summary>Creates a panel with a world preview and its transport controls (ADR-0096).</summary>
    public CutsceneAssetInspectorPanel(
        MGWindow window,
        GraphicsDevice graphicsDevice,
        HostedEditorGameAdapter editorRuntime,
        IWindowInputSource windowInputSource)
        : this(window)
    {
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _preview = new CutscenePreviewSession(editorRuntime);
    }

    /// <summary>The preview session, or null when the panel has no runtime.</summary>
    internal CutscenePreviewSession? Preview => _preview;

    public CutsceneAsset? LoadedCutsceneAsset => _session?.Asset;

    public string? LoadedRelativePath => _loadedRelativePath;

    /// <summary>True when the history of the document holds a change that has not been saved.</summary>
    public bool IsDirty => TryGetHistoryContext(out var historyContext) && _dirtyState.IsDirty(historyContext);

    public event Action<CutsceneAssetInspectorPanel>? DirtyStateChanged;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Cutscene Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No cutscene asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .cutscene asset from the Content Browser.")
        {
            Margin = new Thickness(8, 0, 8, 8),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _contentStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_contentStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        if (_preview == null)
        {
            return _root;
        }

        _documentRoot = CreateDocumentWithPreview(_root);
        return _documentRoot;
    }

    /// <summary>
    /// Sets the history context the edits of this panel belong to (the id of its dock panel) and listens to its history, so that
    /// a global undo or redo rebuilds the fields. Call it before <see cref="LoadAsset"/>; <see cref="Dispose"/> stops listening.
    /// </summary>
    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);

        // The dirty state service listens to the same event and was created before this panel: it is updated first.
        EditorHistoryService.Current.HistoryChanged -= OnHistoryChanged;
        _historyContextId = historyContextId;
        EditorHistoryService.Current.HistoryChanged += OnHistoryChanged;
    }

    /// <summary>
    /// Starts editing a session (see <see cref="TryLoadAsset"/>). Whatever was edited before is replaced; the history of the
    /// context starts again and the document is clean.
    /// </summary>
    public void LoadAsset(CutsceneEditSession session, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _session = session;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _message = string.Empty;
        _document = CutsceneReadOnlyDocumentBuilder.Build(session.Asset, _runtimeSnapshot);

        if (TryGetHistoryContext(out var historyContext))
        {
            // The entries of the previous document would undo into a document nobody shows.
            EditorHistoryService.Current.Clear(historyContext);
            _dirtyState.MarkSaved(historyContext);
        }

        RefreshInspector();
        NotifyDirtyStateIfChanged();
        InitializePreviewWorlds();
    }

    /// <summary>
    /// Updates the runtime part of the display. The fields are rebuilt only when that part shows something other than an idle
    /// cutscene: rebuilding them drops the text being typed, and an edit already rebuilds them.
    /// </summary>
    public void RefreshRuntimeSnapshot(CutsceneDebugSnapshot? runtimeSnapshot)
    {
        _runtimeSnapshot = runtimeSnapshot;
        if (_session == null)
        {
            return;
        }

        var previous = _document;
        _document = CutsceneReadOnlyDocumentBuilder.Build(_session.Asset, runtimeSnapshot);
        if (previous != null && HasSameIdleRuntimeDisplay(previous, _document))
        {
            return;
        }

        RefreshInspector();
    }

    /// <summary>
    /// Writes the asset to its file and marks the history context saved. False, with a message, when the save is refused by
    /// <see cref="CutsceneSaveGuard"/> or the file cannot be written.
    /// </summary>
    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;
        if (_session == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No cutscene asset is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            SetMessage($"Already saved {_loadedRelativePath}");
            return true;
        }

        if (CutsceneSaveGuard.TryGetBlockingReason(_session, out string reason))
        {
            errorMessage = $"Cannot save {_loadedRelativePath}: {reason}";
            SetMessage(errorMessage);
            return false;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _session.Asset, EditorAssetSaveSource.CutsceneEditorPanel);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = $"Failed to save {_loadedRelativePath}: {exception.Message}";
            SetMessage(errorMessage);
            return false;
        }

        if (TryGetHistoryContext(out var historyContext))
        {
            _dirtyState.MarkSaved(historyContext);
        }

        _message = $"Saved {_loadedRelativePath}";
        RefreshInspector();
        NotifyDirtyStateIfChanged();
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        EditorHistoryService.Current.HistoryChanged -= OnHistoryChanged;
        _previewViewport?.Dispose();
        _preview?.Dispose();

        // Removing the fields detaches their keyboard and focus handlers.
        _contentStack?.TryRemoveAll();
    }

    /// <summary>Advances the preview. Called every frame by the editor.</summary>
    public void Update(GameTime gameTime)
    {
        if (_disposed || _preview == null)
        {
            return;
        }

        _preview.Tick(gameTime);
        RefreshPreviewStatus();
    }

    public void DrawViewport(GameTime gameTime)
    {
        _previewViewport?.DrawViewport(gameTime);
    }

    private MGDockPanel CreateDocumentWithPreview(MGDockPanel propertiesRoot)
    {
        _previewViewport = new WorldViewportPanel(_window, _graphicsDevice!, _editorRuntime!, _windowInputSource!)
        {
            EnablePreviewSelection = false,
            EnablePreviewGizmo = false,
            ShowEditorOverlays = false,
            UseFront2dCamera = true,
        };

        var documentRoot = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        documentRoot.TryAddChild(CreateTransportRow(), Dock.Top);

        _previewStatusText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };
        documentRoot.TryAddChild(_previewStatusText, Dock.Top);

        propertiesRoot.PreferredWidth = PropertiesColumnWidth;
        propertiesRoot.VerticalAlignment = VerticalAlignment.Stretch;
        documentRoot.TryAddChild(propertiesRoot, Dock.Right);

        // The last child of the dock panel fills what is left: the viewport gets most of the document.
        documentRoot.TryAddChild(_previewViewport.CreateContent(), Dock.Left);

        ApplyPreviewWorldToViewport();
        _lastPreviewStatusKey = default;
        RefreshPreviewStatus();
        return documentRoot;
    }

    private MGElement CreateTransportRow()
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(8, 6, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, "Preview world")
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = EditorThemePalette.SectionLabelOpacity,
        });

        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 180,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item);
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, item)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _worldCombo = combo;
        FillWorldCombo();
        combo.SelectedItemChanged += (_, args) => OnWorldComboChanged(args.NewValue);
        row.TryAddChild(combo);

        row.TryAddChild(CreateTransportButton("Play", PlayPreview));
        row.TryAddChild(CreateTransportButton("Stop", StopPreview));
        row.TryAddChild(CreateTransportButton("Restart", PlayPreview));
        return row;
    }

    private MGButton CreateTransportButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            Padding = new Thickness(10, 2, 10, 2),
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private void FillWorldCombo()
    {
        if (_worldCombo == null)
        {
            return;
        }

        _suppressControlCallbacks = true;
        _worldCombo.SetItemsSource(_previewWorldLabels.ToArray());
        int selected = IndexOfPreviewWorld(_preview?.WorldId ?? Guid.Empty);
        if (selected >= 0)
        {
            _worldCombo.SelectedItem = _previewWorldLabels[selected];
        }

        _suppressControlCallbacks = false;
    }

    private int IndexOfPreviewWorld(Guid worldId)
    {
        for (int index = 0; index < _previewWorlds.Count; index++)
        {
            if (_previewWorlds[index].WorldId == worldId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Lists the worlds of the project and loads the one chosen by default (or remembered) for this cutscene.</summary>
    private void InitializePreviewWorlds()
    {
        if (_preview == null || _session == null)
        {
            return;
        }

        _previewWorlds.Clear();
        _previewWorldLabels.Clear();
        string projectDirectory = EngineEnvironment.ProjectPath;
        _previewSettings = CutscenePreviewSettings.Load(projectDirectory);

        var candidates = new List<CutscenePreviewWorldCandidate>();
        foreach (AssetInfo info in AssetCatalog.AssetInfos)
        {
            if (!string.Equals(info.AssetType, "world", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(new CutscenePreviewWorldCandidate(info.Id, info.FileName, ReadStartupCutsceneId(projectDirectory, info.FileName)));
        }

        Guid cutsceneId = _session.Asset.AssetId;
        var choice = CutscenePreviewWorldResolver.Resolve(
            candidates,
            cutsceneId,
            GameSettings.ProjectSettings.FirstWorldLoaded,
            _previewSettings.GetWorldId(cutsceneId));
        foreach (CutscenePreviewWorldCandidate world in choice.Worlds)
        {
            _previewWorlds.Add(world);
            _previewWorldLabels.Add(world.FileName);
        }

        if (choice.HasWorld)
        {
            _preview.SetWorld(choice.Default.WorldId);
        }

        FillWorldCombo();
        ApplyPreviewWorldToViewport();
        _lastPreviewStatusKey = default;
        RefreshPreviewStatus();
    }

    private static Guid ReadStartupCutsceneId(string projectDirectory, string worldFileName)
    {
        try
        {
            using var reader = new StreamReader(Path.Combine(projectDirectory, worldFileName));
            return CutscenePreviewWorldResolver.ReadStartupCutsceneAssetId(reader);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return Guid.Empty;
        }
    }

    private void OnWorldComboChanged(string? label)
    {
        if (_suppressControlCallbacks || _preview == null || _session == null || label == null)
        {
            return;
        }

        int index = _previewWorldLabels.IndexOf(label);
        if (index < 0)
        {
            return;
        }

        Guid worldId = _previewWorlds[index].WorldId;
        _preview.SetWorld(worldId);
        if (_previewSettings != null)
        {
            _previewSettings.SetWorldId(_session.Asset.AssetId, worldId);
            try
            {
                _previewSettings.Save(EngineEnvironment.ProjectPath);
            }
            catch (Exception exception)
            {
                Logs.WriteException(exception);
            }
        }

        ApplyPreviewWorldToViewport();
        RefreshPreviewStatus();
    }

    private void PlayPreview()
    {
        if (_preview == null || _session == null)
        {
            return;
        }

        _preview.Play(_session.Asset);
        ApplyPreviewWorldToViewport();
        RefreshPreviewStatus();
    }

    private void StopPreview()
    {
        _preview?.Stop();
        RefreshPreviewStatus();
    }

    /// <summary>Shows the preview world in the viewport and frames its entities.</summary>
    private void ApplyPreviewWorldToViewport()
    {
        if (_previewViewport == null || _preview == null)
        {
            return;
        }

        var world = _preview.World;
        _previewViewport.SetWorldOverride(world);
        if (world == null)
        {
            return;
        }

        bool hasBounds = false;
        BoundingBox bounds = default;
        for (int index = 0; index < world.Entities.Count; index++)
        {
            var entity = world.Entities[index];
            if (entity.RootComponent == null)
            {
                continue;
            }

            BoundingBox entityBounds = entity.GetBoundingBox();
            bounds = hasBounds ? BoundingBox.CreateMerged(bounds, entityBounds) : entityBounds;
            hasBounds = true;
        }

        if (hasBounds)
        {
            _previewViewport.FocusBounds(bounds);
        }
    }

    private readonly record struct PreviewStatusKey(
        CutsceneRuntimeState State,
        bool EndedByWorldChange,
        int RequestCount,
        string Error,
        string FailureReason,
        bool FadeActive,
        bool Fading,
        uint FadeColor,
        int WorldCount);

    /// <summary>Rebuilds the status text only when what it shows changes, never every frame.</summary>
    private void RefreshPreviewStatus()
    {
        if (_previewStatusText == null || _preview == null)
        {
            return;
        }

        var fade = _preview.ScreenEffects;
        var key = new PreviewStatusKey(
            _preview.State,
            _preview.EndedByWorldChange,
            _preview.Requests.Count,
            _preview.Error,
            _preview.FailureReason,
            fade.Active,
            fade.IsFading,
            fade.IsFading ? 0u : ((uint)fade.R << 24) | ((uint)fade.G << 16) | ((uint)fade.B << 8) | fade.A,
            _previewWorlds.Count);
        if (key.Equals(_lastPreviewStatusKey))
        {
            return;
        }

        _lastPreviewStatusKey = key;
        var text = new System.Text.StringBuilder();
        if (_previewWorlds.Count == 0)
        {
            text.Append("The project has no world to preview the cutscene in. ");
        }
        else
        {
            text.Append("Preview: ").Append(_preview.State).Append('.');
            if (_preview.FailureReason.Length > 0)
            {
                text.Append(" Failed: ").Append(_preview.FailureReason).Append('.');
            }
        }

        if (_preview.Error.Length > 0)
        {
            text.Append(' ').Append(_preview.Error);
        }

        for (int index = 0; index < _preview.Requests.Count; index++)
        {
            var request = _preview.Requests[index];
            text.Append(index == 0 ? " Skipped: " : ", ").Append(request.Type);
            if (request.Target.Length > 0)
            {
                text.Append(" -> ").Append(request.Target);
            }
        }

        if (_preview.EndedByWorldChange)
        {
            text.Append(". Would change the world: the play ends here.");
        }

        if (fade.Active)
        {
            text.Append(fade.IsFading
                ? " Screen fade in progress."
                : $" Screen overlay rgba({fade.R},{fade.G},{fade.B},{fade.A}).");
        }

        text.Append(' ').Append(PreviewLimitText);
        _previewStatusText.Text = EscapeMarkup(text.ToString());
    }


    public static bool TryLoadAsset(string fullPath, out CutsceneEditSession? session)
    {
        session = null;

        if (!File.Exists(fullPath)
            || !string.Equals(Path.GetExtension(fullPath), Constants.FileNameExtensions.Cutscene, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            var loadedSession = CutsceneEditSession.Load(document);
            var cutsceneAsset = loadedSession.Asset;
            cutsceneAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(cutsceneAsset.FileName)
                            ?? AssetCatalog.GetByFileName(cutsceneAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                cutsceneAsset.Name = assetInfo.Name;
                cutsceneAsset.AssetId = assetInfo.Id;
                cutsceneAsset.FileName = assetInfo.FileName;
            }
            else
            {
                cutsceneAsset.AssetId = cutsceneAsset.Id;
            }

            session = loadedSession;
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return false;
        }
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Cutscene, _historyContextId);
        return true;
    }

    private void OnHistoryChanged(object? sender, EditorHistoryChangedEventArgs e)
    {
        if (_disposed || _session == null || !TryGetHistoryContext(out var historyContext) || !e.Context.Equals(historyContext))
        {
            return;
        }

        switch (e.ChangeKind)
        {
            case EditorHistoryStackChangeKind.Undone:
                _message = "Undid the last change.";
                break;
            case EditorHistoryStackChangeKind.Redone:
                _message = "Redid the last change.";
                break;
        }

        _document = CutsceneReadOnlyDocumentBuilder.Build(_session.Asset, _runtimeSnapshot);
        RefreshInspector();
        NotifyDirtyStateIfChanged();
    }

    private void NotifyDirtyStateIfChanged()
    {
        bool isDirty = IsDirty;
        if (isDirty == _lastDirty)
        {
            return;
        }

        _lastDirty = isDirty;
        DirtyStateChanged?.Invoke(this);
    }

    private static bool HasSameIdleRuntimeDisplay(CutsceneReadOnlyDocument left, CutsceneReadOnlyDocument right)
    {
        return left.RuntimeState == right.RuntimeState
               && left.ActiveCoroutines.Count == 0
               && right.ActiveCoroutines.Count == 0
               && string.Equals(left.ActiveActionType, right.ActiveActionType, StringComparison.Ordinal)
               && string.Equals(left.ActiveActionEntityName, right.ActiveActionEntityName, StringComparison.Ordinal)
               && left.ActiveActionDestination == right.ActiveActionDestination
               && string.Equals(left.ActiveActionState, right.ActiveActionState, StringComparison.Ordinal)
               && string.Equals(left.ActiveActionStopReason, right.ActiveActionStopReason, StringComparison.Ordinal);
    }

    private void RefreshInspector()
    {
        if (_headerText == null || _sourceText == null || _statusText == null || _contentStack == null)
        {
            return;
        }

        _contentStack.TryRemoveAll();
        if (_document == null || _session == null)
        {
            _headerText.Text = "[b]Cutscene Inspector[/b]";
            _sourceText.Text = "No cutscene asset loaded.";
            _statusText.Text = "Open a .cutscene asset from the Content Browser.";
            return;
        }

        _headerText.Text = $"[b]{EscapeMarkup(_document.AssetName)}[/b]";
        _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "No source path."
            : EscapeMarkup(_loadedRelativePath);
        UpdateStatusText();

        AddProperty("Runtime state", _document.RuntimeState.ToString());
        AddProperty("Active coroutines", _document.ActiveCoroutines.Count.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(_document.ActiveActionType))
        {
            AddProperty("Active action", _document.ActiveActionType);
            AddProperty("Action entity", string.IsNullOrWhiteSpace(_document.ActiveActionEntityName) ? "<none>" : _document.ActiveActionEntityName);
            AddProperty("Action destination", _document.ActiveActionDestination.HasValue ? FormatVector3(_document.ActiveActionDestination.Value) : "<none>");
            AddProperty("Action state", string.IsNullOrWhiteSpace(_document.ActiveActionState) ? "<none>" : _document.ActiveActionState);
            AddProperty("Action stop reason", string.IsNullOrWhiteSpace(_document.ActiveActionStopReason) ? "<none>" : _document.ActiveActionStopReason);
        }

        AddSection("Actions");
        if (_session.Asset.RootAction == null)
        {
            AddText("No root action.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            AddActionNode(_session.Asset.RootAction, "root_action", 0);
        }

        AddSection("Validation");
        if (_document.ValidationMessages.Count == 0 && _session.LoadLosses.Count == 0)
        {
            AddText("No validation warnings or errors.", EditorThemePalette.SecondaryTextOpacity);
        }

        for (int index = 0; index < _document.ValidationMessages.Count; index++)
        {
            var message = _document.ValidationMessages[index];
            AddText($"{message.Severity}: {EscapeMarkup(message.Path)} - {EscapeMarkup(message.Message)}", EditorThemePalette.PrimaryHeaderOpacity);
        }

        for (int index = 0; index < _session.LoadLosses.Count; index++)
        {
            var loss = _session.LoadLosses[index];
            AddText($"Load loss: {EscapeMarkup(loss.Path)} ({EscapeMarkup(loss.Key)}) - {EscapeMarkup(loss.Message)}", EditorThemePalette.PrimaryHeaderOpacity);
        }

        AddSection("Runtime Coroutines");
        if (_document.ActiveCoroutines.Count == 0)
        {
            AddText("No active cutscene coroutine in the current world snapshot.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (int index = 0; index < _document.ActiveCoroutines.Count; index++)
            {
                var coroutine = _document.ActiveCoroutines[index];
                string remainingTime = coroutine.RemainingTime.HasValue
                    ? coroutine.RemainingTime.Value.ToString("0.###", CultureInfo.InvariantCulture)
                    : "<none>";
                AddText($"#{coroutine.Id.ToString(CultureInfo.InvariantCulture)} {EscapeMarkup(coroutine.Name ?? "<unnamed>")} state={EscapeMarkup(coroutine.State)} paused={coroutine.IsPaused} instruction={EscapeMarkup(coroutine.CurrentInstruction ?? "<none>")} remaining={remainingTime}", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }
    }

    private void UpdateStatusText()
    {
        if (_statusText == null || _document == null)
        {
            return;
        }

        int issueCount = _document.ValidationMessages.Count + (_session?.LoadLosses.Count ?? 0);
        string validity = issueCount == 0
            ? "Validation OK."
            : $"{issueCount.ToString(CultureInfo.InvariantCulture)} validation issue(s).";
        string text = $"{validity} {(IsDirty ? "Modified." : "Not modified.")}";
        _statusText.Text = _message.Length == 0 ? text : $"{text} {EscapeMarkup(_message)}";
    }

    private void SetMessage(string message)
    {
        _message = message;
        UpdateStatusText();
    }

    private void AddActionNode(CutsceneActionData action, string path, int depth)
    {
        string indent = new(' ', depth * 2);
        AddText($"{indent}- {EscapeMarkup(action.Type)}  {EscapeMarkup(path)}", EditorThemePalette.PrimaryHeaderOpacity);

        if (action is UnknownCutsceneActionData)
        {
            AddText($"{indent}  Unknown action type: it cannot be edited and a save is refused.", EditorThemePalette.SecondaryTextOpacity);
        }

        var properties = CutsceneActionPropertyEditor.GetProperties(action);
        for (int index = 0; index < properties.Count; index++)
        {
            _contentStack?.TryAddChild(CreateFieldRow(action, properties[index], depth));
        }

        var children = GetChildren(action);
        if (children == null)
        {
            return;
        }

        for (int index = 0; index < children.Count; index++)
        {
            AddActionNode(children[index], $"{path}.actions[{index.ToString(CultureInfo.InvariantCulture)}]", depth + 1);
        }
    }

    private static List<CutsceneActionData>? GetChildren(CutsceneActionData action)
        => action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            ParallelCutsceneActionData parallel => parallel.Actions,
            RepeatCutsceneActionData repeat => repeat.Actions,
            _ => null,
        };

    /// <summary>
    /// A label and a text field for one property. The field commits on Enter, or when it loses the focus, through
    /// <see cref="CommitField"/>, and shows again the text of the value when the commit does not rebuild the fields.
    /// </summary>
    private MGElement CreateFieldRow(CutsceneActionData action, CutsceneActionProperty property, int depth)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(depth * FieldIndentPerDepth, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, property.Key)
        {
            PreferredWidth = FieldLabelWidth,
            Opacity = EditorThemePalette.SectionLabelOpacity,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var textBox = new MGTextBox(_window, CharacterLimit: 256)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HasStableTextFootprint = true,
            AcceptsReturn = false,
            AcceptsTab = false,
            MinWidth = 160,
        };

        string lastCommittedValue = property.GetText(action);

        // Enter commits a field with a problem even when its text is unchanged: that is how the problem is cleared.
        void Commit(bool isEnter)
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            string currentValue = textBox.Text ?? string.Empty;
            bool force = isEnter && HasFieldProblem(action, property.Key);
            if (!force && string.Equals(currentValue, lastCommittedValue, StringComparison.Ordinal))
            {
                return;
            }

            string? shownValue = CommitField(action, property, currentValue);
            if (shownValue == null)
            {
                lastCommittedValue = currentValue;
                return;
            }

            lastCommittedValue = shownValue;
            if (!string.Equals(textBox.Text, shownValue, StringComparison.Ordinal))
            {
                _suppressControlCallbacks = true;
                textBox.SetText(shownValue);
                _suppressControlCallbacks = false;
            }
        }

        EventHandler<BaseKeyPressedEventArgs>? keyPressedHandler = null;
        EventHandler<MGUI.Shared.Helpers.EventArgs<MGElement>>? focusChangedHandler = null;
        EventHandler<MGUI.Shared.Helpers.EventArgs<MGElement>>? parentChangedHandler = null;

        keyPressedHandler = (_, args) =>
        {
            // The text box handles every key of its own handler, which runs first: only a key handled by another element is skipped.
            if ((args.IsHandled && !ReferenceEquals(args.HandledBy, textBox)) || args.Key != Microsoft.Xna.Framework.Input.Keys.Enter)
            {
                return;
            }

            Commit(isEnter: true);
            args.SetHandledBy(textBox, false);
        };

        focusChangedHandler = (_, args) =>
        {
            if (ReferenceEquals(args.PreviousValue, textBox) && !ReferenceEquals(args.NewValue, textBox))
            {
                Commit(isEnter: false);
            }
        };

        parentChangedHandler = (_, args) =>
        {
            if (args.NewValue != null)
            {
                return;
            }

            textBox.KeyboardHandler.Pressed -= keyPressedHandler;
            if (_window.Desktop != null)
            {
                _window.Desktop.FocusedKeyboardHandlerChanged -= focusChangedHandler;
            }

            row.OnParentChanged -= parentChangedHandler;
        };

        textBox.KeyboardHandler.Pressed += keyPressedHandler;
        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged += focusChangedHandler;
        }

        row.OnParentChanged += parentChangedHandler;

        _suppressControlCallbacks = true;
        textBox.SetText(lastCommittedValue);
        _suppressControlCallbacks = false;

        row.TryAddChild(textBox);
        return row;
    }

    /// <summary>
    /// Commits the text of a field. Returns null when the command was executed (the history event rebuilds the fields), and
    /// otherwise the text the field must show: the current text of the value, with the reason in the status line.
    /// </summary>
    private string? CommitField(CutsceneActionData action, CutsceneActionProperty property, string text)
    {
        string currentText = property.GetText(action);
        if (_session == null)
        {
            return currentText;
        }

        bool hasProblem = HasFieldProblem(action, property.Key);
        if (!hasProblem && string.Equals(text, currentText, StringComparison.Ordinal))
        {
            return currentText;
        }

        if (!property.TryParse(text, out object? value, out string error))
        {
            SetMessage(error);
            return currentText;
        }

        // The same value written another way, or a field with a problem that is set to the value it shows, is no change.
        if (!hasProblem && property.AreEqual(property.GetValue(action), value))
        {
            return currentText;
        }

        if (!TryGetHistoryContext(out var historyContext))
        {
            SetMessage("This cutscene has no history context: the change is refused.");
            return currentText;
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            SetMessage("Cutscene edits are disabled during a play session.");
            return currentText;
        }

        var command = new SetCutsceneActionPropertyCommand(_session, action, property, value);
        _message = command.Description;
        EditorHistoryService.Current.Execute(historyContext, command);
        return null;
    }

    private bool HasFieldProblem(CutsceneActionData action, string key)
    {
        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors != null && formatErrors.ContainsKey(key))
        {
            return true;
        }

        var losses = _session?.LoadLosses;
        if (losses != null)
        {
            for (int index = 0; index < losses.Count; index++)
            {
                if (ReferenceEquals(losses[index].Action, action) && string.Equals(losses[index].Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void AddSection(string title)
    {
        AddText($"[b]{EscapeMarkup(title)}[/b]", EditorThemePalette.SectionHeaderOpacity);
    }

    private void AddProperty(string label, string value)
    {
        if (_contentStack == null)
        {
            return;
        }

        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 130,
            Opacity = EditorThemePalette.SectionLabelOpacity,
            WrapText = true,
        });
        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(value))
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            WrapText = true,
        });
        _contentStack.TryAddChild(row);
    }

    private void AddText(string text, float opacity)
    {
        _contentStack?.TryAddChild(new MGTextBlock(_window, text)
        {
            Opacity = opacity,
            WrapText = true,
        });
    }

    private static string FormatVector3(Microsoft.Xna.Framework.Vector3 value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}");
    }

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}
