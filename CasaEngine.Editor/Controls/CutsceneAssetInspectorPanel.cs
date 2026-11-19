#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Runtime.Overlays;
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
    private CutsceneScreenFadeOverlay? _screenFadeOverlay;
    private MGComboBox<string>? _worldCombo;
    private MGTextBlock? _previewStatusText;
    private MGTextBlock? _timeText;
    private MGTextBlock? _playPauseText;
    private int _lastTimeTenths = -1;
    private long _lastTimeFrames = -1;
    private CutscenePreviewStatus? _lastTimeStatus;
    private MGTextBlock? _noPreviewWorldText;
    private MGElement? _previewViewportContent;
    private CutscenePreviewSettings? _previewSettings;
    private readonly List<CutscenePreviewWorldCandidate> _previewWorlds = [];
    private readonly List<string> _previewWorldLabels = [];
    private PreviewStatusKey _lastPreviewStatusKey;

    private CutsceneClipboard _clipboard = new();
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

    /// <summary>The message that refuses <see cref="AddAction"/>, or null when an action can be added. Changes nothing.</summary>
    public string? GetAddRefusal()
    {
        string? refusal = GetEditRefusal(null, out _);
        if (refusal != null)
        {
            return refusal;
        }

        return CutsceneActionTree.TryGetInsertionTarget(_session!.Asset.RootAction, _selectedAction, out _, out _, out string error)
            ? null
            : error;
    }

    /// <summary>The message that refuses <see cref="DuplicateSelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetDuplicateRefusal()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return GetEditRefusal(selected, out _) ?? InsertCutsceneActionCommand.GetDuplicateRefusal(_session!, selected);
    }

    /// <summary>The message that refuses <see cref="RemoveSelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetRemoveRefusal()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return GetEditRefusal(selected, out _) ?? RemoveCutsceneActionCommand.GetRefusal(_session!, selected);
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

    /// <summary>
    /// The clipboard of the actions, shared by every cutscene document of the editor when the editor sets it (P3). A document
    /// that was given none has a clipboard of its own.
    /// </summary>
    internal CutsceneClipboard Clipboard
    {
        get => _clipboard;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _clipboard = value;
        }
    }

    /// <summary>
    /// Moves an action, selected or not, to a container and an index among its children before the move (P1), and selects
    /// it. Returns a message when the move is refused, and null when it is done.
    /// </summary>
    public string? MoveAction(CutsceneActionData action, CutsceneActionData destination, int index)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(destination);

        string? refusal = GetMoveRefusal(action, destination, index);
        if (refusal != null)
        {
            return refusal;
        }

        TryGetHistoryContext(out var historyContext);
        ExecuteCommand(historyContext, new MoveCutsceneActionCommand(_session!, action, destination, index));
        SetSelection(action);
        return null;
    }

    /// <summary>The message that refuses <see cref="MoveAction"/>, or null when the move is allowed. Changes nothing.</summary>
    public string? GetMoveRefusal(CutsceneActionData action, CutsceneActionData destination, int index)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(destination);

        return GetEditRefusal(action, out _) ?? MoveCutsceneActionCommand.GetRefusal(_session!, action, destination, index);
    }

    /// <summary>Swaps the selected action with the one before it, in the same container (P9).</summary>
    public string? MoveSelectedActionUp()
    {
        string? refusal = GetMoveUpRefusal(out var action, out var parent, out int index);
        return refusal ?? MoveAction(action!, parent!, index - 1);
    }

    /// <summary>Swaps the selected action with the one after it, in the same container (P9).</summary>
    public string? MoveSelectedActionDown()
    {
        string? refusal = GetMoveDownRefusal(out var action, out var parent, out int index);
        return refusal ?? MoveAction(action!, parent!, index + 2);
    }

    /// <summary>The message that refuses <see cref="MoveSelectedActionUp"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetMoveUpRefusal() => GetMoveUpRefusal(out _, out _, out _);

    /// <summary>The message that refuses <see cref="MoveSelectedActionDown"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetMoveDownRefusal() => GetMoveDownRefusal(out _, out _, out _);

    private string? GetMoveUpRefusal(out CutsceneActionData? action, out CutsceneActionData? parent, out int index)
    {
        string? refusal = GetSiblingMoveRefusal(out action, out parent, out index);
        if (refusal != null)
        {
            return refusal;
        }

        return index == 0
            ? "The action is already the first of its container."
            : GetMoveRefusal(action!, parent!, index - 1);
    }

    private string? GetMoveDownRefusal(out CutsceneActionData? action, out CutsceneActionData? parent, out int index)
    {
        string? refusal = GetSiblingMoveRefusal(out action, out parent, out index);
        if (refusal != null)
        {
            return refusal;
        }

        return index == CutsceneActionCatalog.GetChildren(parent!).Count - 1
            ? "The action is already the last of its container."
            : GetMoveRefusal(action!, parent!, index + 2);
    }

    // The checks that up and down share: a selected action that has a container.
    private string? GetSiblingMoveRefusal(out CutsceneActionData? action, out CutsceneActionData? parent, out int index)
    {
        action = _selectedAction;
        parent = null;
        index = -1;
        if (action == null)
        {
            return "No action is selected.";
        }

        string? refusal = GetEditRefusal(action, out _);
        if (refusal != null)
        {
            return refusal;
        }

        if (!CutsceneActionTree.TryFindParent(_session!.Asset.RootAction!, action, out var found, out index))
        {
            return "The root action cannot be moved.";
        }

        parent = found;
        return null;
    }

    /// <summary>Wraps the selected action in a new container of a type (P2) and selects the container.</summary>
    public string? WrapSelectedAction(string containerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerType);

        string? refusal = GetWrapRefusal(containerType);
        if (refusal != null)
        {
            return refusal;
        }

        TryGetHistoryContext(out var historyContext);
        var command = new WrapCutsceneActionCommand(_session!, _selectedAction!, containerType);
        ExecuteCommand(historyContext, command);
        SetSelection(command.Container);
        return null;
    }

    /// <summary>The message that refuses <see cref="WrapSelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetWrapRefusal(string containerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerType);

        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return GetEditRefusal(selected, out _) ?? WrapCutsceneActionCommand.GetRefusal(_session!, selected, containerType);
    }

    /// <summary>Replaces the selected container by its actions (P2); selects the first of them, or nothing for an empty container.</summary>
    public string? UngroupSelectedAction()
    {
        string? refusal = GetUngroupRefusal();
        if (refusal != null)
        {
            return refusal;
        }

        var container = _selectedAction!;
        var children = CutsceneActionCatalog.GetChildren(container);
        var first = children.Count > 0 ? children[0] : null;

        TryGetHistoryContext(out var historyContext);
        ExecuteCommand(historyContext, new UngroupCutsceneActionCommand(_session!, container));
        SetSelection(first);
        return null;
    }

    /// <summary>The message that refuses <see cref="UngroupSelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetUngroupRefusal()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return GetEditRefusal(selected, out _) ?? UngroupCutsceneActionCommand.GetRefusal(_session!, selected);
    }

    /// <summary>Keeps a copy of the selected action and its children in the clipboard (P3). The document does not change.</summary>
    public string? CopySelectedAction()
    {
        string? refusal = GetCopyRefusal();
        return refusal ?? _clipboard.TryCopy(_session!, _selectedAction!);
    }

    /// <summary>The message that refuses <see cref="CopySelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetCopyRefusal()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return _session == null ? "No cutscene asset is loaded." : CutsceneClipboard.GetCopyRefusal(_session, selected);
    }

    /// <summary>
    /// Copies the selected action, then removes it (P3): one entry of the history. Nothing is copied or removed when either
    /// part is refused. The selection is empty afterwards.
    /// </summary>
    public string? CutSelectedAction()
    {
        string? refusal = GetCutRefusal();
        if (refusal != null)
        {
            return refusal;
        }

        var selected = _selectedAction!;
        _clipboard.TryCopy(_session!, selected);

        TryGetHistoryContext(out var historyContext);
        ExecuteCommand(historyContext, new EditorCompositeCommand($"Cut {selected.Type}", new RemoveCutsceneActionCommand(_session!, selected)));
        return null;
    }

    /// <summary>The message that refuses <see cref="CutSelectedAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetCutRefusal()
    {
        var selected = _selectedAction;
        if (selected == null)
        {
            return "No action is selected.";
        }

        return GetEditRefusal(selected, out _)
               ?? RemoveCutsceneActionCommand.GetRefusal(_session!, selected)
               ?? CutsceneClipboard.GetCopyRefusal(_session!, selected);
    }

    /// <summary>Inserts a new copy of the clipboard where the insertion rule says (ADR-0097), and selects the copy.</summary>
    public string? PasteAction()
    {
        string? refusal = GetPasteRefusal();
        if (refusal != null)
        {
            return refusal;
        }

        CutsceneActionTree.TryGetInsertionTarget(_session!.Asset.RootAction, _selectedAction, out var parent, out int index, out _);
        var copy = _clipboard.CreatePaste()!;

        TryGetHistoryContext(out var historyContext);
        ExecuteCommand(historyContext, new InsertCutsceneActionCommand(_session, parent, index, copy));
        SetSelection(copy);
        return null;
    }

    /// <summary>The message that refuses <see cref="PasteAction"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetPasteRefusal()
    {
        string? refusal = GetEditRefusal(null, out _);
        if (refusal != null)
        {
            return refusal;
        }

        if (!_clipboard.HasContent)
        {
            return "The clipboard is empty.";
        }

        return CutsceneActionTree.TryGetInsertionTarget(_session!.Asset.RootAction, _selectedAction, out _, out _, out string error)
            ? null
            : error;
    }

    /// <summary>
    /// Places an action, selected or not, at a time of the timeline (ADR-0102): the projection of the current tree, with the
    /// measured durations like the timeline panel, gives the current start of the action; the dropped time is converted by
    /// difference into the <c>seconds</c> of an At, which is either written or created around the action, as one entry of the
    /// history. The action is selected afterwards. Returns a message when the placement is refused (see
    /// <see cref="GetPlaceActionRefusal"/>), and null when it is done.
    /// </summary>
    public string? PlaceActionAt(CutsceneActionData action, double timelineSeconds)
    {
        ArgumentNullException.ThrowIfNull(action);

        string? refusal = PreparePlace(action, timelineSeconds, out var historyContext, out var command);
        if (refusal != null)
        {
            return refusal;
        }

        ExecuteCommand(historyContext, command!);
        SetSelection(action);
        return null;
    }

    /// <summary>
    /// The message that refuses <see cref="PlaceActionAt"/>, or null when it is allowed. Refused: an action that has no
    /// main bar in the timeline (a container) or that no drag moves, a time that is not a number, and the other refusals of
    /// <see cref="CutscenePlacement.GetPlaceRefusal"/>. Changes nothing.
    /// </summary>
    public string? GetPlaceActionRefusal(CutsceneActionData action, double timelineSeconds)
    {
        ArgumentNullException.ThrowIfNull(action);
        return PreparePlace(action, timelineSeconds, out _, out _);
    }

    private string? PreparePlace(CutsceneActionData action, double timelineSeconds, out EditorHistoryContext historyContext, out IEditorCommand? command)
    {
        command = null;
        string? refusal = GetEditRefusal(action, out historyContext);
        if (refusal != null)
        {
            return refusal;
        }

        if (!double.IsFinite(timelineSeconds))
        {
            return "The time must be a number of seconds.";
        }

        var root = _session!.Asset.RootAction!;
        var layout = CutsceneTimelineProjection.Build(root, _preview?.Timings.Durations);
        var item = FindPrimaryItem(layout, action);
        if (item == null)
        {
            return "This action has no bar in the timeline: it cannot be placed in time.";
        }

        var rule = CutscenePlacement.Resolve(root, action);
        double currentSeconds = rule.Kind == CutscenePlacementKind.ChangeAt ? rule.At!.Seconds : 0;
        double seconds = CutscenePlacement.ConvertDrop(currentSeconds, item.StartSeconds, timelineSeconds);

        command = CutscenePlacement.TryCreatePlace(_session, action, seconds, out string error);
        return command == null ? error : null;
    }

    private static CutsceneTimelineItem? FindPrimaryItem(CutsceneTimelineLayout layout, CutsceneActionData action)
    {
        var items = layout.Items;
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.IsPrimary && ReferenceEquals(item.Action, action))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds an action of a catalogue type at a time of the timeline (ADR-0102), and selects it. The target is the selected
    /// Parallel, otherwise the nearest Parallel that holds the selection; the action goes in a new At at the end of it, whose
    /// <c>seconds</c> is the time minus the start of the Parallel in the timeline, aligned on 1/60 s and not below 0. One
    /// entry of the history. Returns a message when the addition is refused (see <see cref="GetAddActionAtRefusal(string, double)"/>),
    /// and null when it is done.
    /// </summary>
    public string? AddActionAt(string type, double timelineSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        string? refusal = PrepareAddAt(type, timelineSeconds, out var historyContext, out var command, out var created);
        if (refusal != null)
        {
            return refusal;
        }

        ExecuteCommand(historyContext, command!);
        SetSelection(created);
        return null;
    }

    /// <summary>The message that refuses <see cref="AddActionAt"/>, or null when it is allowed. Changes nothing.</summary>
    public string? GetAddActionAtRefusal(string type, double timelineSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return PrepareAddAt(type, timelineSeconds, out _, out _, out _);
    }

    /// <summary>
    /// The message that refuses adding an action at a time whatever its type, or null when a type could be added: the edit
    /// refusals, a time that is not a number, and a selection with no Parallel to receive the action. Changes nothing.
    /// </summary>
    public string? GetAddActionAtRefusal(double timelineSeconds)
    {
        string? refusal = GetEditRefusal(null, out _);
        return refusal ?? GetAddAtTarget(timelineSeconds, out _, out _);
    }

    private string? PrepareAddAt(string type, double timelineSeconds, out EditorHistoryContext historyContext, out IEditorCommand? command, out CutsceneActionData? created)
    {
        command = null;
        created = null;
        ParallelCutsceneActionData? target = null;
        double seconds = 0;
        string? refusal = GetEditRefusal(null, out historyContext) ?? GetAddAtTarget(timelineSeconds, out target, out seconds);
        if (refusal != null)
        {
            return refusal;
        }

        command = CutscenePlacement.TryCreateAdd(_session!, target!, type, seconds, out created, out string error);
        return command == null ? error : null;
    }

    // The Parallel that receives an action added at a time, and the seconds of the At: the time minus the start of the Parallel.
    private string? GetAddAtTarget(double timelineSeconds, out ParallelCutsceneActionData? target, out double seconds)
    {
        target = null;
        seconds = 0;
        if (!double.IsFinite(timelineSeconds))
        {
            return "The time must be a number of seconds.";
        }

        var root = _session!.Asset.RootAction;
        target = root == null ? null : CutscenePlacement.FindTargetParallel(root, _selectedAction);
        if (target == null)
        {
            return "Select a Parallel, or an action inside one, to add an action at a time.";
        }

        var layout = CutsceneTimelineProjection.Build(root, _preview?.Timings.Durations);
        if (!layout.TryGetContainerStart(target, out double start))
        {
            return "The Parallel is not shown in the timeline.";
        }

        seconds = CutscenePlacement.ConvertDrop(0, start, timelineSeconds);
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
        RestartPreviewAfterChange();

        DocumentChanged?.Invoke(this);
    }

    /// <summary>
    /// A change of the document replays the preview (ADR-0099): a running play restarts from the beginning, a paused play or
    /// a seek in progress goes to the same time again and stays paused, and nothing is replayed when no play is running.
    /// </summary>
    private void RestartPreviewAfterChange()
    {
        if (_preview == null || _session == null)
        {
            return;
        }

        switch (_preview.Status)
        {
            case CutscenePreviewStatus.Playing:
                PlayPreview();
                break;
            case CutscenePreviewStatus.Seeking:
                SeekPreview(_preview.SeekTargetSeconds);
                break;
            case CutscenePreviewStatus.Paused:
                SeekPreview(_preview.ElapsedSeconds);
                break;
        }
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
        _screenFadeOverlay?.Dispose();
        _screenFadeOverlay = null;
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

    /// <summary>The status word and play time shown under the transport row, or empty without a preview.</summary>
    public string TransportText => _timeText?.Text ?? string.Empty;

    /// <summary>
    /// Rewrites the status word, the play time and the label of the Play / Pause button. It writes only when what they show
    /// changes: the status, the tenth of a second, or while paused the number of frames.
    /// </summary>
    private void RefreshTimeText()
    {
        if (_timeText == null || _preview == null)
        {
            return;
        }

        var status = _preview.Status;
        double elapsed = _preview.ElapsedSeconds;
        long frames = _preview.ElapsedFrames;
        int tenths = (int)(elapsed * 10d);
        bool statusChanged = _lastTimeStatus != status;
        if (!statusChanged
            && tenths == _lastTimeTenths
            && (status != CutscenePreviewStatus.Paused || frames == _lastTimeFrames))
        {
            return;
        }

        _lastTimeStatus = status;
        _lastTimeTenths = tenths;
        _lastTimeFrames = frames;
        _timeText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{GetStatusWord(status)}   {elapsed:0.00} s  ·  {frames} frames");

        if (statusChanged && _playPauseText != null)
        {
            _playPauseText.Text = GetPlayPauseLabel(status);
        }
    }

    private static string GetStatusWord(CutscenePreviewStatus status)
    {
        return status switch
        {
            CutscenePreviewStatus.Seeking => "Seeking",
            CutscenePreviewStatus.Paused => "Paused",
            CutscenePreviewStatus.Playing => "Playing",
            CutscenePreviewStatus.Ended => "Ended",
            _ => "Stopped",
        };
    }

    /// <summary>The button pauses while the play advances or seeks, and plays or resumes otherwise.</summary>
    private static string GetPlayPauseLabel(CutscenePreviewStatus status)
    {
        return status is CutscenePreviewStatus.Playing or CutscenePreviewStatus.Seeking ? "Pause" : "Play";
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

        if (_preview != null)
        {
            // The veil of a fade is drawn over the image of the preview, from the screen effect of the play (ADR-0103).
            _screenFadeOverlay = new CutsceneScreenFadeOverlay(_preview.ScreenEffects);
            _previewViewport.SetPostUiOverlay(_screenFadeOverlay);
        }

        var documentRoot = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        documentRoot.TryAddChild(CreateTransportRow(), Dock.Top);
        documentRoot.TryAddChild(_timeText!, Dock.Top);
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

        var playPauseButton = CreateTransportButton("Play", () => TogglePreviewPlayback());
        playPauseButton.MinWidth = 66;
        _playPauseText = playPauseButton.Content as MGTextBlock;
        row.TryAddChild(playPauseButton);
        row.TryAddChild(CreateTransportButton("Stop", () => StopPreview()));
        row.TryAddChild(CreateTransportButton("Restart", PlayPreview));
        row.TryAddChild(CreateTransportButton("-1 frame", () => StepPreviewBack()));
        row.TryAddChild(CreateTransportButton("+1 frame", () => StepPreviewFrames(1)));

        // The status word and the play time sit under the buttons: the row has no room left for them at a usual panel width.
        _timeText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 8, 2),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
        };
        _lastTimeTenths = -1;
        _lastTimeStatus = null;
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

    /// <summary>True when the document has a preview and an asset to play, as the transport buttons need.</summary>
    public bool HasPreview => _preview != null && _session != null;

    /// <summary>What the status word under the transport row shows; <see cref="CutscenePreviewStatus.Stopped"/> without a preview.</summary>
    public CutscenePreviewStatus PreviewStatus => _preview?.Status ?? CutscenePreviewStatus.Stopped;

    /// <summary>Frame steps requested by <see cref="StepPreviewFrames"/> that the preview has not done yet.</summary>
    public int PreviewPendingFrameSteps => _preview?.PendingFrameSteps ?? 0;

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
        lines.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"Preview transport: status={_preview.Status}, mode={_preview.TransportMode}, playing={_preview.IsPlaying}, seek target={_preview.SeekTargetSeconds:0.000} s, pending frame steps={_preview.PendingFrameSteps}"));
        lines.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"Preview time: {_preview.ElapsedSeconds:0.000} s, {_preview.ElapsedFrames} frames; play/pause button label='{_playPauseText?.Text}'"));
        lines.Add($"Preview play launches: {_preview.LaunchCount}");
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
        RefreshTimeText();
    }

    /// <summary>
    /// Moves the preview to a play time, in seconds, and leaves it paused there; a negative time counts as 0. The upper
    /// bound, the duration of the timeline, is applied by the caller. The camera is kept when the world is rebuilt.
    /// False, and no change, when the document has no preview or no asset, or when the preview world cannot be rebuilt.
    /// </summary>
    public bool SeekPreview(double seconds)
    {
        if (_preview == null || _session == null)
        {
            return false;
        }

        bool done = _preview.Seek(_session.Asset, Math.Max(0d, seconds));
        ApplyPreviewWorldToViewport(keepCamera: true);
        RefreshPreviewStatus();
        RefreshTimeText();
        return done;
    }

    /// <summary>
    /// What the Play / Pause button does: pauses a play that advances or seeks, resumes a paused play, and plays from the
    /// beginning when no play runs (never played, ended or stopped). False when the document has no preview or no asset.
    /// </summary>
    public bool TogglePreviewPlayback()
    {
        if (_preview == null || _session == null)
        {
            return false;
        }

        switch (_preview.Status)
        {
            case CutscenePreviewStatus.Playing:
            case CutscenePreviewStatus.Seeking:
                _preview.Pause();
                break;
            case CutscenePreviewStatus.Paused:
                if (_preview.Resume())
                {
                    ApplyPreviewWorldToViewport(keepCamera: true);
                }

                break;
            default:
                PlayPreview();
                break;
        }

        RefreshPreviewStatus();
        RefreshTimeText();
        return true;
    }

    /// <summary>
    /// Advances the paused preview by <paramref name="count"/> frames of 1/60 s; a running preview is paused first. False
    /// when the document has no preview or no asset, when <paramref name="count"/> is not positive, or when no play runs
    /// (nothing is done then).
    /// </summary>
    public bool StepPreviewFrames(int count)
    {
        if (_preview == null || _session == null || count <= 0 || !_preview.IsPlaying)
        {
            return false;
        }

        if (_preview.StepFrames(count))
        {
            ApplyPreviewWorldToViewport(keepCamera: true);
        }

        RefreshTimeText();
        return true;
    }

    /// <summary>
    /// Moves the preview one frame back: to the current play time minus 1/60 s, not below 0, even after the play ended.
    /// False when the document has no preview or no asset.
    /// </summary>
    public bool StepPreviewBack()
    {
        if (_preview == null || _session == null)
        {
            return false;
        }

        long frame = (long)Math.Round(_preview.ElapsedSeconds / CutscenePreviewTransport.StepSeconds);
        return SeekPreview(Math.Max(0L, frame - 1) * (double)CutscenePreviewTransport.StepSeconds);
    }

    /// <summary>
    /// Puts the preview back to its state before any play, with the play time at 0; the camera is kept. False when the
    /// document has no preview or no asset, or when the preview world cannot be rebuilt.
    /// </summary>
    public bool StopPreview()
    {
        if (_preview == null || _session == null)
        {
            return false;
        }

        bool done = _preview.Rewind();
        ApplyPreviewWorldToViewport(keepCamera: true);
        RefreshPreviewStatus();
        RefreshTimeText();
        return done;
    }

    /// <summary>
    /// Shows the preview world in the viewport and frames its entities, or keeps the camera as it is when
    /// <paramref name="keepCamera"/> is true and the viewport already showed a preview world.
    /// </summary>
    private void ApplyPreviewWorldToViewport(bool keepCamera = false)
    {
        if (_previewViewport == null || _preview == null)
        {
            return;
        }

        var world = _preview.World;
        keepCamera &= _previewViewport.HasWorldOverride;
        if (keepCamera)
        {
            _previewViewport.SetWorldOverrideKeepingCamera(world);
        }
        else
        {
            _previewViewport.SetWorldOverride(world);
        }

        // Without a preview world the viewport would show the world of the editor, which is not what the play runs in.
        if (_previewViewportContent != null)
        {
            _previewViewportContent.Visibility = world == null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_noPreviewWorldText != null)
        {
            _noPreviewWorldText.Visibility = world == null ? Visibility.Visible : Visibility.Collapsed;
        }

        if (world == null || keepCamera)
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
        CutscenePreviewStatus Transport,
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
            _preview.Status,
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
            // While a play runs the word of the transport row is shown; otherwise the state the play ended in.
            var transport = key.Transport;
            if (transport is CutscenePreviewStatus.Playing or CutscenePreviewStatus.Paused or CutscenePreviewStatus.Seeking)
            {
                text.Append("Preview: ").Append(GetStatusWord(transport)).Append('.');
            }
            else
            {
                text.Append("Preview: ").Append(_preview.State).Append('.');
            }
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
