using System;

namespace CasaEngine.Editor.Runtime;

/// <summary>Mouse and camera preferences of the editor's 3D viewports (ADR-0091).</summary>
internal sealed record Camera3dPreferences
{
    public float RotationSensitivity { get; init; } = 0.005f;
    public float PanSensitivity { get; init; } = 0.01f;
    public float WheelStepRatio { get; init; } = 0.1f;
    public float FlySpeed { get; init; } = 10f;
    public float FlySpeedWheelFactor { get; init; } = 1.25f;
    public float FieldOfViewRateDegrees { get; init; } = 45f;
    public float DragThresholdPixels { get; init; } = 4f;
    public bool InvertY { get; init; }
}

/// <summary>Mouse and camera preferences of the editor's 2D viewport (ADR-0091).</summary>
internal sealed record Camera2dPreferences
{
    public float PanSpeed { get; init; } = 1f;
    public int ZoomStepsPerNotch { get; init; } = 1;
    public bool InvertY { get; init; }
}

/// <summary>Per-user editor preferences, saved in <c>%APPDATA%/CasaEngine/editor.preferences.json</c> (ADR-0091).</summary>
internal sealed record EditorPreferences
{
    public const int CurrentVersion = 1;

    public static EditorPreferences Default { get; } = new();

    public Camera3dPreferences Camera3d { get; init; } = new();
    public Camera2dPreferences Camera2d { get; init; } = new();

    /// <summary>Returns a copy with every value inside its bounds; <paramref name="warn"/> receives one message per clamped value.</summary>
    public EditorPreferences Clamp(Action<string> warn = null)
    {
        Camera3dPreferences c3 = Camera3d ?? new Camera3dPreferences();
        Camera2dPreferences c2 = Camera2d ?? new Camera2dPreferences();

        return new EditorPreferences
        {
            Camera3d = c3 with
            {
                RotationSensitivity = EditorPreferenceBounds.RotationSensitivity.Clamp(c3.RotationSensitivity, warn),
                PanSensitivity = EditorPreferenceBounds.PanSensitivity.Clamp(c3.PanSensitivity, warn),
                WheelStepRatio = EditorPreferenceBounds.WheelStepRatio.Clamp(c3.WheelStepRatio, warn),
                FlySpeed = EditorPreferenceBounds.FlySpeed.Clamp(c3.FlySpeed, warn),
                FlySpeedWheelFactor = EditorPreferenceBounds.FlySpeedWheelFactor.Clamp(c3.FlySpeedWheelFactor, warn),
                FieldOfViewRateDegrees = EditorPreferenceBounds.FieldOfViewRateDegrees.Clamp(c3.FieldOfViewRateDegrees, warn),
                DragThresholdPixels = EditorPreferenceBounds.DragThresholdPixels.Clamp(c3.DragThresholdPixels, warn),
            },
            Camera2d = c2 with
            {
                PanSpeed = EditorPreferenceBounds.PanSpeed2d.Clamp(c2.PanSpeed, warn),
                ZoomStepsPerNotch = (int)EditorPreferenceBounds.ZoomStepsPerNotch2d.Clamp(c2.ZoomStepsPerNotch, warn),
            },
        };
    }
}

/// <summary>Allowed range of one preference, named by its JSON key.</summary>
internal readonly struct EditorPreferenceRange
{
    public EditorPreferenceRange(string key, float min, float max)
    {
        Key = key;
        Min = min;
        Max = max;
    }

    public string Key { get; }
    public float Min { get; }
    public float Max { get; }

    public float Clamp(float value, Action<string> warn)
    {
        if (float.IsNaN(value) || value < Min || value > Max)
        {
            float clamped = float.IsNaN(value) ? Min : Math.Clamp(value, Min, Max);
            warn?.Invoke($"Editor preference '{Key}' = {value} is outside [{Min}, {Max}]: {clamped} is used.");
            return clamped;
        }

        return value;
    }
}

/// <summary>Bounds of the editor preferences (ADR-0091).</summary>
internal static class EditorPreferenceBounds
{
    public static readonly EditorPreferenceRange RotationSensitivity = new("rotation_sensitivity", 0.0005f, 0.05f);
    public static readonly EditorPreferenceRange PanSensitivity = new("pan_sensitivity", 0.001f, 0.1f);
    public static readonly EditorPreferenceRange WheelStepRatio = new("wheel_step_ratio", 0.01f, 0.5f);
    public static readonly EditorPreferenceRange FlySpeed = new("fly_speed", 0.1f, 1000f);
    public static readonly EditorPreferenceRange FlySpeedWheelFactor = new("fly_speed_wheel_factor", 1.05f, 3f);
    public static readonly EditorPreferenceRange FieldOfViewRateDegrees = new("field_of_view_rate_degrees", 5f, 180f);
    public static readonly EditorPreferenceRange DragThresholdPixels = new("drag_threshold_pixels", 1f, 20f);
    public static readonly EditorPreferenceRange PanSpeed2d = new("pan_speed", 0.1f, 10f);
    public static readonly EditorPreferenceRange ZoomStepsPerNotch2d = new("zoom_steps_per_notch", 1f, 5f);
}
