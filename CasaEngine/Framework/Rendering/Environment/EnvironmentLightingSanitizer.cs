using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class EnvironmentLightingSanitizer
{
    public static Vector3 NormalizeAmbientColor(Vector3 value)
    {
        return new Vector3(
            NormalizeNonNegative(value.X),
            NormalizeNonNegative(value.Y),
            NormalizeNonNegative(value.Z));
    }

    public static float NormalizeIntensity(float value)
    {
        return NormalizeNonNegative(value);
    }

    private static float NormalizeNonNegative(float value)
    {
        if (float.IsNaN(value))
        {
            return 0.0f;
        }

        return Math.Max(value, 0.0f);
    }
}