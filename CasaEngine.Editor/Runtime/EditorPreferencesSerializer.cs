using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// Json form of <see cref="EditorPreferences"/> (ADR-0091): snake_case keys in a stable order, a version, and a
/// tolerant reader. Free of any file access so that it can be unit tested.
/// </summary>
internal static class EditorPreferencesSerializer
{
    private const string VersionKey = "version";
    private const string Camera3dKey = "camera_3d";
    private const string Camera2dKey = "camera_2d";
    private const string InvertYKey = "invert_y";

    public static string Write(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Camera3dPreferences c3 = preferences.Camera3d;
        Camera2dPreferences c2 = preferences.Camera2d;

        var json = new JObject
        {
            [VersionKey] = EditorPreferences.CurrentVersion,
            [Camera3dKey] = new JObject
            {
                [EditorPreferenceBounds.RotationSensitivity.Key] = c3.RotationSensitivity,
                [EditorPreferenceBounds.PanSensitivity.Key] = c3.PanSensitivity,
                [EditorPreferenceBounds.WheelStepRatio.Key] = c3.WheelStepRatio,
                [EditorPreferenceBounds.FlySpeed.Key] = c3.FlySpeed,
                [EditorPreferenceBounds.FlySpeedWheelFactor.Key] = c3.FlySpeedWheelFactor,
                [EditorPreferenceBounds.FieldOfViewRateDegrees.Key] = c3.FieldOfViewRateDegrees,
                [EditorPreferenceBounds.DragThresholdPixels.Key] = c3.DragThresholdPixels,
                [InvertYKey] = c3.InvertY,
            },
            [Camera2dKey] = new JObject
            {
                [EditorPreferenceBounds.PanSpeed2d.Key] = c2.PanSpeed,
                [EditorPreferenceBounds.ZoomStepsPerNotch2d.Key] = c2.ZoomStepsPerNotch,
                [InvertYKey] = c2.InvertY,
            },
        };

        return json.ToString(Formatting.Indented);
    }

    /// <summary>
    /// Reads preferences. A missing key keeps its default; a mistyped or out-of-range value is defaulted or clamped
    /// and reported through <paramref name="warn"/>. Returns false, with the defaults, when the text is not a Json
    /// object.
    /// </summary>
    public static bool TryRead(string text, Action<string> warn, out EditorPreferences preferences)
    {
        preferences = EditorPreferences.Default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        JObject json;
        try
        {
            json = JObject.Parse(text);
        }
        catch (JsonException ex)
        {
            warn?.Invoke($"Editor preferences are not valid Json, defaults are used: {ex.Message}");
            return false;
        }

        JToken versionToken = json[VersionKey];
        if (versionToken != null
            && (!TryReadNumber(versionToken, out double version) || version != EditorPreferences.CurrentVersion))
        {
            warn?.Invoke(FormattableString.Invariant(
                $"Editor preferences version {versionToken.ToString(Formatting.None)} is not {EditorPreferences.CurrentVersion}: known keys are read, others are ignored."));
        }

        var defaults3d = new Camera3dPreferences();
        var defaults2d = new Camera2dPreferences();
        JObject c3 = ReadBlock(json, Camera3dKey, warn);
        JObject c2 = ReadBlock(json, Camera2dKey, warn);

        var read = new EditorPreferences
        {
            Camera3d = new Camera3dPreferences
            {
                RotationSensitivity = ReadFloat(c3, EditorPreferenceBounds.RotationSensitivity.Key, defaults3d.RotationSensitivity, warn),
                PanSensitivity = ReadFloat(c3, EditorPreferenceBounds.PanSensitivity.Key, defaults3d.PanSensitivity, warn),
                WheelStepRatio = ReadFloat(c3, EditorPreferenceBounds.WheelStepRatio.Key, defaults3d.WheelStepRatio, warn),
                FlySpeed = ReadFloat(c3, EditorPreferenceBounds.FlySpeed.Key, defaults3d.FlySpeed, warn),
                FlySpeedWheelFactor = ReadFloat(c3, EditorPreferenceBounds.FlySpeedWheelFactor.Key, defaults3d.FlySpeedWheelFactor, warn),
                FieldOfViewRateDegrees = ReadFloat(c3, EditorPreferenceBounds.FieldOfViewRateDegrees.Key, defaults3d.FieldOfViewRateDegrees, warn),
                DragThresholdPixels = ReadFloat(c3, EditorPreferenceBounds.DragThresholdPixels.Key, defaults3d.DragThresholdPixels, warn),
                InvertY = ReadBool(c3, InvertYKey, defaults3d.InvertY, warn),
            },
            Camera2d = new Camera2dPreferences
            {
                PanSpeed = ReadFloat(c2, EditorPreferenceBounds.PanSpeed2d.Key, defaults2d.PanSpeed, warn),
                ZoomStepsPerNotch = (int)MathF.Round(ReadFloat(c2, EditorPreferenceBounds.ZoomStepsPerNotch2d.Key, defaults2d.ZoomStepsPerNotch, warn)),
                InvertY = ReadBool(c2, InvertYKey, defaults2d.InvertY, warn),
            },
        };

        preferences = read.Clamp(warn);
        return true;
    }

    private static JObject ReadBlock(JObject json, string key, Action<string> warn)
    {
        JToken token = json[key];
        if (token == null)
        {
            return null;
        }

        if (token is JObject block)
        {
            return block;
        }

        warn?.Invoke($"Editor preference '{key}' is not an object: its defaults are used.");
        return null;
    }

    private static float ReadFloat(JObject block, string key, float fallback, Action<string> warn)
    {
        JToken token = block?[key];
        if (token == null)
        {
            return fallback;
        }

        // Parsed from the literal: an integer beyond Int64 is a BigInteger that Value<float>() cannot convert.
        // A huge value becomes infinity and is clamped by EditorPreferences.Clamp.
        if (TryReadNumber(token, out double value))
        {
            return (float)value;
        }

        warn?.Invoke(FormattableString.Invariant($"Editor preference '{key}' is not a number: {fallback} is used."));
        return fallback;
    }

    private static bool TryReadNumber(JToken token, out double value)
    {
        value = 0;
        return token.Type is JTokenType.Float or JTokenType.Integer
            && double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool ReadBool(JObject block, string key, bool fallback, Action<string> warn)
    {
        JToken token = block?[key];
        if (token == null)
        {
            return fallback;
        }

        if (token.Type == JTokenType.Boolean)
        {
            return token.Value<bool>();
        }

        warn?.Invoke(FormattableString.Invariant($"Editor preference '{key}' is not true or false: {fallback} is used."));
        return fallback;
    }
}
