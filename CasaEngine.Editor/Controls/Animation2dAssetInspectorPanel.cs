#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dAssetInspectorPanel
{
    private readonly MGWindow _window;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _contentStack;

    private Animation2dData? _animationData;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private bool _isDirty;
    private bool _suppressControlCallbacks;

    public Animation2dAssetInspectorPanel(MGWindow window)
    {
        _window = window;
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

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
        };
        toolbar.TryAddChild(CreateButton("Save", SaveLoadedAsset));
        toolbar.TryAddChild(CreateButton("Reload", ReloadLoadedAsset));

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public void LoadAsset(Animation2dData animationData, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(animationData);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _animationData = animationData;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        SetDirty(false);

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
            _headerText.Text = "[b]Animation2D Inspector[/b]";
            _sourceText.Text = "No animation asset loaded.";
            _statusText.Text = "Open a .anim2d asset from the Content Browser.";
            return;
        }

        _headerText.Text = $"[b]{EscapeMarkup(_animationData.Name)}[/b]";
        _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "No source path."
            : EscapeMarkup(_loadedRelativePath);
        _statusText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "Animation2D asset."
            : IsDirty ? $"Modified {EscapeMarkup(_loadedRelativePath)}" : $"Asset: {EscapeMarkup(_loadedRelativePath)}";

        AddProperty("Type", _animationData.AnimationType.ToString());
        AddProperty("Legacy frames", _animationData.Frames.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Parts", _animationData.Parts.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Tracks", _animationData.Tracks.Count.ToString(CultureInfo.InvariantCulture));
        AddProperty("Events", _animationData.Events.Count.ToString(CultureInfo.InvariantCulture));

        AddSection("Validation");
        var invalidTrackTargets = _animationData.GetInvalidTrackTargetPartIds();
        if (invalidTrackTargets.Count == 0)
        {
            AddText("No validation warnings.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < invalidTrackTargets.Count; index++)
            {
                AddText($"Track target part not found: {EscapeMarkup(invalidTrackTargets[index])}", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }

        AddSection("Legacy Frames");
        if (_animationData.Frames.Count == 0)
        {
            AddText("No legacy frames.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < _animationData.Frames.Count; index++)
            {
                var frame = _animationData.Frames[index];
                AddText($"#{index.ToString(CultureInfo.InvariantCulture)} sprite={frame.SpriteId} duration={frame.Duration.ToString("0.###", CultureInfo.InvariantCulture)}s", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }

        AddSection("Parts");
        if (_animationData.Parts.Count == 0)
        {
            AddText("No composed parts.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < _animationData.Parts.Count; index++)
            {
                var part = _animationData.Parts[index];
                AddEditablePart(index, part);
            }
        }

        AddSection("Tracks");
        if (_animationData.Tracks.Count == 0)
        {
            AddText("No composed tracks.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < _animationData.Tracks.Count; index++)
            {
                AddTrack(_animationData.Tracks[index]);
            }
        }

        AddSection("Events");
        AddButton("Add Event", AddAnimationEvent);
        if (_animationData.Events.Count == 0)
        {
            AddText("No animation events.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < _animationData.Events.Count; index++)
            {
                var animationEvent = _animationData.Events[index];
                AddEditableEvent(index, animationEvent);
            }
        }
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
                for (var index = 0; index < track.SpriteKeyframes.Count; index++)
                {
                    var keyframe = track.SpriteKeyframes[index];
                    AddText($"  {FormatSeconds(keyframe.TimeSeconds)} sprite={keyframe.Value:D}", EditorThemePalette.SecondaryTextOpacity);
                }

                break;

            case Animation2dTrackProperty.Position:
                for (var index = 0; index < track.PositionKeyframes.Count; index++)
                {
                    var keyframe = track.PositionKeyframes[index];
                    AddText($"  {FormatSeconds(keyframe.TimeSeconds)} pos=({FormatFloat(keyframe.Value.X)}, {FormatFloat(keyframe.Value.Y)})", EditorThemePalette.SecondaryTextOpacity);
                }

                break;

            case Animation2dTrackProperty.Visible:
                AddBoolKeyframes(track.VisibleKeyframes, "visible");
                break;

            case Animation2dTrackProperty.DrawOrder:
                for (var index = 0; index < track.DrawOrderKeyframes.Count; index++)
                {
                    var keyframe = track.DrawOrderKeyframes[index];
                    AddText($"  {FormatSeconds(keyframe.TimeSeconds)} draw={keyframe.Value.ToString(CultureInfo.InvariantCulture)}", EditorThemePalette.SecondaryTextOpacity);
                }

                break;

            case Animation2dTrackProperty.FlipX:
                AddBoolKeyframes(track.FlipKeyframes, "flipX");
                break;

            case Animation2dTrackProperty.FlipY:
                AddBoolKeyframes(track.FlipKeyframes, "flipY");
                break;
        }
    }

    private void AddBoolKeyframes(IReadOnlyList<Animation2dBoolKeyframeData> keyframes, string label)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            AddText($"  {FormatSeconds(keyframe.TimeSeconds)} {label}={keyframe.Value}", EditorThemePalette.SecondaryTextOpacity);
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