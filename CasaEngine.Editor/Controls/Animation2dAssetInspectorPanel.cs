#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration;
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

    public Animation2dAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    public Animation2dData? LoadedAnimationData => _animationData;

    public string? LoadedRelativePath => _loadedRelativePath;

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

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
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
        RefreshInspector();
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
        _statusText.Text = "Read-only Animation2D asset.";

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
                AddText($"{EscapeMarkup(part.Id)} name={EscapeMarkup(part.Name)} sprite={part.DefaultSpriteId} pos=({part.DefaultPosition.X.ToString("0.###", CultureInfo.InvariantCulture)}, {part.DefaultPosition.Y.ToString("0.###", CultureInfo.InvariantCulture)}) draw={part.DefaultDrawOrder.ToString(CultureInfo.InvariantCulture)} visible={part.DefaultVisible} flipX={part.DefaultFlipX} flipY={part.DefaultFlipY}", EditorThemePalette.PrimaryHeaderOpacity);
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
        if (_animationData.Events.Count == 0)
        {
            AddText("No animation events.", EditorThemePalette.SecondaryTextOpacity);
        }
        else
        {
            for (var index = 0; index < _animationData.Events.Count; index++)
            {
                var animationEvent = _animationData.Events[index];
                AddText($"{animationEvent.TimeSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s {EscapeMarkup(animationEvent.EventName)}", EditorThemePalette.PrimaryHeaderOpacity);
            }
        }
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

    private static string EscapeMarkup(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("[", "[[]", StringComparison.Ordinal).Replace("]", "[]]", StringComparison.Ordinal);
    }
}