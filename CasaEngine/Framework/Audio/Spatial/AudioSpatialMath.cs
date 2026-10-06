using System.Numerics;

namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>Listener-to-source distance and pan. Pure and allocation-free.</summary>
internal static class AudioSpatialMath
{
    /// <summary>
    /// Euclidean distance between the listener and the source: three-dimensional for
    /// <see cref="AudioSpatialMode.Spatial3D"/>, on the X/Y plane (Z ignored) for <see cref="AudioSpatialMode.Spatial2D"/>.
    /// Returns 0 for <see cref="AudioSpatialMode.None"/>.
    /// </summary>
    public static float Distance(AudioSpatialMode mode, Vector3 listenerPosition, Vector3 sourcePosition)
    {
        var delta = sourcePosition - listenerPosition;

        switch (mode)
        {
            case AudioSpatialMode.Spatial3D:
                return delta.Length();
            case AudioSpatialMode.Spatial2D:
                return MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Pan in [-1, 1] (-1 left, +1 right): dot product of the normalized listener-to-source direction with the
    /// listener's right vector (sine of the azimuth, no front/back distinction). The OpenAL 1.1 specification does not
    /// define panning (it leaves it to the implementation), so this is an engine choice. In 2D the direction and the
    /// right vector are projected on X/Y and renormalized (fallback right (1, 0)). Zero distance, no spatialization
    /// or a non-finite result gives 0.
    /// </summary>
    public static float Pan(AudioSpatialMode mode, in AudioListenerPose listener, Vector3 sourcePosition)
    {
        var direction = sourcePosition - listener.Position;
        var right = listener.Right;

        if (mode == AudioSpatialMode.Spatial2D)
        {
            direction.Z = 0f;
            right.Z = 0f;

            var rightLength = MathF.Sqrt(right.X * right.X + right.Y * right.Y);
            right = rightLength < 1e-6f ? new Vector3(1f, 0f, 0f) : right / rightLength;
        }
        else if (mode != AudioSpatialMode.Spatial3D)
        {
            return 0f;
        }

        var length = direction.Length();

        if (!float.IsFinite(length) || length < 1e-9f)
        {
            return 0f;
        }

        var pan = Vector3.Dot(direction / length, right);

        return float.IsFinite(pan) ? Math.Clamp(pan, -1f, 1f) : 0f;
    }
}
