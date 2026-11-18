#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Editing;
using CasaEngine.Editor.Controls.Timeline.Menu;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The timeline of the active cutscene document (ADR-0098): a projection of its action tree on rows and times,
/// a playhead that follows the preview, and a selection shared with the outliner. The tree stays the model: the projection is
/// read-only, except that an item the placement rule allows (ADR-0102, P5) can be dragged in time, which writes the tree through
/// the document. The model is rebuilt when the document changes or a new duration is measured, never per frame; per frame only
/// the playhead moves. A right click on an item opens the structure menu shared with the outliner (P8, P12); the empty space
/// and the track headers have no menu.
/// </summary>
internal sealed class CutsceneTimelinePanel : ITimelineContextMenuProvider
{
    private const float EmptyOpenBarSeconds = 0.25f;
    private const int CompactTrackRowHeight = 23;
    private const float FitMarginFraction = 0.04f;

    // P7: the model is longer than the cutscene by a quarter of it, and by 2 s at least, so that an item can be dragged later than the end.
    private const double DisplayMarginFraction = 0.25;
    private const double DisplayMarginMinimumSeconds = 2d;

    // Frames to wait after a selection before bringing its track into view: the layout of the new model must have run.
    private const int RevealDelayFrames = 2;

    private readonly MGWindow _window;
    private readonly Dictionary<CutsceneActionData, Guid[]> _itemIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Guid, CutsceneActionData> _actionsByItemId = [];

    private MGDockPanel? _root;
    private MGTextBlock? _emptyText;
    private TimelineControl? _timeline;
    private CutsceneAssetInspectorPanel? _document;
    private CutsceneTimelineLayout _layout = CutsceneTimelineLayout.Empty;
    private int _lastTimingsVersion;
    private float _playhead;
    private float? _lastSeekRequestSeconds;
    private bool _lastSeekRequestAccepted;
    private bool _syncingSelection;
    private bool _fitPending;
    private bool _fitApplied;
    private float _fitPixelsPerSecond;
    private float _fitWidth;
    private Guid _revealTrackId;
    private int _revealDelay;
    private float? _lastPlaceRequestSeconds;
    private string? _lastPlaceRefusal;

    public CutsceneTimelinePanel(MGWindow window)
    {
        _window = window;
    }

    /// <summary>The document the panel is attached to, or null.</summary>
    public CutsceneAssetInspectorPanel? Document => _document;

    /// <summary>The projection currently shown.</summary>
    public CutsceneTimelineLayout Layout => _layout;

    /// <summary>The generic timeline control, or null before <see cref="CreateContent"/>.</summary>
    internal TimelineControl? Timeline => _timeline;

    /// <summary>The playhead position last pushed to the control, in seconds.</summary>
    public float PlayheadSeconds => _playhead;

    /// <summary>The play time of the last move requested from the ruler or the track area, bounded to the projection, or null when none was requested.</summary>
    public float? LastSeekRequestSeconds => _lastSeekRequestSeconds;

    /// <summary>Whether the document accepted the last request; false when it has no preview to move.</summary>
    public bool LastSeekRequestAccepted => _lastSeekRequestAccepted;

    /// <summary>The time an item was last dropped at after a drag, or null when none was dropped.</summary>
    public float? LastPlaceRequestSeconds => _lastPlaceRequestSeconds;

    /// <summary>The message that refused the last drop of an item, or null when it was done or none was dropped.</summary>
    public string? LastPlaceRefusal => _lastPlaceRefusal;

    /// <summary>
    /// The duration the timeline model shows for a cutscene that lasts <paramref name="realSeconds"/> (P7): the real duration plus
    /// the larger of a quarter of it and 2 s. The playhead, the seeks and the open bars keep the real duration.
    /// </summary>
    public static double GetDisplayedDuration(double realSeconds)
    {
        double real = double.IsFinite(realSeconds) && realSeconds > 0 ? realSeconds : 0;
        return real + Math.Max(real * DisplayMarginFraction, DisplayMarginMinimumSeconds);
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _root = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _emptyText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 6, 8, 6),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };
        _root.TryAddChild(_emptyText, Dock.Top);

        _timeline = new TimelineControl(_window, verticalScroll: true, preciseHitTest: true, trackRowHeight: CompactTrackRowHeight, rulerDragScrub: true)
        {
            ItemRenderer = new CutsceneTimelineItemRenderer(),
            CornerHeaderText = "Rows",
            EditPolicy = new CutsceneTimelineEditPolicy(),
        };

        // The control calls the policy to align a drag only while the snapping is enabled: the policy aligns on the frames of the preview.
        _timeline.SnapSettings.IsEnabled = true;
        _timeline.SnapSettings.Mode = TimelineSnapMode.Frame;
        _timeline.SnapSettings.FrameRate = (float)CutsceneTimelineProjection.FramesPerSecond;
        _timeline.ContextMenuProvider = this;
        _timeline.ItemTimeEditCommitted += OnTimelineItemTimeEditCommitted;
        _timeline.SelectedItemChanged += OnTimelineSelectedItemChanged;
        _timeline.TimeScrubbed += OnTimelineTimeScrubbed;

        // The last child of the dock panel fills what is left of the tab, so the control has a bounded height.
        _root.TryAddChild(_timeline, Dock.Left);

        Rebuild();
        return _root;
    }

    /// <summary>Attaches the panel to a cutscene document, or detaches it with null.</summary>
    public void SetEditorPanel(CutsceneAssetInspectorPanel? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document != null)
        {
            _document.DocumentChanged -= OnDocumentChanged;
            _document.SelectionChanged -= OnDocumentSelectionChanged;
        }

        _document = document;
        if (_document != null)
        {
            _document.DocumentChanged += OnDocumentChanged;
            _document.SelectionChanged += OnDocumentSelectionChanged;
        }

        _itemIds.Clear();
        _actionsByItemId.Clear();
        _fitPending = _document != null;
        _fitApplied = false;
        Rebuild();
    }

    /// <summary>Called once per frame: moves the playhead, fits the zoom and reveals the selection once the layout is there; rebuilds only when a new measure arrived.</summary>
    public void Update()
    {
        if (_timeline == null || _document == null)
        {
            return;
        }

        var preview = _document.Preview;
        if (preview != null && preview.TimingsVersion != _lastTimingsVersion)
        {
            Rebuild();
        }

        // The playhead stays where the play stopped, ended or was moved; the view only follows an advancing play.
        double elapsed = preview?.ElapsedSeconds ?? 0d;
        _playhead = (float)_layout.MapPlayTime(elapsed);
        _timeline.SetCurrentTimeSeconds(_playhead);
        if (preview != null && preview.Status is CutscenePreviewStatus.Playing or CutscenePreviewStatus.Seeking)
        {
            _timeline.EnsureTimeVisible(_playhead);
        }

        UpdateFit();
        UpdateReveal();
    }

    /// <summary>
    /// Fits the zoom to the projection once the control has a width: when the panel is bound to a document, and again while the
    /// width changes and the user has not zoomed since. A rebuild for the same document keeps the zoom.
    /// </summary>
    private void UpdateFit()
    {
        var timeline = _timeline!;
        if (timeline.Model == null || timeline.Visibility != Visibility.Visible)
        {
            return;
        }

        float width = timeline.VisibleTimeAreaWidth;
        if (width <= 0f)
        {
            return;
        }

        if (!_fitPending)
        {
            bool userZoomed = Math.Abs(timeline.PixelsPerSecond - _fitPixelsPerSecond) > 0.01f;
            if (!_fitApplied || userZoomed || Math.Abs(width - _fitWidth) < 0.5f)
            {
                return;
            }
        }

        if (timeline.FitTimeToWidth(GetContentSeconds(timeline.Model), FitMarginFraction))
        {
            _fitPending = false;
            _fitApplied = true;
            _fitWidth = width;
            _fitPixelsPerSecond = timeline.PixelsPerSecond;
        }
    }

    // The fit reads the duration of the projection, not the one of the model: the room of P7 must not shrink the view.
    private float GetContentSeconds(TimelineModel model)
    {
        float seconds = (float)_layout.DurationSeconds;
        for (int index = 0; index < model.Items.Count; index++)
        {
            var item = model.Items[index];
            seconds = Math.Max(seconds, item.StartTime + item.Duration);
        }

        return seconds;
    }

    private void UpdateReveal()
    {
        if (_revealTrackId == Guid.Empty)
        {
            return;
        }

        if (_revealDelay > 0)
        {
            _revealDelay--;
            return;
        }

        var timeline = _timeline!;
        if (timeline.TrackAreaViewportHeight <= 0)
        {
            return;
        }

        timeline.EnsureTrackVisible(_revealTrackId);
        _revealTrackId = Guid.Empty;
    }

    /// <summary>Appends the state shown by the timeline, one line per fact, for the automation diagnostics.</summary>
    public void AppendAutomationState(StringBuilder builder)
    {
        var preview = _document?.Preview;
        bool playing = preview != null && preview.IsPlaying;
        double elapsed = preview?.ElapsedSeconds ?? 0d;
        long frames = preview?.ElapsedFrames ?? 0;
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Cutscene timeline (attached: {_document != null}, playing: {playing}):"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"  - Play time: {elapsed:0.000} s, {frames} frames; playhead: {_playhead:0.000} s; total duration: {_layout.DurationSeconds:0.###} s"));
        builder.AppendLine($"  - Transport text: {_document?.TransportText}");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"  - Last seek request from the ruler: {(_lastSeekRequestSeconds.HasValue ? _lastSeekRequestSeconds.Value.ToString("0.000", CultureInfo.InvariantCulture) + " s" : "<none>")} (accepted: {_lastSeekRequestAccepted})"));

        var root = _document?.LoadedCutsceneAsset?.RootAction;
        var model = _timeline?.Model;
        int trackCount = model?.Tracks.Count ?? 0;
        int itemCount = model?.Items.Count ?? 0;
        builder.AppendLine($"  - Tracks: {trackCount}, items: {itemCount}");
        if (model != null)
        {
            var trackIndexes = new Dictionary<Guid, int>();
            for (int index = 0; index < model.Tracks.Count; index++)
            {
                trackIndexes[model.Tracks[index].Id] = index;
            }

            for (int index = 0; index < model.Items.Count; index++)
            {
                var item = model.Items[index];
                trackIndexes.TryGetValue(item.TrackId, out int trackIndex);
                var info = item.Source as CutsceneTimelineItemInfo;
                string state = info == null ? "?" : info.Item.IsMeasured ? "measured" : info.Item.IsEstimated ? "estimated" : "exact";
                string shape = info == null ? item.Kind.ToString() : info.Item.Shape.ToString();
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"  - Item track {trackIndex} '{model.Tracks[trackIndex].Label}' | {item.DisplayName} | start {item.StartTime:0.###} s | duration {item.Duration:0.###} s | {shape} | {state} | {DescribeDragRule(root, info)}"));
            }
        }

        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"  - Last drop: {(_lastPlaceRequestSeconds.HasValue ? _lastPlaceRequestSeconds.Value.ToString("0.000", CultureInfo.InvariantCulture) + " s" : "<none>")}; refusal: {_lastPlaceRefusal ?? "<none>"}"));
        if (root != null)
        {
            AppendParallelStarts(builder, root, "root_action");
        }

        string selected = "<none>";
        var selectedId = _timeline?.ViewState.SelectedItemId;
        if (selectedId.HasValue && model != null)
        {
            for (int index = 0; index < model.Items.Count; index++)
            {
                if (model.Items[index].Id == selectedId.Value)
                {
                    selected = model.Items[index].DisplayName;
                    break;
                }
            }
        }

        builder.AppendLine($"  - Selected item: {selected}");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"  - Track area: viewport {_timeline?.TrackAreaViewportHeight ?? 0} px, content {_timeline?.TrackAreaContentHeight ?? 0} px, overflow: {(_timeline?.TrackAreaContentHeight ?? 0) > (_timeline?.TrackAreaViewportHeight ?? 0)}, vertical bar shown: {_timeline?.IsVerticalScrollBarVisible ?? false}, scroll {_timeline?.ScrollY ?? 0f:0.#}"));
    }

    // The diagnostics of an item: what dragging it does (P5) and the lowest time it can be dragged to.
    private static string DescribeDragRule(CutsceneActionData? root, CutsceneTimelineItemInfo? info)
    {
        if (root == null || info == null)
        {
            return "drag: unknown";
        }

        var rule = CutscenePlacement.Resolve(root, info.Item);
        switch (rule.Kind)
        {
            case CutscenePlacementKind.ChangeAt:
                return string.Create(CultureInfo.InvariantCulture,
                    $"drag: changes the At {CutsceneActionTree.GetPath(root, rule.At!)} (seconds {rule.At!.Seconds:0.###}); lowest {info.LowestInstantSeconds:0.###} s");
            case CutscenePlacementKind.WrapInAt:
                return string.Create(CultureInfo.InvariantCulture,
                    $"drag: wraps {CutsceneActionTree.GetPath(root, rule.Node!)} in an At (in the Parallel {CutsceneActionTree.GetPath(root, rule.Parallel!)}); lowest {info.LowestInstantSeconds:0.###} s");
            default:
                return "drag: none";
        }
    }

    // One line per Parallel of the tree: the time at which it starts in the timeline.
    private void AppendParallelStarts(StringBuilder builder, CutsceneActionData action, string path)
    {
        if (action is ParallelCutsceneActionData)
        {
            string start = _layout.TryGetContainerStart(action, out double startSeconds)
                ? startSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s"
                : "<not in the timeline>";
            builder.AppendLine($"  - Parallel {path} starts at {start}");
        }

        var children = CutsceneActionCatalog.GetChildren(action);
        for (int index = 0; index < children.Count; index++)
        {
            AppendParallelStarts(builder, children[index], $"{path}.actions[{index}]");
        }
    }

    private void OnDocumentChanged(CutsceneAssetInspectorPanel document)
    {
        Rebuild();
    }

    private void OnDocumentSelectionChanged(CutsceneAssetInspectorPanel document)
    {
        if (_syncingSelection)
        {
            return;
        }

        SelectDocumentActionInTimeline();
    }

    private void OnTimelineSelectedItemChanged(TimelineItem? item)
    {
        // A click in the empty space clears the control's selection but not the document's (ADR-0098).
        if (_syncingSelection || item == null || _document == null || item.Source is not CutsceneTimelineItemInfo info)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _document.SelectAction(info.Item.Action);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnTimelineItemTimeEditCommitted(TimelineItem item, float seconds)
    {
        // The control has bounded and aligned the time; the document converts it by difference into the delay of an At (P6) and
        // refuses what the rule does not allow, in which case nothing changes and the item is drawn at its place by the next model.
        if (_document == null || item.Source is not CutsceneTimelineItemInfo info)
        {
            return;
        }

        _lastPlaceRequestSeconds = seconds;
        _lastPlaceRefusal = _document.PlaceActionAt(info.Item.Action, seconds);
    }

    MGContextMenu? ITimelineContextMenuProvider.CreateContextMenu(TimelineControl timeline, TimelineTrack? track, TimelineItem? item, float cursorTime)
    {
        if (_document == null)
        {
            return null;
        }

        // The empty space offers "Add here" at the time of the cursor (ADR-0102), when the selection gives a Parallel to receive
        // the action; otherwise it has no menu.
        if (item == null)
        {
            return CutsceneActionMenu.CreateAddHere(_window, _document, cursorTime, null);
        }

        // The structure operations act on an action, which an item stands for.
        if (item.Source is not CutsceneTimelineItemInfo info)
        {
            return null;
        }

        // The right click has already selected the item; doing it again is without effect, and it keeps the menu right
        // when the provider is asked without that click.
        _syncingSelection = true;
        try
        {
            _document.SelectAction(info.Item.Action);
        }
        finally
        {
            _syncingSelection = false;
        }

        return CutsceneActionMenu.Create(_window, _document, null);
    }

    MGContextMenu? ITimelineContextMenuProvider.CreateTrackHeaderContextMenu(TimelineControl timeline, TimelineTrack track)
    {
        return null;
    }

    private void OnTimelineTimeScrubbed(float seconds)
    {
        // A click moves the preview to that time and leaves it paused there; without a preview the playhead goes back.
        float target = Math.Clamp(seconds, 0f, (float)_layout.DurationSeconds);
        _lastSeekRequestSeconds = target;
        _lastSeekRequestAccepted = _document != null && _document.SeekPreview(target);
        if (!_lastSeekRequestAccepted)
        {
            _timeline?.SetCurrentTimeSeconds(_playhead);
        }
    }

    private void SelectDocumentActionInTimeline()
    {
        if (_timeline == null)
        {
            return;
        }

        Guid? itemId = null;
        var action = _document?.SelectedAction;
        if (action != null && _itemIds.TryGetValue(action, out var ids))
        {
            // The action's own item first; a Repeat is shown by its lap.
            for (int index = 0; index < ids.Length && itemId == null; index++)
            {
                if (ids[index] != Guid.Empty && _actionsByItemId.ContainsKey(ids[index]))
                {
                    itemId = ids[index];
                }
            }
        }

        _syncingSelection = true;
        try
        {
            _timeline.SetSelectedItemId(itemId, notify: false);
        }
        finally
        {
            _syncingSelection = false;
        }

        if (!itemId.HasValue)
        {
            // The control selects the first track by default: with nothing selected no row should look selected.
            _timeline.ClearSelectedTrack();
            _revealTrackId = Guid.Empty;
            return;
        }

        if (_timeline.Model != null)
        {
            var items = _timeline.Model.Items;
            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].Id == itemId.Value)
                {
                    // Revealed by Update once the layout is available: before it the viewport has no height.
                    _revealTrackId = items[index].TrackId;
                    _revealDelay = RevealDelayFrames;
                    break;
                }
            }
        }
    }

    private void Rebuild()
    {
        if (_timeline == null)
        {
            return;
        }

        var preview = _document?.Preview;
        _lastTimingsVersion = preview?.TimingsVersion ?? 0;

        var root = _document?.LoadedCutsceneAsset?.RootAction;
        if (_document == null || root == null)
        {
            _layout = CutsceneTimelineLayout.Empty;
            _itemIds.Clear();
            _actionsByItemId.Clear();
            _timeline.SetModel(null);
            ShowEmpty(_document == null ? "No cutscene is open." : "This cutscene has no action to show.");
            return;
        }

        _layout = CutsceneTimelineProjection.Build(root, preview?.Timings.Durations);
        _timeline.SetModel(BuildModel(_layout, root));
        ShowEmpty(_layout.Items.Count == 0 ? "This cutscene has no action to show." : null);
        SelectDocumentActionInTimeline();
    }

    private void ShowEmpty(string? message)
    {
        if (_emptyText != null)
        {
            _emptyText.Text = message ?? string.Empty;
            _emptyText.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_timeline != null)
        {
            _timeline.Visibility = message == null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private TimelineModel BuildModel(CutsceneTimelineLayout layout, CutsceneActionData root)
    {
        var model = new TimelineModel
        {
            DurationSeconds = (float)GetDisplayedDuration(layout.DurationSeconds),
            TimeUnit = TimelineTimeUnit.Seconds,
            FrameRate = (float)CutsceneTimelineProjection.FramesPerSecond,
        };

        // One track per sub-row: the control draws one row per track.
        var trackIds = new List<Guid[]>(layout.Rows.Count);
        for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
        {
            var row = layout.Rows[rowIndex];
            var ids = new Guid[row.SubRowCount];
            for (int sub = 0; sub < ids.Length; sub++)
            {
                var track = new TimelineTrack
                {
                    Label = sub == 0 ? row.Label : string.Empty,
                    IsEditable = false,
                };
                ids[sub] = track.Id;
                model.Tracks.Add(track);
            }

            trackIds.Add(ids);
        }

        var previousIds = new Dictionary<CutsceneActionData, Guid[]>(_itemIds, ReferenceEqualityComparer.Instance);
        _itemIds.Clear();
        _actionsByItemId.Clear();

        var items = layout.Items;
        for (int index = 0; index < items.Count; index++)
        {
            var source = items[index];
            var row = layout.Rows[source.RowIndex];
            int subRow = Math.Min(source.SubRow, trackIds[source.RowIndex].Length - 1);

            float duration = (float)source.DurationSeconds;
            TimelineItemKind kind = TimelineItemKind.Duration;
            switch (source.Shape)
            {
                case CutsceneTimelineItemShape.Marker:
                    kind = TimelineItemKind.Instant;
                    duration = 0f;
                    break;
                case CutsceneTimelineItemShape.Range:
                    kind = TimelineItemKind.Range;
                    break;
                case CutsceneTimelineItemShape.OpenBar:
                    duration = Math.Max((float)(layout.DurationSeconds - source.StartSeconds), EmptyOpenBarSeconds);
                    break;
            }

            // P5: only the item the placement rule allows is dragged, in time only: it has no resize and does not change track.
            var rule = CutscenePlacement.Resolve(root, source);
            bool canDrag = rule.CanDrag;
            float lowestInstant = canDrag
                ? (float)CutscenePlacement.LowestInstant(rule.Kind == CutscenePlacementKind.ChangeAt ? rule.At!.Seconds : 0d, source.StartSeconds)
                : 0f;

            var timelineItem = new TimelineItem
            {
                Id = GetItemId(previousIds, source),
                TrackId = trackIds[source.RowIndex][subRow],
                StartTime = (float)source.StartSeconds,
                Duration = duration,
                Kind = kind,
                ItemType = source.Action.Type,
                DisplayName = source.Label,
                ToolTipText = string.Create(CultureInfo.InvariantCulture, $"{source.Label}\nStart {source.StartSeconds:0.###} s, duration {source.DurationSeconds:0.###} s{(source.IsEstimated && !source.IsMeasured ? " (estimated)" : string.Empty)}{GetDragToolTip(rule)}"),
                IsEditable = canDrag,
                CanMove = canDrag,
                Source = new CutsceneTimelineItemInfo(source, row.Kind, source.Action.Type, double.PositiveInfinity, 0f, source.EndSeconds, canDrag, lowestInstant),
            };
            model.Items.Add(timelineItem);
        }

        // Labels never meet: a marker has room up to the next item of its track and none on a bar; a bar label starts
        // after a marker drawn on its start and ends before a marker drawn over it.
        for (int index = 0; index < model.Items.Count; index++)
        {
            var current = model.Items[index];
            var info = (CutsceneTimelineItemInfo)current.Source!;
            bool isMarker = current.Kind == TimelineItemKind.Instant;
            double next = double.PositiveInfinity;
            double labelEnd = current.StartTime + current.Duration;
            float inset = 0f;
            for (int other = 0; other < model.Items.Count; other++)
            {
                var candidate = model.Items[other];
                if (other == index || candidate.TrackId != current.TrackId)
                {
                    continue;
                }

                if (candidate.StartTime > current.StartTime && candidate.StartTime < next)
                {
                    next = candidate.StartTime;
                }

                if (isMarker && candidate.Kind == TimelineItemKind.Duration
                    && candidate.StartTime <= current.StartTime && candidate.StartTime + candidate.Duration > current.StartTime)
                {
                    // Over a bar: no room for the label.
                    next = current.StartTime;
                }

                if (!isMarker && candidate.Kind == TimelineItemKind.Instant)
                {
                    if (Math.Abs(candidate.StartTime - current.StartTime) < 0.05f)
                    {
                        inset = 14f;
                    }
                    else if (candidate.StartTime > current.StartTime && candidate.StartTime < labelEnd)
                    {
                        labelEnd = candidate.StartTime;
                    }
                }
            }

            current.Source = new CutsceneTimelineItemInfo(info.Item, info.RowKind, info.ShortLabel, next, inset, labelEnd, info.CanDrag, info.LowestInstantSeconds);
        }

        return model;
    }

    private static string GetDragToolTip(CutscenePlacementRule rule)
    {
        return rule.Kind switch
        {
            CutscenePlacementKind.ChangeAt => string.Create(CultureInfo.InvariantCulture, $"\nDrag in time: changes the delay of its At (now {rule.At!.Seconds:0.###} s)."),
            CutscenePlacementKind.WrapInAt => "\nDrag in time: wraps it in an At.",
            _ => string.Empty,
        };
    }

    private Guid GetItemId(Dictionary<CutsceneActionData, Guid[]> previousIds, CutsceneTimelineItem source)
    {
        if (!_itemIds.TryGetValue(source.Action, out var ids))
        {
            ids = previousIds.TryGetValue(source.Action, out var previous) ? previous : new Guid[3];
            _itemIds[source.Action] = ids;
        }

        int slot = (int)source.Role;
        if (ids[slot] == Guid.Empty)
        {
            ids[slot] = Guid.NewGuid();
        }

        _actionsByItemId[ids[slot]] = source.Action;
        return ids[slot];
    }
}
