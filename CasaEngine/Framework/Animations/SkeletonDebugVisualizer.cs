using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct SkeletonDebugDrawOptions(
    float AxisLength,
    bool DrawAxes,
    Color BoneColor,
    Color AxisXColor,
    Color AxisYColor,
    Color AxisZColor)
{
    public static SkeletonDebugDrawOptions Default { get; } = new(
        0.12f,
        true,
        new Color(255, 208, 96),
        new Color(255, 96, 96),
        new Color(96, 255, 140),
        new Color(96, 160, 255));
}

public static class SkeletonDebugVisualizer
{
    public static void Draw(
        Line3dRendererComponent lineRenderer,
        SkeletonPoseModel modelPose,
        Matrix worldMatrix)
    {
        Draw(lineRenderer, modelPose, worldMatrix, SkeletonDebugDrawOptions.Default);
    }

    public static void Draw(
        Line3dRendererComponent lineRenderer,
        SkeletonPoseModel modelPose,
        Matrix worldMatrix,
        SkeletonDebugDrawOptions options)
    {
        ArgumentNullException.ThrowIfNull(lineRenderer);
        ArgumentNullException.ThrowIfNull(modelPose);

        var skeleton = modelPose.Skeleton;
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            var jointWorldMatrix = modelPose.GetTransform(jointIndex) * worldMatrix;
            var jointPosition = jointWorldMatrix.Translation;
            var parentIndex = skeleton.GetJoint(jointIndex).ParentIndex;
            if (parentIndex >= 0)
            {
                var parentWorldMatrix = modelPose.GetTransform(parentIndex) * worldMatrix;
                lineRenderer.AddLine(parentWorldMatrix.Translation, jointPosition, options.BoneColor);
            }

            if (!options.DrawAxes)
            {
                continue;
            }

            var axisLength = options.AxisLength;
            var axisX = SafeNormalize(Vector3.TransformNormal(Vector3.Right, jointWorldMatrix), Vector3.Right);
            var axisY = SafeNormalize(Vector3.TransformNormal(Vector3.Up, jointWorldMatrix), Vector3.Up);
            var axisZ = SafeNormalize(Vector3.TransformNormal(Vector3.Forward, jointWorldMatrix), Vector3.Forward);
            lineRenderer.AddLine(jointPosition, jointPosition + axisX * axisLength, options.AxisXColor);
            lineRenderer.AddLine(jointPosition, jointPosition + axisY * axisLength, options.AxisYColor);
            lineRenderer.AddLine(jointPosition, jointPosition + axisZ * axisLength, options.AxisZColor);
        }
    }

    private static Vector3 SafeNormalize(Vector3 vector, Vector3 fallback)
    {
        if (vector.LengthSquared() <= 1e-6f)
        {
            return fallback.LengthSquared() <= 1e-6f
                ? Vector3.UnitX
                : Vector3.Normalize(fallback);
        }

        return Vector3.Normalize(vector);
    }
}