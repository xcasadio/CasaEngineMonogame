using System.Numerics;

namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>Position and orientation of the audio listener.</summary>
public readonly struct AudioListenerPose
{
    private static readonly Vector3 FallbackRight = new(1f, 0f, 0f);

    private AudioListenerPose(Vector3 position, Vector3 forward, Vector3 up)
    {
        Position = position;
        Forward = forward;
        Up = up;
    }

    /// <summary>The listener position, in world units.</summary>
    public Vector3 Position { get; }

    /// <summary>The normalized direction the listener looks at.</summary>
    public Vector3 Forward { get; }

    /// <summary>The normalized up direction of the listener.</summary>
    public Vector3 Up { get; }

    /// <summary>
    /// The normalized right direction, <c>Cross(Forward, Up)</c>; (1, 0, 0) when the cross product is degenerate
    /// (parallel, zero or non-finite vectors).
    /// </summary>
    public Vector3 Right
    {
        get
        {
            var cross = Vector3.Cross(Forward, Up);
            var lengthSquared = cross.LengthSquared();

            if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-12f)
            {
                return FallbackRight;
            }

            return cross / MathF.Sqrt(lengthSquared);
        }
    }

    /// <summary>
    /// The default pose of the OpenAL 1.1 specification, section 4.2.1
    /// (https://www.openal.org/documentation/openal-1.1-specification.pdf): origin, forward (0, 0, -1), up (0, 1, 0).
    /// </summary>
    public static AudioListenerPose Default { get; } = new(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY);

    /// <summary>
    /// Creates a pose, normalizing <paramref name="forward"/> and <paramref name="up"/>. A zero or non-finite
    /// direction is replaced by the default one.
    /// </summary>
    public static AudioListenerPose Create(Vector3 position, Vector3 forward, Vector3 up)
    {
        return new AudioListenerPose(
            position,
            NormalizeOrDefault(forward, new Vector3(0f, 0f, -1f)),
            NormalizeOrDefault(up, Vector3.UnitY));
    }

    private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
    {
        var lengthSquared = value.LengthSquared();

        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-12f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSquared);
    }
}
