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
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration;
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
    private readonly List<AnimationEventAsset> _timelineDisplayEvents = new();

    private MGDockPanel? _root;
    private MGDockPanel? _inspectorRoot;
    private MGDockPanel? _timelineRoot;
    private WorldViewportPanel? _previewViewportPanel;
    private Entity? _previewEntity;
    private AnimatedSpriteComponent? _previewSpriteComponent;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGTextBlock? _timelineText;
    private Animation2dTimelineControl? _timelineControl;
    private MGStackPanel? _contentStack;

    private Animation2dData? _animationData;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private float _previewDurationSeconds;
    private float _previewTimeSeconds;
    private float _timelineZoomFactor = 1f;
    private int _selectedEventIndex = -1;
    private bool _isDirty;
    private bool _isPreviewPlaying = true;
    private bool _suppressControlCallbacks;
    private bool _disposed;

    private const float TimelineMinimumZoomFactor = 0.5f;
    private const float TimelineMaximumZoomFactor = 3.0f;
    private const float TimelineBasePixelsPerSecond = 96f;

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

    public Animation2dData? LoadedAnimationData => _animationData;

    public string? LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    public event Action<Animation2dAssetInspectorPanel>? DirtyStateChanged;

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

        _headerText = new MGTextBlock(_window, "[b]Animation2D Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No animation asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = EditorThemePalette.SecondaryTextOpacity,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .anim2d asset from the Content Browser.")
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

        var inspectorBorder = new MGBorder(
            _window,
            new Thickness(1),
            new MGUniformBorderBrush(new MGSolidFillBrush(EditorThemePalette.PreviewSurfaceBorder)))
        {
            BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(EditorThemePalette.ContentBackground)),
            Margin = new Thickness(4, 0, 6, 4),
            Padding = new Thickness(0),
            MinWidth = 280,
            PreferredWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        inspectorBorder.SetContent(scrollViewer);

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
        };
        toolbar.TryAddChild(CreateButton("Save", SaveLoadedAsset));
        toolbar.TryAddChild(CreateButton("Reload", ReloadLoadedAsset));

        var headerStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
        };
        headerStack.TryAddChild(_headerText);
        headerStack.TryAddChild(_sourceText);
        headerStack.TryAddChild(toolbar);
        headerStack.TryAddChild(_statusText);

        _inspectorRoot = new MGDockPanel(_window)
        {
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _inspectorRoot.TryAddChild(headerStack, Dock.Top);
        _inspectorRoot.TryAddChild(inspectorBorder, Dock.Top);

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

    public bool TrySaveLoadedAsset(out string? errorMessage)
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
        if (_headerText == null || _sourceText == null || _statusText == null || _contentStack == null)
        {
            return;
        }

        _contentStack.TryRemoveAll();
        if (_animationData == null)
        {
            ClearTimelineDisplayEvents();
            _selectedEventIndex = -1;
            _headerText.Text = "[b]Animation2D Inspector[/b]";
            _sourceText.Text = "No animation asset loaded.";
            _statusText.Text = "Open a .anim2d asset from the Content Browser.";
            RefreshTimelineText();
            RefreshTimelineControls();
            RefreshTimelineData();
            return;
        }

        RebuildTimelineDisplayEvents();
        ClampSelectedEventIndex();

        _headerText.Text = $"[b]{EscapeMarkup(_animationData.Name)}[/b]";
        _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "No source path."
            : EscapeMarkup(_loadedRelativePath);
        _statusText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "Animation2D asset."
            : IsDirty ? $"Modified {EscapeMarkup(_loadedRelativePath)}" : $"Asset: {EscapeMarkup(_loadedRelativePath)}";

        AddProperty("Type", _animationData.AnimationType.ToString());
        AddProperty("Parts", _animationData.Parts.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Tracks", _animationData.Tracks.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Events", _animationData.Events.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Duration", FormatSeconds(_previewDurationSeconds));

        AddSection("Composition");
        AddText("Timeline is read-only.", EditorThemePalette.SecondaryTextOpacity);

        AddSection("Validation");
        var validationMessages = BuildValidationMessages();
        if (validationMessages.Count == 0)
        {
            AddText("No validation warnings.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < validationMessages.Count; index++)
            {
                AddText(validationMessages[index], EditorThemePalette.PrimaryHeaderOpacity);
            }
        }

        AddSection("Selected Event");
        if (!TryGetSelectedEvent(out var selectedEvent))
        {
            AddText(_timelineDisplayEvents.Count == 0
                ? "No animation events."
                : "Select an event from the timeline to inspect it.",
                EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            AddProperty("Index", (_selectedEventIndex + 1).ToString(CultureInfo.InvariantCulture));
            AddProperty("Time", FormatSeconds(selectedEvent.TimeSeconds));
            AddProperty("Type", selectedEvent.EventName);
            if (selectedEvent.SpriteAssetId != Guid.Empty)
            {
                AddProperty("Sprite", selectedEvent.SpriteAssetId.ToString("D"));
            }
        }

        RefreshTimelineText();
        RefreshTimelineControls();
        RefreshTimelineData();
    }

    private void SaveLoadedAsset()
    {
        if (!TrySaveLoadedAsset(out string? errorMessage) && !string.IsNullOrWhiteSpace(errorMessage))
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
        AddTextBoxRow("Draw Order", part.DefaultDrawOrder.ToString(CultureInfo.InvariantCulture), value => ApplyPartDrawOrder(part, value));
        AddCheckBoxRow("Visible", part.DefaultVisible, value => ApplyPartVisible(part, value));
        AddText($"  flipX={part.DefaultFlipX} flipY={part.DefaultFlipY} index={index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
    }

    private void AddEditableEvent(int index, AnimationEventAsset animationEvent)
    {
        AddText($"Event #{index.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.PrimaryHeaderOpacity);
        AddTextBoxRow("Time", FormatFloat(animationEvent.TimeSeconds), value => ApplyEventTime(index, value));
        AddTextBoxRow("Name", animationEvent.EventName, value => ApplyEventName(index, value));
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
        MarkEdited("Updated event time.");
    }

    private void ApplyEventName(int eventIndex, string value)
    {
        if (_animationData == null || eventIndex < 0 || eventIndex >= _animationData.Events.Count)
        {
            return;
        }

        string eventName = value.Trim();
        var animationEvent = _animationData.Events[eventIndex];
        if (animationEvent.EventName == eventName)
        {
            return;
        }

        _animationData.Events[eventIndex] = animationEvent with { EventName = eventName };
        MarkEdited("Updated event name.");
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

    private void AddTextBoxRow(string label, string value, Action<string> onChanged)
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
        _contentStack!.TryAddChild(row);
    }

    private void AddCheckBoxRow(string label, bool isChecked, Action<bool> onChanged)
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

        _contentStack!.TryAddChild(checkBox);
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
        _timelineControl.ScrubRequested += SeekPreviewTime;
        _timelineControl.PixelsPerSecondChanged += OnTimelinePixelsPerSecondChanged;

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
            ClearTimelineDisplayEvents();
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

        RebuildTimelineDisplayEvents();
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

        _timelineText.Text = $"{FormatSeconds(_previewTimeSeconds)} / {FormatSeconds(_previewDurationSeconds)}  Events: {_timelineDisplayEvents.Count.ToString(CultureInfo.InvariantCulture)}  Zoom {FormatZoomPercentage(_timelineZoomFactor)}";
    }

    private bool IsTimelinePresentationAttached()
    {
        MGElement? current = _timelineRoot;
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
        _timelineControl.SetTimelineData(_animationData == null ? null : _timelineDisplayEvents, _previewDurationSeconds);
        _timelineControl.SetPlaybackState(_previewTimeSeconds, _selectedEventIndex);
    }

    private void RefreshTimelinePlaybackState()
    {
        _timelineControl?.SetPlaybackState(_previewTimeSeconds, _selectedEventIndex);
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
            SetStatus("Timeline selection cleared.");
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
        SetStatus($"Selected {DescribeEvent(_timelineDisplayEvents[eventIndex])}.");
        RefreshTimelineText();
        RefreshTimelinePlaybackState();
        RefreshInspector();
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
            _selectedEventIndex = 0;
        }
    }

    private bool TryGetSelectedEvent(out AnimationEventAsset animationEvent)
    {
        if (_animationData == null || _selectedEventIndex < 0 || _selectedEventIndex >= _timelineDisplayEvents.Count)
        {
            animationEvent = default;
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
            if (track.Property != Animation2dTrackProperty.Sprite)
            {
                continue;
            }

            for (var keyframeIndex = 0; keyframeIndex < track.SpriteKeyframes.Count; keyframeIndex++)
            {
                Animation2dGuidKeyframeData keyframe = track.SpriteKeyframes[keyframeIndex];
                _timelineDisplayEvents.Add(new AnimationEventAsset(keyframe.TimeSeconds, "changeSprite", keyframe.Value));
            }
        }

        if (_animationData.Events.Count > 0)
        {
            for (var index = 0; index < _animationData.Events.Count; index++)
            {
                _timelineDisplayEvents.Add(_animationData.Events[index]);
            }
        }

        _timelineDisplayEvents.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    private void ClearTimelineDisplayEvents()
    {
        _timelineDisplayEvents.Clear();
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
            _ => 0,
        };
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