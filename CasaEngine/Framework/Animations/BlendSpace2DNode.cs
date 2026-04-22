using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct BlendSpace2DSample(Vector2 Position, IAnimationGraphNode Node);

public sealed class BlendSpace2DNode : IAnimationGraphRuntimeNode
{
    private readonly BlendSpace2DSample[] _samples;
    private readonly SkeletonPoseLocal _samplePoseA;
    private readonly SkeletonPoseLocal _samplePoseB;
    private readonly SkeletonPoseLocal _samplePoseC;
    private readonly SkeletonPoseLocal[] _weightedBlendPoses;
    private readonly float[] _weightedBlendWeights;

    public BlendSpace2DNode(IReadOnlyList<BlendSpace2DSample> samples, Vector2 parameter)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("A 2D blend space needs at least one sample.", nameof(samples));
        }

        _samples = new BlendSpace2DSample[samples.Count];
        for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
        {
            var sample = samples[sampleIndex];
            if (sample.Node == null)
            {
                throw new ArgumentException("Blend space samples cannot contain null nodes.", nameof(samples));
            }

            if (sampleIndex > 0 && ReferenceEquals(samples[0].Node.Skeleton, sample.Node.Skeleton) == false)
            {
                throw new ArgumentException("All blend space samples must target the same skeleton.", nameof(samples));
            }

            _samples[sampleIndex] = sample;
        }

        Skeleton = _samples[0].Node.Skeleton;
        _samplePoseA = Skeleton.CreateLocalBindPose();
        _samplePoseB = Skeleton.CreateLocalBindPose();
        _samplePoseC = Skeleton.CreateLocalBindPose();
        _weightedBlendPoses = new[] { _samplePoseA, _samplePoseB, _samplePoseC };
        _weightedBlendWeights = new float[3];
        Parameter = parameter;
    }

    public SkeletonDefinition Skeleton { get; }

    public Vector2 Parameter { get; set; }

    public void Advance(float elapsedSeconds)
    {
        for (var sampleIndex = 0; sampleIndex < _samples.Length; sampleIndex++)
        {
            AdvanceNode(_samples[sampleIndex].Node, elapsedSeconds);
        }
    }

    public void Evaluate(SkeletonPoseLocal outputPose)
    {
        ArgumentNullException.ThrowIfNull(outputPose);

        if (!ReferenceEquals(outputPose.Skeleton, Skeleton))
        {
            throw new ArgumentException("The output pose targets a different skeleton.", nameof(outputPose));
        }

        if (_samples.Length == 1)
        {
            _samples[0].Node.Evaluate(outputPose);
            return;
        }

        if (_samples.Length == 2)
        {
            EvaluateSegment(_samples[0], _samples[1], outputPose);
            return;
        }

        if (TryFindContainingTriangle(Parameter, out var sampleIndexA, out var sampleIndexB, out var sampleIndexC, out var weights))
        {
            EvaluateTriangle(sampleIndexA, sampleIndexB, sampleIndexC, weights, outputPose);
            return;
        }

        if (TryFindClosestSegment(Parameter, out sampleIndexA, out sampleIndexB))
        {
            EvaluateSegment(_samples[sampleIndexA], _samples[sampleIndexB], outputPose);
            return;
        }

        EvaluateNearestSample(outputPose);
    }

    private void EvaluateTriangle(int sampleIndexA, int sampleIndexB, int sampleIndexC, Vector3 weights, SkeletonPoseLocal outputPose)
    {
        _samples[sampleIndexA].Node.Evaluate(_samplePoseA);
        _samples[sampleIndexB].Node.Evaluate(_samplePoseB);
        _samples[sampleIndexC].Node.Evaluate(_samplePoseC);

        _weightedBlendWeights[0] = weights.X;
        _weightedBlendWeights[1] = weights.Y;
        _weightedBlendWeights[2] = weights.Z;
        AnimationPoseBlender.BlendWeighted(_weightedBlendPoses, _weightedBlendWeights, outputPose);
    }

    private void EvaluateSegment(BlendSpace2DSample start, BlendSpace2DSample end, SkeletonPoseLocal outputPose)
    {
        var segment = end.Position - start.Position;
        var segmentLengthSquared = segment.LengthSquared();
        if (segmentLengthSquared <= float.Epsilon)
        {
            start.Node.Evaluate(outputPose);
            return;
        }

        var projection = Vector2.Dot(Parameter - start.Position, segment) / segmentLengthSquared;
        var blendWeight = Math.Clamp(projection, 0f, 1f);
        start.Node.Evaluate(_samplePoseA);
        end.Node.Evaluate(_samplePoseB);
        AnimationPoseBlender.Blend(_samplePoseA, _samplePoseB, blendWeight, outputPose);
    }

    private void EvaluateNearestSample(SkeletonPoseLocal outputPose)
    {
        var nearestSampleIndex = 0;
        var bestDistanceSquared = Vector2.DistanceSquared(Parameter, _samples[0].Position);
        for (var sampleIndex = 1; sampleIndex < _samples.Length; sampleIndex++)
        {
            var distanceSquared = Vector2.DistanceSquared(Parameter, _samples[sampleIndex].Position);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestSampleIndex = sampleIndex;
            }
        }

        _samples[nearestSampleIndex].Node.Evaluate(outputPose);
    }

    private bool TryFindContainingTriangle(Vector2 point, out int sampleIndexA, out int sampleIndexB, out int sampleIndexC, out Vector3 weights)
    {
        const float epsilon = 0.0001f;

        for (var firstIndex = 0; firstIndex < _samples.Length - 2; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < _samples.Length - 1; secondIndex++)
            {
                for (var thirdIndex = secondIndex + 1; thirdIndex < _samples.Length; thirdIndex++)
                {
                    if (!TryComputeBarycentricCoordinates(
                            point,
                            _samples[firstIndex].Position,
                            _samples[secondIndex].Position,
                            _samples[thirdIndex].Position,
                            out weights))
                    {
                        continue;
                    }

                    if (weights.X >= -epsilon && weights.Y >= -epsilon && weights.Z >= -epsilon)
                    {
                        sampleIndexA = firstIndex;
                        sampleIndexB = secondIndex;
                        sampleIndexC = thirdIndex;
                        weights = ClampAndNormalize(weights);
                        return true;
                    }
                }
            }
        }

        sampleIndexA = -1;
        sampleIndexB = -1;
        sampleIndexC = -1;
        weights = Vector3.Zero;
        return false;
    }

    private bool TryFindClosestSegment(Vector2 point, out int sampleIndexA, out int sampleIndexB)
    {
        var foundSegment = false;
        var bestDistanceSquared = float.MaxValue;
        sampleIndexA = -1;
        sampleIndexB = -1;

        for (var firstIndex = 0; firstIndex < _samples.Length - 1; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < _samples.Length; secondIndex++)
            {
                var start = _samples[firstIndex].Position;
                var end = _samples[secondIndex].Position;
                var distanceSquared = DistanceSquaredToSegment(point, start, end);
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                sampleIndexA = firstIndex;
                sampleIndexB = secondIndex;
                foundSegment = true;
            }
        }

        return foundSegment;
    }

    private static bool TryComputeBarycentricCoordinates(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights)
    {
        var denominator = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (Math.Abs(denominator) <= float.Epsilon)
        {
            weights = Vector3.Zero;
            return false;
        }

        var weightA = (((b.Y - c.Y) * (point.X - c.X)) + ((c.X - b.X) * (point.Y - c.Y))) / denominator;
        var weightB = (((c.Y - a.Y) * (point.X - c.X)) + ((a.X - c.X) * (point.Y - c.Y))) / denominator;
        var weightC = 1f - weightA - weightB;
        weights = new Vector3(weightA, weightB, weightC);
        return true;
    }

    private static Vector3 ClampAndNormalize(Vector3 weights)
    {
        weights = Vector3.Max(weights, Vector3.Zero);
        var total = weights.X + weights.Y + weights.Z;
        if (total <= float.Epsilon)
        {
            return new Vector3(1f, 0f, 0f);
        }

        return weights / total;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var segmentLengthSquared = segment.LengthSquared();
        if (segmentLengthSquared <= float.Epsilon)
        {
            return Vector2.DistanceSquared(point, start);
        }

        var projection = Vector2.Dot(point - start, segment) / segmentLengthSquared;
        var clampedProjection = Math.Clamp(projection, 0f, 1f);
        var projectedPoint = start + segment * clampedProjection;
        return Vector2.DistanceSquared(point, projectedPoint);
    }

    private static void AdvanceNode(IAnimationGraphNode node, float elapsedSeconds)
    {
        if (node is IAnimationGraphRuntimeNode runtimeNode)
        {
            runtimeNode.Advance(elapsedSeconds);
        }
    }
}