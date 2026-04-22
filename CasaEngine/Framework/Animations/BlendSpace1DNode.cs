namespace CasaEngine.Framework.Animations;

public readonly record struct BlendSpace1DSample(float Position, IAnimationGraphNode Node);

public sealed class BlendSpace1DNode : IAnimationGraphRuntimeNode
{
    private readonly BlendSpace1DSample[] _samples;
    private readonly SkeletonPoseLocal _sourcePose;
    private readonly SkeletonPoseLocal _targetPose;

    public BlendSpace1DNode(IReadOnlyList<BlendSpace1DSample> samples, float parameter = 0f)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("A 1D blend space needs at least one sample.", nameof(samples));
        }

        _samples = new BlendSpace1DSample[samples.Count];
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

        Array.Sort(_samples, static (left, right) => left.Position.CompareTo(right.Position));
        for (var sampleIndex = 1; sampleIndex < _samples.Length; sampleIndex++)
        {
            if (_samples[sampleIndex].Position < _samples[sampleIndex - 1].Position)
            {
                throw new ArgumentException("Blend space samples must be sorted by increasing position.", nameof(samples));
            }
        }

        Skeleton = _samples[0].Node.Skeleton;
        _sourcePose = Skeleton.CreateLocalBindPose();
        _targetPose = Skeleton.CreateLocalBindPose();
        Parameter = parameter;
    }

    public SkeletonDefinition Skeleton { get; }

    public float Parameter { get; set; }

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

        if (Parameter <= _samples[0].Position)
        {
            _samples[0].Node.Evaluate(outputPose);
            return;
        }

        var lastSample = _samples[^1];
        if (Parameter >= lastSample.Position)
        {
            lastSample.Node.Evaluate(outputPose);
            return;
        }

        for (var sampleIndex = 1; sampleIndex < _samples.Length; sampleIndex++)
        {
            var previousSample = _samples[sampleIndex - 1];
            var currentSample = _samples[sampleIndex];
            if (Parameter > currentSample.Position)
            {
                continue;
            }

            var range = currentSample.Position - previousSample.Position;
            if (range <= float.Epsilon)
            {
                currentSample.Node.Evaluate(outputPose);
                return;
            }

            previousSample.Node.Evaluate(_sourcePose);
            currentSample.Node.Evaluate(_targetPose);
            var blendWeight = Math.Clamp((Parameter - previousSample.Position) / range, 0f, 1f);
            AnimationPoseBlender.Blend(_sourcePose, _targetPose, blendWeight, outputPose);
            return;
        }

        lastSample.Node.Evaluate(outputPose);
    }

    private static void AdvanceNode(IAnimationGraphNode node, float elapsedSeconds)
    {
        if (node is IAnimationGraphRuntimeNode runtimeNode)
        {
            runtimeNode.Advance(elapsedSeconds);
        }
    }
}