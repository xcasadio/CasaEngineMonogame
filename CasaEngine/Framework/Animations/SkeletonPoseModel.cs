using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class SkeletonPoseModel
{
    private readonly Matrix[] _modelTransforms;
    private bool _hasValidTransforms;
    private uint _lastSourceVersion;

    public SkeletonPoseModel(SkeletonDefinition skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        _modelTransforms = new Matrix[skeleton.Count];
        ResetToBindPose();
        _hasValidTransforms = false;
        _lastSourceVersion = 0;
    }

    public SkeletonDefinition Skeleton { get; }

    public int Count => _modelTransforms.Length;

    public Matrix GetTransform(int index)
    {
        ValidateJointIndex(index);
        return _modelTransforms[index];
    }

    public Matrix GetSkinningTransform(int index)
    {
        ValidateJointIndex(index);
        return Skeleton.GetInverseBindMatrix(index) * _modelTransforms[index];
    }

    public void ResetToBindPose()
    {
        for (var index = 0; index < _modelTransforms.Length; index++)
        {
            var localMatrix = Skeleton.GetBindLocalTransform(index).ToMatrix();
            var parentIndex = Skeleton.GetJoint(index).ParentIndex;
            _modelTransforms[index] = parentIndex >= 0
                ? localMatrix * _modelTransforms[parentIndex]
                : localMatrix;
        }
    }

    public void UpdateFromLocalPose(SkeletonPoseLocal localPose)
    {
        ArgumentNullException.ThrowIfNull(localPose);

        if (!ReferenceEquals(localPose.Skeleton, Skeleton))
        {
            throw new ArgumentException("Cannot build a model pose from a local pose that targets another skeleton.", nameof(localPose));
        }

        if (_hasValidTransforms && localPose.Version == _lastSourceVersion)
        {
            return;
        }

        var startIndex = !_hasValidTransforms || !localPose.IsDirty
            ? 0
            : localPose.DirtyStartIndex;

        for (var index = startIndex; index < _modelTransforms.Length; index++)
        {
            var localMatrix = localPose.GetTransformMatrix(index);
            var parentIndex = Skeleton.GetJoint(index).ParentIndex;
            _modelTransforms[index] = parentIndex >= 0
                ? localMatrix * _modelTransforms[parentIndex]
                : localMatrix;
        }

        _hasValidTransforms = true;
        _lastSourceVersion = localPose.Version;
    }

    private void ValidateJointIndex(int index)
    {
        if ((uint)index >= (uint)_modelTransforms.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}