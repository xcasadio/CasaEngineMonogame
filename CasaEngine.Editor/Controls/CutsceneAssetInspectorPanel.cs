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
using CasaEngine.Framework.Scene.Entities;
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
    private const string PreviewLimitText = "Sprite animations, physics and scripts are frozen in the preview.";

    private readonly MGWindow _window;
    private readonly EditorDirtyStateService _dirtyState = EditorDirtyStateService.Current;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;

    private readonly GraphicsDevice? _graphicsDevice;
    private readonly HostedEditorGameAdapter? _editorRuntime;
    private readonly IWindowInputSource? _windowInputSource;
    private readonly CutscenePreviewSession? _preview;
    private WorldViewportPanel? _previewViewport;
    private MGComboBox<string>? _worldCombo;
    private MGTextBlock? _previewStatusText;
    private MGTextBlock? _timeText;
    private int _lastTimeTenths = -1;
    private bool _lastTimePlaying;
    private MGTextBlock? _noPreviewWorldText;
    private MGElement? _previewViewportContent;
    private CutscenePreviewSettings? _previewSettings;
    private readonly List<CutscenePreviewWorldCandidate> _previewWorlds = [];
    private readonly List<string> _previewWorldLabels = [];
    private PreviewStatusKey _lastPreviewStatusKey;

    private CutsceneEditSession? _session;
    private CutsceneReadOnlyDocument? _document;
    private CutsceneActionData? _selectedAction;
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

    /// <summary>The action the editing operations aim at, or null. It is always an action of the tree of the document.</summary>
    public CutsceneActionData? SelectedAction => _selectedAction;

    /// <summary>Raised when <see cref="SelectedAction"/> changes, including when a change of the document empties it.</summary>
    public event Action<CutsceneAssetInspectorPanel>? SelectionChanged;

    /// <summary>
    /// Raised once after every command, undo, redo, reload and save of the document. When it is raised the selection is
    /// valid again and a running preview has been restarted on the edited asset.
    /// </summary>
    public event Action<CutsceneAssetInspectorPanel>? DocumentChanged;

    /// <summary>The edit session of the document, or null before <see cref="LoadAsset"/>.</summary>
    internal CutsceneEditSession? Session => _session;

    /// <summary>The validation messages and the runtime state of the document as last built, or null before <see cref="LoadAsset"/>.</summary>
    internal CutsceneReadOnlyDocument? ReadOnlyDocument => _document;

    /// <summary>Raised when the runtime state shown by the document changed (the editor world started, stopped or moved to another action).</summary>
    internal event Action<CutsceneAssetInspectorPanel>? RuntimeDisplayChanged;

    /// <summary>
    /// The names of the entities of the preview world; empty without a preview. The list is reused by every call:
    /// see <see cref="CutscenePreviewSession.GetEntityNames"/>.
    /// </summary>
    internal IReadOnlyList<string> GetPreviewEntityNames()
        => _preview?.GetEntityNames() ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>
    /// Selects an action of the tree, or clears the selection with null. False, and no change, when the action is not in the
    /// tree of the document.
    /// </summary>
    public bool SelectAction(CutsceneActionData? action)
    {
        if (action != null)
        {
            var root = _session?.Asset.RootAction;
            if (root == null || !CutsceneActionTree.Contains(root, action))
            {
                return false;
            }
        }

        SetSelection(action);
        return true;
    }

    private void SetSelection(CutsceneActionData? action)
    {
        if (ReferenceEquals(_selectedAction, action))
        {
            return;
        }

        _selectedAction = action;
        SelectionChanged?.Invoke(this);
    }

    /// <summary>
    /// Writes one property of an action as one entry of the history of the document. Returns a message when the change is
    /// refused, and null when it is done or when the value is already the one of the action (no entry then).
    /// </summary>
    public string? SetProperty(CutsceneActionData action, CutsceneActionProperty property, object? value)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(property);

        string? refusal = GetEditRefusal(action, out var historyContext);
        if (refusal != null)
        {
            return refusal;
        }

        if (!HasFieldProblem(action, property.Key) && property.AreEqual(property.GetValue(action), value))
        {
            return null;
        }

        ExecuteCommand(historyContext, new SetCutsceneActionPropertyCommand(_session!, action, property, value));
        return null;
    }

    /// <summary>
    /// Writes several properties of an action as a single entry of the history. Properties that already hold their value
    /// are skipped; no entry is made when none changes. Returns a message when the change is refused.
    /// </summary>
    internal string? SetProperties(CutsceneActionData action, string description, IReadOnlyList<CutscenePropertyChange> changes)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);

        string? refusal = GetEditRefusal(action, out var historyContext);
        if (refusal != null)
        {
            return refusal;
        }

        var commands = new List<IEditorCommand>(changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            if (!HasFieldProblem(action, change.Property.Key) && change.Property.AreEqual(change.Property.GetValue(action), change.Value))
            {
                continue;
            }

            commands.Add(new SetCutsceneActionPropertyCommand(_session!, action, change.Property, change.Value));
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(historyContext, new EditorCompositeCommand(description, commands));
        }

        return null;
    }

    /// <summary>Adds an action of a catalogue type where the insertion rule says (ADR-0097), and selects it.</summary>
    public string? AddAction(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        string? refusal = GetEditRefusal(null, out var historyContext);
        if (refusal != null)
        {
            return refusal;
        }

        if (!CutsceneActionTree.TryGetInsertionTarget(_session!.Asset.RootAction, _selectedAction, out var parent, out int index, out string error))
        {
            return error;
        }

        var action = CutsceneActionCatalog.Create(type);
        ExecuteCommand(historyContext, new InsertCutsceneActionCommand(_session, parent, index, action));
        SetSelection(action);
        return null;
    }

    /// <summary>Inserts a copy of the selected action just after it and selects the copy.</summary>
    public string? DuplicateSelectedAction()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        string? refusal = GetEditRefusal(selected, out var historyContext);
        if (refusal != null)
        {
            return refusal;
        }

        var command = InsertCutsceneActionCommand.TryCreateDuplicate(_session!, selected, out string error);
        if (command == null)
        {
            return error;
        }

        ExecuteCommand(historyContext, command);
        SetSelection(command.Action);
        return null;
    }

    /// <summary>Removes the selected action and its children; the selection is empty afterwards.</summary>
    public string? RemoveSelectedAction()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        string? refusal = GetEditRefusal(selected, out var historyContext)
                          ?? RemoveCutsceneActionCommand.GetRefusal(_session!, selected);
        if (refusal != null)
        {
            return refusal;
        }

        ExecuteCommand(historyContext, new RemoveCutsceneActionCommand(_session!, selected));
        return null;
    }

    /// <summary>The message that refuses an edit of <paramref name="action"/> (null: no action to check), or null.</summary>
    private string? GetEditRefusal(CutsceneActionData? action, out EditorHistoryContext historyContext)
    {
        historyContext = EditorHistoryContext.Empty;
        if (_session == null)
        {
            return "No cutscene asset is loaded.";
        }

        if (!TryGetHistoryContext(out historyContext))
        {
            return "This cutscene has no history context: the change is refused.";
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            return "Cutscene edits are disabled during a play session.";
        }

        if (action == null)
        {
            return null;
        }

        var root = _session.Asset.RootAction;
        return root != null && CutsceneActionTree.Contains(root, action) ? null : "The action is not in the cutscene.";
    }

    private void ExecuteCommand(EditorHistoryContext historyContext, IEditorCommand command)
    {
        _message = command.Description;
        EditorHistoryService.Current.Execute(historyContext, command);
    }

    /// <summary>Keeps the selection inside the tree, restarts a running preview, then tells the listeners.</summary>
    private void RaiseDocumentChanged()
    {
        if (_selectedAction != null)
        {
            var root = _session?.Asset.RootAction;
            if (root == null || !CutsceneActionTree.Contains(root, _selectedAction))
            {
                SetSelection(null);
            }
        }

        _preview?.ClearTimings();
        if (_preview != null && _preview.IsPlaying)
        {
            PlayPreview();
        }

        DocumentChanged?.Invoke(this);
    }

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

        RefreshHeader();
        if (_preview != null)
        {
            _root = CreateDocumentWithPreview();
            return _root;
        }

        // Without a preview the document is its header, its path and its status line: the properties are in the Inspector panel.
        _root = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        return _root;
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

        RefreshHeader();
        NotifyDirtyStateIfChanged();
        InitializePreviewWorlds();
        SetSelection(null);
        RaiseDocumentChanged();
    }

    /// <summary>
    /// Updates the runtime state shown by the Inspector panel. Listeners are told only when that state shows something other
    /// than an idle cutscene, so that an idle world does not rebuild the details every frame.
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

        RuntimeDisplayChanged?.Invoke(this);
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
        RefreshHeader();
        NotifyDirtyStateIfChanged();
        RaiseDocumentChanged();
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
        RefreshTimeText();
    }

    /// <summary>The play time shown at the end of the transport row, or empty without a preview.</summary>
    public string TransportText => _timeText?.Text ?? string.Empty;

    /// <summary>Rewrites the play time text when its tenth of a second or the play state changed: at most ten times a second.</summary>
    private void RefreshTimeText()
    {
        if (_timeText == null || _preview == null)
        {
            return;
        }

        bool playing = _preview.IsPlaying;
        double elapsed = playing ? _preview.ElapsedSeconds : 0d;
        int tenths = (int)(elapsed * 10d);
        if (tenths == _lastTimeTenths && playing == _lastTimePlaying)
        {
            return;
        }

        _lastTimeTenths = tenths;
        _lastTimePlaying = playing;
        long frames = playing ? _preview.ElapsedFrames : 0;
        _timeText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{(playing ? "Playing" : "Stopped")}   {elapsed:0.00} s  ·  {frames} frames");
    }

    public void DrawViewport(GameTime gameTime)
    {
        _previewViewport?.DrawViewport(gameTime);
    }

    private MGDockPanel CreateDocumentWithPreview()
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
        documentRoot.TryAddChild(_statusText!, Dock.Top);

        _previewStatusText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };
        documentRoot.TryAddChild(_previewStatusText, Dock.Top);

        _noPreviewWorldText = new MGTextBlock(_window, "No preview world is loaded: choose a world above to see the cutscene play.")
        {
            Margin = new Thickness(8, 8, 8, 8),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
            Visibility = Visibility.Collapsed,
        };
        documentRoot.TryAddChild(_noPreviewWorldText, Dock.Top);

        // The last child of the dock panel fills what is left: the viewport gets the whole area under the status lines.
        _previewViewportContent = _previewViewport.CreateContent();
        documentRoot.TryAddChild(_previewViewportContent, Dock.Left);

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

        _timeText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = EditorThemePalette.SecondaryTextOpacity,
        };
        row.TryAddChild(_timeText);
        _lastTimeTenths = -1;
        RefreshTimeText();
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

    /// <summary>Starts the preview play as the Play button does. False when the panel has no preview or no asset.</summary>
    public bool TryStartPreviewPlay()
    {
        if (_preview == null || _session == null)
        {
            return false;
        }

        PlayPreview();
        return true;
    }

    /// <summary>
    /// Lines describing the preview for the automation diagnostics: state, status text, skipped actions, and the
    /// name and position of every entity of the preview world.
    /// </summary>
    public List<string> GetAutomationStateSnapshot()
    {
        var lines = new List<string>();
        if (_preview == null)
        {
            lines.Add("Preview: <none>");
            return lines;
        }

        lines.Add($"Preview state: {_preview.State}, failure='{_preview.FailureReason}', error='{_preview.Error}', endedByWorldChange={_preview.EndedByWorldChange}");
        lines.Add($"Preview status line: {_previewStatusText?.Text}");
        for (int index = 0; index < _preview.Requests.Count; index++)
        {
            var request = _preview.Requests[index];
            lines.Add($"Preview skipped action: {request.Type} -> {request.Target}");
        }

        var world = _preview.World;
        lines.Add($"Preview world: {world?.Name ?? "<none>"}");
        if (world != null)
        {
            AppendEntityPositions(lines, world.Entities);
        }

        return lines;
    }

    private static void AppendEntityPositions(List<string> lines, IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            var position = entity.RootComponent?.Position;
            lines.Add(position.HasValue
                ? string.Create(CultureInfo.InvariantCulture, $"Preview entity '{entity.Name}': x={position.Value.X:0.###} y={position.Value.Y:0.###} z={position.Value.Z:0.###}")
                : $"Preview entity '{entity.Name}': <no root component>");
            AppendEntityPositions(lines, entity.Children);
        }
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

        // Without a preview world the viewport would show the world of the editor, which is not what the play runs in.
        if (_previewViewportContent != null)
        {
            _previewViewportContent.Visibility = world == null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_noPreviewWorldText != null)
        {
            _noPreviewWorldText.Visibility = world == null ? Visibility.Visible : Visibility.Collapsed;
        }

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
            text.Append("The project has no world to preview the cutscene in.");
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

        if (_preview.Requests.Count > 0)
        {
            text.Append('.');
        }

        if (_preview.EndedByWorldChange)
        {
            text.Append(" Would change the world: the play ends here.");
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
        RefreshHeader();
        NotifyDirtyStateIfChanged();
        RaiseDocumentChanged();
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

    private void RefreshHeader()
    {
        if (_headerText == null || _sourceText == null || _statusText == null)
        {
            return;
        }

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
        _statusText.Text = _message.Length == 0 ? text : $"{text}  |  {EscapeMarkup(_message)}";
    }

    private void SetMessage(string message)
    {
        _message = message;
        UpdateStatusText();
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

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}

/// <summary>One property and the value to write in it, for <see cref="CutsceneAssetInspectorPanel.SetProperties"/>.</summary>
internal readonly record struct CutscenePropertyChange(CutsceneActionProperty Property, object? Value);
