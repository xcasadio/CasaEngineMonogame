using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Styling;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Keyboard;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dAssetInspectorPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly IWindowInputSource _windowInputSource;
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly Dictionary<Guid, SpriteData> _spriteDataById = new();
    private readonly List<Guid> _spriteIdsToResolve = new();
    private readonly List<TimelineDisplayLane> _timelineDisplayLanes = new();
    private readonly List<TimelineDisplayEventItem> _timelineDisplayEvents = new();

    private MGDockPanel _root;
    private MGDockPanel _inspectorRoot;
    private MGDockPanel _timelineRoot;
    private WorldViewportPanel _previewViewportPanel;
    private Entity _previewEntity;
    private AnimatedSpriteComponent _previewSpriteComponent;
    private MGTextBlock _headerText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGTextBlock _timelineText;
    private Animation2dTimelineControl _timelineControl;
    private MGStackPanel _contentStack;

    private Animation2dData _animationData;
    private string _loadedRelativePath;
    private string _historyContextId;
    private float _previewDurationSeconds;
    private float _previewTimeSeconds;
    private float _timelineZoomFactor = 1f;
    private int _selectedLaneIndex = -1;
    private int _selectedEventIndex = -1;
    private TimelineClipboardItem _copiedTimelineItem;
    private bool _isDirty;
    private bool _isPreviewPlaying = true;
    private bool _isApplyingHistorySnapshot;
    private bool _suppressControlCallbacks;
    private bool _disposed;

    private const float TimelineMinimumZoomFactor = 0.5f;
    private const float TimelineMaximumZoomFactor = 3.0f;
    private const float TimelineBasePixelsPerSecond = 96f;

    private enum TimelineDisplayEventSourceKind
    {
        PersistedEvent,
        TrackKeyframe,
    }

    private enum TimelineDisplayLaneSourceKind
    {
        TrackKeyframes,
        PersistedEvents,
    }

    private readonly record struct TimelineDisplayEventLocator(
        TimelineDisplayEventSourceKind SourceKind,
        Animation2dTrackProperty? TrackProperty,
        int TrackIndex,
        string EventName,
        Guid SpriteAssetId,
        float TimeSeconds,
        Vector2 Vector2Value,
        bool BoolValue,
        int IntValue,
        float FloatValue);

    private sealed class TimelineClipboardItem
    {
        public TimelineDisplayEventSourceKind SourceKind { get; init; }

        public Animation2dTrackProperty? TrackProperty { get; init; }

        public string EventName { get; init; } = string.Empty;

        public Guid SpriteAssetId { get; init; }

        public Vector2 Vector2Value { get; init; }

        public bool BoolValue { get; init; }

        public int IntValue { get; init; }

        public float FloatValue { get; init; }
    }

    private sealed class AnimationHistorySnapshot
    {
        public required JObject Document { get; init; }

        public required Guid AssetId { get; init; }

        public required string FileName { get; init; }

        public required bool IsDirty { get; init; }

        public required float PreviewTimeSeconds { get; init; }

        public required int SelectedLaneIndex { get; init; }

        public required TimelineDisplayEventLocator? SelectedEventLocator { get; init; }
    }

    private sealed class TimelineDisplayLane
    {
        public TimelineDisplayLane(string label, TimelineDisplayLaneSourceKind sourceKind, int trackIndex = -1, Animation2dTrackProperty? trackProperty = null, bool isEditable = true)
        {
            Label = label;
            SourceKind = sourceKind;
            TrackIndex = trackIndex;
            TrackProperty = trackProperty;
            IsEditable = isEditable;
        }

        public string Label { get; }

        public TimelineDisplayLaneSourceKind SourceKind { get; }

        public int TrackIndex { get; }

        public Animation2dTrackProperty? TrackProperty { get; }

        public bool IsEditable { get; }
    }

    private sealed class TimelineDisplayEventItem
    {
        public TimelineDisplayEventItem(
            AnimationEventAsset eventValue,
            TimelineDisplayEventSourceKind sourceKind,
            int laneIndex,
            string laneLabel,
            Animation2dTrackProperty? trackProperty = null,
            int trackIndex = -1,
            int keyframeIndex = -1,
            int eventIndex = -1,
            string valueText = "",
            Vector2 vector2Value = default,
            bool boolValue = false,
            int intValue = 0,
            float floatValue = 0f)
        {
            Event = eventValue;
            SourceKind = sourceKind;
            LaneIndex = laneIndex;
            LaneLabel = laneLabel;
            TrackProperty = trackProperty;
            TrackIndex = trackIndex;
            KeyframeIndex = keyframeIndex;
            EventIndex = eventIndex;
            ValueText = valueText;
            Vector2Value = vector2Value;
            BoolValue = boolValue;
            IntValue = intValue;
            FloatValue = floatValue;
        }

        public AnimationEventAsset Event { get; }

        public TimelineDisplayEventSourceKind SourceKind { get; }

        public int LaneIndex { get; }

        public string LaneLabel { get; }

        public Animation2dTrackProperty? TrackProperty { get; }

        public int TrackIndex { get; }

        public int KeyframeIndex { get; }

        public int EventIndex { get; }

        public string ValueText { get; }

        public Vector2 Vector2Value { get; }

        public bool BoolValue { get; }

        public int IntValue { get; }

        public float FloatValue { get; }
    }

    public Animation2dAssetInspectorPanel(
        MGWindow window,
        GraphicsDevice graphicsDevice,
        HostedEditorGameAdapter editorRuntime,
        IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _previewWorldDriver = new PreviewWorldDriver(
            _editorRuntime,
            new PreviewWorldDriverOptions
            {
                WorldName = "Animation2D Preview",
                UpdateMode = PreviewWorldUpdateMode.Continuous,
            });
    }

    public Animation2dData LoadedAnimationData => _animationData;

    public string LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    public event Action<Animation2dAssetInspectorPanel> DirtyStateChanged;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _previewViewportPanel = new WorldViewportPanel(_window, _graphicsDevice, _editorRuntime, _windowInputSource)
        {
            EnablePreviewSelection = false,
            EnablePreviewGizmo = false,
            ShowEditorOverlays = false,
            UseFront2dCamera = true,
        };

        _root = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _root.TryAddChild(_previewViewportPanel.CreateContent(), Dock.Top);

        if (_previewWorldDriver.World != null)
        {
            _previewViewportPanel.SetWorldOverride(_previewWorldDriver.World);
            _previewViewportPanel.FocusEntity(_previewEntity);
        }

        return _root;
    }

    public MGElement CreateInspectorContent()
    {
        if (_inspectorRoot != null)
        {
            return _inspectorRoot;
        }

        _headerText = new MGTextBlock(_window, string.Empty);
        _sourceText = new MGTextBlock(_window, string.Empty);
        _statusText = new MGTextBlock(_window, string.Empty);

        _contentStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 8, 8, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_contentStack);

        _inspectorRoot = new MGDockPanel(_window)
        {
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _inspectorRoot.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _inspectorRoot;
    }

    public MGElement CreateTimelineContent()
    {
        if (_timelineRoot != null)
        {
            return _timelineRoot;
        }

        _timelineRoot = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _timelineRoot.TryAddChild(CreateTimelinePanel(), Dock.Top);
        RefreshTimelineText();
        return _timelineRoot;
    }

    public void LoadAsset(Animation2dData animationData, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(animationData);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _animationData = animationData;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        RebuildTimelineDisplayEvents();
        ClampSelectedEventIndex();
        SetDirty(false);
        RebuildPreviewState();

        if (TryGetHistoryContext(out var historyContext))
        {
            EditorDirtyStateService.Current.MarkSaved(historyContext);
        }

        RefreshInspector();
    }

    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);
        _historyContextId = historyContextId;
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath) || IsDirty)
        {
            SetStatus(IsDirty ? $"Unsaved changes kept for {_loadedRelativePath}" : "No Animation2D asset is loaded.");
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadAsset(fullPath, out var animationData))
        {
            SetStatus($"Unable to reload {_loadedRelativePath}.");
            return false;
        }

        LoadAsset(animationData, fullPath);
        return true;
    }

    public bool TrySaveLoadedAsset(out string errorMessage)
    {
        errorMessage = null;
        if (_animationData == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No Animation2D asset is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            SetStatus($"Already saved {_loadedRelativePath}");
            return true;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _animationData, EditorAssetSaveSource.Animation2dEditorPanel);
            SetDirty(false);

            if (TryGetHistoryContext(out var historyContext))
            {
                EditorDirtyStateService.Current.MarkSaved(historyContext);
            }

            SetStatus($"Saved {_loadedRelativePath}");
            RefreshInspector();
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = exception.Message;
            SetStatus($"Failed to save {_loadedRelativePath}: {exception.Message}");
            return false;
        }
    }

    public void Update(GameTime gameTime)
    {
        if (_disposed)
        {
            return;
        }

        _previewWorldDriver.Tick(gameTime);

        if (_previewSpriteComponent != null)
        {
            _previewTimeSeconds = _previewSpriteComponent.CurrentAnimationTimeSeconds;
        }

        if (!IsTimelinePresentationAttached())
        {
            return;
        }

        RefreshTimelineText();
        RefreshTimelineControls();
        RefreshTimelinePlaybackState();
    }

    public void DrawViewport(GameTime gameTime)
    {
        _previewViewportPanel?.DrawViewport(gameTime);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _previewViewportPanel?.Dispose();
        _previewWorldDriver.Dispose();
        _previewViewportPanel = null;
        _previewEntity = null;
        _previewSpriteComponent = null;
    }

    public bool TryRefreshReferencedSpriteAsset(Guid spriteAssetId, SpriteData savedSpriteData = null)
    {
        if (_disposed || _animationData == null || spriteAssetId == Guid.Empty)
        {
            return false;
        }

        _spriteIdsToResolve.Clear();
        Animation2dSpriteReferenceCollector.Collect(_animationData, _spriteIdsToResolve);
        bool isReferencedSprite = false;
        for (var index = 0; index < _spriteIdsToResolve.Count; index++)
        {
            if (_spriteIdsToResolve[index] != spriteAssetId)
            {
                continue;
            }

            isReferencedSprite = true;
            break;
        }

        if (!isReferencedSprite)
        {
            return false;
        }

        SpriteData spriteData = savedSpriteData ?? _editorRuntime.AssetContentManager.Load<SpriteData>(spriteAssetId, cache: false);
        _spriteDataById[spriteAssetId] = spriteData;

        if (_previewSpriteComponent != null)
        {
            _previewSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteData);
            if (_previewSpriteComponent.CurrentCompositionState != null
                && Animation2dBoundsCalculator.TryCalculateLocalBounds(_previewSpriteComponent.CurrentCompositionState, _spriteDataById, out var localBounds))
            {
                Vector3 center = (localBounds.Min + localBounds.Max) * 0.5f;
                _previewSpriteComponent.Position = new Vector3(-center.X, -center.Y, 0f);
                _previewViewportPanel?.FocusEntity(_previewEntity);
            }

            _previewWorldDriver.RefreshNow();
        }

        return true;
    }

    public static bool TryLoadAsset(string fullPath, out Animation2dData animationData)
    {
        animationData = new Animation2dData();

        if (!File.Exists(fullPath)
            || !string.Equals(Path.GetExtension(fullPath), Constants.FileNameExtensions.Animation2d, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            animationData.Load(document);
            animationData.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(animationData.FileName)
                            ?? AssetCatalog.GetByFileName(animationData.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                animationData.Name = assetInfo.Name;
                animationData.AssetId = assetInfo.Id;
                animationData.FileName = assetInfo.FileName;
            }
            else
            {
                animationData.AssetId = animationData.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshInspector()
    {
        if (_contentStack == null)
        {
            return;
        }

        _contentStack.TryRemoveAll();
        if (_animationData == null)
        {
            ClearTimelineDisplayLanes();
            ClearTimelineDisplayEvents();
            _selectedLaneIndex = -1;
            _selectedEventIndex = -1;
            AddText("No animation asset loaded.", EditorThemePalette.SecondaryTextOpacity);
            RefreshTimelineText();
            RefreshTimelineControls();
            RefreshTimelineData();
            return;
        }

        RebuildTimelineDisplayLanes();
        RebuildTimelineDisplayEvents();
        ClampSelectedLaneIndex();
        ClampSelectedEventIndex();

        _contentStack.TryAddChild(CreateAnimationTypeRow());

        if (!TryGetSelectedEvent(out var selectedEvent) || selectedEvent == null)
        {
            AddText(_selectedLaneIndex >= 0 && _selectedLaneIndex < _timelineDisplayLanes.Count
                ? $"Selected lane: {EscapeMarkup(_timelineDisplayLanes[_selectedLaneIndex].Label)}. Use Insert or right click in the timeline to add an item."
                : _timelineDisplayEvents.Count == 0
                    ? "Timeline is empty. Use Insert or right click in the timeline to add an item."
                    : "Select a keyframe or event from the timeline to inspect it.",
                EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            _contentStack.TryAddChild(CreateCommittedTextBoxRow("Time", FormatFloat(selectedEvent.Event.TimeSeconds), value => ApplySelectedEventTime(selectedEvent, value)));
            _contentStack.TryAddChild(CreateSelectedEventTypeBox(selectedEvent));
        }

        RefreshTimelineText();
        RefreshTimelineControls();
        RefreshTimelineData();
    }

    private void SaveLoadedAsset()
    {
        if (!TrySaveLoadedAsset(out string errorMessage) && !string.IsNullOrWhiteSpace(errorMessage))
        {
            SetStatus(errorMessage);
        }
    }

    private void ReloadLoadedAsset()
    {
        if (!ReloadFromDisk())
        {
            SetStatus("Unable to reload Animation2D asset.");
        }
    }

    private void AddEditablePart(int index, Animation2dPartData part)
    {
        AddText($"{EscapeMarkup(part.Id)}", EditorThemePalette.PrimaryHeaderOpacity);
        AddTextBoxRow("Name", part.Name, value => ApplyPartName(part, value));
        AddTextBoxRow("Sprite", part.DefaultSpriteId.ToString("D"), value => ApplyPartSpriteId(part, value));
        AddTextBoxRow("Position X", FormatFloat(part.DefaultPosition.X), value => ApplyPartPositionX(part, value));
        AddTextBoxRow("Position Y", FormatFloat(part.DefaultPosition.Y), value => ApplyPartPositionY(part, value));
        AddTextBoxRow("Rotation", FormatFloat(part.DefaultRotation), value => ApplyPartRotation(part, value));
        AddTextBoxRow("Draw Order", part.DefaultDrawOrder.ToString(CultureInfo.InvariantCulture), value => ApplyPartDrawOrder(part, value));
        AddCheckBoxRow("Visible", part.DefaultVisible, value => ApplyPartVisible(part, value));
        AddText($"  flipX={part.DefaultFlipX} flipY={part.DefaultFlipY} index={index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
    }

    private MGElement CreateAnimationTypeRow()
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, "Type")
        {
            PreferredWidth = 82,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 140,
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
        combo.SetItemsSource(new[] { AnimationType.Once.ToString(), AnimationType.Loop.ToString() });
        combo.SelectedItem = (_animationData?.AnimationType ?? AnimationType.Once).ToString();
        combo.SelectedItemChanged += (_, args) =>
        {
            if (_suppressControlCallbacks
                || string.IsNullOrWhiteSpace(args.NewValue)
                || !Enum.TryParse(args.NewValue, out AnimationType animationType))
            {
                return;
            }

            ApplyAnimationType(animationType);
        };

        row.TryAddChild(combo);
        return row;
    }

    private void ApplyAnimationType(AnimationType animationType)
    {
        ExecuteHistoryMutation("Update animation type", () => ApplyAnimationTypeCore(animationType));
    }

    private void ApplyAnimationTypeCore(AnimationType animationType)
    {
        if (_animationData == null || _animationData.AnimationType == animationType)
        {
            return;
        }

        _animationData.AnimationType = animationType;
        MarkEdited("Updated animation type.");
    }

    private void AddAnimationEvent()
    {
        if (_animationData == null)
        {
            return;
        }

        _animationData.Events.Add(new AnimationEventAsset(0f, "NewEvent"));
        MarkEdited("Added animation event.");
        RefreshInspector();
    }

    private void ApplyPartName(Animation2dPartData part, string value)
    {
        string newName = value.Trim();
        if (part.Name == newName)
        {
            return;
        }

        part.Name = newName;
        MarkEdited("Updated part name.");
    }

    private void ApplyPartSpriteId(Animation2dPartData part, string value)
    {
        if (!Guid.TryParse(value.Trim(), out var spriteId))
        {
            SetStatus("Invalid sprite id GUID.");
            return;
        }

        if (part.DefaultSpriteId == spriteId)
        {
            return;
        }

        part.DefaultSpriteId = spriteId;
        MarkEdited("Updated part sprite id.");
    }

    private void ApplyPartPositionX(Animation2dPartData part, string value)
    {
        if (!TryParseFloat(value, out var x))
        {
            SetStatus("Invalid part X position.");
            return;
        }

        if (part.DefaultPosition.X == x)
        {
            return;
        }

        part.DefaultPosition = new Vector2(x, part.DefaultPosition.Y);
        MarkEdited("Updated part X position.");
    }

    private void ApplyPartPositionY(Animation2dPartData part, string value)
    {
        if (!TryParseFloat(value, out var y))
        {
            SetStatus("Invalid part Y position.");
            return;
        }

        if (part.DefaultPosition.Y == y)
        {
            return;
        }

        part.DefaultPosition = new Vector2(part.DefaultPosition.X, y);
        MarkEdited("Updated part Y position.");
    }

    private void ApplyPartRotation(Animation2dPartData part, string value)
    {
        if (!TryParseFloat(value, out var rotation))
        {
            SetStatus("Invalid part rotation.");
            return;
        }

        if (Math.Abs(part.DefaultRotation - rotation) < 0.0001f)
        {
            return;
        }

        part.DefaultRotation = rotation;
        MarkEdited("Updated part rotation.");
    }

    private void ApplyPartDrawOrder(Animation2dPartData part, string value)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var drawOrder))
        {
            SetStatus("Invalid draw order.");
            return;
        }

        if (part.DefaultDrawOrder == drawOrder)
        {
            return;
        }

        part.DefaultDrawOrder = drawOrder;
        MarkEdited("Updated part draw order.");
    }

    private void ApplyPartVisible(Animation2dPartData part, bool isVisible)
    {
        if (part.DefaultVisible == isVisible)
        {
            return;
        }

        part.DefaultVisible = isVisible;
        MarkEdited("Updated part visibility.");
    }

    private void ApplyEventTime(int eventIndex, string value)
    {
        if (_animationData == null || eventIndex < 0 || eventIndex >= _animationData.Events.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus("Invalid event time.");
            return;
        }

        var animationEvent = _animationData.Events[eventIndex];
        if (animationEvent.TimeSeconds == timeSeconds)
        {
            return;
        }

        _animationData.Events[eventIndex] = animationEvent with { TimeSeconds = timeSeconds };
        SortAnimationEvents(_animationData.Events);
        MarkEdited("Updated event time.");
    }

    private void ApplySelectedEventTime(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus("Invalid event time.");
            return;
        }

        ApplySelectedEventTime(selectedEvent, timeSeconds);
    }

    private void ApplySelectedEventTime(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        ExecuteHistoryMutation("Update timeline item time", () => ApplySelectedEventTimeCore(selectedEvent, timeSeconds));
    }

    private void ApplySelectedEventTimeCore(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null)
        {
            return;
        }

        switch (selectedEvent.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                ApplyPersistedSelectedEventTime(selectedEvent, timeSeconds);
                break;

            case TimelineDisplayEventSourceKind.TrackKeyframe:
                ApplyTrackSelectedEventTime(selectedEvent, timeSeconds);
                break;
        }
    }

    private void ApplyGuidKeyframeTime(List<Animation2dGuidKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus($"Invalid {label} keyframe time.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.TimeSeconds == timeSeconds)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dGuidKeyframeData(timeSeconds, keyframe.Value);
        SortGuidKeyframes(keyframes);
        MarkEdited($"Updated {label} keyframe time.");
        RefreshInspector();
    }

    private void ApplyGuidKeyframeValue(List<Animation2dGuidKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!Guid.TryParse(value.Trim(), out var spriteId))
        {
            SetStatus($"Invalid {label} keyframe GUID.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.Value == spriteId)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dGuidKeyframeData(keyframe.TimeSeconds, spriteId);
        MarkEdited($"Updated {label} keyframe value.");
    }

    private void ApplyVector2KeyframeTime(List<Animation2dVector2KeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus($"Invalid {label} keyframe time.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.TimeSeconds == timeSeconds)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dVector2KeyframeData(timeSeconds, keyframe.Value);
        SortVector2Keyframes(keyframes);
        MarkEdited($"Updated {label} keyframe time.");
        RefreshInspector();
    }

    private void ApplyVector2KeyframeX(List<Animation2dVector2KeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var x))
        {
            SetStatus($"Invalid {label} keyframe X.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.Value.X == x)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dVector2KeyframeData(keyframe.TimeSeconds, new Vector2(x, keyframe.Value.Y));
        MarkEdited($"Updated {label} keyframe X.");
    }

    private void ApplyVector2KeyframeY(List<Animation2dVector2KeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var y))
        {
            SetStatus($"Invalid {label} keyframe Y.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.Value.Y == y)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dVector2KeyframeData(keyframe.TimeSeconds, new Vector2(keyframe.Value.X, y));
        MarkEdited($"Updated {label} keyframe Y.");
    }

    private void ApplyBoolKeyframeTime(List<Animation2dBoolKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus($"Invalid {label} keyframe time.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.TimeSeconds == timeSeconds)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dBoolKeyframeData(timeSeconds, keyframe.Value);
        SortBoolKeyframes(keyframes);
        MarkEdited($"Updated {label} keyframe time.");
        RefreshInspector();
    }

    private void ApplyBoolKeyframeValue(List<Animation2dBoolKeyframeData> keyframes, int keyframeIndex, bool isEnabled, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.Value == isEnabled)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dBoolKeyframeData(keyframe.TimeSeconds, isEnabled);
        MarkEdited($"Updated {label} keyframe value.");
    }

    private void ApplyIntKeyframeTime(List<Animation2dIntKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus($"Invalid {label} keyframe time.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.TimeSeconds == timeSeconds)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dIntKeyframeData(timeSeconds, keyframe.Value);
        SortIntKeyframes(keyframes);
        MarkEdited($"Updated {label} keyframe time.");
        RefreshInspector();
    }

    private void ApplyIntKeyframeValue(List<Animation2dIntKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            SetStatus($"Invalid {label} keyframe value.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (keyframe.Value == intValue)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dIntKeyframeData(keyframe.TimeSeconds, intValue);
        MarkEdited($"Updated {label} keyframe value.");
    }

    private void ApplyFloatKeyframeTime(List<Animation2dFloatKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var timeSeconds) || timeSeconds < 0f)
        {
            SetStatus($"Invalid {label} keyframe time.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (Math.Abs(keyframe.TimeSeconds - timeSeconds) < 0.0001f)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dFloatKeyframeData(timeSeconds, keyframe.Value);
        SortFloatKeyframes(keyframes);
        MarkEdited($"Updated {label} keyframe time.");
        RefreshInspector();
    }

    private void ApplyFloatKeyframeValue(List<Animation2dFloatKeyframeData> keyframes, int keyframeIndex, string value, string label)
    {
        if (keyframeIndex < 0 || keyframeIndex >= keyframes.Count)
        {
            return;
        }

        if (!TryParseFloat(value, out var floatValue))
        {
            SetStatus($"Invalid {label} keyframe value.");
            return;
        }

        var keyframe = keyframes[keyframeIndex];
        if (Math.Abs(keyframe.Value - floatValue) < 0.0001f)
        {
            return;
        }

        keyframes[keyframeIndex] = new Animation2dFloatKeyframeData(keyframe.TimeSeconds, floatValue);
        MarkEdited($"Updated {label} keyframe value.");
    }

    private static void SortGuidKeyframes(List<Animation2dGuidKeyframeData> keyframes)
    {
        keyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private static void SortVector2Keyframes(List<Animation2dVector2KeyframeData> keyframes)
    {
        keyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private static void SortBoolKeyframes(List<Animation2dBoolKeyframeData> keyframes)
    {
        keyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private static void SortIntKeyframes(List<Animation2dIntKeyframeData> keyframes)
    {
        keyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private static void SortFloatKeyframes(List<Animation2dFloatKeyframeData> keyframes)
    {
        keyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private void AddTextBoxRow(string label, string value, Action<string> onChanged)
    {
        _contentStack!.TryAddChild(CreateTextBoxRow(label, value, onChanged));
    }

    private MGElement CreateCommittedTextBoxRow(string label, string value, Action<string> onChanged)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 82,
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

        string lastCommittedValue = value;

        void Commit()
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            string currentValue = textBox.Text ?? string.Empty;
            if (string.Equals(currentValue, lastCommittedValue, StringComparison.Ordinal))
            {
                return;
            }

            lastCommittedValue = currentValue;
            onChanged(currentValue);
        }

        EventHandler<BaseKeyPressedEventArgs> keyPressedHandler = null;
        EventHandler<MGUI.Shared.Helpers.EventArgs<MGElement>> focusChangedHandler = null;
        EventHandler<MGUI.Shared.Helpers.EventArgs<MGElement>> parentChangedHandler = null;

        keyPressedHandler = (_, args) =>
        {
            if (args.IsHandled || args.Key != Microsoft.Xna.Framework.Input.Keys.Enter)
            {
                return;
            }

            Commit();
            args.SetHandledBy(textBox, false);
        };

        focusChangedHandler = (_, args) =>
        {
            if (ReferenceEquals(args.PreviousValue, textBox) && !ReferenceEquals(args.NewValue, textBox))
            {
                Commit();
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
        textBox.SetText(value);
        _suppressControlCallbacks = false;

        row.TryAddChild(textBox);
        return row;
    }

    private void AddCheckBoxRow(string label, bool isChecked, Action<bool> onChanged)
    {
        _contentStack!.TryAddChild(CreateCheckBoxRow(label, isChecked, onChanged));
    }

    private MGElement CreateTextBoxRow(string label, string value, Action<string> onChanged)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 82,
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
        textBox.TextChanged += (_, args) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            onChanged(args.NewValue ?? string.Empty);
        };

        _suppressControlCallbacks = true;
        textBox.SetText(value);
        _suppressControlCallbacks = false;

        row.TryAddChild(textBox);
        return row;
    }

    private MGElement CreateCheckBoxRow(string label, bool isChecked, Action<bool> onChanged)
    {
        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };
        checkBox.SetContent(new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        checkBox.OnCheckStateChanged += (_, args) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            onChanged(args.NewValue ?? false);
        };

        return checkBox;
    }

    private void AddButton(string label, Action onClick)
    {
        _contentStack!.TryAddChild(CreateButton(label, onClick));
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 92,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private void MarkEdited(string message)
    {
        SetDirty(true);
        SetStatus(message);
        RebuildPreviewState();
    }

    private void SetDirty(bool isDirty)
    {
        if (_isDirty == isDirty)
        {
            return;
        }

        _isDirty = isDirty;
        DirtyStateChanged?.Invoke(this);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private MGElement CreateTimelinePanel()
    {
        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        _timelineText = new MGTextBlock(_window, "No animation loaded.")
        {
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };
        stack.TryAddChild(_timelineText);

        _timelineControl = new Animation2dTimelineControl(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinimumPixelsPerSecond = TimelineBasePixelsPerSecond * TimelineMinimumZoomFactor,
            MaximumPixelsPerSecond = TimelineBasePixelsPerSecond * TimelineMaximumZoomFactor,
        };
        _timelineControl.EventSelected += SelectEvent;
        _timelineControl.LaneSelected += SelectLane;
        _timelineControl.LaneLabelEdited += OnTimelineLaneLabelEdited;
        _timelineControl.TrackPropertyInsertRequested += OnTimelineTrackPropertyInsertRequested;
        _timelineControl.TrackRequested += OnTimelineTrackRequested;
        _timelineControl.TrackDeleted += OnTimelineTrackDeleted;
        _timelineControl.EventCopied += OnTimelineEventCopied;
        _timelineControl.EventPasted += OnTimelineEventPasted;
        _timelineControl.PersistedEventInsertRequested += OnTimelinePersistedEventInsertRequested;
        _timelineControl.ScrubRequested += SeekPreviewTime;
        _timelineControl.PixelsPerSecondChanged += OnTimelinePixelsPerSecondChanged;
        _timelineControl.EventTimeEdited += OnTimelineEventTimeEdited;
        _timelineControl.EventDuplicated += OnTimelineEventDuplicated;
        _timelineControl.EventDeleted += OnTimelineEventDeleted;
        _timelineControl.LaneInsertRequested += OnTimelineLaneInsertRequested;

        var surface = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBackground)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        surface.SetContent(_timelineControl);
        stack.TryAddChild(surface);

        var panel = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.ContentBackground)),
            Margin = new Thickness(4, 0, 4, 4),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.SetContent(stack);
        RefreshTimelineControls();
        RefreshTimelineData();
        return panel;
    }

    private void RebuildPreviewState()
    {
        _previewDurationSeconds = 0f;
        _spriteDataById.Clear();
        _spriteIdsToResolve.Clear();

        if (_animationData == null)
        {
            ClearTimelineDisplayLanes();
            ClearTimelineDisplayEvents();
            _selectedLaneIndex = -1;
            _selectedEventIndex = -1;
            _previewTimeSeconds = 0f;
            _previewWorldDriver.Clear();
            _previewViewportPanel?.SetWorldOverride(null);
            _previewEntity = null;
            _previewSpriteComponent = null;
            RefreshTimelineText();
            RefreshTimelineControls();
            RefreshTimelineData();
            return;
        }

        RebuildTimelineDisplayLanes();
        RebuildTimelineDisplayEvents();
        ClampSelectedLaneIndex();
        ClampSelectedEventIndex();

        var composition = Animation2dCompositionAdapter.Create(_animationData);
        _previewDurationSeconds = composition.DurationSeconds;

        var unresolvedSpriteCount = 0;
        Animation2dSpriteReferenceCollector.Collect(_animationData, _spriteIdsToResolve);
        for (var index = 0; index < _spriteIdsToResolve.Count; index++)
        {
            try
            {
                ResolveSprite(_spriteIdsToResolve[index]);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or ArgumentException)
            {
                unresolvedSpriteCount++;
                Logs.WriteException(exception);
            }
        }

        var boundsSampler = new Animation2dCompositionSampler(composition);
        boundsSampler.Reset();
        var previewPosition = Vector3.Zero;
        if (Animation2dBoundsCalculator.TryCalculateLocalBounds(boundsSampler.RuntimeState, _spriteDataById, out var bounds))
        {
            var center = (bounds.Min + bounds.Max) * 0.5f;
            previewPosition = new Vector3(-center.X, -center.Y, 0f);
        }

        RebuildPreviewWorld(previewPosition);
        _previewTimeSeconds = Math.Clamp(_previewTimeSeconds, 0f, Math.Max(0f, _previewDurationSeconds));
        ApplyPreviewPlaybackState();
        SeekPreviewTime(_previewTimeSeconds);
        if (unresolvedSpriteCount > 0)
        {
            SetStatus($"Animation2D preview has {unresolvedSpriteCount.ToString(CultureInfo.InvariantCulture)} unresolved sprite reference(s).");
        }

        RefreshTimelineText();
        RefreshTimelineControls();
        RefreshTimelineData();
    }

    private void RebuildPreviewWorld(Vector3 previewPosition)
    {
        _previewEntity = null;
        _previewSpriteComponent = null;

        if (_animationData == null)
        {
            _previewWorldDriver.Clear();
            _previewViewportPanel?.SetWorldOverride(null);
            return;
        }

        var animationData = _animationData;
        _previewWorldDriver.Rebuild(world =>
        {
            var entity = new Entity
            {
                Name = string.IsNullOrWhiteSpace(animationData.Name) ? "Animation2D Preview" : animationData.Name,
            };

            var spriteComponent = new AnimatedSpriteComponent
            {
                CreatePhysicsForEachFrame = false,
                Position = previewPosition,
            };

            entity.RootComponent = spriteComponent;
            world.AddEntity(entity);

            _previewEntity = entity;
            _previewSpriteComponent = spriteComponent;
        });

        if (_previewSpriteComponent != null)
        {
            _previewSpriteComponent.AddAnimation(new Animation2d(animationData));
            if (_previewSpriteComponent.Animations.Count > 0)
            {
                _previewSpriteComponent.SetCurrentAnimation(0, true);
            }

            _previewSpriteComponent.IsPlaybackPaused = !_isPreviewPlaying;
        }

        _previewWorldDriver.RefreshNow();
        if (_previewViewportPanel != null)
        {
            _previewViewportPanel.SetWorldOverride(_previewWorldDriver.World);
            _previewViewportPanel.FocusEntity(_previewEntity);
        }
    }

    private void ResolveSprite(Guid spriteId)
    {
        if (spriteId == Guid.Empty || _spriteDataById.ContainsKey(spriteId))
        {
            return;
        }

        var spriteData = _editorRuntime.AssetContentManager.Load<SpriteData>(spriteId);
        _spriteDataById.Add(spriteId, spriteData);
    }

    private void RefreshTimelineText()
    {
        if (_timelineText == null)
        {
            return;
        }

        if (_animationData == null)
        {
            _timelineText.Text = "No animation loaded.";
            return;
        }

        _timelineText.Text = $"{FormatSeconds(_previewTimeSeconds)} / {FormatSeconds(_previewDurationSeconds)}  Items: {_timelineDisplayEvents.Count.ToString(CultureInfo.InvariantCulture)}  Lanes: {_timelineDisplayLanes.Count.ToString(CultureInfo.InvariantCulture)}  Zoom {FormatZoomPercentage(_timelineZoomFactor)}";
    }

    private bool IsTimelinePresentationAttached()
    {
        MGElement current = _timelineRoot;
        while (current != null && current.Parent != null)
        {
            current = current.Parent;
        }

        return current is MGWindow;
    }

    private void RefreshTimelineData()
    {
        if (_timelineControl == null)
        {
            return;
        }

        _timelineControl.IsEnabled = _animationData != null;
        _timelineControl.PixelsPerSecond = TimelineBasePixelsPerSecond * _timelineZoomFactor;
        if (_animationData == null)
        {
            _timelineControl.SetTimelineData(null, null, _previewDurationSeconds);
        }
        else
        {
            var laneData = new List<Animation2dTimelineLaneData>(_timelineDisplayLanes.Count);
            var eventData = new List<Animation2dTimelineEventData>(_timelineDisplayEvents.Count);

            for (var laneIndex = 0; laneIndex < _timelineDisplayLanes.Count; laneIndex++)
            {
                TimelineDisplayLane lane = _timelineDisplayLanes[laneIndex];
                laneData.Add(new Animation2dTimelineLaneData(
                    lane.Label,
                    lane.IsEditable,
                    AllowsTrackInsert: true,
                    AllowsTrackDelete: lane.SourceKind == TimelineDisplayLaneSourceKind.TrackKeyframes));
            }

            for (var index = 0; index < _timelineDisplayEvents.Count; index++)
            {
                TimelineDisplayEventItem item = _timelineDisplayEvents[index];
                eventData.Add(new Animation2dTimelineEventData(item.LaneIndex, item.Event.TimeSeconds, item.Event.EventName, item.ValueText));
            }

            _timelineControl.SetTimelineData(laneData, eventData, _previewDurationSeconds);
        }

        _timelineControl.SetPlaybackState(_previewTimeSeconds, _selectedEventIndex, _selectedLaneIndex);
    }

    private void RefreshTimelinePlaybackState()
    {
        _timelineControl?.SetPlaybackState(_previewTimeSeconds, _selectedEventIndex, _selectedLaneIndex);
    }

    private void SelectEvent(int eventIndex)
    {
        if (_animationData == null)
        {
            return;
        }

        if (eventIndex < 0)
        {
            if (_selectedEventIndex < 0)
            {
                return;
            }

            _selectedEventIndex = -1;
            SetStatus(_selectedLaneIndex >= 0 && _selectedLaneIndex < _timelineDisplayLanes.Count
                ? $"Lane {EscapeMarkup(_timelineDisplayLanes[_selectedLaneIndex].Label)} selected."
                : "Timeline selection cleared.");
            RefreshTimelineText();
            RefreshTimelinePlaybackState();
            RefreshInspector();
            return;
        }

        if (eventIndex >= _timelineDisplayEvents.Count)
        {
            return;
        }

        if (_selectedEventIndex == eventIndex)
        {
            return;
        }

        _selectedEventIndex = eventIndex;
        _selectedLaneIndex = _timelineDisplayEvents[eventIndex].LaneIndex;
        SetStatus($"Selected {DescribeEvent(_timelineDisplayEvents[eventIndex].Event)}.");
        RefreshTimelineText();
        RefreshTimelinePlaybackState();
        RefreshInspector();
    }

    private void SelectLane(int laneIndex)
    {
        if (_animationData == null || laneIndex < 0 || laneIndex >= _timelineDisplayLanes.Count)
        {
            return;
        }

        if (_selectedLaneIndex == laneIndex && _selectedEventIndex < 0)
        {
            return;
        }

        _selectedLaneIndex = laneIndex;
        if (_selectedEventIndex >= 0 && _timelineDisplayEvents[_selectedEventIndex].LaneIndex != laneIndex)
        {
            _selectedEventIndex = -1;
        }

        SetStatus($"Selected lane {EscapeMarkup(_timelineDisplayLanes[laneIndex].Label)}.");
        RefreshTimelineText();
        RefreshTimelinePlaybackState();
        RefreshInspector();
    }

    private void ClampSelectedLaneIndex()
    {
        if (_animationData == null || _timelineDisplayLanes.Count == 0)
        {
            _selectedLaneIndex = -1;
            return;
        }

        if (_selectedLaneIndex < 0 || _selectedLaneIndex >= _timelineDisplayLanes.Count)
        {
            _selectedLaneIndex = 0;
        }
    }

    private void ClampSelectedEventIndex()
    {
        if (_animationData == null || _timelineDisplayEvents.Count == 0)
        {
            _selectedEventIndex = -1;
            return;
        }

        if (_selectedEventIndex < 0 || _selectedEventIndex >= _timelineDisplayEvents.Count)
        {
            _selectedEventIndex = -1;
        }
    }

    private bool TryGetSelectedLane(out TimelineDisplayLane lane)
    {
        if (_animationData == null || _selectedLaneIndex < 0 || _selectedLaneIndex >= _timelineDisplayLanes.Count)
        {
            lane = null;
            return false;
        }

        lane = _timelineDisplayLanes[_selectedLaneIndex];
        return true;
    }

    private bool TryGetSelectedEvent(out TimelineDisplayEventItem animationEvent)
    {
        if (_animationData == null || _selectedEventIndex < 0 || _selectedEventIndex >= _timelineDisplayEvents.Count)
        {
            animationEvent = null;
            return false;
        }

        animationEvent = _timelineDisplayEvents[_selectedEventIndex];
        return true;
    }

    private void RebuildTimelineDisplayEvents()
    {
        ClearTimelineDisplayEvents();
        if (_animationData == null)
        {
            return;
        }

        for (var trackIndex = 0; trackIndex < _animationData.Tracks.Count; trackIndex++)
        {
            Animation2dTrackData track = _animationData.Tracks[trackIndex];
            string laneLabel = _timelineDisplayLanes.Count > trackIndex ? _timelineDisplayLanes[trackIndex].Label : _animationData.GetTrackName(trackIndex);
            string trackItemLabel = GetTimelineTrackEventLabel(track.Property);

            switch (track.Property)
            {
                case Animation2dTrackProperty.Sprite:
                    for (var keyframeIndex = 0; keyframeIndex < track.SpriteKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dGuidKeyframeData keyframe = track.SpriteKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel, keyframe.Value),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                            valueText: keyframe.Value.ToString("D")));
                    }
                    break;

                case Animation2dTrackProperty.Position:
                    for (var keyframeIndex = 0; keyframeIndex < track.PositionKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dVector2KeyframeData keyframe = track.PositionKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                            valueText: FormatVector2(keyframe.Value),
                            vector2Value: keyframe.Value));
                    }
                    break;

                case Animation2dTrackProperty.Visible:
                    for (var keyframeIndex = 0; keyframeIndex < track.VisibleKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dBoolKeyframeData keyframe = track.VisibleKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                            valueText: keyframe.Value.ToString(CultureInfo.InvariantCulture),
                            boolValue: keyframe.Value));
                    }
                    break;

                case Animation2dTrackProperty.DrawOrder:
                    for (var keyframeIndex = 0; keyframeIndex < track.DrawOrderKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dIntKeyframeData keyframe = track.DrawOrderKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                            valueText: keyframe.Value.ToString(CultureInfo.InvariantCulture),
                            intValue: keyframe.Value));
                    }
                    break;

                case Animation2dTrackProperty.FlipX:
                case Animation2dTrackProperty.FlipY:
                    for (var keyframeIndex = 0; keyframeIndex < track.FlipKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dBoolKeyframeData keyframe = track.FlipKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                            valueText: keyframe.Value.ToString(CultureInfo.InvariantCulture),
                            boolValue: keyframe.Value));
                    }
                    break;

                case Animation2dTrackProperty.Rotation:
                    for (var keyframeIndex = 0; keyframeIndex < track.RotationKeyframes.Count; keyframeIndex++)
                    {
                        Animation2dFloatKeyframeData keyframe = track.RotationKeyframes[keyframeIndex];
                        _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                            new AnimationEventAsset(keyframe.TimeSeconds, trackItemLabel),
                            TimelineDisplayEventSourceKind.TrackKeyframe,
                            trackIndex,
                            laneLabel,
                            track.Property,
                            trackIndex,
                            keyframeIndex,
                                valueText: FormatFloat(keyframe.Value),
                                floatValue: keyframe.Value));
                    }
                    break;
            }
        }

        int eventsLaneIndex = _timelineDisplayLanes.Count - 1;
        string eventsLaneLabel = eventsLaneIndex >= 0 ? _timelineDisplayLanes[eventsLaneIndex].Label : "Events";
        if (_animationData.Events.Count > 0)
        {
            for (var index = 0; index < _animationData.Events.Count; index++)
            {
                _timelineDisplayEvents.Add(new TimelineDisplayEventItem(
                    _animationData.Events[index],
                    TimelineDisplayEventSourceKind.PersistedEvent,
                    eventsLaneIndex,
                    eventsLaneLabel,
                    eventIndex: index));
            }
        }
    }

    private void ClearTimelineDisplayEvents()
    {
        _timelineDisplayEvents.Clear();
    }

    private void RebuildTimelineDisplayLanes()
    {
        ClearTimelineDisplayLanes();
        if (_animationData == null)
        {
            return;
        }

        for (var trackIndex = 0; trackIndex < _animationData.Tracks.Count; trackIndex++)
        {
            Animation2dTrackData track = _animationData.Tracks[trackIndex];
            _timelineDisplayLanes.Add(new TimelineDisplayLane(_animationData.GetTrackName(trackIndex), TimelineDisplayLaneSourceKind.TrackKeyframes, trackIndex, track.Property));
        }

        _timelineDisplayLanes.Add(new TimelineDisplayLane(_animationData.GetEventTrackName(), TimelineDisplayLaneSourceKind.PersistedEvents));
    }

    private void ClearTimelineDisplayLanes()
    {
        _timelineDisplayLanes.Clear();
    }

    private void OnTimelineEventTimeEdited(int eventIndex, float timeSeconds)
    {
        if (TryGetTimelineDisplayEvent(eventIndex, out var timelineEvent) && timelineEvent != null)
        {
            ApplySelectedEventTime(timelineEvent, timeSeconds);
        }
    }

    private void OnTimelineEventDuplicated(int eventIndex, float timeSeconds)
    {
        if (TryGetTimelineDisplayEvent(eventIndex, out var timelineEvent) && timelineEvent != null)
        {
            ExecuteHistoryMutation("Duplicate timeline item", () => DuplicateTimelineDisplayEvent(timelineEvent, timeSeconds));
        }
    }

    private void OnTimelineEventDeleted(int eventIndex)
    {
        if (TryGetTimelineDisplayEvent(eventIndex, out var timelineEvent) && timelineEvent != null)
        {
            ExecuteHistoryMutation("Delete timeline item", () => DeleteTimelineDisplayEvent(timelineEvent));
        }
    }

    private void OnTimelineLaneInsertRequested(int laneIndex, float timeSeconds)
    {
        ExecuteHistoryMutation("Insert timeline item", () => InsertTimelineItem(laneIndex, timeSeconds));
    }

    private void OnTimelineLaneLabelEdited(int laneIndex, string label)
    {
        ExecuteHistoryMutation("Rename track", () => OnTimelineLaneLabelEditedCore(laneIndex, label));
    }

    private void OnTimelineLaneLabelEditedCore(int laneIndex, string label)
    {
        if (_animationData == null || laneIndex < 0 || laneIndex >= _timelineDisplayLanes.Count)
        {
            return;
        }

        TimelineDisplayLane lane = _timelineDisplayLanes[laneIndex];
        if (lane.SourceKind == TimelineDisplayLaneSourceKind.TrackKeyframes)
        {
            if (lane.TrackIndex < 0 || lane.TrackIndex >= _animationData.Tracks.Count)
            {
                return;
            }

            string normalizedName = string.IsNullOrWhiteSpace(label)
                ? $"track {lane.TrackIndex + 1:00}"
                : label.Trim();
            if (string.Equals(_animationData.Tracks[lane.TrackIndex].Name, normalizedName, StringComparison.Ordinal))
            {
                return;
            }

            _animationData.Tracks[lane.TrackIndex].Name = normalizedName;
            SetDirty(true);
            SetStatus($"Renamed track to {EscapeMarkup(normalizedName)}.");
            RefreshInspector();
            return;
        }

        if (lane.SourceKind == TimelineDisplayLaneSourceKind.PersistedEvents)
        {
            string normalizedName = string.IsNullOrWhiteSpace(label)
                ? $"track {_animationData.Tracks.Count + 1:00}"
                : label.Trim();
            if (string.Equals(_animationData.EventTrackName, normalizedName, StringComparison.Ordinal))
            {
                return;
            }

            _animationData.EventTrackName = normalizedName;
            SetDirty(true);
            SetStatus($"Renamed track to {EscapeMarkup(normalizedName)}.");
            RefreshInspector();
        }
    }

    private void OnTimelineTrackPropertyInsertRequested(Animation2dTrackProperty property, int contextLaneIndex, float timeSeconds)
    {
        ExecuteHistoryMutation("Add track keyframe", () => OnTimelineTrackPropertyInsertRequestedCore(property, contextLaneIndex, timeSeconds));
    }

    private void OnTimelineTrackPropertyInsertRequestedCore(Animation2dTrackProperty property, int contextLaneIndex, float timeSeconds)
    {
        if (_animationData == null)
        {
            return;
        }

        if (!TryResolveInsertionTargetPartId(contextLaneIndex, out string targetPartId))
        {
            SetStatus("Select a track lane to choose the target part for the new keyframe.");
            return;
        }

        int oldTrackCount = _animationData.Tracks.Count;
        int trackIndex = FindTrackIndex(targetPartId, property);
        if (trackIndex < 0)
        {
            trackIndex = _animationData.Tracks.Count;
            _animationData.Tracks.Add(new Animation2dTrackData
            {
                Name = $"track {trackIndex + 1:00}",
                TargetPartId = targetPartId,
                Property = property,
                Interpolation = Animation2dInterpolationMode.Step,
            });

            ShiftDefaultEventTrackNameIfNeeded(oldTrackCount);
        }

        InsertTrackKeyframe(new TimelineDisplayLane(_animationData.GetTrackName(trackIndex), TimelineDisplayLaneSourceKind.TrackKeyframes, trackIndex, property), timeSeconds);
    }

    private void OnTimelinePersistedEventInsertRequested(float timeSeconds)
    {
        ExecuteHistoryMutation("Add custom event", () => InsertPersistedEvent(_timelineDisplayLanes.Count - 1, timeSeconds));
    }

    private void OnTimelineTrackRequested(Animation2dTrackProperty property, int contextLaneIndex)
    {
        ExecuteHistoryMutation("Add track", () => CreateTrack(contextLaneIndex, property));
    }

    private void OnTimelineTrackDeleted(int laneIndex)
    {
        ExecuteHistoryMutation("Delete track", () => DeleteTrack(laneIndex));
    }

    private void OnTimelineEventCopied(int eventIndex)
    {
        if (!TryGetTimelineDisplayEvent(eventIndex, out TimelineDisplayEventItem timelineEvent))
        {
            return;
        }

        _copiedTimelineItem = CreateClipboardItem(timelineEvent);
        SetStatus($"Copied {EscapeMarkup(GetSelectedEventTitle(timelineEvent))}.");
    }

    private void OnTimelineEventPasted(int sourceEventIndex, int laneIndex, float timeSeconds)
    {
        ExecuteHistoryMutation("Paste timeline item", () => OnTimelineEventPastedCore(sourceEventIndex, laneIndex, timeSeconds));
    }

    private void OnTimelineEventPastedCore(int sourceEventIndex, int laneIndex, float timeSeconds)
    {
        if (_animationData == null || laneIndex < 0 || laneIndex >= _timelineDisplayLanes.Count)
        {
            return;
        }

        if (_copiedTimelineItem == null && TryGetTimelineDisplayEvent(sourceEventIndex, out TimelineDisplayEventItem sourceEvent))
        {
            _copiedTimelineItem = CreateClipboardItem(sourceEvent);
        }

        if (_copiedTimelineItem == null)
        {
            SetStatus("Copy a timeline item before pasting.");
            return;
        }

        TimelineDisplayLane lane = _timelineDisplayLanes[laneIndex];
        switch (_copiedTimelineItem.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                if (lane.SourceKind != TimelineDisplayLaneSourceKind.PersistedEvents)
                {
                    SetStatus("Custom events can only be pasted on the event track.");
                    return;
                }

                var pastedEvent = new AnimationEventAsset(timeSeconds, _copiedTimelineItem.EventName)
                {
                    SpriteAssetId = _copiedTimelineItem.SpriteAssetId,
                };
                if (ContainsPersistedEvent(pastedEvent))
                {
                    SetStatus("An identical event already exists at this time.");
                    return;
                }

                _animationData.Events.Add(pastedEvent);
                SortAnimationEvents(_animationData.Events);
                FinalizeTimelineMutation("Pasted event.", new TimelineDisplayEventLocator(
                    TimelineDisplayEventSourceKind.PersistedEvent,
                    null,
                    -1,
                    pastedEvent.EventName,
                    pastedEvent.SpriteAssetId,
                    pastedEvent.TimeSeconds,
                    Vector2.Zero,
                    false,
                    0,
                    0f), laneIndex, true);
                return;

            case TimelineDisplayEventSourceKind.TrackKeyframe:
                if (lane.SourceKind != TimelineDisplayLaneSourceKind.TrackKeyframes || lane.TrackIndex < 0 || lane.TrackProperty == null)
                {
                    SetStatus("Track keyframes can only be pasted on a track lane.");
                    return;
                }

                if (_copiedTimelineItem.TrackProperty != lane.TrackProperty)
                {
                    SetStatus("Copy and paste requires the same track type.");
                    return;
                }

                switch (lane.TrackProperty.Value)
                {
                    case Animation2dTrackProperty.Sprite:
                        UpsertSpriteTrackKeyframe(lane.TrackIndex, timeSeconds, _copiedTimelineItem.SpriteAssetId, lane.TrackProperty.Value, laneIndex, false);
                        return;

                    case Animation2dTrackProperty.Position:
                        UpsertPositionTrackKeyframe(lane.TrackIndex, timeSeconds, _copiedTimelineItem.Vector2Value, lane.TrackProperty.Value, laneIndex, false);
                        return;

                    case Animation2dTrackProperty.Visible:
                    case Animation2dTrackProperty.FlipX:
                    case Animation2dTrackProperty.FlipY:
                        UpsertBoolTrackKeyframe(lane.TrackIndex, timeSeconds, _copiedTimelineItem.BoolValue, lane.TrackProperty.Value, laneIndex, false);
                        return;

                    case Animation2dTrackProperty.DrawOrder:
                        UpsertDrawOrderTrackKeyframe(lane.TrackIndex, timeSeconds, _copiedTimelineItem.IntValue, lane.TrackProperty.Value, laneIndex, false);
                        return;

                    case Animation2dTrackProperty.Rotation:
                        UpsertRotationTrackKeyframe(lane.TrackIndex, timeSeconds, _copiedTimelineItem.FloatValue, lane.TrackProperty.Value, laneIndex, false);
                        return;
                }

                break;
        }
    }

    private static TimelineClipboardItem CreateClipboardItem(TimelineDisplayEventItem timelineEvent)
    {
        return new TimelineClipboardItem
        {
            SourceKind = timelineEvent.SourceKind,
            TrackProperty = timelineEvent.TrackProperty,
            EventName = timelineEvent.Event.EventName,
            SpriteAssetId = timelineEvent.Event.SpriteAssetId,
            Vector2Value = timelineEvent.Vector2Value,
            BoolValue = timelineEvent.BoolValue,
            IntValue = timelineEvent.IntValue,
            FloatValue = timelineEvent.FloatValue,
        };
    }

    private bool TryGetTimelineDisplayEvent(int eventIndex, out TimelineDisplayEventItem timelineEvent)
    {
        if (_animationData == null || eventIndex < 0 || eventIndex >= _timelineDisplayEvents.Count)
        {
            timelineEvent = null;
            return false;
        }

        timelineEvent = _timelineDisplayEvents[eventIndex];
        return true;
    }

    private void InsertTimelineItem(int laneIndex, float timeSeconds)
    {
        if (_animationData == null || laneIndex < 0 || laneIndex >= _timelineDisplayLanes.Count)
        {
            return;
        }

        TimelineDisplayLane lane = _timelineDisplayLanes[laneIndex];
        switch (lane.SourceKind)
        {
            case TimelineDisplayLaneSourceKind.PersistedEvents:
                InsertPersistedEvent(laneIndex, timeSeconds);
                break;

            case TimelineDisplayLaneSourceKind.TrackKeyframes:
                InsertTrackKeyframe(lane, timeSeconds);
                break;
        }
    }

    private void CreateTrack(int contextLaneIndex, Animation2dTrackProperty property)
    {
        if (_animationData == null)
        {
            return;
        }

        if (!TryResolveInsertionTargetPartId(contextLaneIndex, out string targetPartId))
        {
            SetStatus("Select a track lane to choose the target part for the new track.");
            return;
        }

        if (FindTrackIndex(targetPartId, property) >= 0)
        {
            SetStatus($"A {property} track already exists for {targetPartId}.");
            return;
        }

        int oldTrackCount = _animationData.Tracks.Count;
        int trackIndex = _animationData.Tracks.Count;
        _animationData.Tracks.Add(new Animation2dTrackData
        {
            Name = $"track {trackIndex + 1:00}",
            TargetPartId = targetPartId,
            Property = property,
            Interpolation = Animation2dInterpolationMode.Step,
        });

        ShiftDefaultEventTrackNameIfNeeded(oldTrackCount);
        SetDirty(true);
        SetStatus($"Added {property} track for {EscapeMarkup(targetPartId)}.");
        RebuildPreviewState();
        _selectedLaneIndex = Math.Min(trackIndex, _timelineDisplayLanes.Count - 1);
        _selectedEventIndex = -1;
        RefreshInspector();
    }

    private void DeleteTrack(int laneIndex)
    {
        if (_animationData == null || laneIndex < 0 || laneIndex >= _timelineDisplayLanes.Count)
        {
            return;
        }

        TimelineDisplayLane lane = _timelineDisplayLanes[laneIndex];
        if (lane.SourceKind != TimelineDisplayLaneSourceKind.TrackKeyframes || lane.TrackIndex < 0 || lane.TrackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        int oldTrackCount = _animationData.Tracks.Count;
        _animationData.Tracks.RemoveAt(lane.TrackIndex);
        NormalizeTrackNamesAfterStructureChange(oldTrackCount);
        SetDirty(true);
        SetStatus($"Deleted track {EscapeMarkup(lane.Label)}.");
        RebuildPreviewState();
        _selectedLaneIndex = Math.Clamp(laneIndex - 1, 0, Math.Max(0, _timelineDisplayLanes.Count - 1));
        _selectedEventIndex = -1;
        RefreshInspector();
    }

    private void InsertPersistedEvent(int laneIndex, float timeSeconds)
    {
        if (_animationData == null)
        {
            return;
        }

        var newEvent = new AnimationEventAsset(timeSeconds, "NewEvent");
        if (ContainsPersistedEvent(newEvent))
        {
            SetStatus("An identical event already exists at this time.");
            return;
        }

        _animationData.Events.Add(newEvent);
        SortAnimationEvents(_animationData.Events);
        FinalizeTimelineMutation("Added event.", new TimelineDisplayEventLocator(
            TimelineDisplayEventSourceKind.PersistedEvent,
            null,
            -1,
            newEvent.EventName,
            newEvent.SpriteAssetId,
            newEvent.TimeSeconds,
            Vector2.Zero,
            false,
            0,
            0f), laneIndex, true);
    }

    private void InsertTrackKeyframe(TimelineDisplayLane lane, float timeSeconds)
    {
        if (_animationData == null || lane.TrackIndex < 0 || lane.TrackIndex >= _animationData.Tracks.Count || lane.TrackProperty == null)
        {
            return;
        }

        if (!TrySampleTrackPartState(lane.TrackIndex, timeSeconds, out var partState))
        {
            return;
        }

        switch (lane.TrackProperty.Value)
        {
            case Animation2dTrackProperty.Sprite:
                UpsertSpriteTrackKeyframe(lane.TrackIndex, timeSeconds, partState.SpriteId, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.Position:
                UpsertPositionTrackKeyframe(lane.TrackIndex, timeSeconds, partState.Position, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.Visible:
                UpsertBoolTrackKeyframe(lane.TrackIndex, timeSeconds, partState.Visible, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.DrawOrder:
                UpsertDrawOrderTrackKeyframe(lane.TrackIndex, timeSeconds, partState.DrawOrder, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.FlipX:
                UpsertBoolTrackKeyframe(lane.TrackIndex, timeSeconds, partState.FlipX, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.FlipY:
                UpsertBoolTrackKeyframe(lane.TrackIndex, timeSeconds, partState.FlipY, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;

            case Animation2dTrackProperty.Rotation:
                UpsertRotationTrackKeyframe(lane.TrackIndex, timeSeconds, partState.Rotation, lane.TrackProperty.Value, lane.TrackIndex, true);
                break;
        }
    }

    private void DuplicateTimelineDisplayEvent(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null)
        {
            return;
        }

        switch (selectedEvent.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                DuplicatePersistedEvent(selectedEvent, timeSeconds);
                break;

            case TimelineDisplayEventSourceKind.TrackKeyframe:
                DuplicateTrackKeyframe(selectedEvent, timeSeconds);
                break;
        }
    }

    private void DuplicatePersistedEvent(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null || selectedEvent.EventIndex < 0 || selectedEvent.EventIndex >= _animationData.Events.Count)
        {
            return;
        }

        var duplicatedEvent = _animationData.Events[selectedEvent.EventIndex] with { TimeSeconds = timeSeconds };
        if (ContainsPersistedEvent(duplicatedEvent))
        {
            SetStatus("An identical event already exists at this time.");
            return;
        }

        _animationData.Events.Add(duplicatedEvent);
        SortAnimationEvents(_animationData.Events);
        FinalizeTimelineMutation("Duplicated event.", CreateLocator(selectedEvent, duplicatedEvent.TimeSeconds, duplicatedEvent.SpriteAssetId, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue), selectedEvent.LaneIndex, true);
    }

    private void DuplicateTrackKeyframe(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null || selectedEvent.TrackProperty == null)
        {
            return;
        }

        switch (selectedEvent.TrackProperty.Value)
        {
            case Animation2dTrackProperty.Sprite:
                UpsertSpriteTrackKeyframe(selectedEvent.TrackIndex, timeSeconds, selectedEvent.Event.SpriteAssetId, selectedEvent.TrackProperty.Value, selectedEvent.LaneIndex, false);
                break;

            case Animation2dTrackProperty.Position:
                UpsertPositionTrackKeyframe(selectedEvent.TrackIndex, timeSeconds, selectedEvent.Vector2Value, selectedEvent.TrackProperty.Value, selectedEvent.LaneIndex, false);
                break;

            case Animation2dTrackProperty.Visible:
            case Animation2dTrackProperty.FlipX:
            case Animation2dTrackProperty.FlipY:
                UpsertBoolTrackKeyframe(selectedEvent.TrackIndex, timeSeconds, selectedEvent.BoolValue, selectedEvent.TrackProperty.Value, selectedEvent.LaneIndex, false);
                break;

            case Animation2dTrackProperty.DrawOrder:
                UpsertDrawOrderTrackKeyframe(selectedEvent.TrackIndex, timeSeconds, selectedEvent.IntValue, selectedEvent.TrackProperty.Value, selectedEvent.LaneIndex, false);
                break;

            case Animation2dTrackProperty.Rotation:
                UpsertRotationTrackKeyframe(selectedEvent.TrackIndex, timeSeconds, selectedEvent.FloatValue, selectedEvent.TrackProperty.Value, selectedEvent.LaneIndex, false);
                break;
        }
    }

    private void DeleteTimelineDisplayEvent(TimelineDisplayEventItem selectedEvent)
    {
        if (_animationData == null)
        {
            return;
        }

        switch (selectedEvent.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                if (selectedEvent.EventIndex < 0 || selectedEvent.EventIndex >= _animationData.Events.Count)
                {
                    return;
                }

                _animationData.Events.RemoveAt(selectedEvent.EventIndex);
                break;

            case TimelineDisplayEventSourceKind.TrackKeyframe:
                DeleteTrackKeyframe(selectedEvent);
                break;
        }

        FinalizeTimelineMutation("Deleted timeline item.", null, selectedEvent.LaneIndex, false);
    }

    private void DeleteTrackKeyframe(TimelineDisplayEventItem selectedEvent)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0)
        {
            return;
        }

        Animation2dTrackData track = _animationData.Tracks[selectedEvent.TrackIndex];
        switch (selectedEvent.TrackProperty)
        {
            case Animation2dTrackProperty.Sprite when selectedEvent.KeyframeIndex < track.SpriteKeyframes.Count:
                track.SpriteKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;

            case Animation2dTrackProperty.Position when selectedEvent.KeyframeIndex < track.PositionKeyframes.Count:
                track.PositionKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;

            case Animation2dTrackProperty.Visible when selectedEvent.KeyframeIndex < track.VisibleKeyframes.Count:
                track.VisibleKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;

            case Animation2dTrackProperty.DrawOrder when selectedEvent.KeyframeIndex < track.DrawOrderKeyframes.Count:
                track.DrawOrderKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;

            case Animation2dTrackProperty.FlipX when selectedEvent.KeyframeIndex < track.FlipKeyframes.Count:
            case Animation2dTrackProperty.FlipY when selectedEvent.KeyframeIndex < track.FlipKeyframes.Count:
                track.FlipKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;

            case Animation2dTrackProperty.Rotation when selectedEvent.KeyframeIndex < track.RotationKeyframes.Count:
                track.RotationKeyframes.RemoveAt(selectedEvent.KeyframeIndex);
                break;
        }
    }

    private bool TrySampleTrackPartState(int trackIndex, float timeSeconds, out Animation2dPartRuntimeState partState)
    {
        partState = null!;
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return false;
        }

        Animation2dTrackData track = _animationData.Tracks[trackIndex];
        Animation2dCompositionSampler sampler = new(Animation2dCompositionAdapter.Create(_animationData));
        sampler.Seek(timeSeconds);
        if (!sampler.RuntimeState.TryGetPart(track.TargetPartId, out partState))
        {
            SetStatus($"Track target part not found: {track.TargetPartId}");
            return false;
        }

        return true;
    }

    private void UpsertSpriteTrackKeyframe(int trackIndex, float timeSeconds, Guid spriteAssetId, Animation2dTrackProperty property, int laneIndex, bool isInsert)
    {
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[trackIndex].SpriteKeyframes;
        int existingIndex = FindGuidKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && keyframes[existingIndex].Value == spriteAssetId)
        {
            SetStatus("An identical sprite keyframe already exists at this time.");
            return;
        }

        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = new Animation2dGuidKeyframeData(timeSeconds, spriteAssetId);
        }
        else
        {
            keyframes.Add(new Animation2dGuidKeyframeData(timeSeconds, spriteAssetId));
        }

        SortGuidKeyframes(keyframes);
        FinalizeTimelineMutation(existingIndex >= 0 ? "Updated sprite keyframe." : isInsert ? "Added sprite keyframe." : "Duplicated sprite keyframe.", new TimelineDisplayEventLocator(TimelineDisplayEventSourceKind.TrackKeyframe, property, trackIndex, property.ToString(), spriteAssetId, timeSeconds, Vector2.Zero, false, 0, 0f), laneIndex, true);
    }

    private void UpsertPositionTrackKeyframe(int trackIndex, float timeSeconds, Vector2 value, Animation2dTrackProperty property, int laneIndex, bool isInsert)
    {
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[trackIndex].PositionKeyframes;
        int existingIndex = FindVector2KeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && keyframes[existingIndex].Value == value)
        {
            SetStatus("An identical position keyframe already exists at this time.");
            return;
        }

        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = new Animation2dVector2KeyframeData(timeSeconds, value);
        }
        else
        {
            keyframes.Add(new Animation2dVector2KeyframeData(timeSeconds, value));
        }

        SortVector2Keyframes(keyframes);
        FinalizeTimelineMutation(existingIndex >= 0 ? "Updated position keyframe." : isInsert ? "Added position keyframe." : "Duplicated position keyframe.", new TimelineDisplayEventLocator(TimelineDisplayEventSourceKind.TrackKeyframe, property, trackIndex, property.ToString(), Guid.Empty, timeSeconds, value, false, 0, 0f), laneIndex, true);
    }

    private void UpsertBoolTrackKeyframe(int trackIndex, float timeSeconds, bool value, Animation2dTrackProperty property, int laneIndex, bool isInsert)
    {
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        List<Animation2dBoolKeyframeData> keyframes = property == Animation2dTrackProperty.Visible
            ? _animationData.Tracks[trackIndex].VisibleKeyframes
            : _animationData.Tracks[trackIndex].FlipKeyframes;

        int existingIndex = FindBoolKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && keyframes[existingIndex].Value == value)
        {
            SetStatus($"An identical {property} keyframe already exists at this time.");
            return;
        }

        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = new Animation2dBoolKeyframeData(timeSeconds, value);
        }
        else
        {
            keyframes.Add(new Animation2dBoolKeyframeData(timeSeconds, value));
        }

        SortBoolKeyframes(keyframes);
        FinalizeTimelineMutation(existingIndex >= 0 ? $"Updated {property} keyframe." : isInsert ? $"Added {property} keyframe." : $"Duplicated {property} keyframe.", new TimelineDisplayEventLocator(TimelineDisplayEventSourceKind.TrackKeyframe, property, trackIndex, property.ToString(), Guid.Empty, timeSeconds, Vector2.Zero, value, 0, 0f), laneIndex, true);
    }

    private void UpsertDrawOrderTrackKeyframe(int trackIndex, float timeSeconds, int value, Animation2dTrackProperty property, int laneIndex, bool isInsert)
    {
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[trackIndex].DrawOrderKeyframes;
        int existingIndex = FindIntKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && keyframes[existingIndex].Value == value)
        {
            SetStatus("An identical draw order keyframe already exists at this time.");
            return;
        }

        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = new Animation2dIntKeyframeData(timeSeconds, value);
        }
        else
        {
            keyframes.Add(new Animation2dIntKeyframeData(timeSeconds, value));
        }

        SortIntKeyframes(keyframes);
        FinalizeTimelineMutation(existingIndex >= 0 ? "Updated draw order keyframe." : isInsert ? "Added draw order keyframe." : "Duplicated draw order keyframe.", new TimelineDisplayEventLocator(TimelineDisplayEventSourceKind.TrackKeyframe, property, trackIndex, property.ToString(), Guid.Empty, timeSeconds, Vector2.Zero, false, value, 0f), laneIndex, true);
    }

    private void UpsertRotationTrackKeyframe(int trackIndex, float timeSeconds, float value, Animation2dTrackProperty property, int laneIndex, bool isInsert)
    {
        if (_animationData == null || trackIndex < 0 || trackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[trackIndex].RotationKeyframes;
        int existingIndex = FindFloatKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && Math.Abs(keyframes[existingIndex].Value - value) < 0.0001f)
        {
            SetStatus("An identical rotation keyframe already exists at this time.");
            return;
        }

        if (existingIndex >= 0)
        {
            keyframes[existingIndex] = new Animation2dFloatKeyframeData(timeSeconds, value);
        }
        else
        {
            keyframes.Add(new Animation2dFloatKeyframeData(timeSeconds, value));
        }

        SortFloatKeyframes(keyframes);
        FinalizeTimelineMutation(existingIndex >= 0 ? "Updated rotation keyframe." : isInsert ? "Added rotation keyframe." : "Duplicated rotation keyframe.", new TimelineDisplayEventLocator(TimelineDisplayEventSourceKind.TrackKeyframe, property, trackIndex, property.ToString(), Guid.Empty, timeSeconds, Vector2.Zero, false, 0, value), laneIndex, true);
    }

    private void FinalizeTimelineMutation(string message, TimelineDisplayEventLocator? locator, int laneIndex, bool restoreSelection)
    {
        SetDirty(true);
        SetStatus(message);
        _selectedLaneIndex = laneIndex;
        _selectedEventIndex = -1;
        RebuildPreviewState();
        if (restoreSelection && locator.HasValue)
        {
            RestoreSelectedEvent(locator.Value);
        }

        RefreshInspector();
    }

    private bool ContainsPersistedEvent(AnimationEventAsset candidate)
    {
        if (_animationData == null)
        {
            return false;
        }

        for (var index = 0; index < _animationData.Events.Count; index++)
        {
            AnimationEventAsset animationEvent = _animationData.Events[index];
            if (animationEvent.TimeSeconds == candidate.TimeSeconds
                && string.Equals(animationEvent.EventName, candidate.EventName, StringComparison.Ordinal)
                && animationEvent.SpriteAssetId == candidate.SpriteAssetId)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveInsertionTargetPartId(int contextLaneIndex, out string targetPartId)
    {
        targetPartId = string.Empty;
        if (_animationData == null)
        {
            return false;
        }

        if (contextLaneIndex >= 0
            && contextLaneIndex < _timelineDisplayLanes.Count)
        {
            TimelineDisplayLane contextLane = _timelineDisplayLanes[contextLaneIndex];
            if (contextLane.SourceKind == TimelineDisplayLaneSourceKind.TrackKeyframes
                && contextLane.TrackIndex >= 0
                && contextLane.TrackIndex < _animationData.Tracks.Count)
            {
                targetPartId = _animationData.Tracks[contextLane.TrackIndex].TargetPartId;
                return !string.IsNullOrWhiteSpace(targetPartId);
            }
        }

        if (_selectedEventIndex >= 0
            && _selectedEventIndex < _timelineDisplayEvents.Count)
        {
            TimelineDisplayEventItem selectedEvent = _timelineDisplayEvents[_selectedEventIndex];
            if (selectedEvent.SourceKind == TimelineDisplayEventSourceKind.TrackKeyframe
                && selectedEvent.TrackIndex >= 0
                && selectedEvent.TrackIndex < _animationData.Tracks.Count)
            {
                targetPartId = _animationData.Tracks[selectedEvent.TrackIndex].TargetPartId;
                return !string.IsNullOrWhiteSpace(targetPartId);
            }
        }

        if (_selectedLaneIndex >= 0
            && _selectedLaneIndex < _timelineDisplayLanes.Count)
        {
            TimelineDisplayLane selectedLane = _timelineDisplayLanes[_selectedLaneIndex];
            if (selectedLane.SourceKind == TimelineDisplayLaneSourceKind.TrackKeyframes
                && selectedLane.TrackIndex >= 0
                && selectedLane.TrackIndex < _animationData.Tracks.Count)
            {
                targetPartId = _animationData.Tracks[selectedLane.TrackIndex].TargetPartId;
                return !string.IsNullOrWhiteSpace(targetPartId);
            }
        }

        if (_animationData.Parts.Count == 1)
        {
            targetPartId = _animationData.Parts[0].Id;
            return !string.IsNullOrWhiteSpace(targetPartId);
        }

        return false;
    }

    private int FindTrackIndex(string targetPartId, Animation2dTrackProperty property)
    {
        if (_animationData == null)
        {
            return -1;
        }

        for (var index = 0; index < _animationData.Tracks.Count; index++)
        {
            Animation2dTrackData track = _animationData.Tracks[index];
            if (string.Equals(track.TargetPartId, targetPartId, StringComparison.Ordinal)
                && track.Property == property)
            {
                return index;
            }
        }

        return -1;
    }

    private void ShiftDefaultEventTrackNameIfNeeded(int oldTrackCount)
    {
        if (_animationData == null)
        {
            return;
        }

        string previousDefaultName = $"track {oldTrackCount + 1:00}";
        if (!string.Equals(_animationData.EventTrackName, previousDefaultName, StringComparison.Ordinal))
        {
            return;
        }

        _animationData.EventTrackName = $"track {_animationData.Tracks.Count + 1:00}";
    }

    private void NormalizeTrackNamesAfterStructureChange(int oldTrackCount)
    {
        if (_animationData == null)
        {
            return;
        }

        for (var index = 0; index < _animationData.Tracks.Count; index++)
        {
            string previousDefaultName = index < oldTrackCount
                ? $"track {index + 1:00}"
                : string.Empty;

            if (string.IsNullOrWhiteSpace(_animationData.Tracks[index].Name)
                || string.Equals(_animationData.Tracks[index].Name, previousDefaultName, StringComparison.Ordinal)
                || IsDefaultTrackName(_animationData.Tracks[index].Name))
            {
                _animationData.Tracks[index].Name = $"track {index + 1:00}";
            }
        }

        if (string.IsNullOrWhiteSpace(_animationData.EventTrackName)
            || string.Equals(_animationData.EventTrackName, $"track {oldTrackCount + 1:00}", StringComparison.Ordinal)
            || IsDefaultTrackName(_animationData.EventTrackName))
        {
            _animationData.EventTrackName = $"track {_animationData.Tracks.Count + 1:00}";
        }
    }

    private static bool IsDefaultTrackName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("track ", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(name.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static int FindGuidKeyframeIndexAtTime(List<Animation2dGuidKeyframeData> keyframes, float timeSeconds)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Math.Abs(keyframes[index].TimeSeconds - timeSeconds) < TimelineControlMetrics.Epsilon)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindVector2KeyframeIndexAtTime(List<Animation2dVector2KeyframeData> keyframes, float timeSeconds)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Math.Abs(keyframes[index].TimeSeconds - timeSeconds) < TimelineControlMetrics.Epsilon)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindBoolKeyframeIndexAtTime(List<Animation2dBoolKeyframeData> keyframes, float timeSeconds)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Math.Abs(keyframes[index].TimeSeconds - timeSeconds) < TimelineControlMetrics.Epsilon)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindIntKeyframeIndexAtTime(List<Animation2dIntKeyframeData> keyframes, float timeSeconds)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Math.Abs(keyframes[index].TimeSeconds - timeSeconds) < TimelineControlMetrics.Epsilon)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindFloatKeyframeIndexAtTime(List<Animation2dFloatKeyframeData> keyframes, float timeSeconds)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Math.Abs(keyframes[index].TimeSeconds - timeSeconds) < TimelineControlMetrics.Epsilon)
            {
                return index;
            }
        }

        return -1;
    }

    private List<string> BuildValidationMessages()
    {
        var messages = new List<string>();
        if (_animationData == null)
        {
            return messages;
        }

        if (_animationData.Parts.Count == 0)
        {
            messages.Add("Animation has no parts.");
        }

        if (_animationData.Tracks.Count == 0)
        {
            messages.Add("Animation has no tracks.");
        }

        if (!_animationData.AreEventsSortedByTime())
        {
            messages.Add("Events are not sorted by time.");
        }

        foreach (var invalidPartId in _animationData.GetInvalidTrackTargetPartIds())
        {
            messages.Add($"Track targets missing part: {EscapeMarkup(invalidPartId)}");
        }

        return messages;
    }

    private static string DescribeEvent(AnimationEventAsset animationEvent)
    {
        return $"{animationEvent.EventName} @ {FormatSeconds(animationEvent.TimeSeconds)}";
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({FormatFloat(value.X)}, {FormatFloat(value.Y)})";
    }

    private void ApplyPreviewPlaybackState()
    {
        if (_previewSpriteComponent != null)
        {
            _previewSpriteComponent.IsPlaybackPaused = !_isPreviewPlaying;
        }
    }

    private void SeekPreviewTime(float timeSeconds)
    {
        _previewTimeSeconds = Math.Clamp(timeSeconds, 0f, Math.Max(0f, _previewDurationSeconds));
        _previewSpriteComponent?.SeekCurrentAnimation(_previewTimeSeconds);
        RefreshTimelineText();
        RefreshTimelineControls();
        RefreshTimelinePlaybackState();
    }

    private void RefreshTimelineControls()
    {
        _suppressControlCallbacks = true;
        _suppressControlCallbacks = false;
    }

    private void OnTimelinePixelsPerSecondChanged(float pixelsPerSecond)
    {
        float actualZoomFactor = Math.Clamp(pixelsPerSecond / TimelineBasePixelsPerSecond, TimelineMinimumZoomFactor, TimelineMaximumZoomFactor);
        if (Math.Abs(_timelineZoomFactor - actualZoomFactor) < 0.001f)
        {
            return;
        }

        _timelineZoomFactor = actualZoomFactor;
        RefreshTimelineControls();
    }

    private static string FormatZoomPercentage(float zoomFactor)
    {
        int percentage = (int)MathF.Round(zoomFactor * 100f);
        return $"{percentage.ToString(CultureInfo.InvariantCulture)} %";
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Animation2d, _historyContextId);
        return true;
    }

    private void ExecuteHistoryMutation(string description, Action mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(mutation);

        if (_animationData == null)
        {
            return;
        }

        if (_isApplyingHistorySnapshot || !TryGetHistoryContext(out EditorHistoryContext historyContext))
        {
            mutation();
            return;
        }

        AnimationHistorySnapshot before = CaptureHistorySnapshot();
        mutation();
        AnimationHistorySnapshot after = CaptureHistorySnapshot();
        if (HistorySnapshotsEqual(before, after))
        {
            return;
        }

        ApplyHistorySnapshot(before);
        EditorHistoryService.Current.Execute(
            historyContext,
            new EditorDelegateCommand(
                description,
                () => ApplyHistorySnapshot(after),
                () => ApplyHistorySnapshot(before)));

        SetStatus(description);
    }

    private AnimationHistorySnapshot CaptureHistorySnapshot()
    {
        return new AnimationHistorySnapshot
        {
            Document = SerializeAnimationData(_animationData),
            AssetId = _animationData.AssetId,
            FileName = _animationData.FileName ?? string.Empty,
            IsDirty = _isDirty,
            PreviewTimeSeconds = _previewTimeSeconds,
            SelectedLaneIndex = _selectedLaneIndex,
            SelectedEventLocator = TryGetSelectedEventLocator(out TimelineDisplayEventLocator locator) ? locator : null,
        };
    }

    private void ApplyHistorySnapshot(AnimationHistorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _isApplyingHistorySnapshot = true;
        try
        {
            Animation2dData animationData = new();
            animationData.Load((JObject)snapshot.Document.DeepClone());
            animationData.AssetId = snapshot.AssetId;
            animationData.FileName = snapshot.FileName;

            _animationData = animationData;
            _previewTimeSeconds = snapshot.PreviewTimeSeconds;
            _selectedLaneIndex = snapshot.SelectedLaneIndex;
            _selectedEventIndex = -1;

            RebuildPreviewState();
            if (snapshot.SelectedEventLocator.HasValue)
            {
                RestoreSelectedEvent(snapshot.SelectedEventLocator.Value);
            }

            RefreshInspector();
            SetDirty(snapshot.IsDirty);
        }
        finally
        {
            _isApplyingHistorySnapshot = false;
        }
    }

    private bool TryGetSelectedEventLocator(out TimelineDisplayEventLocator locator)
    {
        if (TryGetTimelineDisplayEvent(_selectedEventIndex, out TimelineDisplayEventItem selectedEvent))
        {
            locator = CreateLocator(
                selectedEvent,
                selectedEvent.Event.TimeSeconds,
                selectedEvent.Event.SpriteAssetId,
                selectedEvent.Vector2Value,
                selectedEvent.BoolValue,
                selectedEvent.IntValue,
                selectedEvent.FloatValue);
            return true;
        }

        locator = default;
        return false;
    }

    private static bool HistorySnapshotsEqual(AnimationHistorySnapshot left, AnimationHistorySnapshot right)
    {
        return left.AssetId == right.AssetId
            && string.Equals(left.FileName, right.FileName, StringComparison.Ordinal)
            && left.IsDirty == right.IsDirty
            && Math.Abs(left.PreviewTimeSeconds - right.PreviewTimeSeconds) < 0.0001f
            && left.SelectedLaneIndex == right.SelectedLaneIndex
            && Nullable.Equals(left.SelectedEventLocator, right.SelectedEventLocator)
            && JToken.DeepEquals(left.Document, right.Document);
    }

    private static JObject SerializeAnimationData(Animation2dData animationData)
    {
        animationData.EnsureTrackNames();

        JObject node = new()
        {
            ["id"] = animationData.Id.ToString(),
            ["name"] = animationData.Name,
            ["animation_type"] = animationData.AnimationType.ToString(),
            ["event_track_name"] = animationData.GetEventTrackName(),
        };

        if (animationData.Parts.Count > 0)
        {
            JArray parts = new();
            foreach (Animation2dPartData part in animationData.Parts)
            {
                parts.Add(SerializeAnimationPart(part));
            }

            node.Add("parts", parts);
        }

        if (animationData.Tracks.Count > 0)
        {
            JArray tracks = new();
            for (var trackIndex = 0; trackIndex < animationData.Tracks.Count; trackIndex++)
            {
                tracks.Add(SerializeAnimationTrack(animationData.Tracks[trackIndex], animationData.GetTrackName(trackIndex)));
            }

            node.Add("tracks", tracks);
        }

        JArray events = new();
        foreach (AnimationEventAsset animationEvent in animationData.Events)
        {
            if (string.Equals(animationEvent.EventName, Animation2dEventNames.Restart, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            events.Add(AnimationEventAssetJsonSerializer.Save(animationEvent));
        }

        if (events.Count > 0)
        {
            node.Add("events", events);
        }

        return node;
    }

    private static JObject SerializeAnimationPart(Animation2dPartData part)
    {
        return new JObject
        {
            ["id"] = part.Id,
            ["name"] = part.Name,
            ["default_sprite_id"] = part.DefaultSpriteId,
            ["default_position"] = SerializeVector2(part.DefaultPosition),
            ["default_rotation"] = part.DefaultRotation,
            ["default_draw_order"] = part.DefaultDrawOrder,
            ["default_visible"] = part.DefaultVisible,
            ["default_flip_x"] = part.DefaultFlipX,
            ["default_flip_y"] = part.DefaultFlipY,
        };
    }

    private static JObject SerializeAnimationTrack(Animation2dTrackData track, string trackName)
    {
        JObject node = new()
        {
            ["name"] = trackName,
            ["target_part_id"] = track.TargetPartId,
            ["property"] = track.Property.ToString(),
            ["interpolation"] = track.Interpolation.ToString(),
        };

        AddGuidKeyframes(node, "sprite_keyframes", track.SpriteKeyframes);
        AddVector2Keyframes(node, "position_keyframes", track.PositionKeyframes);
        AddBoolKeyframes(node, "visible_keyframes", track.VisibleKeyframes);
        AddIntKeyframes(node, "draw_order_keyframes", track.DrawOrderKeyframes);
        AddBoolKeyframes(node, "flip_keyframes", track.FlipKeyframes);
        AddFloatKeyframes(node, "rotation_keyframes", track.RotationKeyframes);
        return node;
    }

    private static JObject SerializeVector2(Vector2 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
        };
    }

    private static void AddGuidKeyframes(JObject node, string key, IReadOnlyList<Animation2dGuidKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return;
        }

        JArray keyframeArray = new();
        foreach (Animation2dGuidKeyframeData keyframe in keyframes)
        {
            keyframeArray.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = keyframe.Value,
            });
        }

        node.Add(key, keyframeArray);
    }

    private static void AddVector2Keyframes(JObject node, string key, IReadOnlyList<Animation2dVector2KeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return;
        }

        JArray keyframeArray = new();
        foreach (Animation2dVector2KeyframeData keyframe in keyframes)
        {
            keyframeArray.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = SerializeVector2(keyframe.Value),
            });
        }

        node.Add(key, keyframeArray);
    }

    private static void AddBoolKeyframes(JObject node, string key, IReadOnlyList<Animation2dBoolKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return;
        }

        JArray keyframeArray = new();
        foreach (Animation2dBoolKeyframeData keyframe in keyframes)
        {
            keyframeArray.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = keyframe.Value,
            });
        }

        node.Add(key, keyframeArray);
    }

    private static void AddIntKeyframes(JObject node, string key, IReadOnlyList<Animation2dIntKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return;
        }

        JArray keyframeArray = new();
        foreach (Animation2dIntKeyframeData keyframe in keyframes)
        {
            keyframeArray.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = keyframe.Value,
            });
        }

        node.Add(key, keyframeArray);
    }

    private static void AddFloatKeyframes(JObject node, string key, IReadOnlyList<Animation2dFloatKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            return;
        }

        JArray keyframeArray = new();
        foreach (Animation2dFloatKeyframeData keyframe in keyframes)
        {
            keyframeArray.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = keyframe.Value,
            });
        }

        node.Add(key, keyframeArray);
    }

    private void AddTrack(Animation2dTrackData track)
    {
        AddText($"{EscapeMarkup(track.TargetPartId)}.{track.Property} interpolation={track.Interpolation} keys={GetTrackKeyCount(track).ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.PrimaryHeaderOpacity);
        switch (track.Property)
        {
            case Animation2dTrackProperty.Sprite:
                AddEditableSpriteKeyframes(track.SpriteKeyframes);
                break;

            case Animation2dTrackProperty.Position:
                AddEditablePositionKeyframes(track.PositionKeyframes);
                break;

            case Animation2dTrackProperty.Visible:
                AddEditableBoolKeyframes(track.VisibleKeyframes, "visible");
                break;

            case Animation2dTrackProperty.DrawOrder:
                AddEditableDrawOrderKeyframes(track.DrawOrderKeyframes);
                break;

            case Animation2dTrackProperty.FlipX:
                AddEditableBoolKeyframes(track.FlipKeyframes, "flipX");
                break;

            case Animation2dTrackProperty.FlipY:
                AddEditableBoolKeyframes(track.FlipKeyframes, "flipY");
                break;

            case Animation2dTrackProperty.Rotation:
                AddEditableRotationKeyframes(track.RotationKeyframes);
                break;
        }
    }

    private void AddEditableSpriteKeyframes(List<Animation2dGuidKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            AddText("  No sprite keyframes.", EditorThemePalette.SecondaryTextOpacity);
            return;
        }

        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  Key {index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
            AddTextBoxRow("Time", FormatFloat(keyframe.TimeSeconds), value => ApplyGuidKeyframeTime(keyframes, index, value, "sprite"));
            AddTextBoxRow("Sprite", keyframe.Value.ToString("D"), value => ApplyGuidKeyframeValue(keyframes, index, value, "sprite"));
        }
    }

    private void AddEditablePositionKeyframes(List<Animation2dVector2KeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            AddText("  No position keyframes.", EditorThemePalette.SecondaryTextOpacity);
            return;
        }

        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  Key {index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
            AddTextBoxRow("Time", FormatFloat(keyframe.TimeSeconds), value => ApplyVector2KeyframeTime(keyframes, index, value, "position"));
            AddTextBoxRow("Pos X", FormatFloat(keyframe.Value.X), value => ApplyVector2KeyframeX(keyframes, index, value, "position"));
            AddTextBoxRow("Pos Y", FormatFloat(keyframe.Value.Y), value => ApplyVector2KeyframeY(keyframes, index, value, "position"));
        }
    }

    private void AddEditableBoolKeyframes(List<Animation2dBoolKeyframeData> keyframes, string label)
    {
        if (keyframes.Count == 0)
        {
            AddText($"  No {EscapeMarkup(label)} keyframes.", EditorThemePalette.SecondaryTextOpacity);
            return;
        }

        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  Key {index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
            AddTextBoxRow("Time", FormatFloat(keyframe.TimeSeconds), value => ApplyBoolKeyframeTime(keyframes, index, value, label));
            AddCheckBoxRow("Value", keyframe.Value, value => ApplyBoolKeyframeValue(keyframes, index, value, label));
        }
    }

    private void AddEditableDrawOrderKeyframes(List<Animation2dIntKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            AddText("  No draw order keyframes.", EditorThemePalette.SecondaryTextOpacity);
            return;
        }

        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  Key {index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
            AddTextBoxRow("Time", FormatFloat(keyframe.TimeSeconds), value => ApplyIntKeyframeTime(keyframes, index, value, "draw order"));
            AddTextBoxRow("Value", keyframe.Value.ToString(CultureInfo.InvariantCulture), value => ApplyIntKeyframeValue(keyframes, index, value, "draw order"));
        }
    }

    private void AddEditableRotationKeyframes(List<Animation2dFloatKeyframeData> keyframes)
    {
        if (keyframes.Count == 0)
        {
            AddText("  No rotation keyframes.", EditorThemePalette.SecondaryTextOpacity);
            return;
        }

        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  Key {index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
            AddTextBoxRow("Time", FormatFloat(keyframe.TimeSeconds), value => ApplyFloatKeyframeTime(keyframes, index, value, "rotation"));
            AddTextBoxRow("Value", FormatFloat(keyframe.Value), value => ApplyFloatKeyframeValue(keyframes, index, value, "rotation"));
        }
    }

    private static int GetTrackKeyCount(Animation2dTrackData track)
    {
        return track.Property switch
        {
            Animation2dTrackProperty.Sprite => track.SpriteKeyframes.Count,
            Animation2dTrackProperty.Position => track.PositionKeyframes.Count,
            Animation2dTrackProperty.Visible => track.VisibleKeyframes.Count,
            Animation2dTrackProperty.DrawOrder => track.DrawOrderKeyframes.Count,
            Animation2dTrackProperty.FlipX => track.FlipKeyframes.Count,
            Animation2dTrackProperty.FlipY => track.FlipKeyframes.Count,
            Animation2dTrackProperty.Rotation => track.RotationKeyframes.Count,
            _ => 0,
        };
    }

    private MGElement CreateSelectedEventTypeBox(TimelineDisplayEventItem selectedEvent)
    {
        var content = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        switch (selectedEvent.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                content.TryAddChild(CreateCommittedTextBoxRow("Type", selectedEvent.Event.EventName, value => ApplyPersistedSelectedEventName(selectedEvent, value)));
                if (CanSelectSpriteForEvent(selectedEvent))
                {
                    content.TryAddChild(CreateSpriteSelectorRow(selectedEvent.Event.SpriteAssetId, spriteId => ApplySelectedEventSprite(selectedEvent, spriteId)));
                }

                break;

            case TimelineDisplayEventSourceKind.TrackKeyframe:
                switch (selectedEvent.TrackProperty)
                {
                    case Animation2dTrackProperty.Sprite:
                        content.TryAddChild(CreateSpriteSelectorRow(selectedEvent.Event.SpriteAssetId, spriteId => ApplySelectedEventSprite(selectedEvent, spriteId)));
                        break;

                    case Animation2dTrackProperty.Position:
                        content.TryAddChild(CreateCommittedTextBoxRow("Pos X", FormatFloat(selectedEvent.Vector2Value.X), value => ApplyTrackSelectedEventVector2X(selectedEvent, value)));
                        content.TryAddChild(CreateCommittedTextBoxRow("Pos Y", FormatFloat(selectedEvent.Vector2Value.Y), value => ApplyTrackSelectedEventVector2Y(selectedEvent, value)));
                        break;

                    case Animation2dTrackProperty.Visible:
                    case Animation2dTrackProperty.FlipX:
                    case Animation2dTrackProperty.FlipY:
                        content.TryAddChild(CreateCheckBoxRow("Value", selectedEvent.BoolValue, value => ApplyTrackSelectedEventBoolValue(selectedEvent, value)));
                        break;

                    case Animation2dTrackProperty.DrawOrder:
                        content.TryAddChild(CreateCommittedTextBoxRow("Value", selectedEvent.IntValue.ToString(CultureInfo.InvariantCulture), value => ApplyTrackSelectedEventIntValue(selectedEvent, value)));
                        break;

                    case Animation2dTrackProperty.Rotation:
                        content.TryAddChild(CreateCommittedTextBoxRow("Value", FormatFloat(selectedEvent.FloatValue), value => ApplyTrackSelectedEventFloatValue(selectedEvent, value)));
                        break;
                }

                break;
        }

        var box = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.ContentBackground)),
            Padding = new Thickness(8, 6, 8, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.SetContent(content);

        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        stack.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(GetSelectedEventTitle(selectedEvent))}[/b]")
        {
            WrapText = true,
        });
        stack.TryAddChild(box);
        return stack;
    }

    private MGElement CreateSpriteSelectorRow(Guid selectedSpriteId, Action<Guid> onChanged)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, "Sprite")
        {
            PreferredWidth = 82,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var selector = new AssetSelector(_window)
        {
            AssetId = selectedSpriteId,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Filter = IsSpriteAsset,
        };
        selector.AssetChanged += (_, assetId) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            onChanged(assetId);
        };

        row.TryAddChild(selector);
        return row;
    }

    private void ApplySelectedEventSprite(TimelineDisplayEventItem selectedEvent, Guid spriteAssetId)
    {
        ExecuteHistoryMutation("Update timeline item sprite", () => ApplySelectedEventSpriteCore(selectedEvent, spriteAssetId));
    }

    private void ApplySelectedEventSpriteCore(TimelineDisplayEventItem selectedEvent, Guid spriteAssetId)
    {
        if (_animationData == null)
        {
            return;
        }

        switch (selectedEvent.SourceKind)
        {
            case TimelineDisplayEventSourceKind.PersistedEvent:
                ApplyPersistedSelectedEventSprite(selectedEvent, spriteAssetId);
                break;

            case TimelineDisplayEventSourceKind.TrackKeyframe when selectedEvent.TrackProperty == Animation2dTrackProperty.Sprite:
                ApplySpriteTrackSelectedEventSprite(selectedEvent, spriteAssetId);
                break;
        }
    }

    private void ApplyPersistedSelectedEventName(TimelineDisplayEventItem selectedEvent, string value)
    {
        ExecuteHistoryMutation("Update event type", () => ApplyPersistedSelectedEventNameCore(selectedEvent, value));
    }

    private void ApplyPersistedSelectedEventNameCore(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (_animationData == null || selectedEvent.EventIndex < 0 || selectedEvent.EventIndex >= _animationData.Events.Count)
        {
            return;
        }

        string eventName = value.Trim();
        if (string.IsNullOrWhiteSpace(eventName))
        {
            SetStatus("Invalid event type.");
            return;
        }

        var animationEvent = _animationData.Events[selectedEvent.EventIndex];
        if (string.Equals(animationEvent.EventName, eventName, StringComparison.Ordinal))
        {
            return;
        }

        var updatedEvent = animationEvent with { EventName = eventName };
        _animationData.Events[selectedEvent.EventIndex] = updatedEvent;
        FinalizeSelectedEventEdit("Updated event type.", CreateLocator(selectedEvent, updatedEvent.TimeSeconds, updatedEvent.SpriteAssetId, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue, updatedEvent.EventName));
    }

    private void ApplyPersistedSelectedEventTime(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null || selectedEvent.EventIndex < 0 || selectedEvent.EventIndex >= _animationData.Events.Count)
        {
            return;
        }

        var animationEvent = _animationData.Events[selectedEvent.EventIndex];
        if (animationEvent.TimeSeconds == timeSeconds)
        {
            return;
        }

        var updatedEvent = animationEvent with { TimeSeconds = timeSeconds };
        _animationData.Events[selectedEvent.EventIndex] = updatedEvent;
        SortAnimationEvents(_animationData.Events);
        FinalizeSelectedEventEdit("Updated event time.", CreateLocator(selectedEvent, updatedEvent.TimeSeconds, updatedEvent.SpriteAssetId, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue));
    }

    private void ApplyPersistedSelectedEventSprite(TimelineDisplayEventItem selectedEvent, Guid spriteAssetId)
    {
        if (_animationData == null || selectedEvent.EventIndex < 0 || selectedEvent.EventIndex >= _animationData.Events.Count)
        {
            return;
        }

        var animationEvent = _animationData.Events[selectedEvent.EventIndex];
        if (animationEvent.SpriteAssetId == spriteAssetId)
        {
            return;
        }

        var updatedEvent = animationEvent with { SpriteAssetId = spriteAssetId };
        _animationData.Events[selectedEvent.EventIndex] = updatedEvent;
        FinalizeSelectedEventEdit("Updated event sprite.", CreateLocator(selectedEvent, updatedEvent.TimeSeconds, updatedEvent.SpriteAssetId, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue));
    }

    private void ApplyTrackSelectedEventTime(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.TrackProperty == null)
        {
            return;
        }

        Animation2dTrackData track = _animationData.Tracks[selectedEvent.TrackIndex];
        switch (selectedEvent.TrackProperty.Value)
        {
            case Animation2dTrackProperty.Sprite:
                ApplySpriteTrackSelectedEventTime(selectedEvent, timeSeconds);
                break;

            case Animation2dTrackProperty.Position:
                if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= track.PositionKeyframes.Count)
                {
                    return;
                }

                MovePositionKeyframe(track.PositionKeyframes, selectedEvent.KeyframeIndex, timeSeconds, selectedEvent.Vector2Value);
                FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, Guid.Empty, selectedEvent.Vector2Value, false, 0, 0f));
                break;

            case Animation2dTrackProperty.Visible:
                if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= track.VisibleKeyframes.Count)
                {
                    return;
                }

                MoveBoolKeyframe(track.VisibleKeyframes, selectedEvent.KeyframeIndex, timeSeconds, selectedEvent.BoolValue);
                FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, Guid.Empty, Vector2.Zero, selectedEvent.BoolValue, 0, 0f));
                break;

            case Animation2dTrackProperty.DrawOrder:
                if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= track.DrawOrderKeyframes.Count)
                {
                    return;
                }

                MoveIntKeyframe(track.DrawOrderKeyframes, selectedEvent.KeyframeIndex, timeSeconds, selectedEvent.IntValue);
                FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, Guid.Empty, Vector2.Zero, false, selectedEvent.IntValue, 0f));
                break;

            case Animation2dTrackProperty.FlipX:
            case Animation2dTrackProperty.FlipY:
                if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= track.FlipKeyframes.Count)
                {
                    return;
                }

                MoveBoolKeyframe(track.FlipKeyframes, selectedEvent.KeyframeIndex, timeSeconds, selectedEvent.BoolValue);
                FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, Guid.Empty, Vector2.Zero, selectedEvent.BoolValue, 0, 0f));
                break;

            case Animation2dTrackProperty.Rotation:
                if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= track.RotationKeyframes.Count)
                {
                    return;
                }

                MoveFloatKeyframe(track.RotationKeyframes, selectedEvent.KeyframeIndex, timeSeconds, selectedEvent.FloatValue);
                FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, Guid.Empty, Vector2.Zero, false, 0, selectedEvent.FloatValue));
                break;
        }
    }

    private void ApplySpriteTrackSelectedEventTime(TimelineDisplayEventItem selectedEvent, float timeSeconds)
    {
        if (_animationData == null
            || selectedEvent.TrackIndex < 0
            || selectedEvent.TrackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].SpriteKeyframes;
        if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        var keyframe = keyframes[selectedEvent.KeyframeIndex];
        if (keyframe.TimeSeconds == timeSeconds)
        {
            return;
        }

        keyframes[selectedEvent.KeyframeIndex] = new Animation2dGuidKeyframeData(timeSeconds, keyframe.Value);
        SortGuidKeyframes(keyframes);
        FinalizeSelectedEventEdit("Updated keyframe time.", CreateLocator(selectedEvent, timeSeconds, keyframe.Value, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue));
    }

    private void ApplySpriteTrackSelectedEventSprite(TimelineDisplayEventItem selectedEvent, Guid spriteAssetId)
    {
        if (_animationData == null
            || selectedEvent.TrackIndex < 0
            || selectedEvent.TrackIndex >= _animationData.Tracks.Count)
        {
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].SpriteKeyframes;
        if (selectedEvent.KeyframeIndex < 0 || selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        var keyframe = keyframes[selectedEvent.KeyframeIndex];
        if (keyframe.Value == spriteAssetId)
        {
            return;
        }

        keyframes[selectedEvent.KeyframeIndex] = new Animation2dGuidKeyframeData(keyframe.TimeSeconds, spriteAssetId);
        FinalizeSelectedEventEdit("Updated keyframe sprite.", CreateLocator(selectedEvent, keyframe.TimeSeconds, spriteAssetId, selectedEvent.Vector2Value, selectedEvent.BoolValue, selectedEvent.IntValue, selectedEvent.FloatValue));
    }

    private void ApplyTrackSelectedEventVector2X(TimelineDisplayEventItem selectedEvent, string value)
    {
        ExecuteHistoryMutation("Update position X", () => ApplyTrackSelectedEventVector2XCore(selectedEvent, value));
    }

    private void ApplyTrackSelectedEventVector2XCore(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0)
        {
            return;
        }

        if (!TryParseFloat(value, out float x))
        {
            SetStatus("Invalid position X.");
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].PositionKeyframes;
        if (selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        Vector2 newValue = new(x, keyframes[selectedEvent.KeyframeIndex].Value.Y);
        keyframes[selectedEvent.KeyframeIndex] = new Animation2dVector2KeyframeData(keyframes[selectedEvent.KeyframeIndex].TimeSeconds, newValue);
        FinalizeSelectedEventEdit("Updated position keyframe X.", CreateLocator(selectedEvent, keyframes[selectedEvent.KeyframeIndex].TimeSeconds, Guid.Empty, newValue, false, 0, 0f));
    }

    private void ApplyTrackSelectedEventVector2Y(TimelineDisplayEventItem selectedEvent, string value)
    {
        ExecuteHistoryMutation("Update position Y", () => ApplyTrackSelectedEventVector2YCore(selectedEvent, value));
    }

    private void ApplyTrackSelectedEventVector2YCore(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0)
        {
            return;
        }

        if (!TryParseFloat(value, out float y))
        {
            SetStatus("Invalid position Y.");
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].PositionKeyframes;
        if (selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        Vector2 newValue = new(keyframes[selectedEvent.KeyframeIndex].Value.X, y);
        keyframes[selectedEvent.KeyframeIndex] = new Animation2dVector2KeyframeData(keyframes[selectedEvent.KeyframeIndex].TimeSeconds, newValue);
        FinalizeSelectedEventEdit("Updated position keyframe Y.", CreateLocator(selectedEvent, keyframes[selectedEvent.KeyframeIndex].TimeSeconds, Guid.Empty, newValue, false, 0, 0f));
    }

    private void ApplyTrackSelectedEventBoolValue(TimelineDisplayEventItem selectedEvent, bool value)
    {
        ExecuteHistoryMutation($"Update {selectedEvent.TrackProperty} value", () => ApplyTrackSelectedEventBoolValueCore(selectedEvent, value));
    }

    private void ApplyTrackSelectedEventBoolValueCore(TimelineDisplayEventItem selectedEvent, bool value)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0 || selectedEvent.TrackProperty == null)
        {
            return;
        }

        List<Animation2dBoolKeyframeData> keyframes = selectedEvent.TrackProperty == Animation2dTrackProperty.Visible
            ? _animationData.Tracks[selectedEvent.TrackIndex].VisibleKeyframes
            : _animationData.Tracks[selectedEvent.TrackIndex].FlipKeyframes;
        if (selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        keyframes[selectedEvent.KeyframeIndex] = new Animation2dBoolKeyframeData(keyframes[selectedEvent.KeyframeIndex].TimeSeconds, value);
        FinalizeSelectedEventEdit($"Updated {selectedEvent.TrackProperty} keyframe value.", CreateLocator(selectedEvent, keyframes[selectedEvent.KeyframeIndex].TimeSeconds, Guid.Empty, Vector2.Zero, value, 0, 0f));
    }

    private void ApplyTrackSelectedEventIntValue(TimelineDisplayEventItem selectedEvent, string value)
    {
        ExecuteHistoryMutation("Update draw order", () => ApplyTrackSelectedEventIntValueCore(selectedEvent, value));
    }

    private void ApplyTrackSelectedEventIntValueCore(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0)
        {
            return;
        }

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int drawOrder))
        {
            SetStatus("Invalid draw order.");
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].DrawOrderKeyframes;
        if (selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        keyframes[selectedEvent.KeyframeIndex] = new Animation2dIntKeyframeData(keyframes[selectedEvent.KeyframeIndex].TimeSeconds, drawOrder);
        FinalizeSelectedEventEdit("Updated draw order keyframe value.", CreateLocator(selectedEvent, keyframes[selectedEvent.KeyframeIndex].TimeSeconds, Guid.Empty, Vector2.Zero, false, drawOrder, 0f));
    }

    private void ApplyTrackSelectedEventFloatValue(TimelineDisplayEventItem selectedEvent, string value)
    {
        ExecuteHistoryMutation("Update rotation", () => ApplyTrackSelectedEventFloatValueCore(selectedEvent, value));
    }

    private void ApplyTrackSelectedEventFloatValueCore(TimelineDisplayEventItem selectedEvent, string value)
    {
        if (_animationData == null || selectedEvent.TrackIndex < 0 || selectedEvent.TrackIndex >= _animationData.Tracks.Count || selectedEvent.KeyframeIndex < 0)
        {
            return;
        }

        if (!TryParseFloat(value, out float floatValue))
        {
            SetStatus("Invalid rotation.");
            return;
        }

        var keyframes = _animationData.Tracks[selectedEvent.TrackIndex].RotationKeyframes;
        if (selectedEvent.KeyframeIndex >= keyframes.Count)
        {
            return;
        }

        keyframes[selectedEvent.KeyframeIndex] = new Animation2dFloatKeyframeData(keyframes[selectedEvent.KeyframeIndex].TimeSeconds, floatValue);
        FinalizeSelectedEventEdit("Updated rotation keyframe value.", CreateLocator(selectedEvent, keyframes[selectedEvent.KeyframeIndex].TimeSeconds, Guid.Empty, Vector2.Zero, false, 0, floatValue));
    }

    private void FinalizeSelectedEventEdit(string message, TimelineDisplayEventLocator locator)
    {
        SetDirty(true);
        SetStatus(message);
        RebuildPreviewState();
        RestoreSelectedEvent(locator);
        RefreshInspector();
    }

    private void RestoreSelectedEvent(TimelineDisplayEventLocator locator)
    {
        int selectedIndex = FindDisplayEventIndex(locator);
        if (selectedIndex >= 0)
        {
            _selectedEventIndex = selectedIndex;
            _selectedLaneIndex = _timelineDisplayEvents[selectedIndex].LaneIndex;
        }
    }

    private int FindDisplayEventIndex(TimelineDisplayEventLocator locator)
    {
        for (var index = 0; index < _timelineDisplayEvents.Count; index++)
        {
            var candidate = _timelineDisplayEvents[index];
            if (candidate.SourceKind != locator.SourceKind)
            {
                continue;
            }

            if (locator.SourceKind == TimelineDisplayEventSourceKind.TrackKeyframe && (candidate.TrackIndex != locator.TrackIndex || candidate.TrackProperty != locator.TrackProperty))
            {
                continue;
            }

            if (!string.Equals(candidate.Event.EventName, locator.EventName, StringComparison.Ordinal)
                || candidate.Event.TimeSeconds != locator.TimeSeconds)
            {
                continue;
            }

            if (candidate.SourceKind == TimelineDisplayEventSourceKind.PersistedEvent && candidate.Event.SpriteAssetId != locator.SpriteAssetId)
            {
                continue;
            }

            if (candidate.SourceKind == TimelineDisplayEventSourceKind.TrackKeyframe)
            {
                switch (candidate.TrackProperty)
                {
                    case Animation2dTrackProperty.Sprite when candidate.Event.SpriteAssetId != locator.SpriteAssetId:
                        continue;

                    case Animation2dTrackProperty.Position when candidate.Vector2Value != locator.Vector2Value:
                        continue;

                    case Animation2dTrackProperty.Visible:
                    case Animation2dTrackProperty.FlipX:
                    case Animation2dTrackProperty.FlipY:
                        if (candidate.BoolValue != locator.BoolValue)
                        {
                            continue;
                        }

                        break;

                    case Animation2dTrackProperty.DrawOrder when candidate.IntValue != locator.IntValue:
                        continue;

                    case Animation2dTrackProperty.Rotation when Math.Abs(candidate.FloatValue - locator.FloatValue) > 0.0001f:
                        continue;
                }
            }

            return index;
        }

        return -1;
    }

    private static TimelineDisplayEventLocator CreateLocator(TimelineDisplayEventItem selectedEvent, float timeSeconds, Guid spriteAssetId, Vector2 vector2Value, bool boolValue, int intValue, float floatValue, string eventName = null)
    {
        return new TimelineDisplayEventLocator(
            selectedEvent.SourceKind,
            selectedEvent.TrackProperty,
            selectedEvent.TrackIndex,
            eventName ?? selectedEvent.Event.EventName,
            spriteAssetId,
            timeSeconds,
            vector2Value,
            boolValue,
            intValue,
            floatValue);
    }

    private static void SortAnimationEvents(List<AnimationEventAsset> events)
    {
        events.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private static bool CanSelectSpriteForEvent(TimelineDisplayEventItem selectedEvent)
    {
        return selectedEvent.SourceKind == TimelineDisplayEventSourceKind.TrackKeyframe && selectedEvent.TrackProperty == Animation2dTrackProperty.Sprite
            || string.Equals(selectedEvent.Event.EventName, Animation2dEventNames.ChangeSprite, StringComparison.Ordinal)
            || string.Equals(selectedEvent.Event.EventName, "attachment", StringComparison.Ordinal);
    }

    private static string GetSelectedEventTitle(TimelineDisplayEventItem selectedEvent)
    {
        string typeLabel = selectedEvent.SourceKind == TimelineDisplayEventSourceKind.PersistedEvent
            ? selectedEvent.Event.EventName
            : selectedEvent.TrackProperty?.ToString() ?? selectedEvent.Event.EventName;
        return $"{selectedEvent.LaneLabel} / {typeLabel}";
    }

    private static void MovePositionKeyframe(List<Animation2dVector2KeyframeData> keyframes, int keyframeIndex, float timeSeconds, Vector2 value)
    {
        int existingIndex = FindVector2KeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && existingIndex != keyframeIndex)
        {
            keyframes[existingIndex] = new Animation2dVector2KeyframeData(timeSeconds, value);
            keyframes.RemoveAt(keyframeIndex > existingIndex ? keyframeIndex : keyframeIndex + 1);
        }
        else
        {
            keyframes[keyframeIndex] = new Animation2dVector2KeyframeData(timeSeconds, value);
        }

        SortVector2Keyframes(keyframes);
    }

    private static void MoveBoolKeyframe(List<Animation2dBoolKeyframeData> keyframes, int keyframeIndex, float timeSeconds, bool value)
    {
        int existingIndex = FindBoolKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && existingIndex != keyframeIndex)
        {
            keyframes[existingIndex] = new Animation2dBoolKeyframeData(timeSeconds, value);
            keyframes.RemoveAt(keyframeIndex > existingIndex ? keyframeIndex : keyframeIndex + 1);
        }
        else
        {
            keyframes[keyframeIndex] = new Animation2dBoolKeyframeData(timeSeconds, value);
        }

        SortBoolKeyframes(keyframes);
    }

    private static void MoveIntKeyframe(List<Animation2dIntKeyframeData> keyframes, int keyframeIndex, float timeSeconds, int value)
    {
        int existingIndex = FindIntKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && existingIndex != keyframeIndex)
        {
            keyframes[existingIndex] = new Animation2dIntKeyframeData(timeSeconds, value);
            keyframes.RemoveAt(keyframeIndex > existingIndex ? keyframeIndex : keyframeIndex + 1);
        }
        else
        {
            keyframes[keyframeIndex] = new Animation2dIntKeyframeData(timeSeconds, value);
        }

        SortIntKeyframes(keyframes);
    }

    private static void MoveFloatKeyframe(List<Animation2dFloatKeyframeData> keyframes, int keyframeIndex, float timeSeconds, float value)
    {
        int existingIndex = FindFloatKeyframeIndexAtTime(keyframes, timeSeconds);
        if (existingIndex >= 0 && existingIndex != keyframeIndex)
        {
            keyframes[existingIndex] = new Animation2dFloatKeyframeData(timeSeconds, value);
            keyframes.RemoveAt(keyframeIndex > existingIndex ? keyframeIndex : keyframeIndex + 1);
        }
        else
        {
            keyframes[keyframeIndex] = new Animation2dFloatKeyframeData(timeSeconds, value);
        }

        SortFloatKeyframes(keyframes);
    }

    private static string GetTimelineTrackEventLabel(Animation2dTrackProperty property)
    {
        return property switch
        {
            Animation2dTrackProperty.Sprite => "attachment",
            Animation2dTrackProperty.Position => "localPositionOffset",
            Animation2dTrackProperty.Visible => "visible",
            Animation2dTrackProperty.FlipX => "flipX",
            Animation2dTrackProperty.FlipY => "flipY",
            Animation2dTrackProperty.Rotation => "rotation",
            Animation2dTrackProperty.DrawOrder => "drawOrder",
            _ => property.ToString(),
        };
    }

    private static bool IsSpriteAsset(AssetInfo assetInfo)
    {
        return string.Equals(assetInfo.AssetType, "sprite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(assetInfo.FileName), Constants.FileNameExtensions.Sprite, StringComparison.OrdinalIgnoreCase);
    }

    private void AddSection(string title)
    {
        _contentStack!.TryAddChild(new MGTextBlock(_window, $"[b]{EscapeMarkup(title)}[/b]")
        {
            Margin = new Thickness(0, 8, 0, 0),
            WrapText = true,
        });
    }

    private void AddProperty(string name, string value)
    {
        AddText($"{EscapeMarkup(name)}: {EscapeMarkup(value)}", EditorThemePalette.PrimaryHeaderOpacity);
    }

    private void AddText(string text, float opacity)
    {
        _contentStack!.TryAddChild(new MGTextBlock(_window, text)
        {
            Opacity = opacity,
            WrapText = true,
        });
    }

    private static string FormatSeconds(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "s";

    private static string FormatFloat(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string EscapeMarkup(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("[", "[[]", StringComparison.Ordinal).Replace("]", "[]]", StringComparison.Ordinal);
    }
}