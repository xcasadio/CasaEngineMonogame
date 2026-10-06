using Microsoft.Xna.Framework;
using NumericsVector3 = System.Numerics.Vector3;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Reads the world pose of a scene component for the audio components (plan decision P40).
/// </summary>
/// <remarks>
/// The pose comes from <see cref="SceneComponent.WorldMatrixNoScale"/>, which composes the parent rotations, and never from
/// <see cref="SceneComponent.Position"/>, <see cref="SceneComponent.Orientation"/>, <see cref="SceneComponent.Forward"/> or
/// <see cref="SceneComponent.Up"/>, which add the parent values without composing them (wrong under a rotated parent).
/// A component placed at entity level has no parent, so it keeps its own matrix, like the gizmo of the editor.
/// For a component with a parent inside a child entity, the matrix applies the root of the parent entity twice: this is
/// the behaviour of <see cref="SceneComponent"/> and the audio inherits it.
/// </remarks>
internal static class AudioScenePose
{
    /// <summary>Half size of the bounding box of an audio component, in world units.</summary>
    private const float BoundingBoxHalfSize = 0.1f;

    /// <summary>
    /// World position, forward (the world image of <see cref="Vector3.Forward"/>) and up (the image of
    /// <see cref="Vector3.Up"/>), the last two normalized. Allocation free.
    /// </summary>
    public static void GetWorldPose(SceneComponent component, out NumericsVector3 position, out NumericsVector3 forward, out NumericsVector3 up)
    {
        var matrix = component.WorldMatrixNoScale;

        position = new NumericsVector3(matrix.M41, matrix.M42, matrix.M43);

        // Image of (0, 0, -1) and (0, 1, 0) by the rotation part: minus the third row, the second row.
        forward = Normalize(new NumericsVector3(-matrix.M31, -matrix.M32, -matrix.M33), new NumericsVector3(0f, 0f, -1f));
        up = Normalize(new NumericsVector3(matrix.M21, matrix.M22, matrix.M23), NumericsVector3.UnitY);
    }

    /// <summary>World position of the component. Allocation free.</summary>
    public static NumericsVector3 GetWorldPosition(SceneComponent component)
    {
        var matrix = component.WorldMatrixNoScale;
        return new NumericsVector3(matrix.M41, matrix.M42, matrix.M43);
    }

    /// <summary>A small box around the pose of the component, so it does not stretch the box of its entity.</summary>
    public static BoundingBox GetBoundingBox(SceneComponent component)
    {
        var matrix = component.WorldMatrixNoScale;
        var center = new Vector3(matrix.M41, matrix.M42, matrix.M43);
        var half = new Vector3(BoundingBoxHalfSize);

        return new BoundingBox(center - half, center + half);
    }

    private static NumericsVector3 Normalize(NumericsVector3 value, NumericsVector3 fallback)
    {
        var lengthSquared = value.LengthSquared();

        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-12f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSquared);
    }
}
