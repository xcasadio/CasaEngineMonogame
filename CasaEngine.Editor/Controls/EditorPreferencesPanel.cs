using System;
using System.Collections.Generic;
using CasaEngine.Editor.Runtime;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Windows &gt; Editor Preferences (ADR-0091): edits the per-user mouse and camera preferences. Every change goes to
/// the <see cref="EditorPreferencesStore"/>, which applies it at once to every viewport and writes the file a second
/// after the last change. The panel follows the store, so a fly speed changed with right button + wheel shows here.
/// </summary>
internal sealed class EditorPreferencesPanel
{
    private readonly MGWindow _window;
    private readonly EditorPreferencesStore _store;
    private readonly List<Action<EditorPreferences>> _refreshers = new();
    private MGElement _rootContent;
    private bool _suppressCallbacks;

    public EditorPreferencesPanel(MGWindow window, EditorPreferencesStore store)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public MGElement CreateContent()
    {
        if (_rootContent != null)
        {
            return _rootContent;
        }

        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(8, 6, 8, 6),
        };

        stack.TryAddChild(Header("3D viewport"));
        stack.TryAddChild(Number("Rotation (rad/px)", EditorPreferenceBounds.RotationSensitivity, 0.0005f,
            p => p.Camera3d.RotationSensitivity, (p, v) => p with { Camera3d = p.Camera3d with { RotationSensitivity = v } }));
        stack.TryAddChild(Number("Pan (x distance/px)", EditorPreferenceBounds.PanSensitivity, 0.001f,
            p => p.Camera3d.PanSensitivity, (p, v) => p with { Camera3d = p.Camera3d with { PanSensitivity = v } }));
        stack.TryAddChild(Number("Wheel step (x distance)", EditorPreferenceBounds.WheelStepRatio, 0.01f,
            p => p.Camera3d.WheelStepRatio, (p, v) => p with { Camera3d = p.Camera3d with { WheelStepRatio = v } }));
        stack.TryAddChild(Number("Fly speed (units/s)", EditorPreferenceBounds.FlySpeed, 1f,
            p => p.Camera3d.FlySpeed, (p, v) => p with { Camera3d = p.Camera3d with { FlySpeed = v } }));
        stack.TryAddChild(Number("Fly speed wheel factor", EditorPreferenceBounds.FlySpeedWheelFactor, 0.05f,
            p => p.Camera3d.FlySpeedWheelFactor, (p, v) => p with { Camera3d = p.Camera3d with { FlySpeedWheelFactor = v } }));
        stack.TryAddChild(Number("Field of view rate (deg/s)", EditorPreferenceBounds.FieldOfViewRateDegrees, 5f,
            p => p.Camera3d.FieldOfViewRateDegrees, (p, v) => p with { Camera3d = p.Camera3d with { FieldOfViewRateDegrees = v } }));
        stack.TryAddChild(Number("Click / drag threshold (px)", EditorPreferenceBounds.DragThresholdPixels, 1f,
            p => p.Camera3d.DragThresholdPixels, (p, v) => p with { Camera3d = p.Camera3d with { DragThresholdPixels = v } }));
        stack.TryAddChild(CheckBox("Invert Y (look and orbit)",
            p => p.Camera3d.InvertY, (p, v) => p with { Camera3d = p.Camera3d with { InvertY = v } }));

        stack.TryAddChild(Header("2D viewport"));
        stack.TryAddChild(Number("Pan speed", EditorPreferenceBounds.PanSpeed2d, 0.1f,
            p => p.Camera2d.PanSpeed, (p, v) => p with { Camera2d = p.Camera2d with { PanSpeed = v } }));
        stack.TryAddChild(Number("Zoom steps per notch", EditorPreferenceBounds.ZoomStepsPerNotch2d, 1f,
            p => p.Camera2d.ZoomStepsPerNotch, (p, v) => p with { Camera2d = p.Camera2d with { ZoomStepsPerNotch = (int)MathF.Round(v) } }));
        stack.TryAddChild(CheckBox("Invert Y (pan)",
            p => p.Camera2d.InvertY, (p, v) => p with { Camera2d = p.Camera2d with { InvertY = v } }));

        var resetButton = new MGButton(_window, _ => _store.Update(EditorPreferences.Default))
        {
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        resetButton.SetContent(new MGTextBlock(_window, "Reset to defaults")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.TryAddChild(resetButton);

        stack.TryAddChild(new MGTextBlock(_window, EscapeMarkup(_store.FilePath))
        {
            Margin = new Thickness(0, 4, 0, 0),
        });

        _store.Changed += Refresh;
        Refresh(_store.Current);

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(stack);
        _rootContent = scrollViewer;
        return _rootContent;
    }

    private MGElement Header(string text)
    {
        return new MGTextBlock(_window, $"[b]{text}[/b]")
        {
            Margin = new Thickness(0, 4, 0, 0),
        };
    }

    private MGElement Number(
        string label,
        EditorPreferenceRange range,
        float step,
        Func<EditorPreferences, float> read,
        Func<EditorPreferences, float, EditorPreferences> write)
    {
        var field = new NumericField(_window, label, range.Min, range.Max, step)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        field.ValueChanged += (_, value) =>
        {
            if (!_suppressCallbacks)
            {
                _store.Update(write(_store.Current, value));
            }
        };
        _refreshers.Add(p =>
        {
            float value = read(p);
            if (field.Value != value)
            {
                field.Value = value;
            }
        });
        return field;
    }

    private MGElement CheckBox(string label, Func<EditorPreferences, bool> read, Func<EditorPreferences, bool, EditorPreferences> write)
    {
        var checkBox = new MGCheckBox(_window);
        checkBox.SetContent(new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        checkBox.OnCheckStateChanged += (_, args) =>
        {
            if (!_suppressCallbacks)
            {
                _store.Update(write(_store.Current, args.NewValue ?? false));
            }
        };
        _refreshers.Add(p =>
        {
            bool value = read(p);
            if (checkBox.IsChecked != value)
            {
                checkBox.IsChecked = value;
            }
        });
        return checkBox;
    }

    private void Refresh(EditorPreferences preferences)
    {
        _suppressCallbacks = true;
        try
        {
            for (int i = 0; i < _refreshers.Count; i++)
            {
                _refreshers[i](preferences);
            }
        }
        finally
        {
            _suppressCallbacks = false;
        }
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
